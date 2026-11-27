let _dotNetRef = null;
let _keydownHandler = null;

export function register(dotNetRef) {
    _dotNetRef = dotNetRef;

    _keydownHandler = (e) => {
        if (e.ctrlKey || e.metaKey || e.altKey) return;
        if (e.repeat) return;
        if (isTypingTarget(e.target)) return;
        if (e.key.length !== 1) return;

        if (document.querySelector('.shortcut-modal .modal-open')) {
            if (e.key !== '?') return;
        } else if (document.querySelector('.modal-open')) {
            return;
        }

        _dotNetRef.invokeMethodAsync('HandleShortcutAsync', e.key);
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

export function scrollElementIntoView(id) {
    const el = document.getElementById(id);
    if (el) el.scrollIntoView({ block: 'nearest' });
}

const TYPING_INPUT_TYPES = new Set([
    'color', 'date', 'datetime-local', 'email', 'file', 'month', 'number',
    'password', 'range', 'search', 'tel', 'text', 'time', 'url', 'week'
]);

function isTypingTarget(target) {
    if (!target) return false;

    const tag = (target.tagName || '').toLowerCase();
    if (tag === 'textarea' || tag === 'select') return true;

    if (tag === 'input') {
        const type = (target.type || 'text').toLowerCase();
        return TYPING_INPUT_TYPES.has(type);
    }

    return target.isContentEditable === true;
}
