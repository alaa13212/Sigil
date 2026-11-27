using Sigil.Application.Input;

namespace Sigil.Application.Tests.Input;

public class ListFocusStateTests
{
    [Fact]
    public void NothingIsFocusedToStartWith()
    {
        var focus = new ListFocusState();

        focus.Index.Should().Be(-1);
        focus.HasFocus.Should().BeFalse();
    }

    [Fact]
    public void MovingDownFromNothing_FocusesTheFirstRow()
    {
        var focus = new ListFocusState();

        focus.Move(1, 5).Should().Be(0);
        focus.HasFocus.Should().BeTrue();
    }

    [Fact]
    public void MovingUpFromNothing_LeavesNothingFocused()
    {
        var focus = new ListFocusState();

        focus.Move(-1, 5).Should().Be(-1);
        focus.HasFocus.Should().BeFalse();
    }

    [Fact]
    public void MovingDownStopsAtTheLastRow()
    {
        var focus = new ListFocusState();

        focus.Move(1, 3);
        focus.Move(1, 3).Should().Be(1);
        focus.Move(1, 3).Should().Be(2);
        focus.Move(1, 3).Should().Be(2);
    }

    [Fact]
    public void MovingUpStopsAtTheFirstRow()
    {
        var focus = new ListFocusState();

        focus.Move(1, 3);
        focus.Move(-1, 3).Should().Be(0);
        focus.Move(-1, 3).Should().Be(0);
    }

    [Fact]
    public void AnEmptyList_HasNoFocusedRow()
    {
        var focus = new ListFocusState();

        focus.Move(1, 0).Should().Be(-1);
        focus.HasFocus.Should().BeFalse();
    }

    [Fact]
    public void Reset_DropsTheFocus()
    {
        var focus = new ListFocusState();

        focus.Move(1, 5);
        focus.Reset();
        focus.HasFocus.Should().BeFalse();
    }

    [Fact]
    public void ClampingToAShorterList_PullsTheFocusBack()
    {
        var focus = new ListFocusState();

        focus.Move(1, 5);
        focus.Move(1, 5);
        focus.Index.Should().Be(1);
        focus.ClampTo(2);
        focus.Index.Should().Be(1);
    }

    [Fact]
    public void ClampingToAnEmptyList_DropsTheFocus()
    {
        var focus = new ListFocusState();

        focus.Move(1, 5);
        focus.ClampTo(0);
        focus.HasFocus.Should().BeFalse();
    }
}
