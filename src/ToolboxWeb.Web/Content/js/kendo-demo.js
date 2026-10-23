// Kendo UI Core demo page (Views/DemoKendo/Index.cshtml), a developer page. Every widget is
// started through initWidget: a widget missing from the loaded bundle shows a note on its card
// instead of breaking the page. Texts and sample rows come from #kendo-demo-data.
(function (window, document, $) {
    'use strict';

    const page = document.querySelector('[data-kendo-demo]');
    if (!page) {
        return;
    }

    const data = readJson('kendo-demo-data') || { tasks: [], categories: [], priorities: [], text: {} };
    const text = data.text || {};
    const format = window.Toolbox?.format;
    const vietnamese = (document.documentElement.lang || '').toLowerCase().startsWith('vi');

    function readJson(id) {
        try {
            return JSON.parse(document.getElementById(id)?.textContent || 'null');
        } catch (error) {
            return null;
        }
    }

    function fill(template) {
        const args = Array.prototype.slice.call(arguments, 1);
        return String(template || '').replace(/\{(\d+)\}/g, function (match, index) {
            return args[index] !== undefined ? args[index] : match;
        });
    }

    function escapeHtml(value) {
        return String(value ?? '').replace(/[&<>"']/g, function (ch) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[ch];
        });
    }

    function number(value) {
        return format?.number ? format.number(value, 0) : String(value);
    }

    function dateText(value) {
        return format?.date ? format.date(value) : String(value);
    }

    function icon(name) {
        return window.Toolbox?.icon ? window.Toolbox.icon(name, 16) : '';
    }

    // Each tab has its own address (/demo-kendo/<tab>.html), kept in the URL bar while switching.
    page.querySelectorAll('[data-route-url]').forEach(function (trigger) {
        trigger.addEventListener('shown.bs.tab', function () {
            const next = new URL(trigger.dataset.routeUrl, window.location.origin);
            if (next.pathname !== window.location.pathname) {
                window.history.replaceState(window.history.state, '', next.pathname);
            }
        });
    });

    if (!$ || !window.kendo) {
        page.querySelectorAll('.component-card').forEach(function (card) {
            showMissing(card, 'Kendo UI Core');
        });
        return;
    }

    if (vietnamese && window.kendo.cultures['vi-VN']) {
        window.kendo.culture('vi-VN');
    }

    const popup = { appendTo: $(document.body) };

    function showMissing(card, name) {
        if (!card || card.querySelector('[data-missing]')) {
            return;
        }
        const note = document.createElement('div');
        note.className = 'component-status-note';
        note.setAttribute('data-missing', '');
        note.textContent = fill(text.missing, name);
        card.appendChild(note);
    }

    function initWidget(selector, plugin, options) {
        const element = $(selector);
        if (!element.length) {
            return null;
        }
        if (typeof $.fn[plugin] !== 'function') {
            showMissing(element.closest('.component-card')[0], plugin.replace(/^kendo/, ''));
            return null;
        }
        element[plugin](options || {});
        return element.data(plugin);
    }

    // ------------------------------------------------------------ DataSource + ListView + Pager

    const summary = page.querySelector('[data-kd-summary]');
    const emptyBox = page.querySelector('[data-kd-empty]');
    const tasks = (data.tasks || []).map(function (task) {
        return Object.assign({}, task, { due: new Date(task.due + 'T00:00:00') });
    });

    const dataSource = new window.kendo.data.DataSource({
        data: tasks,
        pageSize: 4,
        sort: { field: 'due', dir: 'asc' },
        schema: {
            model: {
                id: 'id',
                fields: {
                    id: { type: 'number' },
                    progress: { type: 'number' },
                    budget: { type: 'number' },
                    due: { type: 'date' }
                }
            }
        },
        change: updateSummary
    });

    function rowHtml(task) {
        return '<div class="tbx-row tbx-row--two-line demo-row" role="listitem">'
            + '<div class="tbx-row-main">'
            + '<span class="tbx-row-title">' + escapeHtml(task.name) + '</span>'
            + '<span class="tbx-row-sub">' + escapeHtml(task.owner) + ' · ' + escapeHtml(task.categoryLabel)
            + ' · ' + escapeHtml(text.due) + ' ' + escapeHtml(dateText(task.due)) + '</span>'
            + '</div>'
            + '<span class="tbx-pill status-' + escapeHtml(task.statusCode) + '">' + escapeHtml(task.statusLabel) + '</span>'
            + '<span class="tbx-pill priority-' + escapeHtml(task.priorityCode) + ' demo-row-priority">' + escapeHtml(task.priorityLabel) + '</span>'
            + '<span class="demo-row-progress" title="' + escapeHtml(text.progress) + '">'
            + '<span class="mini-progress"><span style="width:' + Number(task.progress) + '%"></span></span>'
            + '<span class="tbx-row-meta">' + number(task.progress) + '%</span></span>'
            + '<span class="tbx-row-actions">'
            + '<button class="btn btn-sm tbx-icon-button" type="button" data-kd-detail="' + task.id + '" title="' + escapeHtml(text.detail) + '" aria-label="' + escapeHtml(text.detail + ': ' + task.name) + '">' + icon('eye') + '</button>'
            + '<button class="btn btn-sm tbx-icon-button is-danger" type="button" data-kd-delete="' + task.id + '" title="' + escapeHtml(text.delete) + '" aria-label="' + escapeHtml(text.delete + ': ' + task.name) + '">' + icon('trash-2') + '</button>'
            + '</span>'
            + '</div>';
    }

    initWidget('#kd-listview', 'kendoListView', {
        dataSource: dataSource,
        template: function (task) { return rowHtml(task); }
    });

    initWidget('#kd-pager', 'kendoPager', {
        dataSource: dataSource,
        buttonCount: 5,
        info: true
    });

    function updateSummary() {
        const all = dataSource.data().toJSON();
        const done = all.filter(function (task) { return task.statusCode === 'done'; }).length;
        const average = all.length ? Math.round(all.reduce(function (sum, task) { return sum + task.progress; }, 0) / all.length) : 0;
        summary.textContent = fill(text.summary, number(all.length), number(dataSource.total()), number(done), number(average));
        emptyBox.hidden = dataSource.total() > 0;
    }

    const searchInput = page.querySelector('[data-kd-search]');
    const sortSelect = page.querySelector('[data-kd-sort]');
    const categoryBox = initWidget('#kd-category', 'kendoComboBox', {
        dataSource: data.categories,
        dataTextField: 'text',
        dataValueField: 'value',
        filter: 'contains',
        clearButton: true,
        change: applyFilters,
        popup: popup
    });

    let searchTimer = 0;
    searchInput.addEventListener('input', function () {
        window.clearTimeout(searchTimer);
        searchTimer = window.setTimeout(applyFilters, 180);
    });

    function applyFilters() {
        const search = searchInput.value.trim();
        const category = categoryBox ? categoryBox.value() : '';
        const filters = [];
        if (search) {
            filters.push({
                logic: 'or',
                filters: ['name', 'owner', 'note'].map(function (field) {
                    return { field: field, operator: 'contains', value: search, ignoreCase: true };
                })
            });
        }
        if (category) {
            filters.push({ field: 'category', operator: 'eq', value: category });
        }
        dataSource.filter(filters.length ? { logic: 'and', filters: filters } : []);
    }

    sortSelect.addEventListener('change', function () {
        const parts = sortSelect.value.split('-');
        dataSource.sort({ field: parts[0], dir: parts[1] });
    });

    page.querySelectorAll('[data-kd-reset]').forEach(function (button) {
        button.addEventListener('click', function () {
            searchInput.value = '';
            categoryBox?.value('');
            sortSelect.value = 'due-asc';
            dataSource.sort({ field: 'due', dir: 'asc' });
            dataSource.filter([]);
        });
    });

    const detailWindow = initWidget('#kd-detail-window', 'kendoWindow', {
        width: 'min(460px, calc(100vw - 32px))',
        title: text.detailTitle,
        visible: false,
        modal: true,
        actions: ['Close']
    });

    page.addEventListener('click', async function (event) {
        const detail = event.target.closest('[data-kd-detail]');
        const remove = event.target.closest('[data-kd-delete]');
        if (detail && detailWindow) {
            const task = dataSource.get(Number(detail.dataset.kdDetail));
            if (!task) {
                return;
            }
            detailWindow.content('<div class="demo-window-body">'
                + '<h3 class="demo-window-title">' + escapeHtml(task.name) + '</h3>'
                + '<p>' + escapeHtml(task.note) + '</p>'
                + '<dl class="demo-facts">'
                + '<dt>' + escapeHtml(text.owner) + '</dt><dd>' + escapeHtml(task.owner) + '</dd>'
                + '<dt>' + escapeHtml(text.due) + '</dt><dd>' + escapeHtml(dateText(task.due)) + '</dd>'
                + '<dt>' + escapeHtml(text.budget) + '</dt><dd>' + escapeHtml(number(task.budget)) + '</dd>'
                + '</dl></div>');
            detailWindow.center().open();
        } else if (remove) {
            const task = dataSource.get(Number(remove.dataset.kdDelete));
            if (!task) {
                return;
            }
            const confirmed = await window.Toolbox.confirm({
                title: text.deleteTitle,
                message: fill(text.deleteConfirm, task.name),
                okText: text.delete
            });
            if (confirmed) {
                dataSource.remove(task);
                window.Toolbox.notify(fill(text.deleted, task.name), 'success');
            }
        }
    });

    // ------------------------------------------------------------ selection

    initWidget('#kd-autocomplete', 'kendoAutoComplete', {
        dataSource: ['API', 'CSS', 'Dashboard', 'JavaScript', 'Jira', 'Kendo', 'Popup', 'SQL', 'UI'],
        filter: 'contains',
        suggest: true,
        popup: popup
    });

    initWidget('#kd-multiselect', 'kendoMultiSelect', {
        dataSource: data.categories,
        dataTextField: 'text',
        dataValueField: 'value',
        value: ['Ui', 'Localization'],
        placeholder: text.multiSelectPlaceholder,
        autoClose: false,
        filter: 'contains',
        popup: popup
    });

    initWidget('#kd-combobox', 'kendoComboBox', {
        dataSource: ['C#', 'JavaScript', 'TypeScript', 'SQL', 'Python', 'PowerShell'],
        placeholder: text.comboPlaceholder,
        filter: 'contains',
        suggest: true,
        clearButton: true,
        popup: popup
    });

    initWidget('#kd-dropdownlist', 'kendoDropDownList', {
        dataSource: data.priorities,
        dataTextField: 'text',
        dataValueField: 'value',
        value: 'medium',
        popup: popup
    });

    initWidget('#kd-listbox', 'kendoListBox', {
        dataSource: tasks.slice(0, 4).map(function (task) { return { text: task.name, value: task.id }; }),
        dataTextField: 'text',
        dataValueField: 'value',
        selectable: 'multiple',
        toolbar: { tools: ['moveUp', 'moveDown'] }
    });

    // ------------------------------------------------------------ dates, numbers, form

    const datePattern = format?.datePattern || 'dd/MM/yyyy';
    const timePattern = vietnamese ? 'HH:mm' : 'h:mm tt';
    const today = new Date();

    initWidget('#kd-datepicker', 'kendoDatePicker', { value: today, format: datePattern, popup: popup });
    initWidget('#kd-datetimepicker', 'kendoDateTimePicker', {
        value: new Date(today.getFullYear(), today.getMonth(), today.getDate(), 14, 0),
        format: datePattern + ' ' + timePattern,
        timeFormat: timePattern,
        interval: 15,
        popup: popup
    });
    initWidget('#kd-timepicker', 'kendoTimePicker', {
        value: new Date(today.getFullYear(), today.getMonth(), today.getDate(), 9, 30),
        format: timePattern,
        interval: 15,
        popup: popup
    });
    initWidget('#kd-numeric', 'kendoNumericTextBox', { value: 5000000, min: 0, step: 100000, format: 'n0', decimals: 0 });
    initWidget('#kd-slider', 'kendoSlider', { min: 0, max: 100, smallStep: 5, largeStep: 25, value: 65, showButtons: false });
    initWidget('#kd-phone', 'kendoMaskedTextBox', { mask: '0000 000 000' });

    const form = page.querySelector('#kd-validation-form');
    const result = page.querySelector('[data-kd-validate-result]');
    function showValidation(valid) {
        result.textContent = valid ? text.validOk : text.validFail;
        result.classList.toggle('is-valid', valid);
        result.classList.toggle('is-invalid', !valid);
    }

    // The validator checks on submit and stops an invalid one itself; its validate event reports both outcomes.
    const validator = initWidget('#kd-validation-form', 'kendoValidator', {
        messages: { required: text.emailRequired, email: text.emailRequired },
        validate: function (event) { showValidation(event.valid); }
    });
    form.addEventListener('submit', function (event) {
        event.preventDefault();
        if (!validator) {
            showValidation(form.checkValidity());
        }
    });

    // ------------------------------------------------------------ layout, popup

    initWidget('#kd-menu', 'kendoMenu', { popupCollision: 'fit flip' });
    initWidget('#kd-splitter', 'kendoSplitter', {
        panes: [{ size: '55%', min: '160px', collapsible: true }, { min: '160px', collapsible: true }]
    });
    initWidget('#kd-panelbar', 'kendoPanelBar', { expandMode: 'multiple' });

    // The splitter is sized when its tab is first shown; a hidden tab has no width yet.
    page.querySelectorAll('[data-bs-toggle="tab"]').forEach(function (trigger) {
        trigger.addEventListener('shown.bs.tab', function () {
            $('#kd-splitter').data('kendoSplitter')?.resize(true);
        });
    });

    const demoWindow = initWidget('#kd-window', 'kendoWindow', {
        width: 'min(420px, calc(100vw - 32px))',
        title: text.windowTitle,
        visible: false,
        modal: true,
        actions: ['Minimize', 'Maximize', 'Close']
    });
    page.querySelector('[data-kd-open-window]').addEventListener('click', function () {
        demoWindow?.center().open();
    });

    initWidget('#kd-tooltip-target', 'kendoTooltip', {
        content: text.tooltip,
        position: 'bottom',
        width: 260,
        showOn: 'mouseenter focus'
    });

    const notices = { info: text.notifyInfo, success: text.notifySuccess, error: text.notifyError };
    page.querySelectorAll('[data-kd-notify]').forEach(function (button) {
        button.addEventListener('click', function () {
            window.Toolbox.notify(notices[button.dataset.kdNotify], button.dataset.kdNotify);
        });
    });

    updateSummary();
})(window, document, window.jQuery);
