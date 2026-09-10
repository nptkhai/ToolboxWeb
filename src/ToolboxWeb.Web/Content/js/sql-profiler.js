(function () {
    'use strict';

    const root = document.getElementById('sp-root');
    const configNode = document.getElementById('sp-config');
    if (!root || !configNode) {
        return;
    }

    const config = JSON.parse(configNode.textContent);
    const messages = config.messages;

    const state = {
        captureId: null,
        running: false,
        grid: null,
        recentGrid: null,
        pollTimer: null,
        clockTimer: null,
        startedAt: 0,
        selected: null,
        canCapture: false
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
        node.classList.remove('ok', 'error');
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

    function number(id, fallback) {
        const value = parseInt(document.getElementById(id).value, 10);
        return Number.isFinite(value) && value >= 0 ? value : fallback;
    }

    function formatClock(ms) {
        const total = Math.floor(ms / 1000);
        const h = String(Math.floor(total / 3600)).padStart(2, '0');
        const m = String(Math.floor((total % 3600) / 60)).padStart(2, '0');
        const s = String(total % 60).padStart(2, '0');
        return h + ':' + m + ':' + s;
    }

    function readConnection() {
        const value = function (field) {
            const node = root.querySelector('[data-field="' + field + '"]');
            if (!node) {
                return '';
            }

            return node.type === 'checkbox' ? node.checked : node.value.trim();
        };

        return {
            server: value('server'),
            database: value('database'),
            username: value('username'),
            password: value('password'),
            trustServerCertificate: value('trustServerCertificate'),
            encrypt: true
        };
    }

    // ---------------------------------------------------------- connection

    function initDatabasePicker() {
        const input = document.getElementById('sp-database');
        if (!input || !window.jQuery || !window.jQuery.fn.kendoComboBox) {
            return;
        }

        window.jQuery(input).kendoComboBox({ dataSource: [], filter: 'contains', suggest: true, clearButton: true });
    }

    async function testConnection() {
        const status = root.querySelector('[data-role="conn-status"]');
        const button = document.getElementById('sp-test');

        setStatus(status, messages.testing);
        button.disabled = true;

        try {
            const result = await callJson(config.urls.testConnection, readConnection());
            if (!result.success) {
                setStatus(status, result.message || messages.genericError, 'error');
                return;
            }

            const data = result.data;
            state.canCapture = data.canCapture;

            const combo = window.jQuery ? window.jQuery('#sp-database').data('kendoComboBox') : null;
            if (combo) {
                const current = combo.value();
                combo.setDataSource(data.databases || []);
                if (current) {
                    combo.value(current);
                }
            }

            if (data.isAzure) {
                setStatus(status, messages.azureNotSupported, 'error');
            } else if (!data.canCapture) {
                setStatus(status, messages.noCapturePermission, 'error');
            } else {
                setStatus(status, messages.connected
                    .replace('{0}', data.serverVersion)
                    .replace('{1}', (data.databases || []).length), 'ok');
            }

            document.getElementById('sp-start').disabled = !data.canCapture;
        } catch (error) {
            setStatus(status, messages.networkError, 'error');
        } finally {
            button.disabled = false;
        }
    }

    // ------------------------------------------------------------- capture

    async function startCapture() {
        const status = root.querySelector('[data-role="conn-status"]');
        const connection = readConnection();

        if (!connection.server || !connection.username) {
            setStatus(status, messages.connectionRequired, 'error');
            return;
        }

        try {
            const result = await callJson(config.urls.start, {
                connection: connection,
                options: {
                    database: connection.database,
                    minDurationMs: number('sp-min-duration', 0),
                    captureProcedures: document.getElementById('sp-cap-proc').checked,
                    captureModules: document.getElementById('sp-cap-module').checked,
                    captureErrors: document.getElementById('sp-cap-error').checked,
                    filterUser: document.getElementById('sp-filter-user').value.trim(),
                    filterHost: document.getElementById('sp-filter-host').value.trim(),
                    filterApp: document.getElementById('sp-filter-app').value.trim(),
                    autoStopMinutes: number('sp-auto-stop', 15)
                }
            });

            if (!result.success) {
                setStatus(status, result.message || messages.genericError, 'error');
                return;
            }

            state.captureId = result.data.captureId;
            state.running = true;
            state.startedAt = Date.now();

            document.getElementById('sp-start').disabled = true;
            document.getElementById('sp-stop').disabled = false;
            document.getElementById('sp-live').hidden = false;
            document.getElementById('sp-export').href =
                config.urls.export + '?captureId=' + encodeURIComponent(state.captureId) + '&mask=' + maskEnabled();
            setStatus(status, messages.capturing, 'ok');

            startClock();
            poll();
        } catch (error) {
            setStatus(status, messages.networkError, 'error');
        }
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
                applyState(result.data);
            }
        } catch (error) {
            // The capture is stopped locally regardless; the server sweeps orphans anyway.
        } finally {
            state.running = false;
            document.getElementById('sp-live').hidden = true;
            document.getElementById('sp-start').disabled = !state.canCapture;
            setStatus(root.querySelector('[data-role="conn-status"]'), messages.stopped, 'ok');
        }
    }

    function maskEnabled() {
        return document.getElementById('sp-mask').checked;
    }

    function startClock() {
        stopTimers();
        state.clockTimer = setInterval(function () {
            document.getElementById('sp-clock').textContent = formatClock(Date.now() - state.startedAt);
        }, 500);
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

            if (result.success) {
                applyState(result.data);

                if (!result.data.running) {
                    // The server auto-stopped it after the time limit.
                    state.running = false;
                    stopTimers();
                    document.getElementById('sp-live').hidden = true;
                    document.getElementById('sp-stop').disabled = true;
                    document.getElementById('sp-start').disabled = !state.canCapture;
                    setStatus(root.querySelector('[data-role="conn-status"]'), messages.autoStop, 'error');
                    return;
                }
            }
        } catch (error) {
            // A failed poll is not fatal; the next tick tries again.
        }

        state.pollTimer = setTimeout(poll, 1500);
    }

    function applyState(data) {
        renderSummary(data.summary, data.eventsDropped);
        renderGrid(data.events || []);
    }

    function renderSummary(summary, dropped) {
        const host = root.querySelector('[data-role="summary"]');
        if (!summary || summary.totalCalls === 0) {
            host.hidden = true;
        } else {
            host.hidden = false;
            host.innerHTML = messages.summary
                .replace('{0}', '<strong>' + summary.totalCalls + '</strong>')
                .replace('{1}', '<strong>' + summary.distinctObjects + '</strong>')
                .replace('{2}', '<strong>' + summary.totalDurationMs.toFixed(0) + '</strong>')
                .replace('{3}', '<strong>' + escapeHtml(summary.slowestObject) + '</strong>')
                .replace('{4}', '<strong>' + summary.slowestMs.toFixed(0) + '</strong>')
                .replace('{5}', '<strong>' + summary.errorCount + '</strong>');
        }

        const warning = root.querySelector('[data-role="dropped"]');
        if (dropped > 0) {
            warning.hidden = false;
            warning.textContent = messages.eventsDropped.replace('{0}', dropped);
        } else {
            warning.hidden = true;
        }
    }

    function rowClass(row) {
        if (row.hasError) {
            return 'sp-failed';
        }

        if (row.durationMs >= number('sp-danger', 1000)) {
            return 'sp-danger';
        }

        return row.durationMs >= number('sp-warn', 300) ? 'sp-warn' : '';
    }

    function renderGrid(rows) {
        if (state.grid) {
            state.grid.replaceData(rows);
            return;
        }

        state.grid = new Tabulator('#sp-grid', {
            data: rows,
            layout: 'fitColumns',
            height: '340px',
            placeholder: messages.gridEmpty,
            index: 'id',
            rowFormatter: function (row) {
                const css = rowClass(row.getData());
                row.getElement().classList.remove('sp-warn', 'sp-danger', 'sp-failed');
                if (css) {
                    row.getElement().classList.add(css);
                }
            },
            columns: [
                { title: config.columns.time, field: 'time', width: 110, hozAlign: 'center', headerHozAlign: 'center' },
                {
                    title: config.columns.kind, field: 'kind', width: 110,
                    hozAlign: 'center', headerHozAlign: 'center',
                    formatter: function (cell) {
                        const kind = cell.getValue();
                        return '<span class="sp-kind ' + kind + '">'
                            + escapeHtml((config.kinds && config.kinds[kind]) || kind) + '</span>';
                    }
                },
                { title: config.columns.objectName, field: 'objectName', hozAlign: 'left', headerHozAlign: 'center' },
                { title: config.columns.user, field: 'userName', width: 150, hozAlign: 'left', headerHozAlign: 'center' },
                { title: config.columns.host, field: 'clientHost', width: 140, hozAlign: 'left', headerHozAlign: 'center' },
                { title: config.columns.app, field: 'appName', width: 160, hozAlign: 'left', headerHozAlign: 'center' },
                {
                    title: config.columns.duration, field: 'durationMs', width: 120,
                    hozAlign: 'right', headerHozAlign: 'center',
                    formatter: function (cell) { return cell.getValue().toFixed(1); }
                },
                {
                    title: config.columns.cpu, field: 'cpuMs', width: 100,
                    hozAlign: 'right', headerHozAlign: 'center',
                    formatter: function (cell) { return cell.getValue().toFixed(1); }
                },
                { title: config.columns.reads, field: 'logicalReads', width: 100, hozAlign: 'right', headerHozAlign: 'center' }
            ]
        });

        state.grid.on('rowClick', function (event, row) {
            showDetail(row.getData());
        });
    }

    // -------------------------------------------------------------- recent

    async function refreshRecent() {
        const status = root.querySelector('[data-role="recent-status"]');
        const connection = readConnection();

        if (!connection.server || !connection.database) {
            setStatus(status, messages.databaseRequired, 'error');
            return;
        }

        setStatus(status, messages.testing);

        try {
            const url = config.urls.recent + '?minutes=' + number('sp-minutes', 5);
            const result = await callJson(url, connection);

            if (!result.success) {
                setStatus(status, result.message || messages.genericError, 'error');
                return;
            }

            renderRecentGrid(result.data.rows || []);
            setStatus(status, messages.planCacheNote, null);
        } catch (error) {
            setStatus(status, messages.networkError, 'error');
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
            height: '340px',
            placeholder: messages.gridEmpty,
            columns: [
                { title: config.columns.objectName, field: 'objectName', hozAlign: 'left', headerHozAlign: 'center' },
                { title: config.columns.calls, field: 'executionCount', width: 100, hozAlign: 'right', headerHozAlign: 'center' },
                { title: config.columns.lastRun, field: 'lastExecutionText', width: 110, hozAlign: 'center', headerHozAlign: 'center' },
                {
                    title: config.columns.average, field: 'averageMs', width: 110,
                    hozAlign: 'right', headerHozAlign: 'center',
                    formatter: function (cell) { return cell.getValue().toFixed(1); }
                },
                {
                    title: config.columns.total, field: 'totalMs', width: 110,
                    hozAlign: 'right', headerHozAlign: 'center',
                    formatter: function (cell) { return cell.getValue().toFixed(1); }
                },
                { title: config.columns.reads, field: 'totalLogicalReads', width: 120, hozAlign: 'right', headerHozAlign: 'center' }
            ]
        });

        state.recentGrid.on('rowClick', function (event, row) {
            const data = row.getData();
            showDetail({ objectName: data.objectName, statement: '', parameters: [], hasError: false });
        });
    }

    // -------------------------------------------------------------- detail

    function showDetail(row) {
        state.selected = row;

        root.querySelector('[data-role="detail-title"]').textContent = row.objectName || '';
        document.getElementById('sp-copy-exec').hidden = !row.statement;
        document.getElementById('sp-load-definition').hidden = !row.objectName;
        setStatus(root.querySelector('[data-role="detail-status"]'), '');

        const body = root.querySelector('[data-role="detail-body"]');
        let html = '';

        if (row.hasError && row.errorMessage) {
            html += '<div class="sp-error-box">' + escapeHtml(row.errorMessage) + '</div>';
        }

        if (row.statement) {
            html += '<div class="sp-exec">' + escapeHtml(row.statement) + '</div>';
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

        body.innerHTML = html || '<p class="muted">' + escapeHtml(messages.detailHint) + '</p>';
    }

    async function loadDefinition() {
        if (!state.selected || !state.selected.objectName) {
            return;
        }

        const status = root.querySelector('[data-role="detail-status"]');
        setStatus(status, messages.loadingDefinition);

        try {
            const url = config.urls.definition + '?objectName=' + encodeURIComponent(state.selected.objectName);
            const result = await callJson(url, readConnection());

            if (!result.success) {
                setStatus(status, result.message || messages.genericError, 'error');
                return;
            }

            const body = root.querySelector('[data-role="detail-body"]');
            const detail = result.data;

            if (detail.definitionError) {
                setStatus(status, detail.definitionError, 'error');
                return;
            }

            setStatus(status, '');
            body.insertAdjacentHTML('beforeend',
                '<div class="sp-definition">' + escapeHtml(detail.definition) + '</div>');
            document.getElementById('sp-load-definition').hidden = true;
        } catch (error) {
            setStatus(status, messages.networkError, 'error');
        }
    }

    // ------------------------------------------------------------- wiring

    initDatabasePicker();
    document.getElementById('sp-start').disabled = true;

    document.getElementById('sp-test').addEventListener('click', testConnection);
    document.getElementById('sp-start').addEventListener('click', startCapture);
    document.getElementById('sp-stop').addEventListener('click', stopCapture);
    document.getElementById('sp-refresh-recent').addEventListener('click', refreshRecent);
    document.getElementById('sp-load-definition').addEventListener('click', loadDefinition);

    // Re-shade rows when a threshold changes, without waiting for the next poll.
    ['sp-warn', 'sp-danger'].forEach(function (id) {
        document.getElementById(id).addEventListener('change', function () {
            if (state.grid) {
                state.grid.redraw(true);
            }
        });
    });

    document.getElementById('sp-mask').addEventListener('change', function () {
        if (state.captureId) {
            document.getElementById('sp-export').href =
                config.urls.export + '?captureId=' + encodeURIComponent(state.captureId) + '&mask=' + maskEnabled();
        }
    });

    document.getElementById('sp-copy-exec').addEventListener('click', async function () {
        const status = root.querySelector('[data-role="detail-status"]');
        try {
            await navigator.clipboard.writeText(state.selected ? state.selected.statement : '');
            setStatus(status, messages.copied, 'ok');
        } catch (error) {
            setStatus(status, messages.copyFailed, 'error');
        }
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
