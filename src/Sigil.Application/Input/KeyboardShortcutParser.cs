namespace Sigil.Application.Input;

/// <summary>Turns pressed keys into shortcut actions.</summary>
public sealed class KeyboardShortcutParser
{
    private const char GoPrefix = 'g';
    private char? _pendingPrefix;

    /// <summary>Whether a prefix is waiting for its second key.</summary>
    public bool HasPendingPrefix => _pendingPrefix.HasValue;

    /// <summary>Maps a pressed key to the action it completes, or <see cref="ShortcutAction.None"/>.</summary>
    public ShortcutAction Push(char key)
    {
        var normalized = char.ToLowerInvariant(key);

        if (_pendingPrefix is { } prefix)
        {
            _pendingPrefix = null;
            if (prefix != GoPrefix) return ShortcutAction.None;

            return normalized switch
            {
                'i' => ShortcutAction.NavigateIssues,
                'r' => ShortcutAction.NavigateReleases,
                'e' => ShortcutAction.NavigateEvents,
                'd' => ShortcutAction.NavigateDashboard,
                _ => ShortcutAction.None,
            };
        }

        switch (normalized)
        {
            case GoPrefix:
                _pendingPrefix = GoPrefix;
                return ShortcutAction.None;
            case 'j':
                return ShortcutAction.FocusNext;
            case 'k':
                return ShortcutAction.FocusPrevious;
            case 'r':
                return ShortcutAction.ResolveFocused;
            case '?':
                return ShortcutAction.ShowHelp;
            default:
                return ShortcutAction.None;
        }
    }

    /// <summary>Drops a pending prefix.</summary>
    public void Reset() => _pendingPrefix = null;
}
