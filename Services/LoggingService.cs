using System;
using Serilog;
using SystemException = System.Exception;

namespace Metraj.Services
{
    public static class LoggingService
    {
        private static readonly object _lock = new();
        private static bool _initialized = false;

        public static ILogger Logger { get; private set; }

        public static void Initialize(string logPath = null)
        {
            lock (_lock)
            {
                if (_initialized) return;

                logPath ??= System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Metraj", "Logs");

                var logFilePath = System.IO.Path.Combine(logPath, "metraj-.log");

                Logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .Enrich.FromLogContext()
                    .Enrich.WithProperty("Application", "Metraj")
                    .WriteTo.File(logFilePath,
                        rollingInterval: RollingInterval.Day,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}",
                        fileSizeLimitBytes: 10 * 1024 * 1024,
                        retainedFileCountLimit: 30,
                        rollOnFileSizeLimit: true,
                        shared: true)
                    .WriteTo.Debug(
                        outputTemplate: "{Timestamp:HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                    .CreateLogger();

                _initialized = true;
                Logger.Information("Logging service initialized. Log path: {LogPath}", logPath);
            }
        }

        public static void Close()
        {
            lock (_lock)
            {
                if (!_initialized) return;
                Log.CloseAndFlush();
                Logger = null;
                _initialized = false;
            }
        }

        public static void EnsureInitialized() { if (!_initialized) Initialize(); }

        public static void Debug(string message, params object[] args) { EnsureInitialized(); Logger.Debug(message, args); }
        public static void Info(string message, params object[] args) { EnsureInitialized(); Logger.Information(message, args); }
        public static void Warning(string message, SystemException ex = null, params object[] args)
        {
            EnsureInitialized();
            if (ex != null) Logger.Warning(ex, message, args); else Logger.Warning(message, args);
        }
        public static void Error(string message, SystemException ex = null, params object[] args)
        {
            EnsureInitialized();
            if (ex != null) Logger.Error(ex, message, args); else Logger.Error(message, args);
        }
        public static void Fatal(string message, SystemException ex = null, params object[] args)
        {
            EnsureInitialized();
            if (ex != null) Logger.Fatal(ex, message, args); else Logger.Fatal(message, args);
        }
    }
}
