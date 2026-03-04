using System;

namespace CTC
{
    /// <summary>
    /// Phase 7: cross-platform console logger.  All log messages are written to
    /// <see cref="Console.Error"/> so they are visible in the terminal even when
    /// stdout is redirected.  The <see cref="OnLogMessage"/> event is still fired
    /// for any in-process subscribers (e.g. future in-game overlays).
    /// </summary>
    public class Log
    {
        public static Log Instance;

        public enum Level
        {
            Fatal,
            Error,
            Warning,
            Notice,
            Debug
        }

        public class Message
        {
            public Level level;
            public string text;
            public object sender;
            public DateTime time;
        }

        static Log()
        {
            Instance = new Log();
        }

        public delegate void LogMessageHandler(object sender, Message message);
        public event LogMessageHandler OnLogMessage;

        private void Dispatch(object sender, Level level, string text)
        {
            Message m = new Message
            {
                text   = text,
                level  = level,
                sender = sender,
                time   = DateTime.Now,
            };

            // Phase 7: write to Console.Error for cross-platform terminal output.
            Console.Error.WriteLine($"[{level.ToString().ToUpperInvariant()}] {text}");

            OnLogMessage?.Invoke(sender, m);
        }

        public static void Debug(string message, object sender = null)
            => Instance.Dispatch(sender, Level.Debug, message);

        public static void Notice(string message, object sender = null)
            => Instance.Dispatch(sender, Level.Notice, message);

        public static void Warning(string message, object sender = null)
            => Instance.Dispatch(sender, Level.Warning, message);

        public static void Error(string message, object sender = null)
            => Instance.Dispatch(sender, Level.Error, message);

        public static void Fatal(string message, object sender = null)
            => Instance.Dispatch(sender, Level.Fatal, message);
    }
}
