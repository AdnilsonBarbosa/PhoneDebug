namespace PhoneDebug.Core.Services;

/// <summary>Outcome of an action, with a message already written for a human.</summary>
public record OperationResult(bool Success, string Message)
{
    public static OperationResult Ok(string message) => new(true, message);

    public static OperationResult Fail(string message) => new(false, message);
}

public sealed record ScreenshotResult(bool Success, string Message, string? Path, long Bytes)
    : OperationResult(Success, Message);

/// <summary>Result of checking an APK before adb is asked to install it.</summary>
public sealed record ApkValidation(bool Success, string Message, string? FullPath)
    : OperationResult(Success, Message)
{
    public static ApkValidation Invalid(string message) => new(false, message, null);

    public static ApkValidation Valid(string fullPath) => new(true, fullPath, fullPath);
}
