// Errors-per-hour chart from GET /api/reports/hourly-errors?hours=…
// The API only returns hours that had errors; the chart fills the gaps with zeros so the time axis
// stays continuous, and labels every hour in the browser's local time.
import { getReport } from '../api.js';
import { dataTable, expectArray, shortTypeName } from '../components.js';
import { el } from '../dom.js';
import { formatHour, formatHourRange, formatInteger } from '../format.js';
import { t } from '../i18n.js';
import { CHART_UNAVAILABLE, createSection } from '../section.js';

const HOUR_MS = 3_600_000;
// The categorical palette has 8 colors; with more exception types the smallest ones are folded into "Other".
const PALETTE_SIZE = 8;
const OTHER = Symbol('other');

// Colors follow the exception type, not its rank: once a type got a color it keeps it for the session.
const colorSlotByType = new Map();

export function createHourlyErrorsSection({ element, getRangeHours, onEnterApiKey }) {
    let chart = null;
    let view = null;
    let series = null; // latest chart data, read by the tooltip callbacks

    function renderChart(content, buckets) {
        series = buildSeries(buckets, getRangeHours());

        // Build the chart once and update it afterwards: re-creating it every 5 s would flash and
        // would close the "show as table" panel.
        if (!view || !content.contains(view.root)) {
            chart?.destroy();
            chart = null;
            view = buildView();
            content.replaceChildren(view.root);
        }

        const multiDay = series.hours.length > 24;
        const labels = series.hours.map((hour) => formatHour(hour, multiDay));
        const datasets = series.series.map(toDataset);

        view.total.textContent = t('hourly.total', { value: formatInteger(series.total) });
        view.toggle.textContent = t('hourly.showTable');
        view.canvas.setAttribute('aria-label', t('hourly.chartLabel', {
            total: formatInteger(series.total),
            peak: formatInteger(series.peak.count),
            peakTime: formatHourRange(series.peak.hour),
        }));
        view.tableHolder.replaceChildren(buildTable(series));

        if (chart) {
            chart.data.labels = labels;
            chart.data.datasets = datasets;
            chart.update('none'); // no animation on routine refreshes
        } else {
            chart = new window.Chart(view.canvas, chartConfig(labels, datasets, () => series));
        }
    }

    return createSection({
        element,
        onEnterApiKey,
        load: async (signal) => {
            const buckets = expectArray(await getReport('hourly-errors', { hours: getRangeHours() }, signal));
            if (!window.Chart) {
                throw new Error(CHART_UNAVAILABLE);
            }
            return buckets;
        },
        isEmpty: (buckets) => buckets.length === 0,
        emptyMessage: () => t('hourly.empty'),
        // The hour window moves forward even when the buckets stay the same.
        signature: (buckets) => `${getRangeHours()}|${Math.floor(Date.now() / HOUR_MS)}|${JSON.stringify(buckets)}`,
        render: renderChart,
    });
}

function buildView() {
    const total = el('p', { className: 'chart-total' });
    const canvas = el('canvas', { attrs: { role: 'img' } });
    const toggle = el('summary');
    const tableHolder = el('div');
    const root = el('div', {}, [
        total,
        el('div', { className: 'chart-box' }, [canvas]),
        el('details', { className: 'chart-table' }, [toggle, tableHolder]),
    ]);
    return { root, total, canvas, toggle, tableHolder };
}

/** Turns API buckets into one count array per exception type over a continuous range of hours. */
function buildSeries(buckets, rangeHours) {
    const currentHour = Math.floor(Date.now() / HOUR_MS) * HOUR_MS; // start of the current UTC hour
    let first = currentHour - (rangeHours - 1) * HOUR_MS;
    let last = currentHour;

    const points = [];
    for (const bucket of buckets) {
        const time = Date.parse(bucket?.hourStartUtc);
        const count = Number(bucket?.count);
        if (Number.isNaN(time) || !Number.isFinite(count)) continue;
        const hour = Math.floor(time / HOUR_MS) * HOUR_MS;
        points.push({ hour, type: String(bucket.errorType ?? '?'), count });
        // Browser and server clocks can differ a little: widen the window instead of dropping a bucket.
        first = Math.min(first, hour);
        last = Math.max(last, hour);
    }

    const slotCount = (last - first) / HOUR_MS + 1;
    const hours = Array.from({ length: slotCount }, (_, index) => new Date(first + index * HOUR_MS));

    const totalsByType = new Map();
    for (const point of points) {
        totalsByType.set(point.type, (totalsByType.get(point.type) ?? 0) + point.count);
    }
    const typesByTotal = [...totalsByType.keys()].sort((a, b) => totalsByType.get(b) - totalsByType.get(a));
    const ownSeries = typesByTotal.length > PALETTE_SIZE ? typesByTotal.slice(0, PALETTE_SIZE - 1) : typesByTotal;
    for (const type of ownSeries) {
        if (!colorSlotByType.has(type) && colorSlotByType.size < PALETTE_SIZE) {
            colorSlotByType.set(type, colorSlotByType.size + 1);
        }
    }

    const countsByKey = new Map();
    const totalsPerHour = new Array(slotCount).fill(0);
    for (const point of points) {
        const key = ownSeries.includes(point.type) ? point.type : OTHER;
        if (!countsByKey.has(key)) {
            countsByKey.set(key, new Array(slotCount).fill(0));
        }
        const index = (point.hour - first) / HOUR_MS;
        countsByKey.get(key)[index] += point.count;
        totalsPerHour[index] += point.count;
    }

    // Stable stacking order: by color slot, "Other" on top.
    const seriesList = [...countsByKey.entries()]
        .map(([key, counts]) => ({ key, counts, slot: key === OTHER ? Infinity : (colorSlotByType.get(key) ?? PALETTE_SIZE + 1) }))
        .sort((a, b) => a.slot - b.slot);

    const peakIndex = totalsPerHour.indexOf(Math.max(...totalsPerHour));
    return {
        hours,
        series: seriesList,
        points: points.sort((a, b) => b.hour - a.hour || b.count - a.count),
        total: totalsPerHour.reduce((sum, count) => sum + count, 0),
        peak: { hour: hours[peakIndex], count: totalsPerHour[peakIndex] },
    };
}

