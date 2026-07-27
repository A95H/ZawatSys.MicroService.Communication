// Persistent rolling-file logger (in addition to console/OTel).
// Self-contained ILoggerProvider so it captures ALL Microsoft.Extensions.Logging output.
// Writes every entry to log-<date>.log, and Error+ entries also to errors-<date>.log.
// Declared in the Microsoft.Extensions.Logging namespace so builder.Logging.AddZawatFileLogger()
// resolves via ImplicitUsings without extra using directives.
using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.Logging;

public sealed class ZawatFileLoggerProvider : ILoggerProvider
{
    private readonly LogLevel _errorLevel;
    private readonly BlockingCollection<(string Line, bool IsError)> _queue = new(new ConcurrentQueue<(string, bool)>());
    private readonly Task _worker;
    private readonly RollingFile _all;
    private readonly RollingFile _errors;

    public ZawatFileLoggerProvider(string dir, int maxFileSizeMb = 100, LogLevel errorLevel = LogLevel.Error)
    {
        Directory.CreateDirectory(dir);
        _errorLevel = errorLevel;
        _all = new RollingFile(dir, "log", maxFileSizeMb);
        _errors = new RollingFile(dir, "errors", maxFileSizeMb);
        _worker = Task.Run(ProcessQueue);
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, this);

    internal LogLevel ErrorLevel => _errorLevel;

    internal void Enqueue(string line, bool isError)
    {
        if (!_queue.IsAddingCompleted) _queue.Add((line, isError));
    }

    private void ProcessQueue()
    {
        foreach (var (line, isError) in _queue.GetConsumingEnumerable())
        {
            try
            {
                _all.Append(line);
                if (isError) _errors.Append(line);
            }
            catch { /* never let logging crash the app */ }
        }
    }

    public void Dispose()
    {
        _queue.CompleteAdding();
        try { _worker.Wait(TimeSpan.FromSeconds(5)); } catch { }
    }

    // One rolling target: daily file with size-based rotation.
    private sealed class RollingFile(string dir, string prefix, int maxFileSizeMb)
    {
        private readonly long _maxBytes = maxFileSizeMb * 1024L * 1024L;
        private string _file = "";
        private long _size;

        public void Append(string line)
        {
            var daily = Path.Combine(dir, $"{prefix}-{DateTime.UtcNow:yyyy-MM-dd}.log");
            if (_file != daily)
            {
                _file = daily;
                _size = File.Exists(_file) ? new FileInfo(_file).Length : 0;
            }
            var bytes = line.Length + Environment.NewLine.Length;
            if (_size + bytes > _maxBytes)
            {
                var rolled = Path.Combine(dir, $"{prefix}-{DateTime.UtcNow:yyyy-MM-dd-HHmmss}.log");
                try { if (File.Exists(_file)) File.Move(_file, rolled); } catch { }
                _size = 0;
            }
            File.AppendAllText(_file, line + Environment.NewLine);
            _size += bytes;
        }
    }

    private sealed class FileLogger(string category, ZawatFileLoggerProvider provider) : ILogger
    {
        private sealed class NullScope : IDisposable { public static readonly NullScope Instance = new(); public void Dispose() { } }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            var sb = new StringBuilder()
                .Append(DateTime.UtcNow.ToString("O")).Append(" [").Append(logLevel).Append("] ")
                .Append(category).Append(" - ").Append(formatter(state, exception));
            if (exception is not null) sb.Append(Environment.NewLine).Append(exception);
            provider.Enqueue(sb.ToString(), logLevel >= provider.ErrorLevel);
        }
    }
}

public static class ZawatFileLoggerExtensions
{
    /// <summary>
    /// Writes all logs to rolling files (default /app/logs, override via Logging:File:Directory).
    /// Error+ entries are additionally written to errors-&lt;date&gt;.log (threshold via Logging:File:ErrorLevel).
    /// </summary>
    public static ILoggingBuilder AddZawatFileLogger(this ILoggingBuilder builder, IConfiguration configuration)
    {
        var dir = configuration["Logging:File:Directory"] ?? "/app/logs";
        var maxMb = int.TryParse(configuration["Logging:File:MaxFileSizeMb"], out var m) ? m : 100;
        var errorLevel = Enum.TryParse<LogLevel>(configuration["Logging:File:ErrorLevel"], ignoreCase: true, out var lvl)
            ? lvl : LogLevel.Error;
        builder.AddProvider(new ZawatFileLoggerProvider(dir, maxMb, errorLevel));
        return builder;
    }
}
