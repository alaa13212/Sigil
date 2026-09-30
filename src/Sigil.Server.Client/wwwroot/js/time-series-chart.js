const instances = new WeakMap();

function buildTooltip() {
    const tip = document.createElement('div');
    tip.className = 'pointer-events-none fixed z-50 hidden rounded-box border border-base-300 bg-base-100 px-2 py-1 text-xs shadow-sm whitespace-nowrap';

    const label = document.createElement('div');
    label.className = 'opacity-60';

    const value = document.createElement('div');
    value.className = 'font-mono font-semibold';

    tip.append(label, value);
    document.body.appendChild(tip);

    return { tip, label, value };
}

function place(tooltip, clientX, clientY) {
    const left = Math.min(clientX + 14, window.innerWidth - tooltip.tip.offsetWidth - 8);
    const top = Math.max(8, clientY - tooltip.tip.offsetHeight - 14);
    tooltip.tip.style.left = `${Math.max(8, left)}px`;
    tooltip.tip.style.top = `${top}px`;
}

function hide(tooltip) {
    tooltip.tip.classList.add('hidden');
}

export function attach(svg) {
    if (!svg || instances.has(svg)) return;

    const tooltip = buildTooltip();

    const onMove = (e) => {
        const target = e.target instanceof Element ? e.target.closest('[data-tip-label]') : null;
        if (!target) {
            hide(tooltip);
            return;
        }

        tooltip.label.textContent = target.getAttribute('data-tip-label');
        tooltip.value.textContent = target.getAttribute('data-tip-value');
        tooltip.tip.classList.remove('hidden');
        place(tooltip, e.clientX, e.clientY);
    };

    const onLeave = () => hide(tooltip);

    svg.addEventListener('mousemove', onMove);
    svg.addEventListener('mouseleave', onLeave);

    instances.set(svg, { tooltip, onMove, onLeave });
}

export function detach(svg) {
    const instance = instances.get(svg);
    if (!instance) return;

    svg.removeEventListener('mousemove', instance.onMove);
    svg.removeEventListener('mouseleave', instance.onLeave);
    instance.tooltip.tip.remove();
    instances.delete(svg);
}
