namespace AudioLab.Server.Decoding;

/// <summary>Refuses decoder limits the server must not start with.</summary>
public static class DecoderOptionsValidator
{
    /// <summary>Adds a problem for each limit that is out of range.</summary>
    public static void Validate(DecoderOptions options, List<string> problems)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(problems);
        if (options.MaxInputBytes is < 1024 or > 512L * 1024 * 1024)
        {
            problems.Add("AudioLab:Decoder:MaxInputBytes must be 1024 to 536870912.");
        }
        if (options.Timeout < TimeSpan.FromSeconds(1) || options.Timeout > TimeSpan.FromHours(1))
        {
            problems.Add("AudioLab:Decoder:Timeout must be 1 second to 1 hour.");
        }
        if (options.InstallTimeout < TimeSpan.FromSeconds(1) || options.InstallTimeout > TimeSpan.FromHours(2))
        {
            problems.Add("AudioLab:Decoder:InstallTimeout must be 1 second to 2 hours.");
        }
        if (options.StderrCapBytes is < 1024 or > 1024 * 1024)
        {
            problems.Add("AudioLab:Decoder:StderrCapBytes must be 1024 to 1048576.");
        }
        if (options.MaxConcurrentPerUser < 1)
        {
            problems.Add("AudioLab:Decoder:MaxConcurrentPerUser must be at least 1.");
        }
        if (options.MaxConcurrent < options.MaxConcurrentPerUser)
        {
            problems.Add("AudioLab:Decoder:MaxConcurrent must be at least MaxConcurrentPerUser.");
        }
    }
}
