// "Open in a new tab" of the code playground (Views/CodePlayground/Run.cshtml). The page asks
// the tab that opened it for the code, accepts it only from that tab and from Toolkit's own
// origin, and shows it in a sandboxed frame without allow-same-origin.
(function () {
    'use strict';

    const message = document.querySelector('[data-pg-run-message]');
    const frame = document.querySelector('[data-pg-run-frame]');
    const opener = window.opener;

    function noOpener() {
        message.textContent = message.dataset.noOpener || '';
    }

    if (!opener) {
        noOpener();
        return;
    }

    window.addEventListener('message', function (event) {
        const data = event.data;
        if (event.origin !== window.location.origin || event.source !== opener || !data || data.type !== 'tbx-playground-document') {
            return;
        }

        if (data.title) {
            document.title = data.title;
        }
        frame.srcdoc = String(data.doc || '');
        frame.hidden = false;
        message.hidden = true;
    });

    try {
        opener.postMessage({ type: 'tbx-playground-ready' }, window.location.origin);
    } catch (error) {
        noOpener();
        return;
    }

    window.setTimeout(function () {
        if (frame.hidden) {
            noOpener();
        }
    }, 4000);
})();
