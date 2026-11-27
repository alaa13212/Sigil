namespace Sigil.Application.Input;

/// <summary>What a keyboard shortcut asks the interface to do.</summary>
public enum ShortcutAction
{
    /// <summary>The key is not a shortcut, or the sequence it started is not complete.</summary>
    None,

    /// <summary>Open the issue list of the current project.</summary>
    NavigateIssues,

    /// <summary>Open the release list of the current project.</summary>
    NavigateReleases,

    /// <summary>Open the event list of the current project.</summary>
    NavigateEvents,

    /// <summary>Open the dashboard.</summary>
    NavigateDashboard,

    /// <summary>Move the focused row down.</summary>
    FocusNext,

    /// <summary>Move the focused row up.</summary>
    FocusPrevious,

    /// <summary>Resolve the focused issue.</summary>
    ResolveFocused,

    /// <summary>Show or hide the shortcut overlay.</summary>
    ShowHelp,
}
