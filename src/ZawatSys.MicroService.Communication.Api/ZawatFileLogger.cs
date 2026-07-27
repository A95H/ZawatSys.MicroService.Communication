// Persistent rolling-file logger (in addition to console/OTel).
// Self-contained ILoggerProvider so it captures ALL Microsoft.Extensions.Logging output.
// Declared in the Microsoft.Extensions.Logging namespace so builder.Logging.AddZawatFileLogger()
// resolves via ImplicitUsings without extra using directives.
using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.Logging;

public sealed class ZawatFileLoggerProvider : ILoggerProvider
{
    private readonly string _dir;
    private readonly long _maxBytes;
    private readonly BlockingCollection<string> _queue = new(new ConcurrentQueue<string>());
    private readonly Task _worker;
    private string _file = "";
    private long _size;

    public ZawatFileLoggerProvider(string dir, int maxFileSizeMb = 100)
    {
        _dir = dir;
        _maxBytes = maxFileSizeMb * 1024L * 1024L;
        Directory.CreateDirectory(_dir);
        _worker = Task.Run(ProcessQueue);
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, this);

    internal void Enqueue(string line)
    {
        if (!_queue.IsAddingCompleted) _queue.Add(line);
    }

    private void ProcessQueue()
    {
        foreach (var line in _queue.GetConsumingEnumerable())
        {
            try
            {
                Roll(line.Length + 1);
                File.AppendAllText(_file, line + Environment.NewLine);
                _size += line.Length + 1;
            }
            catch { /* never let logging crash the app */ }
        }
    }

    private void Roll(int incoming)
    {
        var daily = Path.Combine(_dir, $"log-{DateTime.UtcNow:yyyy-MM-dd}.log");
        if (_file != daily)
        {
            _file = daily;
            _size = File.Exists(_file) ? new FileInfo(_file).Length : 0;
        }
        if (_size + incoming > _maxBytes)
        {
            var rolled = Path.Combine(_dir, $"log-{DateTime.UtcNow:yyyy-MM-dd-HHmmss}.log");
            try { if (File.Exists(_file)) File.Move(_file, rolled); } catch { }
            _size = 0;
        }
    }

    public void Dispose()
    {
        _queue.CompleteAdding();
        try { _worker.Wait(TimeSpan.FromSeconds(5)); } catch { }
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
            provider.Enqueue(sb.ToString());
        }
    }
}

public static class ZawatFileLoggerExtensions
{
    /// <summary>Writes all logs to rolling files (default /app/logs, override via Logging:File:Directory).</summary>
    public static ILoggingBuilder AddZawatFileLogger(this ILoggingBuilder builder, IConfiguration configuration)
    {
        var dir = configuration["Logging:File:Directory"] ?? "/app/logs";
        var maxMb = int.TryParse(configuration["Logging:File:MaxFileSizeMb"], out var m) ? m : 100;
        builder.AddProvider(new ZawatFileLoggerProvider(dir, maxMb));
        return builder;
    }
}
