// Jira Dashboard (stage 5.1): cards, hours-logged chart and the subtask table grouped by parent
// issue. Everything is drawn from one JSON payload, first from the page and then from
// /Jira/Dashboard whenever the period picker changes.
(function (window, document) {
    "use strict";

    const form = document.querySelector("[data-jira-dashboard-form]");
    const jira = window.ToolboxJira;
    if (!form || !jira) {
        return;
    }

    const { escapeHtml, template, grid } = jira;
    const fmt = window.Toolbox?.format;
    const gridHost = document.querySelector("[data-jira-grid]");
    const periodTypeInput = form.querySelector("[data-jira-period-type]");
    const periodValueInput = form.querySelector("[data-jira-period-value]");
    const sortInput = form.querySelector("[data-jira-sort]");
    const directionInput = form.querySelector("[data-jira-direction]");
    const rangeLabelEl = form.querySelector("[data-jira-range-label]");
    const kpiHost = document.querySelector("[data-jira-kpis]");
    const chartHost = document.querySelector("[data-jira-chart]");
    const chartTitle = document.querySelector("[data-jira-chart-title]");
    const chartSummary = document.querySelector("[data-jira-chart-summary]");
    const tableCount = document.querySelector("[data-jira-table-count]");
    const alertHost = document.querySelector("[data-jira-alert-host]");
    const jqlBlock = document.querySelector("[data-jira-jql-block]");
    const chipsHost = document.querySelector("[data-jira-chips]");

    let data = jira.parseJsonScript("jira-dashboard-data", {});
    const config = jira.parseJsonScript("jira-dashboard-config", {});
    const text = config.text || {};
    const columnText = config.columns || {};
    const locale = fmt?.locale || document.documentElement.lang || "vi-VN";
    const vietnamese = locale.toLowerCase().startsWith("vi");

    // Query-string sort name <-> Tabulator field.
    const tabulatorFieldMap = {
        subTaskKey: "subTaskKey",
        subTaskSummary: "subTaskSummary",
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
    let chart = null;
    let requestId = 0;

    const hours = (value) => jira.displayHours(Number(value) || 0);
    const number = (value) => (fmt ? fmt.number(value, 0) : String(value));

    // ------------------------------------------------------------------ cards

    function meter(percent, tone, label) {
        const width = Math.max(0, Math.min(100, Number(percent) || 0));
        return `<div class="jira-meter ${tone ? `is-${tone}` : ""}" role="progressbar" aria-valuemin="0" aria-valuemax="100"
                     aria-valuenow="${Math.round(width)}" aria-label="${escapeHtml(label)}"><span style="width:${width}%"></span></div>`;
    }

    function note(content, tone) {
        return content ? `<span class="jira-kpi-note ${tone ? `is-${tone}` : ""}">${escapeHtml(content)}</span>` : "";
    }

    function card(label, value, body) {
        return `<div class="jira-kpi">
            <span class="jira-kpi-label">${escapeHtml(label)}</span>
            <strong class="jira-kpi-value">${escapeHtml(value)}</strong>
            ${body}
        </div>`;
    }

    function deltaNote(current, previous) {
        if (previous === null || previous === undefined) {
            return "";
        }

        const diff = Number(current) - Number(previous);
        if (Math.abs(diff) < 0.01) {
            return note(text.deltaSame);
        }

        return note(template(diff > 0 ? text.deltaUp : text.deltaDown, hours(Math.abs(diff))));
    }

    function renderCards(summary) {
        if (!kpiHost) {
            return;
        }

        const s = summary || {};
        const logPercent = s.logPercent;
        const over = logPercent !== null && logPercent !== undefined && logPercent > 100;
        const logCard = card(
            text.cardLogVsEstimate,
            logPercent === null || logPercent === undefined ? "—" : `${number(logPercent)}%`,
            (logPercent === null || logPercent === undefined ? "" : meter(logPercent, over ? "warning" : "accent", text.cardLogVsEstimate))
            + `<span class="jira-kpi-detail">${escapeHtml(template(text.cardLogVsEstimateDetail, hours(s.loggedHours), hours(s.estimateHours)))}</span>`
            + (logPercent === null || logPercent === undefined
                ? note(text.cardNoEstimate)
                : over ? note(template(text.cardOverEstimate, number(logPercent - 100)), "warning") : note(text.cardWithinEstimate)));

        const progressCard = card(
            text.cardProgress,
            `${number(s.doneCount || 0)}/${number(s.subtaskCount || 0)}`,
            meter(s.progressPercent, s.subtaskCount && s.doneCount === s.subtaskCount ? "success" : "accent", text.cardProgress)
            + `<span class="jira-kpi-detail">${escapeHtml(template(text.cardProgressDetail, number(s.progressPercent || 0), number(s.openCount || 0)))}</span>`
            + (s.previousSubtaskCount === null || s.previousSubtaskCount === undefined
                ? ""
                : note(template(text.cardProgressPrevious, number(s.previousDoneCount || 0), number(s.previousSubtaskCount)))));

        const expected = Number(s.expectedHours) || 0;
        const logged = Number(s.loggedHours) || 0;
        const short = expected - logged;
        const loggedCard = card(
            text.cardLogged,
            hours(logged),
            (expected > 0 ? meter(logged / expected * 100, short > 0.01 ? "warning" : "success", text.cardLogged) : "")
            + `<span class="jira-kpi-detail">${escapeHtml(expected > 0 ? template(text.cardLoggedNeed, hours(expected)) : text.cardNotStarted)}</span>`
            + (expected > 0 ? (short > 0.01 ? note(template(text.cardLoggedShort, hours(short)), "warning") : note(text.cardLoggedEnough, "success")) : "")
            + deltaNote(logged, s.previousLoggedHours));

        const overdueCard = card(
            text.cardOverdue,
            number(s.overdueCount || 0),
            (s.unestimatedCount
                ? note(template(text.cardUnestimated, number(s.unestimatedCount)), "warning")
                : note(text.cardAllEstimated)));

        kpiHost.innerHTML = logCard + progressCard + loggedCard + overdueCard;
        kpiHost.querySelectorAll(".jira-kpi")[3]?.classList.toggle("is-danger", (s.overdueCount || 0) > 0);
        kpiHost.setAttribute("aria-busy", "false");

        if (tableCount) {
            tableCount.textContent = template(text.tableCount, number(s.subtaskCount || 0), number(s.issueCount || 0));
        }
    }

    // ------------------------------------------------------------------ chart

    const weekdayShort = vietnamese
        ? ["CN", "T2", "T3", "T4", "T5", "T6", "T7"]
        : Array.from({ length: 7 }, (_, i) => new Intl.DateTimeFormat(locale, { weekday: "short" }).format(new Date(2026, 0, 4 + i)));
    const weekdayLong = new Intl.DateTimeFormat(locale, { weekday: "long" });
    const monthShort = new Intl.DateTimeFormat(locale, { month: "short" });
    const monthLong = new Intl.DateTimeFormat(locale, { month: "long", year: "numeric" });

    function barDate(bar) {
        return data.logBarUnit === "month"
            ? new Date(Number(bar.key.slice(0, 4)), Number(bar.key.slice(5, 7)) - 1, 1)
            : jira.dates.parseIsoDate(bar.key);
    }

    function axisLabel(bar, count) {
        const date = barDate(bar);
        if (data.logBarUnit === "month") {
            return monthShort.format(date);
        }

        // A month of bars only has room for the day number.
        return count > 10 ? String(date.getDate()) : `${weekdayShort[date.getDay()]}\n${jira.shortDate(date)}`;
    }

    function tooltipLabel(bar) {
        const date = barDate(bar);
        return data.logBarUnit === "month"
            ? monthLong.format(date)
            : `${weekdayLong.format(date)}, ${jira.displayDate(date)}`;
    }

    function chartOption(t) {
        const bars = data.logBars || [];
        const daily = data.logBarUnit !== "month";
        const peak = Math.max(0, ...bars.map((bar) => Number(bar.hours) || 0));

        // Screen readers get the same sentences as the tooltips, in the UI language, instead of
        // ECharts' built-in English description.
        const description = [daily ? text.chartTitleDay : text.chartTitleMonth]
            .concat(bars.map((bar) => Number(bar.targetHours) > 0
                ? template(text.chartTooltip, tooltipLabel(bar), hours(bar.hours), hours(bar.targetHours))
                : template(text.chartTooltipNoTarget, tooltipLabel(bar), hours(bar.hours))))
            .join(". ");

        return {
            aria: { enabled: true, label: { description } },
            grid: { left: 4, right: 12, top: 20, bottom: 4, containLabel: true },
            tooltip: {
                trigger: "axis",
                axisPointer: { type: "shadow", shadowStyle: { color: t.surface2 } },
                formatter(params) {
                    const bar = bars[params[0]?.dataIndex];
                    if (!bar) {
                        return "";
                    }

                    const label = escapeHtml(tooltipLabel(bar));
                    return Number(bar.targetHours) > 0
                        ? escapeHtml(template(text.chartTooltip, tooltipLabel(bar), hours(bar.hours), hours(bar.targetHours)))
                        : escapeHtml(template(text.chartTooltipNoTarget, tooltipLabel(bar), hours(bar.hours))) || label;
                }
            },
            xAxis: {
                type: "category",
                data: bars.map((bar) => axisLabel(bar, bars.length)),
                axisTick: { show: false },
                axisLine: { lineStyle: { color: t.line } },
                axisLabel: { color: t.muted, interval: 0, hideOverlap: true, lineHeight: 16 }
            },
            yAxis: {
                type: "value",
                min: 0,
                // Keep the 8 h line inside the chart even on a light day.
                max: daily ? Math.max(10, Math.ceil((peak + 1) / 2) * 2) : null,
                interval: daily ? 2 : null,
                splitNumber: 4,
                axisLabel: { color: t.muted, formatter: (value) => `${number(value)}h` },
                splitLine: { lineStyle: { color: t.line } }
            },
            series: [{
                type: "bar",
                barMaxWidth: 36,
                data: bars.map((bar) => {
                    const short = daily && Number(bar.targetHours) > 0 && !bar.isFuture && !bar.isToday
                        && Number(bar.hours) < Number(bar.targetHours);
                    return {
                        value: Number(bar.hours) || 0,
                        itemStyle: {
                            color: bar.isFuture ? t.line : short ? t.warning : t.accentBrand,
                            borderRadius: [3, 3, 0, 0]
                        }
                    };
                }),
                markLine: daily ? {
                    silent: true,
                    symbol: "none",
                    lineStyle: { color: t.muted, type: "dashed", width: 1 },
                    label: { color: t.muted, formatter: text.chartTarget, position: "insideEndTop" },
                    data: [{ yAxis: 8 }]
                } : undefined
            }]
        };
    }

    function renderChart() {
        const bars = data.logBars || [];
        const daily = data.logBarUnit !== "month";
        if (chartTitle) {
            chartTitle.textContent = daily ? text.chartTitleDay : text.chartTitleMonth;
        }

        if (chartSummary) {
            const total = bars.reduce((sum, bar) => sum + (Number(bar.hours) || 0), 0);
            const workingDays = bars.filter((bar) => Number(bar.targetHours) > 0 && !bar.isFuture);
            const met = workingDays.filter((bar) => Number(bar.hours) >= Number(bar.targetHours));
            chartSummary.textContent = total === 0
                ? text.chartEmpty
                : daily && workingDays.length ? template(text.chartSummary, number(met.length), number(workingDays.length)) : "";
        }

        if (!chartHost || !window.ToolboxCharts) {
            return;
        }

        if (chart) {
            chart.update(chartOption);
        } else {
            chart = window.ToolboxCharts.create(chartHost, chartOption);
        }
    }

    // ------------------------------------------------------------------ table

    function dueFormatter(cell) {
        const row = cell.getRow().getData();
        const date = escapeHtml(jira.displayDate(cell.getValue()));
        if (!row.daysOverdue) {
            return date;
        }

        return `<span class="jira-overdue">${date}</span> <span class="jira-late">${escapeHtml(template(text.daysLate, number(row.daysOverdue)))}</span>`;
    }

    function estimateFormatter(cell) {
        const row = cell.getRow().getData();
        if (Number(cell.getValue()) > 0) {
            return escapeHtml(hours(cell.getValue()));
        }

        // Open work without an estimate is something to go and fix in Jira.
        return row.statusTone === "done" ? "-" : `<span class="jira-unestimated">${escapeHtml(text.noEstimate)}</span>`;
    }

    function groupHeader(value, count, rows) {
        const first = rows[0] || {};
        const logged = rows.reduce((sum, row) => sum + (Number(row.loggedHours) || 0), 0);
        const meta = escapeHtml(template(text.groupMeta, number(count), hours(logged)));
        if (!value) {
            return `<span class="jira-group-title">${escapeHtml(text.noParent)}</span><span class="jira-group-meta">${meta}</span>`;
        }

        return `${jira.issueLink(value, config.browseBaseUrl, "jira-group-key")}`
            + `<span class="jira-group-title">${escapeHtml(first.issueSummary || "")}</span>`
            + `<span class="jira-group-meta">${meta}</span>`;
    }

    const chips = chipsHost ? jira.chipBar(chipsHost, applyChipFilter) : null;

    function updateChipCounts(rows) {
        chips?.setCounts({
            all: rows.length,
            open: rows.filter(chipFilters.open).length,
            overdue: rows.filter(chipFilters.overdue).length,
            done: rows.filter(chipFilters.done).length
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

        grid.setEmptyText(table, filter && (data.rows || []).length ? text.filterEmpty : text.emptyState);
    }

    function initTable() {
        if (!gridHost) {
            return;
        }

        if (!window.Tabulator) {
            gridHost.appendChild(grid.options({ placeholder: text.emptyState }).placeholder);
            return;
        }

        const sortField = tabulatorFieldMap[config.initialSort] || "dueDate";
        const sortDirection = config.initialDirection === "desc" ? "desc" : "asc";

        // No fixed height: the page scrolls, not a box inside it. Columns that do not fit step
        // aside instead of scrolling sideways; on a phone a row opens to show them.
        const narrow = window.matchMedia?.("(max-width: 600px)").matches;
        const columns = [
            { title: columnText.subTaskKey, field: "subTaskKey", width: narrow ? 146 : 150, responsive: 0, formatter: grid.formatters.issueKey(config.browseBaseUrl) },
            { title: columnText.subTaskSummary, field: "subTaskSummary", hozAlign: "left", minWidth: narrow ? 140 : 200, widthGrow: 3, responsive: 0, formatter: grid.formatters.text },
            { title: columnText.status, field: "status", width: 140, responsive: 1, formatter: grid.formatters.status },
            { title: columnText.dueDate, field: "dueDate", width: 190, responsive: 2, sorter: grid.dueDateSorter, formatter: dueFormatter },
            { title: columnText.estimate, field: "estimateHours", hozAlign: "right", width: 130, responsive: 3, sorter: "number", formatter: estimateFormatter },
            { title: columnText.logged, field: "loggedHours", hozAlign: "right", width: 110, responsive: 4, sorter: "number", formatter: grid.formatters.hours }
        ];
        if (narrow) {
            columns.unshift({ formatter: "responsiveCollapse", cssClass: "tbx-collapse-cell", width: 36, minWidth: 36, hozAlign: "center", headerSort: false, resizable: false, responsive: 0, tooltip: false });
        }

        table = new window.Tabulator(gridHost, grid.options({
            data: data.rows || [],
            index: "subTaskKey",
            placeholder: text.emptyState,
            responsiveLayout: narrow ? "collapse" : "hide",
            responsiveLayoutCollapseStartOpen: false,
            groupBy: "issueKey",
            groupHeader,
            groupToggleElement: "header",
            columnDefaults: { headerSort: true },
            initialSort: [{ column: sortField, dir: sortDirection }],
            columns
        }));

        table.on("tableBuilt", () => {
            updateChipCounts(data.rows || []);
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

    // ------------------------------------------------------------------ loading

    function renderAlert(message) {
        if (alertHost) {
            alertHost.innerHTML = message
                ? `<div class="alert alert-danger jira-dashboard-alert" role="alert">${escapeHtml(message)}</div>`
                : "";
        }
    }

    function setBusy(isBusy) {
        gridHost?.setAttribute("aria-busy", isBusy ? "true" : "false");
        kpiHost?.setAttribute("aria-busy", isBusy ? "true" : "false");
        chartHost?.classList.toggle("is-busy", isBusy);
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
                window.Toolbox?.notify(payload?.message || text.searchError, "error");
                return;
            }

            data = payload.data || {};
            const filter = data.filter || {};
            if (rangeLabelEl) {
                rangeLabelEl.textContent = filter.rangeLabel || "";
            }

            if (filter.periodType && filter.periodValue) {
                picker.apply(filter.periodType, filter.periodValue);
            }

            renderAlert(data.errorMessage);
            renderCards(data.summary);
            renderChart();
            jira.jql.update(jqlBlock, data.appliedJql);

            updateChipCounts(data.rows || []);
            if (table) {
                await table.replaceData(data.rows || []);
                applyChipFilter(chips?.value() || "all");
            }

            window.history.replaceState({}, "", `${form.getAttribute("action")}?${params.toString()}`);
        } catch {
            if (current === requestId) {
                window.Toolbox?.notify(text.searchError, "error");
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

    renderCards(data.summary);
    renderChart();
    initTable();
})(window, document);
