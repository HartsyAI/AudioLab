namespace AudioLab.Server.Store;

/// <summary>Tells the kinds of file error apart.</summary>
public static class StoreIoErrors
{
    private const int WindowsDiskFull = 112;
    private const int WindowsHandleDiskFull = 39;
    private const int UnixNoSpace = 28;

    /// <summary>Whether <paramref name="exception"/> says the disk has no room left, on Windows or on Unix.</summary>
    public static bool IsDiskFull(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        int code = exception.HResult;
        return code == UnixNoSpace || (code & 0xFFFF) is WindowsDiskFull or WindowsHandleDiskFull;
    }
}
