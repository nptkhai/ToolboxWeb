(function (window, document, $) {
    "use strict";

    const DAY_MS = 24 * 60 * 60 * 1000;
    const WEEKDAY_NAMES_VI = ["Chủ Nhật", "Thứ Hai", "Thứ Ba", "Thứ Tư", "Thứ Năm", "Thứ Sáu", "Thứ Bảy"];

    function parseJsonScript(id, fallback) {
        const el = document.getElementById(id);
        if (!el) {
            return fallback;
        }

        try {
            return JSON.parse(el.textContent || "");
        } catch {
            return fallback;
        }
    }

    function pad2(value) {
        return String(value).padStart(2, "0");
    }

    function startOfDay(date) {
        const result = new Date(date);
        result.setHours(0, 0, 0, 0);
        return result;
    }

    function addDays(date, amount) {
        const result = new Date(date);
        result.setDate(result.getDate() + amount);
        return result;
    }

    function toDateInputValue(date) {
        return `${date.getFullYear()}-${pad2(date.getMonth() + 1)}-${pad2(date.getDate())}`;
    }

    function fmtShort(date) {
        return `${pad2(date.getDate())}-${pad2(date.getMonth() + 1)}`;
    }

    function sameDay(a, b) {
        return toDateInputValue(a) === toDateInputValue(b);
    }

    function parseIsoDate(value) {
        if (!value || !/^\d{4}-\d{2}-\d{2}$/.test(value)) {
            return null;
        }

        const [year, month, day] = value.split("-").map(Number);
        return new Date(year, month - 1, day);
    }

    function parseIsoMonth(value) {
        if (!value || !/^\d{4}-\d{2}$/.test(value)) {
            return null;
        }

        const [year, month] = value.split("-").map(Number);
        return new Date(year, month - 1, 1);
    }

    function formatIsoMonth(date) {
        return `${date.getFullYear()}-${pad2(date.getMonth() + 1)}`;
    }

    function getIsoWeekMonday(date) {
        const normalized = startOfDay(date);
        const day = normalized.getDay() || 7;
        normalized.setDate(normalized.getDate() + 1 - day);
        return normalized;
    }

    function parseIsoWeek(value) {
        const match = /^(\d{4})-W(\d{2})$/.exec(value || "");
        if (!match) {
            return null;
        }

        const year = Number(match[1]);
        const week = Number(match[2]);
        const januaryFourth = new Date(year, 0, 4);
        const januaryFourthDay = januaryFourth.getDay() || 7;
        const firstMonday = new Date(year, 0, 4 - januaryFourthDay + 1);
        firstMonday.setDate(firstMonday.getDate() + (week - 1) * 7);
        return firstMonday;
    }

    function formatIsoWeek(date) {
        const monday = getIsoWeekMonday(date);
        const thursday = addDays(monday, 3);
        const januaryFourth = new Date(thursday.getFullYear(), 0, 4);
        const januaryFourthDay = januaryFourth.getDay() || 7;
        const firstWeekMonday = addDays(januaryFourth, 1 - januaryFourthDay);
        const week = 1 + Math.round((thursday - addDays(firstWeekMonday, 3)) / (7 * DAY_MS));
        return `${thursday.getFullYear()}-W${pad2(week)}`;
    }

    function escapeHtml(value) {
        return String(value ?? "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#39;");
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
        let periodButtons = [];
        let inputWraps = [];
        let navButtons = [];
        let inputs = {};
        let widgets = { day: null, week: null, month: null };
        let config = {};
        let boardConfig = {};
        let rows = [];
        let periodType = "week";
        let referenceDate = null;
        let syncingPickers = false;
        let weekOptionsCenterYear = new Date().getFullYear();
        let boundHandlers = [];

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

        function formatWeekOption(week, year) {
            return (boardConfig.weekPickerOption || "Week {0}, {1}")
                .replace("{0}", String(week))
                .replace("{1}", String(year));
        }

        function buildWeekOptions(centerYear) {
            const options = [];
            for (let year = centerYear - 3; year <= centerYear + 3; year += 1) {
                for (let week = 1; week <= 53; week += 1) {
                    const value = `${year}-W${pad2(week)}`;
                    const weekStart = parseIsoWeek(value);
                    if (!weekStart || !formatIsoWeek(weekStart).startsWith(`${year}-W`)) {
                        continue;
                    }

                    const weekEnd = addDays(weekStart, 6);
                    options.push({
                        value,
                        text: formatWeekOption(week, year),
                        range: `${pad2(weekStart.getDate())}/${pad2(weekStart.getMonth() + 1)} - ${pad2(weekEnd.getDate())}/${pad2(weekEnd.getMonth() + 1)}/${weekEnd.getFullYear()}`
                    });
                }
            }

            return options;
        }

        function ensureWeekOptionForDate(date) {
            if (!widgets.week) {
                return;
            }

            const year = date.getFullYear();
            if (Math.abs(year - weekOptionsCenterYear) > 2) {
                weekOptionsCenterYear = year;
                widgets.week.dataSource.data(buildWeekOptions(weekOptionsCenterYear));
            }
        }

        function applyReferenceDate(date) {
            if (!(date instanceof Date) || Number.isNaN(date.getTime()) || syncingPickers) {
                return;
            }

            referenceDate = date;
            syncingPickers = true;

            try {
                const monthStart = new Date(date.getFullYear(), date.getMonth(), 1);

                if (widgets.day) {
                    widgets.day.value(date);
                } else if (inputs.day) {
                    inputs.day.value = toDateInputValue(date);
                }

                if (widgets.week) {
                    ensureWeekOptionForDate(date);
                    widgets.week.value(formatIsoWeek(date));
                } else if (inputs.week) {
                    inputs.week.value = formatIsoWeek(date);
                }

                if (widgets.month) {
                    widgets.month.value(monthStart);
                } else if (inputs.month) {
                    inputs.month.value = formatIsoMonth(monthStart);
                }
            } finally {
                syncingPickers = false;
            }
        }

        function shiftReferenceDate(steps) {
            if (!referenceDate) {
                return;
            }

            const next = new Date(referenceDate);
            if (periodType === "day") {
                next.setDate(next.getDate() + steps);
            } else if (periodType === "month") {
                next.setMonth(next.getMonth() + steps);
            } else {
                next.setDate(next.getDate() + steps * 7);
            }

            applyReferenceDate(next);
        }

        function initNativeFallback() {
            if (inputs.day) {
                inputs.day.type = "date";
                inputs.day.addEventListener("change", () => applyReferenceDate(parseIsoDate(inputs.day.value)));
            }

            if (inputs.week) {
                inputs.week.type = "week";
                inputs.week.addEventListener("change", () => {
                    const parsed = parseIsoWeek(inputs.week.value);
                    if (parsed) {
                        applyReferenceDate(parsed);
                    }
                });
            }

            if (inputs.month) {
                inputs.month.type = "month";
                inputs.month.addEventListener("change", () => applyReferenceDate(parseIsoMonth(inputs.month.value)));
            }
        }

        function initKendoPickers() {
            if (!(window.kendo && $ && $.fn && typeof $.fn.kendoDatePicker === "function")) {
                initNativeFallback();
                return;
            }

            if (config.currentCulture === "vi-VN" && typeof window.kendo.culture === "function") {
                window.kendo.culture("vi-VN");
            }

            const popupOptions = { appendTo: $(document.body), collision: "fit flip" };

            widgets.day = $(inputs.day).kendoDatePicker({
                format: "dd/MM/yyyy",
                popup: popupOptions
            }).data("kendoDatePicker");
            widgets.day.bind("change", () => applyReferenceDate(widgets.day.value()));

            weekOptionsCenterYear = new Date().getFullYear();
            widgets.week = $(inputs.week).kendoComboBox({
                dataSource: buildWeekOptions(weekOptionsCenterYear),
                dataTextField: "text",
                dataValueField: "value",
                filter: "contains",
                placeholder: boardConfig.weekPickerPlaceholder || "Select week",
                suggest: true,
                template: "<div class='jira-dashboard-week-option'><strong>#: text #</strong><span>#: range #</span></div>"
            }).data("kendoComboBox");
            widgets.week.bind("change", () => {
                const parsed = parseIsoWeek(widgets.week.value());
                if (parsed) {
                    applyReferenceDate(parsed);
                }
            });

            widgets.month = $(inputs.month).kendoDatePicker({
                format: "MM/yyyy",
                start: "year",
                depth: "year",
                popup: popupOptions
            }).data("kendoDatePicker");
            widgets.month.bind("change", () => applyReferenceDate(widgets.month.value()));
        }

        function resolvePeriodValue() {
            if (periodType === "day") {
                return widgets.day ? toDateInputValue(widgets.day.value()) : (inputs.day?.value || "");
            }

            if (periodType === "month") {
                return widgets.month ? formatIsoMonth(widgets.month.value()) : (inputs.month?.value || "");
            }

            if (widgets.week) {
                const selected = widgets.week.value();
                return /^\d{4}-W\d{2}$/.test(selected || "") ? selected : "";
            }

            return inputs.week?.value || "";
        }

        function setActivePeriod(nextPeriod) {
            periodType = nextPeriod;
            periodButtons.forEach((button) => button.classList.toggle("is-active", button.dataset.period === nextPeriod));
            inputWraps.forEach((wrap) => wrap.classList.toggle("is-active", wrap.dataset.periodInput === nextPeriod));
        }

        function cardHtml(row) {
            const overdue = row.dueDate && startOfDay(new Date(row.dueDate)) < startOfDay(new Date()) && !/done/i.test(row.status || "");
            const dueLabel = row.dueDate ? fmtShort(new Date(row.dueDate)) : "-";
            return `<div class="jira-board-card" draggable="true" data-key="${escapeHtml(row.subTaskKey)}">
                <div class="jira-board-card-top">
                    <span class="jira-board-card-key">${escapeHtml(row.subTaskKey)}</span>
                    <span class="jira-board-card-project">${escapeHtml(row.project)}</span>
                </div>
                <div class="jira-board-card-title">${escapeHtml(row.subTaskSummary)}</div>
                <div class="jira-board-card-bottom">
                    <span class="badge jira-dashboard-status jira-dashboard-status--${escapeHtml(row.statusTone)}">${escapeHtml(row.status)}</span>
                    <span class="jira-board-card-due ${overdue ? "jira-board-overdue" : ""}">${dueLabel}</span>
                </div>
                <div class="jira-board-card-hours">Est: ${Number(row.estimateHours || 0)}h · Log: ${Number(row.loggedHours || 0)}h</div>
            </div>`;
        }

        function loadClass(sumHours) {
            const capacity = boardConfig.dayCapacityHours || 8;
            if (sumHours > capacity) {
                return "jira-board-load--over";
            }

            if (sumHours >= capacity * 0.75) {
                return "jira-board-load--high";
            }

            return "jira-board-load--ok";
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
                const sumHours = dayRows.reduce((total, row) => total + Number(row.estimateHours || 0), 0);
                const isToday = sameDay(day, today);
                const pct = Math.min(100, Math.round((sumHours / capacity) * 100));

                return `<div class="jira-board-day-col ${isToday ? "is-today" : ""}" data-date="${dateStr}">
                    <div class="jira-board-day-head">
                        <span class="jira-board-day-label">${WEEKDAY_NAMES_VI[day.getDay()]}, ${fmtShort(day)}${isToday ? " ★" : ""}</span>
                        <span class="jira-board-day-count">${dayRows.length}</span>
                    </div>
                    <div class="jira-board-day-load">
                        <span>${sumHours.toFixed(1)}h/${capacity}h</span>
                        <span class="jira-board-load-bar"><span class="jira-board-load-fill ${loadClass(sumHours)}" style="width:${pct}%"></span></span>
                    </div>
                    <div class="jira-board-day-body" data-date="${dateStr}">
                        ${dayRows.length ? dayRows.map(cardHtml).join("") : `<div class="jira-board-day-empty">${escapeHtml(boardConfig.noTasks || "")}</div>`}
                    </div>
                </div>`;
            }).join("")}</div>`;

            wireDragAndDrop();
        }

        function wireDragAndDrop() {
            root.querySelectorAll(".jira-board-card").forEach((card) => {
                on(card, "dragstart", (event) => {
                    card.classList.add("is-dragging");
                    event.dataTransfer.setData("text/plain", card.dataset.key);
                });
                on(card, "dragend", () => card.classList.remove("is-dragging"));
            });

            root.querySelectorAll(".jira-board-day-body").forEach((zone) => {
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
                    if (!row || sameDay(new Date(row.dueDate || targetDate), parseIsoDate(targetDate))) {
                        return;
                    }

                    row.dueDate = `${targetDate}T00:00:00`;
                    const range = currentRange();
                    renderDayColumns(range.start, range.end);
                    notify(
                        (boardConfig.movedToast || "Moved {0} to {1}").replace("{0}", key).replace("{1}", targetDate),
                        "success"
                    );
                });
            });
        }

        function currentRange() {
            const ref = referenceDate || new Date();

            if (periodType === "day") {
                const day = startOfDay(ref);
                return { start: day, end: day };
            }

            if (periodType === "month") {
                const start = new Date(ref.getFullYear(), ref.getMonth(), 1);
                const end = new Date(ref.getFullYear(), ref.getMonth() + 1, 0);
                return { start, end };
            }

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

            let derivedDate = null;
            if (filter.periodType === "day") {
                derivedDate = parseIsoDate(filter.dayValue);
            } else if (filter.periodType === "month") {
                derivedDate = parseIsoMonth(filter.monthValue);
            } else {
                derivedDate = parseIsoWeek(filter.weekValue);
            }

            if (derivedDate) {
                applyReferenceDate(derivedDate);
            }
        }

        async function fetchAndRender() {
            const periodValue = resolvePeriodValue();
            const params = new URLSearchParams({ periodType, periodValue });

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
                applyFilterResult(data.filter);

                const range = currentRange();
                renderDayColumns(range.start, range.end);
            } catch {
                renderAlert(boardConfig.error);
            }
        }

        function jumpToToday() {
            const el = root.querySelector(".jira-board-day-col.is-today");
            el?.scrollIntoView({ behavior: "smooth", inline: "center", block: "nearest" });
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

                periodButtons = Array.from(toolbar.querySelectorAll("[data-period]"));
                inputWraps = Array.from(toolbar.querySelectorAll("[data-period-input]"));
                navButtons = Array.from(toolbar.querySelectorAll("[data-nav-step]"));
                inputs = {
                    day: document.getElementById("jira-board-day"),
                    week: document.getElementById("jira-board-week"),
                    month: document.getElementById("jira-board-month")
                };
                widgets = { day: null, week: null, month: null };

                config = parseJsonScript("jira-worklist-config", {});
                boardConfig = parseJsonScript("jira-task-board-config", {});
                periodType = "week";
                rows = [];

                setActivePeriod("week");
                initKendoPickers();
                referenceDate = new Date();
                applyReferenceDate(referenceDate);

                periodButtons.forEach((button) => {
                    on(button, "click", () => setActivePeriod(button.dataset.period));
                });

                navButtons.forEach((button) => {
                    on(button, "click", () => {
                        const steps = Number(button.dataset.navStep || "0");
                        if (steps) {
                            shiftReferenceDate(steps);
                        }
                    });
                });

                const searchButton = document.querySelector("[data-jira-board-search]");
                on(searchButton, "click", fetchAndRender);

                const todayButton = document.querySelector("[data-jira-board-today]");
                on(todayButton, "click", jumpToToday);

                fetchAndRender();
            },
            destroy() {
                offAll();
                if (widgets.day) { widgets.day.destroy(); }
                if (widgets.week) { widgets.week.destroy(); }
                if (widgets.month) { widgets.month.destroy(); }
                widgets = { day: null, week: null, month: null };
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
                referenceDate = null;
                root = null;
                toolbar = null;
                alertHost = null;
                rangeLabelEl = null;
            }
        };
    }

    window.ToolboxJiraTaskBoard = createModule();
})(window, document, window.jQuery);
