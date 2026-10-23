// Tabulator demo page (Views/TabulatorDemo/Index.cshtml), a developer page. The grid edits the
// signed-in user's demo rows; added, edited and deleted rows are kept here (pending) until
// "Save changes" posts them in one batch to TabulatorDemo/Save. Deleting asks first and names the
// rows; reloading or leaving the page with unsaved changes asks too.
(function (window, document) {
    'use strict';

    const page = document.querySelector('[data-tabulator-demo]');
    if (!page || !window.Tabulator) {
        return;
    }

    const data = readJson('tabulator-demo-data') || {};
    const text = data.text || {};
    const columnsText = text.columns || {};
    const format = window.Toolbox?.format;
    const token = page.querySelector('input[name="__RequestVerificationToken"]')?.value || '';

    const saveButton = page.querySelector('[data-td-save]');
    const deleteButton = page.querySelector('[data-td-delete]');
    const dirtyPill = page.querySelector('[data-td-dirty]');
    const countText = page.querySelector('[data-td-count]');
    const feedback = page.querySelector('[data-td-feedback]');
    const loading = page.querySelector('[data-td-loading]');
    const errorBox = page.querySelector('[data-td-error]');

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

    function number(value) {
        return format?.number ? format.number(value, 0) : String(value);
    }

    function escapeHtml(value) {
        return String(value ?? '').replace(/[&<>"']/g, function (ch) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[ch];
        });
    }

    function lookup(options) {
        return (options || []).reduce(function (map, item) {
            map[item.value] = item.label;
            return map;
        }, {});
    }

    const categories = lookup(data.categories);
    const statuses = lookup(data.statuses);
    const priorities = lookup(data.priorities);
    const statusTone = { Todo: 'status-todo', InProgress: 'status-doing', Review: 'status-review', Done: 'status-done' };
    const priorityTone = { Low: 'priority-low', Medium: 'priority-medium', High: 'priority-high' };

    // ------------------------------------------------------------ pending changes

    const pending = { created: new Map(), updated: new Map(), deletedIds: new Set() };
    let tempCounter = 0;

    function pendingCount() {
        return pending.created.size + pending.updated.size + pending.deletedIds.size;
    }

    function snapshot(row) {
        const value = Number(row.progress);
        const budget = Number(row.budget);
        return {
            id: row.id,
            title: (row.title || '').trim(),
            category: row.category || 'Ui',
            status: row.status || 'Todo',
            priority: row.priority || 'Medium',
            progress: Number.isFinite(value) ? value : 0,
            budget: Number.isFinite(budget) ? budget : 0,
            dueDate: row.dueDate || null,
            note: (row.note || '').trim()
        };
    }

    function markDirty(row) {
        const item = snapshot(row);
        (item.id == null ? pending.created : pending.updated).set(row.rowKey, item);
        refreshState();
    }

    function clearPending() {
        pending.created.clear();
        pending.updated.clear();
        pending.deletedIds.clear();
        refreshState();
    }

    function refreshState() {
        const count = pendingCount();
        dirtyPill.textContent = count ? fill(text.dirtyState, number(count)) : text.cleanState;
        dirtyPill.classList.toggle('is-warning', count > 0);
        dirtyPill.classList.toggle('is-neutral', count === 0);
        saveButton.disabled = count === 0;
        saveButton.querySelector('[data-td-save-text]').textContent = count ? fill(text.saveCount, number(count)) : text.save;
        countText.textContent = table ? fill(text.rowCount, number(table.getDataCount('active'))) : '';

        const selected = table ? table.getSelectedRows().length : 0;
        deleteButton.disabled = selected === 0;
        deleteButton.querySelector('[data-td-delete-text]').textContent = selected ? fill(text.deleteCount, number(selected)) : text.delete;
    }

    function setFeedback(message, type) {
        feedback.textContent = message || '';
        feedback.classList.toggle('is-success', type === 'success');
        feedback.classList.toggle('is-error', type === 'error');
    }

    // ------------------------------------------------------------ grid

    function pill(tone, label) {
        return label ? '<span class="tbx-pill ' + tone + '">' + escapeHtml(label) + '</span>' : '';
    }

    const empty = page.querySelector('[data-td-empty]');
    const table = new window.Tabulator('#tabulator-grid', {
        index: 'rowKey',
        layout: 'fitColumns',
        maxHeight: '560px',
        selectableRows: 'highlight',
        placeholder: empty ? empty.innerHTML : '',
        columnDefaults: { headerHozAlign: 'center', headerSort: true, headerWordWrap: true, vertAlign: 'middle' },
        columns: [
            { formatter: 'rowSelection', titleFormatter: 'rowSelection', hozAlign: 'center', headerSort: false, width: 48, frozen: true,
                titleFormatterParams: { rowRange: 'active' }, headerTooltip: columnsText.select },
            { title: columnsText.title, field: 'title', editor: 'input', hozAlign: 'left', minWidth: 160,
                formatter: function (cell) { return cell.getValue() ? escapeHtml(cell.getValue()) : '<span class="demo-grid-muted">' + escapeHtml(text.untitled) + '</span>'; } },
            { title: columnsText.category, field: 'category', editor: 'list', editorParams: { values: categories }, hozAlign: 'left', minWidth: 110,
                formatter: function (cell) { return escapeHtml(categories[cell.getValue()] || ''); } },
            { title: columnsText.status, field: 'status', editor: 'list', editorParams: { values: statuses }, hozAlign: 'left', minWidth: 120,
                formatter: function (cell) { return pill(statusTone[cell.getValue()] || 'is-neutral', statuses[cell.getValue()]); } },
            { title: columnsText.priority, field: 'priority', editor: 'list', editorParams: { values: priorities }, hozAlign: 'left', minWidth: 110,
                formatter: function (cell) { return pill(priorityTone[cell.getValue()] || 'is-neutral', priorities[cell.getValue()]); } },
            { title: columnsText.progress, field: 'progress', editor: 'number', editorParams: { min: 0, max: 100, step: 5 }, sorter: 'number', hozAlign: 'center', minWidth: 90,
                formatter: function (cell) { return number(cell.getValue() || 0) + '%'; } },
            { title: columnsText.budget, field: 'budget', editor: 'number', editorParams: { min: 0, step: 100000 }, sorter: 'number', hozAlign: 'right', minWidth: 110,
                formatter: function (cell) { return number(cell.getValue() || 0); } },
            { title: columnsText.dueDate, field: 'dueDate', editor: 'date', hozAlign: 'center', minWidth: 110,
                formatter: function (cell) { return cell.getValue() && format?.date ? escapeHtml(format.date(cell.getValue() + 'T00:00:00')) : escapeHtml(cell.getValue() || ''); } },
            { title: columnsText.note, field: 'note', editor: 'textarea', hozAlign: 'left', minWidth: 130, formatter: 'plaintext' },
            { title: columnsText.updatedAt, field: 'updatedAt', headerSort: false, hozAlign: 'center', minWidth: 140 }
        ]
    });

    const built = new Promise(function (resolve) { table.on('tableBuilt', resolve); });

    table.on('cellEdited', function (cell) {
        markDirty(cell.getRow().getData());
        setFeedback(text.pending);
    });
    table.on('rowSelectionChanged', refreshState);
    table.on('dataProcessed', refreshState);

    // ------------------------------------------------------------ loading and saving

    async function requestJson(url, options) {
        const response = await fetch(url, Object.assign({ credentials: 'same-origin' }, options));
        if (response.status === 401 || response.status === 403) {
            window.location.href = data.loginUrl;
            return null;
        }
        const isJson = (response.headers.get('content-type') || '').includes('application/json');
        const payload = isJson ? await response.json() : await response.text();
        if (!response.ok) {
            const message = typeof payload === 'object' && payload ? payload.message : '';
            const errors = typeof payload === 'object' && payload && Array.isArray(payload.errors) ? payload.errors.join(' ') : '';
            throw new Error([message || text.genericError, errors].filter(Boolean).join(' '));
        }
        return payload;
    }

    // Loading and error replace the grid, so its "no rows yet" text never shows by mistake.
    const shell = page.querySelector('.demo-grid-shell');
    function showState(state) {
        shell.dataset.state = state;
        loading.hidden = state !== 'loading';
        errorBox.hidden = state !== 'error';
        if (state === 'ready') {
            table.redraw(true);
        }
    }

    async function load(showMessage) {
        showState('loading');
        try {
            const rows = await requestJson(data.dataUrl, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
            if (!rows) {
                return;
            }
            await built;
            await table.setData(rows);
            showState('ready');
            clearPending();
            setFeedback(showMessage ? text.reloaded : text.ready, showMessage ? 'success' : null);
        } catch (error) {
            showState('error');
            setFeedback('');
        }
    }

    function confirmDiscard() {
        const count = pendingCount();
        if (!count) {
            return Promise.resolve(true);
        }
        return window.Toolbox.confirm({ title: text.reloadTitle, message: fill(text.reloadConfirm, number(count)), okText: text.reloadOk });
    }

    page.querySelector('[data-td-reload]').addEventListener('click', async function () {
        if (await confirmDiscard()) {
            load(true);
        }
    });
    page.querySelector('[data-td-retry]').addEventListener('click', function () { load(false); });

    page.querySelector('[data-td-add]').addEventListener('click', async function () {
        tempCounter += 1;
        const row = {
            id: null, rowKey: 'tmp-' + tempCounter, title: '', category: 'Ui', status: 'Todo', priority: 'Medium',
            progress: 0, budget: 0, dueDate: '', note: '', updatedAt: ''
        };
        const added = await table.addRow(row, true);
        pending.created.set(row.rowKey, snapshot(row));
        refreshState();
        setFeedback(text.rowAdded);
        // After the click has finished, or the button taking focus would close the editor at once.
        window.setTimeout(function () { added.getCell('title')?.edit(); }, 50);
    });

    deleteButton.addEventListener('click', async function () {
        const rows = table.getSelectedRows();
        if (!rows.length) {
            return;
        }
        const names = rows.slice(0, 3).map(function (row) { return '“' + (row.getData().title || text.untitled) + '”'; });
        const list = names.join(', ') + (rows.length > 3 ? ' ' + fill(text.deleteMore, number(rows.length - 3)) : '');
        const confirmed = await window.Toolbox.confirm({
            title: text.deleteTitle,
            message: fill(text.deleteConfirm, number(rows.length), list),
            okText: text.deleteOk
        });
        if (!confirmed) {
            return;
        }
        rows.forEach(function (row) {
            const item = row.getData();
            if (item.id == null) {
                pending.created.delete(item.rowKey);
            } else {
                pending.updated.delete(item.rowKey);
                pending.deletedIds.add(item.id);
            }
            row.delete();
        });
        refreshState();
        setFeedback(text.rowsDeleted);
    });

    saveButton.addEventListener('click', async function () {
        // An open cell editor commits on blur; wait one tick so its value is in the row.
        document.activeElement?.blur?.();
        await new Promise(function (resolve) { window.setTimeout(resolve, 0); });
        pending.created.forEach(function (_, key) { const row = table.getRow(key); if (row) pending.created.set(key, snapshot(row.getData())); });
        pending.updated.forEach(function (_, key) { const row = table.getRow(key); if (row) pending.updated.set(key, snapshot(row.getData())); });
        if (!pendingCount()) {
            return;
        }

        // A row without a title cannot be saved; point at the first one instead of sending.
        const untitled = table.getRows('active').find(function (row) { return !(row.getData().title || '').trim(); });
        if (untitled) {
            setFeedback(text.titleRequired, 'error');
            table.scrollToRow(untitled, 'nearest', false).finally(function () { untitled.getCell('title')?.edit(); });
            return;
        }

        saveButton.disabled = true;
        setFeedback(text.saving);
        try {
            const result = await requestJson(data.saveUrl, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token, 'X-Requested-With': 'XMLHttpRequest' },
                body: JSON.stringify({
                    created: Array.from(pending.created.values()),
                    updated: Array.from(pending.updated.values()).filter(function (item) { return item.id != null; }),
                    deletedIds: Array.from(pending.deletedIds.values())
                })
            });
            if (!result) {
                return;
            }
            await table.setData(result.items || []);
            clearPending();
            setFeedback(result.message || text.saveSuccess, 'success');
        } catch (error) {
            setFeedback(error.message || text.genericError, 'error');
            refreshState();
        }
    });

    // Leaving with unsaved rows: the browser shows its own "leave site?" question.
    window.addEventListener('beforeunload', function (event) {
        if (pendingCount()) {
            event.preventDefault();
            event.returnValue = '';
        }
    });

    refreshState();
    load(false);
})(window, document);
