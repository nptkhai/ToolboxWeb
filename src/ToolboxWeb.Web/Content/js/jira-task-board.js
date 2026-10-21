(function (window, document) {
    "use strict";

    // Weekday names and short dates follow the UI culture (format.js); Vietnamese keeps the
    // usual short headers T2…CN, which Intl does not produce.
    const LOCALE = window.Toolbox?.format?.locale || document.documentElement.lang || "vi-VN";
    const VIETNAMESE = LOCALE.toLowerCase().startsWith("vi");
    const WEEKDAY_NAMES = Array.from({ length: 7 }, (_, index) =>
        new Intl.DateTimeFormat(LOCALE, { weekday: "long" }).format(new Date(2026, 0, 4 + index)));
    const WEEKDAY_HEADERS = VIETNAMESE
        ? ["T2", "T3", "T4", "T5", "T6", "T7", "CN"]
        : Array.from({ length: 7 }, (_, index) =>
            new Intl.DateTimeFormat(LOCALE, { weekday: "short" }).format(new Date(2026, 0, 5 + index)));
    // Intl gives "19-10" for vi-VN; the app's Vietnamese date format uses slashes.
    const SHORT_DATE = VIETNAMESE
        ? { format: (date) => `${String(date.getDate()).padStart(2, "0")}/${String(date.getMonth() + 1).padStart(2, "0")}` }
        : new Intl.DateTimeFormat(LOCALE, { day: "2-digit", month: "2-digit" });

    function fmtHours(value) {
        return window.Toolbox?.format?.hours(value) ?? `${value}h`;
    }

    const jira = window.ToolboxJira;
    const { escapeHtml, parseJsonScript } = jira;
    const { startOfDay, addDays, sameDay, parseIsoDate } = jira.dates;
    const toDateInputValue = jira.dates.formatIsoDate;
    const getIsoWeekMonday = jira.dates.isoWeekMonday;

    function fmtShort(date) {
        return SHORT_DATE.format(date);
    }

    function notify(message, type) {
        if (window.Toolbox && typeof window.Toolbox.notify === "function") {
            window.Toolbox.notify(message, type || "info");
        }
    }

    function createModule() {
        let root = null;
        let toolbar = null;
        let alertHost = null;
        let rangeLabelEl = null;
        let picker = null;
        let config = {};
        let boardConfig = {};
        let rows = [];
        // subTaskKey -> due date before it was dragged; these moves exist only in the browser.
        let moved = new Map();
        let periodType = "week";
        let referenceDate = null;
        let boundHandlers = [];

        function reportUnsaved() {
            window.ToolboxJiraUnsaved?.set("board", moved.size);
        }

        // Reloading replaces the rows, so ask before throwing away moves that are not in Jira.
        function confirmReload() {
            return window.ToolboxJiraUnsaved ? window.ToolboxJiraUnsaved.confirmDiscard("board") : Promise.resolve(true);
        }

        function on(el, event, handler) {
            if (!el) {
                return;
            }

            el.addEventListener(event, handler);
            boundHandlers.push([el, event, handler]);
        }

        function offAll() {
            boundHandlers.forEach(([el, event, handler]) => el.removeEventListener(event, handler));
            boundHandlers = [];
        }

        function renderAlert(message) {
            if (!alertHost) {
                return;
            }

            alertHost.innerHTML = message
                ? `<div class="alert alert-danger jira-dashboard-alert" role="alert">${escapeHtml(message)}</div>`
                : "";
        }

        function issueLinkHtml(issueKey) {
            // draggable="false" so grabbing the key still drags the card, not the link itself
            return jira.issueLink(issueKey, config.browseBaseUrl, "jira-board-card-key");
        }

        function isDoneRow(row) {
            return row?.statusTone === "done";
        }

        // Done work is measured by the time actually logged; outstanding work by its estimate.
        function summariseDay(dayRows) {
            const doneHours = dayRows
                .filter(isDoneRow)
                .reduce((total, row) => total + Number(row.loggedHours || 0), 0);
            const openHours = dayRows
                .filter((row) => !isDoneRow(row))
                .reduce((total, row) => total + Number(row.estimateHours || 0), 0);

            return { doneHours, openHours, totalHours: doneHours + openHours };
        }

        function loadTitle(doneHours, openHours) {
            return (boardConfig.loadTitle || "{0} + {1}")
                .replace("{0}", fmtHours(doneHours))
                .replace("{1}", fmtHours(openHours));
        }

        function loadBarHtml(dayRows) {
            const capacity = boardConfig.dayCapacityHours || 8;
            const { doneHours, openHours, totalHours } = summariseDay(dayRows);
            // Segments are scaled against whichever is larger so an over-capacity day still
            // shows the done/open split correctly instead of clipping one segment away.
            const scale = Math.max(capacity, totalHours) || 1;
            const donePct = (doneHours / scale) * 100;
            const openPct = (openHours / scale) * 100;

            return `<span class="jira-board-load-bar" title="${escapeHtml(loadTitle(doneHours, openHours))}">
                <span class="jira-board-load-fill jira-board-load-fill--done" style="width:${donePct}%"></span>
                <span class="jira-board-load-fill jira-board-load-fill--open" style="width:${openPct}%"></span>
            </span>`;
        }

        function cardHtml(row) {
            const done = isDoneRow(row);
            const overdue = !done && row.dueDate && startOfDay(new Date(row.dueDate)) < startOfDay(new Date());
            const dueLabel = row.dueDate ? fmtShort(new Date(row.dueDate)) : "-";
            const movedFrom = moved.has(row.subTaskKey)
                ? `<div class="jira-board-card-moved">${escapeHtml((boardConfig.movedFrom || "{0}").replace("{0}", moved.get(row.subTaskKey) ? fmtShort(new Date(moved.get(row.subTaskKey))) : "-"))}</div>`
                : "";
            return `<div class="jira-board-card ${done ? "is-done" : ""} ${moved.has(row.subTaskKey) ? "is-moved" : ""}" draggable="${done ? "false" : "true"}" data-key="${escapeHtml(row.subTaskKey)}" data-done="${done}">
                <div class="jira-board-card-top">
                    ${issueLinkHtml(row.subTaskKey)}
                    <span class="jira-board-card-project">${escapeHtml(row.project)}</span>
                </div>
                <div class="jira-board-card-title">${escapeHtml(row.subTaskSummary)}</div>
                <div class="jira-board-card-bottom">
                    ${jira.statusPill(row.status, row.statusTone)}
                    <span class="jira-board-card-due ${overdue ? "jira-board-overdue" : ""}">${dueLabel}</span>
                </div>
                <div class="jira-board-card-hours">${escapeHtml(jira.template(boardConfig.cardHours || "{0} · {1}", fmtHours(row.estimateHours || 0), fmtHours(row.loggedHours || 0)))}</div>
                ${movedFrom}
            </div>`;
        }

        function renderDayColumns(rangeStart, rangeEnd) {
            const days = [];
            let cursor = new Date(rangeStart);
            while (cursor <= rangeEnd) {
                days.push(new Date(cursor));
                cursor = addDays(cursor, 1);
            }

            const today = startOfDay(new Date());
            const capacity = boardConfig.dayCapacityHours || 8;

            root.innerHTML = `<div class="jira-board-columns">${days.map((day) => {
                const dateStr = toDateInputValue(day);
                const dayRows = rows.filter((row) => row.dueDate && sameDay(new Date(row.dueDate), day));
                const { totalHours } = summariseDay(dayRows);
                const isToday = sameDay(day, today);

                return `<div class="jira-board-day-col ${isToday ? "is-today" : ""}" data-date="${dateStr}">
                    <div class="jira-board-day-head">
                        <span class="jira-board-day-label">${WEEKDAY_NAMES[day.getDay()]}, ${fmtShort(day)}${isToday ? " ★" : ""}</span>
                        <span class="jira-board-day-count">${dayRows.length}</span>
                    </div>
                    <div class="jira-board-day-load">
                        <span>${fmtHours(totalHours)}/${fmtHours(capacity)}</span>
                        ${loadBarHtml(dayRows)}
                    </div>
                    <div class="jira-board-day-body" data-date="${dateStr}">
                        ${dayRows.length ? dayRows.map(cardHtml).join("") : `<div class="jira-board-day-empty">${escapeHtml(boardConfig.noTasks || "")}</div>`}
                    </div>
                </div>`;
            }).join("")}</div>`;

            wireDragAndDrop();
        }

        // Completed subtasks are history — they stay pinned to the day they were finished.
        function wireCardDragging(cardSelector) {
            root.querySelectorAll(cardSelector).forEach((card) => {
                if (card.dataset.done === "true") {
                    return;
                }

                on(card, "dragstart", (event) => {
                    card.classList.add("is-dragging");
                    event.dataTransfer.setData("text/plain", card.dataset.key);
                });
                on(card, "dragend", () => card.classList.remove("is-dragging"));
            });
        }

        function wireDropZones(zoneSelector) {
            root.querySelectorAll(zoneSelector).forEach((zone) => {
                on(zone, "dragover", (event) => {
                    event.preventDefault();
                    zone.classList.add("is-drag-over");
                });
                on(zone, "dragleave", () => zone.classList.remove("is-drag-over"));
                on(zone, "drop", (event) => {
                    event.preventDefault();
                    zone.classList.remove("is-drag-over");
                    const key = event.dataTransfer.getData("text/plain");
                    const targetDate = zone.dataset.date;
                    const row = rows.find((item) => item.subTaskKey === key);
                    if (!row || isDoneRow(row) || sameDay(new Date(row.dueDate || targetDate), parseIsoDate(targetDate))) {
                        return;
                    }

                    const original = moved.has(key) ? moved.get(key) : (row.dueDate || null);
                    row.dueDate = `${targetDate}T00:00:00`;
                    if (original && sameDay(new Date(original), parseIsoDate(targetDate))) {
                        moved.delete(key);
                    } else {
                        moved.set(key, original);
                    }
                    reportUnsaved();
                    render();
                    notify(
                        (boardConfig.movedToast || "Moved {0} to {1}").replace("{0}", key).replace("{1}", targetDate),
                        "info"
                    );
                });
            });
        }

        function wireDragAndDrop() {
            wireCardDragging(".jira-board-card");
            wireDropZones(".jira-board-day-body");
        }

        function calCardHtml(row) {
            const done = isDoneRow(row);
            const label = `${escapeHtml(row.subTaskKey)} — ${escapeHtml(row.subTaskSummary)}`;
            return `<div class="jira-cal-card ${done ? "is-done" : ""} ${moved.has(row.subTaskKey) ? "is-moved" : ""}" draggable="${done ? "false" : "true"}" data-key="${escapeHtml(row.subTaskKey)}" data-done="${done}" title="${label}">
                ${issueLinkHtml(row.subTaskKey)} ${escapeHtml(row.subTaskSummary)}
            </div>`;
        }

        function renderMonthCalendar(monthRef) {
            const monthIndex = monthRef.getMonth();
            const year = monthRef.getFullYear();
            const first = new Date(year, monthIndex, 1);
            const last = new Date(year, monthIndex + 1, 0);
            const startOffset = (first.getDay() + 6) % 7;
            const endOffset = (last.getDay() + 6) % 7;
            const gridStart = addDays(first, -startOffset);
            const gridEnd = addDays(last, 6 - endOffset);

            const days = [];
            let cursor = new Date(gridStart);
            while (cursor <= gridEnd) {
                days.push(new Date(cursor));
                cursor = addDays(cursor, 1);
            }

            const today = startOfDay(new Date());
            const header = WEEKDAY_HEADERS.map((label) => `<span>${label}</span>`).join("");

            const cells = days.map((day) => {
                const inMonth = day.getMonth() === monthIndex;
                if (!inMonth) {
                    return `<div class="jira-cal-cell is-other-month"><div class="jira-cal-cell-head"><span class="jira-cal-daynum">${day.getDate()}</span></div></div>`;
                }

                const dateStr = toDateInputValue(day);
                const dayRows = rows.filter((row) => row.dueDate && sameDay(new Date(row.dueDate), day));
                const { doneHours, openHours, totalHours } = summariseDay(dayRows);
                const isToday = sameDay(day, today);
                const loadText = loadTitle(doneHours, openHours);

                return `<div class="jira-cal-cell ${isToday ? "is-today" : ""}" data-date="${dateStr}">
                    <div class="jira-cal-cell-head">
                        <span class="jira-cal-daynum">${day.getDate()}${isToday ? " •" : ""}</span>
                        ${dayRows.length ? `<span class="jira-cal-load" title="${escapeHtml(loadText)}">${fmtHours(totalHours)}</span>` : ""}
                    </div>
                    <div class="jira-cal-cards" data-date="${dateStr}">
                        ${dayRows.map(calCardHtml).join("")}
                    </div>
                </div>`;
            }).join("");

            root.innerHTML = `<div class="jira-cal-weekday-header">${header}</div><div class="jira-cal-grid">${cells}</div>`;
            wireCalendarDragAndDrop();
        }

        function wireCalendarDragAndDrop() {
            wireCardDragging(".jira-cal-card");
            wireDropZones(".jira-cal-cards");
        }

        function render() {
            if (periodType === "month") {
                renderMonthCalendar(referenceDate || new Date());
                return;
            }

            const range = currentRange();
            renderDayColumns(range.start, range.end);
        }

        function currentRange() {
            const ref = referenceDate || new Date();
            const monday = getIsoWeekMonday(ref);
            return { start: monday, end: addDays(monday, 6) };
        }

        function applyFilterResult(filter) {
            if (!filter) {
                return;
            }

            if (rangeLabelEl) {
                rangeLabelEl.textContent = filter.rangeLabel || "";
            }

            const type = filter.periodType === "month" ? "month" : "week";
            const value = type === "month" ? filter.monthValue : filter.weekValue;
            const derivedDate = jira.dates.parsePeriod(type, value);
            if (derivedDate) {
                periodType = type;
                referenceDate = derivedDate;
                picker?.apply(type, value);
            }
        }

        async function fetchAndRender() {
            const state = picker.state();
            periodType = state.periodType;
            referenceDate = state.date;
            const params = new URLSearchParams({ periodType: state.periodType, periodValue: state.periodValue });

            try {
                const response = await fetch(`${config.taskBoardApiUrl}?${params.toString()}`, {
                    headers: { "X-Requested-With": "XMLHttpRequest" }
                });
                const payload = await response.json().catch(() => null);

                if (payload?.requiresLogin) {
                    const returnUrl = `${window.location.pathname}${window.location.search}`;
                    window.location.href = `${config.loginUrl || "/Identity/Account/Login"}?returnUrl=${encodeURIComponent(returnUrl)}`;
                    return;
                }

                if (!response.ok || !payload?.success) {
                    renderAlert(payload?.message || boardConfig.error);
                    rows = [];
                    root.innerHTML = "";
                    return;
                }

                const data = payload.data || {};
                renderAlert(data.errorMessage);
                rows = Array.isArray(data.rows) ? data.rows.slice() : [];
                moved = new Map();
                reportUnsaved();
                applyFilterResult(data.filter);
                render();
            } catch {
                renderAlert(boardConfig.error);
            }
        }

        function scrollToToday() {
            root?.querySelector(".jira-board-day-col.is-today, .jira-cal-cell.is-today")
                ?.scrollIntoView({ behavior: "smooth", inline: "center", block: "nearest" });
        }

        return {
            init() {
                root = document.getElementById("jira-board-root");
                toolbar = document.querySelector("[data-jira-board-toolbar]");
                alertHost = document.querySelector("[data-jira-board-alert]");
                rangeLabelEl = document.querySelector("[data-jira-board-range-label]");

                if (!root || !toolbar) {
                    return;
                }

                config = parseJsonScript("jira-worklist-config", {});
                boardConfig = parseJsonScript("jira-task-board-config", {});
                rows = [];

                // Every period change reloads; moves that are not in Jira yet are confirmed first.
                picker = jira.periodPicker(toolbar.querySelector("[data-jira-period]"), {
                    beforeChange: confirmReload,
                    onChange: (state) => fetchAndRender().then(() => {
                        if (state.reason === "today") {
                            scrollToToday();
                        }
                    })
                });

                fetchAndRender();
            },
            destroy() {
                offAll();
                picker?.destroy();
                picker = null;
                if (root) {
                    root.innerHTML = "";
                }
                if (alertHost) {
                    alertHost.innerHTML = "";
                }
                if (rangeLabelEl) {
                    rangeLabelEl.textContent = "";
                }
                rows = [];
                moved = new Map();
                reportUnsaved();
                referenceDate = null;
                root = null;
                toolbar = null;
                alertHost = null;
                rangeLabelEl = null;
            }
        };
    }

    window.ToolboxJiraTaskBoard = createModule();
})(window, document);
