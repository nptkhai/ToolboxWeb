(function (window, document, $) {
    const form = document.querySelector("[data-jira-dashboard-form]");
    if (!form) {
        return;
    }

    const rowsScript = document.getElementById("jira-dashboard-rows");
    const configScript = document.getElementById("jira-dashboard-config");
    const gridHost = document.getElementById("jira-dashboard-grid");
    const periodTypeInput = form.querySelector("[data-jira-period-type]");
    const periodValueInput = form.querySelector("[data-jira-period-value]");
    const sortInput = form.querySelector("[data-jira-sort]");
    const directionInput = form.querySelector("[data-jira-direction]");
    const buttons = Array.from(form.querySelectorAll("[data-period]"));
    const inputWraps = Array.from(form.querySelectorAll("[data-period-input]"));
    const navButtons = Array.from(form.querySelectorAll("[data-nav-step]"));
    const jumpTodayButton = form.querySelector("[data-jira-jump-today]");
    const searchButton = form.querySelector(".jira-dashboard-search");
    const rangeLabelEl = form.querySelector("[data-jira-range-label]");
    const statsHost = document.querySelector("[data-jira-stats]");
    const alertHost = document.querySelector("[data-jira-alert-host]");
    const jqlEl = document.querySelector("[data-jira-jql]");
    const currentType = periodTypeInput?.value || "week";

    const inputs = {
        day: document.getElementById("jira-dashboard-day"),
        week: document.getElementById("jira-dashboard-week"),
        month: document.getElementById("jira-dashboard-month"),
        year: document.getElementById("jira-dashboard-year")
    };

    const widgets = {
        day: null,
        week: null,
        month: null,
        year: null
    };

    let table = null;
    let referenceDate = null;
    let syncingPickers = false;

    const tabulatorFieldMap = {
        project: "project",
        subTaskKey: "subTaskKey",
        subTaskSummary: "subTaskSummary",
        issueKey: "issueKey",
        issueSummary: "issueSummary",
        status: "status",
        dueDate: "dueDateSort",
        estimate: "estimateHours",
        logged: "loggedHours"
    };

    const querySortMap = Object.keys(tabulatorFieldMap).reduce((accumulator, key) => {
        accumulator[tabulatorFieldMap[key]] = key;
        return accumulator;
    }, {});

    function parseJsonScript(scriptEl, fallback) {
        if (!scriptEl) {
            return fallback;
        }

        try {
            return JSON.parse(scriptEl.textContent || "");
        } catch {
            return fallback;
        }
    }

    const rows = parseJsonScript(rowsScript, []);
    const config = parseJsonScript(configScript, {});
    const columnText = config.columns || {};
    const browseBaseUrl = (config.browseBaseUrl || "").replace(/\/+$/, "");
    const weekPickerOption = config.weekPickerOption || "Week {0}, {1}";

    function pad2(value) {
        return String(value).padStart(2, "0");
    }

    function escapeHtml(value) {
        return String(value ?? "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#39;");
    }

    function buildBrowseUrl(issueKey) {
        if (!browseBaseUrl || !issueKey) {
            return "";
        }

        return `${browseBaseUrl}/browse/${encodeURIComponent(issueKey)}`;
    }

    function issueLink(issueKey) {
        const text = escapeHtml(issueKey || "-");
        const href = buildBrowseUrl(issueKey);

        if (!href || !issueKey) {
            return `<span class="jira-dashboard-mono">${text}</span>`;
        }

        return `<a class="jira-dashboard-link jira-dashboard-mono" href="${escapeHtml(href)}" target="_blank" rel="noopener noreferrer">${text}</a>`;
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

    function parseIsoWeek(value) {
        const match = /^(\d{4})-W(\d{2})$/.exec(value || "");
        if (!match) {
            return null;
        }

        const year = Number(match[1]);
        const week = Number(match[2]);
        if (week < 1 || week > 53) {
            return null;
        }

        const januaryFourth = new Date(year, 0, 4);
        const januaryFourthDay = januaryFourth.getDay() || 7;
        const firstMonday = new Date(year, 0, 4 - januaryFourthDay + 1);
        firstMonday.setDate(firstMonday.getDate() + (week - 1) * 7);
        return firstMonday;
    }

    function parseYear(value) {
        if (!value || !/^\d{4}$/.test(value)) {
            return null;
        }

        return new Date(Number(value), 0, 1);
    }

    function formatIsoDate(date) {
        if (!(date instanceof Date) || Number.isNaN(date.getTime())) {
            return "";
        }

        return `${date.getFullYear()}-${pad2(date.getMonth() + 1)}-${pad2(date.getDate())}`;
    }

    function formatIsoMonth(date) {
        if (!(date instanceof Date) || Number.isNaN(date.getTime())) {
            return "";
        }

        return `${date.getFullYear()}-${pad2(date.getMonth() + 1)}`;
    }

    function formatIsoWeek(date) {
        if (!(date instanceof Date) || Number.isNaN(date.getTime())) {
            return "";
        }

        const normalized = new Date(date.getFullYear(), date.getMonth(), date.getDate());
        const day = normalized.getDay() || 7;
        normalized.setDate(normalized.getDate() + 4 - day);

        const weekYear = normalized.getFullYear();
        const januaryFourth = new Date(weekYear, 0, 4);
        const januaryFourthDay = januaryFourth.getDay() || 7;
        const firstWeekThursday = new Date(weekYear, 0, 4 + (4 - januaryFourthDay));
        const week = 1 + Math.round((normalized - firstWeekThursday) / 604800000);

        return `${weekYear}-W${pad2(week)}`;
    }

    function formatWeekOption(week, year) {
        return weekPickerOption
            .replace("{0}", String(week))
            .replace("{1}", String(year));
    }

    function formatYear(date) {
        if (!(date instanceof Date) || Number.isNaN(date.getTime())) {
            return "";
        }

        return String(date.getFullYear());
    }

    function toDueDateSortValue(value) {
        if (!value) {
            return Number.MAX_SAFE_INTEGER;
        }

        const normalized = String(value).split("T")[0];
        const parsed = parseIsoDate(normalized);
        return parsed ? parsed.getTime() : Number.MAX_SAFE_INTEGER;
    }

    function syncVisibleInput(period) {
        inputWraps.forEach((wrap) => {
            wrap.classList.toggle("is-active", wrap.dataset.periodInput === period);
        });

        buttons.forEach((button) => {
            button.classList.toggle("is-active", button.dataset.period === period);
        });

        if (periodTypeInput) {
            periodTypeInput.value = period;
        }
    }

    function buildWeekOptions(centerYear) {
        const firstWeekYear = centerYear - 3;
        const lastWeekYear = centerYear + 3;
        const weekOptions = [];

        for (let year = firstWeekYear; year <= lastWeekYear; year += 1) {
            for (let week = 1; week <= 53; week += 1) {
                const value = `${year}-W${pad2(week)}`;
                const weekStart = parseIsoWeek(value);
                if (!weekStart || !formatIsoWeek(weekStart).startsWith(`${year}-W`)) {
                    continue;
                }

                const weekEnd = new Date(weekStart);
                weekEnd.setDate(weekEnd.getDate() + 6);
                weekOptions.push({
                    value,
                    text: formatWeekOption(week, year),
                    range: `${pad2(weekStart.getDate())}/${pad2(weekStart.getMonth() + 1)} - ${pad2(weekEnd.getDate())}/${pad2(weekEnd.getMonth() + 1)}/${weekEnd.getFullYear()}`
                });
            }
        }

        return weekOptions;
    }

    let weekOptionsCenterYear = new Date().getFullYear();

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

    function getPeriodDate(period) {
        if (period === "day") {
            return widgets.day ? widgets.day.value() : parseIsoDate(inputs.day?.value);
        }

        if (period === "month") {
            return widgets.month ? widgets.month.value() : parseIsoMonth(inputs.month?.value);
        }

        if (period === "year") {
            return widgets.year ? widgets.year.value() : parseYear(inputs.year?.value);
        }

        const weekValue = widgets.week ? widgets.week.value() : inputs.week?.value;
        return parseIsoWeek(weekValue);
    }

    function applyReferenceDate(date) {
        if (!(date instanceof Date) || Number.isNaN(date.getTime()) || syncingPickers) {
            return;
        }

        referenceDate = date;
        syncingPickers = true;

        try {
            const monthStart = new Date(date.getFullYear(), date.getMonth(), 1);
            const yearStart = new Date(date.getFullYear(), 0, 1);

            if (widgets.day) {
                widgets.day.value(date);
            } else if (inputs.day) {
                inputs.day.value = formatIsoDate(date);
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

            if (widgets.year) {
                widgets.year.value(yearStart);
            } else if (inputs.year) {
                inputs.year.value = String(date.getFullYear());
            }
        } finally {
            syncingPickers = false;
        }
    }

    function shiftReferenceDate(period, steps) {
        if (!referenceDate) {
            return;
        }

        const next = new Date(referenceDate);

        if (period === "day") {
            next.setDate(next.getDate() + steps);
        } else if (period === "month") {
            next.setMonth(next.getMonth() + steps);
        } else if (period === "year") {
            next.setFullYear(next.getFullYear() + steps);
        } else {
            next.setDate(next.getDate() + (steps * 7));
        }

        applyReferenceDate(next);
    }

    function initNativeFallback() {
        if (inputs.day) {
            inputs.day.type = "date";
            inputs.day.value = periodValueInput?.value && currentType === "day"
                ? periodValueInput.value
                : (inputs.day.value || "");
            inputs.day.addEventListener("change", () => applyReferenceDate(parseIsoDate(inputs.day.value)));
        }

        if (inputs.month) {
            inputs.month.type = "month";
            inputs.month.addEventListener("change", () => applyReferenceDate(parseIsoMonth(inputs.month.value)));
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

        if (inputs.year) {
            inputs.year.type = "number";
            inputs.year.min = "2000";
            inputs.year.max = "2100";
            inputs.year.inputMode = "numeric";
            inputs.year.addEventListener("change", () => applyReferenceDate(parseYear(inputs.year.value)));
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

        const popupOptions = {
            appendTo: $(document.body),
            collision: "fit flip"
        };

        widgets.day = $(inputs.day).kendoDatePicker({
            format: "dd/MM/yyyy",
            value: parseIsoDate(inputs.day.value),
            popup: popupOptions
        }).data("kendoDatePicker");
        widgets.day.bind("change", () => applyReferenceDate(widgets.day.value()));

        const selectedWeekDate = parseIsoWeek(inputs.week.value);
        weekOptionsCenterYear = selectedWeekDate?.getFullYear() || new Date().getFullYear();

        widgets.week = $(inputs.week).kendoComboBox({
            dataSource: buildWeekOptions(weekOptionsCenterYear),
            dataTextField: "text",
            dataValueField: "value",
            filter: "contains",
            placeholder: config.weekPickerPlaceholder || "Select week",
            suggest: true,
            template: "<div class='jira-dashboard-week-option'><strong>#: text #</strong><span>#: range #</span></div>",
            value: inputs.week.value
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
            value: parseIsoMonth(inputs.month.value),
            popup: popupOptions
        }).data("kendoDatePicker");
        widgets.month.bind("change", () => applyReferenceDate(widgets.month.value()));

        widgets.year = $(inputs.year).kendoDatePicker({
            format: "yyyy",
            start: "decade",
            depth: "decade",
            value: parseYear(inputs.year.value),
            popup: popupOptions
        }).data("kendoDatePicker");
        widgets.year.bind("change", () => applyReferenceDate(widgets.year.value()));

        if (widgets.year?.wrapper) {
            widgets.year.wrapper.addClass("jira-dashboard-year-picker");
        }

    }

    function resolvePeriodValue(period) {
        if (period === "day") {
            if (widgets.day) {
                return formatIsoDate(widgets.day.value());
            }

            return inputs.day?.value || "";
        }

        if (period === "month") {
            if (widgets.month) {
                return formatIsoMonth(widgets.month.value());
            }

            return inputs.month?.value || "";
        }

        if (period === "year") {
            if (widgets.year) {
                return formatYear(widgets.year.value());
            }

            return inputs.year?.value || "";
        }

        if (widgets.week) {
            const selectedWeek = widgets.week.value();
            return /^\d{4}-W\d{2}$/.test(selectedWeek || "")
                ? selectedWeek
                : "";
        }

        return inputs.week?.value || "";
    }

    function statusBadge(status, tone) {
        return `<span class="jira-dashboard-status jira-dashboard-status--${escapeHtml(tone || "todo")}">${escapeHtml(status || "-")}</span>`;
    }

    function mapRows(data) {
        return Array.isArray(data)
            ? data.map((row) => ({
                index: row.index,
                project: row.project,
                subTaskKey: row.subTaskKey,
                subTaskSummary: row.subTaskSummary,
                issueKey: row.issueKey,
                issueSummary: row.issueSummary,
                status: row.status,
                statusTone: row.statusTone,
                dueDateText: row.dueDateText,
                dueDateSort: row.dueDate || "",
                dueDateSortValue: toDueDateSortValue(row.dueDate),
                estimateTimeText: row.estimateTimeText,
                loggedTimeText: row.loggedTimeText,
                estimateHours: row.estimateHours,
                loggedHours: row.loggedHours,
                isOverdue: row.isOverdue
            }))
            : [];
    }

    function initTabulator() {
        if (!gridHost) {
            return;
        }

        if (!window.Tabulator) {
            gridHost.innerHTML = `<div class="jira-dashboard-grid-fallback">${escapeHtml(config.emptyState || "")}</div>`;
            return;
        }

        const data = mapRows(rows);
        const sortField = tabulatorFieldMap[config.initialSort] || "dueDateSort";
        const sortDirection = config.initialDirection === "desc" ? "desc" : "asc";

        table = new Tabulator(gridHost, {
            data,
            index: "index",
            layout: "fitDataStretch",
            height: "560px",
            placeholder: config.emptyState || "",
            reactiveData: false,
            columnHeaderVertAlign: "middle",
            columnDefaults: {
                headerHozAlign: "center",
                headerSort: true,
                vertAlign: "middle"
            },
            initialSort: [
                {
                    column: sortField,
                    dir: sortDirection
                }
            ],
            columns: [
                {
                    title: columnText.index || "STT",
                    field: "index",
                    sorter: "number",
                    hozAlign: "center",
                    width: 70,
                    headerSort: false
                },
                {
                    title: columnText.project || "Project",
                    field: "project",
                    hozAlign: "center",
                    width: 150,
                    formatter(cell) {
                        return `<span class="jira-dashboard-project-chip">${escapeHtml(cell.getValue() || "-")}</span>`;
                    }
                },
                {
                    title: columnText.subTaskKey || "Subtask Key",
                    field: "subTaskKey",
                    hozAlign: "center",
                    width: 150,
                    formatter(cell) {
                        return issueLink(cell.getValue());
                    }
                },
                {
                    title: columnText.subTaskSummary || "Subtask Summary",
                    field: "subTaskSummary",
                    hozAlign: "left",
                    minWidth: 260,
                    formatter(cell) {
                        return `<div class="jira-dashboard-summary">${escapeHtml(cell.getValue() || "-")}</div>`;
                    }
                },
                {
                    title: columnText.issueKey || "Issue Key",
                    field: "issueKey",
                    hozAlign: "center",
                    width: 150,
                    formatter(cell) {
                        return issueLink(cell.getValue());
                    }
                },
                {
                    title: columnText.issueSummary || "Issue Summary",
                    field: "issueSummary",
                    hozAlign: "left",
                    minWidth: 260,
                    formatter(cell) {
                        return `<div class="jira-dashboard-summary">${escapeHtml(cell.getValue() || "-")}</div>`;
                    }
                },
                {
                    title: columnText.status || "Status",
                    field: "status",
                    hozAlign: "center",
                    width: 150,
                    formatter(cell) {
                        const row = cell.getRow().getData();
                        return statusBadge(cell.getValue(), row.statusTone);
                    }
                },
                {
                    title: columnText.dueDate || "Due Date",
                    field: "dueDateSortValue",
                    hozAlign: "center",
                    width: 150,
                    sorter: "number",
                    formatter(cell) {
                        const row = cell.getRow().getData();
                        const text = escapeHtml(row.dueDateText || "-");
                        return row.isOverdue
                            ? `<span class="jira-dashboard-overdue">${text}</span>`
                            : text;
                    }
                },
                {
                    title: columnText.estimate || "Estimate Time",
                    field: "estimateHours",
                    hozAlign: "center",
                    width: 150,
                    sorter: "number",
                    formatter(cell) {
                        const row = cell.getRow().getData();
                        return `<span class="jira-dashboard-mono">${escapeHtml(row.estimateTimeText || "-")}</span>`;
                    }
                },
                {
                    title: columnText.logged || "LogTime",
                    field: "loggedHours",
                    hozAlign: "center",
                    width: 150,
                    sorter: "number",
                    formatter(cell) {
                        const row = cell.getRow().getData();
                        return `<span class="jira-dashboard-mono">${escapeHtml(row.loggedTimeText || "-")}</span>`;
                    }
                }
            ]
        });

        table.on("dataSorted", function (_sorters, rowComponents) {
            const activeSorters = table.getSorters();
            if (!activeSorters || !activeSorters.length) {
                return;
            }

            const primarySorter = activeSorters[0];
            const querySort = querySortMap[primarySorter.field] || "dueDate";

            if (sortInput) {
                sortInput.value = querySort;
            }

            if (directionInput) {
                directionInput.value = primarySorter.dir === "desc" ? "desc" : "asc";
            }
        });
    }

    function renderStats(stats) {
        if (!statsHost) {
            return;
        }

        statsHost.innerHTML = (Array.isArray(stats) ? stats : []).map((stat) => `
            <article class="jira-dashboard-stat-card jira-dashboard-stat-card--${escapeHtml(stat.accentClass)}">
                <span class="jira-dashboard-stat-label">${escapeHtml(stat.label)}</span>
                <strong class="jira-dashboard-stat-value">${escapeHtml(stat.value)}</strong>
                <span class="jira-dashboard-stat-hint">${escapeHtml(stat.hint)}</span>
            </article>
        `).join("");
    }

    function renderAlert(message) {
        if (!alertHost) {
            return;
        }

        alertHost.innerHTML = message
            ? `<div class="alert alert-danger jira-dashboard-alert" role="alert">${escapeHtml(message)}</div>`
            : "";
    }

    function getPeriodDateFromFilter(filter) {
        switch (filter.periodType) {
            case "day":
                return parseIsoDate(filter.dayValue);
            case "month":
                return parseIsoMonth(filter.monthValue);
            case "year":
                return parseYear(filter.yearValue);
            default:
                return parseIsoWeek(filter.weekValue);
        }
    }

    function applyFilterResult(filter) {
        if (!filter) {
            return;
        }

        if (rangeLabelEl) {
            rangeLabelEl.textContent = filter.rangeLabel || "";
        }

        if (sortInput && filter.sort) {
            sortInput.value = filter.sort;
        }

        if (directionInput && filter.direction) {
            directionInput.value = filter.direction;
        }

        if (periodValueInput && filter.periodValue) {
            periodValueInput.value = filter.periodValue;
        }

        const derivedDate = getPeriodDateFromFilter(filter);
        if (derivedDate) {
            applyReferenceDate(derivedDate);
        }

        syncVisibleInput(filter.periodType || currentType);
    }

    function setSearchBusy(isBusy) {
        if (!searchButton) {
            return;
        }

        searchButton.disabled = isBusy;
        searchButton.classList.toggle("is-loading", isBusy);
    }

    async function performSearch() {
        const activeType = periodTypeInput?.value || "week";
        const periodValue = resolvePeriodValue(activeType);

        if (periodValueInput) {
            periodValueInput.value = periodValue;
        }

        const params = new URLSearchParams({
            periodType: activeType,
            periodValue,
            sort: sortInput?.value || "dueDate",
            dir: directionInput?.value || "asc"
        });

        setSearchBusy(true);

        try {
            const response = await fetch(`${config.dashboardApiUrl || "/Jira/Dashboard"}?${params.toString()}`, {
                headers: { "X-Requested-With": "XMLHttpRequest" }
            });
            const payload = await response.json().catch(() => null);

            if (payload?.requiresLogin) {
                const returnUrl = `${window.location.pathname}${window.location.search}`;
                window.location.href = `${config.loginUrl || "/Identity/Account/Login"}?returnUrl=${encodeURIComponent(returnUrl)}`;
                return;
            }

            if (!response.ok || !payload?.success) {
                window.Toolbox?.notify(payload?.message || config.searchErrorMessage || "Search failed.", "error");
                return;
            }

            const data = payload.data || {};
            applyFilterResult(data.filter);
            renderStats(data.stats);
            renderAlert(data.errorMessage);

            if (jqlEl) {
                jqlEl.textContent = data.appliedJql || "";
            }

            if (table) {
                table.replaceData(mapRows(data.rows));
            }

            if (data.errorMessage) {
                window.Toolbox?.notify(data.errorMessage, "error");
            }

            const newUrl = `${form.getAttribute("action")}?${params.toString()}`;
            window.history.pushState({}, "", newUrl);
        } catch {
            window.Toolbox?.notify(config.searchErrorMessage || "Search failed.", "error");
        } finally {
            setSearchBusy(false);
        }
    }

    navButtons.forEach((button) => {
        button.addEventListener("click", () => {
            const steps = Number(button.dataset.navStep || "0");
            if (steps) {
                shiftReferenceDate(periodTypeInput?.value || "week", steps);
            }
        });
    });

    jumpTodayButton?.addEventListener("click", () => {
        applyReferenceDate(new Date());
        performSearch();
    });

    buttons.forEach((button) => {
        button.addEventListener("click", () => {
            syncVisibleInput(button.dataset.period || "week");
        });
    });

    form.addEventListener("submit", (event) => {
        event.preventDefault();
        performSearch();
    });

    initKendoPickers();
    initTabulator();
    syncVisibleInput(currentType);
    referenceDate = getPeriodDate(currentType);
})(window, document, window.jQuery);
