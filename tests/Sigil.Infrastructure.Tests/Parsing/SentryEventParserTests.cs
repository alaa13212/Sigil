using Sigil.Infrastructure.Parsing;
using Sigil.Infrastructure.Parsing.Models;

namespace Sigil.Infrastructure.Tests.Parsing;

public class SentryEventParserTests
{
    // --- SelectPrimaryException ---

    [Fact]
    public void SelectPrimary_NullList_ReturnsNull()
    {
        SentryEventParser.SelectPrimaryException(null).Should().BeNull();
    }

    [Fact]
    public void SelectPrimary_EmptyList_ReturnsNull()
    {
        SentryEventParser.SelectPrimaryException([]).Should().BeNull();
    }

    [Fact]
    public void SelectPrimary_NoMechanismMetadata_ReturnsLast()
    {
        var exceptions = new List<SentryException>
        {
            new() { Type = "First", Value = "first error" },
            new() { Type = "Second", Value = "second error" },
        };

        var result = SentryEventParser.SelectPrimaryException(exceptions);

        result!.Type.Should().Be("Second");
    }

    [Fact]
    public void SelectPrimary_WithMechanism_FindsRoot()
    {
        var exceptions = new List<SentryException>
        {
            new() { Type = "Root", Value = "root", Mechanism = new SentryMechanism { ExceptionId = 0 } },
            new() { Type = "Other", Value = "other", Mechanism = new SentryMechanism { ExceptionId = 1 } },
        };

        var result = SentryEventParser.SelectPrimaryException(exceptions);

        result!.Type.Should().Be("Root");
    }

    [Fact]
    public void SelectPrimary_ExceptionGroup_DrillsDown()
    {
        var exceptions = new List<SentryException>
        {
            new()
            {
                Type = "ExceptionGroup", Value = "group",
                Mechanism = new SentryMechanism { ExceptionId = 0, IsExceptionGroup = true }
            },
            new()
            {
                Type = "ChildA", Value = "child a",
                Mechanism = new SentryMechanism { ExceptionId = 1, ParentId = 0 }
            },
            new()
            {
                Type = "ChildB", Value = "child b",
                Mechanism = new SentryMechanism { ExceptionId = 2, ParentId = 0 }
            },
        };

        var result = SentryEventParser.SelectPrimaryException(exceptions);

        result!.Type.Should().Be("ChildB"); // last child of group
    }

    [Fact]
    public void SelectPrimary_NestedExceptionGroup_DrillsToLeaf()
    {
        var exceptions = new List<SentryException>
        {
            new()
            {
                Type = "TopGroup",
                Mechanism = new SentryMechanism { ExceptionId = 0, IsExceptionGroup = true }
            },
            new()
            {
                Type = "SubGroup",
                Mechanism = new SentryMechanism { ExceptionId = 1, ParentId = 0, IsExceptionGroup = true }
            },
            new()
            {
                Type = "Leaf",
                Mechanism = new SentryMechanism { ExceptionId = 2, ParentId = 1 }
            },
        };

        var result = SentryEventParser.SelectPrimaryException(exceptions);

        result!.Type.Should().Be("Leaf");
    }

    [Fact]
    public void SelectPrimary_SyntheticMechanism_SubstitutesValueWithLastInAppFrame()
    {
        var exceptions = new List<SentryException>
        {
            new()
            {
                Type = "UnhandledPromiseRejection",
                Value = "Object",
                Mechanism = new SentryMechanism { ExceptionId = 0, Synthetic = true },
                Stacktrace = new SentryStacktrace
                {
                    Frames =
                    [
                        new SentryStackFrame { Function = "frameworkFunc", InApp = false },
                        new SentryStackFrame { Function = "myHandler", InApp = true },
                    ]
                }
            },
        };

        var result = SentryEventParser.SelectPrimaryException(exceptions);

        result!.Value.Should().Be("myHandler");
    }

    [Fact]
    public void SelectPrimary_SyntheticNoInAppFrames_ValueUnchanged()
    {
        var exceptions = new List<SentryException>
        {
            new()
            {
                Type = "Error",
                Value = "Original",
                Mechanism = new SentryMechanism { ExceptionId = 0, Synthetic = true },
                Stacktrace = new SentryStacktrace
                {
                    Frames =
                    [
                        new SentryStackFrame { Function = "frameworkFunc", InApp = false },
                    ]
                }
            },
        };

        var result = SentryEventParser.SelectPrimaryException(exceptions);

        result!.Value.Should().Be("Original");
    }

    [Fact]
    public void SelectPrimary_SingleException_ReturnsIt()
    {
        var exceptions = new List<SentryException>
        {
            new() { Type = "NullReferenceException", Value = "Object reference not set" },
        };

        var result = SentryEventParser.SelectPrimaryException(exceptions);

        result!.Type.Should().Be("NullReferenceException");
    }

    [Fact]
    public void SelectPrimary_NoRootWithId0_FallsBackToLast()
    {
        var exceptions = new List<SentryException>
        {
            new() { Type = "A", Mechanism = new SentryMechanism { ExceptionId = 5 } },
            new() { Type = "B", Mechanism = new SentryMechanism { ExceptionId = 6 } },
        };

        var result = SentryEventParser.SelectPrimaryException(exceptions);

        result!.Type.Should().Be("B");
    }
}
