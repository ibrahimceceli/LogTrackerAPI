// Paged list of recent requests from GET /api/reports/requests?page=…&pageSize=…
import { createPager, dataTable, loadPage, methodBadge, replaceContent, statusBadge, timeElement } from '../components.js';
import { el } from '../dom.js';
import { formatDuration } from '../format.js';
import { t } from '../i18n.js';
import { createSection } from '../section.js';

const PAGE_SIZE = 10;

export function createRecentRequestsSection({ element, onEnterApiKey }) {
    const pager = createPager(element.querySelector('.pager'), () => section.refresh());

    const section = createSection({
        element,
        onEnterApiKey,
        load: (signal) => loadPage('requests', pager, PAGE_SIZE, signal),
        isEmpty: (result) => result.items.length === 0,
        emptyMessage: () => t('requests.empty'),
        render: (content, result) => replaceContent(content, buildTable(result.items)),
    });

    return {
        ...section,
        paint() {
            section.paint();
            pager.render();
        },
    };
}

function buildTable(items) {
    return dataTable({
        className: 'data-table--requests',
        caption: t('requests.caption'),
        columns: [
            { label: t('requests.time') },
            { label: t('requests.method') },
            { label: t('requests.path'), className: 'cell-grow' },
            { label: t('requests.status') },
            { label: t('requests.duration'), className: 'num' },
        ],
        rows: items.map((item) => el('tr', {}, [
            el('td', { className: 'muted' }, [timeElement(item.occurredAtUtc)]),
            el('td', {}, [methodBadge(item.method)]),
            el('td', {
                className: 'cell-grow mono',
                text: item.path,
                // The route template (e.g. /api/products/{id:int}) is shown on hover.
                attrs: { title: item.endpoint && item.endpoint !== item.path ? `${item.path}\n${item.endpoint}` : item.path },
            }),
            el('td', {}, [statusBadge(item.statusCode)]),
            el('td', { className: 'num', text: formatDuration(item.durationMs) }),
        ])),
    });
}
