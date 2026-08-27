using System;
using System.Diagnostics;
using StudyDocumentManager.Core.Interfaces;

namespace StudyDocumentManager.Services;

public sealed class TraceLog : ILog
{
    public void Information(string message) => Trace.TraceInformation(message);
    public void Warning(string message, Exception? exception = null)
        => Trace.TraceWarning(Format(message, exception));
    public void Error(string message, Exception? exception = null)
        => Trace.TraceError(Format(message, exception));

    private static string Format(string message, Exception? exception)
        => exception is null ? message : $"{message} {exception.GetType().Name}: {exception.Message}";
}
