namespace Sigil.Application.Input;

/// <summary>A list that takes the keyboard focus.</summary>
public interface IShortcutTarget
{
    /// <summary>Moves the focus by <paramref name="delta"/> rows and returns the id of the focused row, or null.</summary>
    Task<string?> MoveFocusAsync(int delta);

    /// <summary>Resolves the focused row and reports whether a row was resolved.</summary>
    Task<bool> ResolveFocusedAsync();
}
