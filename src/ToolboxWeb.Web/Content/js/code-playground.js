// Code playground (/code-playground.html, Views/CodePlayground/Index.cshtml). Everything stays in
// this browser:
// - the editor is CodeMirror 5, one editor with three documents (HTML, CSS, JS) so each tab keeps
//   its own cursor and undo history; colours come from the --syntax-* tokens (theme "toolkit");
// - Run builds the page with Content/js/code-playground-sandbox.js and shows it in a sandboxed
//   frame without allow-same-origin: the code cannot read Toolkit cookies, storage or pages;
// - the console shows what the frame posts back, only from that frame and with this run's token;
// - the draft and up to config.maxSaved named pieces of work live in localStorage
//   (config.storageKey, one per account), view choices in toolbox.playground.prefs.v1;
// - Download builds a standalone file (shipped libraries are copied into it), Share puts the
//   work, compressed, in the link's #fragment, which never reaches the server, and "Open in a new
//   tab" hands the page to /code-playground/run.html with postMessage (never a blob URL, which
//   would run the code with Toolkit's origin).
// Shortcuts go through Toolbox.keys: Ctrl/⌘+Enter run, Ctrl/⌘+S save, Alt+Shift+F format,
// Alt+1/2/3 switch tabs.
(function () {
    'use strict';

    const root = document.querySelector('[data-playground]');
    if (!root || !window.CodeMirror || !window.ToolboxPlaygroundSandbox) {
        return;
    }

    const config = readJson('code-playground-config') || {};
    const text = config.text || {};
    const sandbox = window.ToolboxPlaygroundSandbox;
    const PREFS_KEY = 'toolbox.playground.prefs.v1';
    const TABS = ['html', 'css', 'js'];
    const MODES = { html: 'htmlmixed', css: 'css', js: 'javascript' };
    const MAX_SAVED = config.maxSaved || 20;
    const MAX_LINES = config.maxConsoleLines || 500;
    const narrow = window.matchMedia('(max-width: 899.98px)');

    // Starting points. The sample text is English on purpose: it is code, not app text.
    const TEMPLATES = [
        { id: 'blank', name: 'Templates.Blank', hint: 'Templates.BlankHint', html: '', css: '', js: '', libs: [] },
        {
            id: 'bootstrap', name: 'Templates.Bootstrap', hint: 'Templates.BootstrapHint', libs: ['bootstrap'],
            html: '<div class="container py-4">\n  <h1 class="h3 mb-3">Hello, Bootstrap</h1>\n  <div class="row g-3">\n    <div class="col-md-6">\n      <div class="card">\n        <div class="card-body">\n          <h2 class="h5 card-title">Card title</h2>\n          <p class="card-text">Some quick text inside a card.</p>\n          <button class="btn btn-primary" id="hello">Click me</button>\n        </div>\n      </div>\n    </div>\n    <div class="col-md-6">\n      <div class="alert alert-info mb-0">An alert next to the card.</div>\n    </div>\n  </div>\n</div>\n',
            css: 'body {\n  padding: 8px;\n}\n',
            js: "document.getElementById('hello').addEventListener('click', () => {\n  console.log('Button clicked at', new Date().toLocaleTimeString());\n});\n"
        },
        {
            id: 'jquery', name: 'Templates.Jquery', hint: 'Templates.JqueryHint', libs: ['jquery'],
            html: '<button id="add">Add item</button>\n<span id="count">0</span> items\n<ul id="list"></ul>\n',
            css: 'body {\n  font-family: system-ui, sans-serif;\n  padding: 16px;\n}\n\nbutton {\n  padding: 6px 12px;\n}\n',
            js: "let count = 0;\n\n$('#add').on('click', function () {\n  count++;\n  $('#count').text(count);\n  $('<li>').text('Item ' + count).appendTo('#list');\n  console.log('Added item', count);\n});\n"
        },
        {
            id: 'metric', name: 'Templates.Metric', hint: 'Templates.MetricHint', libs: [],
            html: '<div class="metric-card">\n  <div class="header">\n    <span class="badge">Live telemetry</span>\n    <span class="dot"></span>\n  </div>\n  <h3>API conversion rate</h3>\n  <div class="metric-val">\n    <span id="counter">99.82</span><span class="unit">%</span>\n  </div>\n  <p class="desc">+2.4% compared with the last period</p>\n  <button id="simulateBtn">Simulate request</button>\n</div>\n',
            css: 'body {\n  font-family: system-ui, sans-serif;\n  display: grid;\n  place-items: center;\n  min-height: 100vh;\n  margin: 0;\n  background: whitesmoke;\n}\n\n.metric-card {\n  background: white;\n  border: 1px solid gainsboro;\n  border-radius: 12px;\n  padding: 20px 24px;\n  width: 300px;\n  box-shadow: 0 8px 24px gainsboro;\n}\n\n.header {\n  display: flex;\n  align-items: center;\n  gap: 8px;\n}\n\n.badge {\n  background: aliceblue;\n  color: steelblue;\n  font-size: 12px;\n  padding: 2px 8px;\n  border-radius: 6px;\n}\n\n.dot {\n  width: 8px;\n  height: 8px;\n  border-radius: 50%;\n  background: mediumseagreen;\n}\n\nh3 {\n  margin: 16px 0 4px;\n  font-size: 16px;\n}\n\n.metric-val {\n  font-size: 40px;\n  font-weight: 700;\n}\n\n.unit {\n  font-size: 18px;\n  color: steelblue;\n  margin-left: 4px;\n}\n\n.desc {\n  color: dimgray;\n  font-size: 13px;\n}\n\nbutton {\n  width: 100%;\n  padding: 10px;\n  border: 0;\n  border-radius: 8px;\n  background: steelblue;\n  color: white;\n  font-weight: 600;\n  cursor: pointer;\n}\n',
            js: "const counter = document.getElementById('counter');\n\ndocument.getElementById('simulateBtn').addEventListener('click', () => {\n  const value = (95 + Math.random() * 5).toFixed(2);\n  counter.textContent = value;\n  console.log('[Telemetry] Request simulated, rate =', value + '%');\n});\n\nconsole.info('[DOM ready] Metric card rendered');\n"
        }
    ];

    const els = {
        name: root.querySelector('[data-pg-name]'),
        status: root.querySelector('[data-pg-status]'),
        run: root.querySelector('[data-pg-run]'),
        autorun: root.querySelector('[data-pg-autorun]'),
        format: root.querySelector('[data-pg-format]'),
        libs: root.querySelector('[data-pg-libs]'),
        libsCount: root.querySelector('[data-pg-libs-count]'),
        work: root.querySelector('[data-pg-work]'),
        tabs: root.querySelectorAll('[data-pg-tab]'),
        code: root.querySelector('[data-pg-code]'),
        cursor: root.querySelector('[data-pg-cursor]'),
        syntax: root.querySelector('[data-pg-syntax]'),
        jsError: root.querySelector('[data-pg-js-error]'),
        splitter: root.querySelector('[data-pg-splitter]'),
        preview: root.querySelector('[data-pg-preview]'),
        frameWrap: root.querySelector('[data-pg-frame-wrap]'),
        frame: root.querySelector('[data-pg-frame]'),
        empty: root.querySelector('[data-pg-empty]'),
        fullscreen: root.querySelector('[data-pg-fullscreen]'),
        fullscreenText: root.querySelector('[data-pg-fullscreen-text]'),
        console: root.querySelector('[data-pg-console]'),
        consoleList: root.querySelector('[data-pg-console-list]'),
        consoleEmpty: root.querySelector('[data-pg-console-empty]'),
        consoleToggle: root.querySelector('[data-pg-console-toggle]'),
        consoleCountMobile: root.querySelector('[data-pg-console-count-mobile]'),
        libsModal: document.getElementById('pg-libs-modal'),
        libsForm: document.querySelector('[data-pg-libs-form]'),
        libsUrls: document.querySelector('[data-pg-lib-urls]'),
        libsError: document.querySelector('[data-pg-lib-error]'),
        templatesModal: document.getElementById('pg-templates-modal'),
        templateList: document.querySelector('[data-pg-template-list]'),
        savedModal: document.getElementById('pg-saved-modal'),
        savedList: document.querySelector('[data-pg-saved-list]'),
        savedEmpty: document.querySelector('[data-pg-saved-empty]'),
        savedCount: document.querySelector('[data-pg-saved-count]'),
        nameModal: document.getElementById('pg-name-modal'),
        nameForm: document.querySelector('[data-pg-name-form]'),
        nameTitle: document.querySelector('[data-pg-name-title]'),
        nameInput: document.querySelector('[data-pg-name-input]'),
        nameError: document.querySelector('[data-pg-name-error]'),
        nameSubmit: document.querySelector('[data-pg-name-submit]')
    };

    // Bootstrap puts the backdrop on <body>, but .app-shell has its own z-index, so a dialog left
    // inside it paints under the backdrop. Move the page's dialogs to <body>, as list-page.js
    // does; the host keeps .content-area so the form styles still apply.
    (function moveDialogs() {
        const host = document.createElement('div');
        host.className = 'content-area tbx-modal-host';
        [els.libsModal, els.templatesModal, els.savedModal, els.nameModal].forEach(function (dialog) {
            if (dialog) host.appendChild(dialog);
        });
        document.body.appendChild(host);
    })();

    const state = {
        tab: 'html',
        libs: { builtin: [], urls: [] },
        itemId: null,
        draftSavedAt: null,
        storageOk: true,
        autorun: true,
        layout: 'split',
        ratio: 0.5,
        device: 'desktop',
        consoleOpen: true,
        filter: 'all',
        mobile: 'code',
        logs: [],
        trimmed: false,
        token: '',
        ranges: null,
        nameMode: null,
        nameTarget: null,
        openedTabs: new Set(),
        loading: false
    };

    // ------------------------------------------------------------------ helpers

    function readJson(id) {
        try {
            return JSON.parse(document.getElementById(id)?.textContent || 'null');
        } catch (error) {
            return null;
        }
    }

    function t(key) {
        return text[key] || key;
    }

    function fmt(template) {
        const args = Array.prototype.slice.call(arguments, 1);
        return String(template || '').replace(/\{(\d+)\}/g, function (match, i) {
            return args[i] === undefined ? match : args[i];
        });
    }

    function escapeHtml(value) {
        return String(value ?? '')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    function icon(name, size) {
        return window.Toolbox?.icon ? window.Toolbox.icon(name, size) : '';
    }

    function notify(message, type) {
        if (window.Toolbox?.notify) {
            window.Toolbox.notify(message, type);
        }
    }

    function confirmAsk(options) {
        return window.Toolbox?.confirm ? window.Toolbox.confirm(options) : Promise.resolve(window.confirm(options.message));
    }

    function modal(element) {
        return window.bootstrap?.Modal ? window.bootstrap.Modal.getOrCreateInstance(element) : null;
    }

    // Runs fn once the modal has finished hiding (Bootstrap shows one dialog at a time).
    function afterHidden(element, fn) {
        const instance = modal(element);
        if (!instance || !element.classList.contains('show')) {
            fn();
            return;
        }
        element.addEventListener('hidden.bs.modal', function done() {
            element.removeEventListener('hidden.bs.modal', done);
            fn();
        });
        instance.hide();
    }

    function afterConfirmClosed(fn) {
        const confirmEl = document.getElementById('tbx-confirm');
        if (confirmEl && confirmEl.classList.contains('show')) {
            confirmEl.addEventListener('hidden.bs.modal', function done() {
                confirmEl.removeEventListener('hidden.bs.modal', done);
                fn();
            });
        } else {
            window.setTimeout(fn, 0);
        }
    }

    function debounce(fn, ms) {
        let timer = 0;
        return function () {
            window.clearTimeout(timer);
            timer = window.setTimeout(fn, ms);
        };
    }

    function timeOf(ms) {
        return window.Toolbox?.format ? window.Toolbox.format.time(new Date(ms)) : new Date(ms).toLocaleTimeString();
    }

    function dateTimeOf(ms) {
        return window.Toolbox?.format ? window.Toolbox.format.dateTime(new Date(ms)) : new Date(ms).toLocaleString();
    }

    // Time with seconds for console lines, in the app time zone.
    const clock = (function () {
        try {
            const f = window.Toolbox?.format;
            return new Intl.DateTimeFormat(f?.locale || undefined, { hour: '2-digit', minute: '2-digit', second: '2-digit', hour12: false, timeZone: f?.timeZone || undefined });
        } catch (error) {
            return new Intl.DateTimeFormat(undefined, { hour: '2-digit', minute: '2-digit', second: '2-digit', hour12: false });
        }
    })();

    function newId() {
        return Date.now().toString(36) + Math.random().toString(36).slice(2, 8);
    }

    function slug(value) {
        return String(value || '')
            .normalize('NFD').replace(/[̀-ͯ]/g, '')
            .replace(/đ/g, 'd').replace(/Đ/g, 'D')
            .replace(/[^A-Za-z0-9]+/g, '-')
            .replace(/^-+|-+$/g, '')
            .slice(0, 50)
            .replace(/-+$/, '') || 'playground';
    }

    function copyText(value) {
        if (navigator.clipboard && typeof navigator.clipboard.writeText === 'function') {
            return navigator.clipboard.writeText(value).then(function () { return true; }, function () { return legacyCopy(value); });
        }
        return Promise.resolve(legacyCopy(value));
    }

    // For pages served over plain http on the LAN, where the Clipboard API is not offered.
    function legacyCopy(value) {
        const area = document.createElement('textarea');
        area.value = value;
        area.setAttribute('readonly', '');
        area.style.position = 'fixed';
        area.style.opacity = '0';
        document.body.appendChild(area);
        area.select();
        let ok = false;
        try {
            ok = document.execCommand('copy');
        } catch (error) {
            ok = false;
        }
        area.remove();
        return ok;
    }

    // ------------------------------------------------------------------ storage

    function readStore() {
        try {
            const value = JSON.parse(window.localStorage.getItem(config.storageKey) || 'null');
            if (value && typeof value === 'object') {
                return {
                    draft: value.draft && typeof value.draft === 'object' ? value.draft : null,
                    items: Array.isArray(value.items) ? value.items.filter(validWork) : []
                };
            }
        } catch (error) {
            // Fall through to an empty store.
        }
        return { draft: null, items: [] };
    }

    function writeStore(store) {
        try {
            window.localStorage.setItem(config.storageKey, JSON.stringify(store));
            state.storageOk = true;
            return true;
        } catch (error) {
            state.storageOk = false;
            return false;
        }
    }

    function validWork(x) {
        return x && typeof x === 'object' && typeof x.html === 'string' && typeof x.css === 'string' && typeof x.js === 'string';
    }

    function cleanLibs(libs) {
        const known = (config.libraries || []).map(function (x) { return x.id; });
        const value = libs && typeof libs === 'object' ? libs : {};
        return {
            builtin: (Array.isArray(value.builtin) ? value.builtin : []).filter(function (id) { return known.indexOf(id) >= 0; }),
            urls: (Array.isArray(value.urls) ? value.urls : []).filter(function (u) { return typeof u === 'string' && validUrl(u); })
        };
    }

    function readPrefs() {
        try {
            return JSON.parse(window.localStorage.getItem(PREFS_KEY) || '{}') || {};
        } catch (error) {
            return {};
        }
    }

    function writePrefs() {
        try {
            window.localStorage.setItem(PREFS_KEY, JSON.stringify({
                autorun: state.autorun, layout: state.layout, ratio: state.ratio, device: state.device,
                consoleOpen: state.consoleOpen, tab: state.tab
            }));
        } catch (error) {
            // Private window or storage blocked: the choices just are not remembered.
        }
    }

    // ------------------------------------------------------------------ editor

    const docs = {};
    TABS.forEach(function (tab) {
        docs[tab] = window.CodeMirror.Doc('', MODES[tab]);
    });

    const editor = window.CodeMirror(els.code, {
        value: docs.html,
        theme: 'toolkit',
        lineNumbers: true,
        tabSize: 2,
        indentUnit: 2,
        indentWithTabs: false,
        smartIndent: true,
        autoCloseTags: true,
        autoCloseBrackets: true,
        matchBrackets: true,
        matchTags: { bothTags: true },
        styleActiveLine: true,
        phrases: config.phrases || {},
        extraKeys: {
            'Ctrl-/': 'toggleComment',
            'Cmd-/': 'toggleComment',
            Tab: function (cm) {
                if (cm.somethingSelected()) {
                    cm.indentSelection('add');
                } else {
                    cm.replaceSelection('  ', 'end', '+input');
                }
            },
            'Shift-Tab': function (cm) { cm.indentSelection('subtract'); }
        }
    });
    editor.swapDoc(docs.html);

    function current() {
        return {
            name: els.name.value.trim(),
            html: docs.html.getValue(),
            css: docs.css.getValue(),
            js: docs.js.getValue(),
            libs: { builtin: state.libs.builtin.slice(), urls: state.libs.urls.slice() }
        };
    }

    function isEmptyWork(work) {
        return !work.html.trim() && !work.css.trim() && !work.js.trim();
    }

    function sameWork(a, b) {
        return a.name === b.name && a.html === b.html && a.css === b.css && a.js === b.js
            && JSON.stringify(cleanLibs(a.libs)) === JSON.stringify(cleanLibs(b.libs));
    }

    function currentItem(store) {
        return state.itemId ? (store || readStore()).items.find(function (x) { return x.id === state.itemId; }) : null;
    }

    function setTab(tab, focus) {
        if (TABS.indexOf(tab) < 0) return;
        state.tab = tab;
        editor.swapDoc(docs[tab]);
        els.tabs.forEach(function (button) {
            const on = button.dataset.pgTab === tab;
            button.setAttribute('aria-selected', on ? 'true' : 'false');
            button.tabIndex = on ? 0 : -1;
        });
        els.code.setAttribute('aria-labelledby', 'pg-tab-' + tab);
        // CodeMirror types into its own hidden textarea: give it a name for screen readers.
        editor.getInputField().setAttribute('aria-label', t('Editor') + ' · ' + tab.toUpperCase());
        updateCursor();
        writePrefs();
        if (focus) editor.focus();
    }

    function updateCursor() {
        const pos = editor.getCursor();
        els.cursor.textContent = fmt(t('Cursor'), pos.line + 1, pos.ch + 1);
    }

    function updateDots() {
        els.tabs.forEach(function (button) {
            button.classList.toggle('has-code', Boolean(docs[button.dataset.pgTab].getValue().trim()));
        });
    }

    // Compiles the JS without running it: new Function only parses the body.
    function checkSyntax() {
        const js = docs.js.getValue();
        let problem = '';
        if (js.trim()) {
            try {
                // eslint-disable-next-line no-new-func
                new Function(js);
            } catch (error) {
                if (error instanceof SyntaxError) problem = error.message;
            }
        }
        els.syntax.textContent = problem ? fmt(t('SyntaxError'), problem) : t('SyntaxOk');
        els.syntax.classList.toggle('is-error', Boolean(problem));
        els.jsError.hidden = !problem;
    }

    function setWork(work, itemId) {
        state.loading = true;
        TABS.forEach(function (tab) {
            docs[tab].setValue(work[tab] || '');
            docs[tab].clearHistory();
        });
        els.name.value = work.name || '';
        state.libs = cleanLibs(work.libs);
        state.itemId = itemId || null;
        updateLibsCount();
        updateDots();
        checkSyntax();
        editor.refresh();
        state.loading = false;
    }

    // ------------------------------------------------------------------ draft and status

    const saveDraftSoon = debounce(saveDraft, 1000);

    function saveDraft() {
        const store = readStore();
        state.draftSavedAt = Date.now();
        store.draft = Object.assign(current(), { itemId: state.itemId, savedAt: state.draftSavedAt });
        writeStore(store);
        updateStatus(store);
    }

    function updateStatus(store) {
        if (!state.storageOk) {
            els.status.textContent = t('Status.NotStored');
            els.status.className = 'pg-status is-warning';
            return;
        }

        const item = currentItem(store);
        if (item) {
            const dirty = !sameWork(current(), item);
            els.status.textContent = dirty ? fmt(t('Status.Unsaved'), item.name) : fmt(t('Status.Saved'), item.name, timeOf(item.updatedAt));
            els.status.className = 'pg-status' + (dirty ? ' is-dirty' : '');
            return;
        }

        els.status.textContent = state.draftSavedAt ? fmt(t('Status.Draft'), timeOf(state.draftSavedAt)) : '';
        els.status.className = 'pg-status';
    }

    // Replacing the work on screen asks first when it holds something not kept elsewhere.
    function confirmReplace(newName) {
        const work = current();
        const item = currentItem();
        const unsaved = item ? !sameWork(work, item) : !isEmptyWork(work);
        if (!unsaved) {
            return Promise.resolve(true);
        }
        return confirmAsk({
            title: t('Replace.Title'),
            message: fmt(t('Replace.Message'), newName || t('Name.Default')),
            okText: t('Replace.Ok'),
            tone: 'primary'
        });
    }

    // From a list dialog: close it, ask when needed, then replace; on "no", show the list again.
    function replaceFrom(listModal, newName, apply) {
        afterHidden(listModal, function () {
            confirmReplace(newName).then(function (ok) {
                if (ok) {
                    apply();
                } else {
                    afterConfirmClosed(function () { modal(listModal)?.show(); });
                }
            });
        });
    }

    // ------------------------------------------------------------------ running

    function libsForRun() {
        const out = [];
        (config.libraries || []).forEach(function (lib) {
            if (state.libs.builtin.indexOf(lib.id) < 0) return;
            lib.files.forEach(function (file) {
                out.push({ kind: /\.css(\?|$)/i.test(file) ? 'css' : 'js', url: new URL(file, window.location.href).href });
            });
        });
        state.libs.urls.forEach(function (url) {
            out.push({ kind: /\.css(\?|#|$)/i.test(url) ? 'css' : 'js', url: url });
        });
        return out;
    }

    function run() {
        const work = current();
        state.token = newId() + newId();
        const built = sandbox.build({
            html: work.html, css: work.css, js: work.js, title: work.name || t('Name.Default'),
            libs: libsForRun(),
            bridge: { token: state.token, origin: window.location.origin }
        });
        state.ranges = { html: built.html, js: built.js };
        clearConsole();
        const empty = isEmptyWork(work);
        els.empty.hidden = !empty;
        els.frame.srcdoc = empty ? '' : built.doc;
    }

    const runSoon = debounce(function () {
        if (state.autorun) run();
    }, 800);

    // ------------------------------------------------------------------ console

    function levelGroup(level) {
        return level === 'warn' ? 'warn' : level === 'error' ? 'error' : 'log';
    }

    function where(entry) {
        if (entry.file) {
            return entry.file.split('/').pop() + (entry.line ? ':' + entry.line : '');
        }
        if (!entry.line || !state.ranges) return '';
        const js = state.ranges.js;
        if (js && js.lines && entry.line >= js.start && entry.line < js.start + js.lines) {
            return 'JS · ' + fmt(t('Console.Line'), entry.line - js.start + 1);
        }
        const html = state.ranges.html;
        if (html && html.lines && entry.line >= html.start && entry.line < html.start + html.lines) {
            return 'HTML · ' + fmt(t('Console.Line'), entry.line - html.start + 1);
        }
        return '';
    }

    function logHtml(entry) {
        const place = where(entry);
        return '<li class="pg-log is-' + entry.level + '">'
            + '<span class="pg-log-time">' + escapeHtml(entry.time) + '</span>'
            + '<span class="pg-log-mark" aria-hidden="true">›</span>'
            + '<span class="pg-log-text">' + escapeHtml(entry.text) + '</span>'
            + (place ? '<span class="pg-log-where">' + escapeHtml(place) + '</span>' : '')
            + '</li>';
    }

    function visible(entry) {
        return state.filter === 'all' || levelGroup(entry.level) === state.filter;
    }

    function renderConsole() {
        const shown = state.logs.filter(visible);
        els.consoleList.innerHTML = (state.trimmed ? '<li class="pg-log is-note">' + escapeHtml(fmt(t('Console.Trimmed'), MAX_LINES)) + '</li>' : '')
            + shown.map(logHtml).join('');
        updateCounts();
    }

    function updateCounts() {
        const counts = { all: state.logs.length, log: 0, warn: 0, error: 0 };
        state.logs.forEach(function (entry) { counts[levelGroup(entry.level)]++; });
        Object.keys(counts).forEach(function (key) {
            const el = root.querySelector('[data-pg-count="' + key + '"]');
            if (el) el.textContent = counts[key];
        });
        els.consoleCountMobile.textContent = counts.all;
        els.consoleEmpty.hidden = state.logs.length > 0;
    }

    function addLog(entry) {
        entry.time = clock.format(new Date());
        state.logs.push(entry);
        if (state.logs.length > MAX_LINES) {
            state.logs.shift();
            state.trimmed = true;
            renderConsole();
        } else if (visible(entry)) {
            els.consoleList.insertAdjacentHTML('beforeend', logHtml(entry));
            updateCounts();
        } else {
            updateCounts();
        }
        els.consoleList.scrollTop = els.consoleList.scrollHeight;
    }

    function clearConsole() {
        state.logs = [];
        state.trimmed = false;
        renderConsole();
    }

    function setFilter(filter) {
        state.filter = filter;
        root.querySelectorAll('[data-pg-filter]').forEach(function (button) {
            button.setAttribute('aria-pressed', button.dataset.pgFilter === filter ? 'true' : 'false');
        });
        renderConsole();
    }

    function setConsoleOpen(open) {
        state.consoleOpen = open;
        els.console.classList.toggle('is-collapsed', !open);
        els.consoleToggle.setAttribute('aria-expanded', open ? 'true' : 'false');
        const label = open ? t('Console.Collapse') : t('Console.Expand');
        els.consoleToggle.setAttribute('aria-label', label);
        els.consoleToggle.title = label;
    }

    window.addEventListener('message', function (event) {
        const data = event.data;
        if (!data || typeof data !== 'object') return;

        // "Open in a new tab": the run page asks for the code.
        if (data.type === 'tbx-playground-ready' && event.origin === window.location.origin && state.openedTabs.has(event.source)) {
            const work = current();
            const built = sandbox.build({ html: work.html, css: work.css, js: work.js, title: work.name || t('Name.Default'), libs: libsForRun(), bridge: null });
            event.source.postMessage({ type: 'tbx-playground-document', doc: built.doc, title: work.name || t('Name.Default') }, window.location.origin);
            state.openedTabs.delete(event.source);
            return;
        }

        // The sandboxed frame has an opaque origin ("null"), so check the window and the token.
        if (event.source !== els.frame.contentWindow || data.__tbxPlayground !== state.token) return;
        if (data.type === 'clear') {
            clearConsole();
        } else if (data.type === 'log') {
            const level = ['log', 'info', 'warn', 'error', 'debug'].indexOf(data.level) >= 0 ? data.level : 'log';
            addLog({
                level: level,
                text: data.resource ? fmt(t('Console.LoadFailed'), String(data.resource)) : String(data.text ?? ''),
                line: Number(data.line) || 0,
                file: typeof data.file === 'string' ? data.file : ''
            });
        }
    });

    // ------------------------------------------------------------------ libraries

    function validUrl(value) {
        try {
            const url = new URL(value);
            return url.protocol === 'https:' || url.protocol === 'http:';
        } catch (error) {
            return false;
        }
    }

    function updateLibsCount() {
        const count = state.libs.builtin.length + state.libs.urls.length;
        els.libsCount.hidden = count === 0;
        els.libsCount.textContent = count;
    }

    function openLibs() {
        document.querySelectorAll('[data-pg-lib]').forEach(function (box) {
            box.checked = state.libs.builtin.indexOf(box.value) >= 0;
        });
        els.libsUrls.value = state.libs.urls.join('\n');
        els.libsError.textContent = '';
        els.libsUrls.removeAttribute('aria-invalid');
        modal(els.libsModal)?.show();
    }

    els.libsForm.addEventListener('submit', function (event) {
        event.preventDefault();
        const urls = els.libsUrls.value.split(/\r?\n/).map(function (x) { return x.trim(); }).filter(Boolean);
        const bad = urls.find(function (x) { return !validUrl(x); });
        if (bad) {
            els.libsError.textContent = fmt(t('Libs.Invalid'), bad);
            els.libsUrls.setAttribute('aria-invalid', 'true');
            els.libsUrls.focus();
            return;
        }

        const builtin = [];
        document.querySelectorAll('[data-pg-lib]').forEach(function (box) {
            if (box.checked) builtin.push(box.value);
        });
        state.libs = cleanLibs({ builtin: builtin, urls: urls });
        updateLibsCount();
        modal(els.libsModal)?.hide();
        saveDraft();
        run();
    });

    // ------------------------------------------------------------------ format

    let formatterPromise = null;

    function loadFormatter() {
        if (window.beautifier) return Promise.resolve(window.beautifier);
        if (!formatterPromise) {
            formatterPromise = new Promise(function (resolve, reject) {
                const script = document.createElement('script');
                script.src = config.formatterUrl;
                script.onload = function () { window.beautifier ? resolve(window.beautifier) : reject(new Error('beautifier')); };
                script.onerror = function () { formatterPromise = null; reject(new Error('beautifier')); };
                document.head.appendChild(script);
            });
        }
        return formatterPromise;
    }

    function format() {
        const tab = state.tab;
        const doc = docs[tab];
        loadFormatter().then(function (b) {
            const options = { indent_size: 2, indent_char: ' ', end_with_newline: true, preserve_newlines: true, max_preserve_newlines: 2 };
            const source = doc.getValue();
            let output;
            if (tab === 'html') {
                output = b.html(source, Object.assign({ indent_inner_html: false, wrap_line_length: 0, extra_liners: [] }, options));
            } else if (tab === 'css') {
                output = b.css(source, options);
            } else {
                output = b.js(source, options);
            }
            if (output !== source) {
                const cursor = doc.getCursor();
                doc.replaceRange(output, { line: 0, ch: 0 }, { line: doc.lastLine(), ch: doc.getLine(doc.lastLine()).length }, '+format');
                doc.setCursor({ line: Math.min(cursor.line, doc.lastLine()), ch: 0 });
            }
            editor.focus();
        }).catch(function (error) {
            notify(fmt(t('FormatFailed'), error && error.message ? error.message : ''), 'error');
        });
    }

    // ------------------------------------------------------------------ saved work

    function saveCurrent() {
        const store = readStore();
        const item = currentItem(store);
        if (!item) {
            openNameModal('save', null);
            return;
        }

        Object.assign(item, current(), { name: current().name || item.name, updatedAt: Date.now() });
        if (!writeStore(store)) {
            notify(t('Saved.StorageFull'), 'error');
            return;
        }
        els.name.value = item.name;
        saveDraft();
        notify(fmt(t('Saved.Done'), item.name), 'success');
    }

    function openNameModal(mode, targetId) {
        state.nameMode = mode;
        state.nameTarget = targetId;
        const store = readStore();
        const target = targetId ? store.items.find(function (x) { return x.id === targetId; }) : null;
        els.nameTitle.textContent = mode === 'rename' ? t('NameModal.Rename') : mode === 'saveAs' ? t('NameModal.SaveAs') : t('NameModal.Save');
        els.nameSubmit.textContent = mode === 'rename' ? t('NameModal.Rename') : t('Save');
        els.nameInput.value = target ? target.name : (els.name.value.trim() || t('Name.Default'));
        els.nameError.textContent = '';
        els.nameInput.removeAttribute('aria-invalid');
        const instance = modal(els.nameModal);
        if (instance) {
            els.nameModal.addEventListener('shown.bs.modal', function focusName() {
                els.nameInput.select();
                els.nameModal.removeEventListener('shown.bs.modal', focusName);
            });
            instance.show();
        }
    }

    els.nameForm.addEventListener('submit', function (event) {
        event.preventDefault();
        const name = els.nameInput.value.trim();
        if (!name) {
            els.nameError.textContent = t('NameModal.Required');
            els.nameInput.setAttribute('aria-invalid', 'true');
            els.nameInput.focus();
            return;
        }

        const store = readStore();
        if (state.nameMode === 'rename') {
            const target = store.items.find(function (x) { return x.id === state.nameTarget; });
            if (target) {
                target.name = name;
                target.updatedAt = Date.now();
            }
            if (state.nameTarget === state.itemId) {
                els.name.value = name;
            }
        } else {
            if (store.items.length >= MAX_SAVED) {
                els.nameError.textContent = fmt(t('Saved.Full'), MAX_SAVED);
                els.nameInput.setAttribute('aria-invalid', 'true');
                return;
            }
            els.name.value = name;
            const item = Object.assign(current(), { id: newId(), name: name, updatedAt: Date.now() });
            store.items.unshift(item);
            state.itemId = item.id;
        }

        if (!writeStore(store)) {
            els.nameError.textContent = t('Saved.StorageFull');
            return;
        }
        modal(els.nameModal)?.hide();
        saveDraft();
        renderSaved();
        if (state.nameMode !== 'rename') {
            notify(fmt(t('Saved.Done'), name), 'success');
        }
    });

    function renderSaved() {
        const store = readStore();
        const items = store.items.slice().sort(function (a, b) { return (b.updatedAt || 0) - (a.updatedAt || 0); });
        els.savedCount.textContent = fmt(t('Saved.Count'), items.length, MAX_SAVED);
        els.savedEmpty.hidden = items.length > 0;
        els.savedList.hidden = items.length === 0;
        els.savedList.innerHTML = items.map(function (item) {
            const name = item.name || t('Name.Default');
            const isCurrent = item.id === state.itemId;
            return '<li class="tbx-row tbx-row--two-line pg-pick-row">'
                + '<button type="button" class="pg-pick" data-saved-open="' + escapeHtml(item.id) + '">'
                + '<span class="tbx-row-title">' + escapeHtml(name) + '</span>'
                + '<span class="tbx-row-sub">' + escapeHtml(fmt(t('Saved.Updated'), dateTimeOf(item.updatedAt || Date.now()))) + '</span>'
                + '</button>'
                + (isCurrent ? '<span class="tbx-pill is-accent">' + escapeHtml(t('Saved.Current')) + '</span>' : '')
                + '<span class="tbx-row-actions">'
                + actionButton('pencil', 'rename', item.id, fmt(t('Saved.Rename'), name))
                + actionButton('copy-plus', 'duplicate', item.id, fmt(t('Saved.Duplicate'), name))
                + actionButton('trash-2', 'delete', item.id, fmt(t('Saved.Delete'), name))
                + '</span></li>';
        }).join('');
    }

    function actionButton(iconName, action, id, label) {
        return '<button type="button" class="btn btn-outline-secondary btn-sm tbx-icon-button" data-saved-' + action + '="' + escapeHtml(id) + '"'
            + ' aria-label="' + escapeHtml(label) + '" title="' + escapeHtml(label) + '">' + icon(iconName, 16) + '</button>';
    }

    els.savedList.addEventListener('click', function (event) {
        const store = readStore();
        const find = function (id) { return store.items.find(function (x) { return x.id === id; }); };
        const open = event.target.closest('[data-saved-open]');
        const rename = event.target.closest('[data-saved-rename]');
        const duplicate = event.target.closest('[data-saved-duplicate]');
        const remove = event.target.closest('[data-saved-delete]');

        if (open) {
            const item = find(open.dataset.savedOpen);
            if (!item) return;
            if (item.id === state.itemId) {
                modal(els.savedModal)?.hide();
                return;
            }
            replaceFrom(els.savedModal, item.name, function () {
                setWork(item, item.id);
                saveDraft();
                run();
            });
        } else if (rename) {
            const id = rename.dataset.savedRename;
            afterHidden(els.savedModal, function () { openNameModal('rename', id); });
        } else if (duplicate) {
            const item = find(duplicate.dataset.savedDuplicate);
            if (!item) return;
            if (store.items.length >= MAX_SAVED) {
                notify(fmt(t('Saved.Full'), MAX_SAVED), 'warning');
                return;
            }
            store.items.unshift(Object.assign({}, item, { id: newId(), name: item.name + ' ' + t('Saved.CopySuffix'), updatedAt: Date.now() }));
            if (!writeStore(store)) {
                notify(t('Saved.StorageFull'), 'error');
                return;
            }
            renderSaved();
        } else if (remove) {
            const item = find(remove.dataset.savedDelete);
            if (!item) return;
            // One dialog at a time: close the list, ask, then show the list again.
            afterHidden(els.savedModal, function () {
                confirmAsk({
                title: t('Saved.DeleteTitle'),
                message: fmt(t('Saved.DeleteMessage'), item.name),
                okText: t('delete')
                }).then(function (ok) {
                    if (ok) {
                        const fresh = readStore();
                        fresh.items = fresh.items.filter(function (x) { return x.id !== item.id; });
                        writeStore(fresh);
                        if (state.itemId === item.id) {
                            state.itemId = null;
                            saveDraft();
                        }
                        updateStatus();
                    }
                    renderSaved();
                    afterConfirmClosed(function () { modal(els.savedModal)?.show(); });
                });
            });
        }
    });

    // ------------------------------------------------------------------ templates

    function renderTemplates() {
        els.templateList.innerHTML = TEMPLATES.map(function (template) {
            return '<li class="tbx-row tbx-row--two-line pg-pick-row">'
                + '<button type="button" class="pg-pick" data-template="' + template.id + '">'
                + '<span class="tbx-row-title">' + escapeHtml(t(template.name)) + '</span>'
                + '<span class="tbx-row-sub">' + escapeHtml(t(template.hint)) + '</span>'
                + '</button></li>';
        }).join('');
    }

    els.templateList.addEventListener('click', function (event) {
        const button = event.target.closest('[data-template]');
        const template = button && TEMPLATES.find(function (x) { return x.id === button.dataset.template; });
        if (!template) return;
        replaceFrom(els.templatesModal, t(template.name), function () {
            setWork({ name: '', html: template.html, css: template.css, js: template.js, libs: { builtin: template.libs, urls: [] } }, null);
            setTab('html', false);
            saveDraft();
            run();
        });
    });

    // ------------------------------------------------------------------ download, share, new tab

    function download() {
        const work = current();
        const title = work.name || t('Name.Default');
        const libs = libsForRun();
        // Shipped libraries go into the file so it also works without internet.
        Promise.all(libs.map(function (lib) {
            if (new URL(lib.url).origin !== window.location.origin) return Promise.resolve(lib);
            return fetch(lib.url, { credentials: 'same-origin' }).then(function (response) {
                if (!response.ok) throw new Error(String(response.status));
                return response.text();
            }).then(function (body) { return { kind: lib.kind, text: body }; });
        })).then(function (inlined) {
            const built = sandbox.build({ html: work.html, css: work.css, js: work.js, title: title, libs: inlined, bridge: null });
            const blob = new Blob([built.doc], { type: 'text/html;charset=utf-8' });
            const url = URL.createObjectURL(blob);
            const link = document.createElement('a');
            const fileName = slug(title) + '.html';
            link.href = url;
            link.download = fileName;
            document.body.appendChild(link);
            link.click();
            link.remove();
            window.setTimeout(function () { URL.revokeObjectURL(url); }, 10000);
            notify(fmt(t('Download.Done'), fileName), 'success');
        }).catch(function () {
            notify(t('Download.Failed'), 'error');
        });
    }

    function toBase64Url(bytes) {
        let binary = '';
        for (let i = 0; i < bytes.length; i += 0x8000) {
            binary += String.fromCharCode.apply(null, bytes.subarray(i, i + 0x8000));
        }
        return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
    }

    function fromBase64Url(value) {
        const base = value.replace(/-/g, '+').replace(/_/g, '/');
        const binary = atob(base + '==='.slice((base.length + 3) % 4));
        const bytes = new Uint8Array(binary.length);
        for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
        return bytes;
    }

    // "z" + deflate-raw when the browser has CompressionStream, "u" + plain UTF-8 otherwise.
    function encodeShare(work) {
        const json = JSON.stringify({ v: 1, n: work.name, h: work.html, c: work.css, j: work.js, l: { b: work.libs.builtin, u: work.libs.urls } });
        const bytes = new TextEncoder().encode(json);
        if (!window.CompressionStream) {
            return Promise.resolve('u' + toBase64Url(bytes));
        }
        const stream = new Blob([bytes]).stream().pipeThrough(new CompressionStream('deflate-raw'));
        return new Response(stream).arrayBuffer().then(function (buffer) {
            return 'z' + toBase64Url(new Uint8Array(buffer));
        });
    }

    function decodeShare(value) {
        const kind = value.charAt(0);
        const bytes = fromBase64Url(value.slice(1));
        const parse = function (textValue) {
            const data = JSON.parse(textValue);
            if (!data || data.v !== 1) throw new Error('version');
            const work = {
                name: String(data.n || ''), html: String(data.h || ''), css: String(data.c || ''), js: String(data.j || ''),
                libs: cleanLibs({ builtin: data.l && data.l.b, urls: data.l && data.l.u })
            };
            return work;
        };
        if (kind === 'u') {
            return Promise.resolve(parse(new TextDecoder().decode(bytes)));
        }
        if (kind !== 'z' || !window.DecompressionStream) {
            return Promise.reject(new Error('format'));
        }
        const stream = new Blob([bytes]).stream().pipeThrough(new DecompressionStream('deflate-raw'));
        return new Response(stream).text().then(parse);
    }

    function share() {
        encodeShare(current()).then(function (encoded) {
            const link = window.location.origin + window.location.pathname + '#code=' + encoded;
            if (link.length > (config.maxShareLength || 30000)) {
                notify(fmt(t('Share.TooLong'), link.length.toLocaleString(document.documentElement.lang || undefined)), 'warning');
                return;
            }
            copyText(link).then(function (ok) {
                notify(ok ? t('Share.Copied') : t('Share.Failed'), ok ? 'success' : 'error');
            });
        }).catch(function () {
            notify(t('Share.Failed'), 'error');
        });
    }

    function openSharedFromLink() {
        const hash = window.location.hash || '';
        if (hash.indexOf('#code=') !== 0) {
            return Promise.resolve(false);
        }

        const clearHash = function () {
            window.history.replaceState(null, '', window.location.pathname + window.location.search);
        };
        return decodeShare(hash.slice(6)).then(function (work) {
            return confirmReplace(work.name).then(function (ok) {
                clearHash();
                if (!ok) return false;
                setWork(work, null);
                saveDraft();
                notify(fmt(t('Share.Opened'), work.name || t('Name.Default')), 'success');
                return true;
            });
        }).catch(function () {
            clearHash();
            notify(t('Share.Invalid'), 'error');
            return false;
        });
    }

    function openInTab() {
        const tab = window.open(config.runUrl, '_blank');
        if (!tab) {
            notify(t('PopupBlocked'), 'warning');
            return;
        }
        state.openedTabs.add(tab);
    }

    // ------------------------------------------------------------------ layout

    function setLayout(layout) {
        state.layout = layout === 'stack' ? 'stack' : 'split';
        els.work.dataset.layout = state.layout;
        root.querySelectorAll('[data-pg-layout]').forEach(function (button) {
            const on = button.dataset.pgLayout === state.layout;
            button.setAttribute('aria-pressed', on ? 'true' : 'false');
            button.classList.toggle('active', on);
        });
        els.splitter.setAttribute('aria-orientation', state.layout === 'split' ? 'vertical' : 'horizontal');
        editor.refresh();
    }

    function setRatio(ratio) {
        state.ratio = Math.min(0.8, Math.max(0.2, ratio));
        els.work.style.setProperty('--pg-ratio', String(state.ratio));
        els.splitter.setAttribute('aria-valuenow', String(Math.round(state.ratio * 100)));
    }

    function setDevice(device) {
        state.device = ['desktop', '768', '375'].indexOf(device) >= 0 ? device : 'desktop';
        els.frameWrap.dataset.device = state.device;
        root.querySelectorAll('[data-pg-device]').forEach(function (button) {
            const on = button.dataset.pgDevice === state.device;
            button.setAttribute('aria-pressed', on ? 'true' : 'false');
            button.classList.toggle('active', on);
        });
    }

    function setMobile(view) {
        state.mobile = view;
        els.work.dataset.mobile = view;
        root.querySelectorAll('[data-pg-mobile]').forEach(function (button) {
            button.setAttribute('aria-pressed', button.dataset.pgMobile === view ? 'true' : 'false');
        });
        if (view === 'code') editor.refresh();
    }

    let dragging = false;
    els.splitter.addEventListener('pointerdown', function (event) {
        dragging = true;
        els.splitter.setPointerCapture(event.pointerId);
        els.work.classList.add('is-resizing');
        event.preventDefault();
    });
    els.splitter.addEventListener('pointermove', function (event) {
        if (!dragging) return;
        const rect = els.work.getBoundingClientRect();
        setRatio(state.layout === 'split'
            ? (event.clientX - rect.left) / rect.width
            : (event.clientY - rect.top) / rect.height);
    });
    function endDrag(event) {
        if (!dragging) return;
        dragging = false;
        if (els.splitter.hasPointerCapture(event.pointerId)) els.splitter.releasePointerCapture(event.pointerId);
        els.work.classList.remove('is-resizing');
        writePrefs();
        editor.refresh();
    }
    els.splitter.addEventListener('pointerup', endDrag);
    els.splitter.addEventListener('pointercancel', endDrag);
    // Arrow keys move the splitter (a widget's own keys, not a page shortcut).
    els.splitter.addEventListener('keydown', function (event) {
        const step = { ArrowLeft: -0.05, ArrowUp: -0.05, ArrowRight: 0.05, ArrowDown: 0.05 }[event.key];
        if (step === undefined && event.key !== 'Home' && event.key !== 'End') return;
        event.preventDefault();
        setRatio(event.key === 'Home' ? 0.2 : event.key === 'End' ? 0.8 : state.ratio + step);
        writePrefs();
        editor.refresh();
    });

    function fullscreenOn() {
        return document.fullscreenElement === els.preview || els.preview.classList.contains('is-fullscreen');
    }

    function updateFullscreenLabel() {
        els.fullscreenText.textContent = fullscreenOn() ? t('ExitFullscreen') : t('Fullscreen');
    }

    function toggleFullscreen() {
        if (document.fullscreenElement === els.preview) {
            document.exitFullscreen();
        } else if (els.preview.classList.contains('is-fullscreen')) {
            els.preview.classList.remove('is-fullscreen');
            updateFullscreenLabel();
        } else if (els.preview.requestFullscreen) {
            els.preview.requestFullscreen().catch(function () {
                els.preview.classList.add('is-fullscreen');
                updateFullscreenLabel();
            });
        } else {
            // Phones without the Fullscreen API: cover the window instead.
            els.preview.classList.add('is-fullscreen');
            updateFullscreenLabel();
        }
    }

    document.addEventListener('fullscreenchange', updateFullscreenLabel);

    // ------------------------------------------------------------------ wiring

    editor.on('changes', function () {
        updateDots();
        if (state.tab === 'js') checkSyntaxSoon();
        if (state.loading) return;
        updateStatus();
        saveDraftSoon();
        runSoon();
    });
    editor.on('cursorActivity', updateCursor);
    const checkSyntaxSoon = debounce(checkSyntax, 400);

    els.name.addEventListener('input', function () {
        updateStatus();
        saveDraftSoon();
    });

    els.tabs.forEach(function (button) {
        button.addEventListener('click', function () { setTab(button.dataset.pgTab, true); });
    });
    // Left and right arrows move between the tabs (the tab list's own keys).
    root.querySelector('.pg-tabs').addEventListener('keydown', function (event) {
        if (event.key !== 'ArrowLeft' && event.key !== 'ArrowRight') return;
        event.preventDefault();
        const index = TABS.indexOf(state.tab);
        const next = TABS[(index + (event.key === 'ArrowRight' ? 1 : TABS.length - 1)) % TABS.length];
        setTab(next, false);
        root.querySelector('[data-pg-tab="' + next + '"]').focus();
    });

    els.run.addEventListener('click', run);
    root.querySelector('[data-pg-reload]').addEventListener('click', run);
    els.autorun.addEventListener('change', function () {
        state.autorun = els.autorun.checked;
        writePrefs();
        if (state.autorun) run();
    });
    els.format.addEventListener('click', format);
    els.libs.addEventListener('click', openLibs);
    root.querySelector('[data-pg-new]').addEventListener('click', function () { modal(els.templatesModal)?.show(); });
    root.querySelector('[data-pg-save]').addEventListener('click', saveCurrent);
    root.querySelector('[data-pg-save-as]').addEventListener('click', function () { openNameModal('saveAs', null); });
    root.querySelector('[data-pg-saved]').addEventListener('click', function () {
        renderSaved();
        modal(els.savedModal)?.show();
    });
    root.querySelector('[data-pg-download]').addEventListener('click', download);
    root.querySelector('[data-pg-share]').addEventListener('click', share);
    root.querySelector('[data-pg-open-tab]').addEventListener('click', openInTab);
    els.fullscreen.addEventListener('click', toggleFullscreen);
    root.querySelectorAll('[data-pg-layout]').forEach(function (button) {
        button.addEventListener('click', function () { setLayout(button.dataset.pgLayout); writePrefs(); });
    });
    root.querySelectorAll('[data-pg-device]').forEach(function (button) {
        button.addEventListener('click', function () { setDevice(button.dataset.pgDevice); writePrefs(); });
    });
    root.querySelectorAll('[data-pg-mobile]').forEach(function (button) {
        button.addEventListener('click', function () { setMobile(button.dataset.pgMobile); });
    });
    root.querySelectorAll('[data-pg-filter]').forEach(function (button) {
        button.addEventListener('click', function () { setFilter(button.dataset.pgFilter); });
    });
    root.querySelector('[data-pg-console-clear]').addEventListener('click', clearConsole);
    els.consoleToggle.addEventListener('click', function () {
        setConsoleOpen(!state.consoleOpen);
        writePrefs();
    });
    narrow.addEventListener('change', function () { editor.refresh(); });

    if (window.Toolbox?.keys) {
        const keys = window.Toolbox.keys;
        keys.add({ keys: 'mod+enter', label: t('Run'), inInputs: true, button: els.run, run: run });
        keys.add({ keys: 'mod+s', label: t('Save'), inInputs: true, run: saveCurrent });
        keys.add({ keys: 'alt+shift+f', label: t('Format'), inInputs: true, button: els.format, run: format });
        keys.add({
            keys: ['alt+1', 'alt+2', 'alt+3'],
            label: t('Keys.Tabs'),
            inInputs: true,
            run: function (event) { setTab(TABS[Number(event.key) - 1] || 'html', true); }
        });
        keys.add({
            keys: 'escape',
            label: t('ExitFullscreen'),
            hidden: true,
            when: function () { return els.preview.classList.contains('is-fullscreen'); },
            run: toggleFullscreen
        });
    }

    // ------------------------------------------------------------------ start

    const prefs = readPrefs();
    state.autorun = prefs.autorun !== false;
    els.autorun.checked = state.autorun;
    setLayout(prefs.layout);
    setRatio(typeof prefs.ratio === 'number' ? prefs.ratio : 0.5);
    setDevice(prefs.device);
    setConsoleOpen(prefs.consoleOpen !== false);
    setMobile('code');
    renderTemplates();

    const store = readStore();
    if (store.draft && validWork(store.draft)) {
        setWork(store.draft, store.draft.itemId && store.items.some(function (x) { return x.id === store.draft.itemId; }) ? store.draft.itemId : null);
        state.draftSavedAt = store.draft.savedAt || null;
    } else {
        const first = TEMPLATES.find(function (x) { return x.id === 'metric'; });
        setWork({ name: '', html: first.html, css: first.css, js: first.js, libs: { builtin: first.libs, urls: [] } }, null);
    }
    setTab(TABS.indexOf(prefs.tab) >= 0 ? prefs.tab : 'html', false);
    els.name.placeholder = t('Name.Default');
    updateStatus(store);

    openSharedFromLink().then(function () {
        run();
    });
})();
