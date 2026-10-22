// Schema compare (Views/SchemaCompare/Index.cshtml). Three steps: two connections, the
// options (remembered in the browser), then the run with its progress, result and script.
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
    const OPTIONS_STORAGE_KEY = 'toolbox.schemaCompare.options.v1';

    const state = {
        comparisonId: null,
        running: false,
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
        script: null,
        dropCursor: -1,
        showMapped: false,
        detail: null,
        expandedFolds: new Set(),
        onlyChanges: true,
        detailExpanded: false,
        togglePending: false,
        // Logins that passed "Test connection" in this page view. Compare stays locked until
        // both sides match one of them; editing a field after the test locks it again.
        tested: new Set()
    };

    const SYMBOLS = { Add: '+', Change: '≠', Delete: '−' };

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

    function label(map, code) {
        return (map && map[code]) || code;
    }

    function icon(name, size) {
        return window.Toolbox && window.Toolbox.icon ? window.Toolbox.icon(name, size) : '';
    }

    function formatNumber(value) {
        return window.Toolbox && window.Toolbox.format ? window.Toolbox.format.number(value, 0) : String(value);
    }

    function formatClock(ms) {
        const total = Math.floor(ms / 1000);
        const h = String(Math.floor(total / 3600)).padStart(2, '0');
        const m = String(Math.floor((total % 3600) / 60)).padStart(2, '0');
        const s = String(total % 60).padStart(2, '0');
        return h + ':' + m + ':' + s;
    }

    // The shared states (components.css): empty, error and loading look the same on every screen.
    function emptyHtml(iconName, text) {
        return '<div class="tbx-empty sc-state" role="status">'
            + '<span class="tbx-empty-icon">' + icon(iconName, 20) + '</span>'
            + '<p class="tbx-empty-text">' + escapeHtml(text) + '</p></div>';
    }

    function errorHtml(text) {
        return '<div class="tbx-error-state" role="alert">' + icon('circle-alert')
            + '<span>' + escapeHtml(text) + '</span></div>';
    }

    function loadingHtml(text, lines) {
        let bars = '';
        for (let i = 0; i < (lines || 3); i++) {
            bars += '<span class="tbx-skeleton" style="width: ' + (90 - i * 17) + '%"></span>';
        }
        return '<div class="sc-loading" role="status"><span class="visually-hidden">' + escapeHtml(text) + '</span>' + bars + '</div>';
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
            // still work; the user will simply have to enter the values again next time.
        }
    }

    // ----------------------------------------------------------- connections

    // Source and target forms: Views/Shared/_SqlConnectionForm.cshtml, Content/js/sql-connection.js.
    const connections = {};
    const SIDES = ['source', 'target'];
    const FIELDS = ['server', 'username', 'password', 'database', 'trustServerCertificate'];

    function readEndpoint(side) {
        return connections[side].read();
    }

    function loginSignature(endpoint) {
        return [endpoint.server.toLowerCase(), endpoint.username.toLowerCase(), endpoint.password, endpoint.trustServerCertificate].join('\u0001');
    }

    function restoreConnections() {
        const saved = readStoredJson(window.localStorage, CONNECTION_STORAGE_KEY) || {};
        const secrets = readStoredJson(window.sessionStorage, CONNECTION_SECRET_KEY) || {};

        SIDES.forEach(function (side) {
            const endpoint = saved[side] || {};
            ['server', 'database', 'username', 'trustServerCertificate'].forEach(function (field) {
                connections[side].set(field, endpoint[field]);
            });
            connections[side].set('password', secrets[side]);
        });
    }

    function persistConnections() {
        const source = readEndpoint('source');
        const target = readEndpoint('target');
        const saved = function (endpoint) {
            return {
                server: endpoint.server,
                database: endpoint.database,
                username: endpoint.username,
                trustServerCertificate: endpoint.trustServerCertificate
            };
        };

        // Non-secret values survive browser restarts. Passwords deliberately live only in the
        // current browser session so they are not written as clear text to persistent storage.
        writeStoredJson(window.localStorage, CONNECTION_STORAGE_KEY, { source: saved(source), target: saved(target) });
        writeStoredJson(window.sessionStorage, CONNECTION_SECRET_KEY, { source: source.password, target: target.password });
    }

    // What still stands between the user and the Compare button, in the order they would fix it.
    function readiness() {
        const source = readEndpoint('source');
        const target = readEndpoint('target');
        const missing = [];

        if (!state.tested.has(loginSignature(source))) {
            missing.push(messages.readyTestSource);
        } else if (!source.database) {
            missing.push(messages.readyChooseSource);
        }

        if (!state.tested.has(loginSignature(target))) {
            missing.push(messages.readyTestTarget);
        } else if (!target.database) {
            missing.push(messages.readyChooseTarget);
        }

        const sameDatabase = source.server && source.database
            && source.server.toLowerCase() === target.server.toLowerCase()
            && source.database.toLowerCase() === target.database.toLowerCase();

        return { ok: missing.length === 0 && !sameDatabase, missing: missing, sameDatabase: sameDatabase };
    }

    function updateReadiness() {
        const ready = readiness();
        const node = root.querySelector('[data-role="ready"]');
        document.getElementById('sc-compare').disabled = state.running || !ready.ok;

        if (state.running) {
            setStatus(node, '');
        } else if (ready.sameDatabase) {
            setStatus(node, messages.readySameDatabase, 'warn');
        } else if (ready.missing.length) {
            setStatus(node, messages.readyMissing.replace('{0}', ready.missing.join(', ')));
        } else {
            setStatus(node, '');
        }
    }

    // Swaps every field, the database lists and the test results, so a comparison the wrong
    // way round is one click instead of retyping two forms.
    function swapSides() {
        const a = connections.source;
        const b = connections.target;
        const va = a.read();
        const vb = b.read();
        const listA = a.databases();
        const listB = b.databases();
        const statusA = a.status();
        const statusB = b.status();

        a.setDatabases(listB);
        b.setDatabases(listA);
        FIELDS.forEach(function (field) {
            a.set(field, vb[field]);
            b.set(field, va[field]);
        });
        a.setStatus(statusB.text, statusB.tone);
        b.setStatus(statusA.text, statusA.tone);

        // The name mapping rewrites source names into target names, so it turns round too.
        // A line with several targets (A=B|C) has no single reverse and stays as written.
        const from = document.getElementById('sc-code-from');
        const to = document.getElementById('sc-code-to');
        const code = from.value;
        from.value = to.value;
        to.value = code;
        const mapping = document.getElementById('sc-mapping-input');
        mapping.value = mapping.value.split('\n').map(function (line) {
            const eq = line.indexOf('=');
            if (eq <= 0 || line.indexOf('|') >= 0) {
                return line;
            }
            return line.slice(eq + 1).trim() + '=' + line.slice(0, eq).trim();
        }).join('\n');

        persistConnections();
        saveOptions();
        renderCodePreview();
        updateReadiness();
        setStatus(root.querySelector('[data-role="compare-status"]'), messages.swapped);
    }

    // --------------------------------------------------------------- options

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

    function saveOptions() {
        writeStoredJson(window.localStorage, OPTIONS_STORAGE_KEY, {
            scope: readScope(),
            options: readOptions(),
            sourceCode: document.getElementById('sc-code-from').value,
            targetCode: document.getElementById('sc-code-to').value,
            mappings: document.getElementById('sc-mapping-input').value,
            advancedOpen: !document.getElementById('sc-options').hidden
        });
    }

    function restoreOptions() {
        const saved = readStoredJson(window.localStorage, OPTIONS_STORAGE_KEY);
        if (!saved) {
            return;
        }

        root.querySelectorAll('[data-scope]').forEach(function (node) {
            if (saved.scope && typeof saved.scope[node.dataset.scope] === 'boolean') {
                node.checked = saved.scope[node.dataset.scope];
            }
        });
        root.querySelectorAll('[data-option]').forEach(function (node) {
            if (saved.options && typeof saved.options[node.dataset.option] === 'boolean') {
                node.checked = saved.options[node.dataset.option];
            }
        });
        document.getElementById('sc-code-from').value = saved.sourceCode || '';
        document.getElementById('sc-code-to').value = saved.targetCode || '';
        document.getElementById('sc-mapping-input').value = saved.mappings || '';
        setAdvancedOpen(Boolean(saved.advancedOpen));
    }

    // Back to the shipped defaults for scope and noise; the name mapping belongs to the
    // databases in play, so it is left alone.
    function resetOptions() {
        root.querySelectorAll('[data-scope], [data-option]').forEach(function (node) {
            node.checked = node.dataset.default === 'true';
        });
        saveOptions();
        if (window.Toolbox && window.Toolbox.notify) {
            window.Toolbox.notify(messages.optionsReset, 'success');
        }
    }

    function setAdvancedOpen(open) {
        document.getElementById('sc-options').hidden = !open;
        document.getElementById('sc-toggle-options').setAttribute('aria-expanded', open ? 'true' : 'false');
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

    function readCodes() {
        return {
            sourceCode: document.getElementById('sc-code-from').value.trim(),
            targetCode: document.getElementById('sc-code-to').value.trim()
        };
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
        const sourceDb = readEndpoint('source').database;
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
        const source = readEndpoint('source');
        const target = readEndpoint('target');
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
        saveOptions();
        setStatus(status, messages.mappingAutoFilled);
    }

    // --------------------------------------------------------------- compare

    function setRunning(running) {
        state.running = running;
        const button = document.getElementById('sc-compare');
        button.classList.toggle('is-running', running);
        button.setAttribute('aria-busy', running ? 'true' : 'false');
        document.getElementById('sc-swap').disabled = running;
        updateReadiness();
    }

    function showCompareError(text) {
        const box = root.querySelector('[data-role="compare-error"]');
        box.hidden = !text;
        root.querySelector('[data-role="compare-error-text"]').textContent = text || '';
    }

    async function startCompare() {
        const status = root.querySelector('[data-role="compare-status"]');
        const source = readEndpoint('source');
        const target = readEndpoint('target');
        const codes = readCodes();

        if (!readiness().ok) {
            updateReadiness();
            return;
        }

        persistConnections();
        connections.source.remember();
        connections.target.remember();
        showCompareError('');
        setRunning(true);
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
                finishCompare(started.message || messages.genericError, true);
                return;
            }

            state.comparisonId = started.data.comparisonId;
            document.getElementById('sc-cancel').hidden = false;
            root.querySelector('[data-role="results-empty"]').hidden = true;
            document.getElementById('sc-results').hidden = false;
            showTab('sc-tab-progress');
            startClock();
            poll();
        } catch (error) {
            finishCompare(messages.networkError, true);
        }
    }

    function resetResults() {
        state.rows = [];
        state.stats = null;
        state.selectedId = null;
        state.typeFilter = null;
        state.actionFilter = 'all';
        state.scriptDirty = true;
        state.script = null;
        state.showMapped = false;
        syncFilterButtons();
        root.querySelector('[data-role="mapped-note"]').hidden = true;
        ['progress-source', 'progress-target'].forEach(function (role) {
            const body = root.querySelector('[data-role="' + role + '"]');
            body.innerHTML = '';
            delete body.dataset.signature;
        });
        root.querySelector('[data-role="type-list"]').innerHTML = '';
        root.querySelector('[data-role="type-chips"]').innerHTML = '';
        root.querySelector('[data-role="card-list"]').innerHTML = '';
        root.querySelector('[data-role="result-equal"]').hidden = true;
        root.querySelector('[data-role="result-layout"]').hidden = false;
        root.querySelector('[data-role="quick-summary"]').hidden = true;
        hideDetailPane();
        renderScript(null);
        renderDetail(null);
        if (state.grid) {
            state.grid.setData([]);
        }
    }

    function startClock() {
        const clock = document.getElementById('sc-clock');
        state.startedAt = Date.now();
        stopClock();
        clock.hidden = false;
        clock.textContent = formatClock(0);
        state.clockTimer = setInterval(function () {
            clock.textContent = formatClock(Date.now() - state.startedAt);
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

            finishCompare('', false);
            applyResult(progress.result);
        } catch (error) {
            finishCompare(messages.networkError, true);
        }
    }

    function finishCompare(message, isError) {
        stopClock();
        const cancel = document.getElementById('sc-cancel');
        cancel.hidden = true;
        cancel.disabled = false;
        setRunning(false);
        const status = root.querySelector('[data-role="compare-status"]');
        if (isError) {
            setStatus(status, '');
            showCompareError(message);
        } else {
            setStatus(status, message);
        }
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
        // The last line changes state (running, then done or failed) without a new line coming in.
        const last = lines.length ? lines[lines.length - 1] : null;
        const signature = lines.length + '|' + (last ? last.state : '');
        if (!body || body.dataset.signature === signature) {
            return;
        }

        body.dataset.signature = signature;
        body.innerHTML = lines.map(function (line) {
            return '<tr><td class="time">' + escapeHtml(line.time) + '</td>'
                + '<td>' + escapeHtml(line.operation) + '</td>'
                + '<td class="state ' + escapeHtml(line.state) + '">' + escapeHtml(label(config.progressStates, line.state)) + '</td></tr>';
        }).join('');

        const scroller = body.closest('.sc-table-scroll');
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

        setStatus(root.querySelector('[data-role="compare-status"]'), messages.summary
            .replace('{0}', result.sourceLabel)
            .replace('{1}', result.targetLabel));

        const realCount = state.rows.filter(function (row) { return !row.mappedSame; }).length;
        const equal = result.isEqual || realCount === 0;
        root.querySelector('[data-role="result-equal"]').hidden = !equal;
        root.querySelector('[data-role="result-layout"]').hidden = equal && !(state.stats && state.stats.mappedSame);

        renderTypeList();
        renderFilterCounts();
        renderMappedNote();
        renderGrid();
        renderDetail(null);
        showTab('sc-tab-result');

        if (equal) {
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
            messages.mappedSame.replace('{0}', formatNumber(count));
        document.getElementById('sc-toggle-mapped').textContent =
            state.showMapped ? messages.hideMapped : messages.showMapped;
    }

    function pickType(objectType) {
        state.typeFilter = objectType;
        renderTypeList();
        renderGrid();
    }

    // The same filter twice: a side column on desktop and a chip strip below 1024px.
    function renderTypeList() {
        const stats = state.stats;
        const entries = [{ type: null, text: messages.allTypes, count: stats ? stats.total : 0 }]
            .concat((stats ? stats.byType : []).map(function (entry) {
                return { type: entry.objectType, text: entry.objectType, count: entry.total };
            }));

        const list = root.querySelector('[data-role="type-list"]');
        const chips = root.querySelector('[data-role="type-chips"]');
        list.innerHTML = '';
        chips.innerHTML = '';

        entries.forEach(function (entry) {
            const active = state.typeFilter === entry.type;
            const pick = function () {
                pickType(entry.type === null || state.typeFilter === entry.type ? null : entry.type);
            };

            const item = document.createElement('li');
            const button = document.createElement('button');
            button.type = 'button';
            button.className = 'sc-type-item' + (active ? ' is-active' : '');
            button.setAttribute('aria-pressed', active ? 'true' : 'false');
            button.innerHTML = '<span>' + escapeHtml(entry.text) + '</span><span class="tbx-chip-count">' + formatNumber(entry.count) + '</span>';
            button.addEventListener('click', pick);
            item.appendChild(button);
            list.appendChild(item);

            const chip = document.createElement('button');
            chip.type = 'button';
            chip.className = 'tbx-chip';
            chip.setAttribute('aria-pressed', active ? 'true' : 'false');
            chip.innerHTML = '<span>' + escapeHtml(entry.text) + '</span><span class="tbx-chip-count">' + formatNumber(entry.count) + '</span>';
            chip.addEventListener('click', pick);
            chips.appendChild(chip);
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
                node.textContent = formatNumber(counts[key]);
            });
        });

        root.querySelector('[data-role="quick-summary"]').hidden = !stats;
    }

    function syncFilterButtons() {
        root.querySelectorAll('.sc-filter').forEach(function (button) {
            button.setAttribute('aria-pressed', button.dataset.filter === state.actionFilter ? 'true' : 'false');
        });
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
        return '<span class="sc-status ' + escapeHtml(action) + '" title="' + escapeHtml(text) + '">'
            + '<span aria-hidden="true">' + escapeHtml(SYMBOLS[action] || '?') + '</span>'
            + '<span>' + escapeHtml(text) + '</span></span>';
    }

    function emptyGridElement() {
        const holder = document.createElement('div');
        holder.innerHTML = emptyHtml('search', messages.gridEmpty);
        return holder.firstElementChild;
    }

    // Under 600px the table is unreadable, so the same rows are drawn as cards. Both are in
    // the DOM and CSS decides which one is on screen; they share visibleRows() so they can
    // never disagree.
    function renderCardList() {
        const host = root.querySelector('[data-role="card-list"]');
        const rows = visibleRows();
        host.innerHTML = '';

        if (!rows.length) {
            host.innerHTML = emptyHtml('search', messages.gridEmpty);
            return;
        }

        rows.forEach(function (row) {
            const card = document.createElement('div');
            card.className = 'sc-card' + (row.id === state.selectedId ? ' sc-selected-row' : '');
            card.dataset.id = row.id;

            const name = row.sourceName || row.targetName || '';
            const dim = row.sourceName ? '' : ' dim';

            card.innerHTML = '<span class="sc-card-check">'
                + '<input type="checkbox" class="form-check-input sc-row-check" aria-label="' + escapeHtml(name) + '"'
                + (row.included ? ' checked' : '') + (row.canToggle ? '' : ' disabled') + ' /></span>'
                + '<span class="sc-card-body">'
                + '<span class="sc-card-name' + dim + '">' + escapeHtml(name) + '</span>'
                + '<span class="sc-card-meta">' + statusPill(row.action)
                + '<span>' + escapeHtml(row.objectType || '') + '</span></span>'
                + '<button type="button" class="btn btn-link btn-sm sc-card-open">' + escapeHtml(messages.showDetail) + '</button>'
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

    // On a phone, picking an object and reading its diff are two steps rather than one
    // crowded screen; the class is what the stylesheet keys off.
    function showDetailPane() {
        root.querySelector('.sc-result-main').classList.add('sc-showing-detail');
    }

    function hideDetailPane() {
        root.querySelector('.sc-result-main').classList.remove('sc-showing-detail');
    }

    function markSelectedRow() {
        if (state.grid) {
            state.grid.getRows('active').forEach(function (row) {
                row.getElement().classList.toggle('sc-selected-row', row.getData().id === state.selectedId);
            });
        }

        root.querySelectorAll('.sc-card').forEach(function (card) {
            card.classList.toggle('sc-selected-row', card.dataset.id === state.selectedId);
        });
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
            rowHeight: 36,
            resizableColumns: false,
            placeholder: emptyGridElement(),
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
                            + ' aria-label="' + escapeHtml(row.sourceName || row.targetName) + '"'
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
                    title: config.columns.objectType, field: 'objectType', width: 160,
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
                    title: config.columns.status, field: 'action', width: 160,
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
                    title: config.columns.changes, field: 'childCount', width: 144,
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
        setStatus(status, messages.toggleWorking.replace('{0}', formatNumber(ids.length)));

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
                    : messages.toggleDone.replace('{0}', formatNumber(ids.length)),
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
            document.getElementById(id).disabled = busy;
        });
    }

    // J / K walk the objects in the order they are on screen, the way a code review does.
    function stepRow(direction) {
        const gridVisible = state.grid && document.getElementById('sc-grid').offsetParent !== null;
        const rows = gridVisible
            ? state.grid.getRows('active').map(function (row) { return row.getData(); })
            : visibleRows();
        if (!rows.length) {
            return;
        }

        const index = rows.findIndex(function (row) { return row.id === state.selectedId; });
        const next = index < 0 ? 0 : Math.min(rows.length - 1, Math.max(0, index + direction));
        const id = rows[next].id;

        if (gridVisible) {
            state.grid.scrollToRow(id, 'nearest', false);
        } else {
            const card = root.querySelector('.sc-card[data-id="' + CSS.escape(id) + '"]');
            if (card) {
                card.scrollIntoView({ block: 'nearest' });
            }
        }

        loadDetail(id);
    }

    function isTyping(target) {
        return target instanceof HTMLElement
            && (target.isContentEditable || /^(INPUT|TEXTAREA|SELECT)$/.test(target.tagName));
    }

    // ---------------------------------------------------------------- detail

    async function loadDetail(id) {
        state.selectedId = id;
        markSelectedRow();
        const body = root.querySelector('[data-role="detail-body"]');
        const row = state.rows.find(function (item) { return item.id === id; });
        root.querySelector('[data-role="detail-title"]').textContent = row ? (row.sourceName || row.targetName) : '';
        root.querySelector('[data-role="detail-summary"]').textContent = '';
        setDetailControls(false);
        body.innerHTML = loadingHtml(messages.detailLoading, 6);

        try {
            const response = await callJson(config.urls.detail
                + '?comparisonId=' + encodeURIComponent(state.comparisonId)
                + '&differenceId=' + encodeURIComponent(id));

            // A faster J/K press may have moved on while this was loading.
            if (state.selectedId !== id) {
                return;
            }

            if (!response.success) {
                body.innerHTML = errorHtml(response.message || messages.genericError);
                return;
            }

            renderDetail(response.data);
        } catch (error) {
            if (state.selectedId === id) {
                body.innerHTML = errorHtml(messages.networkError);
            }
        }
    }

    const CONTEXT_LINES = 3;
    const FOLD_THRESHOLD = 8;

    function isChanged(line) {
        return line.state !== 'Same';
    }

    function setDetailControls(visible) {
        root.querySelector('[data-role="only-changes-wrap"]').hidden = !visible;
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
            title.textContent = '';
            summary.textContent = '';
            body.innerHTML = emptyHtml('file-text', messages.detailHint);
            setDetailControls(false);
            return;
        }

        title.textContent = detail.title;
        summary.textContent = formatNumber(detail.changedLines) + ' / ' + formatNumber(detail.lines.length);

        if (!detail.lines.length) {
            body.innerHTML = emptyHtml('file-text', messages.detailEmpty);
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
            + escapeHtml(messages.collapsedLines.replace('{0}', formatNumber(count))) + '</td></tr>';
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

    // Statements that remove a schema object or a column, outside module bodies. Mirrors
    // DacFxScriptPostProcessor on the server: DROP TABLE inside a procedure is application
    // logic (usually a #temp table), not something this script does to the target.
    const GO_LINE = /^\s*GO\s*;?\s*$/i;
    const MODULE_LINE = /^\s*(?:CREATE|ALTER)\s+(?:OR\s+ALTER\s+)?(?:PROC(?:EDURE)?|VIEW|FUNCTION|TRIGGER)\b/i;
    const DROP_LINE = /^\s*DROP\s+(?:TABLE|VIEW|PROC(?:EDURE)?|FUNCTION|TRIGGER|INDEX|SYNONYM|SEQUENCE|TYPE|SCHEMA|USER|ROLE|DEFAULT|RULE|STATISTICS|FULLTEXT\s+INDEX|XML\s+SCHEMA\s+COLLECTION)\b(?!\s+(?:IF\s+EXISTS\s+)?\[?#)/i;
    const DROP_COLUMN_LINE = /^\s*ALTER\s+TABLE\b.*\bDROP\s+COLUMN\b/i;

    function analyseScript(text) {
        const lines = text.split(/\r?\n/);
        const drops = [];
        let start = 0;

        function closeBatch(end) {
            for (let i = start; i < end; i++) {
                if (MODULE_LINE.test(lines[i])) {
                    return;
                }
            }

            for (let i = start; i < end; i++) {
                if (DROP_LINE.test(lines[i]) || DROP_COLUMN_LINE.test(lines[i])) {
                    drops.push(i);
                }
            }
        }

        lines.forEach(function (line, index) {
            if (GO_LINE.test(line)) {
                closeBatch(index);
                start = index + 1;
            }
        });
        closeBatch(lines.length);

        return { lines: lines, drops: drops };
    }

    function setScriptActions(enabled) {
        const download = document.getElementById('sc-download-script');
        download.classList.toggle('disabled', !enabled);
        download.setAttribute('aria-disabled', enabled ? 'false' : 'true');
        download.tabIndex = enabled ? 0 : -1;
        document.getElementById('sc-copy-script').disabled = !enabled;
    }

    function renderWarnings(script) {
        const box = root.querySelector('[data-role="script-warnings"]');
        const list = root.querySelector('[data-role="script-warning-list"]');
        const count = root.querySelector('[data-role="script-drop-count"]');
        const nextDrop = document.getElementById('sc-next-drop');
        const warnings = script ? script.warnings : [];
        const drops = script ? script.drops.length : 0;

        list.innerHTML = warnings.map(function (warning) {
            if (warning.kind === 'Drop') {
                return '<li><code>DROP ' + escapeHtml(warning.objectKind) + '</code> ' + escapeHtml(warning.name) + '</li>';
            }

            const template = warning.kind === 'Rebuild' ? messages.warnRebuild : messages.warnDataLossGuard;
            return '<li>' + escapeHtml(template).replace('{0}', '<code>' + escapeHtml(warning.name) + '</code>') + '</li>';
        }).join('');

        count.textContent = drops ? messages.dropCount.replace('{0}', formatNumber(drops)) : '';
        count.hidden = !drops;
        nextDrop.hidden = !drops;
        list.previousElementSibling.hidden = !warnings.length;
        box.hidden = !drops && !warnings.length;
    }

    // script: null (nothing yet), { loading }, { error }, or the loaded script.
    function renderScript(script) {
        state.script = script && script.text !== undefined ? script : null;
        state.dropCursor = -1;
        const stateNode = root.querySelector('[data-role="script-state"]');
        const pre = document.getElementById('sc-script');
        const code = root.querySelector('[data-role="script-code"]');

        renderWarnings(state.script);
        setScriptActions(Boolean(state.script && state.script.includedCount > 0));

        if (!script) {
            stateNode.innerHTML = '';
            pre.hidden = true;
            code.innerHTML = '';
            return;
        }

        if (script.loading || script.error) {
            stateNode.innerHTML = script.loading ? loadingHtml(messages.scriptLoading, 8) : errorHtml(script.error);
            pre.hidden = true;
            return;
        }

        if (!script.includedCount) {
            stateNode.innerHTML = emptyHtml('file-text', messages.scriptEmpty);
            pre.hidden = true;
            return;
        }

        const dropSet = new Set(script.drops);
        stateNode.innerHTML = '';
        code.innerHTML = script.lines.map(function (line, index) {
            return '<span class="sc-line' + (dropSet.has(index) ? ' is-drop' : '') + '" data-line="' + index + '">'
                + escapeHtml(line) + '\n</span>';
        }).join('');
        pre.hidden = false;
    }

    async function loadScript(force) {
        if (!state.comparisonId || (!state.scriptDirty && !force)) {
            return;
        }

        const status = root.querySelector('[data-role="script-status"]');
        setStatus(status, '');
        renderScript({ loading: true });

        try {
            const response = await callJson(config.urls.script + '?comparisonId=' + encodeURIComponent(state.comparisonId));
            if (!response.success) {
                renderScript({ error: response.message || messages.genericError });
                return;
            }

            const data = response.data;
            const analysed = analyseScript(data.script || '');
            document.getElementById('sc-download-script').href =
                config.urls.download + '?comparisonId=' + encodeURIComponent(state.comparisonId);
            state.scriptDirty = false;
            renderScript({
                text: data.script || '',
                lines: analysed.lines,
                drops: analysed.drops,
                warnings: data.warnings || [],
                includedCount: data.includedCount
            });

            setStatus(status, messages.scriptSelection
                .replace('{0}', formatNumber(data.includedCount))
                .replace('{1}', formatNumber(data.excludedCount)));
        } catch (error) {
            renderScript({ error: messages.networkError });
        }
    }

    function nextDrop() {
        const script = state.script;
        if (!script || !script.drops.length) {
            return;
        }

        state.dropCursor = (state.dropCursor + 1) % script.drops.length;
        const pre = document.getElementById('sc-script');
        const line = pre.querySelector('[data-line="' + script.drops[state.dropCursor] + '"]');
        pre.querySelectorAll('.sc-line.is-current').forEach(function (node) { node.classList.remove('is-current'); });
        if (line) {
            line.classList.add('is-current');
            pre.scrollTop = Math.max(0, line.offsetTop - pre.clientHeight / 3);
        }
    }

    // A script that drops something is shown in full before it leaves the page: the dialog
    // names what will be removed and the user confirms once per download or copy.
    function confirmDrops(okText) {
        const script = state.script;
        if (!script || !script.drops.length || !window.Toolbox || !window.Toolbox.confirm) {
            return Promise.resolve(true);
        }

        const names = script.drops.slice(0, 5).map(function (index) {
            const text = script.lines[index].trim().replace(/;$/, '');
            return text.length > 90 ? text.slice(0, 89) + '…' : text;
        });
        let list = names.join('; ');
        if (script.drops.length > names.length) {
            list += '; ' + messages.confirmMore.replace('{0}', formatNumber(script.drops.length - names.length));
        }

        return window.Toolbox.confirm({
            title: messages.confirmTitle,
            message: messages.confirmMessage.replace('{0}', formatNumber(script.drops.length)).replace('{1}', list),
            okText: okText,
            tone: 'danger'
        });
    }

    function showTab(buttonId) {
        const button = document.getElementById(buttonId);
        if (button && window.bootstrap && window.bootstrap.Tab) {
            window.bootstrap.Tab.getOrCreateInstance(button).show();
        }
    }

    // ----------------------------------------------------------------- wiring

    root.querySelectorAll('[data-sql-connection]').forEach(function (panel) {
        const side = panel.dataset.side;
        connections[side] = window.ToolboxSqlConnection.init(panel, {
            recent: true,
            onChange: function () {
                persistConnections();
                renderCodePreview();
                updateReadiness();
            },
            beforeTest: persistConnections,
            onTested: function () {
                state.tested.add(loginSignature(connections[side].read()));
                window.setTimeout(updateReadiness, 0);
            }
        });
        // The database box is a ComboBox whose change fires on blur; typing should unlock too.
        panel.addEventListener('input', updateReadiness);
    });
    restoreConnections();
    restoreOptions();

    root.querySelector('[data-role="detail-back"]').addEventListener('click', hideDetailPane);
    ['sc-code-from', 'sc-code-to', 'sc-mapping-input'].forEach(function (id) {
        document.getElementById(id).addEventListener('input', function () {
            renderCodePreview();
            saveOptions();
        });
    });
    root.querySelectorAll('[data-scope], [data-option]').forEach(function (node) {
        node.addEventListener('change', saveOptions);
    });
    renderCodePreview();
    renderDetail(null);
    updateReadiness();

    document.getElementById('sc-compare').addEventListener('click', startCompare);
    document.getElementById('sc-cancel').addEventListener('click', cancelCompare);
    document.getElementById('sc-swap').addEventListener('click', swapSides);
    document.getElementById('sc-reset-options').addEventListener('click', resetOptions);

    document.getElementById('sc-toggle-options').addEventListener('click', function () {
        setAdvancedOpen(document.getElementById('sc-options').hidden);
        saveOptions();
    });

    document.getElementById('sc-mapping-auto').addEventListener('click', autoFillMappings);

    document.getElementById('sc-toggle-mapped').addEventListener('click', function () {
        state.showMapped = !state.showMapped;
        root.querySelector('[data-role="result-layout"]').hidden = false;
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

    document.getElementById('sc-search').addEventListener('input', function () {
        renderGrid();
    });

    root.querySelectorAll('.sc-filter').forEach(function (button) {
        button.addEventListener('click', function () {
            state.actionFilter = button.dataset.filter;
            syncFilterButtons();
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

    document.addEventListener('keydown', function (event) {
        const key = event.key ? event.key.toLowerCase() : '';
        if ((key !== 'j' && key !== 'k') || event.ctrlKey || event.metaKey || event.altKey
            || isTyping(event.target) || document.querySelector('.modal.show')
            || document.getElementById('sc-results').hidden
            || !document.getElementById('sc-pane-result').classList.contains('active')
            || !state.rows.length) {
            return;
        }

        event.preventDefault();
        stepRow(key === 'j' ? 1 : -1);
    });

    document.getElementById('sc-refresh-script').addEventListener('click', function () {
        loadScript(true);
    });

    document.getElementById('sc-tab-script').addEventListener('shown.bs.tab', function () {
        loadScript(false);
    });

    document.getElementById('sc-next-drop').addEventListener('click', nextDrop);

    document.getElementById('sc-download-script').addEventListener('click', function (event) {
        event.preventDefault();
        if (!state.script || !state.script.includedCount) {
            return;
        }

        const href = this.href;
        confirmDrops(messages.confirmDownload).then(function (confirmed) {
            if (confirmed) {
                window.location.href = href;
            }
        });
    });

    document.getElementById('sc-copy-script').addEventListener('click', async function () {
        const status = root.querySelector('[data-role="script-status"]');
        if (!state.script || !(await confirmDrops(messages.confirmCopy))) {
            return;
        }

        try {
            await navigator.clipboard.writeText(state.script.text);
            setStatus(status, messages.copied, 'ok');
        } catch (error) {
            setStatus(status, messages.copyFailed, 'error');
        }
    });
})();
