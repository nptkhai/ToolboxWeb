(function (window, document) {
    "use strict";

    const form = document.querySelector("[data-jira-dashboard-form]");
    const jira = window.ToolboxJira;
    if (!form || !jira) {
        return;
    }

    const { escapeHtml, grid } = jira;
    const gridHost = document.querySelector("[data-jira-grid]");
    const periodTypeInput = form.querySelector("[data-jira-period-type]");
    const periodValueInput = form.querySelector("[data-jira-period-value]");
    const sortInput = form.querySelector("[data-jira-sort]");
    const directionInput = form.querySelector("[data-jira-direction]");
    const rangeLabelEl = form.querySelector("[data-jira-range-label]");
    const statsHost = document.querySelector("[data-jira-stats]");
    const alertHost = document.querySelector("[data-jira-alert-host]");
    const jqlBlock = document.querySelector("[data-jira-jql-block]");
    const chipsHost = document.querySelector("[data-jira-chips]");

    const rows = jira.parseJsonScript("jira-dashboard-rows", []);
    const config = jira.parseJsonScript("jira-dashboard-config", {});
    const columnText = config.columns || {};

    // Query-string sort name <-> Tabulator field.
    const tabulatorFieldMap = {
        project: "project",
        subTaskKey: "subTaskKey",
        subTaskSummary: "subTaskSummary",
        issueKey: "issueKey",
        issueSummary: "issueSummary",
        status: "status",
        dueDate: "dueDate",
        estimate: "estimateHours",
        logged: "loggedHours"
    };

    const querySortMap = Object.fromEntries(Object.entries(tabulatorFieldMap).map(([key, field]) => [field, key]));

    const chipFilters = {
        all: null,
        open: (row) => row.statusTone !== "done",
        overdue: (row) => !!row.isOverdue,
        done: (row) => row.statusTone === "done"
    };

    let table = null;
    let allRows = rows;
    let requestId = 0;

    const chips = chipsHost ? jira.chipBar(chipsHost, applyChipFilter) : null;

    function updateChipCounts(data) {
        chips?.setCounts({
            all: data.length,
            open: data.filter(chipFilters.open).length,
            overdue: data.filter(chipFilters.overdue).length,
            done: data.filter(chipFilters.done).length
        });
    }

    function applyChipFilter(value) {
        if (!table) {
            return;
        }

        const filter = chipFilters[value];
        if (filter) {
            table.setFilter(filter);
        } else {
            table.clearFilter();
        }

        grid.setEmptyText(table, filter && allRows.length ? config.filterEmpty : config.emptyState);
    }

    function initTabulator() {
        if (!gridHost) {
            return;
        }

        if (!window.Tabulator) {
            gridHost.appendChild(grid.options({ placeholder: config.emptyState }).placeholder);
            return;
        }

        const sortField = tabulatorFieldMap[config.initialSort] || "dueDate";
        const sortDirection = config.initialDirection === "desc" ? "desc" : "asc";

        table = new window.Tabulator(gridHost, grid.options({
            data: rows,
            index: "index",
            height: "560px",
            placeholder: config.emptyState,
            columnDefaults: { headerSort: true },
            initialSort: [{ column: sortField, dir: sortDirection }],
            columns: [
                { title: columnText.index || "#", field: "index", sorter: "number", width: 56, headerSort: false },
                {
                    title: columnText.project || "Project",
                    field: "project",
                    width: 110,
                    formatter: (cell) => `<span class="jira-project">${escapeHtml(cell.getValue() || "-")}</span>`
                },
                { title: columnText.subTaskKey || "Subtask", field: "subTaskKey", width: 150, formatter: grid.formatters.issueKey(config.browseBaseUrl) },
                { title: columnText.subTaskSummary || "Summary", field: "subTaskSummary", hozAlign: "left", minWidth: 180, widthGrow: 2, formatter: grid.formatters.text },
                { title: columnText.issueKey || "Issue", field: "issueKey", width: 150, formatter: grid.formatters.issueKey(config.browseBaseUrl) },
                { title: columnText.issueSummary || "Issue summary", field: "issueSummary", hozAlign: "left", minWidth: 160, widthGrow: 1, formatter: grid.formatters.text },
                { title: columnText.status || "Status", field: "status", width: 130, formatter: grid.formatters.status },
                { title: columnText.dueDate || "Due", field: "dueDate", width: 110, sorter: grid.dueDateSorter, formatter: grid.formatters.dueDate },
                { title: columnText.estimate || "Estimate", field: "estimateHours", hozAlign: "right", width: 100, sorter: "number", formatter: grid.formatters.hours },
                { title: columnText.logged || "Logged", field: "loggedHours", hozAlign: "right", width: 100, sorter: "number", formatter: grid.formatters.hours }
            ]
        }));

        table.on("tableBuilt", () => {
            updateChipCounts(allRows);
            applyChipFilter(chips?.value() || "all");
        });

        table.on("dataSorted", () => {
            const primary = table.getSorters()[0];
            if (!primary) {
                return;
            }

            if (sortInput) {
                sortInput.value = querySortMap[primary.field] || "dueDate";
            }

            if (directionInput) {
                directionInput.value = primary.dir === "desc" ? "desc" : "asc";
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
        if (alertHost) {
            alertHost.innerHTML = message
                ? `<div class="alert alert-danger jira-dashboard-alert" role="alert">${escapeHtml(message)}</div>`
                : "";
        }
    }

    function setBusy(isBusy) {
        gridHost?.setAttribute("aria-busy", isBusy ? "true" : "false");
    }

    async function performSearch(state) {
        const params = new URLSearchParams({
            periodType: state.periodType,
            periodValue: state.periodValue,
            sort: sortInput?.value || "dueDate",
            dir: directionInput?.value || "asc"
        });

        if (periodTypeInput) {
            periodTypeInput.value = state.periodType;
        }

        if (periodValueInput) {
            periodValueInput.value = state.periodValue;
        }

        // A later period change wins over an earlier answer that arrives late.
        const current = ++requestId;
        setBusy(true);

        try {
            const response = await fetch(`${config.dashboardApiUrl || "/Jira/Dashboard"}?${params.toString()}`, {
                headers: { "X-Requested-With": "XMLHttpRequest" }
            });
            const payload = await response.json().catch(() => null);
            if (current !== requestId) {
                return;
            }

            if (payload?.requiresLogin) {
                const returnUrl = `${window.location.pathname}${window.location.search}`;
                window.location.href = `${config.loginUrl || "/Identity/Account/Login"}?returnUrl=${encodeURIComponent(returnUrl)}`;
                return;
            }

            if (!response.ok || !payload?.success) {
                window.Toolbox?.notify(payload?.message || config.searchErrorMessage, "error");
                return;
            }

            const data = payload.data || {};
            const filter = data.filter || {};
            if (rangeLabelEl) {
                rangeLabelEl.textContent = filter.rangeLabel || "";
            }

            if (filter.periodType && filter.periodValue) {
                picker.apply(filter.periodType, filter.periodValue);
            }

            renderStats(data.stats);
            renderAlert(data.errorMessage);
            jira.jql.update(jqlBlock, data.appliedJql);

            allRows = Array.isArray(data.rows) ? data.rows : [];
            updateChipCounts(allRows);
            if (table) {
                await table.replaceData(allRows);
                applyChipFilter(chips?.value() || "all");
            }

            window.history.replaceState({}, "", `${form.getAttribute("action")}?${params.toString()}`);
        } catch {
            if (current === requestId) {
                window.Toolbox?.notify(config.searchErrorMessage, "error");
            }
        } finally {
            if (current === requestId) {
                setBusy(false);
            }
        }
    }

    const picker = jira.periodPicker(form.querySelector("[data-jira-period]"), { onChange: performSearch });

    // Enter in a picker input submits the form; the picker already reloaded on change.
    form.addEventListener("submit", (event) => {
        event.preventDefault();
        performSearch(picker.state());
    });

    initTabulator();
})(window, document);
