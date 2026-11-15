// Desktop settings (Views/Settings/Index.cshtml): switches the Jira form between password and
// token, and runs "Test connection" without saving (POST /settings/jira/test).
(function (document) {
    'use strict';

    const form = document.querySelector('[data-settings-jira]');
    if (!form) {
        return;
    }

    const result = form.querySelector('[data-settings-result]');
    const secret = form.querySelector('#jira-secret');

    function kind() {
        return form.querySelector('[data-settings-kind]:checked')?.value || 'Password';
    }

    function showKind() {
        const current = kind();
        document.querySelectorAll('[data-kind-only]').forEach(function (element) {
            element.hidden = element.dataset.kindOnly !== current;
        });
        secret?.setAttribute('autocomplete', current === 'Token' ? 'off' : 'current-password');
    }

    function show(message, ok) {
        result.textContent = message || '';
        result.classList.toggle('is-error', ok === false);
        result.classList.toggle('is-success', ok === true);
    }

    form.querySelectorAll('[data-settings-kind]').forEach(function (radio) {
        radio.addEventListener('change', showKind);
    });

    form.querySelector('[data-settings-jira-test]')?.addEventListener('click', async function (event) {
        const button = event.currentTarget;
        button.disabled = true;
        show(form.dataset.testingText);
        try {
            const response = await fetch(form.dataset.testUrl, {
                method: 'POST',
                body: new FormData(form),
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            });
            const data = await response.json();
            show(data.message, Boolean(data.ok));
        } catch (error) {
            show(form.dataset.testFailedText, false);
        } finally {
            button.disabled = false;
        }
    });

    showKind();
})(document);
