// Small wrappers around localStorage.
// Reading or writing localStorage can throw (private browsing, blocked site data, full quota),
// so every access goes through these helpers and the dashboard keeps working without storage.

export function readSetting(key) {
    try {
        return window.localStorage.getItem(key);
    } catch {
        return null;
    }
}

export function writeSetting(key, value) {
    try {
        window.localStorage.setItem(key, value);
        return true;
    } catch {
        return false;
    }
}

export function removeSetting(key) {
    try {
        window.localStorage.removeItem(key);
    } catch {
        // Nothing to clean up if storage is unavailable.
    }
}
