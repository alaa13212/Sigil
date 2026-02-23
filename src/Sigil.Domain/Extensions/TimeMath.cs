namespace Sigil.Domain.Extensions;

public static class TimeMath
{
    public static DateTime Earlier(DateTime a, DateTime b) => a < b ? a : b;
    public static DateTime Later(DateTime a, DateTime b) => a > b ? a : b;
}