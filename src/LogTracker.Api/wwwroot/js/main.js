// Entry point: creates the sections, wires the header controls and starts polling.
import { forgetApiKey, hasApiKey, onUnauthorized, setApiKey } from './api.js';
import { promptForApiKey } from './api-key-dialog.js';
import { openErrorDetails, repaintErrorDetails } from './error-dialog.js';
import { formatTime } from './format.js';
import { applyStaticTranslations, getLanguage, onLanguageChange, setLanguage, t } from './i18n.js';
import { createPoller } from './poller.js';
import { createEndpointsSection } from './sections/endpoints.js';
import { createHourlyErrorsSection } from './sections/hourly-errors.js';
import { createRecentErrorsSection } from './sections/recent-errors.js';
import { createRecentRequestsSection } from './sections/recent-requests.js';
import { createSummarySection } from './sections/summary.js';
import { createWeeklyTrendSection } from './sections/weekly-trend.js';
import { readSetting, writeSetting } from './storage.js';

const REFRESH_INTERVAL_MS = 5_000;
const RANGE_STORAGE_KEY = 'logtracker.rangeHours';
const RANGE_OPTIONS = [24, 168];

const STATUS_LABELS = {
    loading: 'status.connecting',
    live: 'status.live',
    degraded: 'status.degraded',
    offline: 'status.offline',
    locked: 'status.locked',
    paused: 'status.paused',
};

const byId = (id) => document.getElementById(id);
const statusPill = byId('live-status');
const statusText = byId('live-status-text');
const lastUpdatedTime = byId('last-updated');
const pauseButton = byId('pause-button');
const pauseButtonText = byId('pause-button-text');
const forgetKeyButton = byId('forget-key-button');
const rangeButtons = [...document.querySelectorAll('[data-range]')];
const languageButtons = [...document.querySelectorAll('[data-language]')];

const savedRange = Number(readSetting(RANGE_STORAGE_KEY));
let rangeHours = RANGE_OPTIONS.includes(savedRange) ? savedRange : 24;
let connection = 'loading';
let lastUpdated = null;
// After the user cancels the API key dialog it is not shown again on every poll;
// the sections show an "Enter API key" button instead.
let apiKeyPromptDismissed = false;

const getRangeHours = () => rangeHours;

const summary = createSummarySection({ element: byId('summary'), getRangeHours, onEnterApiKey: enterApiKey });
const hourlyErrors = createHourlyErrorsSection({ element: byId('hourly-errors'), getRangeHours, onEnterApiKey: enterApiKey });
const endpoints = createEndpointsSection({ element: byId('endpoints'), getRangeHours, onEnterApiKey: enterApiKey });
const weeklyTrend = createWeeklyTrendSection({ element: byId('weekly-trend'), onEnterApiKey: enterApiKey });
const recentRequests = createRecentRequestsSection({ element: byId('recent-requests'), onEnterApiKey: enterApiKey });
const recentErrors = createRecentErrorsSection({
    element: byId('recent-errors'),
    onEnterApiKey: enterApiKey,
    onOpenDetails: openErrorDetails,
});

const allSections = [summary, hourlyErrors, endpoints, weeklyTrend, recentRequests, recentErrors];
const rangeSections = [summary, hourlyErrors, endpoints];

const poller = createPoller(refreshAll, REFRESH_INTERVAL_MS, renderStatus);

async function refreshAll() {
    // allSettled: even an unexpected exception in one section must not stop the others.
    const results = await Promise.allSettled(allSections.map((section) => section.refresh()));
    const outcomes = results
        .map((result) => (result.status === 'fulfilled' ? result.value : 'failed'))
        .filter((outcome) => outcome !== 'superseded');
    const succeeded = outcomes.filter((outcome) => outcome === 'ok').length;
    const unauthorized = outcomes.filter((outcome) => outcome === 'unauthorized').length;

    if (outcomes.length > 0) {
        if (succeeded === outcomes.length) connection = 'live';
        else if (succeeded > 0) connection = 'degraded';
        else connection = unauthorized > 0 ? 'locked' : 'offline';
    }
    if (succeeded > 0) {
        lastUpdated = new Date();
    }
    renderStatus();
}

// ---------- Header: live status, pause, API key ----------

function renderStatus() {
    const state = poller.paused ? 'paused' : connection;
    statusPill.dataset.state = state;
    // The pill is a live region: only touch it when the text really changes, so it is not re-announced every 5 s.
    setText(statusText, t(STATUS_LABELS[state]));

    setText(lastUpdatedTime, lastUpdated ? formatTime(lastUpdated) : '–');
    if (lastUpdated) {
        lastUpdatedTime.dateTime = lastUpdated.toISOString();
    }

    pauseButton.dataset.paused = String(poller.paused);
    setText(pauseButtonText, t(poller.paused ? 'actions.resume' : 'actions.pause'));
    pauseButton.title = poller.paused
        ? t('actions.resumeHint')
        : t('actions.pauseHint', { seconds: REFRESH_INTERVAL_MS / 1000 });

    forgetKeyButton.hidden = !hasApiKey();
}

function setText(element, text) {
    if (element.textContent !== text) {
        element.textContent = text;
    }
}

pauseButton.addEventListener('click', () => {
    if (poller.paused) poller.resume(); else poller.pause();
});

onUnauthorized(async ({ rejectedKey }) => {
    if (apiKeyPromptDismissed) {
        return false;
    }
    const key = await promptForApiKey({ rejected: rejectedKey });
    if (key === null) {
        apiKeyPromptDismissed = true;
        return false;
    }
    setApiKey(key);
    renderStatus();
    return true;
});

async function enterApiKey() {
    const key = await promptForApiKey();
    if (key === null) {
        return;
    }
    apiKeyPromptDismissed = false;
    setApiKey(key);
    renderStatus();
    poller.refreshNow();
}

forgetKeyButton.addEventListener('click', () => {
    forgetApiKey();
    // Lock the dashboard instead of asking again right away (useful on a shared computer).
    apiKeyPromptDismissed = true;
    allSections.forEach((section) => section.reset());
    renderStatus();
    pauseButton.focus(); // the forget button just disappeared
    poller.refreshNow();
});

// ---------- Time range (summary, chart and endpoint table) ----------

function renderRange() {
    for (const button of rangeButtons) {
        button.setAttribute('aria-pressed', String(Number(button.dataset.range) === rangeHours));
    }
    for (const caption of document.querySelectorAll('[data-range-caption]')) {
        caption.textContent = t(rangeHours === 24 ? 'range.last24h' : 'range.last7d');
    }
}

for (const button of rangeButtons) {
    button.addEventListener('click', () => {
        const hours = Number(button.dataset.range);
        if (hours === rangeHours) return;
        rangeHours = hours;
        writeSetting(RANGE_STORAGE_KEY, String(hours));
        renderRange();
        // Old numbers belong to the other range, so show the loading state rather than stale values.
        for (const section of rangeSections) {
            section.reset();
            section.refresh();
        }
    });
}

// ---------- Language ----------

function renderLanguageToggle() {
    for (const button of languageButtons) {
        button.setAttribute('aria-pressed', String(button.dataset.language === getLanguage()));
    }
}

for (const button of languageButtons) {
    button.addEventListener('click', () => setLanguage(button.dataset.language));
}

onLanguageChange(() => {
    renderLanguageToggle();
    renderRange();
    renderStatus();
    allSections.forEach((section) => section.paint());
    repaintErrorDetails();
});

// ---------- Start ----------

applyStaticTranslations();
renderLanguageToggle();
renderRange();
renderStatus();
allSections.forEach((section) => section.paint());
poller.start();
