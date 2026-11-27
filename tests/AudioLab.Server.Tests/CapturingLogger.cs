using Microsoft.Extensions.Logging;

namespace AudioLab.Server.Tests;

/// <summary>A logger that keeps each message it is given, formatted, so a test can read what the server logged.</summary>
internal sealed class CapturingLogger<T>(List<string> messages) : ILogger<T>
{
    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull => NoScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        messages.Add(formatter(state, exception));
    }

    private sealed class NoScope : IDisposable
    {
        public static readonly NoScope Instance = new();

        public void Dispose()
        {
        }
    }
}
