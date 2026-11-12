using System;
using System.Collections.Generic;
using StardewModdingAPI;

namespace Waterlife;

internal static class Log
{
    private static IMonitor? monitor;
    private static readonly HashSet<string> loggedOnce = new();

    internal static void Initialize(IMonitor modMonitor)
    {
        monitor = modMonitor;
    }

    internal static void Trace(string message)
    {
        monitor?.Log(message, LogLevel.Trace);
    }

    internal static void Info(string message)
    {
        monitor?.Log(message, LogLevel.Info);
    }

    internal static void Warn(string message)
    {
        monitor?.Log(message, LogLevel.Warn);
    }

    internal static void Error(string message, Exception? exception = null)
    {
        monitor?.Log(exception is null ? message : $"{message}\n{exception}", LogLevel.Error);
    }

    /// <summary>Log an error only the first time a given key is used (for per-frame code paths).</summary>
    internal static void ErrorOnce(string key, string message, Exception? exception = null)
    {
        if (loggedOnce.Add(key))
            Error(message, exception);
    }

    /// <summary>Log a warning only the first time a given key is used.</summary>
    internal static void WarnOnce(string key, string message)
    {
        if (loggedOnce.Add(key))
            Warn(message);
    }
}
