let _dotNetRef = null;
let _lastShiftTime = 0;
let _keydownHandler = null;

export function register(dotNetRef) {
    _dotNetRef = dotNetRef;

    _keydownHandler = (e) => {
        // Arrow key / Enter navigation (only when search input is focused)
        const input = document.getElementById('search-palette-input');
        if (input && document.activeElement === input) {
            if (e.key === 'ArrowDown') {
                e.preventDefault();
                _dotNetRef.invokeMethodAsync('HandleKeyNavigation', 1);
                return;
            }
            if (e.key === 'ArrowUp') {
                e.preventDefault();
                _dotNetRef.invokeMethodAsync('HandleKeyNavigation', -1);
                return;
            }
            if (e.key === 'Enter') {
                e.preventDefault();
                _dotNetRef.invokeMethodAsync('HandleEnterKey');
                return;
            }
        }

        // Ctrl+K / Cmd+K
        if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
            e.preventDefault();
            _dotNetRef.invokeMethodAsync('ToggleSearchPalette');
            return;
        }

        // Double-Shift (IntelliJ-style)
        if (e.key === 'Shift' && !e.ctrlKey && !e.altKey && !e.metaKey) {
            const now = Date.now();
            if (now - _lastShiftTime < 400) {
                e.preventDefault();
                _dotNetRef.invokeMethodAsync('ToggleSearchPalette');
                _lastShiftTime = 0;
            } else {
                _lastShiftTime = now;
            }
        } else {
            _lastShiftTime = 0;
        }

        // Escape closes palette
        if (e.key === 'Escape') {
            _dotNetRef.invokeMethodAsync('ClosePalette');
        }
    };

    document.addEventListener('keydown', _keydownHandler);
}

export function unregister() {
    if (_keydownHandler) {
        document.removeEventListener('keydown', _keydownHandler);
        _keydownHandler = null;
    }
    _dotNetRef = null;
}

export function focusInput(id) {
    setTimeout(() => {
        const el = document.getElementById(id);
        if (el) {
            el.focus();
            el.select();
        }
    }, 50);
}

export function scrollResultIntoView(id) {
    const el = document.getElementById(id);
    if (el) el.scrollIntoView({ block: 'nearest' });
}
