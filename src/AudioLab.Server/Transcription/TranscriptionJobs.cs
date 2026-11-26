using System.Threading.Channels;
using AudioLab.Core.Model;
using AudioLab.Core.Transcription;
using AudioLab.Server.Media;

namespace AudioLab.Server.Transcription;

/// <summary>The table of transcription jobs and the turns that run them one chunk at a time (D90, D93, D95, D96).</summary>
/// <remarks>
/// One lock (<see cref="_mutex"/>) guards the table and the rotation. Model calls and file reads of a chunk run outside it, so a route
/// that asks for a status never waits for a model. Only <see cref="StepAsync"/> runs chunks, so there is one model call at a time.
/// </remarks>
public sealed class TranscriptionJobs : IMediaRemoval
{
    private readonly TranscriptionStore _store;
    private readonly MediaStore _media;
    private readonly IAudioWindowSource _source;
    private readonly ISpeechModels _models;
    private readonly TranscriptionOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger<TranscriptionJobs> _logger;
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private readonly Dictionary<JobKey, TranscriptionJob> _jobs = [];
    private readonly LinkedList<TranscriptionJob> _turns = new();
    private readonly Channel<bool> _wake = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    {
        FullMode = BoundedChannelFullMode.DropWrite,
        SingleReader = true,
    });
    private readonly Lazy<Task> _loading;
    private const string UnexpectedReason = "The transcription stopped on an unexpected error; resume to try again.";
    private const string TimedOut = "The speech model did not answer in time.";
    private const string BusyTimeout = "The speech model did not answer in time: it is busy with a call that has not finished.";
    private const string ModelNotInstalled = "The speech model is not installed.";
    private const string StateNotSaved = "The job state could not be saved; resume to try again.";
    private const string TranscriptTooLarge =
        "The transcript would be larger than the server stores, 64 MiB, so the job failed and no transcript was kept.";
    private long _sequence;
    private volatile bool _stopping;

    /// <summary>Creates the table; the jobs on disk are read when first asked for, or when <see cref="WhenLoadedAsync"/> is awaited.</summary>
    public TranscriptionJobs(
        TranscriptionStore store,
        MediaStore media,
        IAudioWindowSource source,
        ISpeechModels models,
        TranscriptionOptions options,
        TimeProvider time,
        ILogger<TranscriptionJobs> logger)
    {
        _store = store;
        _media = media;
        _source = source;
        _models = models;
        _options = options;
        _time = time;
        _logger = logger;
        _loading = new Lazy<Task>(() => LoadAllAsync());
    }

    /// <summary>Completes when every job on disk is in the table.</summary>
    public Task WhenLoadedAsync() => _loading.Value;

    /// <summary>Called when the server's stop begins: no retry starts after it (D93).</summary>
    public void BeginStop() => _stopping = true;

    /// <summary>Starts a job for a media file (D90), or answers with the job there (<c>created</c> false). A queued, running or paused job is
    /// always answered; a failed, cancelled or done job is answered under the model the user has chosen, and a done one only with its
    /// transcript present. Any other start makes a new job from chunk 0; if its state cannot be written, the job there is kept as it was.</summary>
    /// <exception cref="MediaNotFoundException">The file is gone or its delete has begun.</exception>
    /// <exception cref="TranscriptionLimitException">The user already has as many queued, paused or running jobs as allowed.</exception>
    public async Task<StartOutcome> StartAsync(UserId user, MediaRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        await WhenLoadedAsync();
        double duration = _source.DurationOf(record);
        int chunks = TranscriptionWindows.CountOf(duration);
        JobKey key = new(user, record.Id);
        string model = await _store.ReadSpeechModelAsync(user, cancellationToken) ?? SpeechModelCatalog.DefaultSpeechModel;
        await _media.PrepareAsync(user, cancellationToken);
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            // A file whose delete has begun is not found (D37), so no job is written for a file being removed (D90).
            if (_media.Find(user, record.Id) is null)
            {
                throw new MediaNotFoundException();
            }
            TranscriptionJob? existing = _jobs.TryGetValue(key, out TranscriptionJob? found) ? found : null;
            if (existing is not null && await AnswersStartAsync(existing, user, record, model, cancellationToken))
            {
                return new StartOutcome(StatusOf(existing), false);
            }
            int open = OpenCount(user);
            if (open >= _options.MaxQueuedPerUser)
            {
                throw new TranscriptionLimitException(
                    $"You already have {open} transcriptions queued or running; the most is {_options.MaxQueuedPerUser}. "
                    + "Try again when one has finished.");
            }
            // The chunk log of the job being replaced is parked, never read, until the new state is written; a failed write puts it back (D92).
            _store.ParkChunks(user, record.Id);
            DateTimeOffset now = _time.GetUtcNow();
            TranscriptionJobFile file = NewFile(user, record, chunks, duration, now, model);
            try
            {
                await _store.WriteStateAsync(user, record.Id, file, cancellationToken);
            }
            catch
            {
                _store.RestoreParkedChunks(user, record.Id);
                throw;
            }
            _jobs.Remove(key);
            _store.DeleteParkedChunks(user, record.Id);
            TranscriptionJob job = new(user, record.Id)
            {
                File = file,
                Sequence = ++_sequence,
                Duration = duration,
            };
            _jobs.Add(key, job);
            Wake();
            return new StartOutcome(StatusOf(job), true);
        }
        finally
        {
            _mutex.Release();
        }
    }

    /// <summary>The status of a media file's job, or null when there is none.</summary>
    public async Task<TranscriptionStatus?> StatusAsync(UserId user, MediaId id, CancellationToken cancellationToken)
    {
        await WhenLoadedAsync();
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            return _jobs.TryGetValue(new JobKey(user, id), out TranscriptionJob? job) ? StatusOf(job) : null;
        }
        finally
        {
            _mutex.Release();
        }
    }

    /// <summary>The state of a media file's job, or null when there is none.</summary>
    public async Task<TranscriptionState?> StateOfAsync(UserId user, MediaId id, CancellationToken cancellationToken)
    {
        return (await StatusAsync(user, id, cancellationToken))?.State;
    }

    /// <summary>Cancels a job: a queued or paused one at once, a running one at its next chunk boundary (D93).</summary>
    /// <exception cref="TranscriptionNotFoundException">The file has no job.</exception>
    /// <exception cref="TranscriptionConflictException">The job is finished or has failed.</exception>
    public async Task<TranscriptionStatus> CancelAsync(UserId user, MediaId id, CancellationToken cancellationToken)
    {
        await WhenLoadedAsync();
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            TranscriptionJob job = Find(user, id);
            switch (job.File.State)
            {
                case TranscriptionState.Queued:
                case TranscriptionState.Paused:
                    await SetStateAsync(job, TranscriptionState.Cancelled, null, cancellationToken);
                    break;
                case TranscriptionState.Running:
                    job.CancelRequested = true;
                    break;
                case TranscriptionState.Cancelled:
                    break;
                default:
                    throw new TranscriptionConflictException($"The transcription is {Describe(job.File.State)}; there is nothing to cancel.");
            }
            return StatusOf(job);
        }
        finally
        {
            _mutex.Release();
        }
    }

    /// <summary>Queues a cancelled, paused or failed job again; its checkpoints are kept, so it continues where it stopped (D92, D93).</summary>
    /// <exception cref="TranscriptionNotFoundException">The file has no job.</exception>
    /// <exception cref="TranscriptionConflictException">The job is queued, running or done.</exception>
    /// <exception cref="TranscriptionLimitException">The user already has as many queued or running jobs as allowed.</exception>
    public async Task<TranscriptionStatus> ResumeAsync(UserId user, MediaId id, CancellationToken cancellationToken)
    {
        await WhenLoadedAsync();
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            TranscriptionJob job = Find(user, id);
            if (job.File.State is TranscriptionState.Queued or TranscriptionState.Running or TranscriptionState.Done)
            {
                throw new TranscriptionConflictException($"The transcription is {Describe(job.File.State)}; it cannot be resumed.");
            }
            int open = OpenCount(user);
            if (open >= _options.MaxQueuedPerUser)
            {
                throw new TranscriptionLimitException(
                    $"You already have {open} transcriptions queued or running; the most is {_options.MaxQueuedPerUser}. "
                    + "Try again when one has finished.");
            }
            job.CancelRequested = false;
            await SetStateAsync(job, TranscriptionState.Queued, null, cancellationToken);
            Wake();
            return StatusOf(job);
        }
        finally
        {
            _mutex.Release();
        }
    }

    /// <summary>Runs the next turn: starts queued jobs that have a place, then takes one chunk of the next running job.</summary>
    /// <remarks>
    /// Any exception from the turn fails that job with a reason and does not leave it running (D93). Only the worker decides to stop:
    /// it passes a token that is not cancelled, so a chunk in flight always finishes.
    /// </remarks>
    /// <returns>False when there is nothing to run.</returns>
    public async Task<bool> StepAsync(CancellationToken cancellationToken)
    {
        await WhenLoadedAsync();
        TranscriptionJob? job = await TakeTurnAsync(cancellationToken);
        if (job is null)
        {
            return false;
        }
        bool stillRunning;
        try
        {
            stillRunning = await RunTurnAsync(job, cancellationToken);
        }
        catch (OperationCanceledException) when (_stopping || cancellationToken.IsCancellationRequested)
        {
            // The stop abandoned the chunk in flight. The job keeps its last complete chunk and is paused when the stop pauses the running jobs.
            return true;
        }
        catch (Exception exception)
        {
            TranscriptionLog.WorkerFailed(_logger, exception);
            await FinishAsync(job, TranscriptionState.Failed, UnexpectedReason, CancellationToken.None);
            return true;
        }
        if (stillRunning)
        {
            await _mutex.WaitAsync(cancellationToken);
            try
            {
                _turns.AddLast(job);
            }
            finally
            {
                _mutex.Release();
            }
        }
        return true;
    }

    /// <summary>Waits until a job is queued or resumed.</summary>
    public Task WaitForWorkAsync(CancellationToken cancellationToken) => _wake.Reader.ReadAsync(cancellationToken).AsTask();

    /// <summary>At the server's stop: every running job is paused, so the next start queues it again (D90).</summary>
    public async Task PauseRunningAsync(CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            _turns.Clear();
            foreach (TranscriptionJob job in _jobs.Values.Where(job => job.File.State == TranscriptionState.Running))
            {
                await SetStateAsync(job, TranscriptionState.Paused, null, cancellationToken);
            }
        }
        finally
        {
            _mutex.Release();
        }
    }

    /// <inheritdoc />
    public async Task EnsureRemovableAsync(UserId user, MediaId id, CancellationToken cancellationToken)
    {
        await WhenLoadedAsync();
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            if (_jobs.TryGetValue(new JobKey(user, id), out TranscriptionJob? job)
                && job.File.State is TranscriptionState.Queued or TranscriptionState.Running or TranscriptionState.Paused)
            {
                throw new MediaTranscriptionBusyException("A transcription of this file is queued or running; cancel it before deleting the file.");
            }
        }
        finally
        {
            _mutex.Release();
        }
    }

    /// <inheritdoc />
    public async Task RemoveAsync(UserId user, MediaId id, CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            _jobs.Remove(new JobKey(user, id));
            _store.DeleteAll(user, id);
        }
        finally
        {
            _mutex.Release();
        }
    }

    private async Task LoadAllAsync()
    {
        _store.SweepTemporaryFiles();
        foreach ((string path, TranscriptionJobFile file) in _store.ReadAllStates())
        {
            if (!IdRules.IsValid(file.Owner) || !IdRules.IsValid(file.Media) || file.Version != TranscriptionJobFile.CurrentVersion)
            {
                TranscriptionLog.StateSkipped(_logger);
                continue;
            }
            UserId owner = UserId.From(file.Owner);
            MediaId media = MediaId.From(file.Media);
            if (!string.Equals(_store.Paths.TranscriptJobFile(owner, media), path, StringComparison.OrdinalIgnoreCase))
            {
                TranscriptionLog.StateSkipped(_logger);
                continue;
            }
            IReadOnlyList<ChunkRecord> kept = file.State == TranscriptionState.Done ? [] : await ReadProgressAsync(owner, media);
            await _mutex.WaitAsync();
            try
            {
                if (_jobs.ContainsKey(new JobKey(owner, media)))
                {
                    continue;
                }
                TranscriptionJob job = new(owner, media) { Sequence = ++_sequence, Duration = file.Duration };
                job.File = file;
                if (file.State is TranscriptionState.Running or TranscriptionState.Paused)
                {
                    job.File = file with { State = TranscriptionState.Queued, Reason = null };
                }
                if (file.State == TranscriptionState.Done)
                {
                    job.Done = file.Chunks;
                    job.Reached = file.Duration;
                }
                else
                {
                    RestoreProgress(job, kept);
                }
                _jobs.Add(new JobKey(owner, media), job);
            }
            finally
            {
                _mutex.Release();
            }
        }
        Wake();
    }

    /// <summary>Sets a job's progress from its chunk log, so a failed, cancelled or paused job shows how far it got (D92).</summary>
    private static void RestoreProgress(TranscriptionJob job, IReadOnlyList<ChunkRecord> kept)
    {
        job.Done = kept.Count;
        job.Segments = kept.Sum(chunk => chunk.Segments.Count);
        job.Bytes = kept.Sum(TranscriptionStore.ChunkBytes);
        job.Stripped = kept.Sum(chunk => chunk.Stripped);
        job.Reached = kept.Count == 0 ? 0 : TranscriptionWindows.At(kept.Count - 1, job.Duration).OwnedEndSeconds;
    }

    private async Task<TranscriptionJob?> TakeTurnAsync(CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            foreach (TranscriptionJob queued in _jobs.Values.Where(job => job.File.State == TranscriptionState.Queued).OrderBy(job => job.Sequence))
            {
                if (_turns.Count >= _options.MaxRunning)
                {
                    break;
                }
                if (_turns.Count(running => running.Owner == queued.Owner) >= _options.MaxRunningPerUser)
                {
                    continue;
                }
                try
                {
                    await SetStateAsync(queued, TranscriptionState.Running, null, cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    // Any failure of the write fails this job, so one job's state file never stops the others. The file still says queued.
                    TranscriptionLog.StoreFailed(_logger, exception.GetType().Name);
                    FailInMemory(queued);
                    continue;
                }
                _turns.AddLast(queued);
            }
            LinkedListNode<TranscriptionJob>? first = _turns.First;
            if (first is null)
            {
                return null;
            }
            _turns.RemoveFirst();
            return first.Value;
        }
        finally
        {
            _mutex.Release();
        }
    }

    /// <returns>True when the job still has chunks to run.</returns>
    private async Task<bool> RunTurnAsync(TranscriptionJob job, CancellationToken cancellationToken)
    {
        if (!job.Prepared && !await PrepareAsync(job, cancellationToken))
        {
            return false;
        }
        if (job.CancelRequested)
        {
            await FinishAsync(job, TranscriptionState.Cancelled, null, cancellationToken);
            return false;
        }
        if (job.Done >= job.File.Chunks)
        {
            await CompleteAsync(job, cancellationToken);
            return false;
        }
        ISpeechModel? model = _models.Find(job.File.Model);
        if (model is null)
        {
            await FinishAsync(job, TranscriptionState.Failed, ModelNotInstalled, cancellationToken);
            return false;
        }
        MediaRecord? record = _media.Find(job.Owner, job.Media);
        if (record is null)
        {
            await RemoveAsync(job.Owner, job.Media, cancellationToken);
            return false;
        }
        TranscriptionWindow window = TranscriptionWindows.At(job.Done, job.Duration);
        ReadOnlyMemory<float> samples;
        try
        {
            samples = await _source.ReadAsync(job.Owner, record, window, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            TranscriptionLog.WorkerFailed(_logger, exception);
            await FinishAsync(job, TranscriptionState.Failed, "The media file could not be read; resume to try again.", cancellationToken);
            return false;
        }
        IReadOnlyList<SpeechSegment> answer;
        try
        {
            answer = await AskModelAsync(model, window, samples, cancellationToken);
        }
        catch (SpeechModelException exception)
        {
            await FinishAsync(job, TranscriptionState.Failed, exception.Message, cancellationToken);
            return false;
        }
        if (!TranscriptRules.TryOwn(window, answer, job.Duration, out ValueList<TranscriptSegment> owned, out string problem, out int stripped))
        {
            await FinishAsync(job, TranscriptionState.Failed, $"The model's answer is malformed: {problem}", cancellationToken);
            return false;
        }
        ChunkRecord chunk = new(window.Index, owned, stripped);
        long bytes = TranscriptionStore.ChunkBytes(chunk);
        if (job.Bytes + bytes > TranscriptionLimits.MaxTranscriptBytes - TranscriptionLimits.TranscriptOverheadBytes)
        {
            await FinishAsync(job, TranscriptionState.Failed, TranscriptTooLarge, cancellationToken);
            return false;
        }
        if (job.Segments + owned.Count > TranscriptionLimits.MaxSegments)
        {
            await FinishAsync(
                job,
                TranscriptionState.Failed,
                $"The transcript would hold more than {TranscriptionLimits.MaxSegments} segments, which is the most one transcript can hold.",
                cancellationToken);
            return false;
        }
        try
        {
            await _store.AppendChunkAsync(job.Owner, job.Media, chunk, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TranscriptionLog.StoreFailed(_logger, exception.GetType().Name);
            await FinishAsync(job, TranscriptionState.Failed, "The store could not be written; resume to try again.", cancellationToken);
            return false;
        }
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            job.Done = window.Index + 1;
            job.Reached = window.OwnedEndSeconds;
            job.Segments += owned.Count;
            job.Bytes += bytes;
            job.Stripped += stripped;
            if (job.CancelRequested)
            {
                await SetStateAsync(job, TranscriptionState.Cancelled, null, cancellationToken);
                return false;
            }
            return true;
        }
        finally
        {
            _mutex.Release();
        }
    }

    private async Task<IReadOnlyList<SpeechSegment>> AskModelAsync(
        ISpeechModel model,
        TranscriptionWindow window,
        ReadOnlyMemory<float> samples,
        CancellationToken cancellationToken)
    {
        int attempt = 0;
        while (true)
        {
            try
            {
                return await AskOnceAsync(model, window, samples, cancellationToken);
            }
            catch (SpeechModelException exception) when (exception.Retryable && attempt < _options.Retries)
            {
                if (_stopping)
                {
                    // A stop has begun: no retry starts, and the job is paused rather than failed (D93).
                    throw new OperationCanceledException("The transcription is paused for the server's stop.", exception);
                }
                attempt++;
                TranscriptionLog.ModelRetried(_logger, attempt);
                await Task.Delay(_options.RetryDelay, _time, cancellationToken);
                if (_stopping)
                {
                    throw new OperationCanceledException("The transcription is paused for the server's stop.");
                }
            }
        }
    }

    /// <summary>One model call, bounded by <see cref="TranscriptionOptions.ModelTimeout"/>; a call past the bound is abandoned (D93).</summary>
    private async Task<IReadOnlyList<SpeechSegment>> AskOnceAsync(
        ISpeechModel model,
        TranscriptionWindow window,
        ReadOnlyMemory<float> samples,
        CancellationToken cancellationToken)
    {
        // A call the model gave up on still holds it, so no new call is made until it is free; a retry waits and asks again (D93).
        if (model.IsBusy)
        {
            throw new SpeechModelException(BusyTimeout, retryable: true);
        }
        using CancellationTokenSource call = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        // The model is given only the audio the window owns (D91): the overlap would be transcribed twice.
        ReadOnlyMemory<float> owned = window.IsLast
            ? samples
            : samples[..(int)Math.Min(samples.Length, TranscriptionWindows.StepFrames)];
        Task<IReadOnlyList<SpeechSegment>> answer = model.TranscribeAsync(new SpeechWindow(window.StartSeconds, owned), call.Token);
        Task limit = Task.Delay(_options.ModelTimeout, _time, call.Token);
        Task finished = await Task.WhenAny(answer, limit);
        if (finished == answer)
        {
            call.Cancel();
            return await answer;
        }
        call.Cancel();
        cancellationToken.ThrowIfCancellationRequested();
        throw new SpeechModelException(model.IsBusy ? BusyTimeout : TimedOut, retryable: true);
    }

    /// <summary>Reads the chunk log and checks the job's identity; a job whose identity has changed starts again from chunk 0 (D92). The model
    /// is the one the job started with, so a change of the user's choice does not reach a job that is running.</summary>
    /// <returns>False when the media file is gone and the job was removed.</returns>
    private async Task<bool> PrepareAsync(TranscriptionJob job, CancellationToken cancellationToken)
    {
        await _media.PrepareAsync(job.Owner, cancellationToken);
        MediaRecord? record = _media.Find(job.Owner, job.Media);
        if (record is null)
        {
            await RemoveAsync(job.Owner, job.Media, cancellationToken);
            return false;
        }
        job.Duration = _source.DurationOf(record);
        int chunks = TranscriptionWindows.CountOf(job.Duration);
        TranscriptionJobFile file = job.File;
        bool sameJob = file.Sha256 == record.Sha256
            && file.Chunks == chunks
            && file.WindowFrames == TranscriptionWindows.WindowFrames
            && file.StepFrames == TranscriptionWindows.StepFrames;
        IReadOnlyList<ChunkRecord> kept = [];
        if (sameJob)
        {
            kept = await _store.ReadChunksAsync(job.Owner, job.Media, cancellationToken);
        }
        else
        {
            // The checkpoints go first, then the new identity is written: a crash between the two leaves the old identity with no
            // checkpoints, and the next start writes the new one again (D92).
            _store.DeleteChunks(job.Owner, job.Media);
            file = file with
            {
                Sha256 = record.Sha256,
                Chunks = chunks,
                WindowFrames = TranscriptionWindows.WindowFrames,
                StepFrames = TranscriptionWindows.StepFrames,
                Duration = job.Duration,
                Updated = _time.GetUtcNow(),
            };
            await _store.WriteStateAsync(job.Owner, job.Media, file, cancellationToken);
        }
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            job.File = file;
            job.Done = kept.Count;
            RestoreProgress(job, kept);
            job.Prepared = true;
            if (kept.Count > 0)
            {
                TranscriptionLog.Resumed(_logger, job.Done, chunks);
            }
        }
        finally
        {
            _mutex.Release();
        }
        return true;
    }

    private async Task CompleteAsync(TranscriptionJob job, CancellationToken cancellationToken)
    {
        IReadOnlyList<ChunkRecord> chunks = await _store.ReadChunksAsync(job.Owner, job.Media, cancellationToken);
        if (chunks.Count != job.File.Chunks)
        {
            await FinishAsync(job, TranscriptionState.Failed, "The chunk log is incomplete; resume to run it again.", cancellationToken);
            return;
        }
        TranscriptDocument document = new(
            TranscriptDocument.CurrentVersion,
            job.Media.Value,
            job.File.Sha256,
            job.File.Model,
            job.Duration,
            new ValueList<TranscriptSegment>(chunks.SelectMany(chunk => chunk.Segments)));
        try
        {
            await _store.WriteTranscriptAsync(job.Owner, job.Media, document, cancellationToken);
        }
        catch (TranscriptRefusedException exception)
        {
            await FinishAsync(job, TranscriptionState.Failed, exception.Message, cancellationToken);
            return;
        }
        // The checkpoints go only once the done state is written: if it is not, a resume finishes from them with no model call (D92).
        if (await FinishAsync(job, TranscriptionState.Done, null, cancellationToken))
        {
            _store.DeleteChunks(job.Owner, job.Media);
        }
    }

    /// <returns>True when the state was written; false when the job was failed in the table instead.</returns>
    private async Task<bool> FinishAsync(TranscriptionJob job, TranscriptionState state, string? reason, CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            await SetStateAsync(job, state, reason, cancellationToken);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The state file could not be written, for whatever reason. The table still leaves the running state and the job gives up its turn,
            // so it is never
            // held with no turn. The file keeps what it said, so a restart queues the job again from its checkpoints (D93).
            TranscriptionLog.StoreFailed(_logger, exception.GetType().Name);
            FailInMemory(job);
            return false;
        }
        finally
        {
            _mutex.Release();
        }
    }

    /// <summary>Fails a job in the table after its state file could not be written, with the reason that says so (D93).</summary>
    private void FailInMemory(TranscriptionJob job)
    {
        job.File = job.File with { State = TranscriptionState.Failed, Reason = StateNotSaved, Updated = _time.GetUtcNow() };
        job.Prepared = false;
        _turns.Remove(job);
    }

    /// <summary>Whether a start answers with the job that is there (D90): a job that is queued, running or paused always; a failed or
    /// cancelled job, a done one, under the same model only, and a done one only with its transcript present.</summary>
    private async Task<bool> AnswersStartAsync(
        TranscriptionJob job,
        UserId user,
        MediaRecord record,
        string model,
        CancellationToken cancellationToken)
    {
        if (job.File.State is TranscriptionState.Queued or TranscriptionState.Running or TranscriptionState.Paused)
        {
            return true;
        }
        if (job.File.Model != model)
        {
            return false;
        }
        return job.File.State != TranscriptionState.Done || await HasUsableTranscriptAsync(user, record, cancellationToken);
    }

    /// <summary>The chunk log of a job, or none when it cannot be read; one unreadable file must not fault start-up for every route (D92).</summary>
    private async Task<IReadOnlyList<ChunkRecord>> ReadProgressAsync(UserId owner, MediaId media)
    {
        try
        {
            return await _store.ReadChunksAsync(owner, media, CancellationToken.None);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TranscriptionLog.StoreFailed(_logger, exception.GetType().Name);
            return [];
        }
    }

    // Writes the state file before the table changes, so a failed write leaves both as they were. A job that leaves the running state
    // must read its chunk log again before it runs, because a failed write may have left a torn line in it (D92).
    private async Task SetStateAsync(TranscriptionJob job, TranscriptionState state, string? reason, CancellationToken cancellationToken)
    {
        TranscriptionJobFile file = job.File with { State = state, Reason = reason, Updated = _time.GetUtcNow() };
        await _store.WriteStateAsync(job.Owner, job.Media, file, cancellationToken);
        job.File = file;
        if (state != TranscriptionState.Running)
        {
            job.Prepared = false;
        }
        if (state is TranscriptionState.Cancelled or TranscriptionState.Failed or TranscriptionState.Done or TranscriptionState.Paused)
        {
            _turns.Remove(job);
        }
    }

    private async Task<bool> HasUsableTranscriptAsync(UserId user, MediaRecord record, CancellationToken cancellationToken)
    {
        try
        {
            return await _store.ReadTranscriptForAsync(user, record, cancellationToken) is not null;
        }
        catch (TranscriptRefusedException)
        {
            return false;
        }
    }

    private TranscriptionJob Find(UserId user, MediaId id)
    {
        return _jobs.TryGetValue(new JobKey(user, id), out TranscriptionJob? job)
            ? job
            : throw new TranscriptionNotFoundException("There is no transcription of that file.");
    }

    private int OpenCount(UserId user)
    {
        return _jobs.Values.Count(job => job.Owner == user && job.File.State is (TranscriptionState.Queued or TranscriptionState.Running
            or TranscriptionState.Paused));
    }

    private void Wake() => _wake.Writer.TryWrite(true);

    private static TranscriptionJobFile NewFile(UserId user, MediaRecord record, int chunks, double duration, DateTimeOffset now, string model)
    {
        return new TranscriptionJobFile(
            TranscriptionJobFile.CurrentVersion,
            user.Value,
            record.Id.Value,
            record.Sha256,
            model,
            TranscriptionWindows.WindowFrames,
            TranscriptionWindows.StepFrames,
            chunks,
            TranscriptionState.Queued,
            null,
            now,
            now,
            duration);
    }

    private static TranscriptionStatus StatusOf(TranscriptionJob job)
    {
        return new TranscriptionStatus(
            job.Media.Value,
            job.File.State,
            job.Done,
            job.File.Chunks,
            job.Reached,
            job.CancelRequested,
            job.File.Reason,
            job.Stripped);
    }

    private static string Describe(TranscriptionState state) => state switch
    {
        TranscriptionState.Done => "done",
        TranscriptionState.Failed => "failed",
        TranscriptionState.Cancelled => "cancelled",
        TranscriptionState.Paused => "paused",
        TranscriptionState.Running => "running",
        _ => "queued",
    };

    private readonly record struct JobKey(UserId Owner, MediaId Media);
}
