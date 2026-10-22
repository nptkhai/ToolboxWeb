// Jira Worklist, Search tab (stage 5.2). Two exclusive ways to search (filters or JQL), searches
// through POST /Jira/Search without reloading, folds the form into a one-line summary, keeps the
// last search in the URL and in this browser, and shows the result in a grouped Tabulator with
// status chips, paging and Excel export (SheetJS, loaded on first use).
(function (window, document) {
    "use strict";

    const root = document.querySelector("[data-jira-search]");
    const jira = window.ToolboxJira;
    if (!root || !jira) {
        return;
    }

    const { escapeHtml, template, grid } = jira;
    const config = jira.parseJsonScript("jira-search-config", {});
    const text = config.text || {};
    const labels = text.labels || {};
    const columnText = config.columns || {};
    const STORAGE_KEY = "toolbox.jira.search";
    const PAGE_SIZE = 50;
    const FIELDS = ["assignee", "from", "to", "status", "project", "issueKey", "reporter", "jql"];
    const URL_KEYS = ["mode"].concat(FIELDS);

    const form = root.querySelector("[data-search-form]");
    const summary = root.querySelector("[data-search-summary]");
    const summaryText = root.querySelector("[data-search-summary-text]");
    const modeButtons = Array.from(root.querySelectorAll("[data-search-mode-button]"));
    const modeBlocks = Array.from(root.querySelectorAll("[data-search-mode]"));
    const errorEl = root.querySelector("[data-search-error]");
    const savedSelect = root.querySelector("[data-search-saved]");
    const countEl = root.querySelector("[data-search-count]");
    const truncatedEl = root.querySelector("[data-search-truncated]");
    const stateEl = root.querySelector("[data-search-state]");
    const gridHost = root.querySelector("[data-search-grid]");
    const exportButton = root.querySelector("[data-search-export]");
    const jqlBlock = root.querySelector("[data-jira-jql-block]");
    const fields = Object.fromEntries(FIELDS.map((name) => [name, root.querySelector(`[data-search-field="${name}"]`)]));

    let mode = "filter";
    let table = null;
    let rows = [];
    let lastJql = "";
    let requestId = 0;
    const datePickers = {};

    // ------------------------------------------------------------------ form

    function initDates() {
        const $ = window.jQuery;
        const kendoReady = !!($ && $.fn && $.fn.kendoDatePicker);
        const locale = window.Toolbox?.format?.locale;
        if (kendoReady && locale && window.kendo.cultures?.[locale]) {
            window.kendo.culture(locale);
        }

        root.querySelectorAll("[data-search-date]").forEach((input) => {
            const name = input.dataset.searchField;
            const initial = input.value;
            if (kendoReady) {
                datePickers[name] = $(input).kendoDatePicker({
                    format: window.Toolbox?.format?.datePattern || "dd/MM/yyyy",
                    value: jira.dates.parseIsoDate(initial)
                }).data("kendoDatePicker");
            } else {
                input.type = "date";
            }
        });
    }

    function readField(name) {
        if (datePickers[name]) {
            return jira.dates.formatIsoDate(datePickers[name].value());
        }

        return (fields[name]?.value || "").trim();
    }

    function writeField(name, value) {
        if (datePickers[name]) {
            datePickers[name].value(jira.dates.parseIsoDate(value));
            return;
        }

        if (fields[name]) {
            fields[name].value = value || "";
        }
    }

    function readState() {
        const state = { mode };
        FIELDS.forEach((name) => {
            state[name] = readField(name);
        });
        return state;
    }

    function writeState(state) {
        FIELDS.forEach((name) => writeField(name, state[name] || ""));
        setMode(state.mode === "jql" ? "jql" : "filter");
    }

    function setMode(next) {
        mode = next;
        modeButtons.forEach((button) => {
            const active = button.dataset.searchModeButton === next;
            button.classList.toggle("is-active", active);
            button.setAttribute("aria-pressed", active ? "true" : "false");
        });
        modeBlocks.forEach((block) => {
            block.hidden = block.dataset.searchMode !== next;
        });

        // Coming to JQL with nothing typed: start from what the filters just searched.
        if (next === "jql" && fields.jql && !fields.jql.value.trim() && lastJql) {
            fields.jql.value = jira.jql.format(lastJql);
        }
    }

    function selectedText(select) {
        return select?.selectedOptions?.[0]?.textContent.trim() || "";
    }

    function describe(state) {
        if (state.mode === "jql") {
            return template(text.summaryJql, (state.jql || "").replace(/\s+/g, " "));
        }

        const parts = [
            `${labels.assignee}: ${selectedText(fields.assignee) || text.any}`,
            `${labels.due}: ${jira.displayDate(state.from)} – ${jira.displayDate(state.to)}`
        ];
        if (state.status) {
            parts.push(`${labels.status}: ${state.status}`);
        }

        if (state.project) {
            parts.push(`${labels.project}: ${state.project}`);
        }

        if (state.issueKey) {
            parts.push(`${labels.issueKey}: ${state.issueKey}`);
        }

        if (state.reporter) {
            parts.push(`${labels.reporter}: ${selectedText(fields.reporter)}`);
        }

        return parts.join(" · ");
    }

    function fold(folded) {
        form.hidden = folded;
        summary.hidden = !folded;
    }

    function validate(state) {
        if (state.mode === "jql") {
            return null;
        }

        if (!state.assignee) {
            return text.assigneeRequired;
        }

        return state.from && state.to && state.from > state.to ? text.dateOrder : null;
    }

    // ------------------------------------------------------------------ memory

    function stateFromUrl() {
        const params = new URLSearchParams(window.location.search);
        if (!params.has("mode")) {
            return null;
        }

        return Object.fromEntries(URL_KEYS.map((key) => [key, params.get(key) || ""]));
    }

    function remember(state) {
        const params = new URLSearchParams();
        URL_KEYS.forEach((key) => {
            if (state[key] && !(key === "jql" && state.mode !== "jql")) {
                params.set(key, state[key]);
            }
        });
        window.history.replaceState({}, "", `${window.location.pathname}?${params.toString()}${window.location.hash}`);

        try {
            window.localStorage.setItem(STORAGE_KEY, JSON.stringify(state));
        } catch {
            // Storage can be off (private window); the URL still keeps the search.
        }
    }

    function recalled() {
        try {
            return JSON.parse(window.localStorage.getItem(STORAGE_KEY) || "null");
        } catch {
            return null;
        }
    }

    // ------------------------------------------------------------------ states

    function setState(html) {
        stateEl.innerHTML = html || "";
    }

    function showLoading() {
        gridHost.setAttribute("aria-busy", "true");
        setState(`<p class="jira-search-loading" role="status"><span class="spinner-border spinner-border-sm" aria-hidden="true"></span>${escapeHtml(text.searching)}</p>`);
    }

    function showError(message, retry) {
        gridHost.setAttribute("aria-busy", "false");
        destroyTable();
        setState(`<div class="tbx-error-state" role="alert">${jira.iconSvg("circle-alert")}
            <div class="jira-search-error-body"><p>${escapeHtml(message)}</p>
            <button type="button" class="btn btn-sm btn-outline-secondary" data-search-retry>${escapeHtml(text.retry)}</button></div></div>`);
        stateEl.querySelector("[data-search-retry]")?.addEventListener("click", retry);
    }

    function showEmpty() {
        setState(`<div class="tbx-empty" role="status"><span class="tbx-empty-icon">${jira.iconSvg("inbox", 20)}</span>
            <p class="tbx-empty-text">${escapeHtml(text.empty)}</p>
            <button type="button" class="btn btn-sm btn-outline-secondary" data-search-edit-empty>${jira.iconSvg("pencil")}${escapeHtml(text.editFilters)}</button></div>`);
        stateEl.querySelector("[data-search-edit-empty]")?.addEventListener("click", openForm);
    }

    function openForm() {
        fold(false);
        form.querySelector(mode === "jql" ? "[data-search-field='jql']" : "[data-search-field='assignee']")?.focus();
    }

    // ------------------------------------------------------------------ results

    const chips = jira.chipBar(root.querySelector("[data-search-chips]"), applyChip);

    function applyChip(value) {
        if (!table) {
            return;
        }

        const filter = jira.statusFilters[value];
        if (filter) {
            table.setFilter(filter);
        } else {
            table.clearFilter();
        }

        grid.setEmptyText(table, text.filterEmpty);
    }

    function destroyTable() {
        table?.destroy();
        table = null;
        gridHost.innerHTML = "";
    }

    function loggedTooltip(_event, cell) {
        const days = cell.getRow().getData().logDays || [];
        return days.map((day) => `${jira.displayDate(day.date)}: ${jira.displayHours(day.hours)}`).join("\n");
    }

    function buildTable(data) {
        destroyTable();
        const narrow = grid.isNarrow();
        const grouped = data.some((row) => row.parent);
        const paged = data.length > PAGE_SIZE;

        const columns = [
            { title: columnText.key, field: "issueKey", width: narrow ? 146 : 150, responsive: 0, formatter: grid.formatters.issueKey(config.browseBaseUrl) },
            { title: columnText.summary, field: "summary", hozAlign: "left", minWidth: narrow ? 140 : 220, widthGrow: 3, responsive: 0, formatter: grid.formatters.text },
            { title: columnText.status, field: "status", width: 140, responsive: 1, formatter: grid.formatters.status },
            { title: columnText.type, field: "type", width: 110, responsive: 5, formatter: grid.formatters.text },
            { title: columnText.dueDate, field: "dueDate", width: 190, responsive: 2, sorter: grid.dueDateSorter, formatter: grid.formatters.dueWithLate(text.daysLate) },
            { title: columnText.estimate, field: "estimateHours", hozAlign: "right", width: 130, responsive: 3, sorter: "number", formatter: grid.formatters.estimate(text.noEstimate) },
            { title: columnText.logged, field: "loggedHours", hozAlign: "right", width: 110, responsive: 4, sorter: "number", formatter: grid.formatters.hours, tooltip: loggedTooltip }
        ];
        if (narrow) {
            columns.unshift(grid.collapseColumn());
        }

        table = new window.Tabulator(gridHost, grid.options({
            data,
            index: "issueKey",
            placeholder: text.filterEmpty,
            responsiveLayout: narrow ? "collapse" : "hide",
            responsiveLayoutCollapseStartOpen: false,
            groupBy: grouped ? "parent" : false,
            groupHeader: grouped ? grid.parentGroupHeader({
                browseBaseUrl: config.browseBaseUrl,
                noParentText: text.noParent,
                metaTemplate: text.groupMeta,
                summaryField: "parentSummary"
            }) : undefined,
            groupToggleElement: "header",
            pagination: paged,
            paginationSize: PAGE_SIZE,
            paginationCounter: paged
                ? (size, currentRow, _page, totalRows) => template(text.pageCounter, currentRow, Math.min(currentRow + size - 1, totalRows), totalRows)
                : undefined,
            locale: "app",
            langs: {
                app: {
                    pagination: {
                        first: text.pageFirst,
                        first_title: text.pageFirst,
                        prev: text.pagePrev,
                        prev_title: text.pagePrev,
                        next: text.pageNext,
                        next_title: text.pageNext,
                        last: text.pageLast,
                        last_title: text.pageLast
                    }
                }
            },
            columnDefaults: { headerSort: true },
            initialSort: [{ column: "dueDate", dir: "desc" }],
            columns
        }));

        table.on("tableBuilt", () => applyChip(chips.value()));
    }

    function renderResults(data) {
        rows = Array.isArray(data.rows) ? data.rows : [];
        lastJql = data.jql || "";
        gridHost.setAttribute("aria-busy", "false");
        countEl.textContent = template(text.count, rows.length);
        chips.setCounts(jira.statusCounts(rows));
        exportButton.disabled = rows.length === 0;
        jira.jql.update(jqlBlock, lastJql);

        truncatedEl.hidden = !data.truncated;
        truncatedEl.textContent = data.truncated ? template(text.truncated, data.limit) : "";

        if (rows.length === 0) {
            destroyTable();
            showEmpty();
            return;
        }

        setState("");
        buildTable(rows);
    }

    // ------------------------------------------------------------------ search

    async function search(state) {
        const invalid = validate(state);
        errorEl.textContent = invalid || "";
        if (invalid) {
            fold(false);
            return;
        }

        const current = ++requestId;
        showLoading();

        try {
            const response = await fetch(config.searchUrl, {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    "RequestVerificationToken": form.querySelector("input[name='__RequestVerificationToken']")?.value || "",
                    "X-Requested-With": "XMLHttpRequest"
                },
                body: JSON.stringify({
                    advanced: state.mode === "jql",
                    advancedJql: state.jql || null,
                    assignee: state.assignee || null,
                    reporter: state.reporter || null,
                    status: state.status || null,
                    project: state.project || null,
                    issueKey: state.issueKey || null,
                    // Left out when empty: the server then uses today.
                    fromDate: state.from || undefined,
                    toDate: state.to || undefined
                })
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
                showError(payload?.message || text.failed, () => search(state));
                return;
            }

            renderResults(payload.data || {});
            summaryText.textContent = describe(state);
            fold(true);
            remember(state);
        } catch {
            if (current === requestId) {
                showError(text.failed, () => search(state));
            }
        }
    }

    // ------------------------------------------------------------------ export

    let sheetLoader = null;

    function loadSheetJs() {
        if (window.XLSX) {
            return Promise.resolve(window.XLSX);
        }

        sheetLoader = sheetLoader || new Promise((resolve, reject) => {
            const script = document.createElement("script");
            script.src = config.sheetUrl;
            script.onload = () => resolve(window.XLSX);
            script.onerror = () => {
                sheetLoader = null;
                reject(new Error("SheetJS"));
            };
            document.head.appendChild(script);
        });
        return sheetLoader;
    }

    async function exportExcel() {
        if (!table) {
            return;
        }

        exportButton.disabled = true;
        try {
            const XLSX = await loadSheetJs();
            // What the user sees: the active chip and the current sort.
            const data = table.getData("active");
            const header = [columnText.key, columnText.summary, columnText.status, columnText.type,
                columnText.parent, columnText.parentSummary, columnText.dueDate, columnText.estimate, columnText.logged];
            const body = data.map((row) => [
                row.issueKey,
                row.summary,
                row.status,
                row.type,
                row.parent,
                row.parentSummary,
                jira.dates.parseIsoDate(row.dueDate),
                Number(row.estimateHours) || 0,
                Number(row.loggedHours) || 0
            ]);
            const sheet = XLSX.utils.aoa_to_sheet([header].concat(body), { cellDates: true, dateNF: window.Toolbox?.format?.datePattern || "dd/MM/yyyy" });
            sheet["!cols"] = [16, 60, 16, 12, 16, 50, 12, 10, 10].map((wch) => ({ wch }));
            const book = XLSX.utils.book_new();
            XLSX.utils.book_append_sheet(book, sheet, (text.exportSheet || "Jira").slice(0, 31));
            XLSX.writeFile(book, `jira-${jira.dates.formatIsoDate(new Date())}.xlsx`);
        } catch {
            window.Toolbox?.notify(text.exportFailed, "error");
        } finally {
            exportButton.disabled = rows.length === 0;
        }
    }

    // ------------------------------------------------------------------ wiring

    modeButtons.forEach((button) => button.addEventListener("click", () => {
        setMode(button.dataset.searchModeButton);
        errorEl.textContent = "";
    }));

    form.addEventListener("submit", (event) => {
        event.preventDefault();
        search(readState());
    });

    fields.jql?.addEventListener("keydown", (event) => {
        if (event.key === "Enter" && (event.ctrlKey || event.metaKey)) {
            event.preventDefault();
            search(readState());
        }
    });

    savedSelect?.addEventListener("change", () => {
        const saved = savedSelect.selectedOptions[0]?.dataset.jql;
        if (saved) {
            fields.jql.value = jira.jql.format(saved);
        }
    });

    root.querySelector("[data-search-format]")?.addEventListener("click", () => {
        fields.jql.value = jira.jql.format(fields.jql.value);
    });

    root.querySelector("[data-search-reset]")?.addEventListener("click", () => {
        writeState(Object.assign({}, config.defaults));
        errorEl.textContent = "";
    });

    root.querySelector("[data-search-edit]")?.addEventListener("click", openForm);
    exportButton?.addEventListener("click", exportExcel);

    initDates();
    const initial = Object.assign({}, config.defaults, stateFromUrl() || recalled() || {});
    writeState(initial);
    search(readState());
})(window, document);
