namespace Sigil.Domain.Extensions;

public static class TimeMath
{
    // Stryker disable once Equality: equivalent mutation: when a == b both branches return the same value
    public static DateTime Earlier(DateTime a, DateTime b) => a < b ? a : b;
    // Stryker disable once Equality: equivalent mutation: when a == b both branches return the same value
    public static DateTime Later(DateTime a, DateTime b) => a > b ? a : b;
}