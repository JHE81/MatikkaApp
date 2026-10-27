// Raahaus osoitintapahtumilla (hiiri, kosketus, kynä). Toimii MAUI:n WebView'ssä ja selaimessa.
//
// Raahattavat elementit: [data-drag-id]. Pudotuskohteet: [data-drop-zone].
// Pudotus kohteeseen tai napautus (ei liikettä) kutsuu .NET-metodia OnDragDropped(id, tapped).
// Näppäimistöllä Enter/välilyönti toimii kuten napautus.

const MOVE_THRESHOLD = 8;

export function init(root, dotnet) {
    let drag = null;
    let overZone = null;

    function zoneAt(x, y, dragged) {
        dragged.style.visibility = 'hidden';
        const under = document.elementFromPoint(x, y);
        dragged.style.visibility = '';
        return under ? under.closest('[data-drop-zone]') : null;
    }

    function setOver(zone) {
        if (overZone === zone) return;
        if (overZone) overZone.classList.remove('is-over');
        overZone = zone;
        if (overZone) overZone.classList.add('is-over');
    }

    function reset(el) {
        el.classList.remove('is-dragging');
        el.style.transform = '';
        setOver(null);
    }

    function onDown(e) {
        const el = e.target.closest('[data-drag-id]');
        if (!el || el.disabled || !root.contains(el)) return;
        drag = { el, id: Number(el.dataset.dragId), x: e.clientX, y: e.clientY, moved: false, pointerId: e.pointerId };
        el.setPointerCapture(e.pointerId);
    }

    function onMove(e) {
        if (!drag || e.pointerId !== drag.pointerId) return;
        const dx = e.clientX - drag.x;
        const dy = e.clientY - drag.y;
        if (!drag.moved && Math.hypot(dx, dy) < MOVE_THRESHOLD) return;
        if (!drag.moved) {
            drag.moved = true;
            drag.el.classList.add('is-dragging');
        }
        drag.el.style.transform = `translate(${dx}px, ${dy}px) rotate(-4deg) scale(1.05)`;
        setOver(zoneAt(e.clientX, e.clientY, drag.el));
        e.preventDefault();
    }

    function onUp(e, cancelled) {
        if (!drag || e.pointerId !== drag.pointerId) return;
        const d = drag;
        drag = null;

        if (cancelled) {
            reset(d.el);
            return;
        }

        if (!d.moved) {
            reset(d.el);
            dotnet.invokeMethodAsync('OnDragDropped', d.id, true);
            return;
        }

        const zone = zoneAt(e.clientX, e.clientY, d.el);
        reset(d.el);
        if (zone) dotnet.invokeMethodAsync('OnDragDropped', d.id, false);
    }

    function onKey(e) {
        if (e.key !== 'Enter' && e.key !== ' ') return;
        const el = e.target.closest('[data-drag-id]');
        if (!el || el.disabled) return;
        e.preventDefault();
        dotnet.invokeMethodAsync('OnDragDropped', Number(el.dataset.dragId), true);
    }

    const up = e => onUp(e, false);
    const cancel = e => onUp(e, true);
    root.addEventListener('pointerdown', onDown);
    root.addEventListener('pointermove', onMove);
    root.addEventListener('pointerup', up);
    root.addEventListener('pointercancel', cancel);
    root.addEventListener('keydown', onKey);

    return {
        dispose() {
            root.removeEventListener('pointerdown', onDown);
            root.removeEventListener('pointermove', onMove);
            root.removeEventListener('pointerup', up);
            root.removeEventListener('pointercancel', cancel);
            root.removeEventListener('keydown', onKey);
        },
    };
}

export function scrollToEnd(el) {
    if (el) el.scrollTo({ left: el.scrollWidth, behavior: 'smooth' });
}