function cssVar(name) {
    return getComputedStyle(document.documentElement).getPropertyValue(name).trim();
}

function toDataset({ key, counts, slot }) {
    const color = slot <= PALETTE_SIZE ? cssVar(`--series-${slot}`) : cssVar('--series-other');
    const isOther = key === OTHER;
    return {
        label: isOther ? t('hourly.other') : shortTypeName(key),
        fullName: isOther ? t('hourly.other') : key,
        data: counts,
        backgroundColor: color,
        hoverBackgroundColor: color,
        // A 2px line in the surface color separates stacked segments.
        borderColor: cssVar('--surface'),
        borderWidth: { top: 2, right: 0, bottom: 0, left: 0 },
        borderSkipped: 'start',
        borderRadius: roundTopSegment,
        maxBarThickness: 24,
        categoryPercentage: 0.85,
        barPercentage: 0.9,
    };
}

// Only the top segment of each stacked bar gets rounded corners.
function roundTopSegment(context) {
    const { chart, dataIndex, datasetIndex } = context;
    const hasSegmentAbove = chart.data.datasets.some((dataset, index) =>
        index > datasetIndex && chart.isDatasetVisible(index) && dataset.data[dataIndex] > 0);
    return hasSegmentAbove ? 0 : { topLeft: 4, topRight: 4 };
}

function chartConfig(labels, datasets, getSeries) {
    const Chart = window.Chart;
    Chart.defaults.font.family = cssVar('--font-sans');
    Chart.defaults.font.size = 12;

    const muted = cssVar('--text-muted');
    const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    return {
        type: 'bar',
        data: { labels, datasets },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            animation: reduceMotion ? false : { duration: 400 },
            interaction: { mode: 'index', intersect: false },
            scales: {
                x: {
                    stacked: true,
                    grid: { display: false },
                    border: { color: cssVar('--chart-axis') },
                    ticks: { color: muted, maxRotation: 0, autoSkip: true, autoSkipPadding: 16, font: { size: 11 } },
                },
                y: {
                    stacked: true,
                    beginAtZero: true,
                    border: { display: false },
                    grid: { color: cssVar('--chart-grid') },
                    ticks: { color: muted, precision: 0, maxTicksLimit: 5, font: { size: 11 } },
                },
            },
            plugins: {
                legend: {
                    position: 'top',
                    align: 'start',
                    labels: {
                        color: cssVar('--text-secondary'),
                        usePointStyle: true,
                        pointStyle: 'rectRounded',
                        boxWidth: 8,
                        boxHeight: 8,
                        padding: 14,
                    },
                },
                tooltip: {
                    backgroundColor: cssVar('--bg'),
                    borderColor: cssVar('--border-strong'),
                    borderWidth: 1,
                    titleColor: cssVar('--text'),
                    bodyColor: cssVar('--text-secondary'),
                    footerColor: cssVar('--text'),
                    padding: 10,
                    cornerRadius: 8,
                    usePointStyle: true,
                    boxPadding: 4,
                    filter: (item) => item.raw > 0,
                    itemSort: (a, b) => b.raw - a.raw,
                    callbacks: {
                        title: (items) => (items.length ? formatHourRange(getSeries().hours[items[0].dataIndex]) : ''),
                        label: (item) => ` ${item.dataset.fullName}: ${formatInteger(item.raw)}`,
                        footer: (items) => t('hourly.tooltipTotal', {
                            value: formatInteger(items.reduce((sum, item) => sum + item.raw, 0)),
                        }),
                    },
                },
            },
        },
    };
}

// Accessible alternative to the canvas: the same numbers as a table (only hours with errors).
function buildTable({ points }) {
    const rows = points.map((point) => el('tr', {}, [
        el('td', { text: formatHourRange(new Date(point.hour)) }),
        el('td', { className: 'mono cell-grow', text: point.type, attrs: { title: point.type } }),
        el('td', { className: 'num', text: formatInteger(point.count) }),
    ]));
    return dataTable({
        className: 'data-table--chart',
        caption: t('hourly.tableCaption'),
        columns: [
            { label: t('hourly.hour') },
            { label: t('hourly.type'), className: 'cell-grow' },
            { label: t('hourly.count'), className: 'num' },
        ],
        rows,
    });
}
