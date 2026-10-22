// Personal overview (stage 5.5): panels the visitor hid stay hidden in this browser, and the
// Jira hours card fills itself from /Jira/TodayHours when a Jira session is open.
(function (window, document) {
    'use strict';

    const root = document.querySelector('[data-dashboard]');
    if (!root) {
        return;
    }

    const STORAGE_KEY = 'toolbox.dashboard.hidden';

    function readHidden() {
        try {
            const value = JSON.parse(window.localStorage.getItem(STORAGE_KEY) || '[]');
            return Array.isArray(value) ? value : [];
        } catch {
            return [];
        }
    }

    function writeHidden(ids) {
        try {
            window.localStorage.setItem(STORAGE_KEY, JSON.stringify(ids));
        } catch {
            // Storage can be off; the choice then lasts until the page reloads.
        }
    }

    let hidden = readHidden();

    function applyHidden() {
        root.querySelectorAll('[data-dash-panel]').forEach(function (panel) {
            panel.hidden = hidden.includes(panel.dataset.dashPanel);
        });
        document.querySelectorAll('[data-dash-toggle]').forEach(function (box) {
            box.checked = !hidden.includes(box.dataset.dashToggle);
        });
    }

    document.querySelectorAll('[data-dash-toggle]').forEach(function (box) {
        box.addEventListener('change', function () {
            const id = box.dataset.dashToggle;
            hidden = box.checked ? hidden.filter(function (x) { return x !== id; }) : hidden.concat(id);
            writeHidden(hidden);
            applyHidden();
        });
    });

    applyHidden();

    // ------------------------------------------------------------------ Jira hours
    const card = root.querySelector('[data-jira-hours]');
    if (!card) {
        return;
    }

    let text = {};
    try {
        text = JSON.parse(document.getElementById('dashboard-config')?.textContent || '{}');
    } catch {
        text = {};
    }

    const format = window.Toolbox?.format;
    const hours = function (value) { return format ? format.hours(value) : value + 'h'; };
    const fill = function (value, sub, ratio, state) {
        card.querySelector('[data-jira-hours-value]').textContent = value;
        card.querySelector('[data-jira-hours-sub]').textContent = sub;
        card.querySelector('[data-jira-hours-bar]').style.width = Math.round(Math.min(1, ratio) * 100) + '%';
        card.classList.toggle('is-warning', state === 'short');
        card.classList.toggle('is-success', state === 'done');
        card.setAttribute('aria-busy', 'false');
    };
    const template = function (value, arg) { return String(value || '').split('{0}').join(arg); };

    fetch(card.dataset.url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
        .then(function (response) { return response.json(); })
        .then(function (payload) {
            if (!payload || !payload.success) {
                // No session any more: the card has nothing to say.
                if (payload && payload.requiresLogin) {
                    card.hidden = true;
                    return;
                }

                fill('–', payload?.message || text.failed, 0);
                return;
            }

            const logged = Number(payload.data?.hours) || 0;
            const target = Number(payload.data?.target) || 0;
            if (target <= 0) {
                fill(hours(logged), text.dayOff, 0);
            } else if (logged >= target) {
                fill(hours(logged), text.done, 1, 'done');
            } else {
                // Short of the day's hours only matters once the afternoon is under way.
                const late = new Date().getHours() >= 16;
                fill(hours(logged), template(text.left, hours(target - logged)) + ' · ' + template(text.target, hours(target)), logged / target, late ? 'short' : null);
            }
        })
        .catch(function () {
            fill('–', text.failed, 0);
        });
})(window, document);
