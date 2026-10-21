(function (window, document) {
    "use strict";

    // Due-date changes made on the board and auto-schedule tabs live only in the browser until
    // they are written to Jira. This tracker counts them per tab, shows the warning bar, and asks
    // before anything (switching tab, reloading data, leaving the page) would throw them away.
    const sources = {};
    let leaving = false;

    function readConfig() {
        try {
            return JSON.parse(document.getElementById("jira-worklist-config")?.textContent || "{}").unsaved || {};
        } catch {
            return {};
        }
    }

    function format(template, value) {
        return String(template || "").replace("{0}", value);
    }

    function total() {
        return Object.values(sources).reduce((sum, value) => sum + value, 0);
    }

    function renderBar() {
        const bar = document.querySelector("[data-jira-unsaved]");
        if (!bar) {
            return;
        }

        const count = total();
        bar.classList.toggle("d-none", count === 0);
        bar.querySelector("[data-jira-unsaved-text]").textContent = format(readConfig().message, count);
    }

    const tracker = {
        set(source, count) {
            sources[source] = Math.max(0, Number(count) || 0);
            renderBar();
        },
        count(source) {
            return source ? (sources[source] || 0) : total();
        },
        // Resolves true when there is nothing to lose, or the user agrees to discard it.
        confirmDiscard(source) {
            const count = tracker.count(source);
            if (count === 0) {
                return Promise.resolve(true);
            }

            const text = readConfig();
            const ask = window.Toolbox?.confirm
                ? window.Toolbox.confirm({ title: text.leaveTitle, message: format(text.leaveMessage, count), okText: text.leaveOk })
                : Promise.resolve(window.confirm(format(text.leaveMessage, count)));
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
        guardTab(document.getElementById("jira-tab-board"), "board", () => window.ToolboxJiraTaskBoard);
        guardTab(document.getElementById("jira-tab-auto"), "auto", () => window.ToolboxJiraAutoSchedule);

        document.querySelector("[data-jira-unsaved-discard]")?.addEventListener("click", function () {
            tracker.confirmDiscard().then((confirmed) => {
                if (confirmed) {
                    leaving = true;
                    window.location.reload();
                }
            });
        });
    });
})(window, document);
