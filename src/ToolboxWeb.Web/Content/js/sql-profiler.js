// SQL capture (Views/SqlProfiler/Index.cshtml). Three steps: the connection, how to look
// (record a session, or a quick look at SQL Server's own statistics), then the result.
(function () {
    'use strict';

    const root = document.getElementById('sp-root');
    const configNode = document.getElementById('sp-config');
    if (!root || !configNode) {
        return;
    }

    const config = JSON.parse(configNode.textContent);
    const messages = config.messages;
    const OPTIONS_STORAGE_KEY = 'toolbox.sqlProfiler.options.v1';
    const POLL_MS = 1500;

    const state = {
        captureId: null,
        running: false,
        grid: null,
        recentGrid: null,
        pollTimer: null,
        clockTimer: null,
        startedAt: 0,
        autoStopMs: 0,
        pollFailures: 0,
        // Results of "Test connection" for each login typed, so a later edit locks Start again.
        tested: new Map(),
        events: [],
        pending: null,
        dropped: 0,
        clearedIds: new Set(),
        view: 'calls',
        filter: 'all',
        selected: null,
        paused: false,
        panelOpen: false
    };

    // ------------------------------------------------------------ utilities

    function token() {
        const input = root.querySelector('input[name="__RequestVerificationToken"]');
        return input ? input.value : '';
    }

    async function callJson(url, payload) {
        const options = {
            method: payload === undefined ? 'GET' : 'POST',
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        };

        if (payload !== undefined) {
            options.headers['Content-Type'] = 'application/json';
            options.headers['RequestVerificationToken'] = token();
            options.body = payload === null ? '' : JSON.stringify(payload);
        }

        const response = await fetch(url, options);
        if (!response.ok) {
            throw new Error(response.status + ' ' + response.statusText);
        }

        return response.json();
    }

    function setStatus(node, text, tone) {
        if (!node) {
            return;
        }

        node.textContent = text || '';
        node.classList.remove('ok', 'error', 'warn');
        if (tone) {
            node.classList.add(tone);
        }
    }

    function escapeHtml(value) {
        if (value === null || value === undefined) {
            return '';
        }

        return String(value)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
    }

    function icon(name, size) {
        return window.Toolbox && window.Toolbox.icon ? window.Toolbox.icon(name, size) : '';
    }

    function formatNumber(value, decimals) {
        return window.Toolbox.format.number(value || 0, decimals || 0);
    }

    function number(id, fallback) {
        const value = parseInt(document.getElementById(id).value, 10);
        return Number.isFinite(value) && value >= 0 ? value : fallback;
    }

    function formatClock(ms) {
        const total = Math.max(0, Math.floor(ms / 1000));
        const h = String(Math.floor(total / 3600)).padStart(2, '0');
        const m = String(Math.floor((total % 3600) / 60)).padStart(2, '0');
        const s = String(total % 60).padStart(2, '0');
        return h + ':' + m + ':' + s;
    }

    // The shared states (components.css): empty, error and loading look the same on every screen.
    function emptyHtml(iconName, text) {
        return '<div class="tbx-empty" role="status">'
            + '<span class="tbx-empty-icon">' + icon(iconName, 20) + '</span>'
            + '<p class="tbx-empty-text">' + escapeHtml(text) + '</p></div>';
    }

    function errorHtml(text) {
        return '<div class="tbx-error-state" role="alert">' + icon('circle-alert')
            + '<span>' + escapeHtml(text) + '</span></div>';
    }

    function loadingHtml(text, lines) {
        let bars = '';
        for (let i = 0; i < (lines || 4); i++) {
            bars += '<span class="tbx-skeleton" style="width: ' + (92 - i * 15) + '%"></span>';
        }
        return '<div class="sp-loading" role="status"><span class="visually-hidden">' + escapeHtml(text) + '</span>' + bars + '</div>';
    }

    function element(html) {
        const holder = document.createElement('div');
        holder.innerHTML = html;
        return holder.firstElementChild;
    }

    function isTyping(target) {
        return target instanceof HTMLElement
            && (target.isContentEditable || /^(INPUT|TEXTAREA|SELECT)$/.test(target.tagName));
    }

    // ---------------------------------------------------------- connection

    function loginSignature(value) {
        return [value.server.toLowerCase(), value.username.toLowerCase(), value.password, value.trustServerCertificate].join('\u0001');
    }

    // The connection form: Views/Shared/_SqlConnectionForm.cshtml, Content/js/sql-connection.js.
    const connection = window.ToolboxSqlConnection.init(root.querySelector('[data-sql-connection]'), {
        recent: true,
        onChange: updateReadiness,
        // Connecting is not enough to capture; the line under Start says why it stays disabled.
        onTested: function (data) {
            state.tested.set(loginSignature(connection.read()), {
                canCapture: Boolean(data.canCapture) && !data.isAzure,
                isAzure: Boolean(data.isAzure)
            });
            window.setTimeout(updateReadiness, 0);

            if (data.isAzure) {
                return { text: messages.azureNotSupported, tone: 'error' };
            }

            if (!data.canCapture) {
                return { text: messages.noCapturePermission, tone: 'error' };
            }

            return {
                text: messages.connected
                    .replace('{0}', data.serverVersion)
                    .replace('{1}', formatNumber((data.databases || []).length)),
                tone: 'ok'
            };
        }
    });
    connection.panel.addEventListener('input', updateReadiness);

    function readiness() {
        const result = state.tested.get(loginSignature(connection.read()));
        if (!result) {
            return { ok: false, text: messages.readyTest, tone: null };
        }

        if (result.isAzure) {
            return { ok: false, text: messages.azureNotSupported, tone: 'warn' };
        }

        if (!result.canCapture) {
            return { ok: false, text: messages.noCapturePermission, tone: 'warn' };
        }

        return { ok: true, text: '', tone: null };
    }

    function updateReadiness() {
        const ready = readiness();
        const node = root.querySelector('[data-role="ready"]');
        document.getElementById('sp-start').disabled = state.running || !ready.ok;
        if (state.running) {
            setStatus(node, messages.readyRunning);
        } else {
            setStatus(node, ready.text, ready.tone);
        }
    }

    // -------------------------------------------------------------- options

    function rememberedInputs() {
        return Array.from(root.querySelectorAll('[data-remember]'));
    }

    function readValue(input) {
        return input.type === 'checkbox' ? input.checked : input.value;
    }

    function writeValue(input, value) {
        if (input.type === 'checkbox') {
            input.checked = value === true || value === 'true';
        } else {
            input.value = value === null || value === undefined ? '' : String(value);
        }
    }

    function saveOptions() {
        const values = {};
        rememberedInputs().forEach(function (input) {
            values[input.id] = readValue(input);
        });

        try {
            window.localStorage.setItem(OPTIONS_STORAGE_KEY, JSON.stringify({
                values: values,
                advancedOpen: !document.getElementById('sp-advanced').hidden
            }));
        } catch (error) {
            // Storage can be off (private window); the options then reset with the page.
        }

        updateAdvancedCount();
    }

    function restoreOptions() {
        let saved = null;
        try {
            saved = JSON.parse(window.localStorage.getItem(OPTIONS_STORAGE_KEY) || 'null');
        } catch (error) {
            saved = null;
        }

        if (saved && saved.values) {
            rememberedInputs().forEach(function (input) {
                if (Object.prototype.hasOwnProperty.call(saved.values, input.id)) {
                    writeValue(input, saved.values[input.id]);
                }
            });
        }

        setAdvancedOpen(Boolean(saved && saved.advancedOpen));
        updateAdvancedCount();
    }

    function resetOptions() {
        rememberedInputs().forEach(function (input) {
            writeValue(input, input.dataset.default);
        });
        saveOptions();
        refreshRows();
        if (window.Toolbox.notify) {
            window.Toolbox.notify(messages.optionsReset, 'success');
        }
    }

    function setAdvancedOpen(open) {
        document.getElementById('sp-advanced').hidden = !open;
        document.getElementById('sp-toggle-advanced').setAttribute('aria-expanded', open ? 'true' : 'false');
    }

    // A closed panel still says how many of its filters are in force.
    function updateAdvancedCount() {
        const changed = Array.from(document.querySelectorAll('#sp-advanced [data-remember]')).filter(function (input) {
            return String(readValue(input)) !== String(input.dataset.default);
        }).length;
        const badge = root.querySelector('[data-role="advanced-count"]');
        badge.hidden = changed === 0;
        badge.textContent = formatNumber(changed);
    }

    // ------------------------------------------------------------- capture

    function captureOptions(value) {
        return {
            database: value.database,
            minDurationMs: number('sp-min-duration', 0),
            captureProcedures: document.getElementById('sp-cap-proc').checked,
            captureModules: document.getElementById('sp-cap-module').checked,
            captureErrors: document.getElementById('sp-cap-error').checked,
            filterUser: document.getElementById('sp-filter-user').value.trim(),
            filterHost: document.getElementById('sp-filter-host').value.trim(),
            filterApp: document.getElementById('sp-filter-app').value.trim(),
            autoStopMinutes: Math.min(120, Math.max(1, number('sp-auto-stop', 15)))
        };
    }

    function showCaptureError(text) {
        root.querySelector('[data-role="capture-error"]').hidden = !text;
        root.querySelector('[data-role="capture-error-text"]').textContent = text || '';
    }

    // Recording creates an Extended Events session on the server, so it is confirmed first.
    async function startCapture() {
        const value = connection.read();
        if (!readiness().ok) {
            updateReadiness();
            return;
        }

        const options = captureOptions(value);
        const confirmed = await window.Toolbox.confirm({
            title: messages.confirmTitle,
            message: messages.confirmMessage
                .replace('{0}', value.server)
                .replace('{1}', value.database || messages.confirmAllDatabases)
                .replace('{2}', formatNumber(options.autoStopMinutes)),
            okText: messages.confirmOk,
            tone: 'primary'
        });
        if (!confirmed) {
            return;
        }

        showCaptureError('');
        document.getElementById('sp-start').disabled = true;

        try {
            const result = await callJson(config.urls.start, { connection: value, options: options });
            if (!result.success) {
                showCaptureError(result.message || messages.genericError);
                updateReadiness();
                return;
            }

            connection.remember();
            state.captureId = result.data.captureId;
            state.running = true;
            state.startedAt = Date.now();
            state.autoStopMs = options.autoStopMinutes * 60000;
            state.pollFailures = 0;
            state.events = [];
            state.pending = null;
            state.clearedIds = new Set();
            setRunningUi(true);
            setStatus(connection.panel.querySelector('[data-sqlconn-status]'), messages.capturing, 'ok');
            updateExportLink();
            refreshRows();
            startClock();
            poll();
        } catch (error) {
            showCaptureError(messages.networkError);
            updateReadiness();
        }
    }

    function setRunningUi(running) {
        document.getElementById('sp-stop').hidden = !running;
        document.getElementById('sp-stop').disabled = false;
        document.getElementById('sp-live').hidden = !running;
        document.getElementById('sp-clock').hidden = false;
        root.querySelector('[data-role="countdown"]').hidden = !running;
        updateReadiness();
    }

    async function stopCapture() {
        if (!state.captureId) {
            return;
        }

        stopTimers();
        document.getElementById('sp-stop').disabled = true;

        try {
            const url = config.urls.stop
                + '?captureId=' + encodeURIComponent(state.captureId)
                + '&mask=' + maskEnabled();
            const result = await callJson(url, null);

            if (result.success) {
                applyState(result.data, true);
            }
        } catch (error) {
            // The capture is stopped locally regardless; the server sweeps orphans anyway.
        } finally {
            state.running = false;
            setRunningUi(false);
            setStatus(connection.panel.querySelector('[data-sqlconn-status]'), messages.stopped, 'ok');
            refreshRows();
        }
    }

    function maskEnabled() {
        return document.getElementById('sp-mask').checked;
    }

    function updateExportLink() {
        const link = document.getElementById('sp-export');
        const enabled = Boolean(state.captureId);
        link.classList.toggle('disabled', !enabled);
        link.setAttribute('aria-disabled', enabled ? 'false' : 'true');
        link.href = enabled
            ? config.urls.export + '?captureId=' + encodeURIComponent(state.captureId) + '&mask=' + maskEnabled()
            : '#';
    }

    function startClock() {
        stopTimers();
        const tick = function () {
            const elapsed = Date.now() - state.startedAt;
            document.getElementById('sp-clock').textContent = formatClock(elapsed);
            root.querySelector('[data-role="countdown"]').textContent =
                messages.autoStopIn.replace('{0}', formatClock(state.autoStopMs - elapsed).replace(/^00:/, ''));
        };
        tick();
        state.clockTimer = setInterval(tick, 500);
    }

    function stopTimers() {
        if (state.clockTimer) {
            clearInterval(state.clockTimer);
            state.clockTimer = null;
        }

        if (state.pollTimer) {
            clearTimeout(state.pollTimer);
            state.pollTimer = null;
        }
    }

    async function poll() {
        if (!state.running || !state.captureId) {
            return;
        }

        try {
            const url = config.urls.events
                + '?captureId=' + encodeURIComponent(state.captureId)
                + '&mask=' + maskEnabled();
            const result = await callJson(url);

            if (!result.success) {
                throw new Error(result.message || '');
            }

            state.pollFailures = 0;
            showCaptureError(result.data.error ? messages.pollFailed : '');
            applyState(result.data, false);

            if (!result.data.running) {
                // The server stopped it after the time limit.
                state.running = false;
                stopTimers();
                setRunningUi(false);
                setStatus(connection.panel.querySelector('[data-sqlconn-status]'), messages.autoStop, 'error');
                refreshRows();
                return;
            }
        } catch (error) {
            // One failed poll is not fatal; three in a row are worth saying.
            state.pollFailures += 1;
            if (state.pollFailures >= 3) {
                showCaptureError(messages.pollFailed);
            }
        }

        state.pollTimer = setTimeout(poll, POLL_MS);
    }

    function isHeld() {
        return state.paused || state.panelOpen;
    }

    // While the user reads (paused, or a detail open) new rows wait in a buffer, so the row
    // under the cursor does not move; a bar says how many are waiting.
    function applyState(data, force) {
        state.dropped = data.eventsDropped || 0;
        const events = data.events || [];

        if (isHeld() && !force) {
            state.pending = events;
            renderNewRowsBar();
            return;
        }

        state.events = events;
        state.pending = null;
        renderNewRowsBar();
        refreshRows();
    }

    function flushPending() {
        if (state.pending) {
            state.events = state.pending;
            state.pending = null;
        }
        renderNewRowsBar();
        refreshRows();
    }

    function renderNewRowsBar() {
        const bar = root.querySelector('[data-role="new-rows"]');
        const count = state.pending
            ? state.pending.filter(function (row) { return !state.clearedIds.has(row.id); }).length - baseRows().length
            : 0;
        bar.hidden = count <= 0;
        root.querySelector('[data-role="new-rows-text"]').textContent = messages.newRows.replace('{0}', formatNumber(count));
    }

    function setPaused(paused) {
        state.paused = paused;
        const button = document.getElementById('sp-pause');
        button.setAttribute('aria-pressed', paused ? 'true' : 'false');
        button.querySelector('[data-role="pause-text"]').textContent = paused ? messages.resume : messages.pause;
        if (!paused && !state.panelOpen) {
            flushPending();
        }
    }

    // Hides what is on screen now; later calls still come in. The export file keeps everything.
    function clearList() {
        (state.pending || state.events).forEach(function (row) {
            state.clearedIds.add(row.id);
        });
        state.selected = null;
        flushPending();
        if (window.Toolbox.notify) {
            window.Toolbox.notify(messages.cleared, 'info');
        }
    }

    // ------------------------------------------------------------- results

    function warnMs() {
        return number('sp-warn', 300);
    }

    function dangerMs() {
        return number('sp-danger', 1000);
    }

    function isSlow(row) {
        return !row.hasError && row.durationMs >= warnMs();
    }

    function baseRows() {
        return state.events.filter(function (row) { return !state.clearedIds.has(row.id); });
    }

    function searchText() {
        return document.getElementById('sp-search').value.trim().toLowerCase();
    }

    function matchesSearch(row, search) {
        return !search || [row.objectName, row.userName, row.clientHost, row.appName, row.database]
            .join(' ').toLowerCase().indexOf(search) >= 0;
    }

    function matchesFilter(row) {
        if (state.filter === 'slow') {
            return isSlow(row);
        }

        if (state.filter === 'error') {
            return row.hasError;
        }

        return true;
    }

    // Newest first: the call just made in the other app is the one at the top.
    function visibleCalls() {
        const search = searchText();
        return baseRows()
            .filter(function (row) { return matchesSearch(row, search) && matchesFilter(row); })
            .reverse();
    }

    function groupedRows() {
        const groups = new Map();
        visibleCalls().forEach(function (row) {
            const key = row.kind + '|' + row.objectName;
            let group = groups.get(key);
            if (!group) {
                group = { id: key, kind: row.kind, objectName: row.objectName, calls: 0, totalMs: 0, slowestMs: 0, errors: 0 };
                groups.set(key, group);
            }
            group.calls += 1;
            group.totalMs += row.durationMs || 0;
            group.slowestMs = Math.max(group.slowestMs, row.durationMs || 0);
            group.errors += row.hasError ? 1 : 0;
        });

        return Array.from(groups.values())
            .map(function (group) {
                group.averageMs = group.calls ? group.totalMs / group.calls : 0;
                return group;
            })
            .sort(function (a, b) { return b.totalMs - a.totalMs; });
    }

    function renderCounts() {
        const search = searchText();
        const rows = baseRows().filter(function (row) { return matchesSearch(row, search); });
        const counts = {
            all: rows.length,
            slow: rows.filter(isSlow).length,
            error: rows.filter(function (row) { return row.hasError; }).length
        };
        Object.keys(counts).forEach(function (key) {
            root.querySelector('[data-count="' + key + '"]').textContent = formatNumber(counts[key]);
        });
    }

    function renderSummary() {
        const rows = baseRows();
        const host = root.querySelector('[data-role="summary"]');
        if (!rows.length) {
            host.hidden = true;
        } else {
            const slowest = rows.reduce(function (best, row) { return row.durationMs > best.durationMs ? row : best; }, rows[0]);
            const objects = new Set(rows.map(function (row) { return row.objectName; })).size;
            const total = rows.reduce(function (sum, row) { return sum + (row.durationMs || 0); }, 0);
            const errors = rows.filter(function (row) { return row.hasError; }).length;
            host.hidden = false;
            host.innerHTML = escapeHtml(messages.summary)
                .replace('{0}', '<strong>' + formatNumber(rows.length) + '</strong>')
                .replace('{1}', '<strong>' + formatNumber(objects) + '</strong>')
                .replace('{2}', '<strong>' + formatNumber(total) + '</strong>')
                .replace('{3}', '<strong>' + escapeHtml(slowest.objectName) + '</strong>')
                .replace('{4}', '<strong>' + formatNumber(slowest.durationMs) + '</strong>')
                .replace('{5}', '<strong>' + formatNumber(errors) + '</strong>');
        }

        const warning = root.querySelector('[data-role="dropped"]');
        warning.hidden = state.dropped <= 0;
        warning.textContent = messages.eventsDropped.replace('{0}', formatNumber(state.dropped));
    }

    function placeholder() {
        if (!state.captureId) {
            return element(emptyHtml('activity', messages.gridBeforeStart));
        }

        if (baseRows().length) {
            return element(emptyHtml('search', messages.gridNoMatch));
        }

        return element(emptyHtml(state.running ? 'loader-circle' : 'inbox', state.running ? messages.waiting : messages.gridEmpty));
    }

    function kindPill(kind) {
        const tone = kind === 'Error' ? 'is-danger' : (kind === 'Procedure' ? 'is-accent' : 'is-neutral');
        return '<span class="tbx-pill ' + tone + '">' + escapeHtml((config.kinds && config.kinds[kind]) || kind) + '</span>';
    }

    function rowClass(row) {
        if (row.hasError) {
            return 'sp-failed';
        }

        if (row.durationMs >= dangerMs()) {
            return 'sp-danger';
        }

        return row.durationMs >= warnMs() ? 'sp-warn' : '';
    }

    function msFormatter(cell) {
        return formatNumber(cell.getValue(), 1);
    }

    function callColumns() {
        return [
            { title: config.columns.time, field: 'time', width: 116, hozAlign: 'center', headerHozAlign: 'center' },
            {
                title: config.columns.kind, field: 'kind', width: 116, hozAlign: 'center', headerHozAlign: 'center',
                formatter: function (cell) { return kindPill(cell.getValue()); }
            },
            { title: config.columns.objectName, field: 'objectName', minWidth: 200, hozAlign: 'left', headerHozAlign: 'center', responsive: 0 },
            { title: config.columns.user, field: 'userName', width: 140, hozAlign: 'left', headerHozAlign: 'center', responsive: 3 },
            { title: config.columns.host, field: 'clientHost', width: 130, hozAlign: 'left', headerHozAlign: 'center', responsive: 4 },
            { title: config.columns.app, field: 'appName', width: 150, hozAlign: 'left', headerHozAlign: 'center', responsive: 5 },
            { title: config.columns.duration, field: 'durationMs', width: 140, hozAlign: 'right', headerHozAlign: 'center', formatter: msFormatter, responsive: 0 },
            { title: config.columns.cpu, field: 'cpuMs', width: 110, hozAlign: 'right', headerHozAlign: 'center', formatter: msFormatter, responsive: 2 },
            {
                title: config.columns.reads, field: 'logicalReads', width: 96, hozAlign: 'right', headerHozAlign: 'center', responsive: 2,
                formatter: function (cell) { return formatNumber(cell.getValue()); }
            }
        ];
    }

    function objectColumns() {
        return [
            {
                title: config.columns.kind, field: 'kind', width: 116, hozAlign: 'center', headerHozAlign: 'center',
                formatter: function (cell) { return kindPill(cell.getValue()); }
            },
            { title: config.columns.objectName, field: 'objectName', minWidth: 200, hozAlign: 'left', headerHozAlign: 'center', responsive: 0 },
            {
                title: config.groupColumns.calls, field: 'calls', width: 130, hozAlign: 'right', headerHozAlign: 'center', responsive: 0,
                formatter: function (cell) { return formatNumber(cell.getValue()); }
            },
            { title: config.groupColumns.average, field: 'averageMs', width: 160, hozAlign: 'right', headerHozAlign: 'center', formatter: msFormatter, responsive: 1 },
            { title: config.groupColumns.slowest, field: 'slowestMs', width: 160, hozAlign: 'right', headerHozAlign: 'center', formatter: msFormatter, responsive: 2 },
            { title: config.groupColumns.total, field: 'totalMs', width: 120, hozAlign: 'right', headerHozAlign: 'center', formatter: msFormatter, responsive: 3 },
            {
                title: config.groupColumns.errors, field: 'errors', width: 90, hozAlign: 'right', headerHozAlign: 'center', responsive: 4,
                formatter: function (cell) { return formatNumber(cell.getValue()); }
            }
        ];
    }

    function refreshRows() {
        renderSummary();
        renderCounts();
        renderNewRowsBar();

        const rows = state.view === 'objects' ? groupedRows() : visibleCalls();

        if (!state.grid) {
            state.grid = new Tabulator('#sp-grid', {
                data: rows,
                layout: 'fitColumns',
                responsiveLayout: 'hide',
                maxHeight: '60vh',
                index: 'id',
                placeholder: placeholder,
                columns: callColumns(),
                rowFormatter: function (row) {
                    const data = row.getData();
                    const css = state.view === 'calls' ? rowClass(data) : (data.errors ? 'sp-failed' : '');
                    row.getElement().classList.remove('sp-warn', 'sp-danger', 'sp-failed', 'sp-selected');
                    if (css) {
                        row.getElement().classList.add(css);
                    }
                    if (state.selected && state.selected.id === data.id) {
                        row.getElement().classList.add('sp-selected');
                    }
                }
            });

            state.grid.on('rowClick', function (event, row) {
                if (state.view === 'objects') {
                    openGroup(row.getData());
                } else {
                    showDetail(row.getData());
                }
            });
            return;
        }

        state.grid.replaceData(rows);
    }

    function setView(view) {
        state.view = view;
        root.querySelectorAll('[data-view]').forEach(function (button) {
            button.setAttribute('aria-pressed', button.dataset.view === view ? 'true' : 'false');
        });
        if (state.grid) {
            state.grid.setColumns(view === 'objects' ? objectColumns() : callColumns());
        }
        refreshRows();
    }

    // An object row narrows the call list to that object.
    function openGroup(group) {
        document.getElementById('sp-search').value = group.objectName;
        setView('calls');
    }

    function setFilter(filter) {
        state.filter = filter;
        root.querySelectorAll('[data-filter]').forEach(function (button) {
            button.setAttribute('aria-pressed', button.dataset.filter === filter ? 'true' : 'false');
        });
        refreshRows();
    }

    // J / K walk the calls in the order they are on screen and keep the detail panel in step.
    function stepRow(direction) {
        if (!state.grid || state.view !== 'calls') {
            return;
        }

        const rows = state.grid.getRows('active').map(function (row) { return row.getData(); });
        if (!rows.length) {
            return;
        }

        const index = state.selected ? rows.findIndex(function (row) { return row.id === state.selected.id; }) : -1;
        const next = index < 0 ? 0 : Math.min(rows.length - 1, Math.max(0, index + direction));
        state.grid.scrollToRow(rows[next].id, 'nearest', false);
        showDetail(rows[next]);
    }

    // -------------------------------------------------------------- recent

    function showResults(mode) {
        root.querySelector('[data-results="capture"]').hidden = mode !== 'capture';
        root.querySelector('[data-results="recent"]').hidden = mode !== 'recent';
        if (mode === 'capture' && state.grid) {
            state.grid.redraw();
        }
        if (mode === 'recent' && state.recentGrid) {
            state.recentGrid.redraw();
        }
    }

    function setRecentState(html) {
        const host = root.querySelector('[data-role="recent-state"]');
        host.innerHTML = html || '';
        document.getElementById('sp-recent-grid').hidden = Boolean(html);
    }

    async function refreshRecent() {
        const value = connection.read();
        if (!value.server || !value.database) {
            setRecentState(errorHtml(messages.databaseRequired));
            return;
        }

        const button = document.getElementById('sp-refresh-recent');
        const minutes = Math.min(1440, Math.max(1, number('sp-minutes', 5)));
        button.disabled = true;
        root.querySelector('[data-role="recent-note"]').hidden = true;
        setRecentState(loadingHtml(messages.recentLoading, 5));

        try {
            const result = await callJson(config.urls.recent + '?minutes=' + minutes, value);
            if (!result.success) {
                setRecentState(errorHtml(result.message || messages.genericError));
                return;
            }

            const rows = result.data.rows || [];
            root.querySelector('[data-role="recent-note"]').hidden = false;
            if (!rows.length) {
                setRecentState(emptyHtml('inbox', messages.recentEmpty.replace('{0}', formatNumber(minutes))));
                return;
            }

            setRecentState('');
            renderRecentGrid(rows);
        } catch (error) {
            setRecentState(errorHtml(messages.networkError));
        } finally {
            button.disabled = false;
        }
    }

    function renderRecentGrid(rows) {
        if (state.recentGrid) {
            state.recentGrid.replaceData(rows);
            return;
        }

        state.recentGrid = new Tabulator('#sp-recent-grid', {
            data: rows,
            layout: 'fitColumns',
            responsiveLayout: 'hide',
            maxHeight: '60vh',
            columns: [
                { title: config.columns.objectName, field: 'objectName', minWidth: 200, hozAlign: 'left', headerHozAlign: 'center', responsive: 0 },
                {
                    title: config.columns.calls, field: 'executionCount', width: 110, hozAlign: 'right', headerHozAlign: 'center', responsive: 0,
                    formatter: function (cell) { return formatNumber(cell.getValue()); }
                },
                { title: config.columns.lastRun, field: 'lastExecutionText', width: 110, hozAlign: 'center', headerHozAlign: 'center', responsive: 2 },
                { title: config.columns.average, field: 'averageMs', width: 130, hozAlign: 'right', headerHozAlign: 'center', formatter: msFormatter, responsive: 1 },
                { title: config.columns.total, field: 'totalMs', width: 120, hozAlign: 'right', headerHozAlign: 'center', formatter: msFormatter, responsive: 3 },
                {
                    title: config.columns.reads, field: 'totalLogicalReads', width: 110, hozAlign: 'right', headerHozAlign: 'center', responsive: 4,
                    formatter: function (cell) { return formatNumber(cell.getValue()); }
                }
            ]
        });

        state.recentGrid.on('rowClick', function (event, row) {
            const data = row.getData();
            showDetail({ id: 'recent|' + data.objectName, objectName: data.objectName, kind: 'Procedure', statement: '', parameters: [], hasError: false, recent: true });
        });
    }

    // -------------------------------------------------------------- detail

    const panelElement = document.getElementById('sp-detail');
    const panel = window.bootstrap.Offcanvas.getOrCreateInstance(panelElement);

    function metaItem(labelText, value) {
        if (value === null || value === undefined || value === '') {
            return '';
        }
        return '<div><dt>' + escapeHtml(labelText) + '</dt><dd>' + escapeHtml(value) + '</dd></div>';
    }

    function showDetail(row) {
        state.selected = row;
        if (state.grid) {
            state.grid.getRows('active').forEach(function (gridRow) {
                gridRow.getElement().classList.toggle('sp-selected', gridRow.getData().id === row.id);
            });
        }

        const labels = config.detailLabels;
        panelElement.querySelector('[data-role="detail-title"]').textContent = row.objectName || '';
        panelElement.querySelector('[data-role="detail-meta"]').innerHTML = row.recent ? '' : kindPill(row.kind)
            + '<dl class="sp-meta">'
            + metaItem(labels.time, row.time)
            + metaItem(labels.duration, formatNumber(row.durationMs, 1))
            + metaItem(labels.cpu, formatNumber(row.cpuMs, 1))
            + metaItem(labels.reads, formatNumber(row.logicalReads))
            + metaItem(labels.rows, formatNumber(row.rowCount))
            + metaItem(labels.database, row.database)
            + metaItem(labels.user, row.userName)
            + metaItem(labels.host, row.clientHost)
            + metaItem(labels.app, row.appName)
            + metaItem(labels.session, row.sessionId)
            + '</dl>';

        let html = '';
        if (row.hasError && row.errorMessage) {
            html += errorHtml(row.errorMessage);
        }

        if (row.statement) {
            html += '<pre class="sp-code">' + escapeHtml(row.statement) + '</pre>';
        } else if (!row.recent) {
            html += '<p class="sp-note">' + escapeHtml(messages.noStatement) + '</p>';
        }

        if (row.parameters && row.parameters.length) {
            html += '<table class="sp-param-table"><thead><tr>'
                + '<th>' + escapeHtml(config.params.name) + '</th>'
                + '<th>' + escapeHtml(config.params.kind) + '</th>'
                + '<th>' + escapeHtml(config.params.value) + '</th>'
                + '</tr></thead><tbody>'
                + row.parameters.map(function (p) {
                    return '<tr><td class="name">' + escapeHtml(p.name) + '</td>'
                        + '<td class="kind">' + escapeHtml(p.kind) + '</td>'
                        + '<td class="value">' + escapeHtml(p.value) + '</td></tr>';
                }).join('')
                + '</tbody></table>';
        }

        panelElement.querySelector('[data-role="detail-body"]').innerHTML = html;
        panelElement.querySelector('[data-role="definition"]').innerHTML = '';
        setStatus(panelElement.querySelector('[data-role="detail-status"]'), '');
        document.getElementById('sp-copy-exec').disabled = !row.statement;
        document.getElementById('sp-load-definition').disabled = !row.objectName;
        panel.show();
    }

    async function loadDefinition() {
        const row = state.selected;
        if (!row || !row.objectName) {
            return;
        }

        const host = panelElement.querySelector('[data-role="definition"]');
        const button = document.getElementById('sp-load-definition');
        button.disabled = true;
        host.innerHTML = loadingHtml(messages.loadingDefinition, 6);

        try {
            const url = config.urls.definition + '?objectName=' + encodeURIComponent(row.objectName);
            const result = await callJson(url, connection.read());
            if (state.selected !== row) {
                return;
            }

            if (!result.success || result.data.definitionError) {
                host.innerHTML = errorHtml(result.success ? result.data.definitionError : (result.message || messages.genericError));
                button.disabled = false;
                return;
            }

            host.innerHTML = '<pre class="sp-code sp-definition">' + escapeHtml(result.data.definition) + '</pre>';
        } catch (error) {
            host.innerHTML = errorHtml(messages.networkError);
            button.disabled = false;
        }
    }

    // ------------------------------------------------------------- wiring

    restoreOptions();
    updateReadiness();
    updateExportLink();
    refreshRows();
    setRecentState(emptyHtml('clock', messages.recentBefore));

    document.getElementById('sp-start').addEventListener('click', startCapture);
    document.getElementById('sp-stop').addEventListener('click', stopCapture);
    document.getElementById('sp-refresh-recent').addEventListener('click', refreshRecent);
    document.getElementById('sp-load-definition').addEventListener('click', loadDefinition);
    document.getElementById('sp-reset-options').addEventListener('click', resetOptions);
    document.getElementById('sp-clear').addEventListener('click', clearList);
    document.getElementById('sp-show-new').addEventListener('click', function () {
        setPaused(false);
        if (state.panelOpen) {
            flushPending();
        }
    });
    document.getElementById('sp-pause').addEventListener('click', function () {
        setPaused(!state.paused);
    });

    document.getElementById('sp-toggle-advanced').addEventListener('click', function () {
        setAdvancedOpen(document.getElementById('sp-advanced').hidden);
        saveOptions();
    });

    rememberedInputs().forEach(function (input) {
        input.addEventListener(input.type === 'checkbox' ? 'change' : 'input', saveOptions);
    });

    // Re-shade rows when a threshold changes, without waiting for the next poll.
    ['sp-warn', 'sp-danger'].forEach(function (id) {
        document.getElementById(id).addEventListener('input', refreshRows);
    });

    document.getElementById('sp-mask').addEventListener('change', updateExportLink);
    document.getElementById('sp-search').addEventListener('input', refreshRows);

    root.querySelectorAll('[data-filter]').forEach(function (button) {
        button.addEventListener('click', function () { setFilter(button.dataset.filter); });
    });
    root.querySelectorAll('[data-view]').forEach(function (button) {
        button.addEventListener('click', function () { setView(button.dataset.view); });
    });

    document.getElementById('sp-tab-capture').addEventListener('shown.bs.tab', function () { showResults('capture'); });
    document.getElementById('sp-tab-recent').addEventListener('shown.bs.tab', function () { showResults('recent'); });

    // Reading a call holds new rows back, so it does not slide away under the reader.
    panelElement.addEventListener('show.bs.offcanvas', function () {
        state.panelOpen = true;
    });
    panelElement.addEventListener('hidden.bs.offcanvas', function () {
        state.panelOpen = false;
        if (!state.paused) {
            flushPending();
        }
    });

    document.getElementById('sp-copy-exec').addEventListener('click', async function () {
        const status = panelElement.querySelector('[data-role="detail-status"]');
        try {
            await navigator.clipboard.writeText(state.selected ? state.selected.statement : '');
            setStatus(status, messages.copied, 'ok');
        } catch (error) {
            setStatus(status, messages.copyFailed, 'error');
        }
    });

    document.getElementById('sp-export').addEventListener('click', function (event) {
        if (!state.captureId) {
            event.preventDefault();
        }
    });

    document.addEventListener('keydown', function (event) {
        const key = event.key ? event.key.toLowerCase() : '';
        if ((key !== 'j' && key !== 'k') || event.ctrlKey || event.metaKey || event.altKey
            || isTyping(event.target) || document.querySelector('.modal.show')
            || root.querySelector('[data-results="capture"]').hidden) {
            return;
        }

        event.preventDefault();
        stepRow(key === 'j' ? 1 : -1);
    });

    // Closing the tab would otherwise leave an event session running on the server until the
    // auto-stop deadline. sendBeacon cannot carry the antiforgery header, so this posts to a
    // dedicated endpoint that only ever stops the caller's own capture.
    window.addEventListener('beforeunload', function () {
        if (!state.running || !state.captureId) {
            return;
        }

        navigator.sendBeacon(
            config.urls.abandon + '?captureId=' + encodeURIComponent(state.captureId),
            new Blob([], { type: 'text/plain' }));
    });
})();
