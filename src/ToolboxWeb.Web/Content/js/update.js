// Desktop update page (Views/Desktop/Update.cshtml): "Update now" asks the app to download the new
// version (DesktopUpdateService), shows the progress, then lets the app quit and come back as the
// new version. This window waits for it and reloads itself.
(function () {
    'use strict';

    const root = document.querySelector('[data-update]');
    const button = root && root.querySelector('[data-update-start]');
    if (!root || !button) {
        return;
    }

    const wait = root.querySelector('[data-update-wait]');
    const text = root.querySelector('[data-update-text]');
    const errorBox = root.querySelector('[data-update-error]');
    const errorText = root.querySelector('[data-update-error-text]');
    const token = (root.querySelector('input[name="__RequestVerificationToken"]') || {}).value;
    const headers = { 'X-Requested-With': 'XMLHttpRequest', 'RequestVerificationToken': token };
    const errors = { 'Update.Error.NoPackage': 'textNoPackage', 'Update.Error.Unreachable': 'textUnreachable', 'Update.Error.Failed': 'textFailed' };

    function show(message) {
        wait.hidden = false;
        errorBox.hidden = true;
        text.textContent = message;
    }

    function fail(key) {
        wait.hidden = true;
        errorBox.hidden = false;
        errorText.textContent = root.dataset[errors[key] || 'textFailed'];
        button.disabled = false;
    }

    async function post(url) {
        const response = await fetch(url, { method: 'POST', headers: headers });
        if (!response.ok) {
            throw new Error(String(response.status));
        }
        return response.json();
    }

    async function waitForNewVersion(oldBuild) {
        show(root.dataset.textApplying);
        // The app goes away for a moment; it is back once /app/version answers with another build.
        for (let i = 0; i < 180; i++) {
            await new Promise(function (resolve) { setTimeout(resolve, 1000); });
            try {
                const response = await fetch('/app/version', { cache: 'no-store', headers: { 'X-Requested-With': 'XMLHttpRequest' } });
                const data = response.ok ? await response.json() : null;
                if (data && data.build && data.build !== oldBuild) {
                    show(root.dataset.textBack);
                    window.location.href = '/';
                    return;
                }
            } catch (error) {
                // Still restarting.
            }
        }
        fail('Update.Error.Failed');
    }

    async function follow() {
        for (;;) {
            const response = await fetch(root.dataset.statusUrl, { cache: 'no-store', headers: headers });
            const progress = await response.json();
            if (progress.stage === 'Checking') {
                show(root.dataset.textChecking);
            } else if (progress.stage === 'Downloading') {
                show(root.dataset.textDownloading.replace('{0}', progress.percent));
            } else if (progress.stage === 'Ready') {
                show(root.dataset.textReady);
                const applied = await post(root.dataset.applyUrl);
                await waitForNewVersion(applied.build);
                return;
            } else if (progress.stage === 'NoUpdate' || progress.stage === 'Failed') {
                fail(progress.errorKey);
                return;
            }
            await new Promise(function (resolve) { setTimeout(resolve, 700); });
        }
    }

    button.addEventListener('click', async function () {
        button.disabled = true;
        show(root.dataset.textChecking);
        try {
            await post(root.dataset.startUrl);
            await follow();
        } catch (error) {
            fail('Update.Error.Failed');
        }
    });
})();
