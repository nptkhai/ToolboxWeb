// Managing files in "Files & folders" (stage C): new file / folder, the menu of a file or folder
// (right click, Shift+F10 or the "…" button: open, open with the default program, show in
// Explorer, rename F2, duplicate, copy path, delete), Toolkit's trash where the Windows Recycle
// Bin is not used, drag and drop inside the tree (Ctrl/Alt to copy) and from Explorer, and the
// live refresh of the tree from GET /Files/Events (Server-Sent Events).
(function (window, document) {
    'use strict';

    const NS = window.ToolboxFiles = window.ToolboxFiles || {};
    const P = NS.page;
    if (!P) {
        return;
    }

    const { text, urls, config, state } = P;
    const DRAG_TYPE = 'application/x-toolkit-file';
    const pendingFolders = new Map();
    let statsTimer = 0;

    function rootOf(node) {
        return state.roots.find((r) => r.id === node.rootId);
    }

    function folderOf(node) {
        if (!node) return null;
        return node.isFolder ? node : state.nodes.get(P.keyOf(node.rootId, P.parentPath(node.path)));
    }

    function selectedNode() {
        const li = P.els.tree.querySelector('[aria-selected="true"]') || P.els.tree.querySelector('[role="treeitem"]:focus');
        return li ? state.nodes.get(li.dataset.key) : null;
    }

    function fullPathText(node) {
        return P.fullPath(node.rootId, node.path);
    }

    async function post(url, body) {
        return P.request(url, body);
    }

    // ------------------------------------------------------------------ name dialog (new, rename)

    function askName(title, label, initial, selectStem) {
        const dialog = P.moveToBody('fx-name');
        const form = dialog.querySelector('[data-fx-name-form]');
        const input = dialog.querySelector('[data-fx-name-input]');
        const error = dialog.querySelector('[data-fx-name-error]');
        dialog.querySelector('[data-fx-name-title]').textContent = title;
        dialog.querySelector('[data-fx-name-label]').textContent = label;
        input.value = initial || '';
        error.textContent = '';
        return new Promise((resolve) => {
            let answer = null;
            form.onsubmit = (event) => {
                event.preventDefault();
                const value = input.value.trim();
                if (!value || /[\\/:*?"<>|]/.test(value) || /[. ]$/.test(value)) {
                    error.textContent = text['Name.Invalid'];
                    input.focus();
                    return;
                }
                answer = value;
                P.modal('fx-name').hide();
            };
            dialog.addEventListener('shown.bs.modal', () => {
                input.focus();
                const dot = selectStem ? input.value.lastIndexOf('.') : -1;
                input.setSelectionRange(0, dot > 0 ? dot : input.value.length);
            }, { once: true });
            dialog.addEventListener('hidden.bs.modal', () => resolve(answer), { once: true });
            P.modal('fx-name').show();
        });
    }

    // ------------------------------------------------------------------ actions

    async function createIn(folderNode, isFolder) {
        const target = folderNode || state.nodes.get(P.keyOf(state.roots.find((r) => r.available)?.id, ''));
        if (!target) return;
        const name = await askName(
            isFolder ? text['New.FolderTitle'] : text['New.FileTitle'],
            isFolder ? text['New.FolderLabel'] : text['New.FileLabel'],
            isFolder ? text['New.FolderDefault'] : text['New.FileDefault'],
            !isFolder);
        if (!name) return;
        try {
            const entry = await post(urls.create, { root: target.rootId, folder: target.path, name, isFolder });
            if (!target.expanded) await P.toggleFolder(target, true);
            else await P.reloadFolder(target.rootId, target.path);
            if (isFolder) {
                P.notify(P.fmt(text['New.FolderDone'], name), 'success');
            } else {
                const tab = await P.openFile(target.rootId, entry.path);
                if (tab && tab.doc?.editable) {
                    tab.mode = 'edit';
                    P.showDoc(tab);
                }
            }
            scheduleStats();
        } catch (error) {
            P.notify(error.message, 'error');
        }
    }

    async function rename(node) {
        const name = await askName(text['Rename.Title'], text['Rename.Label'], node.name, !node.isFolder);
        if (!name || name === node.name) return;
        try {
            const entry = await post(urls.rename, { root: node.rootId, path: node.path, name });
            P.retargetTabs(node.rootId, node.path, node.rootId, entry.path);
            await P.reloadFolder(node.rootId, P.parentPath(node.path));
            P.notify(P.fmt(text['Rename.Done'], node.name, name), 'success');
        } catch (error) {
            P.notify(error.message, 'error');
        }
    }

    async function duplicate(node) {
        try {
            const entry = await post(urls.duplicate, { root: node.rootId, path: node.path });
            await P.reloadFolder(node.rootId, P.parentPath(node.path));
            P.notify(P.fmt(text['Duplicate.Done'], entry.name), 'success');
            scheduleStats();
        } catch (error) {
            P.notify(error.message, 'error');
        }
    }

    function tabsUnder(node) {
        return state.tabs.filter((t) => t.rootId === node.rootId && (t.path === node.path || t.path.startsWith(`${node.path}/`)));
    }

    async function remove(node) {
        const dirty = tabsUnder(node).some((t) => NS.isDirty?.(t));
        const message = P.fmt(config.usesToolkitTrash ? text['Delete.MessageToolkit'] : text['Delete.MessageRecycle'], node.name)
            + (dirty ? ' ' + text['Delete.Unsaved'] : '');
        const ok = await window.Toolbox.confirm({
            title: node.isFolder ? text['Delete.FolderTitle'] : text['Delete.FileTitle'],
            message,
            okText: text['Delete.Ok']
        });
        if (!ok) return;
        try {
            await post(urls.delete, { root: node.rootId, path: node.path });
            const closed = tabsUnder(node);
            for (const tab of closed) await P.closeTab(tab.key, true);
            await P.reloadFolder(node.rootId, P.parentPath(node.path));
            P.notify(P.fmt(config.usesToolkitTrash ? text['Delete.DoneToolkit'] : text['Delete.DoneRecycle'], node.name)
                + (closed.length ? ' ' + P.fmt(text['Delete.TabsClosed'], P.number(closed.length)) : ''), 'success');
            scheduleStats();
        } catch (error) {
            P.notify(error.message, 'error');
        }
    }

    function copyPath(node) {
        return P.copyText(fullPathText(node));
    }

    function outside(node, reveal) {
        return P.openOutside({ rootId: node.rootId, path: node.path, name: node.name }, reveal);
    }

    NS.folderItems = (node) => [
        { label: text['New.File'], icon: 'file-plus', run: () => createIn(node, false) },
        { label: text['New.Folder'], icon: 'folder-plus', run: () => createIn(node, true) }
    ];

    NS.fileMenu = (node, anchor, point) => {
        const items = [];
        if (node.isFolder) {
            items.push(...NS.folderItems(node), 'separator');
        } else {
            items.push({ label: text['Menu.Open'], icon: 'file-text', run: () => P.openFile(node.rootId, node.path) });
            if (config.canOpenLocal) {
                items.push({ label: text['Info.OpenDefault'], icon: 'external-link', run: () => outside(node, false) });
            }
            if (!config.isDesktop) {
                items.push({ label: text['Info.Download'], icon: 'download', run: () => { window.location.href = P.rawUrl(node.rootId, node.path, true); } });
            }
            items.push('separator');
        }
        items.push({ label: text['Menu.Rename'], icon: 'pencil', run: () => rename(node) });
        items.push({ label: text['Menu.Duplicate'], icon: 'copy-plus', run: () => duplicate(node) });
        items.push({ label: text['Menu.CopyPath'], icon: 'clipboard-paste', run: () => copyPath(node) });
        if (config.canOpenLocal) items.push({ label: text['Menu.Reveal'], icon: 'folder-open', run: () => outside(node, true) });
        items.push('separator');
        items.push({ label: text['Menu.Delete'], icon: 'trash-2', danger: true, run: () => remove(node) });
        P.showMenu(anchor, items, point);
    };

    NS.treeKey = (event, node) => {
        if (node.isRoot) return;
        if (event.key === 'F2') {
            event.preventDefault();
            rename(node);
        } else if (event.key === 'Delete') {
            event.preventDefault();
            remove(node);
        }
    };

    document.querySelector('[data-fx-new-file]')?.addEventListener('click', () => createIn(folderOf(selectedNode()), false));
    document.querySelector('[data-fx-new-folder]')?.addEventListener('click', () => createIn(folderOf(selectedNode()), true));

    // ------------------------------------------------------------------ Toolkit's trash

    async function openTrash() {
        const dialog = P.moveToBody('fx-trash');
        const list = dialog.querySelector('[data-fx-trash-list]');
        list.innerHTML = `<li class="tbx-row fx-picker-note">${P.escapeHtml(text['Tree.Loading'])}</li>`;
        P.modal('fx-trash').show();
        try {
            const items = await P.request(urls.trash);
            list.innerHTML = items.length
                ? items.map((item) => `<li class="tbx-row fx-trash-row"><span class="fx-node-icon" aria-hidden="true">${P.icon(item.isFolder ? 'folder' : 'file')}</span>`
                    + `<span class="tbx-row-main"><span class="tbx-row-title">${P.escapeHtml(item.name)}</span><span class="tbx-row-sub">${P.escapeHtml(`${item.rootName}/${item.path}`)} · ${P.escapeHtml(P.when(item.deletedAt))} · ${P.escapeHtml(P.bytes(item.size))}</span></span>`
                    + `<span class="fx-trash-actions"><button type="button" class="btn btn-sm btn-outline-secondary" data-restore="${P.escapeHtml(item.id)}">${P.escapeHtml(text['Trash.Restore'])}</button>`
                    + `<button type="button" class="btn btn-sm btn-outline-danger" data-purge="${P.escapeHtml(item.id)}" data-name="${P.escapeHtml(item.name)}">${P.escapeHtml(text['Trash.Purge'])}</button></span></li>`).join('')
                : `<li class="tbx-empty" role="status"><span class="tbx-empty-icon">${P.icon('trash-2', 20)}</span><p class="tbx-empty-text">${P.escapeHtml(text['Trash.Empty'])}</p></li>`;
        } catch (error) {
            list.innerHTML = `<li class="tbx-row fx-picker-note is-error">${P.escapeHtml(error.message)}</li>`;
        }
    }

    document.querySelector('[data-fx-trash]')?.addEventListener('click', openTrash);
    document.getElementById('fx-trash')?.addEventListener('click', async (event) => {
        const restore = event.target.closest('[data-restore]');
        const purge = event.target.closest('[data-purge]');
        if (restore) {
            try {
                const entry = await post(urls.restore, { id: restore.dataset.restore });
                P.notify(P.fmt(text['Trash.Restored'], entry.name), 'success');
                P.refreshTree();
                openTrash();
            } catch (error) {
                P.notify(error.message, 'error');
            }
        } else if (purge) {
            P.modal('fx-trash').hide();
            const ok = await window.Toolbox.confirm({ title: text['Trash.PurgeTitle'], message: P.fmt(text['Trash.PurgeMessage'], purge.dataset.name), okText: text['Trash.Purge'] });
            if (ok) {
                try {
                    await post(urls.purge, { id: purge.dataset.purge });
                } catch (error) {
                    P.notify(error.message, 'error');
                }
            }
            openTrash();
        }
    });

    // ------------------------------------------------------------------ drag and drop

    let dragging = null;

    function dropTarget(event) {
        const li = event.target.closest?.('[role="treeitem"]');
        if (!li) return null;
        return folderOf(state.nodes.get(li.dataset.key));
    }

    function clearDropMarks() {
        P.els.tree.querySelectorAll('.is-drop').forEach((el) => el.classList.remove('is-drop'));
    }

    P.els.tree.addEventListener('dragstart', (event) => {
        const li = event.target.closest('[role="treeitem"]');
        const node = li && state.nodes.get(li.dataset.key);
        if (!node || node.isRoot) {
            event.preventDefault();
            return;
        }
        dragging = node;
        event.dataTransfer.effectAllowed = 'copyMove';
        event.dataTransfer.setData(DRAG_TYPE, JSON.stringify({ root: node.rootId, path: node.path }));
        event.dataTransfer.setData('text/plain', node.name);
    });

    P.els.tree.addEventListener('dragend', () => {
        dragging = null;
        clearDropMarks();
    });

    P.els.tree.addEventListener('dragover', (event) => {
        const types = Array.from(event.dataTransfer?.types || []);
        const internal = types.includes(DRAG_TYPE);
        if (!internal && !types.includes('Files')) return;
        const target = dropTarget(event);
        if (!target || (internal && dragging && (target.key === dragging.key || (dragging.isFolder && target.rootId === dragging.rootId && (target.path + '/').startsWith(dragging.path + '/'))))) return;
        event.preventDefault();
        event.dataTransfer.dropEffect = internal ? (event.ctrlKey || event.altKey ? 'copy' : 'move') : 'copy';
        clearDropMarks();
        target.el?.querySelector(':scope > .fx-node')?.classList.add('is-drop');
    });

    P.els.tree.addEventListener('dragleave', (event) => {
        if (!P.els.tree.contains(event.relatedTarget)) clearDropMarks();
    });

    P.els.tree.addEventListener('drop', async (event) => {
        const target = dropTarget(event);
        clearDropMarks();
        if (!target) return;
        const raw = event.dataTransfer.getData(DRAG_TYPE);
        event.preventDefault();
        if (raw) {
            const source = JSON.parse(raw);
            const node = state.nodes.get(P.keyOf(source.root, source.path));
            if (node) await moveTo(node, target, event.ctrlKey || event.altKey);
            return;
        }
        if (event.dataTransfer.items?.length || event.dataTransfer.files?.length) {
            await uploadInto(target, event.dataTransfer);
        }
    });

    async function moveTo(node, target, copy) {
        const destination = target.path ? `${P.rootName(target.rootId)}/${target.path}` : P.rootName(target.rootId);
        const ok = await window.Toolbox.confirm({
            title: copy ? text['Move.CopyTitle'] : text['Move.Title'],
            message: P.fmt(copy ? text['Move.CopyMessage'] : text['Move.Message'], node.name, destination),
            okText: copy ? text['Move.CopyOk'] : text['Move.Ok'],
            tone: 'primary'
        });
        if (!ok) return;
        const body = { root: node.rootId, path: node.path, targetRoot: target.rootId, targetFolder: target.path, copy, overwrite: false };
        let entry;
        try {
            entry = await post(urls.move, body);
        } catch (error) {
            if (error.code !== 'exists' || node.isFolder) {
                P.notify(error.message, 'error');
                return;
            }
            const replace = await window.Toolbox.confirm({ title: text['Move.ReplaceTitle'], message: P.fmt(text['Move.ReplaceMessage'], node.name), okText: text['Move.ReplaceOk'] });
            if (!replace) return;
            try {
                entry = await post(urls.move, Object.assign(body, { overwrite: true }));
            } catch (failure) {
                P.notify(failure.message, 'error');
                return;
            }
        }
        if (!copy) P.retargetTabs(node.rootId, node.path, target.rootId, entry.path);
        await P.reloadFolder(node.rootId, P.parentPath(node.path));
        if (!target.expanded) await P.toggleFolder(target, true);
        else await P.reloadFolder(target.rootId, target.path);
        P.notify(P.fmt(copy ? text['Move.CopyDone'] : text['Move.Done'], node.name, destination), 'success');
    }

    /** Files (and folders, through webkitGetAsEntry) dropped from Explorer. */
    async function collect(dataTransfer) {
        const out = [];
        const entries = Array.from(dataTransfer.items || []).map((item) => item.webkitGetAsEntry?.()).filter(Boolean);
        if (!entries.length) {
            Array.from(dataTransfer.files || []).forEach((file) => out.push({ file, path: file.name }));
            return out;
        }
        async function walk(entry, prefix) {
            if (entry.isFile) {
                const file = await new Promise((resolve, reject) => entry.file(resolve, reject));
                out.push({ file, path: prefix + entry.name });
            } else if (entry.isDirectory) {
                const reader = entry.createReader();
                let batch;
                do {
                    batch = await new Promise((resolve, reject) => reader.readEntries(resolve, reject));
                    for (const child of batch) await walk(child, `${prefix}${entry.name}/`);
                } while (batch.length);
            }
        }
        for (const entry of entries) await walk(entry, '');
        return out;
    }

    async function uploadInto(target, dataTransfer) {
        let items;
        try {
            items = await collect(dataTransfer);
        } catch (error) {
            P.notify(text['Upload.Failed'], 'error');
            return;
        }
        if (!items.length) return;
        const send = async (overwrite) => {
            const form = new FormData();
            form.append('root', String(target.rootId));
            form.append('folder', target.path || '');
            form.append('overwrite', overwrite ? 'true' : 'false');
            items.forEach((item) => {
                form.append('files', item.file, item.file.name);
                form.append('paths', item.path);
            });
            const response = await fetch(urls.upload, { method: 'POST', headers: { RequestVerificationToken: config.token || '', 'X-Requested-With': 'XMLHttpRequest' }, body: form });
            const result = await response.json().catch(() => null);
            if (!response.ok || !result?.success) {
                const error = new Error(result?.message || text['Upload.Failed']);
                error.code = result?.code;
                throw error;
            }
            return result.data;
        };
        P.notify(P.fmt(text['Upload.Sending'], P.number(items.length)), 'info');
        try {
            let answer;
            try {
                answer = await send(false);
            } catch (error) {
                if (error.code !== 'exists') throw error;
                const replace = await window.Toolbox.confirm({ title: text['Upload.ReplaceTitle'], message: error.message, okText: text['Move.ReplaceOk'] });
                if (!replace) return;
                answer = await send(true);
            }
            if (!target.expanded) await P.toggleFolder(target, true);
            else await P.reloadFolder(target.rootId, target.path);
            P.notify(P.fmt(text['Upload.Done'], P.number(answer.count)), 'success');
            scheduleStats();
        } catch (error) {
            P.notify(error.message, 'error');
        }
    }

    // ------------------------------------------------------------------ live refresh

    // Every 3 s while the page is visible: GET /Files/Changes with the last cursor answers the
    // folders that changed outside Toolkit; those are read again (expanded ones only).
    let cursor = 0;
    let polling = false;

    function scheduleStats() {
        window.clearTimeout(statsTimer);
        statsTimer = window.setTimeout(() => P.loadStats(), 1500);
    }

    function applyChange(change) {
        if (change.all) {
            P.reloadOpenFolders(change.root);
            return;
        }
        const key = P.keyOf(change.root, change.folder || '');
        window.clearTimeout(pendingFolders.get(key));
        pendingFolders.set(key, window.setTimeout(() => {
            pendingFolders.delete(key);
            P.reloadFolder(change.root, change.folder || '');
        }, 100));
    }

    async function poll() {
        if (polling || !window.Toolbox.isScreenVisible() || !state.roots.some((r) => r.available)) return;
        polling = true;
        try {
            const answer = await P.request(urls.changes, null, { since: cursor });
            const first = cursor === 0;
            cursor = answer.cursor;
            if (!first && answer.changes.length) {
                answer.changes.forEach(applyChange);
                scheduleStats();
                NS.checkOpenTabs?.();
            }
        } catch (error) {
            // Offline or the root went away: tried again on the next tick.
        } finally {
            polling = false;
        }
    }

    NS.onRoots = () => {
        cursor = 0;
        poll();
    };

    window.setInterval(poll, 3000);
})(window, document);
