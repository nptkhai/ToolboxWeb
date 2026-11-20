// Jira catalogs in the browser (window.ToolboxJiraCatalog), loaded by Views/Shared/_JiraCatalogScript.
// - load(): the catalogs and settings. The IIS site and the desktop keep them on the server
//   (GET /JiraCatalog/State); the web copy keeps the catalogs in this browser's localStorage
//   (one key per account, checked against the Jira address) and its settings in a cookie.
// - ensure(): load(), and when there is no catalog yet, read them from Jira once (the first,
//   automatic sync). After that, only sync() changes them: Toolkit never reads catalogs on
//   sign-in or on page load by itself.
// - sync(kinds): POST /JiraCatalog/Sync for the chosen catalogs.
// - saveSettings(settings): waiting statuses and the projects of the Versions catalog.
// Every change fires the "toolbox:jira-catalog" event on window with the new state.
(function (window, document) {
    'use strict';

    const config = readJson('jira-catalog-config');
    if (!config) {
        return;
    }

    const text = config.text || {};
    const FIRST_TRY_KEY = 'toolbox.jiraCatalog.firstTried:' + (config.storageKey || config.baseUrl);
    let current = null;
    let loading = null;

    function readJson(id) {
        try {
            return JSON.parse(document.getElementById(id)?.textContent || 'null');
        } catch (error) {
            return null;
        }
    }

    function notify(message, type) {
        if (message && window.Toolbox?.notify) {
            window.Toolbox.notify(message, type);
        }
    }

    async function request(url, body) {
        const response = await fetch(url, {
            method: body ? 'POST' : 'GET',
            headers: Object.assign(
                { 'X-Requested-With': 'XMLHttpRequest' },
                body ? { 'Content-Type': 'application/json', 'RequestVerificationToken': config.token || '' } : {}),
            body: body ? JSON.stringify(body) : undefined
        });
        const result = await response.json().catch(() => null);
        if (!response.ok || !result || result.success === false) {
            const error = new Error(result?.message || String(response.status));
            error.requiresLogin = Boolean(result?.requiresLogin);
            throw error;
        }
        return result.data;
    }

    // ------------------------------------------------------------------ browser storage (web copy)

    function readLocal() {
        try {
            const saved = JSON.parse(window.localStorage.getItem(config.storageKey) || 'null');
            return saved && saved.baseUrl === config.baseUrl && saved.catalogs ? saved : null;
        } catch (error) {
            return null;
        }
    }

    function writeLocal(state) {
        try {
            window.localStorage.setItem(config.storageKey, JSON.stringify({
                baseUrl: config.baseUrl,
                catalogs: state.catalogs || {},
                versionProjects: state.settings?.versionProjects || []
            }));
            return true;
        } catch (error) {
            notify(text.storageFull, 'warning');
            return false;
        }
    }

    /** Bytes this browser uses for the catalogs (web copy), for the note on the catalog page. */
    function storedBytes() {
        try {
            const raw = window.localStorage.getItem(config.storageKey) || '';
            return new Blob([raw]).size;
        } catch (error) {
            return 0;
        }
    }

    // ------------------------------------------------------------------ state

    function publish(state) {
        current = state;
        window.dispatchEvent(new CustomEvent('toolbox:jira-catalog', { detail: state }));
        return state;
    }

    function normalise(state) {
        return {
            baseUrl: state?.baseUrl || config.baseUrl,
            storedInBrowser: Boolean(config.storedInBrowser),
            catalogs: state?.catalogs || {},
            settings: {
                waitingStatuses: state?.settings?.waitingStatuses || [],
                versionProjects: state?.settings?.versionProjects || []
            }
        };
    }

    async function fetchState() {
        const server = normalise(await request(config.stateUrl));
        if (!config.storedInBrowser) {
            return server;
        }

        const local = readLocal();
        server.catalogs = local?.catalogs || {};
        server.settings.versionProjects = local?.versionProjects || [];
        return server;
    }

    function load(force) {
        if (current && !force) {
            return Promise.resolve(current);
        }
        if (!loading || force) {
            loading = fetchState().then(publish).finally(() => { loading = null; });
        }
        return loading;
    }

    function isEmpty(state) {
        return !state || Object.keys(state.catalogs || {}).filter((kind) => !state.catalogs[kind]?.error).length === 0;
    }

    /** Items of one catalog (array), or [] when it has not been synced. */
    function items(state, kind) {
        const data = (state || current)?.catalogs?.[kind]?.data;
        return Array.isArray(data) ? data : [];
    }

    function data(state, kind) {
        return (state || current)?.catalogs?.[kind]?.data ?? null;
    }

    async function sync(kinds, options) {
        const settings = options?.settings || current?.settings || {};
        const answer = normalise(await request(config.syncUrl, {
            kinds: kinds,
            versionProjects: settings.versionProjects || [],
            first: Boolean(options?.first)
        }));

        let state = answer;
        if (config.storedInBrowser) {
            // The server only answers with what it read: merge it into what this browser keeps.
            const base = current ? normalise(current) : normalise({ catalogs: readLocal()?.catalogs, settings: answer.settings });
            Object.keys(answer.catalogs).forEach((kind) => {
                const entry = answer.catalogs[kind];
                base.catalogs[kind] = entry.error
                    ? Object.assign({}, base.catalogs[kind] || {}, { error: entry.error })
                    : entry;
            });
            base.settings.waitingStatuses = answer.settings.waitingStatuses;
            state = base;
            const toStore = normalise(state);
            Object.keys(toStore.catalogs).forEach((kind) => {
                if (toStore.catalogs[kind]?.error && !toStore.catalogs[kind].data) delete toStore.catalogs[kind];
            });
            writeLocal(toStore);
        }

        return publish(state);
    }

    async function saveSettings(settings) {
        const saved = await request(config.settingsUrl, settings);
        const state = normalise(current);
        state.settings.waitingStatuses = saved.waitingStatuses || [];
        state.settings.versionProjects = config.storedInBrowser ? (settings.versionProjects || []) : (saved.versionProjects || []);
        if (config.storedInBrowser) {
            writeLocal(state);
        }
        return publish(state);
    }

    /** load(), and the first automatic sync while there are no catalogs (once per browser session). */
    async function ensure() {
        const state = await load();
        if (!isEmpty(state)) {
            return state;
        }

        try {
            if (window.sessionStorage.getItem(FIRST_TRY_KEY)) {
                return state;
            }
            window.sessionStorage.setItem(FIRST_TRY_KEY, '1');
        } catch (error) {
            // No session storage: try anyway.
        }

        notify(text.firstSyncStart, 'info');
        try {
            const synced = await sync(config.firstSync || [], { first: true });
            notify(isEmpty(synced) ? text.firstSyncFailed : text.firstSyncDone, isEmpty(synced) ? 'warning' : 'success');
            return synced;
        } catch (error) {
            notify(text.firstSyncFailed, 'warning');
            return state;
        }
    }

    window.ToolboxJiraCatalog = {
        config: config,
        load: load,
        ensure: ensure,
        sync: sync,
        saveSettings: saveSettings,
        isEmpty: isEmpty,
        items: items,
        data: data,
        storedBytes: storedBytes,
        get state() { return current; }
    };
})(window, document);
