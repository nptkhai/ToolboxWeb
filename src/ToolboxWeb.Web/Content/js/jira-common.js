// Shared pieces of the Jira screens (Dashboard, Search, Board, Auto-schedule):
// ISO period dates, status pills, Tabulator defaults and formatters, the period picker,
// the filter chip bar and the JQL block. docs/UI_UX_REVIEW.md 2.4 and stage 4.3.
// Needs format.js (window.Toolbox.format). Kendo and Tabulator are optional.
(function (window, document) {
    "use strict";

    const DAY_MS = 24 * 60 * 60 * 1000;

    function fmt() {
        return window.Toolbox?.format;
    }

    function escapeHtml(value) {
        return String(value ?? "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#39;");
    }

    /** Replaces {0}, {1}… in a localized template. */
    function template(text, ...args) {
        return args.reduce((result, value, index) => result.split(`{${index}}`).join(String(value)), text || "");
    }

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

    // ------------------------------------------------------------ dates
    // Calendar dates in local time with no time zone shift: due dates and period values.
    const dates = (function () {
        function pad2(value) {
            return String(value).padStart(2, "0");
        }

        function isValid(date) {
            return date instanceof Date && !Number.isNaN(date.getTime());
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

        /** "2026-10-21" or "2026-10-21T00:00:00" → local Date. */
        function parseIsoDate(value) {
            const normalized = String(value || "").split("T")[0];
            if (!/^\d{4}-\d{2}-\d{2}$/.test(normalized)) {
                return null;
            }

            const [year, month, day] = normalized.split("-").map(Number);
            return new Date(year, month - 1, day);
        }

        function formatIsoDate(date) {
            return isValid(date) ? `${date.getFullYear()}-${pad2(date.getMonth() + 1)}-${pad2(date.getDate())}` : "";
        }

        function parseIsoMonth(value) {
            if (!/^\d{4}-\d{2}$/.test(value || "")) {
                return null;
            }

            const [year, month] = value.split("-").map(Number);
            return new Date(year, month - 1, 1);
        }

        function formatIsoMonth(date) {
            return isValid(date) ? `${date.getFullYear()}-${pad2(date.getMonth() + 1)}` : "";
        }

        function isoWeekMonday(date) {
            const result = startOfDay(date);
            result.setDate(result.getDate() + 1 - (result.getDay() || 7));
            return result;
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
            return addDays(isoWeekMonday(januaryFourth), (week - 1) * 7);
        }

        function formatIsoWeek(date) {
            if (!isValid(date)) {
                return "";
            }

            const thursday = addDays(isoWeekMonday(date), 3);
            const firstThursday = addDays(isoWeekMonday(new Date(thursday.getFullYear(), 0, 4)), 3);
            const week = 1 + Math.round((thursday - firstThursday) / (7 * DAY_MS));
            return `${thursday.getFullYear()}-W${pad2(week)}`;
        }

        function parseYear(value) {
            return /^\d{4}$/.test(String(value || "")) ? new Date(Number(value), 0, 1) : null;
        }

        function formatYear(date) {
            return isValid(date) ? String(date.getFullYear()) : "";
        }

        function sameDay(a, b) {
            return formatIsoDate(a) === formatIsoDate(b);
        }

        /** Period value ("2026-10-21", "2026-W43", "2026-10", "2026") → the date it starts on. */
        function parsePeriod(periodType, value) {
            switch (periodType) {
                case "day": return parseIsoDate(value);
                case "month": return parseIsoMonth(value);
                case "year": return parseYear(value);
                default: return parseIsoWeek(value);
            }
        }

        function formatPeriod(periodType, date) {
            switch (periodType) {
                case "day": return formatIsoDate(date);
                case "month": return formatIsoMonth(date);
                case "year": return formatYear(date);
                default: return formatIsoWeek(date);
            }
        }

        function shiftPeriod(periodType, date, steps) {
            const result = new Date(date);
            switch (periodType) {
                case "day":
                    result.setDate(result.getDate() + steps);
                    break;
                case "month":
                    result.setDate(1);
                    result.setMonth(result.getMonth() + steps);
                    break;
                case "year":
                    result.setFullYear(result.getFullYear() + steps);
                    break;
                default:
                    result.setDate(result.getDate() + steps * 7);
            }

            return result;
        }

        /** Today in the app time zone (App:TimeZone), as a local Date at midnight. */
        function today() {
            try {
                const text = new Intl.DateTimeFormat("en-CA", {
                    timeZone: window.Toolbox?.format?.timeZone || undefined,
                    year: "numeric",
                    month: "2-digit",
                    day: "2-digit"
                }).format(new Date());
                return parseIsoDate(text) || startOfDay(new Date());
            } catch {
                return startOfDay(new Date());
            }
        }

        return {
            DAY_MS, pad2, isValid, startOfDay, addDays, sameDay, today,
            parseIsoDate, formatIsoDate, parseIsoMonth, formatIsoMonth,
            isoWeekMonday, parseIsoWeek, formatIsoWeek, parseYear, formatYear,
            parsePeriod, formatPeriod, shiftPeriod
        };
    })();

    /** A calendar date in the UI culture (19/10/2026, 10/19/2026). */
    function displayDate(value) {
        const date = value instanceof Date ? value : dates.parseIsoDate(value);
        if (!dates.isValid(date)) {
            return "-";
        }

        return fmt() ? fmt().date(date) : dates.formatIsoDate(date);
    }

    /** Day and month only, in the UI culture's order: 19/10 (vi), 10/19 (en). */
    function shortDate(date) {
        const locale = fmt()?.locale || document.documentElement.lang || "vi-VN";
        return locale.toLowerCase().startsWith("vi")
            ? `${dates.pad2(date.getDate())}/${dates.pad2(date.getMonth() + 1)}`
            : new Intl.DateTimeFormat(locale, { day: "2-digit", month: "2-digit" }).format(date);
    }

    function displayHours(value) {
        return fmt() ? fmt().hours(value) : `${Number(value) || 0}h`;
    }

    // ------------------------------------------------------------ status
    // One mapping for every Jira screen. Tones come from Services/JiraStatus.cs; the server
    // side of this mapping is JiraStatus.PillClass.
    const STATUS_PILL = { done: "is-success", doing: "is-accent", todo: "is-neutral" };

    function statusPill(status, tone) {
        const pillClass = STATUS_PILL[tone] || STATUS_PILL.todo;
        return `<span class="tbx-pill ${pillClass}">${escapeHtml(status || "-")}</span>`;
    }

    function browseUrl(baseUrl, issueKey) {
        const base = String(baseUrl || "").replace(/\/+$/, "");
        return base && issueKey ? `${base}/browse/${encodeURIComponent(issueKey)}` : "";
    }

    /** An issue key, linked to Jira when the base URL is known. extraClass lets cards add theirs. */
    function issueLink(issueKey, baseUrl, extraClass) {
        const text = escapeHtml(issueKey || "-");
        const className = `jira-key${extraClass ? ` ${extraClass}` : ""}`;
        const href = browseUrl(baseUrl, issueKey);

        return href
            ? `<a class="${className}" href="${escapeHtml(href)}" target="_blank" rel="noopener noreferrer" draggable="false">${text}</a>`
            : `<span class="${className}">${text}</span>`;
    }

    // ------------------------------------------------------------ grid
    // Tabulator defaults that follow the grid convention in AGENTS.md: headers centered,
    // text left, numbers right, everything else centered.
    // An element, not a string, so the text can change later (setEmptyText) without a rebuild.
    function emptyGridElement(text) {
        const el = document.createElement("div");
        el.className = "jira-grid-empty";
        el.innerHTML = `<svg class="tbx-icon" width="20" height="20" aria-hidden="true" focusable="false"><use href="${spriteHref("inbox")}"></use></svg><span data-empty-text></span>`;
        el.querySelector("[data-empty-text]").textContent = text || "";
        return el;
    }

    /** An icon from the app sprite, for markup built in the browser. */
    function iconSvg(name, size) {
        const px = size || 16;
        return `<svg class="tbx-icon" width="${px}" height="${px}" aria-hidden="true" focusable="false"><use href="${spriteHref(name)}"></use></svg>`;
    }

    function spriteHref(name) {
        const sprite = document.querySelector("svg.tbx-icon use")?.getAttribute("href") || "";
        const base = sprite.split("#")[0] || "/vendor/lucide/lucide-sprite.svg";
        return `${base}#${name}`;
    }

    // Shows the full text only when the cell cut it off.
    function truncatedTooltip(_event, cell) {
        const el = cell.getElement();
        const target = el.firstElementChild && el.firstElementChild.scrollWidth > el.firstElementChild.clientWidth
            ? el.firstElementChild
            : el;
        return target.scrollWidth > target.clientWidth ? el.textContent.trim() : "";
    }

    const grid = {
        options(overrides) {
            const settings = overrides || {};
            return {
                layout: "fitColumns",
                reactiveData: false,
                columnHeaderVertAlign: "middle",
                ...settings,
                placeholder: emptyGridElement(settings.placeholder),
                columnDefaults: {
                    headerHozAlign: "center",
                    hozAlign: "center",
                    vertAlign: "middle",
                    resizable: false,
                    tooltip: truncatedTooltip,
                    ...(settings.columnDefaults || {})
                }
            };
        },
        formatters: {
            text(cell) {
                return escapeHtml(cell.getValue() || "-");
            },
            issueKey(baseUrl) {
                return (cell) => issueLink(cell.getValue(), baseUrl);
            },
            /** Status pill; the tone is read from the row's statusTone. */
            status(cell) {
                return statusPill(cell.getValue(), cell.getRow().getData().statusTone);
            },
            /** Due date in the UI culture, marked when the row is overdue. */
            dueDate(cell) {
                const row = cell.getRow().getData();
                const text = escapeHtml(displayDate(cell.getValue()));
                return row.isOverdue ? `<span class="jira-overdue">${text}</span>` : text;
            },
            /** Hours in the UI culture (6,5h); "-" when there are none. */
            hours(cell) {
                const value = Number(cell.getValue());
                return value ? escapeHtml(displayHours(value)) : "-";
            },
            /** Due date plus "Late by N d" (template) while the row is overdue (row.daysOverdue). */
            dueWithLate(lateTemplate) {
                return (cell) => {
                    const row = cell.getRow().getData();
                    const date = escapeHtml(displayDate(cell.getValue()));
                    return row.daysOverdue
                        ? `<span class="jira-overdue">${date}</span><span class="jira-late">${escapeHtml(template(lateTemplate, row.daysOverdue))}</span>`
                        : date;
                };
            },
            /** Estimate in hours; open work without one says so, since it needs fixing in Jira. */
            estimate(missingText) {
                return (cell) => {
                    if (Number(cell.getValue()) > 0) {
                        return escapeHtml(displayHours(cell.getValue()));
                    }

                    return cell.getRow().getData().statusTone === "done"
                        ? "-"
                        : `<span class="jira-unestimated">${escapeHtml(missingText)}</span>`;
                };
            }
        },
        /**
         * Group row for a table grouped by parent issue: key link, parent summary, and a meta line
         * from metaTemplate ("{0} subtasks · logged {1}"). summaryField names the parent summary.
         */
        parentGroupHeader({ browseBaseUrl, noParentText, metaTemplate, summaryField, hoursField }) {
            return (value, count, rows) => {
                const first = rows[0] || {};
                const logged = rows.reduce((sum, row) => sum + (Number(row[hoursField || "loggedHours"]) || 0), 0);
                const meta = `<span class="jira-group-meta">${escapeHtml(template(metaTemplate, count, displayHours(logged)))}</span>`;
                if (!value) {
                    return `<span class="jira-group-title">${escapeHtml(noParentText)}</span>${meta}`;
                }

                return issueLink(value, browseBaseUrl, "jira-group-key")
                    + `<span class="jira-group-title">${escapeHtml(first[summaryField] || "")}</span>${meta}`;
            };
        },
        /** True on a phone, where rows open (+) to show the columns that did not fit. */
        isNarrow() {
            return !!window.matchMedia?.("(max-width: 600px)").matches;
        },
        /** The "+" column of responsiveLayout "collapse". */
        collapseColumn() {
            return { formatter: "responsiveCollapse", cssClass: "tbx-collapse-cell", width: 36, minWidth: 36, hozAlign: "center", headerSort: false, resizable: false, responsive: 0, tooltip: false };
        },
        /** Sorts ISO date strings with empty values last. */
        dueDateSorter(a, b, _aRow, _bRow, _column, dir) {
            const missing = dir === "desc" ? -Infinity : Infinity;
            const left = dates.parseIsoDate(a)?.getTime() ?? missing;
            const right = dates.parseIsoDate(b)?.getTime() ?? missing;
            return left - right;
        },
        /** Changes the empty-state sentence of a table built with grid.options. */
        setEmptyText(table, text) {
            const target = table?.options?.placeholder?.querySelector?.("[data-empty-text]");
            if (target) {
                target.textContent = text || "";
            }
        }
    };

    // ------------------------------------------------------------ period picker
    // Markup: Views/Jira/_JiraPeriodPicker.cshtml. Any change (period, value, ‹ ›, Today)
    // reloads by itself; there is no Search button, no clear button and no « ».
    function periodPicker(root, options) {
        const settings = options || {};
        const $ = window.jQuery;
        const kendo = window.kendo;
        const useKendo = !!(kendo && $ && $.fn && typeof $.fn.kendoDatePicker === "function" && typeof $.fn.kendoComboBox === "function");
        const tabs = Array.from(root.querySelectorAll("[data-period]"));
        const wraps = Array.from(root.querySelectorAll("[data-period-input]"));
        const pristine = wraps.map((wrap) => wrap.innerHTML);
        const listeners = [];
        const widgets = {};
        const inputs = {};
        const weekOptionText = root.dataset.weekOption || "{0}/{1}";
        const weekShortText = root.dataset.weekShort || "{0} · {1}";
        let weekCenterYear = null;
        let syncing = false;
        let timer = null;

        wraps.forEach((wrap) => {
            inputs[wrap.dataset.periodInput] = wrap.querySelector("input");
        });

        const initialType = root.dataset.periodType || tabs[0]?.dataset.period || "week";
        let current = {
            periodType: initialType,
            date: dates.parsePeriod(initialType, inputs[initialType]?.value) || dates.startOfDay(new Date())
        };

        function on(el, event, handler) {
            if (el) {
                el.addEventListener(event, handler);
                listeners.push([el, event, handler]);
            }
        }

        function weekOptions(centerYear) {
            const result = [];
            for (let year = centerYear - 3; year <= centerYear + 3; year += 1) {
                for (let week = 1; week <= 53; week += 1) {
                    const value = `${year}-W${dates.pad2(week)}`;
                    const start = dates.parseIsoWeek(value);
                    if (!start || dates.formatIsoWeek(start) !== value) {
                        continue;
                    }

                    const end = dates.addDays(start, 6);
                    result.push({
                        value,
                        // The box shows the dates too ("Tuần 43 · 19/10 – 25/10"), so the page
                        // needs no separate "from … to …" line.
                        text: template(weekShortText, week, `${shortDate(start)} – ${shortDate(end)}`),
                        title: template(weekOptionText, week, year),
                        range: `${displayDate(start)} – ${displayDate(end)}`
                    });
                }
            }

            return result;
        }

        function ensureWeekOptions(date) {
            if (widgets.week && Math.abs(date.getFullYear() - weekCenterYear) > 2) {
                weekCenterYear = date.getFullYear();
                widgets.week.dataSource.data(weekOptions(weekCenterYear));
            }
        }

        function readInput(periodType) {
            const widget = widgets[periodType];
            if (!widget) {
                return dates.parsePeriod(periodType, inputs[periodType]?.value);
            }

            return periodType === "week" ? dates.parseIsoWeek(widget.value()) : widget.value();
        }

        // Puts a state on screen without reporting a change.
        function render(state) {
            syncing = true;
            try {
                tabs.forEach((tab) => {
                    const active = tab.dataset.period === state.periodType;
                    tab.classList.toggle("is-active", active);
                    tab.setAttribute("aria-pressed", active ? "true" : "false");
                });
                wraps.forEach((wrap) => {
                    wrap.hidden = wrap.dataset.periodInput !== state.periodType;
                });

                Object.keys(inputs).forEach((periodType) => {
                    const widget = widgets[periodType];
                    if (!widget) {
                        if (inputs[periodType]) {
                            inputs[periodType].value = dates.formatPeriod(periodType, state.date);
                        }

                        return;
                    }

                    if (periodType === "week") {
                        ensureWeekOptions(state.date);
                        widget.value(dates.formatIsoWeek(state.date));
                    } else if (periodType === "month") {
                        widget.value(new Date(state.date.getFullYear(), state.date.getMonth(), 1));
                    } else if (periodType === "year") {
                        widget.value(new Date(state.date.getFullYear(), 0, 1));
                    } else {
                        widget.value(state.date);
                    }
                });
            } finally {
                syncing = false;
            }
        }

        function snapshot(reason) {
            return {
                periodType: current.periodType,
                periodValue: dates.formatPeriod(current.periodType, current.date),
                date: new Date(current.date),
                reason: reason || "change"
            };
        }

        async function commit(periodType, date, reason) {
            if (!dates.isValid(date)) {
                render(current);
                return;
            }

            if (settings.beforeChange && !(await settings.beforeChange())) {
                render(current);
                return;
            }

            current = { periodType, date: dates.startOfDay(date) };
            render(current);

            // Stepping several periods in a row loads once, after the last click.
            clearTimeout(timer);
            timer = setTimeout(() => settings.onChange?.(snapshot(reason)), reason === "step" ? 300 : 0);
        }

        function onInputChange(periodType) {
            if (!syncing) {
                commit(periodType, readInput(periodType), "change");
            }
        }

        function initWidgets() {
            const popup = { appendTo: $(document.body), collision: "fit flip" };
            const locale = fmt()?.locale;
            if (locale && kendo.cultures?.[locale]) {
                kendo.culture(locale);
            }

            if (inputs.day) {
                widgets.day = $(inputs.day).kendoDatePicker({
                    format: fmt()?.datePattern || "dd/MM/yyyy",
                    popup
                }).data("kendoDatePicker");
            }

            if (inputs.week) {
                weekCenterYear = current.date.getFullYear();
                widgets.week = $(inputs.week).kendoComboBox({
                    dataSource: weekOptions(weekCenterYear),
                    dataTextField: "text",
                    dataValueField: "value",
                    filter: "contains",
                    suggest: true,
                    clearButton: false,
                    placeholder: root.dataset.weekPlaceholder || "",
                    template: "<div class='jira-period-week-option'><strong>#: title #</strong><span>#: range #</span></div>",
                    popup
                }).data("kendoComboBox");
            }

            if (inputs.month) {
                widgets.month = $(inputs.month).kendoDatePicker({
                    format: "MM/yyyy",
                    start: "year",
                    depth: "year",
                    popup
                }).data("kendoDatePicker");
            }

            if (inputs.year) {
                widgets.year = $(inputs.year).kendoDatePicker({
                    format: "yyyy",
                    start: "decade",
                    depth: "decade",
                    popup
                }).data("kendoDatePicker");
            }

            Object.keys(widgets).forEach((periodType) => {
                widgets[periodType].bind("change", () => onInputChange(periodType));
            });
        }

        function initNative() {
            const types = { day: "date", week: "week", month: "month", year: "number" };
            Object.keys(inputs).forEach((periodType) => {
                const input = inputs[periodType];
                if (!input) {
                    return;
                }

                input.type = types[periodType];
                if (periodType === "year") {
                    input.min = "2000";
                    input.max = "2100";
                    input.inputMode = "numeric";
                }

                on(input, "change", () => onInputChange(periodType));
            });
        }

        if (useKendo) {
            initWidgets();
        } else {
            initNative();
        }

        tabs.forEach((tab) => on(tab, "click", () => {
            if (tab.dataset.period !== current.periodType) {
                commit(tab.dataset.period, current.date, "period");
            }
        }));
        root.querySelectorAll("[data-period-step]").forEach((button) => on(button, "click", () => {
            const steps = Number(button.dataset.periodStep) || 0;
            commit(current.periodType, dates.shiftPeriod(current.periodType, current.date, steps), "step");
        }));
        on(root.querySelector("[data-period-today]"), "click", () => commit(current.periodType, new Date(), "today"));

        render(current);

        return {
            state: () => snapshot(),
            /** Shows the period the server answered with, without reloading. */
            apply(periodType, periodValue) {
                const date = dates.parsePeriod(periodType, periodValue);
                if (!date) {
                    return;
                }

                // Keep the picked day when it lies in that period, so Month → Week after
                // "Today" lands on this week rather than the first week of the month.
                const keep = dates.formatPeriod(periodType, current.date) === periodValue;
                current = { periodType, date: keep ? current.date : date };
                render(current);
            },
            destroy() {
                clearTimeout(timer);
                listeners.forEach(([el, event, handler]) => el.removeEventListener(event, handler));
                Object.values(widgets).forEach((widget) => widget.destroy());
                // Kendo's destroy() leaves its wrapper behind; restore the original inputs.
                wraps.forEach((wrap, index) => { wrap.innerHTML = pristine[index]; });
            }
        };
    }

    // ------------------------------------------------------------ chip bar
    // The four filters every Jira list offers: All / Not done / Overdue / Done.
    const statusFilters = {
        all: null,
        open: (row) => row.statusTone !== "done",
        overdue: (row) => !!row.isOverdue,
        done: (row) => row.statusTone === "done"
    };

    function statusCounts(rows) {
        return {
            all: rows.length,
            open: rows.filter(statusFilters.open).length,
            overdue: rows.filter(statusFilters.overdue).length,
            done: rows.filter(statusFilters.done).length
        };
    }

    // Single-choice filter chips: <button class="tbx-chip" data-chip="open" aria-pressed="false">
    // with an optional <span data-chip-count>.
    function chipBar(root, onChange) {
        const chips = Array.from(root.querySelectorAll("[data-chip]"));
        let value = chips.find((chip) => chip.getAttribute("aria-pressed") === "true")?.dataset.chip || chips[0]?.dataset.chip || "";

        function select(next) {
            value = next;
            chips.forEach((chip) => chip.setAttribute("aria-pressed", chip.dataset.chip === next ? "true" : "false"));
        }

        chips.forEach((chip) => chip.addEventListener("click", () => {
            if (chip.dataset.chip !== value) {
                select(chip.dataset.chip);
                onChange?.(value);
            }
        }));

        select(value);

        return {
            value: () => value,
            select,
            setCounts(counts) {
                chips.forEach((chip) => {
                    const countEl = chip.querySelector("[data-chip-count]");
                    if (countEl) {
                        countEl.textContent = fmt() ? fmt().number(counts[chip.dataset.chip] || 0, 0) : String(counts[chip.dataset.chip] || 0);
                    }
                });
            }
        };
    }

    // ------------------------------------------------------------ JQL block
    // Markup: Views/Jira/_JiraJqlBlock.cshtml.
    const jql = {
        /** One condition per line: breaks before AND, OR and ORDER BY outside quotes. */
        format(query) {
            const parts = [];
            let current = "";
            let quote = null;
            const text = String(query || "").replace(/\s+/g, " ").trim();
            for (let i = 0; i < text.length; i += 1) {
                const ch = text[i];
                if (quote) {
                    current += ch;
                    if (ch === quote && text[i - 1] !== "\\") {
                        quote = null;
                    }
                    continue;
                }

                if (ch === '"' || ch === "'") {
                    quote = ch;
                    current += ch;
                    continue;
                }

                const rest = text.slice(i);
                const keyword = /^ (AND|OR|ORDER BY) /i.exec(rest);
                if (keyword) {
                    parts.push(current.trim());
                    current = keyword[1].toUpperCase() + " ";
                    i += keyword[0].length - 1;
                    continue;
                }

                current += ch;
            }

            parts.push(current.trim());
            return parts.filter(Boolean).join("\n");
        },
        searchUrl(baseUrl, query) {
            const base = String(baseUrl || "").replace(/\/+$/, "");
            return base && query ? `${base}/issues/?jql=${encodeURIComponent(query)}` : "";
        },
        update(block, query) {
            if (!block) {
                return;
            }

            block.hidden = !query;
            const code = block.querySelector("[data-jira-jql-code]");
            if (code) {
                code.textContent = query || "";
            }

            const link = block.querySelector("[data-jira-jql-open]");
            if (link) {
                const href = jql.searchUrl(block.dataset.browseBase, query);
                link.hidden = !href;
                link.setAttribute("href", href || "#");
            }
        }
    };

    window.ToolboxJira = {
        escapeHtml,
        template,
        parseJsonScript,
        dates,
        displayDate,
        shortDate,
        displayHours,
        statusPill,
        browseUrl,
        issueLink,
        iconSvg,
        grid,
        periodPicker,
        chipBar,
        statusFilters,
        statusCounts,
        jql
    };
})(window, document);
