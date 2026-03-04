namespace mtanksl.OpenTibia.Common;

/// <summary>
/// Severity levels for server log entries.
/// </summary>
public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}

/// <summary>
/// Minimal server-side logger. Writes to the console; replace with a full
/// logging framework (e.g. Microsoft.Extensions.Logging) if needed.
/// </summary>
public static class Logger
{
    public static LogLevel MinimumLevel { get; set; } = LogLevel.Info;

    public static void Log(LogLevel level, string message)
    {
        if (level < MinimumLevel)
            return;

        string prefix = level switch
        {
            LogLevel.Debug   => "[DBG]",
            LogLevel.Info    => "[INF]",
            LogLevel.Warning => "[WRN]",
            LogLevel.Error   => "[ERR]",
            _                => "[???]"
        };

        Console.WriteLine($"{DateTime.UtcNow:HH:mm:ss.fff} {prefix} {message}");
    }

    public static void Debug(string message)   => Log(LogLevel.Debug,   message);
    public static void Info(string message)    => Log(LogLevel.Info,    message);
    public static void Warning(string message) => Log(LogLevel.Warning, message);
    public static void Error(string message)   => Log(LogLevel.Error,   message);
}

/// <summary>
/// Carries a Tibia map co-ordinate (x, y, z).
/// </summary>
public readonly record struct Position(ushort X, ushort Y, byte Z)
{
    public override string ToString() => $"({X}, {Y}, {Z})";
}

/// <summary>
/// A value object that wraps a task-based continuation.
/// Used by the game engine to chain async operations cleanly.
/// </summary>
public class Promise
{
    private readonly Task _task;

    private Promise(Task task) { _task = task; }

    public static Promise Completed { get; } = new Promise(Task.CompletedTask);

    public static Promise FromTask(Task task) => new Promise(task);

    public static Promise Run(Func<Task> action) => new Promise(action());

    public Task AsTask() => _task;
}
