// A dashboard section: loads its own data and shows a loading, empty, error or data state.
// Every section catches its own failures, so one broken endpoint never breaks the others.
import { ApiError } from './api.js';
import { el, icon } from './dom.js';
import { formatTime } from './format.js';
import { getLanguage, t } from './i18n.js';

/** Thrown by the chart section when the Chart.js script did not load. */
export const CHART_UNAVAILABLE = 'chart-unavailable';

export function describeError(error) {
    if (error instanceof ApiError) {
        switch (error.kind) {
            case 'network': return t('error.network');
            case 'timeout': return t('error.timeout');
            case 'unauthorized': return t('error.unauthorized');
            case 'notFound': return t('error.notFound');
            case 'badResponse': return t('error.badResponse');
            default: return t('error.http', { status: error.status });
        }
    }
    if (error?.message === CHART_UNAVAILABLE) {
        return t('hourly.chartUnavailable');
    }
    return t('error.unexpected');
}

const isUnauthorized = (error) => error instanceof ApiError && error.kind === 'unauthorized';

/**
 * @param {object} options
 * @param {HTMLElement} options.element   section element containing .panel__alert and .panel__content
 * @param {(signal: AbortSignal) => Promise<any>} options.load
 * @param {(content: HTMLElement, data: any) => void} options.render
 * @param {(data: any) => boolean} [options.isEmpty]
 * @param {() => string} [options.emptyMessage]
 * @param {(data: any) => string} [options.signature]  identifies what is on screen; unchanged data is not re-rendered
 * @param {() => void} options.onEnterApiKey
 */
export function createSection({
    element,
    load,
    render,
    isEmpty = () => false,
    emptyMessage = () => '',
    signature = (data) => JSON.stringify(data),
    onEnterApiKey,
}) {
    const alertBox = element.querySelector('.panel__alert');
    const content = element.querySelector('.panel__content');

    let data;            // last successful response; undefined until the first success
    let error = null;    // last failure; cleared by the next success
    let loadedAt = null;
    let controller = null;
    let paintedKey = null;

    async function refresh() {
        // Cancel a request that is still running (e.g. the user changed page twice quickly).
        controller?.abort();
        const own = new AbortController();
        controller = own;
        try {
            const result = await load(own.signal);
            if (own !== controller) return 'superseded';
            data = result;
            error = null;
            loadedAt = new Date();
        } catch (failure) {
            if (own !== controller) return 'superseded';
            if (!(failure instanceof ApiError)) {
                console.error(failure);
            }
            error = failure;
        }
        paint();
        if (!error) return 'ok';
        return isUnauthorized(error) ? 'unauthorized' : 'failed';
    }

    /** Forgets the data on screen (e.g. after the time range changed) and shows the loading state. */
    function reset() {
        controller?.abort();
        controller = null;
        data = undefined;
        error = null;
        paint();
    }

    function paint() {
        // Polling returns the same data most of the time. Skipping identical renders avoids flicker
        // and keeps keyboard focus and scroll positions where the user left them.
        const key = `${getLanguage()}|${currentStateKey()}`;
        if (key !== paintedKey) {
            paintedKey = key;
            if (data === undefined) {
                if (error) renderError(); else renderLoading();
            } else if (isEmpty(data)) {
                renderEmpty();
            } else {
                render(content, data);
            }
        }
        updateAlert();
    }

    function currentStateKey() {
        if (data === undefined) {
            // Every failed request has a new trace ID; leaving it out keeps a repeating error on screen
            // as it is (with the first trace ID) instead of rebuilding it, and its focused button, every poll.
            return error ? `error|${describeError(error)}` : 'loading';
        }
        return isEmpty(data) ? `empty|${emptyMessage()}` : `data|${signature(data)}`;
    }

    function renderLoading() {
        content.replaceChildren(el('div', { className: 'state', attrs: { 'aria-busy': 'true' } }, [
            el('span', { className: 'spinner', attrs: { 'aria-hidden': 'true' } }),
            el('p', { text: t('state.loading') }),
        ]));
    }

    function renderEmpty() {
        content.replaceChildren(el('div', { className: 'state' }, [
            icon('inbox', 'icon state__icon'),
            el('p', { text: emptyMessage() }),
        ]));
    }

    function renderError() {
        const unauthorized = isUnauthorized(error);
        content.replaceChildren(el('div', { className: 'state state--error' }, [
            icon(unauthorized ? 'lock' : 'alert', 'icon state__icon'),
            el('p', { className: 'state__title', text: unauthorized ? t('apiKey.title') : t('state.errorTitle') }),
            el('p', { text: describeError(error) }),
            // The trace ID lets an operator find the failing request in the server logs; a 401 needs no investigation.
            error.traceId && !unauthorized ? el('p', { className: 'state__trace', text: t('error.traceId', { traceId: error.traceId }) }) : null,
            actionButton(),
        ]));
    }

    // Shown above data that could not be refreshed: the old data stays visible.
    function updateAlert() {
        const stale = data !== undefined && error !== null;
        const text = stale ? `${t('error.stale', { time: formatTime(loadedAt) })} ${describeError(error)}` : '';
        if (alertBox.dataset.text === text) return;
        alertBox.dataset.text = text;
        alertBox.hidden = !stale;
        alertBox.replaceChildren();
        if (stale) {
            alertBox.append(icon('alert'), el('span', { text }), actionButton());
        }
    }

    function actionButton() {
        if (isUnauthorized(error)) {
            return el('button', { className: 'btn btn--sm', text: t('actions.enterKey'), attrs: { type: 'button' }, on: { click: onEnterApiKey } });
        }
        return el('button', { className: 'btn btn--sm', text: t('actions.retry'), attrs: { type: 'button' }, on: { click: () => refresh() } });
    }

    return { refresh, reset, paint };
}
