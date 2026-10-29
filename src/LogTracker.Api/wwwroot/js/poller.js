// Runs `task` every `intervalMs`, but only after the previous run has finished (a chained
// setTimeout instead of setInterval), so slow responses can never pile up overlapping requests.
// Polling stops while the tab is hidden and when the user presses "Pause".
export function createPoller(task, intervalMs, onStateChange = () => {}) {
    let timer = null;
    let running = false;
    let runAgain = false;
    let pausedByUser = false;

    async function run() {
        clearTimeout(timer);
        timer = null;
        if (running) {
            // Someone asked for a refresh during a run: do one more right after it instead of overlapping.
            runAgain = true;
            return;
        }
        running = true;
        try {
            await task();
        } catch (error) {
            console.error('Refresh failed', error);
        } finally {
            running = false;
            if (runAgain) {
                runAgain = false;
                run();
            } else {
                scheduleNext();
            }
        }
    }

    function scheduleNext() {
        clearTimeout(timer);
        timer = null;
        if (!pausedByUser && !document.hidden) {
            timer = setTimeout(run, intervalMs);
        }
    }

    document.addEventListener('visibilitychange', () => {
        if (document.hidden) {
            clearTimeout(timer);
            timer = null;
        } else if (!pausedByUser && !running) {
            // Coming back to the tab: show fresh data immediately instead of waiting a full interval.
            run();
        }
        onStateChange();
    });

    return {
        start: run,
        /** Refreshes once now (also while paused); automatic polling continues only if not paused. */
        refreshNow: run,
        pause() {
            pausedByUser = true;
            clearTimeout(timer);
            timer = null;
            onStateChange();
        },
        resume() {
            pausedByUser = false;
            run();
            onStateChange();
        },
        get paused() {
            return pausedByUser;
        },
    };
}
