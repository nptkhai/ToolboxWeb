(function (window, document) {
    "use strict";

    // Due-date changes made on the board and auto-schedule tabs live only in the browser until
    // they are written to Jira. This tracker counts them per tab, shows the floating bar, asks
    // before anything (switching tab, reloading data, leaving the page) would throw them away,
    // and runs "Write to Jira" for a tab that registered how to save (stage 5.3).
    const sources = {};
    const handlers = {};
    let leaving = false;

    function readConfig() {
        try {
            return JSON.parse(document.getElementById("jira-worklist-config")?.textContent || "{}");
        } catch {
            return {};
        }
    }

    const config = readConfig();
    const unsavedText = config.unsaved || {};
    const saveText = config.save || {};

    function format(template, ...values) {
        return values.reduce((text, value, index) => text.split(`{${index}}`).join(String(value)), String(template || ""));
    }

    function escapeHtml(value) {
        return window.ToolboxJira?.escapeHtml(value) ?? String(value ?? "");
    }

    function total() {
        return Object.values(sources).reduce((sum, value) => sum + value, 0);
    }

    // The tab whose changes the bar is about: the one with changes (tabs are exclusive, since
    // leaving a tab with changes asks first).
    function activeSource() {
        return Object.keys(sources).find((key) => sources[key] > 0) || null;
    }

    function renderBar() {
        const bar = document.querySelector("[data-jira-unsaved]");
        if (!bar) {
            return;
        }

        const count = total();
        const source = activeSource();
        bar.hidden = count === 0;
        bar.querySelector("[data-jira-unsaved-text]").textContent = format(unsavedText.message, count);
        const save = bar.querySelector("[data-jira-unsaved-save]");
        if (save) {
            save.hidden = !(source && handlers[source]?.changes);
        }
    }

    const tracker = {
        set(source, count) {
            sources[source] = Math.max(0, Number(count) || 0);
            renderBar();
        },
        count(source) {
            return source ? (sources[source] || 0) : total();
        },
        /**
         * A tab tells the bar how to undo and save its changes:
         * { discard(), changes() -> [{ issueKey, summary, dueDate, previousDueDate }], applyResults(results) }
         */
        register(source, sourceHandlers) {
            handlers[source] = sourceHandlers;
            renderBar();
        },
        unregister(source) {
            delete handlers[source];
            renderBar();
        },
        // Resolves true when there is nothing to lose, or the user agrees to discard it.
        confirmDiscard(source) {
            const count = tracker.count(source);
            if (count === 0) {
                return Promise.resolve(true);
            }

            const ask = window.Toolbox?.confirm
                ? window.Toolbox.confirm({ title: unsavedText.leaveTitle, message: format(unsavedText.leaveMessage, count), okText: unsavedText.leaveOk })
                : Promise.resolve(window.confirm(format(unsavedText.leaveMessage, count)));
            return ask.then((confirmed) => {
                if (confirmed) {
                    if (source) {
                        tracker.set(source, 0);
                    } else {
                        Object.keys(sources).forEach((key) => { sources[key] = 0; });
                        renderBar();
                    }
                }
                return confirmed;
            });
        }
    };

    window.ToolboxJiraUnsaved = tracker;

    window.addEventListener("beforeunload", function (event) {
        if (!leaving && total() > 0) {
            event.preventDefault();
            event.returnValue = "";
        }
    });

    // ------------------------------------------------------------------ write to Jira

    function displayDate(iso) {
        return iso ? window.ToolboxJira.displayDate(iso) : saveText.noDue;
    }

    function token() {
        return document.querySelector("input[name='__RequestVerificationToken']")?.value || "";
    }

    function openSave() {
        const source = activeSource();
        const sourceHandlers = source && handlers[source];
        const modalEl = document.querySelector("[data-jira-save-modal]");
        if (!sourceHandlers?.changes || !modalEl || !window.bootstrap) {
            return;
        }

        const changes = sourceHandlers.changes();
        if (!changes.length) {
            return;
        }

        const list = modalEl.querySelector("[data-jira-save-list]");
        const intro = modalEl.querySelector("[data-jira-save-intro]");
        const confirm = modalEl.querySelector("[data-jira-save-confirm]");
        const cancel = modalEl.querySelector("[data-jira-save-cancel]");

        intro.textContent = saveText.intro;
        list.innerHTML = changes.map((change) => `<li class="jira-save-item" data-key="${escapeHtml(change.issueKey)}">
                <span class="jira-save-key">${escapeHtml(change.issueKey)}</span>
                <span class="jira-save-summary">${escapeHtml(change.summary || "")}</span>
                <span class="jira-save-dates">${escapeHtml(displayDate(change.previousDueDate))} → <strong>${escapeHtml(displayDate(change.dueDate))}</strong></span>
                <span class="jira-save-result" data-result></span>
            </li>`).join("");
        confirm.hidden = false;
        confirm.disabled = false;
        confirm.textContent = format(saveText.confirm, changes.length);
        cancel.textContent = cancel.dataset.cancelText || cancel.textContent;
        cancel.dataset.cancelText = cancel.textContent;

        confirm.onclick = async function () {
            confirm.disabled = true;
            intro.textContent = saveText.saving;
            let results = [];
            try {
                const response = await fetch(saveText.url, {
                    method: "POST",
                    headers: {
                        "Content-Type": "application/json",
                        "RequestVerificationToken": token(),
                        "X-Requested-With": "XMLHttpRequest"
                    },
                    body: JSON.stringify({
                        changes: changes.map((change) => ({
                            issueKey: change.issueKey,
                            dueDate: change.dueDate || null,
                            previousDueDate: change.previousDueDate || null
                        }))
                    })
                });
                const payload = await response.json().catch(() => null);
                if (payload?.requiresLogin) {
                    leaving = true;
                    window.location.href = `${config.loginUrl || "/Identity/Account/Login"}?returnUrl=${encodeURIComponent(window.location.pathname)}`;
                    return;
                }

                results = response.ok && payload?.success
                    ? payload.data || []
                    : changes.map((change) => ({ issueKey: change.issueKey, success: false, message: payload?.message || saveText.failed }));
            } catch {
                results = changes.map((change) => ({ issueKey: change.issueKey, success: false, message: saveText.failed }));
            }

            // One line per issue, then the tab updates its cards.
            results.forEach((result) => {
                const item = list.querySelector(`[data-key="${CSS.escape(result.issueKey)}"]`);
                const cell = item?.querySelector("[data-result]");
                item?.classList.toggle("is-ok", !!result.success);
                item?.classList.toggle("is-error", !result.success);
                if (cell) {
                    cell.textContent = result.success ? `✓ ${saveText.ok}` : `⚠ ${result.message || saveText.failed}`;
                }
            });

            const written = results.filter((result) => result.success).length;
            intro.textContent = written === results.length
                ? format(saveText.done, written)
                : format(saveText.partial, written, results.length);
            confirm.hidden = true;
            cancel.textContent = saveText.close;
            sourceHandlers.applyResults?.(results);
            window.Toolbox?.notify(intro.textContent, written === results.length ? "success" : "error");
        };

        window.bootstrap.Modal.getOrCreateInstance(modalEl).show();
    }

    // ------------------------------------------------------------------ tabs

    // Switching away from a tab destroys its module and its in-browser changes, so ask first.
    function guardTab(tabButton, source, module) {
        if (!tabButton) {
            return;
        }

        tabButton.addEventListener("shown.bs.tab", function () {
            module()?.init();
        });
        tabButton.addEventListener("hide.bs.tab", function (event) {
            if (tracker.count(source) > 0) {
                event.preventDefault();
                const next = event.relatedTarget;
                tracker.confirmDiscard(source).then((confirmed) => {
                    if (confirmed && next && window.bootstrap?.Tab) {
                        window.bootstrap.Tab.getOrCreateInstance(next).show();
                    }
                });
                return;
            }

            module()?.destroy();
        });
    }

    document.addEventListener("DOMContentLoaded", function () {
        // Bootstrap puts the backdrop on <body>, but .app-shell has its own z-index, so dialogs
        // left inside it paint under the backdrop. Move the page's dialogs to <body>; the host
        // keeps .content-area so the form styles still apply (display: contents).
        const dialogs = document.querySelectorAll(".jira-shell .modal, .jira-shell .offcanvas, [data-jira-save-modal]");
        if (dialogs.length) {
            const host = document.createElement("div");
            host.className = "content-area tbx-modal-host";
            dialogs.forEach((dialog) => host.appendChild(dialog));
            document.body.appendChild(host);
        }

        guardTab(document.getElementById("jira-tab-board"), "board", () => window.ToolboxJiraTaskBoard);
        guardTab(document.getElementById("jira-tab-auto"), "auto", () => window.ToolboxJiraAutoSchedule);

        document.querySelector("[data-jira-unsaved-save]")?.addEventListener("click", openSave);
        document.querySelector("[data-jira-unsaved-discard]")?.addEventListener("click", function () {
            const source = activeSource();
            tracker.confirmDiscard().then((confirmed) => {
                if (!confirmed) {
                    return;
                }

                if (source && handlers[source]?.discard) {
                    handlers[source].discard();
                    return;
                }

                leaving = true;
                window.location.reload();
            });
        });
    });
})(window, document);
