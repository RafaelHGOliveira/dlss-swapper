using System;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace DLSS_Swapper;

public enum LoggingLevel : int
{
    Off = 0,
    Verbose = 10,
    Debug = 20,
    Info = 30,
    Warning = 40,
    Error = 50,
}

internal static class Logger
{
    static string? _logDirectory;
    public static string LogDirectory => _logDirectory ?? Path.GetTempPath();

#if DEBUG
    static LoggingLevelSwitch levelSwitch = new LoggingLevelSwitch(LogEventLevel.Verbose);
#else
    static LoggingLevelSwitch levelSwitch = new LoggingLevelSwitch(LogEventLevel.Fatal);
#endif

    public static bool IsVerboseEnabled => levelSwitch.MinimumLevel <= LogEventLevel.Verbose;

    internal static void Init(string logDirectory, LoggingLevel defaultLevel)
    {
        _logDirectory = logDirectory;
        if (Directory.Exists(logDirectory) == false)
            Directory.CreateDirectory(logDirectory);

        var loggingFile = Path.Combine(logDirectory, "dlss_swapper_.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(levelSwitch)
            .WriteTo.Debug(formatProvider: CultureInfo.InvariantCulture)
            .WriteTo.File(loggingFile, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7, formatProvider: CultureInfo.InvariantCulture)
            .CreateLogger();

        ChangeLoggingLevel(defaultLevel);
    }

    public static string GetCurrentLogPath()
    {
        var loggingFile = Path.Combine(LogDirectory, "dlss_swapper_.log");
        var withoutExtension = Path.GetFileNameWithoutExtension(loggingFile);
        var justExtension = Path.GetExtension(loggingFile);
        return Path.Combine(LogDirectory, $"{withoutExtension}{DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}{justExtension}");
    }

    public static void ChangeLoggingLevel(LoggingLevel loggingLevel)
    {
        levelSwitch.MinimumLevel = loggingLevel switch
        {
            LoggingLevel.Verbose => LogEventLevel.Verbose,
            LoggingLevel.Debug => LogEventLevel.Debug,
            LoggingLevel.Info => LogEventLevel.Information,
            LoggingLevel.Warning => LogEventLevel.Warning,
            LoggingLevel.Error => LogEventLevel.Error,
            _ => LogEventLevel.Fatal,
        };
    }


    public static void Verbose(string message, [CallerMemberName] string? memberName = null, [CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = 0)
    {
        Log.Verbose(FormatLine(message, memberName, sourceFilePath, sourceLineNumber));
    }

    public static void Debug(string message, [CallerMemberName] string? memberName = null, [CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = 0)
    {
        Log.Debug(FormatLine(message, memberName, sourceFilePath, sourceLineNumber));
    }

    public static void Info(string message, [CallerMemberName] string? memberName = null, [CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = 0)
    {
        Log.Information(FormatLine(message, memberName, sourceFilePath, sourceLineNumber));
    }

    public static void Warning(string message, [CallerMemberName] string? memberName = null, [CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = 0)
    {
        Log.Warning(FormatLine(message, memberName, sourceFilePath, sourceLineNumber));
    }

    public static void Error(string message, [CallerMemberName] string? memberName = null, [CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = 0)
    {
        Log.Error(FormatLine(message, memberName, sourceFilePath, sourceLineNumber));
    }

    public static void Error(Exception exception, string? message = null, [CallerMemberName] string? memberName = null, [CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = 0)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            Log.Error(FormatLine($"{exception}\n{exception.StackTrace}", memberName, sourceFilePath, sourceLineNumber));
        }
        else
        {
            Log.Error(FormatLine($"{message}\n{exception}\n{exception.StackTrace}", memberName, sourceFilePath, sourceLineNumber));
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static string FormatLine(string message, string? memberName, string? sourceFilePath, int sourceLineNumber)
    {
        if (memberName is null || sourceFilePath is null || sourceLineNumber == 0)
        {
            return message;
        }

        return $"{Path.GetFileName(sourceFilePath)}:{sourceLineNumber} {memberName} - {message}";
    }
}
