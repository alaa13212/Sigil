namespace Sigil.Application.Input;

/// <summary>Holds which row of a list the keyboard focus is on.</summary>
public sealed class ListFocusState
{
    /// <summary>Index of the focused row, or -1 when no row is focused.</summary>
    public int Index { get; private set; } = -1;

    /// <summary>Whether a row is focused.</summary>
    public bool HasFocus => Index >= 0;

    /// <summary>Moves the focus by <paramref name="delta"/> rows and clamps it to the list.</summary>
    public int Move(int delta, int itemCount)
    {
        if (itemCount <= 0)
        {
            Index = -1;
            return Index;
        }

        if (Index < 0)
        {
            Index = delta > 0 ? 0 : -1;
            return Index;
        }

        Index = Math.Clamp(Index + delta, 0, itemCount - 1);
        return Index;
    }

    /// <summary>Drops the focus.</summary>
    public void Reset() => Index = -1;

    /// <summary>Keeps the focus inside a list that changed size.</summary>
    public void ClampTo(int itemCount)
    {
        if (itemCount <= 0)
        {
            Index = -1;
            return;
        }

        if (Index >= itemCount) Index = itemCount - 1;
    }
}
