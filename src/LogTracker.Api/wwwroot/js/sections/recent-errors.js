// Paged list of recent exceptions from GET /api/reports/errors?page=…&pageSize=…
// Selecting a row opens the detail dialog (GET /api/reports/errors/{id}).
import { createPager, dataTable, loadPage, replaceContent, shortTypeName, statusBadge, timeElement } from '../components.js';
import { el } from '../dom.js';
import { t } from '../i18n.js';
import { createSection } from '../section.js';

const PAGE_SIZE = 10;

export function createRecentErrorsSection({ element, onEnterApiKey, onOpenDetails }) {
    const pager = createPager(element.querySelector('.pager'), () => section.refresh());

    const section = createSection({
        element,
        onEnterApiKey,
        load: (signal) => loadPage('errors', pager, PAGE_SIZE, signal),
        isEmpty: (result) => result.items.length === 0,
        emptyMessage: () => t('errors.empty'),
        render: (content, result) => replaceContent(content, buildTable(result.items, onOpenDetails)),
    });

    return {
        ...section,
        paint() {
            section.paint();
            pager.render();
        },
    };
}

function buildTable(items, onOpenDetails) {
    return dataTable({
        className: 'data-table--errors',
        caption: t('errors.caption'),
        columns: [
            { label: t('errors.time') },
            { label: t('errors.type') },
            { label: t('errors.message'), className: 'cell-grow' },
            { label: t('errors.status') },
        ],
        rows: items.map((item) => buildRow(item, onOpenDetails)),
    });
}

function buildRow(item, onOpenDetails) {
    const message = item.message || t('errors.noMessage');
    // The message is a real <button> so keyboard and screen-reader users can open the details;
    // the whole row is clickable as a convenience for mouse users.
    const button = el('button', {
        className: 'row-button',
        attrs: { type: 'button', title: message },
        dataset: { focusKey: `error-${item.id}` },
        on: { click: () => onOpenDetails(item.id) },
    }, [
        el('span', { className: 'visually-hidden', text: `${t('details.title')}: ` }),
        message,
    ]);

    return el('tr', {
        on: {
            click: (event) => {
                if (event.target.closest('button, a') || window.getSelection()?.toString()) return;
                onOpenDetails(item.id);
            },
        },
    }, [
        el('td', { className: 'muted' }, [timeElement(item.occurredAtUtc)]),
        el('td', {}, [el('span', { className: 'mono error-type truncate', text: shortTypeName(item.type), attrs: { title: item.type } })]),
        el('td', { className: 'cell-grow' }, [button]),
        el('td', {}, [statusBadge(item.statusCode)]),
    ]);
}
