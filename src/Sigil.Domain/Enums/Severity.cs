namespace Sigil.Domain.Enums;

public enum Severity
{
    Fatal,
    Error,
    Warning,
    Info,
    Debug,
}

public static class SeverityHelper
{
    public static Severity Parse(string? name)
    {
        return name?.ToLowerInvariant() switch
        {
            null => Severity.Error,
            
            "fatal" => Severity.Fatal,
            "error" => Severity.Error,
            "warning" => Severity.Warning,
            "info" => Severity.Info,
            "debug" => Severity.Debug,
            _ => throw new ArgumentOutOfRangeException(nameof(name), name)
        };
    }
    
    public static string ToStringValue(this Severity severity) => severity switch
    {
        Severity.Fatal => "fatal",
        Severity.Error => "error",
        Severity.Warning => "warning",
        Severity.Info => "info",
        Severity.Debug => "debug",
        _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, null)
    };
    
    

    public static bool IsMoreSevereThan(this Severity thisLevel, Severity otherLevel)
    {
        return thisLevel < otherLevel;
    }
}