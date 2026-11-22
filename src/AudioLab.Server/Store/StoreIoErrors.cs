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
        // A Windows error arrives as 0x8007 and the Win32 code; on Unix the raw number is an errno, where 39 and 112 mean something else.
        return OperatingSystem.IsWindows()
            ? (code & unchecked((int)0xFFFF0000)) == unchecked((int)0x80070000) && (code & 0xFFFF) is WindowsDiskFull or WindowsHandleDiskFull
            : code == UnixNoSpace;
    }
}
