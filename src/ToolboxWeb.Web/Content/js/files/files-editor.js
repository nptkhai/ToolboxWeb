// Editing in "Files & folders" (stage B). One CodeMirror 5 editor (theme cm-s-toolkit) moves
// between tabs; each tab keeps its own CodeMirror.Doc, so undo history survives tab switches.
// - View | Edit per tab; Markdown edits side by side with a live preview that scrolls along.
// - Unsaved changes: a dot on the tab, "Unsaved changes" in the header, a question before a tab
//   or the window closes; drafts kept in IndexedDB every couple of seconds and offered back when
//   the file is opened again (after a crash, a closed window…).
// - Save (Ctrl+S, or automatically after 2 s when "Autosave" is on) writes through POST
//   /Files/Save with the hash the file was read with: a change made elsewhere meanwhile asks
//   Reload / Overwrite / Compare. The file's encoding and line ending are kept by the server.
// - Open files are checked every 5 s (and when the window gets focus) for outside changes.
(function (window, document) {
    'use strict';

    const NS = window.ToolboxFiles = window.ToolboxFiles || {};
    const P = NS.page;
    if (!P || !window.CodeMirror) {
        return;
    }

    const { text, urls, els, config } = P;
    const CM = window.CodeMirror;
    const PREFS_KEY = 'toolbox.files.prefs.v1';
    const POLL_MS = 5000;
    const DRAFT_MS = 1500;
    const AUTOSAVE_MS = 2000;
    const head = {
        mode: document.querySelector('[data-fx-mode]'),
        modeButtons: Array.from(document.querySelectorAll('[data-fx-mode-btn]')),
        save: document.querySelector('[data-fx-save]'),
        discard: document.querySelector('[data-fx-discard]'),
        previewToggle: document.querySelector('[data-fx-preview-toggle]'),
        saveState: document.querySelector('[data-fx-save-state]')
    };
    const prefs = readPrefs();
    let editor = null;
    let editorHost = null;
    let previewTimer = 0;
    let statusTimer = 0;
    let autosaveTimer = 0;
    let syncLock = 0;

    // ------------------------------------------------------------------ helpers

    function readPrefs() {
        try {
            return Object.assign({ autosave: false, split: true }, JSON.parse(window.localStorage.getItem(PREFS_KEY) || '{}'));
        } catch (error) {
            return { autosave: false, split: true };
        }
    }

    function savePrefs() {
        try {
            window.localStorage.setItem(PREFS_KEY, JSON.stringify(prefs));
        } catch (error) {
            // Remembered for this page only.
        }
    }

    function time(date) {
        return window.Toolbox?.format?.time ? window.Toolbox.format.time(date) : date.toLocaleTimeString().slice(0, 5);
    }

    function isNarrow() {
        return window.matchMedia('(max-width: 899.98px)').matches;
    }

    function canEdit(tab) {
        const doc = tab?.doc;
        return Boolean(doc && typeof doc.text === 'string' && doc.editable && !doc.plain && !tab.error);
    }

    function isDirty(tab) {
        return Boolean(tab?.cmDoc && !tab.cmDoc.isClean(tab.cleanGen));
    }

    function currentText(tab) {
        return tab?.cmDoc ? tab.cmDoc.getValue() : tab?.doc?.text;
    }

    const MODES = {
        md: { name: 'yaml-frontmatter', base: 'gfm' }, markdown: { name: 'yaml-frontmatter', base: 'gfm' },
        json: { name: 'javascript', json: true }, jsonc: { name: 'javascript', json: true }, webmanifest: { name: 'javascript', json: true },
        js: 'javascript', mjs: 'javascript', cjs: 'javascript', jsx: 'javascript', ts: { name: 'javascript', typescript: true }, tsx: { name: 'javascript', typescript: true },
        css: 'css', scss: 'text/x-scss', less: 'text/x-less',
        html: 'htmlmixed', htm: 'htmlmixed', cshtml: 'htmlmixed', razor: 'htmlmixed', vue: 'htmlmixed',
        xml: 'xml', svg: 'xml', xaml: 'xml', csproj: 'xml', props: 'xml', targets: 'xml', config: 'xml', resx: 'xml', nuspec: 'xml',
        yml: 'yaml', yaml: 'yaml', sql: 'text/x-sql', sh: 'shell', bash: 'shell', zsh: 'shell', ps1: 'powershell', psm1: 'powershell',
        py: 'python', cs: 'text/x-csharp', java: 'text/x-java', kt: 'text/x-kotlin', c: 'text/x-csrc', h: 'text/x-csrc',
        cpp: 'text/x-c++src', hpp: 'text/x-c++src', go: 'text/x-go', ini: 'properties', properties: 'properties', env: 'properties',
        editorconfig: 'properties', gitignore: 'properties', cfg: 'properties', toml: 'toml', diff: 'diff', patch: 'diff'
    };

    function modeFor(name) {
        const ext = NS.viewers.extension(name);
        return MODES[ext] || (name.toLowerCase().startsWith('.env') ? 'properties' : null);
    }

    function isMarkdown(tab) {
        return tab?.doc?.kind === 'markdown';
    }

    // ------------------------------------------------------------------ drafts (IndexedDB)

    const drafts = (function () {
        let dbPromise = null;
        function open() {
            if (!window.indexedDB) return Promise.reject(new Error('no indexedDB'));
            dbPromise = dbPromise || new Promise((resolve, reject) => {
                const request = window.indexedDB.open('toolbox-files', 1);
                request.onupgradeneeded = () => request.result.createObjectStore('drafts');
                request.onsuccess = () => resolve(request.result);
                request.onerror = () => reject(request.error);
            });
            return dbPromise;
        }
        function key(tab) {
            return `${config.stateKey}|${tab.rootId}|${tab.path}`;
        }
        function run(mode, work) {
            return open().then((db) => new Promise((resolve, reject) => {
                const tx = db.transaction('drafts', mode);
                const result = work(tx.objectStore('drafts'));
                tx.oncomplete = () => resolve(result && 'result' in result ? result.result : undefined);
                tx.onerror = () => reject(tx.error);
            })).catch(() => undefined);
        }
        return {
            get: (tab) => run('readonly', (store) => store.get(key(tab))),
            put: (tab, value) => run('readwrite', (store) => store.put(value, key(tab))),
            remove: (tab) => run('readwrite', (store) => store.delete(key(tab)))
        };
    })();

    function scheduleDraft(tab) {
        window.clearTimeout(tab.draftTimer);
        tab.draftTimer = window.setTimeout(() => {
            if (isDirty(tab)) {
                drafts.put(tab, { text: tab.cmDoc.getValue(), baseHash: tab.doc.hash, savedAt: Date.now() });
            } else {
                drafts.remove(tab);
            }
        }, DRAFT_MS);
    }

    /** Looks for a draft once per loaded file; showDraftBanner() then shows it on every redraw until answered. */
    async function offerDraft(tab) {
        if (!canEdit(tab) || isDirty(tab)) return;
        if (tab.draftChecked !== tab.doc) {
            tab.draftChecked = tab.doc;
            const draft = await drafts.get(tab);
            if (draft && typeof draft.text === 'string' && draft.text !== tab.doc.text) {
                tab.pendingDraft = draft;
            } else {
                tab.pendingDraft = null;
                if (draft) drafts.remove(tab);
            }
        }
        showDraftBanner(tab);
    }

    function showDraftBanner(tab) {
        const draft = tab.pendingDraft;
        if (!draft || P.state.active !== tab.key || els.body.querySelector('.fx-draft')) return;
        const banner = document.createElement('div');
        banner.className = 'alert alert-warning fx-draft';
        banner.setAttribute('role', 'status');
        const message = draft.baseHash === tab.doc.hash ? text['Draft.Found'] : text['Draft.FoundChanged'];
        banner.innerHTML = `${P.icon('history')}<span>${P.escapeHtml(P.fmt(message, time(new Date(draft.savedAt))))}</span>`
            + `<span class="fx-draft-actions"><button type="button" class="btn btn-sm btn-primary" data-draft="restore">${P.escapeHtml(text['Draft.Restore'])}</button>`
            + `<button type="button" class="btn btn-sm btn-outline-secondary" data-draft="drop">${P.escapeHtml(text['Draft.Drop'])}</button></span>`;
        banner.addEventListener('click', (event) => {
            const action = event.target.closest('[data-draft]')?.dataset.draft;
            if (!action) return;
            banner.remove();
            tab.pendingDraft = null;
            if (action === 'drop') {
                drafts.remove(tab);
                return;
            }
            ensureDoc(tab);
            tab.cmDoc.setValue(draft.text);
            tab.mode = 'edit';
            changed(tab);
            P.showDoc(tab);
            P.notify(text['Draft.Restored'], 'info');
        });
        els.body.prepend(banner);
    }

    // ------------------------------------------------------------------ editor

    function ensureDoc(tab) {
        if (tab.cmDoc && tab.cmDocFor === tab.doc) return tab.cmDoc;
        if (tab.cmDoc && isDirty(tab)) {
            // The file was reloaded under unsaved changes: keep the changes, take the new base.
            tab.cmDocFor = tab.doc;
            return tab.cmDoc;
        }
        tab.cmDoc = CM.Doc(tab.doc.text.replace(/\r\n?/g, '\n'), modeFor(tab.doc.name));
        tab.cmDocFor = tab.doc;
        tab.cleanGen = tab.cmDoc.changeGeneration(true);
        return tab.cmDoc;
    }

    function ensureEditor() {
        if (editor) return editor;
        editorHost = document.createElement('div');
        editorHost.className = 'fx-editor-host';
        editor = CM(editorHost, {
            theme: 'toolkit',
            lineNumbers: true,
            lineWrapping: true,
            indentUnit: 4,
            tabSize: 4,
            matchBrackets: true,
            autoCloseBrackets: true,
            styleActiveLine: true,
            phrases: config.phrases || {},
            extraKeys: {
                'Ctrl-H': 'replace',
                'Cmd-Alt-F': 'replace',
                'Ctrl-B': () => mdAction('bold'),
                'Cmd-B': () => mdAction('bold'),
                'Ctrl-I': () => mdAction('italic'),
                'Cmd-I': () => mdAction('italic'),
                'Ctrl-K': false,
                'Cmd-K': false,
                Tab: (cm) => (cm.somethingSelected() ? cm.indentSelection('add') : cm.replaceSelection(cm.getOption('indentWithTabs') ? '\t' : ' '.repeat(cm.getOption('indentUnit')), 'end')),
                'Shift-Tab': (cm) => cm.indentSelection('subtract')
            }
        });
        editor.getInputField().setAttribute('aria-label', text['Edit.Editor']);
        editor.on('changes', () => {
            const tab = P.activeTab();
            if (tab && tab.cmDoc === editor.getDoc()) changed(tab);
        });
        editor.on('cursorActivity', () => {
            window.clearTimeout(statusTimer);
            statusTimer = window.setTimeout(() => P.setStatus(P.activeTab()), 120);
        });
        editor.on('scroll', () => syncFromEditor());
        return editor;
    }

    function changed(tab) {
        const dirty = isDirty(tab);
        if (tab.wasDirty !== dirty) {
            tab.wasDirty = dirty;
            P.renderTabs();
        }
        updateSaveState(tab);
        scheduleDraft(tab);
        if (isMarkdown(tab)) schedulePreview(tab);
        window.clearTimeout(statusTimer);
        statusTimer = window.setTimeout(() => P.setStatus(tab), 250);
        window.clearTimeout(autosaveTimer);
        if (prefs.autosave && dirty) {
            autosaveTimer = window.setTimeout(() => {
                if (isDirty(tab) && !tab.saving && !tab.conflict) save(tab, { quiet: true });
            }, AUTOSAVE_MS);
        }
    }

    // ------------------------------------------------------------------ edit layout and preview

    function buildEdit(tab, ctx) {
        const markdown = isMarkdown(tab);
        const split = markdown && prefs.split;
        els.body.classList.add('is-fill');
        els.body.innerHTML = '';
        const wrap = document.createElement('div');
        wrap.className = 'fx-edit' + (split ? ' is-split' : '') + (markdown && !prefs.split && tab.previewOnly ? ' is-preview' : '');
        if (markdown) wrap.appendChild(mdToolbar());
        const panes = document.createElement('div');
        panes.className = 'fx-edit-panes';
        const editorPane = document.createElement('div');
        editorPane.className = 'fx-edit-pane';
        ensureEditor();
        editorPane.appendChild(editorHost);
        panes.appendChild(editorPane);
        if (markdown) {
            panes.appendChild(splitHandle(panes));
            const preview = document.createElement('div');
            preview.className = 'fx-preview';
            preview.setAttribute('aria-label', text['Edit.Preview']);
            preview.tabIndex = 0;
            preview.innerHTML = '<article class="fx-md" data-fx-preview></article>';
            preview.addEventListener('scroll', () => syncFromPreview(preview));
            panes.appendChild(preview);
        }
        wrap.appendChild(panes);
        els.body.appendChild(wrap);

        editor.swapDoc(ensureDoc(tab));
        editor.setOption('readOnly', false);
        editor.refresh();
        if (markdown) renderPreview(tab, ctx);
        window.setTimeout(() => {
            editor.refresh();
            if (tab.cursor) editor.getDoc().setCursor(tab.cursor);
            // Opened from the tree: keep the keyboard there (arrows, F2, Delete).
            if (!P.els.tree.contains(document.activeElement)) editor.focus();
        }, 0);
    }

    // The bar between the editor and the preview. The editor's share (20-80 %) is kept in the prefs.
    const SPLIT_MIN = 20;
    const SPLIT_MAX = 80;

    function splitShare() {
        const value = Number(prefs.splitShare);
        return Number.isFinite(value) && value >= SPLIT_MIN && value <= SPLIT_MAX ? value : 50;
    }

    function applySplit(panes, handle, share) {
        const value = Math.min(SPLIT_MAX, Math.max(SPLIT_MIN, Math.round(share)));
        panes.style.setProperty('--fx-split-left', `${value}fr`);
        panes.style.setProperty('--fx-split-right', `${100 - value}fr`);
        handle.setAttribute('aria-valuenow', String(value));
        prefs.splitShare = value;
    }

    let splitRefresh = 0;
    function refreshAfterSplit() {
        window.cancelAnimationFrame(splitRefresh);
        splitRefresh = window.requestAnimationFrame(() => editor?.refresh());
    }

    function splitHandle(panes) {
        const handle = document.createElement('div');
        handle.className = 'fx-split-handle';
        handle.tabIndex = 0;
        handle.setAttribute('role', 'separator');
        handle.setAttribute('aria-orientation', 'vertical');
        handle.setAttribute('aria-label', text['Edit.Resize']);
        handle.setAttribute('aria-valuemin', String(SPLIT_MIN));
        handle.setAttribute('aria-valuemax', String(SPLIT_MAX));
        handle.title = text['Edit.Resize'];
        applySplit(panes, handle, splitShare());

        handle.addEventListener('pointerdown', (event) => {
            if (event.button !== 0) return;
            event.preventDefault();
            handle.setPointerCapture(event.pointerId);
            handle.classList.add('is-dragging');
            const box = panes.getBoundingClientRect();
            const move = (e) => {
                applySplit(panes, handle, ((e.clientX - box.left) / box.width) * 100);
                refreshAfterSplit();
            };
            const up = () => {
                handle.classList.remove('is-dragging');
                handle.removeEventListener('pointermove', move);
                handle.removeEventListener('pointerup', up);
                handle.removeEventListener('pointercancel', up);
                savePrefs();
                editor?.refresh();
            };
            handle.addEventListener('pointermove', move);
            handle.addEventListener('pointerup', up);
            handle.addEventListener('pointercancel', up);
        });
        handle.addEventListener('dblclick', () => {
            applySplit(panes, handle, 50);
            savePrefs();
            editor?.refresh();
        });
        handle.addEventListener('keydown', (event) => {
            const steps = { ArrowLeft: -5, ArrowRight: 5, Home: SPLIT_MIN - 100, End: 100 };
            if (!(event.key in steps)) return;
            event.preventDefault();
            applySplit(panes, handle, splitShare() + steps[event.key]);
            savePrefs();
            editor?.refresh();
        });
        return handle;
    }

    NS.onLayout = () => refreshAfterSplit();

    function schedulePreview(tab) {
        window.clearTimeout(previewTimer);
        previewTimer = window.setTimeout(() => renderPreview(tab), 250);
    }

    function renderPreview(tab, ctx) {
        const article = els.body.querySelector('[data-fx-preview]');
        if (!article || P.activeTab() !== tab) return;
        const context = ctx || P.viewContext(tab);
        const preview = article.parentElement;
        const keep = preview.scrollTop;
        NS.markdown.render(article, tab.cmDoc.getValue(), {
            path: tab.path,
            text,
            rawUrl: (path) => context.rawUrl(path),
            openPath: (path, anchor) => context.openPath(path, anchor)
        });
        preview.scrollTop = keep;
    }

    function blocksOf(preview) {
        return Array.from(preview.querySelectorAll('[data-line]')).map((el) => ({ el, line: Number(el.dataset.line) }));
    }

    function syncFromEditor() {
        const preview = els.body.querySelector('.fx-edit.is-split .fx-preview');
        if (!preview || Date.now() < syncLock) return;
        const scroll = editor.getScrollInfo();
        if (scroll.top + scroll.clientHeight >= scroll.height - 2) {
            syncLock = Date.now() + 120;
            preview.scrollTop = preview.scrollHeight;
            return;
        }
        const line = editor.lineAtHeight(scroll.top, 'local');
        const blocks = blocksOf(preview);
        let before = null;
        let after = null;
        for (const block of blocks) {
            if (block.line <= line) before = block;
            else { after = block; break; }
        }
        if (!before) {
            syncLock = Date.now() + 120;
            preview.scrollTop = 0;
            return;
        }
        const top = (el) => el.getBoundingClientRect().top - preview.getBoundingClientRect().top + preview.scrollTop;
        let target = top(before.el);
        if (after) {
            const startY = editor.heightAtLine(before.line, 'local');
            const endY = editor.heightAtLine(after.line, 'local');
            const ratio = endY > startY ? Math.min(1, Math.max(0, (scroll.top - startY) / (endY - startY))) : 0;
            target += (top(after.el) - top(before.el)) * ratio;
        }
        syncLock = Date.now() + 120;
        preview.scrollTop = Math.max(0, target - 8);
    }

    function syncFromPreview(preview) {
        if (Date.now() < syncLock || !editor) return;
        const blocks = blocksOf(preview);
        const viewTop = preview.getBoundingClientRect().top;
        let current = null;
        for (const block of blocks) {
            if (block.el.getBoundingClientRect().top - viewTop <= 8) current = block;
            else break;
        }
        syncLock = Date.now() + 120;
        editor.scrollTo(null, current ? editor.heightAtLine(current.line, 'local') : 0);
    }

    // ------------------------------------------------------------------ Markdown toolbar

    const MD_ACTIONS = [
        ['bold', 'bold', 'Md.Bold', 'Ctrl+B'], ['italic', 'italic', 'Md.Italic', 'Ctrl+I'], ['strike', 'strikethrough', 'Md.Strike'], '|',
        ['h1', 'heading-1', 'Md.H1'], ['h2', 'heading-2', 'Md.H2'], ['h3', 'heading-3', 'Md.H3'], '|',
        ['quote', 'quote', 'Md.Quote'], ['code', 'code-xml', 'Md.Code'], ['list', 'list', 'Md.List'], ['ordered', 'list-ordered', 'Md.OrderedList'],
        ['task', 'square-check', 'Md.Checklist'], '|', ['link', 'link', 'Md.Link'], ['image', 'image-plus', 'Md.Image'], ['rule', 'minus', 'Md.Rule']
    ];

    function mdToolbar() {
        const bar = document.createElement('div');
        bar.className = 'fx-md-toolbar';
        bar.setAttribute('role', 'toolbar');
        bar.setAttribute('aria-label', text['Md.Toolbar']);
        bar.innerHTML = MD_ACTIONS.map((item) => (item === '|'
            ? '<span class="fx-md-sep" aria-hidden="true"></span>'
            : `<button type="button" class="fx-md-btn" data-md="${item[0]}" title="${P.escapeHtml(text[item[2]] + (item[3] ? ` (${item[3]})` : ''))}" aria-label="${P.escapeHtml(text[item[2]])}">${P.icon(item[1])}</button>`)).join('');
        bar.addEventListener('mousedown', (event) => event.preventDefault());
        bar.addEventListener('click', (event) => {
            const action = event.target.closest('[data-md]')?.dataset.md;
            if (action) mdAction(action);
        });
        return bar;
    }

    function wrapSelection(cm, before, after, placeholder) {
        const doc = cm.getDoc();
        const selections = doc.listSelections();
        doc.replaceSelections(doc.getSelections().map((value) => {
            if (value.startsWith(before) && value.endsWith(after) && value.length >= before.length + after.length) {
                return value.slice(before.length, value.length - after.length);
            }
            return before + (value || placeholder) + after;
        }), 'around');
        if (selections.length === 1 && selections[0].empty()) {
            const cursor = doc.getCursor('from');
            doc.setSelection({ line: cursor.line, ch: cursor.ch + before.length }, { line: cursor.line, ch: cursor.ch + before.length + placeholder.length });
        }
    }

    function prefixLines(cm, prefix, numbered) {
        const doc = cm.getDoc();
        const from = doc.getCursor('from').line;
        const to = doc.getCursor('to').line;
        cm.operation(() => {
            let n = 1;
            for (let line = from; line <= to; line++) {
                const value = doc.getLine(line);
                const stripped = value.replace(/^(#{1,6}\s+|>\s?|[-*+]\s+\[[ xX]\]\s+|[-*+]\s+|\d+\.\s+)/, '');
                const marker = numbered ? `${n++}. ` : prefix;
                const has = numbered ? /^\d+\.\s+/.test(value) : value.startsWith(prefix);
                doc.replaceRange(has ? stripped : marker + stripped, { line, ch: 0 }, { line, ch: value.length });
            }
        });
    }

    function mdAction(action) {
        const tab = P.activeTab();
        if (!editor || !tab || tab.mode !== 'edit' || !isMarkdown(tab)) return CM.Pass;
        const cm = editor;
        switch (action) {
            case 'bold': wrapSelection(cm, '**', '**', text['Md.Bold']); break;
            case 'italic': wrapSelection(cm, '*', '*', text['Md.Italic']); break;
            case 'strike': wrapSelection(cm, '~~', '~~', text['Md.Strike']); break;
            case 'h1': prefixLines(cm, '# '); break;
            case 'h2': prefixLines(cm, '## '); break;
            case 'h3': prefixLines(cm, '### '); break;
            case 'quote': prefixLines(cm, '> '); break;
            case 'list': prefixLines(cm, '- '); break;
            case 'ordered': prefixLines(cm, '', true); break;
            case 'task': prefixLines(cm, '- [ ] '); break;
            case 'code': {
                const value = cm.getSelection();
                if (value.includes('\n')) cm.replaceSelection('```\n' + value + (value.endsWith('\n') ? '' : '\n') + '```\n');
                else wrapSelection(cm, '`', '`', 'code');
                break;
            }
            case 'link': {
                const value = cm.getSelection() || text['Md.LinkText'];
                cm.replaceSelection(`[${value}](https://)`);
                const cursor = cm.getCursor();
                cm.setSelection({ line: cursor.line, ch: cursor.ch - 9 }, { line: cursor.line, ch: cursor.ch - 1 });
                break;
            }
            case 'image': {
                const value = cm.getSelection() || text['Md.ImageText'];
                cm.replaceSelection(`![${value}](images/)`);
                const cursor = cm.getCursor();
                cm.setCursor({ line: cursor.line, ch: cursor.ch - 1 });
                break;
            }
            case 'rule': {
                const cursor = cm.getCursor();
                const lineText = cm.getLine(cursor.line);
                cm.replaceRange((lineText ? '\n\n' : '\n') + '---\n\n', { line: cursor.line, ch: lineText.length });
                break;
            }
            default: return CM.Pass;
        }
        cm.focus();
        return undefined;
    }

    // ------------------------------------------------------------------ hooks into files.js

    NS.isDirty = isDirty;
    // A kept screen with unsaved files asks before it is closed (screens.js).
    window.Toolbox?.pendingChanges?.add(() => P.state.tabs.filter((tab) => isDirty(tab)).length);
    NS.currentText = currentText;

    NS.onDeactivate = (tab) => {
        if (editor && tab.cmDoc && editor.getDoc() === tab.cmDoc) {
            tab.cursor = tab.cmDoc.getCursor();
        }
    };

    /** Shows the tab when the editor has a say: edit mode, or a view of unsaved text. */
    NS.showDoc = (tab, elements, ctx) => {
        if (!tab.doc) return false;
        if (tab.mode === 'edit' && !canEdit(tab)) tab.mode = 'view';
        if (tab.cmDoc && tab.cmDocFor !== tab.doc) ensureDoc(tab);
        if (tab.mode === 'edit') {
            buildEdit(tab, ctx);
            updateHead(tab);
            P.reveal(tab.rootId, tab.path);
            return true;
        }
        if (isDirty(tab)) {
            // Viewing text that is not saved yet: show what is in the editor.
            const view = Object.assign({}, tab.doc, { text: tab.cmDoc.getValue() });
            tab.view?.destroy?.();
            tab.view = NS.viewers.render(elements.body, view, ctx);
            updateHead(tab);
            P.reveal(tab.rootId, tab.path);
            return true;
        }
        window.setTimeout(() => offerDraft(tab), 0);
        return false;
    };

    NS.afterHead = (tab) => updateHead(tab);

    NS.statusFor = (tab) => {
        if (!tab?.doc || typeof tab.doc.text !== 'string') return null;
        const value = currentText(tab);
        const stats = NS.viewers.textStats(value);
        const parts = [
            P.escapeHtml(tab.doc.encoding.toUpperCase()) + (tab.doc.bom ? ' ' + P.escapeHtml(text['Doc.Bom']) : ''),
            P.escapeHtml(tab.doc.lineEnding),
            P.escapeHtml(P.fmt(text['Doc.Lines'], P.number(stats.lines))),
            P.escapeHtml(P.fmt(text['Doc.Words'], P.number(stats.words))),
            P.escapeHtml(P.fmt(text['Doc.Chars'], P.number(stats.chars)))
        ];
        if (tab.mode === 'edit' && editor && editor.getDoc() === tab.cmDoc) {
            const cursor = tab.cmDoc.getCursor();
            parts.push(P.escapeHtml(P.fmt(text['Edit.Cursor'], P.number(cursor.line + 1), P.number(cursor.ch + 1))));
        }
        const editing = tab.mode === 'edit';
        const modeText = !canEdit(tab) ? text['Doc.ReadOnly'] : editing ? text['Edit.Mode'] : text['Doc.View'];
        parts.push(`<span class="fx-status-mode${canEdit(tab) ? '' : ' is-readonly'}" title="${P.escapeHtml(tab.doc.readOnlyReason || '')}">${P.escapeHtml(modeText)}</span>`);
        if (tab.doc.plain) parts.push(P.escapeHtml(text['Doc.Plain']));
        let html = parts.join('<span class="fx-meta-sep" aria-hidden="true">·</span>');
        if (canEdit(tab)) {
            html += `<label class="fx-autosave"><input type="checkbox" class="form-check-input" data-fx-autosave${prefs.autosave ? ' checked' : ''}><span>${P.escapeHtml(text['Edit.Autosave'])}</span></label>`;
        }
        return html;
    };

    els.status.addEventListener('change', (event) => {
        if (!event.target.matches('[data-fx-autosave]')) return;
        prefs.autosave = event.target.checked;
        savePrefs();
        const tab = P.activeTab();
        if (prefs.autosave && tab && isDirty(tab)) changed(tab);
    });

    function updateSaveState(tab) {
        if (!head.saveState) return;
        if (!tab || !tab.doc) {
            head.saveState.textContent = '';
            head.saveState.className = 'fx-save-state';
            return;
        }
        if (tab.saving) {
            head.saveState.textContent = text['Edit.Saving'];
            head.saveState.className = 'fx-save-state';
        } else if (isDirty(tab)) {
            head.saveState.innerHTML = `<span class="fx-dot" aria-hidden="true"></span>${P.escapeHtml(text['Edit.Unsaved'])}`;
            head.saveState.className = 'fx-save-state is-dirty';
        } else if (tab.savedAt) {
            head.saveState.textContent = P.fmt(text['Edit.SavedAt'], time(tab.savedAt));
            head.saveState.className = 'fx-save-state is-saved';
        } else {
            head.saveState.textContent = '';
            head.saveState.className = 'fx-save-state';
        }
        if (head.save) head.save.disabled = !isDirty(tab) || Boolean(tab.saving);
        if (head.discard) head.discard.disabled = !isDirty(tab);
    }

    function updateHead(tab) {
        const editable = canEdit(tab);
        const editing = editable && tab.mode === 'edit';
        head.mode.hidden = !editable;
        head.modeButtons.forEach((button) => {
            const on = button.dataset.fxModeBtn === (editing ? 'edit' : 'view');
            button.setAttribute('aria-pressed', on ? 'true' : 'false');
            button.classList.toggle('is-active', on);
        });
        head.save.hidden = !editable || (!editing && !isDirty(tab));
        head.discard.hidden = !editable || (!editing && !isDirty(tab));
        const showPreviewToggle = editing && isMarkdown(tab);
        head.previewToggle.hidden = !showPreviewToggle;
        head.previewToggle.setAttribute('aria-pressed', isNarrow() ? String(Boolean(tab.previewOnly)) : String(prefs.split));
        updateSaveState(tab);
    }

    function setMode(tab, mode) {
        if (!tab || !canEdit(tab) || tab.mode === mode) return;
        if (tab.mode === 'edit') NS.onDeactivate(tab);
        tab.mode = mode;
        P.showDoc(tab);
    }

    head.modeButtons.forEach((button) => button.addEventListener('click', () => setMode(P.activeTab(), button.dataset.fxModeBtn)));

    head.previewToggle.addEventListener('click', () => {
        const tab = P.activeTab();
        if (!tab) return;
        if (isNarrow()) {
            tab.previewOnly = !tab.previewOnly;
            const wrap = els.body.querySelector('.fx-edit');
            wrap?.classList.toggle('is-preview', tab.previewOnly);
            if (tab.previewOnly) renderPreview(tab);
            else editor?.refresh();
        } else {
            prefs.split = !prefs.split;
            savePrefs();
            NS.onDeactivate(tab);
            P.showDoc(tab);
        }
        updateHead(tab);
    });

    head.save.addEventListener('click', () => save(P.activeTab()));
    head.discard.addEventListener('click', () => discard(P.activeTab()));

    // ------------------------------------------------------------------ save, discard, conflicts

    async function save(tab, options) {
        if (!tab || !tab.cmDoc || tab.saving) return false;
        const settings = options || {};
        tab.saving = true;
        updateSaveState(tab);
        const value = tab.cmDoc.getValue();
        const generation = tab.cmDoc.changeGeneration();
        try {
            const saved = await P.request(urls.save, {
                root: tab.rootId,
                path: tab.path,
                text: value,
                expectedHash: tab.doc.hash,
                encoding: settings.encoding || tab.doc.encoding,
                bom: settings.encoding ? false : tab.doc.bom,
                lineEnding: tab.doc.lineEnding,
                force: Boolean(settings.force)
            });
            tab.doc = Object.assign({}, tab.doc, {
                text: value, hash: saved.hash, size: saved.size, modified: saved.modified,
                encoding: saved.encoding, bom: saved.bom, lineEnding: saved.lineEnding
            });
            tab.cmDocFor = tab.doc;
            tab.draftChecked = tab.doc;
            tab.cleanGen = generation;
            tab.savedAt = new Date();
            tab.conflict = false;
            tab.deleted = false;
            tab.saving = false;
            tab.wasDirty = isDirty(tab);
            drafts.remove(tab);
            if (isDirty(tab)) scheduleDraft(tab);
            P.renderTabs();
            if (P.activeTab() === tab) {
                P.renderDocHead(tab);
                P.setStatus(tab);
            }
            return true;
        } catch (error) {
            tab.saving = false;
            updateSaveState(tab);
            if (error.code === 'conflict') {
                tab.conflict = true;
                return conflict(tab);
            }
            if (error.code === 'deleted') {
                const again = await window.Toolbox.confirm({ title: text['Save.Deleted'], message: P.fmt(text['External.DeletedDirty'], tab.name), okText: text['Save.DeletedOk'], tone: 'primary' });
                return again ? save(tab, Object.assign({}, settings, { force: true })) : false;
            }
            if (error.code === 'encoding') {
                const convert = await window.Toolbox.confirm({ title: text['Save.EncodingTitle'], message: error.message, okText: text['Save.EncodingOk'], tone: 'primary' });
                return convert ? save(tab, Object.assign({}, settings, { encoding: 'utf-8' })) : false;
            }
            P.notify(error.message || text['Edit.SaveFailed'], 'error');
            return false;
        }
    }

    async function discard(tab) {
        if (!tab || !isDirty(tab)) return;
        const ok = await window.Toolbox.confirm({ title: text['Edit.DiscardTitle'], message: P.fmt(text['Edit.DiscardMessage'], tab.name), okText: text['Edit.DiscardOk'] });
        if (!ok) return;
        tab.cmDoc.setValue(tab.doc.text.replace(/\r\n?/g, '\n'));
        tab.cleanGen = tab.cmDoc.changeGeneration(true);
        changed(tab);
        drafts.remove(tab);
        P.notify(text['Edit.Discarded'], 'info');
        P.showDoc(tab);
    }

    let conflictResolve = null;
    let conflictTab = null;

    function conflict(tab) {
        conflictTab = tab;
        const dialog = P.moveToBody('fx-conflict');
        dialog.querySelector('[data-fx-conflict-message]').textContent = P.fmt(text['Save.Conflict'], tab.name);
        P.modal('fx-conflict').show();
        return new Promise((resolve) => { conflictResolve = resolve; });
    }

    function settleConflict(value) {
        const resolve = conflictResolve;
        conflictResolve = null;
        resolve?.(value);
    }

    async function reloadFromDisk(tab) {
        tab.cmDoc = null;
        tab.cmDocFor = null;
        tab.conflict = false;
        tab.wasDirty = false;
        drafts.remove(tab);
        await P.loadDoc(tab);
    }

    async function showCompare(tab) {
        let disk;
        try {
            disk = await P.request(urls.read, null, { root: tab.rootId, path: tab.path });
        } catch (error) {
            P.notify(error.message, 'error');
            return;
        }
        const body = P.moveToBody('fx-compare').querySelector('[data-fx-compare-body]');
        const mine = tab.cmDoc.getValue();
        const theirs = (disk.text || '').replace(/\r\n?/g, '\n');
        const parts = window.Diff ? window.Diff.diffLines(theirs, mine) : [{ value: mine }];
        let left = 0;
        let right = 0;
        const rows = [];
        parts.forEach((part) => {
            const lines = part.value.replace(/\n$/, '').split('\n');
            lines.forEach((line) => {
                if (part.added) rows.push(`<tr class="is-added"><td class="fx-cmp-n"></td><td class="fx-cmp-n">${++right}</td><td class="fx-cmp-sign">+</td><td><code>${P.escapeHtml(line)}</code></td></tr>`);
                else if (part.removed) rows.push(`<tr class="is-removed"><td class="fx-cmp-n">${++left}</td><td class="fx-cmp-n"></td><td class="fx-cmp-sign">−</td><td><code>${P.escapeHtml(line)}</code></td></tr>`);
                else rows.push(`<tr><td class="fx-cmp-n">${++left}</td><td class="fx-cmp-n">${++right}</td><td class="fx-cmp-sign"></td><td><code>${P.escapeHtml(line)}</code></td></tr>`);
            });
        });
        const same = parts.every((p) => !p.added && !p.removed);
        body.innerHTML = `<p class="fx-cmp-legend"><span class="fx-cmp-key is-removed">− ${P.escapeHtml(text['Compare.Disk'])}</span><span class="fx-cmp-key is-added">+ ${P.escapeHtml(text['Compare.Mine'])}</span></p>`
            + (same ? `<p>${P.escapeHtml(text['Compare.Same'])}</p>` : `<div class="fx-cmp-wrap"><table class="fx-cmp">${rows.join('')}</table></div>`);
        P.modal('fx-compare').show();
    }

    document.addEventListener('click', async (event) => {
        const action = event.target.closest('[data-fx-conflict]')?.dataset.fxConflict;
        if (!action || !conflictTab) return;
        const tab = conflictTab;
        if (action === 'compare') {
            P.modal('fx-conflict').hide();
            showCompare(tab);
            return;
        }
        P.modal('fx-conflict').hide();
        P.modal('fx-compare').hide();
        conflictTab = null;
        if (action === 'reload') {
            await reloadFromDisk(tab);
            settleConflict(false);
        } else if (action === 'overwrite') {
            const ok = await save(tab, { force: true });
            settleConflict(ok);
        }
    });

    ['fx-conflict', 'fx-compare'].forEach((id) => {
        document.getElementById(id)?.addEventListener('hidden.bs.modal', () => {
            window.setTimeout(() => {
                if (!document.querySelector('#fx-conflict.show, #fx-compare.show') && conflictResolve) {
                    conflictTab = null;
                    settleConflict(false);
                }
            }, 50);
        });
    });

    // ------------------------------------------------------------------ closing with unsaved changes

    NS.beforeClose = (tab) => {
        if (!isDirty(tab)) {
            drafts.remove(tab);
            return Promise.resolve(true);
        }
        const dialog = P.moveToBody('fx-unsaved');
        dialog.querySelector('[data-fx-unsaved-message]').textContent = P.fmt(text['Unsaved.Message'], tab.name);
        return new Promise((resolve) => {
            let answer = false;
            const onClick = async (event) => {
                const choice = event.target.closest('[data-fx-unsaved]')?.dataset.fxUnsaved;
                if (!choice) return;
                if (choice === 'save') {
                    P.modal('fx-unsaved').hide();
                    answer = await save(tab);
                    finish();
                } else {
                    drafts.remove(tab);
                    answer = true;
                    P.modal('fx-unsaved').hide();
                }
            };
            let done = false;
            const finish = () => {
                if (done) return;
                done = true;
                dialog.removeEventListener('click', onClick);
                resolve(answer);
            };
            dialog.addEventListener('click', onClick);
            dialog.addEventListener('hidden.bs.modal', () => window.setTimeout(() => { if (!tab.saving) finish(); }, 0), { once: true });
            P.modal('fx-unsaved').show();
        });
    };

    window.addEventListener('beforeunload', (event) => {
        if (P.state.tabs.some(isDirty)) {
            P.state.tabs.filter(isDirty).forEach((tab) => drafts.put(tab, { text: tab.cmDoc.getValue(), baseHash: tab.doc.hash, savedAt: Date.now() }));
            event.preventDefault();
            event.returnValue = text['Unsaved.Leave'] || '';
        }
    });

    // ------------------------------------------------------------------ outside changes

    async function checkTab(tab) {
        if (!tab?.doc || tab.saving || tab.loading || tab.checking) return;
        tab.checking = true;
        try {
            const info = await P.request(urls.info, null, { root: tab.rootId, path: tab.path });
            if (!info) {
                if (tab.deleted) return;
                tab.deleted = true;
                if (isDirty(tab)) {
                    P.notify(P.fmt(text['External.DeletedDirty'], tab.name), 'warning');
                } else {
                    P.notify(P.fmt(text['External.Deleted'], tab.name), 'warning');
                    P.closeTab(tab.key, true);
                }
                return;
            }
            tab.deleted = false;
            const changedOutside = info.size !== tab.doc.size || new Date(info.modified).getTime() !== new Date(tab.doc.modified).getTime();
            if (!changedOutside || tab.conflict) return;
            if (!isDirty(tab)) {
                const mode = tab.mode;
                await reloadFromDisk(tab);
                tab.mode = mode;
                if (P.activeTab() === tab) P.showDoc(tab);
                P.notify(P.fmt(text['External.Reloaded'], tab.name), 'info');
            } else if (P.activeTab() === tab) {
                tab.conflict = true;
                await conflict(tab);
            } else {
                tab.pendingConflict = true;
            }
        } catch (error) {
            // Root unavailable or offline: tried again on the next tick.
        } finally {
            tab.checking = false;
        }
    }

    function checkOpenTabs() {
        if (!window.Toolbox.isScreenVisible()) return;
        const active = P.activeTab();
        if (active?.pendingConflict) {
            active.pendingConflict = false;
            active.conflict = true;
            conflict(active);
            return;
        }
        P.state.tabs.filter((t) => t.doc).forEach(checkTab);
    }

    NS.checkOpenTabs = checkOpenTabs;

    /** Shows line <paramref name="line"/> (1-based): the editor's cursor when editing, else the closest block. */
    NS.goToLine = (tab, line) => {
        const index = Math.max(0, (Number(line) || 1) - 1);
        if (tab.mode === 'edit' && editor && editor.getDoc() === tab.cmDoc) {
            window.setTimeout(() => {
                editor.setCursor({ line: index, ch: 0 });
                editor.scrollIntoView({ line: index, ch: 0 }, editor.getScrollInfo().clientHeight / 2);
                editor.addLineClass(index, 'background', 'fx-line-flash');
                window.setTimeout(() => editor.removeLineClass(index, 'background', 'fx-line-flash'), 1600);
            }, 30);
            return;
        }
        const gutter = els.body.querySelector('.fx-gutter');
        if (gutter) {
            const height = parseFloat(getComputedStyle(gutter).lineHeight) || 20;
            els.body.scrollTop = Math.max(0, index * height - els.body.clientHeight / 3);
            return;
        }
        const blocks = Array.from(els.body.querySelectorAll('[data-line]')).filter((el) => Number(el.dataset.line) <= index);
        const block = blocks[blocks.length - 1];
        if (block) {
            block.scrollIntoView({ block: 'center' });
            block.classList.add('fx-block-flash');
            window.setTimeout(() => block.classList.remove('fx-block-flash'), 1600);
        }
    };

    /** Inserts text at the cursor of the active editor (paste image, …). */
    NS.insertText = (value) => {
        if (!editor) return;
        editor.replaceSelection(value);
        editor.focus();
    };

    NS.editorFor = (tab) => (editor && tab?.cmDoc && editor.getDoc() === tab.cmDoc ? editor : null);
    window.setInterval(checkOpenTabs, POLL_MS);
    window.addEventListener('focus', checkOpenTabs);

    // ------------------------------------------------------------------ shortcuts

    const keys = window.Toolbox?.keys;
    if (keys) {
        keys.add({
            keys: 'mod+s', label: text['Keys.Save'], inInputs: true, button: head.save,
            // Always taken on this page, so the browser's own "Save page" never opens.
            when: () => Boolean(P.activeTab()),
            run: () => {
                const tab = P.activeTab();
                if (isDirty(tab)) save(tab);
            }
        });
        keys.add({
            keys: 'e', label: text['Keys.Mode'],
            when: () => canEdit(P.activeTab()),
            run: () => {
                const tab = P.activeTab();
                setMode(tab, tab.mode === 'edit' ? 'view' : 'edit');
            }
        });
    }
})(window, document);
