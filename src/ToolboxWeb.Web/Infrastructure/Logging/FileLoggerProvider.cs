using System.Collections.Concurrent;
using System.Text;

namespace ToolboxWeb.Web.Infrastructure.Logging;

/// <summary>
/// Options for <see cref="FileLoggerProvider"/>, bound from the <c>Logging:File</c> section.
/// </summary>
public sealed class FileLoggerOptions
{
    /// <summary>Folder the log files live in. Relative paths resolve against the content root.</summary>
    public string Directory { get; set; } = "logs";

    /// <summary>File name stem; the date and, when rolled, an index are appended.</summary>
    public string FileName { get; set; } = "toolboxweb";

    /// <summary>Rolls to a new file past this size so one long session cannot fill the disk.</summary>
    public int MaxFileSizeMb { get; set; } = 32;

    /// <summary>Deletes log files older than this many days on startup.</summary>
    public int RetainedDays { get; set; } = 7;
}

/// <summary>
/// Appends log entries to a dated text file.
/// <para>
/// The console provider is useless when the app runs under IIS Express from Visual Studio:
/// there is no console to read, so a long-running diagnosis such as a schema compare that
/// takes half an hour leaves nothing behind to look at afterwards. This writes the same
/// entries somewhere they can be read once the run is over.
/// </para>
/// </summary>
[ProviderAlias("File")]
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new(StringComparer.Ordinal);
    private readonly FileLoggerOptions _options;
    private readonly string _directory;
    private readonly long _maxBytes;
    private readonly Lock _gate = new();

    public FileLoggerProvider(FileLoggerOptions options, string contentRootPath)
    {
        _options = options;
        _directory = Path.IsPathRooted(options.Directory)
            ? options.Directory
            : Path.Combine(contentRootPath, options.Directory);
        _maxBytes = Math.Clamp(options.MaxFileSizeMb, 1, 1024) * 1024L * 1024L;

        System.IO.Directory.CreateDirectory(_directory);
        PurgeOldFiles();
    }

    /// <summary>Full path of the file currently being written, for diagnostics.</summary>
    public string Directory => _directory;

    public ILogger CreateLogger(string categoryName) =>
        _loggers.GetOrAdd(categoryName, name => new FileLogger(this, name));

    internal void Write(string categoryName, LogLevel level, EventId eventId, string message, Exception? exception)
    {
        var builder = new StringBuilder();
        builder.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
        builder.Append(" [").Append(Abbreviate(level)).Append("] ");
        builder.Append(categoryName);

        if (eventId.Id != 0)
        {
            builder.Append('[').Append(eventId.Id).Append(']');
        }

        builder.Append(" — ").Append(message);

        if (exception is not null)
        {
            builder.AppendLine();
            builder.Append(exception);
        }

        Append(builder.ToString());
    }

    /// <summary>
    /// Opens, appends and closes on every entry rather than holding a writer open.
    /// <para>
    /// Holding the handle would leave the file locked against <c>File.ReadAllText</c> and
    /// most editors, and the whole point of this log is being read while the run it describes
    /// is still going. The volume here is a few hundred lines per comparison, so the extra
    /// opens cost nothing worth measuring.
    /// </para>
    /// </summary>
    private void Append(string line)
    {
        lock (_gate)
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    using var stream = new FileStream(
                        ResolvePath(), FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                    using var writer = new StreamWriter(
                        stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                    writer.WriteLine(line);
                    return;
                }
                catch (IOException)
                {
                    // Something else has it open exclusively for a moment; a logger must never
                    // take the application down over that.
                    Thread.Sleep(20);
                }
                catch (UnauthorizedAccessException)
                {
                    return;
                }
            }
        }
    }

    private string ResolvePath()
    {
        var stem = Path.Combine(_directory, $"{_options.FileName}-{DateTime.Now:yyyyMMdd}");

        for (var index = 0; index < 1000; index++)
        {
            var candidate = index == 0 ? $"{stem}.log" : $"{stem}.{index}.log";
            var info = new FileInfo(candidate);
            if (!info.Exists || info.Length < _maxBytes)
            {
                return candidate;
            }
        }

        return $"{stem}.overflow.log";
    }

    private void PurgeOldFiles()
    {
        var retain = Math.Clamp(_options.RetainedDays, 1, 365);
        var cutoff = DateTime.Now.AddDays(-retain);

        try
        {
            foreach (var file in System.IO.Directory.EnumerateFiles(_directory, $"{_options.FileName}-*.log"))
            {
                if (File.GetLastWriteTime(file) < cutoff)
                {
                    File.Delete(file);
                }
            }
        }
        catch (IOException)
        {
            // Housekeeping must never stop the application from starting.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string Abbreviate(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRC",
        LogLevel.Debug => "DBG",
        LogLevel.Information => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error => "ERR",
        LogLevel.Critical => "CRT",
        _ => "???"
    };

    public void Dispose()
    {
        // Nothing is held open between entries, so there is no handle to release.
        _loggers.Clear();
    }

    private sealed class FileLogger : ILogger
    {
        private readonly FileLoggerProvider _provider;
        private readonly string _categoryName;

        public FileLogger(FileLoggerProvider provider, string categoryName)
        {
            _provider = provider;
            _categoryName = categoryName;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            _provider.Write(_categoryName, logLevel, eventId, formatter(state, exception), exception);
        }
    }
}

public static class FileLoggerExtensions
{
    /// <summary>
    /// Adds the file provider using the <c>Logging:File</c> configuration section. Level
    /// filtering goes through the normal <c>Logging:File:LogLevel</c> keys.
    /// </summary>
    public static ILoggingBuilder AddFile(this ILoggingBuilder builder, IConfiguration configuration, string contentRootPath)
    {
        var options = new FileLoggerOptions();
        configuration.GetSection("Logging:File").Bind(options);
        builder.AddProvider(new FileLoggerProvider(options, contentRootPath));
        return builder;
    }
}
