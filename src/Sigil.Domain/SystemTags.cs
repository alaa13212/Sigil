namespace Sigil.Domain;

public static class SystemTags
{
    public const string Prefix      = "sigil.";
    public const string Regression  = Prefix + "regression";
    public const string Reopened    = Prefix + "reopened";
    public const string HighVolume  = Prefix + "high-volume";

    public static bool IsSystemTag(string key) =>
        key.StartsWith(Prefix, StringComparison.Ordinal);
}
