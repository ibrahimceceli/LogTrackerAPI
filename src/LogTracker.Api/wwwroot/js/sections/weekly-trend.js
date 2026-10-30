// Weekly latency heat map from GET /api/reports/weekly-trend.
// Each cell is the average duration of one endpoint on one weekday (last 7 UTC days, null = no data).
import { getReport } from '../api.js';
import { dataTable, expectArray, methodAndRoute, replaceContent } from '../components.js';
import { el } from '../dom.js';
import { formatDuration, formatWeekday } from '../format.js';
import { t } from '../i18n.js';
import { createSection } from '../section.js';

const DAYS = ['monday', 'tuesday', 'wednesday', 'thursday', 'friday', 'saturday', 'sunday'];

export function createWeeklyTrendSection({ element, onEnterApiKey }) {
    return createSection({
        element,
        onEnterApiKey,
        load: async (signal) => expectArray(await getReport('weekly-trend', {}, signal)),
        isEmpty: (rows) => rows.length === 0,
        emptyMessage: () => t('weekly.empty'),
        // Include the UTC day so the "today" marker moves at midnight even if the data does not change.
        signature: (rows) => `${new Date().getUTCDay()}|${JSON.stringify(rows)}`,
        render: (content, rows) => replaceContent(content, buildTable(rows)),
    });
}

function buildTable(rows) {
    // getUTCDay() counts from Sunday = 0; the table starts on Monday.
    const todayIndex = (new Date().getUTCDay() + 6) % 7;
    const valueRows = rows.map((row) => DAYS.map((day) => (typeof row[day] === 'number' && Number.isFinite(row[day]) ? row[day] : null)));
    const shade = createShadeScale(valueRows.flat().filter((value) => value !== null));

    return dataTable({
        className: 'data-table--weekly',
        caption: t('weekly.caption'),
        columns: [
            { label: t('endpoints.endpoint'), className: 'cell-route' },
            ...DAYS.map((_, index) => dayColumn(index, index === todayIndex)),
        ],
        rows: rows.map((row, index) => el('tr', {}, [
            el('td', { className: 'cell-route' }, [methodAndRoute(row.method, row.endpoint)]),
            ...valueRows[index].map((value, dayIndex) => heatCell(value, shade, dayIndex)),
        ])),
    });
}

// Latencies range from ~1 ms to several seconds, so a linear scale would leave every fast endpoint
// the same pale color. A logarithmic scale over all cells keeps both slow endpoints and
// day-to-day changes visible. Returns a function mapping a duration to 0..1.
function createShadeScale(values) {
    if (values.length === 0) {
        return () => 0;
    }
    const low = Math.log1p(Math.min(...values));
    const high = Math.log1p(Math.max(...values));
    return (value) => (high > low ? (Math.log1p(value) - low) / (high - low) : 0.5);
}

function dayColumn(index, isToday) {
    const fullName = formatWeekday(index, 'long');
    if (!isToday) {
        return { label: formatWeekday(index, 'short'), className: 'day', title: fullName };
    }
    // Today's column is still filling up, so it is marked (dot via CSS, words for screen readers).
    return {
        label: formatWeekday(index, 'short'),
        className: 'day day--today',
        title: `${fullName} · ${t('weekly.today')}`,
        extra: el('span', { className: 'visually-hidden', text: ` (${t('weekly.today')})` }),
    };
}

function heatCell(value, shade, dayIndex) {
    const dayName = formatWeekday(dayIndex, 'long');
    if (value === null) {
        return el('td', { className: 'heat heat--empty', attrs: { title: `${dayName}: ${t('weekly.noData')}` } }, [
            el('span', { text: '–', attrs: { 'aria-hidden': 'true' } }),
            el('span', { className: 'visually-hidden', text: t('weekly.noData') }),
        ]);
    }
    const cell = el('td', { className: 'heat', text: formatDuration(value), attrs: { title: `${dayName}: ${formatDuration(value)}` } });
    // Set through the CSSOM (not a style attribute), which the Content-Security-Policy allows.
    cell.style.setProperty('--intensity', shade(value).toFixed(3));
    return cell;
}
