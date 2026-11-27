using System.Reflection;
using Microsoft.AspNetCore.Components;
using Sigil.Domain.Enums;
using Sigil.Server.Client.Pages.Issues;
using Sigil.Server.Client.Services;

namespace Sigil.Server.Tests;

public class QueryParameterBindingTests
{
    private static readonly Type[] BindableTypes =
    [
        typeof(string),
        typeof(bool),
        typeof(int),
        typeof(long),
        typeof(float),
        typeof(double),
        typeof(decimal),
        typeof(Guid),
        typeof(DateTime),
        typeof(DateOnly),
        typeof(TimeOnly),
    ];

    [Fact]
    public void Query_parameters_use_types_the_router_can_bind()
    {
        var assembly = typeof(IssueList).Assembly;
        var offenders = new List<string>();
        var inspected = 0;

        foreach (var type in Types(assembly))
        {
            foreach (var property in type.GetProperties(
                         BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (property.GetCustomAttribute<SupplyParameterFromQueryAttribute>() is null)
                    continue;

                inspected++;
                if (!IsBindable(property.PropertyType))
                    offenders.Add($"{type.FullName}.{property.Name} ({property.PropertyType.Name})");
            }
        }

        inspected.Should().BeGreaterThan(0);
        offenders.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Fatal", Severity.Fatal)]
    [InlineData("fatal", Severity.Fatal)]
    [InlineData("WARNING", Severity.Warning)]
    public void Enum_parses_a_defined_member_name(string value, Severity expected)
    {
        QueryValue.Enum<Severity>(value).Should().Be(expected);
    }

    [Fact]
    public void Enum_returns_null_for_an_unknown_name()
    {
        QueryValue.Enum<Severity>("nonsense").Should().BeNull();
    }

    [Fact]
    public void Enum_returns_null_for_a_number_that_is_not_a_defined_member()
    {
        QueryValue.Enum<Severity>("42").Should().BeNull();
    }

    [Fact]
    public void Enum_returns_null_for_a_missing_value()
    {
        QueryValue.Enum<Severity>(null).Should().BeNull();
    }

    [Fact]
    public void Instant_parses_a_valid_instant()
    {
        QueryValue.Instant("2026-03-04T05:06:07.0000000Z")
            .Should().Be(new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-date")]
    public void Instant_returns_null_for_a_missing_or_malformed_value(string? value)
    {
        QueryValue.Instant(value).Should().BeNull();
    }

    [Fact]
    public void Format_writes_a_value_that_round_trips()
    {
        var instant = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);

        var text = QueryValue.Format(instant);

        text.Should().NotBeNullOrWhiteSpace();
        QueryValue.Instant(text).Should().Be(instant);
    }

    [Fact]
    public void Format_returns_null_for_a_missing_instant()
    {
        QueryValue.Format(null).Should().BeNull();
    }

    private static bool IsBindable(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying is not null)
            type = underlying;

        if (type.IsArray)
        {
            var element = type.GetElementType()!;
            var elementUnderlying = Nullable.GetUnderlyingType(element);
            if (elementUnderlying is not null)
                element = elementUnderlying;

            return BindableTypes.Contains(element);
        }

        return BindableTypes.Contains(type);
    }

    private static IEnumerable<Type> Types(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!;
        }
    }
}
