// Jira Worklist, Board tab (stage 5.3). Subtasks by due date: one column a day (a calendar in the
// Month view). A card moves by drag and drop, by keyboard (Space, ← →, Enter / Esc) or with
// "Change due date"; moves stay in the browser until "Write to Jira" (jira-worklist-tabs.js).
(function (window, document) {
    "use strict";

    const jira = window.ToolboxJira;
    if (!jira) {
        return;
    }

    const { escapeHtml, template, dates } = jira;
    const LOCALE = window.Toolbox?.format?.locale || document.documentElement.lang || "vi-VN";
    const VIETNAMESE = LOCALE.toLowerCase().startsWith("vi");
    // Index by Date.getDay(): 0 = Sunday. Vietnamese keeps the usual short names T2…CN.
    const WEEKDAY_SHORT = VIETNAMESE
        ? ["CN", "T2", "T3", "T4", "T5", "T6", "T7"]
        : Array.from({ length: 7 }, (_, i) => new Intl.DateTimeFormat(LOCALE, { weekday: "short" }).format(new Date(2026, 0, 4 + i)));
    const WEEKDAY_LONG = new Intl.DateTimeFormat(LOCALE, { weekday: "long" });
    const PREFS_KEY = "toolbox.jira.board";
    const CHIPS_ON_CELL = 3;

    function hours(value) {
        return jira.displayHours(Number(value) || 0);
    }

    function readPrefs() {
        try {
            return JSON.parse(window.localStorage.getItem(PREFS_KEY) || "{}") || {};
        } catch {
            return {};
        }
    }

    function writePrefs(prefs) {
        try {
            window.localStorage.setItem(PREFS_KEY, JSON.stringify(prefs));
        } catch {
            // Storage can be off; the switches still work for this visit.
        }
    }

    function createModule() {
        let root = null;
        let board = null;
        let toolbar = null;
        let alertHost = null;
        let rangeLabelEl = null;
        let totalsEl = null;
        let liveEl = null;
        let picker = null;
        let config = {};
        let text = {};
        let rows = [];
        // subTaskKey -> due date (yyyy-MM-dd or "") before the first move; moves exist only here.
        let moved = new Map();
        // subTaskKey -> message, for moves Jira refused.
        let errors = new Map();
        let periodType = "week";
        let rangeStart = null;
        let rangeEnd = null;
        let selectedDay = null;
        let picked = null;
        let chipValue = "all";
        let prefs = {};
        let loaded = false;
        let boundHandlers = [];
        let moveKey = null;
        let movePicker = null;

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

        function announce(message) {
            if (liveEl) {
                liveEl.textContent = "";
                liveEl.textContent = message;
            }
        }

        function reportUnsaved() {
            window.ToolboxJiraUnsaved?.set("board", moved.size);
        }

        // Reloading replaces the rows, so ask before throwing away moves that are not in Jira.
        function confirmReload() {
            return window.ToolboxJiraUnsaved ? window.ToolboxJiraUnsaved.confirmDiscard("board") : Promise.resolve(true);
        }

        // ------------------------------------------------------------------ rows

        const todayIso = () => dates.formatIsoDate(dates.today());
        const dueIso = (row) => (row?.dueDate ? String(row.dueDate).slice(0, 10) : "");
        const isDone = (row) => row?.statusTone === "done";
        const isOverdue = (row) => !isDone(row) && !!dueIso(row) && dueIso(row) < todayIso();
        const findRow = (key) => rows.find((row) => row.subTaskKey === key);

        function filterRow(row) {
            if (prefs.hideDone && isDone(row)) {
                return false;
            }

            if (chipValue === "open") {
                return !isDone(row);
            }

            return chipValue === "overdue" ? isOverdue(row) : true;
        }

        function rowsOn(iso) {
            return rows.filter((row) => dueIso(row) === iso && filterRow(row));
        }

        // Done work counts its logged time; open work its estimate.
        function summarise(dayRows) {
            const done = dayRows.filter(isDone).reduce((sum, row) => sum + (Number(row.loggedHours) || 0), 0);
            const open = dayRows.filter((row) => !isDone(row)).reduce((sum, row) => sum + (Number(row.estimateHours) || 0), 0);
            return { done, open, total: done + open };
        }

        function shortDate(date) {
            return jira.shortDate(date);
        }

        function daysBetween(start, end) {
            const result = [];
            for (let day = dates.startOfDay(start); day <= end; day = dates.addDays(day, 1)) {
                result.push(day);
            }

            return result;
        }

        const isWeekend = (date) => date.getDay() === 0 || date.getDay() === 6;

        // ------------------------------------------------------------------ pieces

        function loadHtml(dayRows, capacity) {
            const { done, open, total } = summarise(dayRows);
            const scale = Math.max(capacity, total) || 1;
            const over = capacity > 0 && total > capacity;
            // A day off has no capacity: show the hours alone.
            const label = capacity === 0
                ? hours(total)
                : over
                    ? template(text.over, hours(total - capacity))
                    : template(text.load, hours(total), hours(capacity));
            return `<div class="jira-load ${over ? "is-over" : ""}">
                <span class="jira-load-text">${escapeHtml(over ? `${hours(total)} · ${label}` : label)}</span>
                <span class="jira-load-bar" aria-hidden="true">
                    <span class="jira-load-fill is-done" style="width:${(done / scale) * 100}%"></span>
                    <span class="jira-load-fill is-open" style="width:${(open / scale) * 100}%"></span>
                </span>
            </div>`;
        }

        function cardHtml(row) {
            const done = isDone(row);
            const key = row.subTaskKey;
            const origin = moved.get(key);
            const overdue = isOverdue(row);
            const late = overdue ? Math.round((dates.parseIsoDate(todayIso()) - dates.parseIsoDate(dueIso(row))) / dates.DAY_MS) : 0;
            const notes = [];
            if (moved.has(key)) {
                const from = origin ? shortDate(dates.parseIsoDate(origin)) : "–";
                const to = dueIso(row) ? shortDate(dates.parseIsoDate(dueIso(row))) : "–";
                notes.push(`<span class="jira-card-moved">${escapeHtml(from)} → ${escapeHtml(to)}</span>`);
            }

            if (late > 0) {
                notes.push(`<span class="jira-late">${escapeHtml(template(text.late, late))}</span>`);
            }

            if (errors.has(key)) {
                notes.push(`<span class="jira-card-error">${escapeHtml(template(text.saveError, errors.get(key)))}</span>`);
            }

            return `<article class="jira-card ${done ? "is-done" : ""} ${moved.has(key) ? "is-moved" : ""} ${overdue ? "is-overdue" : ""} ${errors.has(key) ? "has-error" : ""} ${picked?.key === key ? "is-picked" : ""}"
                        data-card data-key="${escapeHtml(key)}" draggable="${done ? "false" : "true"}" tabindex="0"
                        title="${escapeHtml(row.project)}" aria-label="${escapeHtml(`${key}: ${row.subTaskSummary}`)}">
                <div class="jira-card-top">
                    ${jira.issueLink(key, config.browseBaseUrl, "jira-card-key")}
                    ${done ? "" : `<button type="button" class="jira-card-move" data-move="${escapeHtml(key)}" title="${escapeHtml(template(text.moveButton, key))}" aria-label="${escapeHtml(template(text.moveButton, key))}">${jira.iconSvg("calendar", 14)}</button>`}
                </div>
                <p class="jira-card-title">${escapeHtml(row.subTaskSummary)}</p>
                <div class="jira-card-meta">
                    ${jira.statusPill(row.status, row.statusTone)}
                    <span class="jira-card-hours">${escapeHtml(template(text.cardHours, hours(row.estimateHours), hours(row.loggedHours)))}</span>
                </div>
                ${notes.length ? `<div class="jira-card-notes">${notes.join("")}</div>` : ""}
            </article>`;
        }

        function chipHtml(row) {
            const key = row.subTaskKey;
            return `<div class="jira-cal-chip ${isDone(row) ? "is-done" : ""} ${moved.has(key) ? "is-moved" : ""} ${isOverdue(row) ? "is-overdue" : ""} ${picked?.key === key ? "is-picked" : ""}"
                        data-card data-key="${escapeHtml(key)}" draggable="${isDone(row) ? "false" : "true"}" tabindex="0"
                        title="${escapeHtml(`${key} — ${row.subTaskSummary}`)}" aria-label="${escapeHtml(`${key}: ${row.subTaskSummary}`)}">${escapeHtml(key)}</div>`;
        }

        // ------------------------------------------------------------------ views

        function visibleDays() {
            return daysBetween(rangeStart, rangeEnd).filter((day) => !(prefs.hideWeekend && isWeekend(day)));
        }

        function renderDayStrip(days) {
            const strip = board.querySelector("[data-board-daystrip]");
            if (!strip) {
                return;
            }

            strip.hidden = periodType !== "week";
            strip.innerHTML = periodType !== "week" ? "" : days.map((day) => {
                const iso = dates.formatIsoDate(day);
                const active = iso === selectedDay;
                return `<button type="button" class="jira-daystrip-day ${active ? "is-active" : ""} ${iso === todayIso() ? "is-today" : ""}" data-select-day="${iso}" aria-pressed="${active}">
                    <span>${escapeHtml(WEEKDAY_SHORT[day.getDay()])}</span><strong>${day.getDate()}</strong>
                </button>`;
            }).join("");
        }

        function renderWeek() {
            const days = visibleDays();
            const isos = days.map(dates.formatIsoDate);
            if (!isos.includes(selectedDay)) {
                selectedDay = isos.includes(todayIso()) ? todayIso() : isos[0];
            }

            renderDayStrip(days);
            // Saturday and Sunday get a narrow column unless they hold work.
            const columns = days.map((day) => (isWeekend(day) && rowsOn(dates.formatIsoDate(day)).length === 0
                ? "minmax(96px, .55fr)"
                : "minmax(150px, 1fr)")).join(" ");
            root.innerHTML = `<div class="jira-week" style="--week-columns: ${columns}">${days.map((day) => {
                const iso = dates.formatIsoDate(day);
                const dayRows = rowsOn(iso);
                const capacity = isWeekend(day) ? 0 : config.dayCapacityHours || 8;
                const today = iso === todayIso();
                return `<section class="jira-day ${today ? "is-today" : ""} ${isWeekend(day) ? "is-weekend" : ""} ${iso === selectedDay ? "is-selected" : ""}" data-drop-date="${iso}" aria-label="${escapeHtml(`${WEEKDAY_LONG.format(day)}, ${jira.displayDate(day)}`)}">
                    <header class="jira-day-head">
                        <span class="jira-day-name">${escapeHtml(today ? text.today : WEEKDAY_SHORT[day.getDay()])} <strong>${escapeHtml(shortDate(day))}</strong></span>
                        ${dayRows.length ? `<span class="jira-day-count">${dayRows.length}</span>` : ""}
                    </header>
                    ${capacity || dayRows.length ? loadHtml(dayRows, capacity) : ""}
                    <div class="jira-day-body">
                        ${dayRows.length ? dayRows.map(cardHtml).join("") : `<p class="jira-day-empty">${escapeHtml(text.noTasks)}</p>`}
                    </div>
                </section>`;
            }).join("")}</div>`;
        }

        function renderMonth() {
            renderDayStrip([]);
            const first = new Date(rangeStart.getFullYear(), rangeStart.getMonth(), 1);
            const last = new Date(first.getFullYear(), first.getMonth() + 1, 0);
            const gridStart = dates.isoWeekMonday(first);
            const gridEnd = dates.addDays(dates.isoWeekMonday(last), 6);
            const days = daysBetween(gridStart, gridEnd).filter((day) => !(prefs.hideWeekend && isWeekend(day)));
            const header = days.slice(0, prefs.hideWeekend ? 5 : 7)
                .map((day) => `<span>${escapeHtml(WEEKDAY_SHORT[day.getDay()])}</span>`).join("");

            const cells = days.map((day) => {
                const iso = dates.formatIsoDate(day);
                if (day.getMonth() !== first.getMonth()) {
                    return `<div class="jira-cal-cell is-other-month" aria-hidden="true"><span class="jira-cal-num">${day.getDate()}</span></div>`;
                }

                const dayRows = rowsOn(iso);
                const { total } = summarise(dayRows);
                const capacity = isWeekend(day) ? 0 : config.dayCapacityHours || 8;
                const over = capacity > 0 && total > capacity;
                const today = iso === todayIso();
                const extra = dayRows.length - CHIPS_ON_CELL;
                return `<div class="jira-cal-cell ${today ? "is-today" : ""} ${isWeekend(day) ? "is-weekend" : ""}" data-drop-date="${iso}">
                    <div class="jira-cal-head">
                        <span class="jira-cal-num">${day.getDate()}</span>
                        ${today ? `<span class="tbx-pill is-accent">${escapeHtml(text.today)}</span>` : ""}
                        ${dayRows.length ? `<span class="jira-cal-hours ${over ? "is-over" : ""}">${escapeHtml(hours(total))}</span>` : ""}
                    </div>
                    <div class="jira-cal-chips">${dayRows.slice(0, CHIPS_ON_CELL).map(chipHtml).join("")}</div>
                    ${dayRows.length ? `<button type="button" class="jira-cal-open" data-open-day="${iso}">
                        <span class="jira-cal-open-wide">${escapeHtml(extra > 0 ? template(text.moreCards, extra) : template(text.dayCount, dayRows.length))}</span>
                        <span class="jira-cal-open-narrow">${escapeHtml(template(text.dayCount, dayRows.length))}</span>
                    </button>` : ""}
                </div>`;
            }).join("");

            root.innerHTML = `<div class="jira-cal ${prefs.hideWeekend ? "is-no-weekend" : ""}"><div class="jira-cal-weekdays">${header}</div><div class="jira-cal-grid">${cells}</div></div>`;
        }

        function renderTotals() {
            if (!totalsEl || !rangeStart) {
                totalsEl && (totalsEl.innerHTML = "");
                return;
            }

            const all = rows;
            const logged = all.reduce((sum, row) => sum + (Number(row.loggedHours) || 0), 0);
            const open = all.filter((row) => !isDone(row)).reduce((sum, row) => sum + (Number(row.estimateHours) || 0), 0);
            const capacity = daysBetween(rangeStart, rangeEnd).filter((day) => !isWeekend(day)).length * (config.dayCapacityHours || 8);
            totalsEl.innerHTML = [
                [text.totalsLogged, hours(logged)],
                [text.totalsOpen, hours(open)],
                [text.totalsCapacity, hours(capacity)]
            ].map(([label, value]) => `<div><dt>${escapeHtml(label)}</dt><dd>${escapeHtml(value)}</dd></div>`).join("");
        }

        function render() {
            if (!root || !loaded) {
                return;
            }

            renderTotals();
            chips?.setCounts({
                all: rows.length,
                open: rows.filter((row) => !isDone(row)).length,
                overdue: rows.filter(isOverdue).length
            });

            if (rows.length === 0) {
                renderDayStrip([]);
                root.innerHTML = `<div class="tbx-empty" role="status"><span class="tbx-empty-icon">${jira.iconSvg("inbox", 20)}</span><p class="tbx-empty-text">${escapeHtml(text.empty)}</p></div>`;
                return;
            }

            if (periodType === "month") {
                renderMonth();
            } else {
                renderWeek();
            }
        }

        function focusCard(key) {
            const card = root?.querySelector(`[data-card][data-key="${CSS.escape(key)}"]`);
            card?.focus();
            card?.scrollIntoView({ block: "nearest", inline: "nearest" });
        }

        // ------------------------------------------------------------------ moving

        /** Moves a card to another day (yyyy-MM-dd, or "" for no due date). */
        function moveRow(key, iso, options) {
            const row = findRow(key);
            if (!row || isDone(row)) {
                return false;
            }

            const current = dueIso(row);
            if (current === iso) {
                return false;
            }

            const origin = moved.has(key) ? moved.get(key) : current;
            row.dueDate = iso ? `${iso}T00:00:00` : null;
            if (iso === origin) {
                moved.delete(key);
            } else {
                moved.set(key, origin);
            }

            errors.delete(key);
            reportUnsaved();
            render();
            if (!options?.quiet) {
                window.Toolbox?.notify(template(text.movedToast, key, iso ? jira.displayDate(iso) : "–"), "info");
            }

            return true;
        }

        function onKeyDown(event) {
            const card = event.target.closest?.("[data-card]");
            if (!card || event.target !== card) {
                return;
            }

            const key = card.dataset.key;
            const row = findRow(key);
            if (!row || isDone(row)) {
                return;
            }

            const label = (iso) => (iso ? jira.displayDate(iso) : "–");
            if (!picked) {
                if (event.key === " " || event.key === "Enter") {
                    event.preventDefault();
                    picked = { key, origin: dueIso(row) };
                    card.classList.add("is-picked");
                    announce(template(text.picked, key));
                }

                return;
            }

            if (picked.key !== key) {
                return;
            }

            const step = { ArrowLeft: -1, ArrowRight: 1, ArrowUp: periodType === "month" ? -7 : 0, ArrowDown: periodType === "month" ? 7 : 0 }[event.key];
            if (step) {
                event.preventDefault();
                let next = dates.addDays(dates.parseIsoDate(dueIso(row)) || dates.today(), step);
                while (prefs.hideWeekend && isWeekend(next)) {
                    next = dates.addDays(next, Math.sign(step));
                }

                const iso = dates.formatIsoDate(next);
                if (periodType === "week" && (next < rangeStart || next > rangeEnd)) {
                    return;
                }

                selectedDay = iso;
                moveRow(key, iso, { quiet: true });
                focusCard(key);
                announce(template(text.movedTo, key, label(iso)));
            } else if (event.key === "Enter" || event.key === " ") {
                event.preventDefault();
                picked = null;
                render();
                focusCard(key);
                announce(template(text.dropped, key, label(dueIso(row))));
            } else if (event.key === "Escape") {
                event.preventDefault();
                const origin = picked.origin;
                picked = null;
                moveRow(key, origin, { quiet: true });
                render();
                focusCard(key);
                announce(template(text.cancelled, key, label(origin)));
            }
        }

        function onDragStart(event) {
            const card = event.target.closest?.("[data-card]");
            if (!card || card.getAttribute("draggable") !== "true") {
                return;
            }

            card.classList.add("is-dragging");
            event.dataTransfer.setData("text/plain", card.dataset.key);
            event.dataTransfer.effectAllowed = "move";
        }

        function onDragEnd(event) {
            event.target.closest?.("[data-card]")?.classList.remove("is-dragging");
            root.querySelectorAll(".is-drag-over").forEach((el) => el.classList.remove("is-drag-over"));
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
            if (!zone) {
                return;
            }

            event.preventDefault();
            zone.classList.remove("is-drag-over");
            moveRow(event.dataTransfer.getData("text/plain"), zone.dataset.dropDate);
        }

        function openMove(key) {
            const row = findRow(key);
            const modalEl = document.querySelector("[data-board-move-modal]");
            if (!row || !modalEl || !window.bootstrap) {
                return;
            }

            moveKey = key;
            modalEl.querySelector("[data-board-move-item]").textContent =
                template(text.moveItem, `${key} — ${row.subTaskSummary}`, dueIso(row) ? jira.displayDate(dueIso(row)) : "–");
            const input = modalEl.querySelector("[data-board-move-date]");
            const $ = window.jQuery;
            if (!movePicker && $?.fn?.kendoDatePicker) {
                movePicker = $(input).kendoDatePicker({ format: window.Toolbox?.format?.datePattern || "dd/MM/yyyy" }).data("kendoDatePicker");
            } else if (!movePicker) {
                input.type = "date";
            }

            if (movePicker) {
                movePicker.value(dates.parseIsoDate(dueIso(row)));
            } else {
                input.value = dueIso(row);
            }

            window.bootstrap.Modal.getOrCreateInstance(modalEl).show();
        }

        function applyMove(event) {
            event.preventDefault();
            const modalEl = document.querySelector("[data-board-move-modal]");
            const input = modalEl.querySelector("[data-board-move-date]");
            const iso = movePicker ? dates.formatIsoDate(movePicker.value()) : input.value;
            window.bootstrap.Modal.getOrCreateInstance(modalEl).hide();
            if (moveKey) {
                const next = dates.parseIsoDate(iso);
                if (next && periodType === "week") {
                    selectedDay = iso;
                }

                moveRow(moveKey, iso);
            }
        }

        function openDay(iso) {
            const panel = document.querySelector("[data-board-day-panel]");
            if (!panel || !window.bootstrap) {
                return;
            }

            const day = dates.parseIsoDate(iso);
            panel.querySelector("[data-board-day-title]").textContent = `${WEEKDAY_LONG.format(day)}, ${jira.displayDate(day)}`;
            const dayRows = rowsOn(iso);
            panel.querySelector("[data-board-day-body]").innerHTML = `${loadHtml(dayRows, isWeekend(day) ? 0 : config.dayCapacityHours || 8)}
                <div class="jira-day-body">${dayRows.map(cardHtml).join("") || `<p class="jira-day-empty">${escapeHtml(text.noTasks)}</p>`}</div>`;
            window.bootstrap.Offcanvas.getOrCreateInstance(panel).show();
        }

        function onClick(event) {
            const move = event.target.closest?.("[data-move]");
            if (move) {
                event.preventDefault();
                openMove(move.dataset.move);
                return;
            }

            const open = event.target.closest?.("[data-open-day]");
            if (open) {
                openDay(open.dataset.openDay);
                return;
            }

            const select = event.target.closest?.("[data-select-day]");
            if (select) {
                selectedDay = select.dataset.selectDay;
                render();
            }
        }

        // ------------------------------------------------------------------ loading

        function setRange(state) {
            periodType = state.periodType;
            if (periodType === "month") {
                rangeStart = new Date(state.date.getFullYear(), state.date.getMonth(), 1);
                rangeEnd = new Date(rangeStart.getFullYear(), rangeStart.getMonth() + 1, 0);
            } else {
                rangeStart = dates.isoWeekMonday(state.date);
                rangeEnd = dates.addDays(rangeStart, 6);
            }
        }

        function showLoading() {
            root.setAttribute("aria-busy", "true");
            if (!loaded) {
                root.innerHTML = `<div class="jira-board-loading" role="status">
                    ${Array.from({ length: 5 }, () => '<span class="tbx-skeleton"></span>').join("")}
                    <span class="visually-hidden">${escapeHtml(text.loading)}</span></div>`;
            }
        }

        function showError(message) {
            root.setAttribute("aria-busy", "false");
            loaded = false;
            renderDayStrip([]);
            root.innerHTML = `<div class="tbx-error-state" role="alert">${jira.iconSvg("circle-alert")}
                <div class="jira-search-error-body"><p>${escapeHtml(message)}</p>
                <button type="button" class="btn btn-sm btn-outline-secondary" data-board-retry>${escapeHtml(text.retry)}</button></div></div>`;
            root.querySelector("[data-board-retry]")?.addEventListener("click", () => fetchAndRender());
        }

        async function fetchAndRender() {
            const state = picker.state();
            setRange(state);
            const params = new URLSearchParams({ periodType: state.periodType, periodValue: state.periodValue });
            showLoading();

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
                    showError(payload?.message || text.error);
                    return;
                }

                const data = payload.data || {};
                if (data.errorMessage) {
                    showError(data.errorMessage);
                    return;
                }

                rows = Array.isArray(data.rows) ? data.rows.slice() : [];
                moved = new Map();
                errors = new Map();
                picked = null;
                reportUnsaved();
                const filter = data.filter || {};
                if (rangeLabelEl) {
                    rangeLabelEl.textContent = filter.rangeLabel || "";
                }

                if (filter.periodType) {
                    picker.apply(filter.periodType, filter.periodType === "month" ? filter.monthValue : filter.weekValue);
                }

                loaded = true;
                root.setAttribute("aria-busy", "false");
                render();
            } catch {
                showError(text.error);
            }
        }

        // ------------------------------------------------------------------ chips (built once)

        let chips = null;

        return {
            init() {
                board = document.querySelector("[data-jira-board]");
                root = document.querySelector("[data-board-root]");
                toolbar = document.querySelector("[data-jira-board-toolbar]");
                alertHost = document.querySelector("[data-jira-board-alert]");
                rangeLabelEl = document.querySelector("[data-jira-board-range-label]");
                totalsEl = document.querySelector("[data-board-totals]");
                liveEl = document.querySelector("[data-board-live]");
                if (!board || !root || !toolbar) {
                    return;
                }

                config = jira.parseJsonScript("jira-worklist-config", {});
                text = jira.parseJsonScript("jira-task-board-config", {});
                prefs = readPrefs();
                rows = [];
                loaded = false;
                if (alertHost) {
                    alertHost.innerHTML = "";
                }

                // The chip buttons stay in the page between visits, so their listeners are added once.
                chips = chips || jira.chipBar(board.querySelector("[data-board-chips]"), (value) => {
                    chipValue = value;
                    render();
                });
                chipValue = chips.value();

                const hideDone = board.querySelector("[data-board-hide-done]");
                const hideWeekend = board.querySelector("[data-board-hide-weekend]");
                hideDone.checked = !!prefs.hideDone;
                hideWeekend.checked = !!prefs.hideWeekend;
                on(hideDone, "change", () => { prefs.hideDone = hideDone.checked; writePrefs(prefs); render(); });
                on(hideWeekend, "change", () => { prefs.hideWeekend = hideWeekend.checked; writePrefs(prefs); render(); });

                on(root, "dragstart", onDragStart);
                on(root, "dragend", onDragEnd);
                on(root, "dragover", onDragOver);
                on(root, "dragleave", onDragLeave);
                on(root, "drop", onDrop);
                on(root, "keydown", onKeyDown);
                on(root, "click", onClick);
                on(board.querySelector("[data-board-daystrip]"), "click", onClick);
                on(document.querySelector("[data-board-day-panel]"), "click", onClick);
                on(document.querySelector("[data-board-move-form]"), "submit", applyMove);

                window.ToolboxJiraUnsaved?.register("board", {
                    changes: () => Array.from(moved.entries()).map(([key, origin]) => ({
                        issueKey: key,
                        summary: findRow(key)?.subTaskSummary || "",
                        dueDate: dueIso(findRow(key)),
                        previousDueDate: origin
                    })),
                    applyResults: (results) => {
                        results.forEach((result) => {
                            if (result.success) {
                                moved.delete(result.issueKey);
                                errors.delete(result.issueKey);
                            } else {
                                errors.set(result.issueKey, result.message || "");
                            }
                        });
                        reportUnsaved();
                        render();
                    },
                    discard: () => {
                        moved.forEach((origin, key) => {
                            const row = findRow(key);
                            if (row) {
                                row.dueDate = origin ? `${origin}T00:00:00` : null;
                            }
                        });
                        moved = new Map();
                        errors = new Map();
                        picked = null;
                        reportUnsaved();
                        render();
                    }
                });

                // Every period change reloads; moves that are not in Jira yet are confirmed first.
                picker = jira.periodPicker(toolbar.querySelector("[data-jira-period]"), {
                    beforeChange: confirmReload,
                    onChange: (state) => fetchAndRender().then(() => {
                        if (state.reason === "today") {
                            selectedDay = todayIso();
                            render();
                            root.querySelector(".jira-day.is-today, .jira-cal-cell.is-today")
                                ?.scrollIntoView({ behavior: "smooth", inline: "center", block: "nearest" });
                        }
                    })
                });

                fetchAndRender();
            },
            destroy() {
                offAll();
                picker?.destroy();
                picker = null;
                window.ToolboxJiraUnsaved?.unregister("board");
                if (root) {
                    root.innerHTML = "";
                }

                if (totalsEl) {
                    totalsEl.innerHTML = "";
                }

                if (rangeLabelEl) {
                    rangeLabelEl.textContent = "";
                }

                rows = [];
                moved = new Map();
                errors = new Map();
                picked = null;
                loaded = false;
                reportUnsaved();
                root = null;
                toolbar = null;
            }
        };
    }

    window.ToolboxJiraTaskBoard = createModule();
})(window, document);
