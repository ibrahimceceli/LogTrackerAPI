// Error detail dialog: loads GET /api/reports/errors/{id} and shows the stack trace.
// The stack trace and message are attacker-influenced text, so they are only ever set via textContent.
import { ApiError, getReport } from './api.js';
import { methodBadge, routeText, statusBadge } from './components.js';
import { el, icon } from './dom.js';
import { formatFullDateTime, parseTimestamp } from './format.js';
import { t } from './i18n.js';
import { describeError } from './section.js';

const dialog = document.getElementById('error-dialog');
const title = document.getElementById('error-dialog-title');
const body = document.getElementById('error-dialog-body');

let current = null;    // { id, status: 'loading' | 'loaded' | 'failed', detail?, error? }
let controller = null;

dialog.querySelector('[data-close]').addEventListener('click', () => dialog.close());
// A click on the dialog element itself (not its content) is a click on the backdrop.
dialog.addEventListener('click', (event) => {
    if (event.target === dialog) dialog.close();
});
dialog.addEventListener('close', () => {
    controller?.abort();
    const closedId = current?.id;
    current = null;
    restoreFocus(closedId);
});

export async function openErrorDetails(id) {
    controller?.abort();
    const own = new AbortController();
    controller = own;
    current = { id, status: 'loading' };
    paint();
    if (!dialog.open) {
        dialog.showModal();
    }
    try {
        const detail = await getReport(`errors/${encodeURIComponent(String(id))}`, {}, own.signal);
        if (own !== controller) return;
        current = { id, status: 'loaded', detail };
    } catch (error) {
        if (own !== controller) return;
        current = { id, status: 'failed', error };
    }
    paint();
}

/** Re-renders the open dialog, e.g. after the language changed. */
export function repaintErrorDetails() {
    if (current) paint();
}

function paint() {
    title.textContent = t('details.titleWithId', { id: current.id });
    if (current.status === 'loading') {
        body.replaceChildren(el('div', { className: 'state', attrs: { 'aria-busy': 'true' } }, [
            el('span', { className: 'spinner', attrs: { 'aria-hidden': 'true' } }),
            el('p', { text: t('state.loading') }),
        ]));
    } else if (current.status === 'failed') {
        const { error } = current;
        const notFound = error instanceof ApiError && error.kind === 'notFound';
        body.replaceChildren(el('div', { className: 'state state--error' }, [
            icon('alert', 'icon state__icon'),
            el('p', { text: notFound ? t('details.notFound') : describeError(error) }),
            error.traceId ? el('p', { className: 'state__trace', text: t('error.traceId', { traceId: error.traceId }) }) : null,
        ]));
    } else {
        body.replaceChildren(...buildDetail(current.detail));
    }
}

function buildDetail(detail) {
    const occurredAt = parseTimestamp(detail.occurredAtUtc);
    return [
        el('div', { className: 'detail-summary' }, [
            el('span', { className: 'mono error-type', text: detail.type ?? '–' }),
            el('p', { className: 'detail-summary__message', text: detail.message || t('errors.noMessage') }),
        ]),
        el('dl', { className: 'detail-grid' }, [
            field(t('details.occurredAt'), occurredAt
                ? el('time', { text: formatFullDateTime(occurredAt), attrs: { datetime: occurredAt.toISOString() } })
                : '–'),
            field(t('details.request'), el('span', { className: 'method-route' }, [
                methodBadge(detail.method),
                el('span', { className: 'mono', text: detail.path ?? '–' }),
            ])),
            field(t('details.endpoint'), routeText(detail.endpoint)),
            field(t('details.status'), statusBadge(detail.statusCode)),
            field(t('details.source'), el('span', { className: 'mono', text: detail.source || '–' })),
            field(t('details.traceId'), el('span', { className: 'mono', text: detail.traceId || '–' })),
        ]),
        el('h3', { className: 'detail-section-title', text: t('details.stackTrace') }),
        detail.stackTrace
            // tabindex lets keyboard users scroll a long trace.
            ? el('pre', { className: 'stack-trace', text: detail.stackTrace, attrs: { tabindex: '0', 'aria-label': t('details.stackTrace') } })
            : el('p', { className: 'muted', text: t('details.noStackTrace') }),
    ];
}

function field(label, value) {
    return el('div', {}, [
        el('dt', { text: label }),
        el('dd', {}, [value]),
    ]);
}

// The browser returns focus to the button that opened the dialog, but the error table may have been
// re-rendered while the dialog was open; in that case focus the matching button in the new table.
function restoreFocus(id) {
    requestAnimationFrame(() => {
        if (id === undefined || (document.activeElement && document.activeElement !== document.body)) return;
        document.querySelector(`[data-focus-key="${CSS.escape(`error-${id}`)}"]`)?.focus();
    });
}
