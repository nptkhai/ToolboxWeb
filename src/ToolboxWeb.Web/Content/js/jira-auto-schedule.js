(function (window, document) {
    "use strict";

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

    function fmtFull(date) {
        return `${pad2(date.getDate())}/${pad2(date.getMonth() + 1)}/${date.getFullYear()}`;
    }

    function parseIsoDate(value) {
        if (!value) {
            return null;
        }

        const normalized = String(value).split("T")[0];
        if (!/^\d{4}-\d{2}-\d{2}$/.test(normalized)) {
            return null;
        }

        const [year, month, day] = normalized.split("-").map(Number);
        return new Date(year, month - 1, day);
    }

    function escapeHtml(value) {
        return String(value ?? "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#39;");
    }

    function format(template, ...args) {
        return args.reduce((text, value, index) => text.replace(`{${index}}`, value), template || "");
    }

    function notify(message, type) {
        if (window.Toolbox && typeof window.Toolbox.notify === "function") {
            window.Toolbox.notify(message, type || "info");
        }
    }

    function statusFormatter(cell) {
        const row = cell.getRow().getData();
        return `<span class="badge jira-dashboard-status jira-dashboard-status--${escapeHtml(row.statusTone)}">${escapeHtml(row.status)}</span>`;
    }

    function dueDateFormatter(cell) {
        const parsed = parseIsoDate(cell.getValue());
        return parsed ? fmtFull(parsed) : "-";
    }

    function dueDateEditor(cell, onRendered, success, cancel) {
        const input = document.createElement("input");
        input.type = "date";
        input.className = "jira-due-editor";
        const parsed = parseIsoDate(cell.getValue());
        input.value = parsed ? toDateInputValue(parsed) : "";

        onRendered(() => {
            input.focus();
        });

        function commit() {
            success(input.value ? `${input.value}T00:00:00` : null);
        }

        input.addEventListener("change", commit);
        input.addEventListener("blur", commit);
        input.addEventListener("keydown", (event) => {
            if (event.key === "Escape") {
                cancel();
            } else if (event.key === "Enter") {
                commit();
            }
        });

        return input;
    }

    function estimateFormatter(cell) {
        return `${Number(cell.getValue() || 0)}h`;
    }

    function createModule() {
        let config = {};
        let autoConfig = {};
        let searchTable = null;
        let resultTable = null;
        let boundHandlers = [];
        let searchRows = [];
        let proposals = [];
        let editedKeys = new Set();
        let modalEl = null;
        let modalInstance = null;

        let criteriaOrder = ["dueDate", "status", "createDate"];
        let criteriaEnabled = { dueDate: true, status: false, createDate: false };
        let criteriaDirection = { dueDate: "dueSoonestFirst", status: "doingFirst", createDate: "createOldestFirst" };

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
            const host = document.querySelector("[data-jira-auto-alert]");
            if (!host) {
                return;
            }

            host.innerHTML = message
                ? `<div class="alert alert-danger" role="alert">${escapeHtml(message)}</div>`
                : "";
        }

        function updateSortButtonState() {
            const sortButton = document.querySelector("[data-jira-auto-sort]");
            if (sortButton) {
                sortButton.disabled = searchRows.length === 0;
            }
        }

        function refreshProjectOptions() {
            const select = document.getElementById("jira-auto-project");
            if (!select) {
                return;
            }

            const currentValue = select.value;
            const projects = Array.from(new Set(searchRows.map((row) => row.project).filter(Boolean))).sort();
            select.innerHTML = `<option value="">${select.dataset.allLabel || select.querySelector("option")?.textContent || ""}</option>` +
                projects.map((project) => `<option value="${escapeHtml(project)}">${escapeHtml(project)}</option>`).join("");
            if (projects.includes(currentValue)) {
                select.value = currentValue;
            }
        }

        async function performSearch() {
            const params = new URLSearchParams();
            const project = document.getElementById("jira-auto-project")?.value;
            const status = document.getElementById("jira-auto-status")?.value;
            const text = document.getElementById("jira-auto-text")?.value?.trim();
            const dueFrom = document.getElementById("jira-auto-due-from")?.value;
            const dueTo = document.getElementById("jira-auto-due-to")?.value;

            if (project) params.set("project", project);
            if (status) params.set("status", status);
            if (text) params.set("text", text);
            if (dueFrom) params.set("dueFrom", dueFrom);
            if (dueTo) params.set("dueTo", dueTo);

            try {
                const response = await fetch(`${config.autoScheduleApiUrl}?${params.toString()}`, {
                    headers: { "X-Requested-With": "XMLHttpRequest" }
                });
                const payload = await response.json().catch(() => null);

                if (payload?.requiresLogin) {
                    const returnUrl = `${window.location.pathname}${window.location.search}`;
                    window.location.href = `${config.loginUrl || "/Identity/Account/Login"}?returnUrl=${encodeURIComponent(returnUrl)}`;
                    return;
                }

                if (!response.ok || !payload?.success) {
                    renderAlert(payload?.message || autoConfig.error);
                    searchRows = [];
                } else {
                    const data = payload.data || {};
                    renderAlert(data.errorMessage);
                    searchRows = Array.isArray(data.rows) ? data.rows.slice() : [];
                }
            } catch {
                renderAlert(autoConfig.error);
                searchRows = [];
            }

            editedKeys.clear();
            proposals = [];
            searchTable?.setData(searchRows);
            resultTable?.setData([]);
            document.querySelector("[data-jira-auto-count]").textContent = `${searchRows.length} subtask`;
            document.querySelector("[data-jira-auto-result-hint]").textContent = "";
            document.querySelector("[data-jira-auto-discard]").disabled = true;
            document.querySelector("[data-jira-auto-save]").disabled = true;
            refreshProjectOptions();
            updateSortButtonState();
        }

        function onDueDateEdited(cell) {
            const row = cell.getRow().getData();
            editedKeys.add(row.subTaskKey);
            notify(format(autoConfig.dueEditedToast || "Updated {0}", row.subTaskKey), "success");
        }

        function subTaskKeyFormatter(cell) {
            const key = cell.getValue();
            const text = escapeHtml(key || "-");
            const baseUrl = (config.browseBaseUrl || "").replace(/\/+$/, "");

            if (!baseUrl || !key) {
                return `<span class="jira-dashboard-mono">${text}</span>`;
            }

            const href = `${baseUrl}/browse/${encodeURIComponent(key)}`;
            return `<a class="jira-dashboard-link jira-dashboard-mono" href="${escapeHtml(href)}" target="_blank" rel="noopener noreferrer">${text}</a>`;
        }

        function buildSearchTable() {
            const host = document.getElementById("jira-auto-search-grid");
            if (!host || !window.Tabulator) {
                return;
            }

            const columns = config.columns || {};
            searchTable = new window.Tabulator(host, {
                data: [],
                layout: "fitColumns",
                height: "60vh",
                reactiveData: false,
                placeholder: autoConfig.resultEmpty || "",
                columnDefaults: {
                    resizable: false,
                    tooltip: true
                },
                columns: [
                    { title: columns.index || "#", field: "index", width: 48, minWidth: 44, hozAlign: "right" },
                    { title: columns.subTaskKey || "Key", field: "subTaskKey", width: 150, minWidth: 130, formatter: subTaskKeyFormatter },
                    { title: columns.subTaskSummary || "Summary", field: "subTaskSummary", widthGrow: 2, minWidth: 200 },
                    { title: columns.status || "Status", field: "status", width: 130, minWidth: 110, hozAlign: "center", formatter: statusFormatter },
                    {
                        title: columns.dueDate || "Due date",
                        field: "dueDate",
                        width: 140,
                        minWidth: 130,
                        hozAlign: "center",
                        formatter: dueDateFormatter,
                        editor: dueDateEditor,
                        cellEdited: onDueDateEdited
                    },
                    { title: columns.estimate || "Estimate", field: "estimateHours", width: 130, minWidth: 110, hozAlign: "center", formatter: estimateFormatter }
                ]
            });
        }

        function groupHeader(value, count, data) {
            const parsed = parseIsoDate(value);
            const label = parsed ? `${WEEKDAY_NAMES_VI[parsed.getDay()]}, ${fmtFull(parsed)}` : value;
            const totalHours = data.reduce((sum, row) => sum + Number(row.estimateHours || 0), 0);
            return `${label} <span class="jira-auto-day-group-sum">· ${format(autoConfig.groupSummary || "{0} task · {1}h", count, totalHours.toFixed(1))}</span>`;
        }

        function buildResultTable() {
            const host = document.getElementById("jira-auto-result-grid");
            if (!host || !window.Tabulator) {
                return;
            }

            const columns = config.columns || {};
            resultTable = new window.Tabulator(host, {
                data: [],
                layout: "fitColumns",
                height: "60vh",
                reactiveData: false,
                placeholder: autoConfig.resultEmpty || "",
                groupBy: "newDateKey",
                groupHeader,
                columnDefaults: {
                    resizable: false,
                    tooltip: true
                },
                columns: [
                    { title: columns.subTaskKey || "Key", field: "subTaskKey", width: 150, minWidth: 130, formatter: subTaskKeyFormatter },
                    { title: columns.subTaskSummary || "Summary", field: "subTaskSummary", widthGrow: 2, minWidth: 200 },
                    { title: columns.status || "Status", field: "status", width: 130, minWidth: 110, hozAlign: "center", formatter: statusFormatter },
                    { title: columns.estimate || "Estimate", field: "estimateHours", width: 130, minWidth: 110, hozAlign: "center", formatter: estimateFormatter }
                ]
            });
        }

        function buildComparator() {
            const active = criteriaOrder.filter((id) => criteriaEnabled[id]);
            const ranked = active.length ? active : ["dueDate"];

            return (a, b) => {
                for (const id of ranked) {
                    let cmp = 0;

                    if (id === "status") {
                        const doingFirst = criteriaDirection.status === "doingFirst";
                        const rank = (row) => {
                            const isDoing = /progress|analysis|review|doing|testing|qa|verify/i.test(row.status || "");
                            return doingFirst ? (isDoing ? 0 : 1) : (isDoing ? 1 : 0);
                        };
                        cmp = rank(a) - rank(b);
                    } else if (id === "createDate") {
                        const av = a.createDate ? new Date(a.createDate).getTime() : 0;
                        const bv = b.createDate ? new Date(b.createDate).getTime() : 0;
                        cmp = criteriaDirection.createDate === "createOldestFirst" ? av - bv : bv - av;
                    } else {
                        const av = a.dueDate ? new Date(a.dueDate).getTime() : Number.MAX_SAFE_INTEGER;
                        const bv = b.dueDate ? new Date(b.dueDate).getTime() : Number.MAX_SAFE_INTEGER;
                        cmp = criteriaDirection.dueDate === "dueSoonestFirst" ? av - bv : bv - av;
                    }

                    if (cmp !== 0) {
                        return cmp;
                    }
                }

                return 0;
            };
        }

        function nextMonday() {
            const today = startOfDay(new Date());
            const day = today.getDay();
            const daysUntilMonday = day === 0 ? 1 : 8 - day;
            return addDays(today, daysUntilMonday);
        }

        function renderCriteriaList() {
            const host = document.querySelector("[data-jira-auto-criteria-list]");
            if (!host) {
                return;
            }

            const labels = autoConfig.criteria || {};
            const dirLabels = autoConfig.directions || {};
            const directionOptions = {
                dueDate: [
                    { value: "dueSoonestFirst", label: dirLabels.dueSoonestFirst },
                    { value: "dueLatestFirst", label: dirLabels.dueLatestFirst }
                ],
                status: [
                    { value: "doingFirst", label: dirLabels.doingFirst },
                    { value: "todoFirst", label: dirLabels.todoFirst }
                ],
                createDate: [
                    { value: "createOldestFirst", label: dirLabels.createOldestFirst },
                    { value: "createNewestFirst", label: dirLabels.createNewestFirst }
                ]
            };

            host.innerHTML = criteriaOrder.map((id, index) => {
                const options = directionOptions[id]
                    .map((option) => `<option value="${option.value}" ${criteriaDirection[id] === option.value ? "selected" : ""}>${escapeHtml(option.label || option.value)}</option>`)
                    .join("");

                return `<div class="jira-auto-crit-row" data-id="${id}">
                    <input type="checkbox" class="form-check-input jira-auto-crit-enable" ${criteriaEnabled[id] ? "checked" : ""} />
                    <span class="jira-auto-crit-label">${escapeHtml(labels[id] || id)}</span>
                    <select class="form-select form-select-sm jira-auto-crit-dir">${options}</select>
                    <button type="button" class="jira-auto-crit-btn jira-auto-crit-up" ${index === 0 ? "disabled" : ""}>&uarr;</button>
                    <button type="button" class="jira-auto-crit-btn jira-auto-crit-down" ${index === criteriaOrder.length - 1 ? "disabled" : ""}>&darr;</button>
                </div>`;
            }).join("");

            host.querySelectorAll(".jira-auto-crit-row").forEach((row) => {
                const id = row.dataset.id;
                on(row.querySelector(".jira-auto-crit-enable"), "change", (event) => { criteriaEnabled[id] = event.target.checked; });
                on(row.querySelector(".jira-auto-crit-dir"), "change", (event) => { criteriaDirection[id] = event.target.value; });
                on(row.querySelector(".jira-auto-crit-up"), "click", () => {
                    const i = criteriaOrder.indexOf(id);
                    if (i > 0) {
                        [criteriaOrder[i - 1], criteriaOrder[i]] = [criteriaOrder[i], criteriaOrder[i - 1]];
                        renderCriteriaList();
                    }
                });
                on(row.querySelector(".jira-auto-crit-down"), "click", () => {
                    const i = criteriaOrder.indexOf(id);
                    if (i < criteriaOrder.length - 1) {
                        [criteriaOrder[i + 1], criteriaOrder[i]] = [criteriaOrder[i], criteriaOrder[i + 1]];
                        renderCriteriaList();
                    }
                });
            });
        }

        function openConfigModal() {
            if (!searchRows.length) {
                notify(autoConfig.noEligibleTasks, "warning");
                return;
            }

            document.querySelector("[data-jira-auto-config-summary]").textContent =
                format(autoConfig.configSummary || "{0} task(s)", searchRows.length);

            const noDue = searchRows.filter((row) => !row.dueDate);
            const warningEl = document.querySelector("[data-jira-auto-config-warning]");
            if (noDue.length > 0) {
                warningEl.textContent = format(
                    autoConfig.noDueWarning || "{0} without due date: {1}",
                    noDue.length,
                    noDue.map((row) => row.subTaskKey).join(", ")
                );
                warningEl.classList.remove("d-none");
            } else {
                warningEl.classList.add("d-none");
            }

            const manualList = document.querySelector("[data-jira-auto-manual-list]");
            manualList.innerHTML = searchRows
                .map((row) => `<label class="jira-auto-manual-item"><input type="checkbox" value="${escapeHtml(row.subTaskKey)}" /> <span class="jira-auto-manual-key">${escapeHtml(row.subTaskKey)}</span> ${escapeHtml(row.subTaskSummary)}</label>`)
                .join("");

            renderCriteriaList();
            document.getElementById("jira-auto-cfg-start-date").value = toDateInputValue(nextMonday());

            modalInstance = window.bootstrap?.Modal?.getOrCreateInstance(modalEl);
            modalInstance?.show();
        }

        function runAutoArrange() {
            const startDateValue = document.getElementById("jira-auto-cfg-start-date").value;
            if (!startDateValue) {
                notify(autoConfig.startDateRequired, "warning");
                return;
            }

            const manualKeys = new Set(Array.from(document.querySelectorAll("[data-jira-auto-manual-list] input:checked")).map((el) => el.value));
            const capByPercent = document.getElementById("jira-auto-cap-percent").checked;
            const capValue = capByPercent
                ? Number(document.getElementById("jira-auto-cap-percent-value").value) || 100
                : Number(document.getElementById("jira-auto-cap-hours-value").value) || 8;
            const capacity = capByPercent ? (8 * capValue) / 100 : capValue;
            const skipWeekend = document.getElementById("jira-auto-cfg-skip-weekend").checked;
            const scopeUnplanned = document.getElementById("jira-auto-scope-unplanned").checked;

            let pool = searchRows.slice();
            if (scopeUnplanned) {
                pool = pool.filter((row) => !editedKeys.has(row.subTaskKey));
            }

            const manualRows = pool.filter((row) => manualKeys.has(row.subTaskKey))
                .sort((a, b) => (a.dueDate ? new Date(a.dueDate).getTime() : Infinity) - (b.dueDate ? new Date(b.dueDate).getTime() : Infinity));
            const remaining = pool.filter((row) => !manualKeys.has(row.subTaskKey)).sort(buildComparator());
            const ordered = manualRows.concat(remaining);

            modalInstance?.hide();

            if (ordered.length === 0) {
                notify(autoConfig.noProposalsToast, "warning");
                return;
            }

            let cursor = startOfDay(parseIsoDate(startDateValue));
            if (skipWeekend) {
                while (cursor.getDay() === 0 || cursor.getDay() === 6) {
                    cursor = addDays(cursor, 1);
                }
            }

            let used = 0;
            proposals = ordered.map((row) => {
                const remainingHours = Math.max(0, Number(row.estimateHours || 0) - Number(row.loggedHours || 0)) || Number(row.estimateHours || 0);
                if (used > 0 && used + remainingHours > capacity) {
                    cursor = addDays(cursor, 1);
                    if (skipWeekend) {
                        while (cursor.getDay() === 0 || cursor.getDay() === 6) {
                            cursor = addDays(cursor, 1);
                        }
                    }
                    used = 0;
                }

                used += remainingHours;
                return Object.assign({}, row, { newDateKey: toDateInputValue(cursor) });
            });

            renderResultGrid();
            notify(format("Đã tạo đề xuất cho {0} task.", proposals.length), "success");
        }

        function renderResultGrid() {
            resultTable?.setData(proposals);
            const dayCount = new Set(proposals.map((row) => row.newDateKey)).size;
            document.querySelector("[data-jira-auto-result-hint]").textContent = proposals.length
                ? `${proposals.length} task · ${dayCount} ngày`
                : "";
            document.querySelector("[data-jira-auto-discard]").disabled = proposals.length === 0;
            document.querySelector("[data-jira-auto-save]").disabled = proposals.length === 0;
        }

        function saveProposal() {
            proposals.forEach((proposal) => {
                const row = searchRows.find((item) => item.subTaskKey === proposal.subTaskKey);
                if (row) {
                    row.dueDate = `${proposal.newDateKey}T00:00:00`;
                    editedKeys.add(row.subTaskKey);
                }
            });

            const count = proposals.length;
            proposals = [];
            searchTable?.setData(searchRows);
            renderResultGrid();
            notify(format(autoConfig.savedToast || "Applied {0} task(s)", count), "success");
        }

        function discardProposal() {
            proposals = [];
            renderResultGrid();
            notify(autoConfig.discardedToast, "info");
        }

        function toggleCapMode() {
            const percentMode = document.getElementById("jira-auto-cap-percent").checked;
            document.querySelector("[data-jira-auto-cap-hours-row]").classList.toggle("d-none", percentMode);
            document.querySelector("[data-jira-auto-cap-percent-row]").classList.toggle("d-none", !percentMode);
        }

        return {
            init() {
                config = parseJsonScript("jira-worklist-config", {});
                autoConfig = parseJsonScript("jira-auto-schedule-config", {});
                modalEl = document.getElementById("jira-auto-config-modal");
                searchRows = [];
                proposals = [];
                editedKeys = new Set();

                buildSearchTable();
                buildResultTable();

                on(document.querySelector("[data-jira-auto-search]"), "click", performSearch);
                on(document.querySelector("[data-jira-auto-sort]"), "click", openConfigModal);
                on(document.querySelector("[data-jira-auto-config-run]"), "click", runAutoArrange);
                on(document.querySelector("[data-jira-auto-save]"), "click", saveProposal);
                on(document.querySelector("[data-jira-auto-discard]"), "click", discardProposal);
                on(document.getElementById("jira-auto-cap-hours"), "change", toggleCapMode);
                on(document.getElementById("jira-auto-cap-percent"), "change", toggleCapMode);
                on(document.getElementById("jira-auto-cap-percent-value"), "input", (event) => {
                    const pct = Number(event.target.value) || 0;
                    const preview = document.querySelector("[data-jira-auto-cap-percent-preview]");
                    if (preview) {
                        preview.textContent = ((8 * pct) / 100).toFixed(1);
                    }
                });

                performSearch();
            },
            destroy() {
                offAll();
                if (modalInstance) {
                    modalInstance.hide();
                    modalInstance.dispose();
                    modalInstance = null;
                }
                searchTable?.destroy();
                resultTable?.destroy();
                searchTable = null;
                resultTable = null;
                searchRows = [];
                proposals = [];
                editedKeys = new Set();
                renderAlert("");
            }
        };
    }

    window.ToolboxJiraAutoSchedule = createModule();
})(window, document);
