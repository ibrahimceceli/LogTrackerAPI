// DOM helpers. Everything the API returns is attacker-controllable (request paths, exception messages,
// stack traces), so the dashboard never builds HTML strings: elements are created one by one and
// text is assigned through textContent / text nodes, which the browser never parses as markup.

/**
 * Creates an element.
 *   el('td', { className: 'num', text: 42, attrs: { title: 'x' } })
 *   el('tr', {}, [cell1, cell2])
 * Strings in `children` become text nodes. Only pass API data to safe attributes such as
 * title, datetime or aria-label — never to href, src, style or on* attributes.
 */
export function el(tag, { className, text, attrs, dataset, on } = {}, children = []) {
    const element = document.createElement(tag);
    if (className) {
        element.className = className;
    }
    if (text !== undefined && text !== null) {
        element.textContent = String(text);
    }
    for (const [name, value] of Object.entries(attrs ?? {})) {
        if (value === undefined || value === null || value === false) continue;
        element.setAttribute(name, value === true ? '' : String(value));
    }
    Object.assign(element.dataset, dataset ?? {});
    for (const [eventName, handler] of Object.entries(on ?? {})) {
        element.addEventListener(eventName, handler);
    }
    element.append(...children.filter((child) => child !== null && child !== undefined && child !== false));
    return element;
}

const SVG_NS = 'http://www.w3.org/2000/svg';

// Static icon shapes (24×24, stroked). These strings are part of the dashboard, never API data.
const ICON_PATHS = {
    inbox: ['M3 13h5l2 3h4l2-3h5', 'M5 5h14l2 8v6H3v-6z'],
    alert: ['M12 3l10 18H2z', 'M12 10v4', 'M12 17.5v.01'],
    lock: ['M6 11h12v10H6z', 'M8.5 11V8a3.5 3.5 0 0 1 7 0v3'],
    check: ['M4 12.5l5 5L20 6.5'],
};

export function icon(name, className = 'icon') {
    const svg = document.createElementNS(SVG_NS, 'svg');
    svg.setAttribute('viewBox', '0 0 24 24');
    svg.setAttribute('class', className);
    svg.setAttribute('aria-hidden', 'true');
    svg.setAttribute('focusable', 'false');
    for (const d of ICON_PATHS[name] ?? []) {
        const path = document.createElementNS(SVG_NS, 'path');
        path.setAttribute('d', d);
        svg.append(path);
    }
    return svg;
}
