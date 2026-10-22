// Jira Worklist, auto-schedule tab (stage 5.4). Three steps: 1 find the open subtasks, 2 place them
// day by day without going over the hours per day, 3 review the proposal (table or calendar), change
// any date, and write it to Jira through the dialog in jira-worklist-tabs.js. Nothing changes in
// Jira before that.
(function (window, document) {
    "use strict";

    const jira = window.ToolboxJira;
    if (!jira) {
        return;
    }

    const { escapeHtml, template, dates, grid } = jira;
    const LOCALE = window.Toolbox?.format?.locale || document.documentElement.lang || "vi-VN";
    const VIETNAMESE = LOCALE.toLowerCase().startsWith("vi");
    // Index by Date.getDay(): 0 = Sunday. Vietnamese keeps the usual short names T2…CN.
    const WEEKDAY_SHORT = VIETNAMESE
        ? ["CN", "T2", "T3", "T4", "T5", "T6", "T7"]
        : Array.from({ length: 7 }, (_, i) => new Intl.DateTimeFormat(LOCALE, { weekday: "short" }).format(new Date(2026, 0, 4 + i)));
    const WEEKDAY_LONG = new Intl.DateTimeFormat(LOCALE, { weekday: "long" });
    const STORAGE_KEY = "toolbox.jira.auto";
    // The server reads at most 120 days of existing work (JiraController.ScheduledSubtasks).
    const DAYS_AHEAD = 120;
    const CRITERIA = ["dueDate", "status", "createDate"];
    const DIRECTIONS = {
        dueDate: ["dueSoonestFirst", "dueLatestFirst"],
        status: ["doingFirst", "todoFirst"],
        createDate: ["createOldestFirst", "createNewestFirst"]
    };
    const DEFAULT_SETTINGS = {
        hoursPerDay: 8,
        skipWeekend: true,
        countExisting: true,
        order: [
            { id: "dueDate", on: true, dir: "dueSoonestFirst" },
            { id: "status", on: false, dir: "doingFirst" },
            { id: "createDate", on: false, dir: "createOldestFirst" }
        ]
    };
    const DOING = /progress|develop|analysis|review|doing|testing|qa|verify/i;

    function readStore() {
        try {
            return JSON.parse(window.localStorage.getItem(STORAGE_KEY) || "{}") || {};
        } catch {
            return {};
        }
    }

    function writeStore(patch) {
        try {
            window.localStorage.setItem(STORAGE_KEY, JSON.stringify(Object.assign(readStore(), patch)));
        } catch {
            // Storage can be off; the page still works, it just forgets.
        }
    }

    // Settings from an older visit may miss fields or name criteria that no longer exist.
    function normaliseSettings(saved) {
        const settings = Object.assign({}, DEFAULT_SETTINGS, saved || {});
        const hours = Number(settings.hoursPerDay);
        settings.hoursPerDay = hours >= 0.5 && hours <= 24 ? hours : DEFAULT_SETTINGS.hoursPerDay;
        const order = Array.isArray(settings.order) ? settings.order.filter((item) => CRITERIA.includes(item?.id)) : [];
        CRITERIA.forEach((id) => {
            if (!order.some((item) => item.id === id)) {
                order.push(Object.assign({}, DEFAULT_SETTINGS.order.find((item) => item.id === id)));
            }
        });
        settings.order = order.map((item) => ({
            id: item.id,
            on: !!item.on,
            dir: DIRECTIONS[item.id].includes(item.dir) ? item.dir : DIRECTIONS[item.id][0]
        }));
        return settings;
    }

    const isWeekend = (date) => date.getDay() === 0 || date.getDay() === 6;
    const dueIso = (row) => (row?.dueDate ? String(row.dueDate).slice(0, 10) : "");
    const hours = (value) => jira.displayHours(Number(value) || 0);

    function createModule() {
        let root = null;
        let text = {};
        let config = {};
        let settings = normaliseSettings();
        let rows = [];
        let proposals = [];
        // Rows due in the scheduled days that are not being scheduled (iso -> { hours, count }).
        let existingByDay = new Map();
        let capacity = DEFAULT_SETTINGS.hoursPerDay;
        let skipWeekend = true;
        let view = "table";
        let searchTable = null;
        let resultTable = null;
        let boundHandlers = [];
        let projectsSeen = new Set();
        let moveKey = null;
        let requestId = 0;

        // Kendo widgets live in markup that stays on the page between visits, so they are made once.
        const widgets = window.ToolboxJiraAutoWidgets || (window.ToolboxJiraAutoWidgets = {});

        function on(el, event, handler) {
            if (el) {
                el.addEventListener(event, handler);
                boundHandlers.push([el, event, handler]);
            }
        }

        function offAll() {
            boundHandlers.forEach(([el, event, handler]) => el.removeEventListener(event, handler));
            boundHandlers = [];
        }

        const q = (selector) => root?.querySelector(selector);
        const tracker = () => window.ToolboxJiraUnsaved;
        const isChanged = (proposal) => proposal.newDue !== proposal.originalDue;
        const findProposal = (key) => proposals.find((proposal) => proposal.subTaskKey === key);

        function kendoReady() {
            const $ = window.jQuery;
            if (!$?.fn?.kendoDatePicker) {
                return false;
            }

            if (window.kendo.cultures?.[LOCALE]) {
                window.kendo.culture(LOCALE);
            }

            return true;
        }

        function datePicker(input) {
            if (!input) {
                return null;
            }

            if (input.dataset.pickerReady) {
                return window.jQuery?.(input).data("kendoDatePicker") || null;
            }

            input.dataset.pickerReady = "true";
            if (kendoReady()) {
                return window.jQuery(input).kendoDatePicker({ format: window.Toolbox?.format?.datePattern || "dd/MM/yyyy" }).data("kendoDatePicker");
            }

            input.type = "date";
            return null;
        }

        function readDate(input) {
            const picker = window.jQuery?.(input).data("kendoDatePicker");
            return picker ? dates.formatIsoDate(picker.value()) : (input?.value || "");
        }

        function writeDate(input, iso) {
            const picker = window.jQuery?.(input).data("kendoDatePicker");
            if (picker) {
                picker.value(dates.parseIsoDate(iso));
            } else if (input) {
                input.value = iso || "";
            }
        }

        // ------------------------------------------------------------------ step 1

        function readFilter() {
            const field = (name) => q(`[data-auto-field="${name}"]`);
            return {
                project: field("project").value,
                status: field("status").value,
                text: field("text").value.trim(),
                dueFrom: readDate(field("dueFrom")),
                dueTo: readDate(field("dueTo"))
            };
        }

        function writeFilter(filter) {
            const field = (name) => q(`[data-auto-field="${name}"]`);
            if (filter.project) {
                projectsSeen.add(filter.project);
                refreshProjects(filter.project);
            }

            field("status").value = ["all", "todo", "doing"].includes(filter.status) ? filter.status : "all";
            field("text").value = filter.text || "";
            writeDate(field("dueFrom"), filter.dueFrom);
            writeDate(field("dueTo"), filter.dueTo);
        }

        // The project list grows with every project seen, so a filtered search keeps the others.
        function refreshProjects(selected) {
            const select = q('[data-auto-field="project"]');
            const current = selected ?? select.value;
            rows.forEach((row) => row.project && projectsSeen.add(row.project));
            const allLabel = select.dataset.allLabel || (select.dataset.allLabel = select.options[0]?.textContent || "");
            select.innerHTML = `<option value="">${escapeHtml(allLabel)}</option>`
                + Array.from(projectsSeen).sort().map((project) => `<option>${escapeHtml(project)}</option>`).join("");
            select.value = projectsSeen.has(current) ? current : "";
        }

        function setListState(kind, message) {
            const host = q("[data-auto-state]");
            const gridHost = q("[data-auto-grid]");
            gridHost.hidden = !!kind;
            if (!kind) {
                host.innerHTML = "";
                return;
            }

            if (kind === "loading") {
                host.innerHTML = `<div class="jira-board-loading" role="status">${'<span class="tbx-skeleton"></span>'.repeat(4)}<span class="visually-hidden">${escapeHtml(text.loading)}</span></div>`;
            } else if (kind === "error") {
                host.innerHTML = `<div class="tbx-error-state" role="alert">${jira.iconSvg("circle-alert")}
                    <div class="jira-search-error-body"><p>${escapeHtml(message || text.error)}</p>
                    <button type="button" class="btn btn-sm btn-outline-secondary" data-auto-retry>${escapeHtml(text.retry)}</button></div></div>`;
            } else {
                host.innerHTML = `<div class="tbx-empty" role="status"><span class="tbx-empty-icon">${jira.iconSvg("inbox", 20)}</span>
                    <p class="tbx-empty-text">${escapeHtml(text.empty)}</p>
                    <button type="button" class="btn btn-sm btn-outline-secondary" data-auto-edit-filters>${escapeHtml(text.editFilters)}</button></div>`;
            }
        }

        async function search() {
            const filter = readFilter();
            writeStore({ filter });
            const params = new URLSearchParams();
            Object.entries(filter).forEach(([key, value]) => value && params.set(key, value));

            const id = ++requestId;
            setListState("loading");
            q("[data-auto-list-title]").textContent = "";
            q("[data-auto-noestimate]").hidden = true;
            let message = null;
            let found = null;

            try {
                const response = await fetch(`${text.apiUrl}?${params.toString()}`, { headers: { "X-Requested-With": "XMLHttpRequest" } });
                const payload = await response.json().catch(() => null);
                if (payload?.requiresLogin) {
                    window.location.href = `${config.loginUrl || "/Identity/Account/Login"}?returnUrl=${encodeURIComponent(window.location.pathname)}`;
                    return;
                }

                if (!response.ok || !payload?.success) {
                    message = payload?.message || text.error;
                } else if (payload.data?.errorMessage) {
                    message = payload.data.errorMessage;
                } else {
                    found = Array.isArray(payload.data?.rows) ? payload.data.rows : [];
                }
            } catch {
                message = text.error;
            }

            if (id !== requestId || !root) {
                return;
            }

            rows = (found || []).map((row) => Object.assign({}, row, { remainingHours: jira.remainingHours(row) }));
            discard();
            refreshProjects();
            if (message) {
                setListState("error", message);
            } else if (rows.length === 0) {
                setListState("empty");
            } else {
                setListState(null);
            }

            renderList();
            renderStep2();
        }

        function renderList() {
            q("[data-auto-list-title]").textContent = rows.length ? template(text.listTitle, rows.length) : "";
            const missing = rows.filter((row) => !(Number(row.estimateHours) > 0));
            const warning = q("[data-auto-noestimate]");
            warning.hidden = missing.length === 0;
            q("[data-auto-noestimate-text]").textContent = template(text.noEstimateWarning, missing.length);
            q("[data-auto-noestimate-keys]").innerHTML = missing
                .map((row) => jira.issueLink(row.subTaskKey, config.browseBaseUrl)).join(", ");

            searchTable?.destroy();
            searchTable = null;
            if (!rows.length || !window.Tabulator) {
                return;
            }

            const narrow = grid.isNarrow();
            const columns = [
                { title: text.columns.key, field: "subTaskKey", width: narrow ? 110 : 150, responsive: 0, formatter: grid.formatters.issueKey(config.browseBaseUrl) },
                { title: text.columns.summary, field: "subTaskSummary", hozAlign: "left", minWidth: narrow ? 120 : 220, widthGrow: 3, responsive: 0, formatter: grid.formatters.text },
                { title: text.columns.status, field: "status", width: 140, responsive: 2, formatter: grid.formatters.status },
                { title: text.columns.dueDate, field: "dueDate", width: 180, responsive: 1, sorter: grid.dueDateSorter, formatter: grid.formatters.dueWithLate(text.late) },
                { title: text.columns.estimate, field: "estimateHours", hozAlign: "right", width: 130, responsive: 3, sorter: "number", formatter: grid.formatters.estimate(text.noEstimate) },
                { title: text.columns.logged, field: "loggedHours", hozAlign: "right", width: 100, responsive: 4, sorter: "number", formatter: grid.formatters.hours },
                { title: text.columns.remaining, field: "remainingHours", hozAlign: "right", width: 100, responsive: 1, sorter: "number", formatter: grid.formatters.hours }
            ];
            if (narrow) {
                columns.unshift(grid.collapseColumn());
            }

            // Group by parent issue only when a parent holds more than one of these subtasks.
            const parents = rows.map((row) => row.issueKey).filter(Boolean);
            const grouped = parents.length !== new Set(parents).size;
            searchTable = new window.Tabulator(q("[data-auto-grid]"), grid.options({
                data: rows,
                index: "subTaskKey",
                placeholder: text.empty,
                responsiveLayout: narrow ? "collapse" : "hide",
                responsiveLayoutCollapseStartOpen: false,
                groupBy: grouped ? "issueKey" : false,
                groupHeader: grouped ? grid.parentGroupHeader({
                    browseBaseUrl: config.browseBaseUrl,
                    noParentText: text.noParent,
                    metaTemplate: text.groupMeta,
                    summaryField: "issueSummary"
                }) : undefined,
                groupToggleElement: "header",
                columns
            }));
        }

        // ------------------------------------------------------------------ step 2

        function defaultStart() {
            // The next Monday: auto-schedule is usually run to plan the coming week.
            const today = dates.today();
            return dates.addDays(today, ((8 - today.getDay()) % 7) || 7);
        }

        function renderStep2() {
            const button = q("[data-auto-schedule]");
            button.disabled = rows.length === 0;
            const summary = q("[data-auto-settings-summary]");
            if (!rows.length) {
                summary.textContent = text.waiting;
                return;
            }

            const parts = [template(text.settingsSummary, jira.displayDate(defaultStart()), hours(settings.hoursPerDay))];
            if (settings.skipWeekend) {
                parts.push(text.settingsWeekendOff);
            }

            if (settings.countExisting) {
                parts.push(text.settingsExisting);
            }

            summary.textContent = parts.join(" · ");
        }

        function configModal() {
            return document.querySelector("[data-auto-config-modal]");
        }

        function hoursInput() {
            const input = configModal().querySelector("[data-auto-cfg-hours]");
            if (!widgets.hours && window.jQuery?.fn?.kendoNumericTextBox && kendoReady()) {
                widgets.hours = window.jQuery(input).kendoNumericTextBox({ min: 0.5, max: 24, step: 0.5, decimals: 1, format: "n1" }).data("kendoNumericTextBox");
            }

            return {
                get: () => (widgets.hours ? widgets.hours.value() : Number(input.value)),
                set: (value) => (widgets.hours ? widgets.hours.value(value) : (input.value = value))
            };
        }

        function firstSelect() {
            const select = configModal().querySelector("[data-auto-cfg-first]");
            const data = rows.map((row) => ({ value: row.subTaskKey, text: `${row.subTaskKey} — ${row.subTaskSummary}` }));
            if (window.jQuery?.fn?.kendoMultiSelect && kendoReady()) {
                widgets.first = widgets.first || window.jQuery(select).kendoMultiSelect({
                    dataTextField: "text",
                    dataValueField: "value",
                    autoClose: false,
                    tagTemplate: (item) => escapeHtml(item.value)
                }).data("kendoMultiSelect");
                widgets.first.setDataSource(new window.kendo.data.DataSource({ data }));
                widgets.first.value([]);
                return () => widgets.first.value();
            }

            select.innerHTML = data.map((item) => `<option value="${escapeHtml(item.value)}">${escapeHtml(item.text)}</option>`).join("");
            return () => Array.from(select.selectedOptions).map((option) => option.value);
        }

        let readFirstKeys = () => [];

        function renderOrder() {
            const list = configModal().querySelector("[data-auto-cfg-order]");
            list.innerHTML = settings.order.map((item, index) => {
                const label = text.criteria[item.id] || item.id;
                const options = DIRECTIONS[item.id]
                    .map((dir) => `<option value="${dir}" ${dir === item.dir ? "selected" : ""}>${escapeHtml(text.directions[dir] || dir)}</option>`).join("");
                return `<li class="jira-auto-order-item" data-criterion="${item.id}">
                    <div class="form-check">
                        <input class="form-check-input" type="checkbox" id="jira-auto-crit-${item.id}" data-crit-on ${item.on ? "checked" : ""} />
                        <label class="form-check-label" for="jira-auto-crit-${item.id}">${escapeHtml(label)}</label>
                    </div>
                    <select class="form-select form-select-sm" data-crit-dir aria-label="${escapeHtml(`${text.directionOf}: ${label}`)}">${options}</select>
                    <button type="button" class="btn tbx-icon-button jira-auto-order-up" data-crit-move="-1" ${index === 0 ? "disabled" : ""}
                        aria-label="${escapeHtml(template(text.moveUp, label))}" title="${escapeHtml(template(text.moveUp, label))}">${jira.iconSvg("chevron-down", 16)}</button>
                    <button type="button" class="btn tbx-icon-button" data-crit-move="1" ${index === settings.order.length - 1 ? "disabled" : ""}
                        aria-label="${escapeHtml(template(text.moveDown, label))}" title="${escapeHtml(template(text.moveDown, label))}">${jira.iconSvg("chevron-down", 16)}</button>
                </li>`;
            }).join("");
        }

        function onOrderChange(event) {
            const item = event.target.closest("[data-criterion]");
            const entry = item && settings.order.find((x) => x.id === item.dataset.criterion);
            if (!entry) {
                return;
            }

            if (event.target.matches("[data-crit-on]")) {
                entry.on = event.target.checked;
            } else if (event.target.matches("[data-crit-dir]")) {
                entry.dir = event.target.value;
            }
        }

        function onOrderClick(event) {
            const button = event.target.closest("[data-crit-move]");
            const item = button?.closest("[data-criterion]");
            if (!item) {
                return;
            }

            const from = settings.order.findIndex((x) => x.id === item.dataset.criterion);
            const to = from + Number(button.dataset.critMove);
            if (to < 0 || to >= settings.order.length) {
                return;
            }

            [settings.order[from], settings.order[to]] = [settings.order[to], settings.order[from]];
            renderOrder();
            configModal().querySelector(`[data-criterion="${item.dataset.criterion}"] [data-crit-move="${button.dataset.critMove}"]:not(:disabled)`)?.focus();
        }

        function openConfig() {
            if (!rows.length || !window.bootstrap) {
                return;
            }

            const modal = configModal();
            settings = normaliseSettings(readStore().settings);
            const start = modal.querySelector("[data-auto-cfg-start]");
            datePicker(start);
            writeDate(start, dates.formatIsoDate(defaultStart()));
            hoursInput().set(settings.hoursPerDay);
            modal.querySelector("[data-auto-cfg-weekend]").checked = settings.skipWeekend;
            modal.querySelector("[data-auto-cfg-existing]").checked = settings.countExisting;
            modal.querySelector("[data-auto-cfg-error]").textContent = "";
            readFirstKeys = firstSelect();
            renderOrder();
            window.bootstrap.Modal.getOrCreateInstance(modal).show();
        }

        async function onConfigSubmit(event) {
            event.preventDefault();
            const modal = configModal();
            const error = modal.querySelector("[data-auto-cfg-error]");
            const startIso = readDate(modal.querySelector("[data-auto-cfg-start]"));
            const perDay = Number(hoursInput().get());
            if (!dates.parseIsoDate(startIso)) {
                error.textContent = text.startDateRequired;
                return;
            }

            if (!(perDay >= 0.5 && perDay <= 24)) {
                error.textContent = text.hoursRange;
                return;
            }

            settings.hoursPerDay = perDay;
            settings.skipWeekend = modal.querySelector("[data-auto-cfg-weekend]").checked;
            settings.countExisting = modal.querySelector("[data-auto-cfg-existing]").checked;
            writeStore({ settings });
            renderStep2();
            const firstKeys = readFirstKeys();
            window.bootstrap.Modal.getOrCreateInstance(modal).hide();

            // A new proposal replaces the one on screen, including dates not yet in Jira.
            if (tracker() && !(await tracker().confirmDiscard("auto"))) {
                return;
            }

            await schedule(dates.parseIsoDate(startIso), firstKeys);
        }

        function comparator() {
            const active = settings.order.filter((item) => item.on);
            const ranked = active.length ? active : [{ id: "dueDate", dir: "dueSoonestFirst" }];
            const time = (value, missing) => (value ? new Date(value).getTime() : missing);

            return (a, b) => {
                for (const { id, dir } of ranked) {
                    let cmp;
                    if (id === "status") {
                        const rank = (row) => (DOING.test(row.status || "") === (dir === "doingFirst") ? 0 : 1);
                        cmp = rank(a) - rank(b);
                    } else if (id === "createDate") {
                        cmp = time(a.createDate, 0) - time(b.createDate, 0);
                        cmp = dir === "createOldestFirst" ? cmp : -cmp;
                    } else {
                        // Without a due date: last either way.
                        const left = time(a.dueDate, null);
                        const right = time(b.dueDate, null);
                        cmp = left === null || right === null
                            ? (left === null) - (right === null)
                            : (dir === "dueSoonestFirst" ? left - right : right - left);
                    }

                    if (cmp) {
                        return cmp;
                    }
                }

                return 0;
            };
        }

        // Resolves false when the existing work could not be read (days are then treated as free).
        async function loadExisting(start) {
            existingByDay = new Map();
            if (!settings.countExisting) {
                return true;
            }

            const params = new URLSearchParams({
                from: dates.formatIsoDate(start),
                to: dates.formatIsoDate(dates.addDays(start, DAYS_AHEAD))
            });
            try {
                const response = await fetch(`${text.existingUrl}?${params.toString()}`, { headers: { "X-Requested-With": "XMLHttpRequest" } });
                const payload = await response.json().catch(() => null);
                if (!response.ok || !payload?.success) {
                    return false;
                }

                const scheduling = new Set(rows.map((row) => row.subTaskKey));
                (payload.data?.rows || [])
                    .filter((row) => !scheduling.has(row.subTaskKey) && dueIso(row))
                    .forEach((row) => {
                        const day = existingByDay.get(dueIso(row)) || { hours: 0, count: 0 };
                        day.hours += jira.loadHours(row);
                        day.count += 1;
                        existingByDay.set(dueIso(row), day);
                    });
                return true;
            } catch {
                return false;
            }
        }

        async function schedule(start, firstKeys) {
            const button = q("[data-auto-schedule]");
            button.disabled = true;
            const existingRead = await loadExisting(start);
            if (!root) {
                return;
            }

            q("[data-auto-existing-warning]").hidden = existingRead;

            button.disabled = rows.length === 0;
            capacity = settings.hoursPerDay;
            skipWeekend = settings.skipWeekend;

            const first = new Set(firstKeys);
            const byDue = (a, b) => (dueIso(a) || "9999").localeCompare(dueIso(b) || "9999");
            const ordered = rows.filter((row) => first.has(row.subTaskKey)).sort(byDue)
                .concat(rows.filter((row) => !first.has(row.subTaskKey)).sort(comparator()));
            if (!ordered.length) {
                window.Toolbox?.notify(text.noProposals, "warning");
                return;
            }

            const used = new Map(Array.from(existingByDay, ([iso, day]) => [iso, day.hours]));
            let cursor = dates.startOfDay(start);
            proposals = ordered.map((row, index) => {
                const need = jira.remainingHours(row);
                // Move on while the day is off, or already holds work and this subtask would not fit.
                for (let guard = 0; guard < 366; guard += 1) {
                    const load = used.get(dates.formatIsoDate(cursor)) || 0;
                    if (!(skipWeekend && isWeekend(cursor)) && !(load > 0 && load + need > capacity)) {
                        break;
                    }

                    cursor = dates.addDays(cursor, 1);
                }

                const iso = dates.formatIsoDate(cursor);
                used.set(iso, (used.get(iso) || 0) + need);
                return Object.assign({}, row, { order: index, originalDue: dueIso(row), newDue: iso, written: false, error: "" });
            });

            reportUnsaved();
            const step = q('[data-auto-step="3"]');
            step.hidden = false;
            renderResult();
            window.Toolbox?.notify(template(text.proposalsToast, proposals.length), "success");
            step.scrollIntoView({ behavior: "smooth", block: "start" });
        }

        // ------------------------------------------------------------------ step 3

        function reportUnsaved() {
            tracker()?.set("auto", proposals.filter(isChanged).length);
        }

        function dayLoad(iso) {
            const existing = existingByDay.get(iso) || { hours: 0, count: 0 };
            const proposed = proposals.filter((proposal) => proposal.newDue === iso)
                .reduce((sum, proposal) => sum + jira.remainingHours(proposal), 0);
            return { existing, proposed, total: existing.hours + proposed };
        }

        function dateText(iso) {
            return iso ? jira.displayDate(iso) : "–";
        }

        function changeHtml(proposal) {
            const button = `<button type="button" class="jira-card-move" data-auto-move="${escapeHtml(proposal.subTaskKey)}"
                title="${escapeHtml(template(text.moveButton, proposal.subTaskKey))}" aria-label="${escapeHtml(template(text.moveButton, proposal.subTaskKey))}">${jira.iconSvg("calendar", 14)}</button>`;
            const range = isChanged(proposal)
                ? `<span class="jira-auto-old">${escapeHtml(dateText(proposal.originalDue))}</span> → <strong>${escapeHtml(dateText(proposal.newDue))}</strong>`
                : `<span>${escapeHtml(dateText(proposal.newDue))}</span> <span class="jira-auto-same">${escapeHtml(text.unchanged)}</span>`;
            const status = proposal.error
                ? `<span class="jira-card-error">${escapeHtml(template(text.saveError, proposal.error))}</span>`
                : proposal.written ? `<span class="jira-auto-written">✓ ${escapeHtml(text.written)}</span>` : "";
            return `<span class="jira-auto-change">${range}${button}</span>${status}`;
        }

        function renderResult() {
            if (!proposals.length) {
                return;
            }

            const changed = proposals.filter(isChanged).length;
            const days = new Set(proposals.map((proposal) => proposal.newDue)).size;
            q("[data-auto-result-meta]").textContent = template(text.resultMeta, proposals.length, days, changed);
            q("[data-auto-write]").disabled = changed === 0;
            q("[data-auto-write]").title = changed === 0 ? text.nothingToWrite : "";

            const showTable = view === "table";
            q("[data-auto-result-table]").hidden = !showTable;
            q("[data-auto-calendar-wrap]").hidden = showTable;
            if (showTable) {
                renderTable();
            } else {
                renderCalendar();
            }
        }

        function sortedProposals() {
            return proposals.slice().sort((a, b) => a.newDue.localeCompare(b.newDue) || a.order - b.order);
        }

        function dayGroupHeader(value, count) {
            const day = dates.parseIsoDate(value);
            const { total } = dayLoad(value);
            const over = total > capacity;
            const load = over ? `${hours(total)} · ${template(text.over, hours(total - capacity))}` : template(text.load, hours(total), hours(capacity));
            return `<span class="jira-group-title">${escapeHtml(`${WEEKDAY_LONG.format(day)}, ${jira.displayDate(day)}`)}</span>`
                + `<span class="jira-group-meta ${over ? "is-over" : ""}">${escapeHtml(template(text.dayGroup, count, load))}</span>`;
        }

        function renderTable() {
            resultTable?.destroy();
            resultTable = null;
            if (!window.Tabulator) {
                return;
            }

            const narrow = grid.isNarrow();
            const columns = [
                { title: text.columns.key, field: "subTaskKey", width: narrow ? 110 : 150, responsive: 0, formatter: grid.formatters.issueKey(config.browseBaseUrl) },
                { title: text.columns.summary, field: "subTaskSummary", hozAlign: "left", minWidth: narrow ? 140 : 220, widthGrow: 3, responsive: 2, formatter: grid.formatters.text },
                { title: text.columns.status, field: "status", width: 140, responsive: 3, formatter: grid.formatters.status },
                { title: text.columns.remaining, field: "remainingHours", hozAlign: "right", width: 100, responsive: 4, formatter: grid.formatters.hours },
                { title: text.columns.dueChange, field: "newDue", width: narrow ? undefined : 250, minWidth: narrow ? 130 : 200, responsive: 0, headerSort: false, tooltip: false, formatter: (cell) => changeHtml(cell.getRow().getData()) }
            ];
            if (narrow) {
                columns.unshift(grid.collapseColumn());
            }

            resultTable = new window.Tabulator(q("[data-auto-result-table]"), grid.options({
                data: sortedProposals(),
                index: "subTaskKey",
                columnDefaults: { headerSort: false },
                responsiveLayout: narrow ? "collapse" : "hide",
                responsiveLayoutCollapseStartOpen: false,
                groupBy: "newDue",
                groupHeader: dayGroupHeader,
                groupToggleElement: false,
                rowFormatter: (row) => {
                    const data = row.getData();
                    const el = row.getElement();
                    el.classList.toggle("is-changed", isChanged(data));
                    el.classList.toggle("has-error", !!data.error);
                },
                columns
            }));
        }

        function calendarDays() {
            const isos = proposals.map((proposal) => proposal.newDue).sort();
            const first = dates.parseIsoDate(isos[0]);
            const last = dates.parseIsoDate(isos[isos.length - 1]);
            const result = [];
            for (let day = first; day <= last; day = dates.addDays(day, 1)) {
                const iso = dates.formatIsoDate(day);
                if (!(skipWeekend && isWeekend(day)) || isos.includes(iso)) {
                    result.push(day);
                }
            }

            return result;
        }

        function cardHtml(proposal) {
            const classes = ["jira-card", isChanged(proposal) ? "is-moved" : "", proposal.error ? "has-error" : ""].join(" ");
            return `<article class="${classes}" data-card data-key="${escapeHtml(proposal.subTaskKey)}" draggable="true" tabindex="0"
                    title="${escapeHtml(proposal.project || "")}" aria-label="${escapeHtml(`${proposal.subTaskKey}: ${proposal.subTaskSummary}`)}">
                <div class="jira-card-top">${jira.issueLink(proposal.subTaskKey, config.browseBaseUrl, "jira-card-key")}</div>
                <p class="jira-card-title">${escapeHtml(proposal.subTaskSummary)}</p>
                <div class="jira-card-meta">
                    ${jira.statusPill(proposal.status, proposal.statusTone)}
                    <span class="jira-card-hours">${escapeHtml(hours(proposal.remainingHours))}</span>
                </div>
                <div class="jira-card-notes jira-auto-card-change">${changeHtml(proposal)}</div>
            </article>`;
        }

        function renderCalendar() {
            const host = q("[data-auto-calendar]");
            host.innerHTML = calendarDays().map((day) => {
                const iso = dates.formatIsoDate(day);
                const items = sortedProposals().filter((proposal) => proposal.newDue === iso);
                const { existing, proposed, total } = dayLoad(iso);
                const over = total > capacity;
                const scale = Math.max(capacity, total) || 1;
                const label = over ? `${hours(total)} · ${template(text.over, hours(total - capacity))}` : template(text.load, hours(total), hours(capacity));
                return `<section class="jira-day ${isWeekend(day) ? "is-weekend" : ""}" data-drop-date="${iso}" aria-label="${escapeHtml(`${WEEKDAY_LONG.format(day)}, ${jira.displayDate(day)}`)}">
                    <header class="jira-day-head">
                        <span class="jira-day-name">${escapeHtml(WEEKDAY_SHORT[day.getDay()])} <strong>${escapeHtml(jira.shortDate(day))}</strong></span>
                        ${items.length ? `<span class="jira-day-count">${items.length}</span>` : ""}
                    </header>
                    <div class="jira-load ${over ? "is-over" : ""}">
                        <span class="jira-load-text">${escapeHtml(label)}</span>
                        <span class="jira-load-bar" aria-hidden="true">
                            <span class="jira-load-fill is-existing" style="width:${(existing.hours / scale) * 100}%"></span>
                            <span class="jira-load-fill is-open" style="width:${(proposed / scale) * 100}%"></span>
                        </span>
                        ${existing.count ? `<span class="jira-load-text">${escapeHtml(template(text.existingLine, existing.count, hours(existing.hours)))}</span>` : ""}
                    </div>
                    <div class="jira-day-body">${items.map(cardHtml).join("")}</div>
                </section>`;
            }).join("");
        }

        function setView(next) {
            view = next;
            root.querySelectorAll("[data-auto-view]").forEach((button) => {
                const active = button.dataset.autoView === next;
                button.classList.toggle("is-active", active);
                button.setAttribute("aria-pressed", active ? "true" : "false");
            });
            renderResult();
        }

        function moveProposal(key, iso) {
            const proposal = findProposal(key);
            if (!proposal || !dates.parseIsoDate(iso) || proposal.newDue === iso) {
                return;
            }

            proposal.newDue = iso;
            proposal.written = false;
            proposal.error = "";
            reportUnsaved();
            renderResult();
            window.Toolbox?.notify(template(text.movedToast, key, jira.displayDate(iso)), "info");
        }

        function openMove(key) {
            const proposal = findProposal(key);
            const modal = document.querySelector("[data-auto-move-modal]");
            if (!proposal || !modal || !window.bootstrap) {
                return;
            }

            moveKey = key;
            modal.querySelector("[data-auto-move-item]").textContent =
                template(text.moveItem, `${key} — ${proposal.subTaskSummary}`, jira.displayDate(proposal.newDue));
            const input = modal.querySelector("[data-auto-move-date]");
            datePicker(input);
            writeDate(input, proposal.newDue);
            window.bootstrap.Modal.getOrCreateInstance(modal).show();
        }

        function onMoveSubmit(event) {
            event.preventDefault();
            const modal = document.querySelector("[data-auto-move-modal]");
            const iso = readDate(modal.querySelector("[data-auto-move-date]"));
            if (!dates.parseIsoDate(iso)) {
                return;
            }

            window.bootstrap.Modal.getOrCreateInstance(modal).hide();
            moveProposal(moveKey, iso);
        }

        function discard() {
            proposals = [];
            existingByDay = new Map();
            reportUnsaved();
            resultTable?.destroy();
            resultTable = null;
            if (root) {
                q('[data-auto-step="3"]').hidden = true;
                q("[data-auto-calendar]").innerHTML = "";
            }
        }

        function applyResults(results) {
            results.forEach((result) => {
                const proposal = findProposal(result.issueKey);
                if (!proposal) {
                    return;
                }

                if (result.success) {
                    proposal.originalDue = proposal.newDue;
                    proposal.written = true;
                    proposal.error = "";
                    const row = rows.find((item) => item.subTaskKey === result.issueKey);
                    if (row) {
                        row.dueDate = `${proposal.newDue}T00:00:00`;
                        row.isOverdue = false;
                        row.daysOverdue = 0;
                    }
                } else {
                    proposal.error = result.message || "";
                }
            });
            reportUnsaved();
            renderResult();
            searchTable?.replaceData(rows);
        }

        // ------------------------------------------------------------------ events

        function onRootClick(event) {
            if (event.target.closest("[data-auto-retry]")) {
                search();
            } else if (event.target.closest("[data-auto-edit-filters]")) {
                q('[data-auto-field="project"]').focus();
            } else if (event.target.closest("[data-auto-move]")) {
                event.preventDefault();
                openMove(event.target.closest("[data-auto-move]").dataset.autoMove);
            } else if (event.target.closest("[data-auto-view]")) {
                setView(event.target.closest("[data-auto-view]").dataset.autoView);
            }
        }

        function onDragStart(event) {
            const card = event.target.closest?.("[data-card]");
            if (card) {
                card.classList.add("is-dragging");
                event.dataTransfer.setData("text/plain", card.dataset.key);
                event.dataTransfer.effectAllowed = "move";
            }
        }

        function onDragEnd(event) {
            event.target.closest?.("[data-card]")?.classList.remove("is-dragging");
            q("[data-auto-calendar]").querySelectorAll(".is-drag-over").forEach((el) => el.classList.remove("is-drag-over"));
        }

        function onDragOver(event) {
            const zone = event.target.closest?.("[data-drop-date]");
            if (zone) {
                event.preventDefault();
                zone.classList.add("is-drag-over");
            }
        }

        function onDragLeave(event) {
            const zone = event.target.closest?.("[data-drop-date]");
            if (zone && !zone.contains(event.relatedTarget)) {
                zone.classList.remove("is-drag-over");
            }
        }

        function onDrop(event) {
            const zone = event.target.closest?.("[data-drop-date]");
            if (zone) {
                event.preventDefault();
                zone.classList.remove("is-drag-over");
                moveProposal(event.dataTransfer.getData("text/plain"), zone.dataset.dropDate);
            }
        }

        async function onSearchSubmit(event) {
            event.preventDefault();
            // A new search replaces the rows, including a proposal not yet in Jira.
            if (tracker() && !(await tracker().confirmDiscard("auto"))) {
                return;
            }

            search();
        }

        async function onDiscardClick() {
            if (!tracker() || await tracker().confirmDiscard("auto")) {
                discard();
            }
        }

        function onWriteClick() {
            if (!proposals.some(isChanged)) {
                window.Toolbox?.notify(text.nothingToWrite, "info");
                return;
            }

            tracker()?.save();
        }

        return {
            init() {
                root = document.querySelector("[data-jira-auto]");
                if (!root) {
                    return;
                }

                config = jira.parseJsonScript("jira-worklist-config", {});
                text = jira.parseJsonScript("jira-auto-schedule-config", {});
                settings = normaliseSettings(readStore().settings);
                rows = [];
                proposals = [];
                view = "table";

                root.querySelectorAll("[data-auto-date]").forEach(datePicker);
                writeFilter(readStore().filter || {});

                on(q("[data-auto-filter]"), "submit", onSearchSubmit);
                on(q("[data-auto-schedule]"), "click", openConfig);
                on(q("[data-auto-discard]"), "click", onDiscardClick);
                on(q("[data-auto-write]"), "click", onWriteClick);
                on(root, "click", onRootClick);
                const calendar = q("[data-auto-calendar]");
                on(calendar, "dragstart", onDragStart);
                on(calendar, "dragend", onDragEnd);
                on(calendar, "dragover", onDragOver);
                on(calendar, "dragleave", onDragLeave);
                on(calendar, "drop", onDrop);
                const modal = configModal();
                on(modal.querySelector("[data-auto-config-form]"), "submit", onConfigSubmit);
                on(modal.querySelector("[data-auto-cfg-order]"), "change", onOrderChange);
                on(modal.querySelector("[data-auto-cfg-order]"), "click", onOrderClick);
                on(document.querySelector("[data-auto-move-form]"), "submit", onMoveSubmit);

                tracker()?.register("auto", {
                    changes: () => proposals.filter(isChanged).map((proposal) => ({
                        issueKey: proposal.subTaskKey,
                        summary: proposal.subTaskSummary,
                        dueDate: proposal.newDue,
                        previousDueDate: proposal.originalDue
                    })),
                    applyResults,
                    discard
                });

                // Cards by day read better than a table on a phone.
                setView(grid.isNarrow() ? "calendar" : "table");
                renderStep2();
                search();
            },
            destroy() {
                offAll();
                requestId += 1;
                discard();
                searchTable?.destroy();
                searchTable = null;
                tracker()?.unregister("auto");
                rows = [];
                if (root) {
                    setListState(null);
                    q("[data-auto-list-title]").textContent = "";
                    q("[data-auto-noestimate]").hidden = true;
                }

                root = null;
            }
        };
    }

    window.ToolboxJiraAutoSchedule = createModule();
})(window, document);
