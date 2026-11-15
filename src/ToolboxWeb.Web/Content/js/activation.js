// Desktop activation screen (Views/Activation/Index.cshtml): while a request waits for the
// owner's approval, asks /activate/status every 10 seconds; once approved, opens the app.
(function () {
    'use strict';

    const root = document.querySelector('[data-activation]');
    if (!root || root.dataset.pending !== 'true') {
        return;
    }

    const url = root.dataset.statusUrl;
    const waitText = root.querySelector('[data-activation-wait-text]');
    let busy = false;

    async function poll() {
        if (busy || document.visibilityState === 'hidden') {
            return;
        }
        busy = true;
        try {
            const response = await fetch(url, { cache: 'no-store', headers: { 'X-Requested-With': 'XMLHttpRequest' } });
            const data = response.ok ? await response.json() : null;
            if (data && data.active) {
                window.location.href = '/';
                return;
            }
            if (data && data.activation !== 'pending') {
                // Rejected, or the server forgot the request: show the page again with the reason.
                window.location.reload();
                return;
            }
            if (waitText && data && data.message) {
                waitText.textContent = data.message;
            }
        } catch (error) {
            // This computer's own app restarting: the next round tries again.
        } finally {
            busy = false;
        }
    }

    window.setInterval(poll, 10000);
    document.addEventListener('visibilitychange', function () {
        if (document.visibilityState === 'visible') {
            poll();
        }
    });
})();
