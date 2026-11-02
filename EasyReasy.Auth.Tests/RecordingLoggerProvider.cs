using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// An <see cref="ILoggerProvider"/> that keeps every entry written through its loggers, so a test can assert
    /// what the library logged, under which category and at which level.
    /// </summary>
    internal sealed class RecordingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<Entry> _entries = new ConcurrentQueue<Entry>();

        public IReadOnlyList<Entry> Entries => _entries.ToArray();

        public ILogger CreateLogger(string categoryName) => new RecordingLogger(categoryName, _entries);

        public void Dispose()
        {
        }

        /// <summary>
        /// One logged entry, with its message formatted.
        /// </summary>
        internal sealed class Entry
        {
            public required string Category { get; init; }
            public required LogLevel Level { get; init; }
            public required string Message { get; init; }
        }

        private sealed class RecordingLogger : ILogger
        {
            private readonly string _category;
            private readonly ConcurrentQueue<Entry> _entries;

            public RecordingLogger(string category, ConcurrentQueue<Entry> entries)
            {
                _category = category;
                _entries = entries;
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                _entries.Enqueue(new Entry { Category = _category, Level = logLevel, Message = formatter(state, exception) });
            }
        }
    }
}
