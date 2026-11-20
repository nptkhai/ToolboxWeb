// Catalogs page (/catalogs.html, Views/JiraCatalog/Index.cshtml). Reads and syncs through
// window.ToolboxJiraCatalog (Content/js/jira-catalog.js): one row per catalog with its count and
// last sync, "Sync" per row and "Sync all", a dialog to look through the items, and the settings
// (waiting statuses, projects for Versions) in Kendo MultiSelects.
(function (window, document) {
    'use strict';

    const root = document.querySelector('[data-jira-catalog-page]');
    const catalog = window.ToolboxJiraCatalog;
    if (!root || !catalog) {
        return;
    }

    const config = readJson('jira-catalog-page-config') || {};
    const text = config.text || {};
    const kinds = config.kinds || [];
    const MAX_ROWS = 300;
    const $ = window.jQuery;
    const els = {
        summary: root.querySelector('[data-jc-summary]'),
        note: root.querySelector('[data-jc-note]'),
        syncAll: root.querySelector('[data-jc-sync-all]'),
        settings: root.querySelector('[data-jc-settings]'),
        waiting: root.querySelector('[data-jc-waiting]'),
        versionProjects: root.querySelector('[data-jc-version-projects]'),
        versionHint: root.querySelector('[data-jc-version-hint]'),
        save: root.querySelector('[data-jc-save-settings]'),
        modal: document.getElementById('jc-view-modal'),
        viewTitle: document.querySelector('[data-jc-view-title]'),
        viewSearch: document.querySelector('[data-jc-view-search]'),
        viewBody: document.querySelector('[data-jc-view-body]'),
        viewNote: document.querySelector('[data-jc-view-note]')
    };
    let busy = false;
    let viewing = null;
    let waitingBox = null;
    let projectsBox = null;

    // Dialogs paint under the backdrop inside .app-shell: move this one to <body> (as list-page.js does).
    if (els.modal) {
        const host = document.createElement('div');
        host.className = 'content-area tbx-modal-host';
        host.appendChild(els.modal);
        document.body.appendChild(host);
    }

    function readJson(id) {
        try {
            return JSON.parse(document.getElementById(id)?.textContent || 'null');
        } catch (error) {
            return null;
        }
    }

    function fmt(template) {
        const args = Array.prototype.slice.call(arguments, 1);
        return String(template || '').replace(/\{(\d+)\}/g, (m, i) => (args[i] === undefined ? m : args[i]));
    }

    function escapeHtml(value) {
        return String(value ?? '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
    }

    function number(value) {
        return window.Toolbox?.format ? window.Toolbox.format.number(value, 0) : String(value);
    }

    function when(iso) {
        const date = iso ? new Date(iso) : null;
        if (!date || Number.isNaN(date.getTime()) || date.getFullYear() < 2000) return '';
        return window.Toolbox?.format ? window.Toolbox.format.dateTime(date) : date.toLocaleString();
    }

    function notify(message, type) {
        if (window.Toolbox?.notify) window.Toolbox.notify(message, type);
    }

    function kindName(kind) {
        return text['Kind.' + kind] || kind;
    }

    // ------------------------------------------------------------------ rows and summary

    function render(state) {
        const catalogs = state?.catalogs || {};
        const have = kinds.filter((kind) => catalogs[kind]?.data !== undefined && catalogs[kind]?.data !== null);
        const latest = have.map((kind) => catalogs[kind].syncedAt).filter(Boolean).sort().pop();
        els.summary.textContent = have.length
            ? fmt(text.Summary, number(have.length), number(kinds.length), when(latest))
            : text.SummaryEmpty;

        if (state?.storedInBrowser) {
            const kb = Math.max(1, Math.round(catalog.storedBytes() / 1024));
            els.note.textContent = fmt(text.NoteBrowser, number(kb) + ' KB');
        } else {
            els.note.textContent = text.NoteServer;
        }

        kinds.forEach((kind) => {
            const row = root.querySelector(`[data-jc-kind="${kind}"]`);
            const entry = catalogs[kind];
            const hasData = entry && entry.data !== undefined && entry.data !== null;
            row.querySelector('[data-jc-count]').textContent = hasData ? fmt(text.Count, number(entry.count || 0)) : '–';
            row.querySelector('[data-jc-time]').textContent = hasData ? fmt(text.SyncedAt, when(entry.syncedAt)) : text.NotSynced;
            const error = row.querySelector('[data-jc-error]');
            error.hidden = !entry?.error;
            error.textContent = entry?.error || '';
            row.querySelector('[data-jc-view]').disabled = !hasData;
            row.querySelector('[data-jc-sync]').disabled = busy;
            row.classList.toggle('is-empty', !hasData);
        });
        els.syncAll.disabled = busy;
        renderSettings(state);
    }

    function setBusy(on, kind) {
        busy = on;
        els.syncAll.disabled = on;
        root.querySelectorAll('[data-jc-sync]').forEach((button) => { button.disabled = on; });
        root.querySelectorAll('[data-jc-kind]').forEach((row) => {
            row.classList.toggle('is-syncing', on && (!kind || row.dataset.jcKind === kind));
        });
        if (on) els.summary.textContent = text.Syncing;
    }

    async function run(list) {
        if (busy) return;
        setBusy(true, list.length === 1 ? list[0] : null);
        try {
            const state = await catalog.sync(list);
            const failed = list.filter((kind) => state.catalogs?.[kind]?.error);
            const done = list.filter((kind) => !failed.includes(kind));
            if (done.length) notify(fmt(text.SyncDone, done.map(kindName).join(', ')), failed.length ? 'warning' : 'success');
            if (failed.length) notify(fmt(text.SyncPartial, failed.map(kindName).join(', ')), 'error');
        } catch (error) {
            notify(error?.message || text.SyncFailed, 'error');
        } finally {
            setBusy(false);
            render(catalog.state);
        }
    }

    // ------------------------------------------------------------------ settings

    function multiSelect(select, items, value, placeholder) {
        if ($?.fn?.kendoMultiSelect) {
            let box = $(select).data('kendoMultiSelect');
            if (!box) {
                box = $(select).kendoMultiSelect({
                    dataSource: items,
                    dataTextField: 'text',
                    dataValueField: 'value',
                    filter: 'contains',
                    autoClose: false,
                    placeholder: placeholder || ''
                }).data('kendoMultiSelect');
            } else {
                box.setDataSource(new window.kendo.data.DataSource({ data: items }));
            }
            box.value(value);
            return box;
        }

        // Without Kendo: a plain multiple select.
        select.innerHTML = items.map((x) => `<option value="${escapeHtml(x.value)}">${escapeHtml(x.text)}</option>`).join('');
        Array.from(select.options).forEach((option) => { option.selected = value.includes(option.value); });
        return { value: () => Array.from(select.selectedOptions).map((x) => x.value), enable: (on) => { select.disabled = !on; } };
    }

    function renderSettings(state) {
        const settings = state?.settings || {};
        const statusNames = catalog.items(state, 'statuses').map((x) => x.name);
        const waiting = settings.waitingStatuses || [];
        const names = Array.from(new Set([].concat(statusNames, config.defaultWaiting || [], waiting)))
            .sort((a, b) => a.localeCompare(b));
        waitingBox = multiSelect(els.waiting, names.map((x) => ({ text: x, value: x })), waiting);

        const projects = catalog.items(state, 'projects');
        const chosen = settings.versionProjects || [];
        const projectItems = projects.map((x) => ({ text: x.key + ' · ' + x.name, value: x.key }));
        chosen.filter((key) => !projects.some((x) => x.key === key)).forEach((key) => projectItems.push({ text: key, value: key }));
        projectsBox = multiSelect(els.versionProjects, projectItems, chosen);
        projectsBox.enable?.(projectItems.length > 0);
        els.versionHint.textContent = projectItems.length ? els.versionHint.dataset.hint : text['Settings.NeedProjects'];
        els.save.disabled = false;
    }

    els.versionHint.dataset.hint = els.versionHint.textContent.trim();

    els.settings.addEventListener('submit', async (event) => {
        event.preventDefault();
        els.save.disabled = true;
        try {
            await catalog.saveSettings({
                waitingStatuses: waitingBox ? waitingBox.value() : [],
                versionProjects: projectsBox ? projectsBox.value() : []
            });
            notify(text['Settings.Saved'], 'success');
        } catch (error) {
            notify(error?.message || text['Settings.SaveFailed'], 'error');
        } finally {
            els.save.disabled = false;
        }
    });

    root.querySelector('[data-jc-reset-waiting]').addEventListener('click', () => {
        waitingBox?.value(config.defaultWaiting || []);
    });

    // ------------------------------------------------------------------ view dialog

    function columns(kind) {
        switch (kind) {
            case 'projects': return [[text['Col.Key'], (x) => x.key], [text['Col.Name'], (x) => x.name], [text['Col.Category'], (x) => x.category], [text['Col.Lead'], (x) => x.lead]];
            case 'statuses': return [[text['Col.Name'], (x) => x.name], [text['Col.Group'], (x) => text['Category.' + x.category] || x.category]];
            case 'issuetypes': return [[text['Col.Name'], (x) => x.name], [text['Col.Subtask'], (x) => (x.subtask ? text.Yes : text.No)]];
            case 'priorities':
            case 'resolutions': return [[text['Col.Key'], (x) => x.id], [text['Col.Name'], (x) => x.name]];
            case 'filters': return [[text['Col.Name'], (x) => x.name], [text['Col.Jql'], (x) => x.jql]];
            case 'users': return [[text['Col.Name'], (x) => x.displayName], [text['Col.Username'], (x) => x.name]];
            case 'versions': return [[text['Col.Project'], (x) => x.project], [text['Col.Name'], (x) => x.name], [text['Col.Released'], (x) => (x.released ? text.Yes : text.No)], [text['Col.ReleaseDate'], (x) => x.releaseDate || '']];
            case 'config': return [[text['Col.Item'], (x) => x.label], [text['Col.Value'], (x) => x.value]];
            default: return [[text['Col.Name'], (x) => (typeof x === 'string' ? x : x.name)]];
        }
    }

    function rowsOf(kind) {
        const data = catalog.data(catalog.state, kind);
        if (kind === 'versions' && data && !Array.isArray(data)) {
            return Object.keys(data).flatMap((project) => (data[project] || []).map((v) => Object.assign({ project: project }, v)));
        }
        if (kind === 'config' && data && !Array.isArray(data)) {
            return Object.keys(data).filter((key) => text['Config.' + key]).map((key) => ({ label: text['Config.' + key], value: data[key] ?? '' }));
        }
        return Array.isArray(data) ? data : [];
    }

    function renderView() {
        if (!viewing) return;
        const cols = columns(viewing);
        const query = els.viewSearch.value.trim().toLowerCase();
        const all = rowsOf(viewing);
        const matches = query
            ? all.filter((x) => cols.some(([, get]) => String(get(x) ?? '').toLowerCase().includes(query)))
            : all;
        const shown = matches.slice(0, MAX_ROWS);
        els.viewTitle.textContent = fmt(text.ViewTitle, kindName(viewing), number(all.length));
        els.viewBody.innerHTML = shown.length
            ? '<table class="jc-table"><thead><tr>' + cols.map(([label]) => `<th scope="col">${escapeHtml(label)}</th>`).join('') + '</tr></thead><tbody>'
                + shown.map((x) => '<tr>' + cols.map(([, get]) => `<td>${escapeHtml(get(x))}</td>`).join('') + '</tr>').join('')
                + '</tbody></table>'
            : `<div class="tbx-empty"><p class="tbx-empty-text">${escapeHtml(text.SearchEmpty)}</p></div>`;
        els.viewNote.textContent = matches.length > MAX_ROWS ? fmt(text.ShowingFirst, number(MAX_ROWS)) : '';
    }

    els.viewSearch.addEventListener('input', renderView);

    // ------------------------------------------------------------------ wiring

    els.syncAll.addEventListener('click', () => run(kinds));
    root.querySelectorAll('[data-jc-kind]').forEach((row) => {
        const kind = row.dataset.jcKind;
        row.querySelector('[data-jc-sync]').addEventListener('click', () => run([kind]));
        row.querySelector('[data-jc-view]').addEventListener('click', () => {
            viewing = kind;
            els.viewSearch.value = '';
            renderView();
            window.bootstrap?.Modal.getOrCreateInstance(els.modal).show();
        });
    });

    window.addEventListener('toolbox:jira-catalog', (event) => render(event.detail));

    catalog.ensure().then(render).catch(() => {
        els.summary.textContent = text.LoadFailed;
        els.syncAll.disabled = false;
        root.querySelectorAll('[data-jc-sync]').forEach((button) => { button.disabled = false; });
    });
})(window, document);
