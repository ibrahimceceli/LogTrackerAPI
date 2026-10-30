// KPI cards from GET /api/reports/summary?hours=…
import { getReport } from '../api.js';
import { el } from '../dom.js';
import { formatDecimal, formatDuration, formatInteger, formatPercent } from '../format.js';
import { t } from '../i18n.js';
import { createSection } from '../section.js';

// Thresholds (share of 5xx responses, in percent) for the health badge on the error-rate card.
const WARNING_ERROR_RATE = 1;
const CRITICAL_ERROR_RATE = 5;

export function createSummarySection({ element, getRangeHours, onEnterApiKey }) {
    return createSection({
        element,
        onEnterApiKey,
        load: (signal) => getReport('summary', { hours: getRangeHours() }, signal),
        render: (content, summary) => content.replaceChildren(...buildCards(summary, getRangeHours())),
    });
}

function buildCards(summary, hours) {
    const totalRequests = Number(summary.totalRequests) || 0;
    const errorRate = Number(summary.errorRatePercent) || 0;
    const hasTraffic = totalRequests > 0;

    const cards = el('dl', { className: 'kpi-grid' }, [
        card(t('summary.totalRequests'), formatInteger(totalRequests), [
            t('summary.perHour', { value: formatDecimal(totalRequests / hours) }),
        ]),
        card(t('summary.serverErrors'), formatInteger(summary.serverErrorCount), [
            t('summary.clientErrors', { value: formatInteger(summary.clientErrorCount) }),
        ]),
        card(t('summary.errorRate'), formatPercent(errorRate), [
            hasTraffic ? healthBadge(errorRate) : null,
            t('summary.errorRateHint'),
        ]),
        // Averages of zero requests are meaningless, so they show a dash instead of "0 ms".
        card(t('summary.avgLatency'), hasTraffic ? formatDuration(summary.averageDurationMs) : '–', [
            t('summary.avgLatencyHint'),
        ]),
        card(t('summary.p95Latency'), hasTraffic ? formatDuration(summary.p95DurationMs) : '–', [
            t('summary.p95LatencyHint'),
        ]),
    ]);

    return hasTraffic ? [cards] : [cards, el('p', { className: 'kpi-note', text: t('summary.noTraffic') })];
}

function card(label, value, details) {
    return el('div', { className: 'kpi' }, [
        el('dt', { className: 'kpi__label', text: label }),
        el('dd', { className: 'kpi__value', text: value }),
        el('dd', { className: 'kpi__sub' }, details.map((part) => (typeof part === 'string' ? el('span', { text: part }) : part))),
    ]);
}

// Status is shown as a colored dot plus a word, never by color alone.
function healthBadge(errorRate) {
    if (errorRate >= CRITICAL_ERROR_RATE) {
        return el('span', { className: 'health health--critical', text: t('summary.healthCritical') });
    }
    if (errorRate >= WARNING_ERROR_RATE) {
        return el('span', { className: 'health health--warning', text: t('summary.healthWarning') });
    }
    return el('span', { className: 'health', text: t('summary.healthGood') });
}
