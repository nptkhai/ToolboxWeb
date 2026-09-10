(function () {
    'use strict';

    const root = document.getElementById('sc-root');
    const configNode = document.getElementById('sc-config');
    if (!root || !configNode) {
        return;
    }

    const config = JSON.parse(configNode.textContent);
    const messages = config.messages;
    const CONNECTION_STORAGE_KEY = 'toolbox.schemaCompare.connections.v1';
    const CONNECTION_SECRET_KEY = 'toolbox.schemaCompare.connectionSecrets.v1';

    const state = {
        comparisonId: null,
        rows: [],
        stats: null,
        grid: null,
        typeFilter: null,
        actionFilter: 'all',
        selectedId: null,
        pollTimer: null,
        clockTimer: null,
        startedAt: 0,
        scriptDirty: true,
        showMapped: false,
        detail: null,
        expandedFolds: new Set(),
        onlyChanges: true,
        detailExpanded: false,
        togglePending: false
    };

    const SYMBOLS = { Add: '⇒', Change: '><', Delete: 'X' };

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
            options.body = JSON.stringify(payload);
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

    function label(map, code) {
        return (map && map[code]) || code;
    }

    function formatClock(ms) {
        const total = Math.floor(ms / 1000);
        const h = String(Math.floor(total / 3600)).padStart(2, '0');
        const m = String(Math.floor((total % 3600) / 60)).padStart(2, '0');
        const s = String(total % 60).padStart(2, '0');
        return h + ':' + m + ':' + s;
    }

    // ----------------------------------------------------------- connections

    function readEndpoint(panel) {
        const value = function (field) {
            const node = panel.querySelector('[data-field="' + field + '"]');
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

    function readStoredJson(storage, key) {
        try {
            const raw = storage.getItem(key);
            return raw ? JSON.parse(raw) : null;
        } catch (error) {
            return null;
        }
    }

    function writeStoredJson(storage, key, value) {
        try {
            storage.setItem(key, JSON.stringify(value));
        } catch (error) {
            // Private browsing or a storage policy can disable browser storage. Comparing must
            // still work; the user will simply have to enter the connection again next time.
        }
    }

    function savedEndpoint(endpoint) {
        return {
            server: endpoint.server,
            database: endpoint.database,
            username: endpoint.username,
            trustServerCertificate: endpoint.trustServerCertificate
        };
    }

    function setEndpointField(panel, field, value) {
        const node = panel.querySelector('[data-field="' + field + '"]');
        if (!node || value === null || value === undefined) {
            return;
        }

        if (node.type === 'checkbox') {
            node.checked = Boolean(value);
            return;
        }

        if (field === 'database') {
            const combo = databaseComboBox(panel);
            if (combo) {
                combo.value(String(value));
                return;
            }
        }

        node.value = String(value);
    }

    function restoreConnections() {
        const saved = readStoredJson(window.localStorage, CONNECTION_STORAGE_KEY) || {};
        const secrets = readStoredJson(window.sessionStorage, CONNECTION_SECRET_KEY) || {};

        ['source', 'target'].forEach(function (side) {
            const panel = root.querySelector('[data-side="' + side + '"]');
            if (!panel) {
                return;
            }

            const endpoint = saved[side] || {};
            ['server', 'database', 'username', 'trustServerCertificate'].forEach(function (field) {
                setEndpointField(panel, field, endpoint[field]);
            });
            setEndpointField(panel, 'password', secrets[side]);
        });
    }

    function persistConnections() {
        const source = readEndpoint(root.querySelector('[data-side="source"]'));
        const target = readEndpoint(root.querySelector('[data-side="target"]'));

        // Non-secret values survive browser restarts. Passwords deliberately live only in the
        // current browser session so they are not written as clear text to persistent storage.
        writeStoredJson(window.localStorage, CONNECTION_STORAGE_KEY, {
            source: savedEndpoint(source),
            target: savedEndpoint(target)
        });
        writeStoredJson(window.sessionStorage, CONNECTION_SECRET_KEY, {
            source: source.password,
            target: target.password
        });
    }

    function databaseComboBox(panel) {
        const input = panel.querySelector('[data-field="database"]');
        return input && window.jQuery ? window.jQuery(input).data('kendoComboBox') : null;
    }

    function initDatabasePicker(panel) {
        const input = panel.querySelector('[data-field="database"]');
        if (!input || !window.jQuery || !window.jQuery.fn.kendoComboBox) {
            return;
        }

        window.jQuery(input).kendoComboBox({
            dataSource: [],
            filter: 'contains',
            suggest: true,
            clearButton: true,
            change: persistConnections
        });
    }

    async function testConnection(panel) {
        const status = panel.querySelector('[data-role="status"]');
        const button = panel.querySelector('[data-action="test"]');

        persistConnections();
        setStatus(status, messages.testing);
        button.disabled = true;

        try {
            const result = await callJson(config.urls.testConnection, readEndpoint(panel));
            if (!result.success) {
                setStatus(status, result.message || messages.genericError, 'error');
                return;
            }

            const combo = databaseComboBox(panel);
            if (combo) {
                const current = combo.value();
                combo.setDataSource(result.data.databases || []);
                if (current) {
                    combo.value(current);
                }
            }

            setStatus(status, messages.connected
                .replace('{0}', result.data.serverVersion)
                .replace('{1}', (result.data.databases || []).length), 'ok');
        } catch (error) {
            setStatus(status, messages.networkError, 'error');
        } finally {
            button.disabled = false;
        }
    }

    // --------------------------------------------------------------- compare

    function readScope() {
        const scope = {};
        root.querySelectorAll('[data-scope]').forEach(function (node) {
            scope[node.dataset.scope] = node.checked;
        });
        return scope;
    }

    function readOptions() {
        const options = {};
        root.querySelectorAll('[data-option]').forEach(function (node) {
            options[node.dataset.option] = node.checked;
        });
        return options;
    }

    // Each non-empty line is "SOURCE=TARGET"; anything malformed is skipped.
    function readMappings() {
        return document.getElementById('sc-mapping-input').value
            .split('\n')
            .map(function (line) {
                const eq = line.indexOf('=');
                if (eq <= 0) {
                    return null;
                }

                const from = line.slice(0, eq).trim();
                const to = line.slice(eq + 1).trim();
                return from && to ? { from: from, to: to } : null;
            })
            .filter(function (pair) { return pair !== null; });
    }

    // Mirrors SqlNameMapper.SwapCode on the server: a whole underscore-delimited segment is
    // replaced, so ORG turns EDU_ORG_DATA into EDU_FBU_DATA but leaves ORGANIZATION alone.
    function swapCode(name, from, to) {
        if (!name || !from || !to) {
            return null;
        }

        const parts = name.split('_');
        let hit = false;
        const swapped = parts.map(function (part) {
            if (part.toUpperCase() === from.toUpperCase()) {
                hit = true;
                return to;
            }
            return part;
        });

        return hit ? swapped.join('_') : null;
    }

    function codeToken(text, isTarget) {
        return '<span class="sc-code-token' + (isTarget ? ' to' : '') + '">' + escapeHtml(text) + '</span>';
    }

    // Shows the rewrite the typed codes produce, using the databases actually in play rather
    // than a fixed sentence, so a wrong code is visible before the comparison runs.
    function renderCodePreview() {
        const box = root.querySelector('[data-role="code-preview"]');
        if (!box) {
            return;
        }

        const codes = readCodes();
        if (!codes.sourceCode || !codes.targetCode) {
            box.innerHTML = escapeHtml(messages.mappingCodeEmpty);
            return;
        }

        const candidates = [];
        const sourceDb = readEndpoint(root.querySelector('[data-side="source"]')).database;
        if (sourceDb) {
            candidates.push(sourceDb);
        }

        readMappings().forEach(function (pair) {
            if (candidates.indexOf(pair.from) < 0) {
                candidates.push(pair.from);
            }
        });

        const pairs = [];
        candidates.forEach(function (name) {
            const swapped = swapCode(name, codes.sourceCode, codes.targetCode);
            if (swapped && pairs.length < 3) {
                pairs.push(codeToken(name) + ' <span aria-hidden="true">&rarr;</span> ' + codeToken(swapped, true));
            }
        });

        if (!pairs.length) {
            pairs.push(codeToken('*_' + codes.sourceCode + '_*')
                + ' <span aria-hidden="true">&rarr;</span> '
                + codeToken('*_' + codes.targetCode + '_*', true));
        }

        box.innerHTML = '<strong>' + escapeHtml(messages.mappingCodeTitle) + '</strong> '
            + pairs.map(function (p) { return '<span class="sc-code-pair">' + p + '</span>'; }).join('')
            + (candidates.length > pairs.length ? '&hellip; ' : '')
            + escapeHtml(messages.mappingCodeSuffix);
    }

    function readCodes() {
        return {
            sourceCode: document.getElementById('sc-code-from').value.trim(),
            targetCode: document.getElementById('sc-code-to').value.trim()
        };
    }

    // EDU_ORG_DATA and EDU_FBU_DATA differ in exactly one underscore segment, and that
    // segment is the school code. Deriving it covers every database at once instead of
    // guessing at a fixed list of suffixes.
    function deriveCodes(sourceDatabase, targetDatabase) {
        const from = sourceDatabase.split('_');
        const to = targetDatabase.split('_');
        if (from.length !== to.length) {
            return null;
        }

        let index = -1;
        for (let i = 0; i < from.length; i += 1) {
            if (from[i].toUpperCase() === to[i].toUpperCase()) {
                continue;
            }

            // More than one differing segment is not a school code, so stay silent
            // rather than guess wrong.
            if (index >= 0) {
                return null;
            }

            index = i;
        }

        return index < 0 ? null : { sourceCode: from[index], targetCode: to[index] };
    }

    function autoFillMappings() {
        const source = readEndpoint(root.querySelector('[data-side="source"]'));
        const target = readEndpoint(root.querySelector('[data-side="target"]'));
        const status = root.querySelector('[data-role="compare-status"]');
        if (!source.database || !target.database || source.database === target.database) {
            return;
        }

        const codes = deriveCodes(source.database, target.database);
        if (!codes) {
            setStatus(status, messages.mappingAutoFailed, 'error');
            return;
        }

        document.getElementById('sc-code-from').value = codes.sourceCode;
        document.getElementById('sc-code-to').value = codes.targetCode;
        renderCodePreview();
        setStatus(status, messages.mappingAutoFilled);
    }

    async function startCompare() {
        const button = document.getElementById('sc-compare');
        const status = root.querySelector('[data-role="compare-status"]');
        const source = readEndpoint(root.querySelector('[data-side="source"]'));
        const target = readEndpoint(root.querySelector('[data-side="target"]'));
        const codes = readCodes();

        if (!source.server || !source.database || !target.server || !target.database) {
            setStatus(status, messages.bothSidesRequired, 'error');
            return;
        }

        persistConnections();
        button.disabled = true;
        setStatus(status, messages.comparing);
        resetResults();

        try {
            const started = await callJson(config.urls.start, {
                source: source,
                target: target,
                scope: readScope(),
                options: readOptions(),
                nameMappings: readMappings(),
                sourceCode: codes.sourceCode,
                targetCode: codes.targetCode
            });

            if (!started.success) {
                setStatus(status, started.message || messages.genericError, 'error');
                button.disabled = false;
                return;
            }

            state.comparisonId = started.data.comparisonId;
            document.getElementById('sc-cancel').hidden = false;
            document.getElementById('sc-results').hidden = false;
            showTab('sc-tab-progress');
            startClock();
            poll();
        } catch (error) {
            setStatus(status, messages.networkError, 'error');
            button.disabled = false;
        }
    }

    function resetResults() {
        state.rows = [];
        state.stats = null;
        state.selectedId = null;
        state.typeFilter = null;
        state.actionFilter = 'all';
        state.scriptDirty = true;
        state.showMapped = false;
        root.querySelector('[data-role="mapped-note"]').hidden = true;
        root.querySelector('[data-role="progress-source"]').innerHTML = '';
        root.querySelector('[data-role="progress-target"]').innerHTML = '';
        root.querySelector('[data-role="type-list"]').innerHTML = '';
        root.querySelector('[data-role="type-chips"]').innerHTML = '';
        root.querySelector('[data-role="card-list"]').innerHTML = '';
        hideDetailPane();
        document.getElementById('sc-script').value = '';
        renderScriptWarnings([]);
        renderDetail(null);
        if (state.grid) {
            state.grid.setData([]);
        }
    }

    function startClock() {
        state.startedAt = Date.now();
        stopClock();
        state.clockTimer = setInterval(function () {
            document.getElementById('sc-clock').textContent = formatClock(Date.now() - state.startedAt);
        }, 500);
    }

    function stopClock() {
        if (state.clockTimer) {
            clearInterval(state.clockTimer);
            state.clockTimer = null;
        }
    }

    async function poll() {
        try {
            const response = await callJson(config.urls.progress + '?comparisonId=' + encodeURIComponent(state.comparisonId));
            if (!response.success) {
                finishCompare(response.message || messages.genericError, true);
                return;
            }

            const progress = response.data;
            renderProgress(progress);

            if (!progress.completed) {
                state.pollTimer = setTimeout(poll, 700);
                return;
            }

            stopClock();
            document.getElementById('sc-clock').textContent = formatClock(progress.elapsedMs);

            if (progress.error) {
                finishCompare(progress.error, true);
                return;
            }

            applyResult(progress.result);
            finishCompare('', false);
        } catch (error) {
            finishCompare(messages.networkError, true);
        }
    }

    // The script carries its own warnings — DROP statements above all — and they matter more
    // than anything else on the tab, so they go above the script rather than inside it.
    function renderScriptWarnings(warnings) {
        const box = root.querySelector('[data-role="script-warnings"]');
        const list = root.querySelector('[data-role="script-warning-list"]');
        if (!box || !list) {
            return;
        }

        const items = Array.isArray(warnings) ? warnings : [];
        list.innerHTML = items.map(function (line) {
            return '<li>' + escapeHtml(line) + '</li>';
        }).join('');
        box.hidden = items.length === 0;
    }

    function finishCompare(message, isError) {
        stopClock();
        const cancel = document.getElementById('sc-cancel');
        cancel.hidden = true;
        cancel.disabled = false;
        document.getElementById('sc-compare').disabled = false;
        setStatus(root.querySelector('[data-role="compare-status"]'), message, isError ? 'error' : null);
    }

    // Extracting a large database runs for minutes. The server owns the cancellation token,
    // so this only asks; the poll loop reports whatever the server settles on.
    async function cancelCompare() {
        if (!state.comparisonId) {
            return;
        }

        const cancel = document.getElementById('sc-cancel');
        cancel.disabled = true;
        setStatus(root.querySelector('[data-role="compare-status"]'), messages.cancelling);

        try {
            await callJson(config.urls.cancel, { comparisonId: state.comparisonId });
        } catch (error) {
            cancel.disabled = false;
        }
    }

    function renderProgress(progress) {
        renderProgressColumn('progress-source', progress.source);
        renderProgressColumn('progress-target', progress.target);
    }

    function renderProgressColumn(role, lines) {
        const body = root.querySelector('[data-role="' + role + '"]');
        if (!body || body.childElementCount === lines.length) {
            return;
        }

        const html = lines.map(function (line) {
            return '<tr><td class="time">' + escapeHtml(line.time) + '</td>'
                + '<td>' + escapeHtml(line.operation) + '</td>'
                + '<td class="state ' + escapeHtml(line.state) + '">' + escapeHtml(line.state) + '</td></tr>';
        }).join('');

        body.innerHTML = html;
        const scroller = body.closest('.sc-scroll');
        if (scroller) {
            scroller.scrollTop = scroller.scrollHeight;
        }
    }

    // ---------------------------------------------------------------- result

    function applyResult(result) {
        if (!result) {
            return;
        }

        state.rows = result.rows || [];
        state.stats = result.stats;
        state.scriptDirty = true;

        root.querySelector('[data-role="compare-status"]').textContent = messages.summary
            .replace('{0}', result.sourceLabel)
            .replace('{1}', result.targetLabel);

        renderTypeList();
        renderFilterCounts();
        renderMappedNote();
        renderGrid();
        renderDetail(null);
        showTab('sc-tab-result');

        const realCount = state.rows.filter(function (row) { return !row.mappedSame; }).length;
        if (result.isEqual || realCount === 0) {
            setStatus(root.querySelector('[data-role="compare-status"]'), messages.noDifferences, 'ok');
        }
    }

    function renderMappedNote() {
        const note = root.querySelector('[data-role="mapped-note"]');
        const count = state.stats ? state.stats.mappedSame : 0;

        if (!count) {
            note.hidden = true;
            return;
        }

        note.hidden = false;
        root.querySelector('[data-role="mapped-text"]').textContent =
            messages.mappedSame.replace('{0}', count);
        document.getElementById('sc-toggle-mapped').textContent =
            state.showMapped ? messages.hideMapped : messages.showMapped;
    }

    function renderTypeList() {
        renderTypeChips();
        const host = root.querySelector('[data-role="type-list"]');
        const stats = state.stats;
        host.innerHTML = '';

        const all = document.createElement('li');
        all.className = state.typeFilter === null ? 'active' : '';
        all.innerHTML = '<span>' + escapeHtml(messages.allTypes) + '</span><span class="count">' + (stats ? stats.total : 0) + '</span>';
        all.addEventListener('click', function () {
            state.typeFilter = null;
            renderTypeList();
            renderGrid();
        });

        host.appendChild(all);

        (stats ? stats.byType : []).forEach(function (entry) {
            const item = document.createElement('li');
            item.className = state.typeFilter === entry.objectType ? 'active' : '';
            item.innerHTML = '<span>' + escapeHtml(entry.objectType) + '</span><span class="count">' + entry.total + '</span>';
            item.addEventListener('click', function () {
                state.typeFilter = state.typeFilter === entry.objectType ? null : entry.objectType;
                renderTypeList();
                renderGrid();
            });
            host.appendChild(item);
        });
    }

    function renderFilterCounts() {
        const stats = state.stats;
        const counts = {
            all: stats ? stats.total : 0,
            Add: stats ? stats.onlyInSource : 0,
            Change: stats ? stats.different : 0,
            Delete: stats ? stats.onlyInTarget : 0
        };

        Object.keys(counts).forEach(function (key) {
            // The same counts appear on the filter chips and in the run bar summary.
            root.querySelectorAll('[data-count="' + key + '"]').forEach(function (node) {
                node.textContent = counts[key];
            });
        });

        const summary = root.querySelector('[data-role="quick-summary"]');
        if (summary) {
            summary.hidden = !stats;
        }
    }

    function visibleRows() {
        const search = document.getElementById('sc-search').value.trim().toLowerCase();

        return state.rows.filter(function (row) {
            // Mapped-same rows are equivalent after mapping; hidden unless asked for.
            if (row.mappedSame && !state.showMapped) {
                return false;
            }

            if (state.typeFilter && row.objectType !== state.typeFilter) {
                return false;
            }

            if (state.actionFilter !== 'all' && row.action !== state.actionFilter) {
                return false;
            }

            if (!search) {
                return true;
            }

            return (row.sourceName + ' ' + row.targetName + ' ' + row.objectType).toLowerCase().indexOf(search) >= 0;
        });
    }

    function syncHeaderCheck() {
        const checkbox = document.querySelector('#sc-grid .sc-header-check');
        if (!checkbox) {
            return;
        }

        const rows = visibleRows().filter(function (row) { return row.canToggle; });
        const included = rows.filter(function (row) { return row.included; }).length;
        checkbox.checked = rows.length > 0 && included === rows.length;
        checkbox.indeterminate = included > 0 && included < rows.length;
        checkbox.disabled = state.togglePending || rows.length === 0;
    }

    function statusPill(action) {
        const text = label(config.actions, action);
        return '<span class="sc-status-symbol ' + escapeHtml(action) + '" title="' + escapeHtml(text) + '">'
            + '<span aria-hidden="true">' + escapeHtml(SYMBOLS[action] || '?') + '</span>'
            + '<span>' + escapeHtml(text) + '</span></span>';
    }

    // Under 600px the table is unreadable, so the same rows are drawn as cards. Both are in
    // the DOM and CSS decides which one is on screen; they share visibleRows() so they can
    // never disagree.
    function renderCardList() {
        const host = root.querySelector('[data-role="card-list"]');
        if (!host) {
            return;
        }

        const rows = visibleRows();
        host.innerHTML = '';

        if (!rows.length) {
            const empty = document.createElement('p');
            empty.className = 'sc-empty';
            empty.textContent = messages.gridEmpty;
            host.appendChild(empty);
            return;
        }

        rows.forEach(function (row) {
            const card = document.createElement('div');
            card.className = 'sc-card' + (row.id === state.selectedId ? ' sc-selected-row' : '');

            const name = row.sourceName || row.targetName || '';
            const dim = row.sourceName ? '' : ' dim';

            card.innerHTML = '<span class="sc-card-check">'
                + '<input type="checkbox" class="form-check-input sc-row-check"'
                + (row.included ? ' checked' : '') + (row.canToggle ? '' : ' disabled') + ' /></span>'
                + '<span class="sc-card-body">'
                + '<span class="sc-card-name' + dim + '">' + escapeHtml(name) + '</span>'
                + '<span class="sc-card-meta">' + statusPill(row.action)
                + '<span>' + escapeHtml(row.objectType || '') + '</span></span>'
                + '<button type="button" class="sc-card-open">' + escapeHtml(messages.showDetail) + '</button>'
                + '</span>';

            card.querySelector('.sc-row-check').addEventListener('click', function (event) {
                event.stopPropagation();
                if (row.canToggle) {
                    toggleRows([row.id], !row.included);
                }
            });

            card.addEventListener('click', function () {
                showDetailPane();
                loadDetail(row.id);
            });

            host.appendChild(card);
        });
    }

    // Same filter as the desktop side column, drawn as a horizontal strip for narrow screens.
    function renderTypeChips() {
        const host = root.querySelector('[data-role="type-chips"]');
        const stats = state.stats;
        if (!host) {
            return;
        }

        host.innerHTML = '';

        function chip(labelText, count, isActive, onPick) {
            const button = document.createElement('button');
            button.type = 'button';
            button.className = 'sc-type-chip' + (isActive ? ' active' : '');
            button.innerHTML = '<span>' + escapeHtml(labelText) + '</span>'
                + '<span class="count">' + count + '</span>';
            button.addEventListener('click', onPick);
            host.appendChild(button);
        }

        chip(messages.allTypes, stats ? stats.total : 0, state.typeFilter === null, function () {
            state.typeFilter = null;
            renderTypeList();
            renderTypeChips();
            renderGrid();
        });

        (stats ? stats.byType : []).forEach(function (entry) {
            chip(entry.objectType, entry.total, state.typeFilter === entry.objectType, function () {
                state.typeFilter = state.typeFilter === entry.objectType ? null : entry.objectType;
                renderTypeList();
                renderTypeChips();
                renderGrid();
            });
        });
    }

    // On a phone, picking an object and reading its diff are two steps rather than one
    // crowded screen; the class is what the stylesheet keys off.
    function showDetailPane() {
        const main = root.querySelector('.sc-result-main');
        if (main) {
            main.classList.add('sc-showing-detail');
        }
    }

    function hideDetailPane() {
        const main = root.querySelector('.sc-result-main');
        if (main) {
            main.classList.remove('sc-showing-detail');
        }
    }

    function markSelectedRow() {
        if (!state.grid) {
            return;
        }

        state.grid.getRows('active').forEach(function (row) {
            row.getElement().classList.toggle('sc-selected-row', row.getData().id === state.selectedId);
        });
    }

    function bindDetailBack() {
        const back = root.querySelector('[data-role="detail-back"]');
        if (back) {
            back.addEventListener('click', hideDetailPane);
        }
    }

    function headerCheckFormatter() {
        const checkbox = document.createElement('input');
        checkbox.type = 'checkbox';
        checkbox.className = 'form-check-input sc-header-check';
        checkbox.setAttribute('aria-label', document.getElementById('sc-check-all').textContent.trim());
        checkbox.addEventListener('click', function (event) {
            event.stopPropagation();
            if (state.togglePending) {
                return;
            }

            const rows = visibleRows().filter(function (row) { return row.canToggle; });
            const include = !rows.length || !rows.every(function (row) { return row.included; });
            const ids = rows.filter(function (row) { return row.included !== include; })
                .map(function (row) { return row.id; });
            if (ids.length) {
                toggleRows(ids, include);
            }
        });

        window.setTimeout(syncHeaderCheck, 0);
        return checkbox;
    }

    function renderGrid(preserveScroll) {
        renderCardList();

        if (state.grid) {
            const holder = document.querySelector('#sc-grid .tabulator-tableholder');
            const scrollTop = preserveScroll && holder ? holder.scrollTop : 0;
            const replaced = state.grid.replaceData(visibleRows());
            Promise.resolve(replaced).then(function () {
                if (preserveScroll && holder) {
                    holder.scrollTop = scrollTop;
                }
                syncHeaderCheck();
                markSelectedRow();
            });
            return;
        }

        state.grid = new Tabulator('#sc-grid', {
            data: visibleRows(),
            layout: 'fitColumns',
            responsiveLayout: 'hide',
            // maxHeight rather than height: with a handful of rows the table hugs them
            // instead of leaving half a screen of empty grid under the last one.
            maxHeight: '52vh',
            rowHeight: 40,
            resizableColumns: false,
            placeholder: messages.gridEmpty,
            index: 'id',
            columns: [
                {
                    title: '', titleFormatter: headerCheckFormatter, field: 'included', width: 46,
                    hozAlign: 'center', headerHozAlign: 'center', resizable: false,
                    headerSort: false,
                    formatter: function (cell) {
                        const row = cell.getRow().getData();
                        const disabled = row.canToggle ? '' : ' disabled';
                        const checked = cell.getValue() ? ' checked' : '';
                        const title = row.destructive
                            ? ' title="' + escapeHtml(messages.destructiveDefault) + '"'
                            : '';
                        return '<input type="checkbox" class="form-check-input sc-row-check"'
                            + checked + disabled + title + ' />';
                    },
                    cellClick: function (event, cell) {
                        if (event.target && event.target.classList.contains('sc-row-check')) {
                            const row = cell.getRow().getData();
                            if (row.canToggle) {
                                toggleRows([row.id], !row.included);
                            }
                            event.stopPropagation();
                        }
                    }
                },
                {
                    title: config.columns.objectType, field: 'objectType', width: 178,
                    hozAlign: 'center', headerHozAlign: 'center', resizable: false,
                    // The object-type filter beside the table already says this.
                    responsive: 3
                },
                {
                    title: config.columns.sourceName, field: 'sourceName',
                    hozAlign: 'left', headerHozAlign: 'center', minWidth: 180,
                    widthGrow: 4, resizable: false, responsive: 0
                },
                {
                    title: config.columns.status, field: 'action', width: 168,
                    hozAlign: 'center', headerHozAlign: 'center', resizable: false,
                    formatter: function (cell) {
                        return statusPill(cell.getValue());
                    }
                },
                {
                    title: config.columns.targetName, field: 'targetName',
                    hozAlign: 'left', headerHozAlign: 'center', minWidth: 180,
                    widthGrow: 4, resizable: false, responsive: 0
                },
                {
                    title: config.columns.changes, field: 'childCount', width: 132,
                    hozAlign: 'center', headerHozAlign: 'center', resizable: false,
                    // Repeated in the detail pane when a row is opened.
                    responsive: 4
                }
            ]
        });

        state.grid.on('renderComplete', function () {
            syncHeaderCheck();
            markSelectedRow();
        });

        state.grid.on('rowClick', function (event, row) {
            if (event.target && event.target.classList.contains('sc-row-check')) {
                return;
            }

            loadDetail(row.getData().id);
        });
    }

    async function toggleRows(ids, include) {
        const status = root.querySelector('[data-role="compare-status"]');

        if (state.togglePending || !ids.length) {
            return;
        }

        state.togglePending = true;
        syncHeaderCheck();
        setBulkBusy(true);
        setStatus(status, messages.toggleWorking.replace('{0}', ids.length));

        try {
            const response = await callJson(config.urls.toggle, {
                comparisonId: state.comparisonId,
                ids: ids,
                include: include
            });

            if (!response.success) {
                setStatus(status, response.message || messages.genericError, 'error');
                return;
            }

            state.rows = response.data.rows || [];
            state.stats = response.data.stats;
            state.scriptDirty = true;
            renderTypeList();
            renderFilterCounts();
            renderMappedNote();
            renderGrid(true);

            const blocked = response.data.blocked || [];
            setStatus(
                status,
                blocked.length
                    ? messages.blocked.replace('{0}', blocked.join(', '))
                    : messages.toggleDone.replace('{0}', ids.length),
                blocked.length ? 'error' : 'ok');
        } catch (error) {
            setStatus(status, messages.networkError, 'error');
        } finally {
            state.togglePending = false;
            setBulkBusy(false);
            syncHeaderCheck();
        }
    }

    // Ticking a thousand rows is one request, but it is not instant; the buttons that started
    // it say so instead of staying live and inviting a second click.
    function setBulkBusy(busy) {
        ['sc-check-all', 'sc-uncheck-all'].forEach(function (id) {
            const button = document.getElementById(id);
            if (button) {
                button.disabled = busy;
            }
        });
    }

    // ---------------------------------------------------------------- detail

    async function loadDetail(id) {
        state.selectedId = id;
        markSelectedRow();
        const body = root.querySelector('[data-role="detail-body"]');
        body.innerHTML = '<p class="sc-empty">' + escapeHtml(messages.comparing) + '</p>';

        try {
            const response = await callJson(config.urls.detail
                + '?comparisonId=' + encodeURIComponent(state.comparisonId)
                + '&differenceId=' + encodeURIComponent(id));

            if (!response.success) {
                body.innerHTML = '<p class="sc-empty">' + escapeHtml(response.message || messages.genericError) + '</p>';
                return;
            }

            renderDetail(response.data);
        } catch (error) {
            body.innerHTML = '<p class="sc-empty">' + escapeHtml(messages.networkError) + '</p>';
        }
    }

    const CONTEXT_LINES = 3;
    const FOLD_THRESHOLD = 8;

    function isChanged(line) {
        return line.state !== 'Same';
    }

    function setDetailControls(visible) {
        ['only-changes-wrap'].forEach(function (role) {
            const node = root.querySelector('[data-role="' + role + '"]');
            if (node) {
                node.hidden = !visible;
            }
        });
        ['sc-prev-change', 'sc-next-change', 'sc-expand-detail'].forEach(function (id) {
            document.getElementById(id).hidden = !visible;
        });
    }

    function renderDetail(detail) {
        state.detail = detail;
        state.expandedFolds = new Set();

        const title = root.querySelector('[data-role="detail-title"]');
        const summary = root.querySelector('[data-role="detail-summary"]');
        const body = root.querySelector('[data-role="detail-body"]');

        if (!detail) {
            title.textContent = messages.detailHint;
            summary.textContent = '';
            body.innerHTML = '<p class="sc-empty">' + escapeHtml(messages.detailHint) + '</p>';
            setDetailControls(false);
            return;
        }

        title.textContent = detail.title;
        summary.textContent = detail.changedLines + ' / ' + detail.lines.length;

        if (!detail.lines.length) {
            body.innerHTML = '<p class="sc-empty">' + escapeHtml(messages.detailEmpty) + '</p>';
            setDetailControls(false);
            return;
        }

        setDetailControls(true);
        paintDiff();
    }

    // Builds the diff table, folding long runs of identical lines into one clickable row so a
    // 1400-line procedure with a handful of changes stays readable.
    function paintDiff() {
        const detail = state.detail;
        const body = root.querySelector('[data-role="detail-body"]');
        const lines = detail.lines;
        const collapse = state.onlyChanges;

        const rows = [];
        let i = 0;

        while (i < lines.length) {
            if (isChanged(lines[i])) {
                rows.push(lineRow(lines[i]));
                i++;
                continue;
            }

            // A run of identical lines.
            let runEnd = i;
            while (runEnd < lines.length && !isChanged(lines[runEnd])) {
                runEnd++;
            }

            const runLength = runEnd - i;
            const foldKey = i;
            const nearStart = i === 0;
            const nearEnd = runEnd === lines.length;
            const keep = CONTEXT_LINES;

            if (!collapse || state.expandedFolds.has(foldKey) || runLength <= FOLD_THRESHOLD) {
                for (let j = i; j < runEnd; j++) {
                    rows.push(lineRow(lines[j]));
                }
            } else {
                const head = nearStart ? 0 : keep;
                const tail = nearEnd ? 0 : keep;
                for (let j = i; j < i + head; j++) {
                    rows.push(lineRow(lines[j]));
                }
                rows.push(foldRow(foldKey, runLength - head - tail));
                for (let j = runEnd - tail; j < runEnd; j++) {
                    rows.push(lineRow(lines[j]));
                }
            }

            i = runEnd;
        }

        body.innerHTML = '<table class="sc-diff-table">'
            + '<colgroup><col class="num" /><col /><col class="num" /><col /></colgroup>'
            + '<thead><tr>'
            + '<th>#</th><th>' + escapeHtml(config.columns.sourceName) + '</th>'
            + '<th>#</th><th>' + escapeHtml(config.columns.targetName) + '</th>'
            + '</tr></thead><tbody>' + rows.join('') + '</tbody></table>';

        body.querySelectorAll('tr.sc-fold').forEach(function (row) {
            row.addEventListener('click', function () {
                state.expandedFolds.add(parseInt(row.dataset.fold, 10));
                paintDiff();
            });
        });
    }

    function lineRow(line) {
        return '<tr class="' + line.state + '">'
            + '<td class="num">' + (line.sourceLine === null ? '' : line.sourceLine) + '</td>'
            + '<td class="src">' + escapeHtml(line.sourceText) + '</td>'
            + '<td class="num">' + (line.targetLine === null ? '' : line.targetLine) + '</td>'
            + '<td class="dst">' + escapeHtml(line.targetText) + '</td>'
            + '</tr>';
    }

    function foldRow(key, count) {
        return '<tr class="sc-fold" data-fold="' + key + '"><td colspan="4">'
            + escapeHtml(messages.collapsedLines.replace('{0}', count)) + '</td></tr>';
    }

    // Scrolls the detail body to the next or previous changed row from the current position.
    function jumpToChange(direction) {
        const body = root.querySelector('[data-role="detail-body"]');
        const changed = Array.from(body.querySelectorAll('tr.Changed, tr.OnlyInSource, tr.OnlyInTarget'));
        if (!changed.length) {
            return;
        }

        const bodyTop = body.getBoundingClientRect().top;
        // Offset of each row within the scrollable body, independent of positioning context.
        const offsets = changed.map(function (row) {
            return row.getBoundingClientRect().top - bodyTop + body.scrollTop;
        });

        const viewTop = body.scrollTop;
        let target = null;

        if (direction > 0) {
            const idx = offsets.findIndex(function (top) { return top > viewTop + 4; });
            target = idx >= 0 ? offsets[idx] : offsets[0];
        } else {
            const candidates = offsets.filter(function (top) { return top < viewTop - 4; });
            target = candidates.length ? candidates[candidates.length - 1] : offsets[offsets.length - 1];
        }

        body.scrollTo({ top: Math.max(0, target - 40), behavior: 'smooth' });
    }

    // ---------------------------------------------------------------- script

    async function loadScript(force) {
        if (!state.comparisonId || (!state.scriptDirty && !force)) {
            return;
        }

        const status = root.querySelector('[data-role="script-status"]');
        setStatus(status, messages.comparing);

        try {
            const response = await callJson(config.urls.script + '?comparisonId=' + encodeURIComponent(state.comparisonId));
            if (!response.success) {
                setStatus(status, response.message || messages.genericError, 'error');
                return;
            }

            document.getElementById('sc-script').value = response.data.script || '';
            document.getElementById('sc-download-script').href =
                config.urls.download + '?comparisonId=' + encodeURIComponent(state.comparisonId);
            renderScriptWarnings(response.data.warnings);
            state.scriptDirty = false;

            setStatus(status, messages.scriptSelection
                .replace('{0}', response.data.includedCount)
                .replace('{1}', response.data.excludedCount), 'ok');
        } catch (error) {
            setStatus(status, messages.networkError, 'error');
        }
    }

    function showTab(buttonId) {
        const button = document.getElementById(buttonId);
        if (button && window.bootstrap && window.bootstrap.Tab) {
            window.bootstrap.Tab.getOrCreateInstance(button).show();
        }
    }

    // ----------------------------------------------------------------- wiring

    root.querySelectorAll('.sc-endpoint').forEach(initDatabasePicker);
    restoreConnections();

    root.querySelectorAll('.sc-endpoint').forEach(function (panel) {
        panel.querySelectorAll('[data-field]').forEach(function (node) {
            node.addEventListener(node.type === 'checkbox' ? 'change' : 'input', persistConnections);
        });
        panel.querySelector('[data-action="test"]').addEventListener('click', function () {
            testConnection(panel);
        });
    });

    bindDetailBack();
    ['sc-code-from', 'sc-code-to', 'sc-mapping-input'].forEach(function (id) {
        document.getElementById(id).addEventListener('input', renderCodePreview);
    });
    root.querySelectorAll('[data-side] [data-field="database"]').forEach(function (node) {
        node.addEventListener('change', renderCodePreview);
    });
    renderCodePreview();

    document.getElementById('sc-compare').addEventListener('click', startCompare);
    document.getElementById('sc-cancel').addEventListener('click', cancelCompare);

    document.getElementById('sc-toggle-options').addEventListener('click', function () {
        const options = document.getElementById('sc-options');
        options.hidden = !options.hidden;
        this.setAttribute('aria-expanded', options.hidden ? 'false' : 'true');
    });

    document.getElementById('sc-mapping-auto').addEventListener('click', autoFillMappings);

    document.getElementById('sc-toggle-mapped').addEventListener('click', function () {
        state.showMapped = !state.showMapped;
        renderMappedNote();
        renderGrid();
    });

    document.getElementById('sc-only-changes').addEventListener('change', function () {
        state.onlyChanges = this.checked;
        if (state.detail) {
            paintDiff();
        }
    });

    document.getElementById('sc-prev-change').addEventListener('click', function () {
        jumpToChange(-1);
    });

    document.getElementById('sc-next-change').addEventListener('click', function () {
        jumpToChange(1);
    });

    document.getElementById('sc-expand-detail').addEventListener('click', function () {
        state.detailExpanded = !state.detailExpanded;
        root.querySelector('[data-role="detail"]').classList.toggle('expanded', state.detailExpanded);
        this.textContent = state.detailExpanded ? messages.collapse : messages.expand;
    });

    document.getElementById('sc-search').addEventListener('input', renderGrid);

    root.querySelectorAll('.sc-filter').forEach(function (button) {
        button.addEventListener('click', function () {
            state.actionFilter = button.dataset.filter;
            root.querySelectorAll('.sc-filter').forEach(function (other) {
                other.classList.toggle('active', other === button);
            });
            renderGrid();
        });
    });

    document.getElementById('sc-check-all').addEventListener('click', function () {
        const ids = visibleRows().filter(function (row) { return row.canToggle && !row.included; })
            .map(function (row) { return row.id; });
        if (ids.length) {
            toggleRows(ids, true);
        }
    });

    document.getElementById('sc-uncheck-all').addEventListener('click', function () {
        const ids = visibleRows().filter(function (row) { return row.canToggle && row.included; })
            .map(function (row) { return row.id; });
        if (ids.length) {
            toggleRows(ids, false);
        }
    });

    document.getElementById('sc-refresh-script').addEventListener('click', function () {
        loadScript(true);
    });

    document.getElementById('sc-tab-script').addEventListener('shown.bs.tab', function () {
        loadScript(false);
    });

    document.getElementById('sc-copy-script').addEventListener('click', async function () {
        const status = root.querySelector('[data-role="script-status"]');
        try {
            await navigator.clipboard.writeText(document.getElementById('sc-script').value);
            setStatus(status, messages.copied, 'ok');
        } catch (error) {
            setStatus(status, messages.copyFailed, 'error');
        }
    });
})();
