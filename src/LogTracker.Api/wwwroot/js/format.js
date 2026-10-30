// Number and date formatting through Intl, always in the language chosen in the header.
import { getLanguage } from './i18n.js';

const formatterCache = new Map();

function intlLocale() {
    const language = getLanguage();
    // Keep the user's regional conventions (e.g. en-US vs en-GB) when they match the chosen language.
    const preferred = (navigator.languages ?? []).find((tag) => tag.toLowerCase().startsWith(language));
    return preferred ?? (language === 'tr' ? 'tr-TR' : 'en-GB');
}

// Creating Intl formatters is relatively expensive and the dashboard formats hundreds of values
// every refresh, so each formatter is created once per locale and reused.
function cached(name, factory) {
    const locale = intlLocale();
    const key = `${locale}|${name}`;
    if (!formatterCache.has(key)) {
        formatterCache.set(key, factory(locale));
    }
    return formatterCache.get(key);
}

const isNumber = (value) => typeof value === 'number' && Number.isFinite(value);

export function formatInteger(value) {
    if (!isNumber(value)) return '–';
    return cached('integer', (locale) => new Intl.NumberFormat(locale, { maximumFractionDigits: 0 })).format(value);
}

export function formatDecimal(value) {
    if (!isNumber(value)) return '–';
    return cached('decimal', (locale) => new Intl.NumberFormat(locale, { maximumFractionDigits: 1 })).format(value);
}

/** Formats a value that is already a percentage (12.5 means 12.5 %). */
export function formatPercent(value) {
    if (!isNumber(value)) return '–';
    return cached('percent', (locale) => new Intl.NumberFormat(locale, { style: 'percent', maximumFractionDigits: 2 }))
        .format(value / 100);
}

/** Durations below 10 s are shown in ms, longer ones in seconds. */
export function formatDuration(milliseconds) {
    if (!isNumber(milliseconds)) return '–';
    if (milliseconds >= 10_000) {
        return cached('seconds', (locale) => new Intl.NumberFormat(locale, {
            style: 'unit', unit: 'second', unitDisplay: 'short', maximumFractionDigits: 1,
        })).format(milliseconds / 1000);
    }
    const digits = milliseconds < 10 ? 1 : 0;
    return cached(`ms${digits}`, (locale) => new Intl.NumberFormat(locale, {
        style: 'unit', unit: 'millisecond', unitDisplay: 'short', maximumFractionDigits: digits,
    })).format(milliseconds);
}

/** Parses an ISO-8601 timestamp from the API; returns null for missing or invalid values. */
export function parseTimestamp(value) {
    const time = typeof value === 'string' ? Date.parse(value) : NaN;
    return Number.isNaN(time) ? null : new Date(time);
}

export function formatTime(date) {
    if (!date) return '–';
    return cached('time', (locale) => new Intl.DateTimeFormat(locale, {
        hour: '2-digit', minute: '2-digit', second: '2-digit',
    })).format(date);
}

/** Short timestamp for table rows: only the time for today, date and time otherwise. */
export function formatTimestamp(date) {
    if (!date) return '–';
    if (date.toDateString() === new Date().toDateString()) {
        return formatTime(date);
    }
    return cached('timestamp', (locale) => new Intl.DateTimeFormat(locale, {
        day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit', second: '2-digit',
    })).format(date);
}

/** Long timestamp including the time zone, used in tooltips and the error dialog. */
export function formatFullDateTime(date) {
    if (!date) return '–';
    return cached('full', (locale) => new Intl.DateTimeFormat(locale, { dateStyle: 'full', timeStyle: 'long' })).format(date);
}

/** Axis label for an hourly bucket; the weekday is added when the chart spans several days. */
export function formatHour(date, withWeekday) {
    const name = withWeekday ? 'hourWithDay' : 'hour';
    return cached(name, (locale) => new Intl.DateTimeFormat(locale, withWeekday
        ? { weekday: 'short', hour: '2-digit', minute: '2-digit' }
        : { hour: '2-digit', minute: '2-digit' })).format(date);
}

/** "Tue, 28 Oct, 14:00 – 15:00" for chart tooltips and the chart's table view. */
export function formatHourRange(start) {
    const end = new Date(start.getTime() + 3_600_000);
    return cached('hourRange', (locale) => new Intl.DateTimeFormat(locale, {
        weekday: 'short', day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit',
    })).formatRange(start, end);
}

/** Weekday name where 0 = Monday (the order of the weekly-trend columns). */
export function formatWeekday(index, style) {
    // 1 January 2024 was a Monday; formatting in UTC keeps the result independent of the local time zone.
    const date = new Date(Date.UTC(2024, 0, 1 + index));
    return cached(`weekday-${style}`, (locale) => new Intl.DateTimeFormat(locale, { weekday: style, timeZone: 'UTC' })).format(date);
}
