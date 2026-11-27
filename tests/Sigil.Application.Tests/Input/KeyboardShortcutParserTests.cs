using Sigil.Application.Input;

namespace Sigil.Application.Tests.Input;

public class KeyboardShortcutParserTests
{
    [Fact]
    public void GoThenI_AsksForTheIssueList()
    {
        var parser = new KeyboardShortcutParser();

        parser.Push('g').Should().Be(ShortcutAction.None);
        parser.Push('i').Should().Be(ShortcutAction.NavigateIssues);
    }

    [Fact]
    public void GoThenR_AsksForTheReleaseList()
    {
        var parser = new KeyboardShortcutParser();

        parser.Push('g');
        parser.Push('r').Should().Be(ShortcutAction.NavigateReleases);
    }

    [Fact]
    public void GoThenE_AsksForTheEventList()
    {
        var parser = new KeyboardShortcutParser();

        parser.Push('g');
        parser.Push('e').Should().Be(ShortcutAction.NavigateEvents);
    }

    [Fact]
    public void GoThenD_AsksForTheDashboard()
    {
        var parser = new KeyboardShortcutParser();

        parser.Push('g');
        parser.Push('d').Should().Be(ShortcutAction.NavigateDashboard);
    }

    [Fact]
    public void APrefixOnItsOwn_IsNotAnAction()
    {
        var parser = new KeyboardShortcutParser();

        parser.Push('g').Should().Be(ShortcutAction.None);
    }

    [Fact]
    public void AnUnknownKeyAfterThePrefix_ResetsTheSequence()
    {
        var parser = new KeyboardShortcutParser();

        parser.Push('g');
        parser.Push('x').Should().Be(ShortcutAction.None);
        parser.Push('i').Should().Be(ShortcutAction.None);
    }

    [Fact]
    public void AnUnknownKeyBeforeThePrefix_LeavesTheSequenceAlone()
    {
        var parser = new KeyboardShortcutParser();

        parser.Push('x').Should().Be(ShortcutAction.None);
        parser.Push('g');
        parser.Push('i').Should().Be(ShortcutAction.NavigateIssues);
    }

    [Fact]
    public void JAndK_MoveTheFocus()
    {
        var parser = new KeyboardShortcutParser();

        parser.Push('j').Should().Be(ShortcutAction.FocusNext);
        parser.Push('k').Should().Be(ShortcutAction.FocusPrevious);
    }

    [Fact]
    public void R_AsksForTheFocusedIssueToBeResolved()
    {
        var parser = new KeyboardShortcutParser();

        parser.Push('r').Should().Be(ShortcutAction.ResolveFocused);
    }

    [Fact]
    public void AQuestionMark_AsksForTheOverlay()
    {
        var parser = new KeyboardShortcutParser();

        parser.Push('?').Should().Be(ShortcutAction.ShowHelp);
    }

    [Fact]
    public void AnUppercaseKey_IsTheSameShortcut()
    {
        var parser = new KeyboardShortcutParser();

        parser.Push('G');
        parser.Push('I').Should().Be(ShortcutAction.NavigateIssues);
    }

    [Fact]
    public void Reset_DropsThePendingPrefix()
    {
        var parser = new KeyboardShortcutParser();

        parser.Push('g');
        parser.Reset();
        parser.Push('i').Should().Be(ShortcutAction.None);
    }

    [Fact]
    public void ThePrefixIsOnlyPendingBetweenTwoKeys()
    {
        var parser = new KeyboardShortcutParser();

        parser.Push('g').Should().Be(ShortcutAction.None);
        parser.HasPendingPrefix.Should().BeTrue();
        parser.Push('i').Should().Be(ShortcutAction.NavigateIssues);
        parser.HasPendingPrefix.Should().BeFalse();
    }
}
