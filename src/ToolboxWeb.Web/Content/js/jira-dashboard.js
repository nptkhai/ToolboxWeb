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
  const currentType = periodTypeInput?.value || "week";

  const inputs = {
    day: document.getElementById("jira-dashboard-day"),
    week: document.getElementById("jira-dashboard-week"),
    month: document.getElementById("jira-dashboard-month"),
    year: document.getElementById("jira-dashboard-year")
  };

  const widgets = {
    day: null,
    month: null,
    year: null
  };

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

  function initNativeFallback() {
    if (inputs.day) {
      inputs.day.type = "date";
      inputs.day.value = periodValueInput?.value && currentType === "day"
        ? periodValueInput.value
        : (inputs.day.value || "");
    }

    if (inputs.month) {
      inputs.month.type = "month";
    }

    if (inputs.year) {
      inputs.year.type = "number";
      inputs.year.min = "2000";
      inputs.year.max = "2100";
      inputs.year.inputMode = "numeric";
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

    widgets.month = $(inputs.month).kendoDatePicker({
      format: "MM/yyyy",
      start: "year",
      depth: "year",
      value: parseIsoMonth(inputs.month.value),
      popup: popupOptions
    }).data("kendoDatePicker");

    widgets.year = $(inputs.year).kendoDatePicker({
      format: "yyyy",
      start: "decade",
      depth: "decade",
      value: parseYear(inputs.year.value),
      popup: popupOptions
    }).data("kendoDatePicker");

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

    return inputs.week?.value || "";
  }

  function statusBadge(status, tone) {
    return `<span class="jira-dashboard-status jira-dashboard-status--${escapeHtml(tone || "todo")}">${escapeHtml(status || "-")}</span>`;
  }

  function mapRows(data) {
    return Array.isArray(data)
      ? data.map((row) => ({
          index: row.Index,
          project: row.Project,
          subTaskKey: row.SubTaskKey,
          subTaskSummary: row.SubTaskSummary,
          issueKey: row.IssueKey,
          issueSummary: row.IssueSummary,
          status: row.Status,
          statusTone: row.StatusTone,
          dueDateText: row.DueDateText,
          dueDateSort: row.DueDate || "",
          dueDateSortValue: toDueDateSortValue(row.DueDate),
          estimateTimeText: row.EstimateTimeText,
          loggedTimeText: row.LoggedTimeText,
          estimateHours: row.EstimateHours,
          loggedHours: row.LoggedHours,
          isOverdue: row.IsOverdue
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

    const table = new Tabulator(gridHost, {
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

  buttons.forEach((button) => {
    button.addEventListener("click", () => {
      syncVisibleInput(button.dataset.period || "week");
    });
  });

  form.addEventListener("submit", () => {
    const activeType = periodTypeInput?.value || "week";
    if (periodValueInput) {
      periodValueInput.value = resolvePeriodValue(activeType);
    }
  });

  initKendoPickers();
  initTabulator();
  syncVisibleInput(currentType);
})(window, document, window.jQuery);
