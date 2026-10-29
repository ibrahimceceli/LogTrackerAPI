// Endpoint table from GET /api/reports/endpoints?hours=… (already sorted by request count).
import { getReport } from '../api.js';
import { dataTable, expectArray, methodAndRoute, replaceContent } from '../components.js';
import { el } from '../dom.js';
import { formatDuration, formatInteger, formatPercent } from '../format.js';
import { t } from '../i18n.js';
import { createSection } from '../section.js';

export function createEndpointsSection({ element, getRangeHours, onEnterApiKey }) {
    return createSection({
        element,
        onEnterApiKey,
        load: async (signal) => expectArray(await getReport('endpoints', { hours: getRangeHours() }, signal)),
        isEmpty: (rows) => rows.length === 0,
        emptyMessage: () => t('endpoints.empty'),
        render: (content, rows) => replaceContent(content, buildTable(rows)),
    });
}

function buildTable(rows) {
    const requestCounts = rows.map((row) => Number(row.totalRequests) || 0);
    const totalRequests = requestCounts.reduce((sum, count) => sum + count, 0);
    const busiest = Math.max(1, ...requestCounts);

    return dataTable({
        className: 'data-table--endpoints',
        caption: t('endpoints.caption'),
        columns: [
            { label: t('endpoints.endpoint'), className: 'cell-grow' },
            { label: t('endpoints.requests'), className: 'num' },
            { label: t('endpoints.serverErrors'), className: 'num' },
            { label: t('endpoints.avg'), className: 'num' },
            { label: t('endpoints.p95'), className: 'num' },
            { label: t('endpoints.max'), className: 'num' },
        ],
        rows: rows.map((row, index) => buildRow(row, requestCounts[index], totalRequests, busiest)),
    });
}

function buildRow(row, requests, totalRequests, busiest) {
    const serverErrors = Number(row.serverErrorCount) || 0;
    const share = totalRequests > 0 ? (requests / totalRequests) * 100 : 0;

    // Bar length is relative to the busiest endpoint; the tooltip gives the exact share of all traffic.
    const fill = el('span', { className: 'share__fill' });
    fill.style.width = `${(requests / busiest) * 100}%`;

    return el('tr', {}, [
        el('td', { className: 'cell-grow' }, [methodAndRoute(row.method, row.endpoint)]),
        el('td', { className: 'num' }, [
            el('span', { className: 'share', attrs: { title: t('endpoints.share', { value: formatPercent(share) }) } }, [
                el('span', { className: 'share__bar', attrs: { 'aria-hidden': 'true' } }, [fill]),
                el('span', { text: formatInteger(requests) }),
            ]),
        ]),
        el('td', { className: serverErrors > 0 ? 'num count--bad' : 'num muted', text: formatInteger(serverErrors) }),
        el('td', { className: 'num', text: formatDuration(row.averageDurationMs) }),
        el('td', { className: 'num', text: formatDuration(row.p95DurationMs) }),
        el('td', { className: 'num', text: formatDuration(row.maxDurationMs) }),
    ]);
}
