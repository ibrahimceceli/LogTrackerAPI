// Client for the /api/reports endpoints: adds the optional X-Api-Key header, applies a timeout
// and turns every failure into an ApiError with a `kind` the UI can translate.
import { readSetting, removeSetting, writeSetting } from './storage.js';

// Relative URL so the dashboard also works when the API is hosted under a path prefix.
const REPORTS_BASE = 'api/reports/';
const API_KEY_STORAGE_KEY = 'logtracker.apiKey';
const REQUEST_TIMEOUT_MS = 15_000;
const MAX_AUTH_ATTEMPTS = 3;

let apiKey = readSetting(API_KEY_STORAGE_KEY) ?? '';
let unauthorizedHandler = async () => false;

export class ApiError extends Error {
    /**
     * @param {'network'|'timeout'|'unauthorized'|'notFound'|'http'|'badResponse'} kind
     * @param {{ status?: number, problem?: object|null }} details
     */
    constructor(kind, { status = 0, problem = null } = {}) {
        super(status ? `${kind} (HTTP ${status})` : kind);
        this.name = 'ApiError';
        this.kind = kind;
        this.status = status;
        // RFC 7807 ProblemDetails body, when the server sent one.
        this.problem = problem;
    }

    get traceId() {
        const traceId = this.problem?.traceId;
        return typeof traceId === 'string' ? traceId : null;
    }
}

export function hasApiKey() {
    return apiKey !== '';
}

export function setApiKey(key) {
    apiKey = key.trim();
    // If storage is blocked the key still works until the tab is closed.
    writeSetting(API_KEY_STORAGE_KEY, apiKey);
}

export function forgetApiKey() {
    apiKey = '';
    removeSetting(API_KEY_STORAGE_KEY);
}

/**
 * Registers the function that runs when the API answers 401.
 * It receives { rejectedKey } (true when a key was sent but refused) and resolves to true
 * when a new key was set and the request should be retried.
 */
export function onUnauthorized(handler) {
    unauthorizedHandler = handler;
}

/** GET a report, e.g. getReport('summary', { hours: 24 }, signal). */
export async function getReport(path, params = {}, signal) {
    const url = new URL(REPORTS_BASE + path, document.baseURI);
    for (const [name, value] of Object.entries(params)) {
        url.searchParams.set(name, String(value));
    }

    for (let attempt = 1; attempt <= MAX_AUTH_ATTEMPTS; attempt++) {
        const keyUsed = apiKey;
        const response = await send(url, keyUsed, signal);

        if (response.status === 401) {
            // Several sections refresh in parallel; another one may already have obtained a new key.
            if (apiKey !== keyUsed) continue;
            const retry = attempt < MAX_AUTH_ATTEMPTS && await unauthorizedHandler({ rejectedKey: keyUsed !== '' });
            if (retry) continue;
            throw new ApiError('unauthorized', { status: 401, problem: await readProblem(response) });
        }
        if (!response.ok) {
            const kind = response.status === 404 ? 'notFound' : 'http';
            throw new ApiError(kind, { status: response.status, problem: await readProblem(response) });
        }
        try {
            return await response.json();
        } catch {
            throw new ApiError('badResponse', { status: response.status });
        }
    }
    throw new ApiError('unauthorized', { status: 401 });
}

async function send(url, key, signal) {
    const headers = { Accept: 'application/json' };
    if (key) {
        headers['X-Api-Key'] = key;
    }
    const timeout = AbortSignal.timeout(REQUEST_TIMEOUT_MS);
    const combined = signal ? AbortSignal.any([signal, timeout]) : timeout;
    try {
        return await fetch(url, { headers, signal: combined, cache: 'no-store' });
    } catch (error) {
        // The caller cancelled on purpose (a newer refresh replaced this one): pass the AbortError on.
        if (signal?.aborted) throw error;
        if (timeout.aborted) throw new ApiError('timeout');
        throw new ApiError('network');
    }
}

async function readProblem(response) {
    try {
        const body = await response.json();
        return body && typeof body === 'object' ? body : null;
    } catch {
        return null;
    }
}
