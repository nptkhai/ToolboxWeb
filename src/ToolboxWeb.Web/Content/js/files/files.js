// "Files & folders" (/files.html, Views/Files/Index.cshtml). Roots and their tree on the left,
// open files in tabs on the right; everything is read through FilesController (JSON), which keeps
// paths inside their root. What the page remembers (open tabs, the active one, expanded folders,
// the tree width) stays in this browser under config.stateKey; the roots are on the server.
// Viewers are in files-viewers.js, Markdown in files-markdown.js.
(function (window, document) {
    'use strict';

    const page = document.querySelector('[data-files-page]');
    const NS = window.ToolboxFiles = window.ToolboxFiles || {};
    if (!page || !NS.viewers) {
        return;
    }

    const config = readJson('files-config') || {};
    const text = config.text || {};
    const urls = config.urls || {};
    const $ = (selector, scope) => (scope || document).querySelector(selector);
    const els = {
        welcome: $('[data-fx-welcome]'),
        shell: $('[data-fx-shell]'),
        side: $('[data-fx-side]'),
        tree: $('[data-fx-tree]'),
        stats: $('[data-fx-stats]'),
        find: $('[data-fx-find]'),
        findResults: $('[data-fx-find-results]'),
        resizer: $('[data-fx-resizer]'),
        tabs: $('[data-fx-tabs]'),
        start: $('[data-fx-start]'),
        doc: $('[data-fx-doc]'),
        crumbs: $('[data-fx-crumbs]'),
        meta: $('[data-fx-meta]'),
        body: $('[data-fx-body]'),
        status: $('[data-fx-status]'),
        copy: $('[data-fx-copy]'),
        print: $('[data-fx-print]'),
        saveCopy: $('[data-fx-save-copy]'),
        download: $('[data-fx-download]')
    };
    const KIND_ICONS = {
        folder: 'folder', markdown: 'file-text', json: 'file-json', csv: 'file-spreadsheet', image: 'file-image',
        pdf: 'file-text', office: 'file-text', binary: 'file-archive', text: 'file-code'
    };
    const MAX_TABS = 20;

    const state = {
        roots: [],
        nodes: new Map(),
        tabs: [],
        active: null,
        saved: readState(),
        showHidden: false
    };

    // ------------------------------------------------------------------ helpers

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

    function icon(name, size) {
        return window.Toolbox?.icon ? window.Toolbox.icon(name, size || 16) : '';
    }

    function notify(message, type) {
        if (message) window.Toolbox?.notify(message, type);
    }

    function number(value, decimals) {
        return window.Toolbox?.format ? window.Toolbox.format.number(value, decimals || 0) : String(value);
    }

    function bytes(value) {
        const n = Number(value) || 0;
        if (n < 1024) return `${number(n)} B`;
        if (n < 1024 * 1024) return `${number(n / 1024, 1)} KB`;
        if (n < 1024 * 1024 * 1024) return `${number(n / 1024 / 1024, 1)} MB`;
        return `${number(n / 1024 / 1024 / 1024, 1)} GB`;
    }

    function when(iso) {
        const date = iso ? new Date(iso) : null;
        if (!date || Number.isNaN(date.getTime())) return '';
        return window.Toolbox?.format ? window.Toolbox.format.dateTime(date) : date.toLocaleString();
    }

    function keyOf(rootId, path) {
        return `${rootId}:${path || ''}`;
    }

    function parentPath(path) {
        const at = String(path || '').lastIndexOf('/');
        return at < 0 ? '' : path.slice(0, at);
    }

    function baseName(path) {
        const value = String(path || '');
        return value.slice(value.lastIndexOf('/') + 1);
    }

    async function request(url, body, params) {
        let target = url;
        if (params) {
            const query = new URLSearchParams();
            Object.keys(params).forEach((k) => {
                if (params[k] !== undefined && params[k] !== null) query.set(k, params[k]);
            });
            target += (url.includes('?') ? '&' : '?') + query.toString();
        }
        const response = await fetch(target, {
            method: body ? 'POST' : 'GET',
            headers: Object.assign(
                { 'X-Requested-With': 'XMLHttpRequest' },
                body ? { 'Content-Type': 'application/json', RequestVerificationToken: config.token || '' } : {}),
            body: body ? JSON.stringify(body) : undefined
        });
        const result = await response.json().catch(() => null);
        if (!response.ok || !result || !result.success) {
            const error = new Error(result?.message || text['Doc.LoadFailed']);
            error.code = result?.code || String(response.status);
            throw error;
        }
        return result.data;
    }

    function rawUrl(rootId, path, download) {
        const query = new URLSearchParams({ root: rootId, path: path || '' });
        if (download) query.set('download', 'true');
        return `${urls.raw}?${query.toString()}`;
    }

    function moveToBody(id) {
        const modal = document.getElementById(id);
        if (modal && !modal.parentElement.classList.contains('tbx-modal-host')) {
            const host = document.createElement('div');
            host.className = 'content-area tbx-modal-host';
            document.body.appendChild(host);
            host.appendChild(modal);
        }
        return modal;
    }

    function modal(id) {
        return window.bootstrap?.Modal.getOrCreateInstance(moveToBody(id));
    }

    // ------------------------------------------------------------------ remembered state

    function readState() {
        try {
            const saved = JSON.parse(window.localStorage.getItem(config.stateKey) || 'null');
            return saved && typeof saved === 'object' ? saved : {};
        } catch (error) {
            return {};
        }
    }

    let saveTimer = 0;
    function saveState(now) {
        window.clearTimeout(saveTimer);
        if (now === true) {
            writeState();
            return;
        }
        saveTimer = window.setTimeout(writeState, 200);
    }

    function writeState() {
        const expanded = [];
        state.nodes.forEach((node, key) => {
            if (node.isFolder && node.expanded) expanded.push(key);
        });
        const value = {
            tabs: state.tabs.map((t) => ({ rootId: t.rootId, path: t.path })),
            active: state.active,
            expanded,
            sideWidth: state.saved.sideWidth,
            sideHidden: Boolean(state.saved.sideHidden),
            showHidden: state.showHidden
        };
        state.saved = value;
        try {
            window.localStorage.setItem(config.stateKey, JSON.stringify(value));
        } catch (error) {
            // Private window or full storage: the page still works, it just forgets.
        }
    }

    window.addEventListener('pagehide', () => saveState(true));

    // ------------------------------------------------------------------ menu (roots now, files later)

    let openMenu = null;

    function closeMenu() {
        if (openMenu) {
            openMenu.remove();
            openMenu = null;
            document.removeEventListener('mousedown', onMenuOutside, true);
        }
    }

    function onMenuOutside(event) {
        if (openMenu && !openMenu.contains(event.target)) closeMenu();
    }

    /** A small menu at an element or a point: items = [{ label, icon, run, danger, disabled } | 'separator']. */
    function showMenu(anchor, items, point) {
        closeMenu();
        const menu = document.createElement('div');
        menu.className = 'fx-menu';
        menu.setAttribute('role', 'menu');
        items.forEach((item) => {
            if (item === 'separator') {
                menu.insertAdjacentHTML('beforeend', '<div class="fx-menu-sep" role="separator"></div>');
                return;
            }
            const button = document.createElement('button');
            button.type = 'button';
            button.className = 'fx-menu-item' + (item.danger ? ' is-danger' : '');
            button.setAttribute('role', 'menuitem');
            button.disabled = Boolean(item.disabled);
            button.innerHTML = `${icon(item.icon || 'chevron-right')}<span>${escapeHtml(item.label)}</span>`;
            button.addEventListener('click', () => {
                closeMenu();
                item.run();
            });
            menu.appendChild(button);
        });
        menu.addEventListener('keydown', (event) => {
            const buttons = Array.from(menu.querySelectorAll('.fx-menu-item:not(:disabled)'));
            const at = buttons.indexOf(document.activeElement);
            if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
                event.preventDefault();
                const next = event.key === 'ArrowDown' ? (at + 1) % buttons.length : (at - 1 + buttons.length) % buttons.length;
                buttons[next]?.focus();
            } else if (event.key === 'Escape' || event.key === 'Tab') {
                event.preventDefault();
                closeMenu();
                anchor?.focus();
            }
        });
        document.body.appendChild(menu);
        const rect = point ? { left: point.x, right: point.x, top: point.y, bottom: point.y } : anchor.getBoundingClientRect();
        const width = menu.offsetWidth;
        const height = menu.offsetHeight;
        const left = Math.min(Math.max(8, point ? rect.left : rect.right - width), window.innerWidth - width - 8);
        const top = rect.bottom + height + 8 > window.innerHeight ? Math.max(8, rect.top - height - 4) : rect.bottom + 4;
        menu.style.left = `${left + window.scrollX}px`;
        menu.style.top = `${top + window.scrollY}px`;
        openMenu = menu;
        document.addEventListener('mousedown', onMenuOutside, true);
        menu.querySelector('.fx-menu-item:not(:disabled)')?.focus();
    }

    NS.showMenu = showMenu;

    // ------------------------------------------------------------------ roots

    async function loadRoots() {
        try {
            state.roots = await request(urls.roots);
        } catch (error) {
            state.roots = [];
            notify(error.message, 'error');
        }
        const hasRoots = state.roots.length > 0;
        els.welcome.hidden = hasRoots;
        els.shell.hidden = !hasRoots;
        renderTree();
        loadStats();
        NS.onRoots?.(state.roots);
    }

    async function addRoot() {
        if (config.hasWindowsDialog) {
            try {
                const answer = await request(urls.pickFolder, {});
                if (answer?.path) await saveRoot(answer.path);
                return;
            } catch (error) {
                // Fall back to the picker in the page.
            }
        }
        openPicker(text['Picker.Title'], (path) => saveRoot(path));
    }

    async function saveRoot(path) {
        try {
            const root = await request(urls.addRoot, { path });
            notify(fmt(text['Root.Added'], root.name), 'success');
            await loadRoots();
            const node = state.nodes.get(keyOf(root.id, ''));
            if (node) await toggleFolder(node, true);
            return true;
        } catch (error) {
            notify(error.message, 'error');
            return false;
        }
    }

    function rootMenu(root, anchor, point) {
        const index = state.roots.findIndex((r) => r.id === root.id);
        const node = state.nodes.get(keyOf(root.id, ''));
        const extra = root.available && node && NS.folderItems ? NS.folderItems(node).concat(['separator']) : [];
        showMenu(anchor, extra.concat([
            { label: text['Root.Rename'], icon: 'pencil', run: () => renameRoot(root) },
            { label: text['Root.Relink'], icon: 'folder-sync', run: () => relinkRoot(root) },
            { label: text['Root.Up'], icon: 'arrow-up', disabled: index <= 0, run: () => moveRoot(root, -1) },
            { label: text['Root.Down'], icon: 'arrow-down', disabled: index >= state.roots.length - 1, run: () => moveRoot(root, 1) },
            'separator',
            { label: text['Root.Remove'], icon: 'x', danger: true, run: () => removeRoot(root) }
        ]), point);
    }

    function renameRoot(root) {
        const dialog = moveToBody('fx-rename-root');
        const form = dialog.querySelector('[data-fx-rename-root-form]');
        const input = dialog.querySelector('[data-fx-root-name]');
        const error = dialog.querySelector('[data-fx-root-name-error]');
        input.value = root.name;
        error.textContent = '';
        dialog.querySelector('[data-fx-root-path]').textContent = root.path;
        form.onsubmit = async (event) => {
            event.preventDefault();
            if (!input.value.trim()) {
                error.textContent = text['Root.NameRequired'] || '';
                input.focus();
                return;
            }
            try {
                await request(urls.renameRoot, { id: root.id, name: input.value.trim() });
                modal('fx-rename-root').hide();
                notify(text['Root.Renamed'], 'success');
                await loadRoots();
                refreshTabs();
            } catch (failure) {
                error.textContent = failure.message;
            }
        };
        dialog.addEventListener('shown.bs.modal', () => input.select(), { once: true });
        modal('fx-rename-root').show();
    }

    function relinkRoot(root) {
        openPicker(fmt(text['Picker.RelinkTitle'], root.name), async (path) => {
            try {
                await request(urls.relinkRoot, { id: root.id, path });
                notify(fmt(text['Root.Relinked'], root.name), 'success');
                await loadRoots();
                return true;
            } catch (error) {
                notify(error.message, 'error');
                return false;
            }
        }, root.path);
    }

    async function moveRoot(root, offset) {
        try {
            state.roots = await request(urls.moveRoot, { id: root.id, offset });
            renderTree();
        } catch (error) {
            notify(error.message, 'error');
        }
    }

    async function removeRoot(root) {
        const ok = await window.Toolbox.confirm({
            title: text['Root.RemoveTitle'],
            message: fmt(text['Root.RemoveMessage'], root.name, root.path),
            okText: text['Root.RemoveOk']
        });
        if (!ok) return;
        try {
            await request(urls.removeRoot, { id: root.id });
            state.tabs.filter((t) => t.rootId === root.id).forEach((t) => closeTab(t.key, true));
            notify(fmt(text['Root.Removed'], root.name), 'success');
            await loadRoots();
        } catch (error) {
            notify(error.message, 'error');
        }
    }

    async function loadStats() {
        const available = state.roots.filter((r) => r.available);
        if (!available.length) {
            els.stats.textContent = '';
            return;
        }
        const results = await Promise.all(available.map((r) => request(urls.stats, null, { root: r.id, hidden: state.showHidden }).catch(() => null)));
        const total = results.filter(Boolean).reduce((sum, s) => ({
            files: sum.files + s.files, folders: sum.folders + s.folders, bytes: sum.bytes + s.bytes, partial: sum.partial || s.partial
        }), { files: 0, folders: 0, bytes: 0, partial: false });
        els.stats.textContent = fmt(total.partial ? text['Tree.StatsPartial'] : text['Tree.Stats'], number(total.files), number(total.folders), bytes(total.bytes));
    }

    // ------------------------------------------------------------------ folder picker in the page

    let pickerDone = null;
    let pickerPath = null;

    function openPicker(title, done, start) {
        const dialog = moveToBody('fx-picker');
        pickerDone = done;
        dialog.querySelector('[data-fx-picker-title]').textContent = title;
        dialog.querySelector('[data-fx-picker-error]').textContent = '';
        dialog.querySelector('[data-fx-picker-input]').value = start || '';
        modal('fx-picker').show();
        browse(start ? parentOrSelf(start) : null);
    }

    function parentOrSelf(path) {
        return path;
    }

    async function browse(path) {
        const dialog = document.getElementById('fx-picker');
        const list = dialog.querySelector('[data-fx-picker-list]');
        const error = dialog.querySelector('[data-fx-picker-error]');
        list.innerHTML = `<li class="tbx-row fx-picker-note">${escapeHtml(text['Picker.Loading'])}</li>`;
        try {
            const view = await request(urls.browse, null, { path: path || '' });
            pickerPath = view.path;
            error.textContent = '';
            dialog.querySelector('[data-fx-picker-current]').textContent = view.path || text['Picker.Start'];
            dialog.querySelector('[data-fx-picker-up]').disabled = !view.path;
            dialog.querySelector('[data-fx-picker-up]').dataset.target = view.parent || '';
            dialog.querySelector('[data-fx-picker-choose]').disabled = !view.path;
            if (view.path) dialog.querySelector('[data-fx-picker-input]').value = view.path;
            list.innerHTML = view.folders.length
                ? view.folders.map((f) => `<li class="tbx-row fx-picker-item"><button type="button" class="fx-picker-open" data-path="${escapeHtml(f.path)}">${icon(view.path ? 'folder' : 'hard-drive')}<span>${escapeHtml(f.name)}</span></button></li>`).join('')
                : `<li class="tbx-row fx-picker-note">${escapeHtml(text['Picker.Empty'])}</li>`;
        } catch (failure) {
            error.textContent = failure.message;
            list.innerHTML = '';
        }
    }

    (function wirePicker() {
        const dialog = document.getElementById('fx-picker');
        if (!dialog) return;
        dialog.querySelector('[data-fx-picker-list]').addEventListener('click', (event) => {
            const button = event.target.closest('[data-path]');
            if (button) browse(button.dataset.path);
        });
        dialog.querySelector('[data-fx-picker-up]').addEventListener('click', (event) => {
            browse(event.currentTarget.dataset.target || null);
        });
        dialog.querySelector('[data-fx-picker-form]').addEventListener('submit', (event) => {
            event.preventDefault();
            const value = dialog.querySelector('[data-fx-picker-input]').value.trim();
            browse(value || null);
        });
        dialog.querySelector('[data-fx-picker-choose]').addEventListener('click', async () => {
            if (!pickerPath || !pickerDone) return;
            const ok = await pickerDone(pickerPath);
            if (ok !== false) modal('fx-picker').hide();
        });
    })();

    // ------------------------------------------------------------------ tree

    function nodeIcon(node) {
        if (node.isRoot) return node.available ? 'folder-tree' : 'triangle-alert';
        if (node.isFolder) return node.expanded ? 'folder-open' : 'folder';
        return KIND_ICONS[node.kind] || 'file';
    }

    function nodeRow(node) {
        const chevron = node.isFolder
            ? `<span class="fx-chevron" aria-hidden="true">${icon(node.expanded ? 'chevron-down' : 'chevron-right', 14)}</span>`
            : '<span class="fx-chevron" aria-hidden="true"></span>';
        const meta = node.isRoot
            ? (node.available ? '' : `<span class="fx-node-warn">${escapeHtml(text['Root.Unavailable'])}</span>`)
            : (node.isFolder ? '' : `<span class="fx-node-size">${escapeHtml(bytes(node.size))}</span>`);
        const menu = node.isRoot || NS.fileMenu
            ? `<button type="button" class="fx-node-menu" tabindex="-1" aria-label="${escapeHtml(fmt(text['Root.Menu'], node.name))}" data-fx-root-menu>${icon('ellipsis')}</button>`
            : '';
        return `<div class="fx-node${node.hidden ? ' is-hidden' : ''}" style="--fx-depth:${node.depth}">${chevron}<span class="fx-node-icon fx-kind-${node.isFolder ? 'folder' : node.kind}" aria-hidden="true">${icon(nodeIcon(node))}</span><span class="fx-node-name">${escapeHtml(node.name)}</span>${meta}${menu}</div>`;
    }

    function makeItem(node) {
        const li = document.createElement('li');
        li.setAttribute('role', 'treeitem');
        li.setAttribute('aria-level', String(node.depth + 1));
        li.setAttribute('aria-selected', 'false');
        li.tabIndex = -1;
        li.dataset.key = node.key;
        if (node.isFolder) li.setAttribute('aria-expanded', node.expanded ? 'true' : 'false');
        if (node.isRoot) {
            li.classList.add('fx-root');
            li.title = node.path;
        } else if (NS.fileMenu) {
            li.draggable = true;
        }
        li.innerHTML = nodeRow(node);
        node.el = li;
        if (node.isFolder) {
            const group = document.createElement('ul');
            group.setAttribute('role', 'group');
            group.hidden = !node.expanded;
            li.appendChild(group);
            node.group = group;
        }
        return li;
    }

    function updateRow(node) {
        const group = node.group;
        node.el.innerHTML = nodeRow(node);
        if (group) node.el.appendChild(group);
        if (node.isFolder) node.el.setAttribute('aria-expanded', node.expanded ? 'true' : 'false');
    }

    function renderTree() {
        const expanded = new Set(state.saved.expanded || []);
        state.nodes.clear();
        els.tree.innerHTML = '';
        state.roots.forEach((root) => {
            const node = {
                key: keyOf(root.id, ''), rootId: root.id, path: '', name: root.name, isFolder: true, isRoot: true,
                available: root.available, problem: root.problem, depth: 0, expanded: false, loaded: false, rootPath: root.path
            };
            state.nodes.set(node.key, node);
            els.tree.appendChild(makeItem(node));
            if (root.available && (expanded.has(node.key) || state.roots.length === 1)) {
                toggleFolder(node, true, expanded);
            }
        });
        const first = els.tree.querySelector('[role="treeitem"]');
        if (first) first.tabIndex = 0;
    }

    async function loadChildren(node, expanded) {
        node.group.innerHTML = `<li class="fx-tree-note" role="none">${escapeHtml(text['Tree.Loading'])}</li>`;
        try {
            const list = await request(urls.list, null, { root: node.rootId, path: node.path, hidden: state.showHidden });
            node.group.innerHTML = '';
            list.entries.forEach((entry) => {
                const child = {
                    key: keyOf(node.rootId, entry.path), rootId: node.rootId, path: entry.path, name: entry.name,
                    isFolder: entry.isFolder, kind: entry.kind, size: entry.size, modified: entry.modified, hidden: entry.hidden,
                    depth: node.depth + 1, expanded: false, loaded: false
                };
                state.nodes.set(child.key, child);
                node.group.appendChild(makeItem(child));
                if (child.isFolder && expanded && expanded.has(child.key)) {
                    toggleFolder(child, true, expanded);
                }
            });
            if (!list.entries.length) {
                node.group.innerHTML = `<li class="fx-tree-note" role="none" style="--fx-depth:${node.depth + 1}">${escapeHtml(text['Tree.Empty'])}</li>`;
            } else if (list.truncated) {
                node.group.insertAdjacentHTML('beforeend', `<li class="fx-tree-note" role="none" style="--fx-depth:${node.depth + 1}">${escapeHtml(fmt(text['Tree.More'], number(list.total - list.entries.length)))}</li>`);
            }
            node.loaded = true;
            applyFilter();
            markActiveInTree();
        } catch (error) {
            node.group.innerHTML = `<li class="fx-tree-note is-error" role="none" style="--fx-depth:${node.depth + 1}">${escapeHtml(error.message)}</li>`;
            if (node.isRoot && error.code === 'unavailable') {
                node.available = false;
                updateRow(node);
            }
        }
    }

    async function toggleFolder(node, open, expanded) {
        if (!node.isFolder) return;
        if (node.isRoot && !node.available) {
            const root = state.roots.find((r) => r.id === node.rootId);
            if (root) relinkRoot(root);
            return;
        }
        node.expanded = open === undefined ? !node.expanded : open;
        updateRow(node);
        node.group.hidden = !node.expanded;
        if (node.expanded && !node.loaded) {
            await loadChildren(node, expanded);
        }
        saveState();
    }

    /** Reads a folder of the tree again (after a change), keeping what was expanded under it. */
    async function reloadFolder(rootId, folder) {
        const node = state.nodes.get(keyOf(rootId, folder || ''));
        if (!node || !node.isFolder) return;
        if (!node.loaded || !node.expanded) {
            node.loaded = false;
            return;
        }
        const expanded = new Set();
        state.nodes.forEach((n, key) => {
            if (n.rootId === rootId && n.isFolder && n.expanded && key !== node.key) expanded.add(key);
        });
        const prefix = node.path ? `${node.path}/` : '';
        Array.from(state.nodes.keys()).forEach((key) => {
            const n = state.nodes.get(key);
            if (n.rootId === rootId && n !== node && (prefix === '' || n.path.startsWith(prefix))) state.nodes.delete(key);
        });
        await loadChildren(node, expanded);
    }

    /** After a rename or move: open tabs follow the file (or everything under a folder). */
    function retargetTabs(rootId, oldPath, newRootId, newPath) {
        let changed = false;
        state.tabs.forEach((tab) => {
            if (tab.rootId !== rootId) return;
            let path = null;
            if (tab.path === oldPath) path = newPath;
            else if (tab.path.startsWith(`${oldPath}/`)) path = newPath + tab.path.slice(oldPath.length);
            if (path === null) return;
            const wasActive = state.active === tab.key;
            tab.rootId = newRootId;
            tab.path = path;
            tab.name = baseName(path);
            tab.key = keyOf(newRootId, path);
            if (tab.doc) tab.doc = Object.assign({}, tab.doc, { path, name: tab.name, rootId: newRootId });
            if (wasActive) state.active = tab.key;
            changed = true;
        });
        if (changed) {
            renderTabs();
            const active = findTab(state.active);
            if (active) renderDocHead(active);
            saveState();
        }
    }

    /** Hides files of other kinds than state.filter (folders stay); "" shows everything. */
    function applyFilter() {
        const kind = state.filter || '';
        els.tree.classList.toggle('is-filtered', Boolean(kind));
        state.nodes.forEach((node) => {
            if (!node.el || node.isFolder) return;
            node.el.hidden = Boolean(kind) && node.kind !== kind;
        });
    }

    function collapseAll() {
        state.nodes.forEach((node) => {
            if (node.isFolder && node.expanded && !node.isRoot) {
                node.expanded = false;
                updateRow(node);
                node.group.hidden = true;
            }
        });
        saveState();
    }

    function refreshTree() {
        state.saved.expanded = Array.from(state.nodes.values()).filter((n) => n.isFolder && n.expanded).map((n) => n.key);
        loadRoots();
    }

    function visibleItems() {
        return Array.from(els.tree.querySelectorAll('[role="treeitem"]')).filter((li) => !li.closest('ul[hidden]'));
    }

    function focusItem(li) {
        if (!li) return;
        els.tree.querySelectorAll('[role="treeitem"][tabindex="0"]').forEach((x) => { x.tabIndex = -1; });
        li.tabIndex = 0;
        li.focus();
    }

    function activateNode(node) {
        if (node.isFolder) {
            toggleFolder(node);
        } else {
            openFile(node.rootId, node.path);
            if (window.matchMedia('(max-width: 899.98px)').matches) setSideOpen(false);
        }
    }

    els.tree.addEventListener('click', (event) => {
        const li = event.target.closest('[role="treeitem"]');
        if (!li) return;
        const node = state.nodes.get(li.dataset.key);
        if (!node) return;
        if (event.target.closest('[data-fx-root-menu]')) {
            const button = event.target.closest('[data-fx-root-menu]');
            if (node.isRoot) {
                const root = state.roots.find((r) => r.id === node.rootId);
                if (root) rootMenu(root, button);
            } else if (NS.fileMenu) {
                NS.fileMenu(node, button);
            }
            return;
        }
        event.stopPropagation();
        focusItem(li);
        activateNode(node);
    });

    els.tree.addEventListener('contextmenu', (event) => {
        const li = event.target.closest('[role="treeitem"]');
        const node = li && state.nodes.get(li.dataset.key);
        if (!node) return;
        if (node.isRoot) {
            event.preventDefault();
            const root = state.roots.find((r) => r.id === node.rootId);
            if (root) rootMenu(root, li, { x: event.clientX, y: event.clientY });
        } else if (NS.fileMenu) {
            event.preventDefault();
            NS.fileMenu(node, li, { x: event.clientX, y: event.clientY });
        }
    });

    els.tree.addEventListener('keydown', (event) => {
        const li = event.target.closest('[role="treeitem"]');
        if (!li) return;
        const node = state.nodes.get(li.dataset.key);
        const items = visibleItems();
        const at = items.indexOf(li);
        switch (event.key) {
            case 'ArrowDown': event.preventDefault(); focusItem(items[at + 1]); break;
            case 'ArrowUp': event.preventDefault(); focusItem(items[at - 1]); break;
            case 'Home': event.preventDefault(); focusItem(items[0]); break;
            case 'End': event.preventDefault(); focusItem(items[items.length - 1]); break;
            case 'ArrowRight':
                event.preventDefault();
                if (node.isFolder && !node.expanded) toggleFolder(node, true);
                else if (node.isFolder) focusItem(items[at + 1]);
                break;
            case 'ArrowLeft':
                event.preventDefault();
                if (node.isFolder && node.expanded) toggleFolder(node, false);
                else focusItem(li.parentElement.closest('[role="treeitem"]'));
                break;
            case 'Enter':
            case ' ':
                event.preventDefault();
                activateNode(node);
                break;
            case 'ContextMenu':
            case 'F10':
                if (event.key === 'F10' && !event.shiftKey) return;
                event.preventDefault();
                if (node.isRoot) {
                    const root = state.roots.find((r) => r.id === node.rootId);
                    if (root) rootMenu(root, li.querySelector('.fx-node'));
                } else if (NS.fileMenu) {
                    NS.fileMenu(node, li.querySelector('.fx-node'));
                }
                break;
            default:
                if (NS.treeKey) NS.treeKey(event, node);
        }
    });

    function markActiveInTree() {
        els.tree.querySelectorAll('[aria-selected="true"]').forEach((x) => x.setAttribute('aria-selected', 'false'));
        const node = state.active && state.nodes.get(state.active);
        if (node?.el) node.el.setAttribute('aria-selected', 'true');
    }

    /** Opens the folders down to a file and shows it in the tree. */
    async function reveal(rootId, path) {
        const parts = String(path || '').split('/');
        let current = '';
        const root = state.nodes.get(keyOf(rootId, ''));
        if (!root) return;
        if (!root.expanded || !root.loaded) await toggleFolder(root, true);
        for (let i = 0; i < parts.length - 1; i++) {
            current = current ? `${current}/${parts[i]}` : parts[i];
            const node = state.nodes.get(keyOf(rootId, current));
            if (!node) return;
            if (!node.expanded || !node.loaded) await toggleFolder(node, true);
        }
        markActiveInTree();
        state.nodes.get(keyOf(rootId, path))?.el?.scrollIntoView({ block: 'nearest' });
    }

    // ------------------------------------------------------------------ tabs

    function findTab(key) {
        return state.tabs.find((t) => t.key === key);
    }

    async function openFile(rootId, path, options) {
        const key = keyOf(rootId, path);
        let tab = findTab(key);
        if (!tab) {
            if (state.tabs.length >= MAX_TABS) {
                const oldest = state.tabs.find((t) => t.key !== state.active && !(NS.isDirty && NS.isDirty(t)));
                if (oldest) closeTab(oldest.key, true);
            }
            tab = { key, rootId, path, name: baseName(path), doc: null, view: null, loading: false, error: null };
            state.tabs.push(tab);
        }
        await activateTab(key, options);
        return tab;
    }

    function rootName(rootId) {
        return state.roots.find((r) => r.id === rootId)?.name || '';
    }

    function renderTabs() {
        els.tabs.innerHTML = state.tabs.map((tab) => {
            const active = tab.key === state.active;
            const dirty = NS.isDirty && NS.isDirty(tab);
            const kind = tab.doc?.kind || NS.kindOf?.(tab.name) || 'text';
            return `<div class="fx-tab${active ? ' is-active' : ''}${dirty ? ' is-dirty' : ''}" data-key="${escapeHtml(tab.key)}">`
                + `<button type="button" class="fx-tab-open" role="tab" id="fx-tab-${escapeHtml(btoa(unescape(encodeURIComponent(tab.key))).replace(/[^a-z0-9]/gi, ''))}" aria-selected="${active}" tabindex="${active ? 0 : -1}" title="${escapeHtml(rootName(tab.rootId) + ' / ' + tab.path)}">`
                + `<span class="fx-node-icon fx-kind-${escapeHtml(kind)}" aria-hidden="true">${icon(KIND_ICONS[kind] || 'file')}</span><span class="fx-tab-name">${escapeHtml(tab.name)}</span>`
                + `${dirty ? `<span class="fx-tab-dot" aria-label="${escapeHtml(text['Tabs.Unsaved'] || '')}"></span>` : ''}</button>`
                + `<button type="button" class="fx-tab-close" tabindex="-1" aria-label="${escapeHtml(fmt(text['Tabs.Close'], tab.name))}" data-fx-close>${icon('x', 14)}</button></div>`;
        }).join('');
        els.start.hidden = state.tabs.length > 0;
        els.doc.hidden = state.tabs.length === 0;
        els.tabs.querySelector('.fx-tab.is-active')?.scrollIntoView({ block: 'nearest', inline: 'nearest' });
    }

    function refreshTabs() {
        renderTabs();
        const tab = findTab(state.active);
        if (tab) renderDocHead(tab);
    }

    els.tabs.addEventListener('click', (event) => {
        const holder = event.target.closest('.fx-tab');
        if (!holder) return;
        if (event.target.closest('[data-fx-close]')) {
            closeTab(holder.dataset.key);
        } else {
            activateTab(holder.dataset.key);
        }
    });

    els.tabs.addEventListener('auxclick', (event) => {
        const holder = event.target.closest('.fx-tab');
        if (holder && event.button === 1) {
            event.preventDefault();
            closeTab(holder.dataset.key);
        }
    });

    els.tabs.addEventListener('keydown', (event) => {
        if (!['ArrowLeft', 'ArrowRight', 'Home', 'End', 'Delete'].includes(event.key)) return;
        const at = state.tabs.findIndex((t) => t.key === state.active);
        if (at < 0) return;
        event.preventDefault();
        if (event.key === 'Delete') {
            closeTab(state.active);
            return;
        }
        const next = event.key === 'Home' ? 0 : event.key === 'End' ? state.tabs.length - 1
            : (at + (event.key === 'ArrowRight' ? 1 : -1) + state.tabs.length) % state.tabs.length;
        activateTab(state.tabs[next].key).then(() => els.tabs.querySelector('.fx-tab.is-active .fx-tab-open')?.focus());
    });

    async function closeTab(key, force) {
        const tab = findTab(key);
        if (!tab) return;
        if (!force && NS.beforeClose && !(await NS.beforeClose(tab))) return;
        tab.view?.destroy?.();
        const at = state.tabs.indexOf(tab);
        state.tabs.splice(at, 1);
        if (state.active === key) {
            const next = state.tabs[at] || state.tabs[at - 1];
            state.active = next ? next.key : null;
            if (next) {
                await activateTab(next.key);
            } else {
                renderTabs();
                markActiveInTree();
            }
        } else {
            renderTabs();
        }
        saveState();
    }

    async function activateTab(key, options) {
        const tab = findTab(key);
        if (!tab) return;
        const previous = findTab(state.active);
        if (previous && previous !== tab) {
            previous.scroll = els.body.scrollTop;
            NS.onDeactivate?.(previous);
        }
        state.active = key;
        renderTabs();
        markActiveInTree();
        saveState();
        if (!tab.doc || options?.reload) {
            tab.pendingOptions = options;
            await loadDoc(tab);
        } else {
            showDoc(tab, options);
        }
    }

    async function loadDoc(tab) {
        tab.loading = true;
        renderDocHead(tab);
        els.body.innerHTML = `<div class="fx-loading" role="status"><span class="tbx-skeleton"></span><span class="tbx-skeleton"></span><span class="tbx-skeleton"></span><span class="visually-hidden">${escapeHtml(text['Doc.Loading'])}</span></div>`;
        try {
            tab.doc = await request(urls.read, null, { root: tab.rootId, path: tab.path });
            tab.error = null;
            tab.name = tab.doc.name;
        } catch (error) {
            tab.error = error;
        }
        tab.loading = false;
        const options = tab.pendingOptions;
        tab.pendingOptions = null;
        if (state.active === tab.key) showDoc(tab, options);
        renderTabs();
        NS.onOpened?.(tab);
    }

    NS.reloadTab = (tab) => loadDoc(tab);

    function renderDocHead(tab) {
        const root = state.roots.find((r) => r.id === tab.rootId);
        const parts = tab.path.split('/');
        const crumbs = [`<button type="button" class="fx-crumb" data-crumb="">${escapeHtml(root?.name || '')}</button>`];
        let current = '';
        parts.forEach((part, i) => {
            current = current ? `${current}/${part}` : part;
            crumbs.push(i === parts.length - 1
                ? `<span class="fx-crumb is-current" aria-current="page">${escapeHtml(part)}</span>`
                : `<button type="button" class="fx-crumb" data-crumb="${escapeHtml(current)}">${escapeHtml(part)}</button>`);
        });
        els.crumbs.innerHTML = crumbs.join(`<span class="fx-crumb-sep" aria-hidden="true">${icon('chevron-right', 14)}</span>`);
        els.crumbs.dataset.root = String(tab.rootId);

        const doc = tab.doc;
        if (!doc) {
            els.meta.textContent = '';
            NS.afterHead?.(tab);
            return;
        }
        const pieces = [
            `<span class="tbx-pill is-neutral">${escapeHtml(text['Kind.' + doc.kind] || doc.kind)}</span>`,
            `<span>${escapeHtml(bytes(doc.size))}</span>`,
            `<span>${escapeHtml(fmt(text['Doc.Modified'], when(doc.modified)))}</span>`
        ];
        if (doc.text !== null && doc.text !== undefined) {
            pieces.push(`<span>${escapeHtml(doc.encoding.toUpperCase())}${doc.bom ? ' ' + escapeHtml(text['Doc.Bom']) : ''} · ${escapeHtml(doc.lineEnding)}</span>`);
        }
        els.meta.innerHTML = pieces.join('<span class="fx-meta-sep" aria-hidden="true">·</span>');
        const hasText = typeof doc.text === 'string';
        if (els.copy) els.copy.disabled = !hasText;
        if (els.download) els.download.href = rawUrl(tab.rootId, tab.path, true);
        NS.afterHead?.(tab);
    }

    els.crumbs.addEventListener('click', (event) => {
        const crumb = event.target.closest('[data-crumb]');
        if (!crumb) return;
        const rootId = Number(els.crumbs.dataset.root);
        const path = crumb.dataset.crumb;
        reveal(rootId, path ? `${path}/x` : 'x').then(() => {
            const node = state.nodes.get(keyOf(rootId, path));
            if (node) {
                if (!node.expanded) toggleFolder(node, true);
                setSideOpen(true);
                focusItem(node.el);
            }
        });
    });

    function viewContext(tab) {
        return {
            text,
            isDesktop: config.isDesktop,
            rawUrl: (path, download) => rawUrl(tab.rootId, path, download),
            openPath: (path, anchor) => openFile(tab.rootId, path, { anchor }),
            openDefault: (doc) => openOutside(tab, false)
        };
    }

    function showDoc(tab, options) {
        els.body.classList.remove('has-toc');
        renderDocHead(tab);
        tab.view?.destroy?.();
        tab.view = null;
        if (tab.error) {
            els.body.innerHTML = `<div class="tbx-error-state" role="alert">${icon('circle-alert', 20)}<p>${escapeHtml(tab.error.message)}</p><button type="button" class="btn btn-sm btn-outline-secondary" data-fx-retry>${escapeHtml(text['Doc.Retry'])}</button></div>`;
            els.body.querySelector('[data-fx-retry]').addEventListener('click', () => loadDoc(tab));
            setStatus(tab);
            return;
        }
        if (NS.showDoc && NS.showDoc(tab, els, viewContext(tab), options)) {
            NS.afterRender?.(tab, tab.view);
            setStatus(tab);
            if (options?.line) NS.goToLine?.(tab, options.line);
            return;
        }
        tab.view = NS.viewers.render(els.body, tab.doc, viewContext(tab));
        NS.afterRender?.(tab, tab.view);
        setStatus(tab);
        els.body.scrollTop = tab.scroll || 0;
        if (options?.anchor) {
            const target = els.body.querySelector(`[id="fx-h-${CSS.escape(String(options.anchor).toLowerCase())}"]`);
            target?.scrollIntoView({ block: 'start' });
        }
        if (options?.line) NS.goToLine?.(tab, options.line);
        reveal(tab.rootId, tab.path);
    }

    NS.renderView = (tab) => {
        tab.view?.destroy?.();
        tab.view = NS.viewers.render(els.body, tab.doc, viewContext(tab));
        setStatus(tab);
        return tab.view;
    };

    function setStatus(tab) {
        const doc = tab.doc;
        if (!doc) {
            els.status.textContent = '';
            return;
        }
        if (NS.statusFor) {
            const custom = NS.statusFor(tab);
            if (custom) {
                els.status.innerHTML = custom;
                NS.afterStatus?.(tab, els.status);
                return;
            }
        }
        const stats = tab.view?.stats;
        const parts = [];
        if (typeof doc.text === 'string') {
            parts.push(`${escapeHtml(doc.encoding.toUpperCase())}`, escapeHtml(doc.lineEnding));
        }
        if (stats) {
            parts.push(escapeHtml(fmt(text['Doc.Lines'], number(stats.lines))), escapeHtml(fmt(text['Doc.Words'], number(stats.words))), escapeHtml(fmt(text['Doc.Chars'], number(stats.chars))));
        }
        const mode = doc.editable ? text['Doc.View'] : text['Doc.ReadOnly'];
        parts.push(`<span class="fx-status-mode${doc.editable ? '' : ' is-readonly'}" title="${escapeHtml(doc.readOnlyReason || '')}">${escapeHtml(mode)}</span>`);
        if (doc.plain) parts.push(escapeHtml(text['Doc.Plain']));
        els.status.innerHTML = parts.join('<span class="fx-meta-sep" aria-hidden="true">·</span>');
        NS.afterStatus?.(tab, els.status);
    }

    NS.setStatus = setStatus;

    // ------------------------------------------------------------------ document actions

    function activeTab() {
        return findTab(state.active);
    }

    els.copy?.addEventListener('click', () => {
        const tab = activeTab();
        const value = NS.currentText ? NS.currentText(tab) : tab?.doc?.text;
        if (typeof value !== 'string') return;
        navigator.clipboard?.writeText(value).then(() => notify(text['Doc.Copied'], 'success')).catch(() => notify(text['Doc.CopyFailed'], 'error'));
    });

    function printDoc() {
        const tab = activeTab();
        if (!tab?.doc) return;
        if (tab.view?.print) {
            tab.view.print();
            return;
        }
        window.print();
    }

    els.print?.addEventListener('click', printDoc);

    els.saveCopy?.addEventListener('click', async () => {
        const tab = activeTab();
        if (!tab) return;
        try {
            const answer = await request(urls.saveCopyAs, { root: tab.rootId, path: tab.path });
            if (answer?.path) notify(fmt(text['Doc.SavedCopy'], answer.path), 'success');
        } catch (error) {
            window.location.href = rawUrl(tab.rootId, tab.path, true);
        }
    });

    async function openOutside(tab, revealIt) {
        try {
            await request(revealIt ? urls.reveal : urls.openDefault, { root: tab.rootId, path: tab.path });
            if (!revealIt) notify(fmt(text['Info.Opened'], tab.name), 'info');
        } catch (error) {
            notify(error.message, 'error');
        }
    }

    // ------------------------------------------------------------------ quick open (Ctrl+P)

    let findTimer = 0;
    let findSeq = 0;
    let findItems = [];
    let findIndex = -1;

    function renderFind(suggest) {
        const list = els.findResults;
        if (!els.find.value.trim()) {
            const suggestions = suggest && NS.findSuggestions ? NS.findSuggestions() : [];
            if (!suggestions.length) {
                list.hidden = true;
                els.find.setAttribute('aria-expanded', 'false');
                return;
            }
            findItems = suggestions;
            if (findIndex < 0 || findIndex >= findItems.length) findIndex = 0;
        }
        list.hidden = false;
        els.find.setAttribute('aria-expanded', 'true');
        list.innerHTML = findItems.length
            ? findItems.map((item, i) => `<li role="option" id="fx-find-${i}" class="fx-find-item${i === findIndex ? ' is-active' : ''}" aria-selected="${i === findIndex}" data-index="${i}"><span class="fx-node-icon fx-kind-${escapeHtml(item.kind)}" aria-hidden="true">${icon(KIND_ICONS[item.kind] || 'file')}</span><span class="fx-find-name">${escapeHtml(item.name)}</span><span class="fx-find-path">${escapeHtml(item.rootName + ' / ' + parentPath(item.path))}</span></li>`).join('')
            : `<li class="fx-find-note" role="presentation">${escapeHtml(text['Find.Empty'])}</li>`;
        if (findIndex >= 0) els.find.setAttribute('aria-activedescendant', `fx-find-${findIndex}`);
        else els.find.removeAttribute('aria-activedescendant');
    }

    let findPending = null;

    function runFind() {
        window.clearTimeout(findTimer);
        const query = els.find.value.trim();
        const seq = ++findSeq;
        if (!query) {
            findItems = [];
            renderFind(true);
            findPending = null;
            return Promise.resolve();
        }
        els.findResults.hidden = false;
        els.findResults.innerHTML = `<li class="fx-find-note" role="presentation">${escapeHtml(text['Find.Searching'])}</li>`;
        findPending = request(urls.find, null, { q: query, hidden: state.showHidden }).then((items) => {
            if (seq !== findSeq) return;
            findItems = items;
            findIndex = items.length ? 0 : -1;
            renderFind();
        }).catch((error) => {
            if (seq !== findSeq) return;
            findItems = [];
            els.findResults.innerHTML = `<li class="fx-find-note is-error" role="presentation">${escapeHtml(error.message)}</li>`;
        }).finally(() => {
            if (seq === findSeq) findPending = null;
        });
        return findPending;
    }

    function chooseFind(index) {
        const item = findItems[index];
        if (!item) return;
        els.find.value = '';
        findItems = [];
        renderFind();
        openFile(item.rootId, item.path);
    }

    els.find.addEventListener('focus', () => {
        if (!els.find.value.trim()) renderFind(true);
    });
    els.find.addEventListener('input', () => {
        window.clearTimeout(findTimer);
        // What was listed belongs to the old text: Enter must not open it.
        findItems = [];
        findIndex = -1;
        if (els.find.value.trim()) {
            findPending = new Promise((resolve) => {
                findTimer = window.setTimeout(() => runFind().then(resolve, resolve), 200);
            });
        } else {
            runFind();
        }
    });
    els.find.addEventListener('keydown', (event) => {
        if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
            if (!findItems.length) return;
            event.preventDefault();
            findIndex = (findIndex + (event.key === 'ArrowDown' ? 1 : -1) + findItems.length) % findItems.length;
            renderFind(true);
        } else if (event.key === 'Enter') {
            event.preventDefault();
            if (findPending) {
                // Still searching for what was just typed: open the first match when it comes.
                const pending = els.find.value.trim() ? runFind() : findPending;
                pending.then(() => chooseFind(findIndex));
            } else {
                chooseFind(findIndex);
            }
        } else if (event.key === 'Escape') {
            els.find.value = '';
            findItems = [];
            renderFind();
        }
    });
    els.findResults.addEventListener('mousedown', (event) => {
        const item = event.target.closest('[data-index]');
        if (item) {
            event.preventDefault();
            chooseFind(Number(item.dataset.index));
        }
    });
    els.find.addEventListener('blur', () => window.setTimeout(() => {
        els.findResults.hidden = true;
        els.find.setAttribute('aria-expanded', 'false');
    }, 150));

    // ------------------------------------------------------------------ layout: tree width, phone drawer

    function applySideWidth(width) {
        const value = Math.min(600, Math.max(200, Math.round(width)));
        els.shell.style.setProperty('--fx-side-width', `${value}px`);
        els.resizer.setAttribute('aria-valuenow', String(value));
        state.saved.sideWidth = value;
    }

    if (state.saved.sideWidth) applySideWidth(state.saved.sideWidth);

    els.resizer.addEventListener('pointerdown', (event) => {
        event.preventDefault();
        const startX = event.clientX;
        const startWidth = els.side.getBoundingClientRect().width;
        els.resizer.setPointerCapture(event.pointerId);
        const move = (e) => applySideWidth(startWidth + e.clientX - startX);
        const up = () => {
            els.resizer.removeEventListener('pointermove', move);
            els.resizer.removeEventListener('pointerup', up);
            saveState();
        };
        els.resizer.addEventListener('pointermove', move);
        els.resizer.addEventListener('pointerup', up);
    });
    els.resizer.addEventListener('keydown', (event) => {
        if (event.key !== 'ArrowLeft' && event.key !== 'ArrowRight') return;
        event.preventDefault();
        applySideWidth(els.side.getBoundingClientRect().width + (event.key === 'ArrowRight' ? 24 : -24));
        saveState();
    });

    const narrowQuery = window.matchMedia('(max-width: 899.98px)');
    const sideOpenButton = $('[data-fx-side-open]');

    /** Phones: the tree is a drawer over the file. */
    function setSideOpen(open) {
        if (open && !narrowQuery.matches) {
            setSideHidden(false);
            return;
        }
        els.shell.classList.toggle('is-side-open', open);
        labelSideButton();
        if (open) els.tree.querySelector('[tabindex="0"]')?.focus();
    }

    /** Wide screens: the tree column can be hidden so the file takes the whole width; remembered. */
    function setSideHidden(hidden, options) {
        const changed = Boolean(state.saved.sideHidden) !== hidden;
        state.saved.sideHidden = hidden;
        els.shell.classList.toggle('is-side-hidden', hidden);
        labelSideButton();
        if (changed && !options?.quiet) {
            saveState();
            NS.onLayout?.();
            if (hidden) sideOpenButton?.focus();
            else (els.tree.querySelector('[aria-selected="true"]') || els.tree.querySelector('[tabindex="0"]'))?.focus();
        }
    }

    /** One button for both: "Show the folder tree" while it is hidden (or a closed drawer), "Hide" otherwise. */
    function labelSideButton() {
        if (!sideOpenButton) return;
        const shown = narrowQuery.matches ? els.shell.classList.contains('is-side-open') : !state.saved.sideHidden;
        const label = text[shown ? 'Tree.Hide' : 'Tree.Show'];
        sideOpenButton.setAttribute('aria-expanded', String(shown));
        sideOpenButton.setAttribute('aria-label', label);
        sideOpenButton.title = label;
    }

    function toggleSide() {
        if (narrowQuery.matches) setSideOpen(!els.shell.classList.contains('is-side-open'));
        else setSideHidden(!state.saved.sideHidden);
    }

    setSideHidden(Boolean(state.saved.sideHidden), { quiet: true });
    narrowQuery.addEventListener?.('change', labelSideButton);

    sideOpenButton?.addEventListener('click', toggleSide);
    $('[data-fx-side-close]')?.addEventListener('click', () => setSideOpen(false));

    document.querySelectorAll('[data-fx-add-root]').forEach((b) => b.addEventListener('click', addRoot));
    $('[data-fx-collapse]')?.addEventListener('click', collapseAll);
    $('[data-fx-refresh]')?.addEventListener('click', refreshTree);

    // ------------------------------------------------------------------ shortcuts

    const keys = window.Toolbox?.keys;
    if (keys) {
        keys.add({
            keys: 'mod+p', label: text['Keys.Find'], inInputs: true, button: els.find,
            // Ctrl+Shift+P is the command palette (files-extras.js), not this.
            when: (event) => !event.shiftKey,
            run: () => {
                setSideOpen(true);
                els.find.focus();
                els.find.select();
            }
        });
        keys.add({ keys: 'alt+w', label: text['Keys.CloseTab'], inInputs: true, when: () => Boolean(state.active), run: () => closeTab(state.active) });
        keys.add({ keys: 'alt+arrowright', label: text['Keys.NextTab'] || text['Keys.CloseTab'], hidden: true, inInputs: true, when: () => state.tabs.length > 1, run: () => {
            const at = state.tabs.findIndex((t) => t.key === state.active);
            activateTab(state.tabs[(at + 1) % state.tabs.length].key);
        } });
        keys.add({ keys: 'alt+arrowleft', label: text['Keys.PrevTab'] || text['Keys.CloseTab'], hidden: true, inInputs: true, when: () => state.tabs.length > 1, run: () => {
            const at = state.tabs.findIndex((t) => t.key === state.active);
            activateTab(state.tabs[(at - 1 + state.tabs.length) % state.tabs.length].key);
        } });
        keys.add({ keys: 'alt+t', label: text['Keys.Tree'], inInputs: true, run: () => {
            setSideOpen(true);
            (els.tree.querySelector('[aria-selected="true"]') || els.tree.querySelector('[tabindex="0"]'))?.focus();
        } });
    }

    // ------------------------------------------------------------------ start

    NS.page = {
        state, els, config, text, urls, request, rawUrl, openFile, closeTab, activateTab, activeTab, findTab, renderTabs,
        refreshTabs, refreshTree, loadRoots, loadStats, reveal, keyOf, parentPath, baseName, notify, fmt, escapeHtml, icon,
        bytes, when, number, saveState, toggleFolder, showMenu, openOutside, setStatus, focusItem, rootName, moveToBody, modal,
        loadDoc, openPicker, showDoc, renderDocHead, viewContext, markActiveInTree, reloadFolder, retargetTabs, toggleSide,
        applyFilter: () => applyFilter(), setShowHidden: (on) => { state.showHidden = on; saveState(); refreshTree(); }
    };

    state.showHidden = Boolean(state.saved.showHidden);
    loadRoots().then(() => {
        const tabs = (state.saved.tabs || []).filter((t) => state.roots.some((r) => r.id === t.rootId && r.available));
        tabs.forEach((t) => state.tabs.push({ key: keyOf(t.rootId, t.path), rootId: t.rootId, path: t.path, name: baseName(t.path), doc: null }));
        const active = tabs.find((t) => keyOf(t.rootId, t.path) === state.saved.active) || tabs[tabs.length - 1];
        renderTabs();
        if (active) activateTab(keyOf(active.rootId, active.path));
        NS.onReady?.();
    });
})(window, document);
