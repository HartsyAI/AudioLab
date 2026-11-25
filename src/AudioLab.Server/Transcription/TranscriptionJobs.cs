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
    private readonly ISpeechModel _model;
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
    private long _sequence;

    /// <summary>Creates the table; the jobs on disk are read when first asked for, or when <see cref="WhenLoadedAsync"/> is awaited.</summary>
    public TranscriptionJobs(
        TranscriptionStore store,
        MediaStore media,
        IAudioWindowSource source,
        ISpeechModel model,
        TranscriptionOptions options,
        TimeProvider time,
        ILogger<TranscriptionJobs> logger)
    {
        _store = store;
        _media = media;
        _source = source;
        _model = model;
        _options = options;
        _time = time;
        _logger = logger;
        _loading = new Lazy<Task>(() => LoadAllAsync());
    }

    /// <summary>Completes when every job on disk is in the table.</summary>
    public Task WhenLoadedAsync() => _loading.Value;

    /// <summary>Starts a job for a media file, or returns the one already there (D90).</summary>
    /// <exception cref="TranscriptionLimitException">The user already has as many queued or running jobs as allowed.</exception>
    public async Task<StartOutcome> StartAsync(UserId user, MediaRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        await WhenLoadedAsync();
        double duration = _source.DurationOf(record);
        int chunks = TranscriptionWindows.CountOf(duration);
        JobKey key = new(user, record.Id);
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            if (_jobs.TryGetValue(key, out TranscriptionJob? existing))
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
            DateTimeOffset now = _time.GetUtcNow();
            TranscriptionJobFile file = NewFile(user, record, chunks, now);
            await _store.WriteStateAsync(user, record.Id, file, cancellationToken);
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
    /// <returns>False when there is nothing to run.</returns>
    /// <exception cref="OperationCanceledException">The server is stopping; the chunk in flight is abandoned.</exception>
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
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TranscriptionLog.StoreFailed(_logger, exception.GetType().Name);
            await _mutex.WaitAsync(CancellationToken.None);
            try
            {
                await SetStateAsync(job, TranscriptionState.Failed, "The store could not be written; resume to try again.", CancellationToken.None);
            }
            finally
            {
                _mutex.Release();
            }
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
            if (!IdRules.IsValid(file.Owner) || !IdRules.IsValid(file.Media))
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
            await _mutex.WaitAsync();
            try
            {
                if (_jobs.ContainsKey(new JobKey(owner, media)))
                {
                    continue;
                }
                TranscriptionJob job = new(owner, media) { Sequence = ++_sequence };
                job.File = file;
                if (file.State is TranscriptionState.Running or TranscriptionState.Paused)
                {
                    job.File = file with { State = TranscriptionState.Queued, Reason = null };
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
                await SetStateAsync(queued, TranscriptionState.Running, null, cancellationToken);
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
        MediaRecord? record = _media.Find(job.Owner, job.Media);
        if (record is null)
        {
            await RemoveAsync(job.Owner, job.Media, cancellationToken);
            return false;
        }
        TranscriptionWindow window = TranscriptionWindows.At(job.Done, job.Duration);
        ReadOnlyMemory<float> samples = await _source.ReadAsync(job.Owner, record, window, cancellationToken);
        IReadOnlyList<SpeechSegment> answer;
        try
        {
            answer = await AskModelAsync(window, samples, cancellationToken);
        }
        catch (SpeechModelException exception)
        {
            await FinishAsync(job, TranscriptionState.Failed, exception.Message, cancellationToken);
            return false;
        }
        IReadOnlyList<string> problems = TranscriptRules.AnswerProblems(answer, window.LengthSeconds);
        if (problems.Count > 0)
        {
            await FinishAsync(job, TranscriptionState.Failed, $"The model's answer is malformed: {problems[0]}", cancellationToken);
            return false;
        }
        ValueList<TranscriptSegment> owned = TranscriptRules.Own(window, answer, job.Duration);
        await _store.AppendChunkAsync(job.Owner, job.Media, new ChunkRecord(window.Index, owned), cancellationToken);
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            job.Done = window.Index + 1;
            job.Reached = window.OwnedEndSeconds;
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
        TranscriptionWindow window,
        ReadOnlyMemory<float> samples,
        CancellationToken cancellationToken)
    {
        int attempt = 0;
        while (true)
        {
            try
            {
                return await _model.TranscribeAsync(new SpeechWindow(window.StartSeconds, samples), cancellationToken);
            }
            catch (SpeechModelException exception) when (exception.Retryable && attempt < _options.Retries)
            {
                attempt++;
                TranscriptionLog.ModelRetried(_logger, attempt);
                await Task.Delay(_options.RetryDelay, _time, cancellationToken);
            }
        }
    }

    /// <summary>Reads the chunk log and checks the job's identity; a job whose identity has changed starts again from chunk 0 (D92).</summary>
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
            && file.Model == _model.ModelId
            && file.Chunks == chunks
            && file.WindowFrames == TranscriptionWindows.WindowFrames
            && file.StepFrames == TranscriptionWindows.StepFrames;
        IReadOnlyList<ChunkRecord> kept = sameJob
            ? await _store.ReadChunksAsync(job.Owner, job.Media, cancellationToken)
            : [];
        if (!sameJob)
        {
            _store.DeleteChunks(job.Owner, job.Media);
        }
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            if (!sameJob)
            {
                job.File = file with
                {
                    Sha256 = record.Sha256,
                    Model = _model.ModelId,
                    Chunks = chunks,
                    WindowFrames = TranscriptionWindows.WindowFrames,
                    StepFrames = TranscriptionWindows.StepFrames,
                };
            }
            job.Done = kept.Count;
            job.Reached = kept.Count == 0 ? 0 : TranscriptionWindows.At(kept.Count - 1, job.Duration).OwnedEndSeconds;
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
        await FinishAsync(job, TranscriptionState.Done, null, cancellationToken);
        _store.DeleteChunks(job.Owner, job.Media);
    }

    private async Task FinishAsync(TranscriptionJob job, TranscriptionState state, string? reason, CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            await SetStateAsync(job, state, reason, cancellationToken);
        }
        finally
        {
            _mutex.Release();
        }
    }

    // Writes the state file before the table changes, so a failed write leaves both as they were.
    private async Task SetStateAsync(TranscriptionJob job, TranscriptionState state, string? reason, CancellationToken cancellationToken)
    {
        TranscriptionJobFile file = job.File with { State = state, Reason = reason, Updated = _time.GetUtcNow() };
        await _store.WriteStateAsync(job.Owner, job.Media, file, cancellationToken);
        job.File = file;
        if (state is TranscriptionState.Cancelled or TranscriptionState.Failed or TranscriptionState.Done or TranscriptionState.Paused)
        {
            _turns.Remove(job);
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

    private TranscriptionJobFile NewFile(UserId user, MediaRecord record, int chunks, DateTimeOffset now)
    {
        return new TranscriptionJobFile(
            TranscriptionJobFile.CurrentVersion,
            user.Value,
            record.Id.Value,
            record.Sha256,
            _model.ModelId,
            TranscriptionWindows.WindowFrames,
            TranscriptionWindows.StepFrames,
            chunks,
            TranscriptionState.Queued,
            null,
            now,
            now);
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
            job.File.Reason);
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
