// Small reusable building blocks for the tables: badges, timestamps, table shell and pager.
import { ApiError, getReport } from './api.js';
import { el } from './dom.js';
import { formatFullDateTime, formatInteger, formatTimestamp, parseTimestamp } from './format.js';
import { t } from './i18n.js';

const KNOWN_METHODS = ['GET', 'POST', 'PUT', 'PATCH', 'DELETE'];
const UNMATCHED_ENDPOINT = '(unmatched)';

export function methodBadge(method) {
    const name = String(method ?? '').toUpperCase();
    // The CSS class comes from a fixed list, not straight from API data.
    const variant = KNOWN_METHODS.includes(name) ? name.toLowerCase() : 'other';
    return el('span', { className: `method method--${variant}`, text: name || '?' });
}

export function statusBadge(statusCode) {
    const code = Number(statusCode);
    if (!Number.isFinite(code)) {
        return el('span', { className: 'muted', text: '–' });
    }
    const group = code >= 500 ? '5xx' : code >= 400 ? '4xx' : code >= 300 ? '3xx' : code >= 200 ? '2xx' : 'other';
    return el('span', { className: `status status--${group}`, text: code });
}

/** Route template such as /api/products/{id:int}; the API reports unknown routes as "(unmatched)". */
export function routeText(endpoint) {
    if (endpoint === UNMATCHED_ENDPOINT) {
        return el('span', { className: 'route route--unmatched', text: t('endpoint.unmatched') });
    }
    return el('span', { className: 'route mono', text: endpoint ?? '–', attrs: { title: endpoint } });
}

export function methodAndRoute(method, endpoint) {
    return el('span', { className: 'method-route' }, [methodBadge(method), routeText(endpoint)]);
}

export function timeElement(isoTimestamp) {
    const date = parseTimestamp(isoTimestamp);
    if (!date) {
        return el('span', { className: 'muted', text: '–' });
    }
    return el('time', {
        text: formatTimestamp(date),
        attrs: { datetime: date.toISOString(), title: formatFullDateTime(date) },
    });
}

/** "System.InvalidOperationException" -> "InvalidOperationException" for compact display. */
export function shortTypeName(type) {
    const name = String(type ?? '');
    return name.slice(name.lastIndexOf('.') + 1) || name;
}

/**
 * Builds a horizontally scrollable table.
 * columns: [{ label, className, title?, extra? }] (extra: a node appended after the label), rows: <tr> elements.
 */
export function dataTable({ className, caption, columns, rows }) {
    return el('div', {
        className: 'table-scroll',
        // Focusable so keyboard users can scroll the table sideways on small screens.
        attrs: { tabindex: '0', role: 'region', 'aria-label': caption },
        dataset: { focusKey: 'table' },
    }, [
        el('table', { className: `data-table ${className}` }, [
            el('caption', { className: 'visually-hidden', text: caption }),
            el('thead', {}, [
                el('tr', {}, columns.map((column) => el('th', {
                    className: column.className,
                    text: column.label,
                    attrs: { scope: 'col', title: column.title },
                }, [column.extra]))),
            ]),
            el('tbody', {}, rows),
        ]),
    ]);
}

/**
 * Replaces a section's content while keeping the horizontal scroll position of its table and
 * keyboard focus on the element with the same data-focus-key (rows are rebuilt on every refresh).
 */
export function replaceContent(content, node) {
    const oldScroller = content.querySelector('.table-scroll');
    const scrollLeft = oldScroller?.scrollLeft ?? 0;
    const focused = content.contains(document.activeElement) ? document.activeElement.dataset.focusKey : undefined;

    content.replaceChildren(node);

    const newScroller = content.querySelector('.table-scroll');
    if (newScroller) {
        newScroller.scrollLeft = scrollLeft;
    }
    if (focused) {
        content.querySelector(`[data-focus-key="${CSS.escape(focused)}"]`)?.focus();
    }
}

/** Previous/next pager bound to the static <nav class="pager"> markup of a section. */
export function createPager(nav, onPageChange) {
    const previousButton = nav.querySelector('[data-pager="prev"]');
    const nextButton = nav.querySelector('[data-pager="next"]');
    const info = nav.querySelector('.pager__info');

    let page = 1;
    let pageCount = 1;
    let totalCount = null;

    previousButton.addEventListener('click', () => goTo(page - 1));
    nextButton.addEventListener('click', () => goTo(page + 1));

    function goTo(target) {
        if (target < 1 || target > pageCount) return;
        page = target;
        render();
        onPageChange();
    }

    function render() {
        previousButton.disabled = page <= 1;
        nextButton.disabled = page >= pageCount;
        info.textContent = totalCount === null
            ? ''
            : t('pager.summary', { page: formatInteger(page), pages: formatInteger(pageCount), total: formatInteger(totalCount) });
    }

    return {
        get page() {
            return page;
        },
        setPage(value) {
            page = value;
            render();
        },
        update(total, pageSize) {
            totalCount = total;
            pageCount = Math.max(1, Math.ceil(total / pageSize));
            render();
        },
        render,
    };
}

/** Loads one page of a paged report ({ items, page, pageSize, totalCount }) and updates the pager. */
export async function loadPage(path, pager, pageSize, signal) {
    let result = await getReport(path, { page: pager.page, pageSize }, signal);
    const lastPage = Math.max(1, Math.ceil((Number(result?.totalCount) || 0) / pageSize));
    // Rows can disappear (retention cleanup) while the user is on a later page: show the last page that still exists.
    if (pager.page > lastPage) {
        pager.setPage(lastPage);
        result = await getReport(path, { page: lastPage, pageSize }, signal);
    }
    expectArray(result?.items);
    pager.update(Number(result.totalCount) || 0, pageSize);
    return result;
}

/** Throws a "bad response" error when a list endpoint did not return the expected shape. */
export function expectArray(value) {
    if (!Array.isArray(value)) {
        throw new ApiError('badResponse');
    }
    return value;
}
