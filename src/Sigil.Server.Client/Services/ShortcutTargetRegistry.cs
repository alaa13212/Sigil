using Sigil.Application.Input;

namespace Sigil.Server.Client.Services;

/// <summary>Keeps the list that owns the keyboard focus.</summary>
public sealed class ShortcutTargetRegistry
{
    private IShortcutTarget? _target;

    /// <summary>The list that currently takes the keyboard focus, if any.</summary>
    public IShortcutTarget? Target => _target;

    /// <summary>Claims the keyboard focus for a list.</summary>
    public void Register(IShortcutTarget target) => _target = target;

    /// <summary>Releases the keyboard focus when the list is gone.</summary>
    public void Unregister(IShortcutTarget target)
    {
        if (ReferenceEquals(_target, target))
            _target = null;
    }
}
