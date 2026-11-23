// Extras of "Files & folders" (stage D):
// - search in the content of every root (Ctrl+Shift+F, GET /Files/Search), a result opens the file at its line;
// - recent and pinned files (this browser), shown on the start pane and in quick open when its box is empty;
// - table of contents of a Markdown file;
// - show hidden files and filter the tree by kind;
// - paste an image into Markdown (saved next to the file in assets/, link inserted);
// - checks: JSON and YAML syntax, links in Markdown that lead to missing files;
// - export Markdown as a standalone HTML file, print / save as PDF;
// - "Files: …" commands in the Ctrl+K palette (Ctrl+Shift+P opens it);
// - /files.html?open=<full path>: a .md file opened from Explorer on the desktop.
(function (window, document) {
    'use strict';

    const NS = window.ToolboxFiles = window.ToolboxFiles || {};
    const P = NS.page;
    if (!P) {
        return;
    }

    const { text, urls, config, state, els } = P;
    const LISTS_KEY = config.stateKey.replace('toolbox.files.state.v1', 'toolbox.files.lists.v1');
    const RECENT_MAX = 15;
    const lists = readLists();

    // ------------------------------------------------------------------ recent and pinned

    function readLists() {
        try {
            const value = JSON.parse(window.localStorage.getItem(LISTS_KEY) || '{}');
            return { recent: Array.isArray(value.recent) ? value.recent : [], pinned: Array.isArray(value.pinned) ? value.pinned : [] };
        } catch (error) {
            return { recent: [], pinned: [] };
        }
    }

    function saveLists() {
        try {
            window.localStorage.setItem(LISTS_KEY, JSON.stringify(lists));
        } catch (error) {
            // Kept for this page only.
        }
        renderStart();
    }

    const same = (a, b) => a.rootId === b.rootId && a.path === b.path;

    function isPinned(tab) {
        return lists.pinned.some((x) => same(x, tab));
    }

    function togglePin(tab) {
        if (isPinned(tab)) {
            lists.pinned = lists.pinned.filter((x) => !same(x, tab));
            P.notify(P.fmt(text['Pin.Removed'], tab.name), 'info');
        } else {
            lists.pinned.unshift({ rootId: tab.rootId, path: tab.path, name: tab.name, kind: tab.doc?.kind || 'text' });
            P.notify(P.fmt(text['Pin.Added'], tab.name), 'success');
        }
        saveLists();
    }

    NS.onOpened = (tab) => {
        if (!tab.doc || tab.error) return;
        lists.recent = [{ rootId: tab.rootId, path: tab.path, name: tab.doc.name, kind: tab.doc.kind }]
            .concat(lists.recent.filter((x) => !same(x, tab)))
            .slice(0, RECENT_MAX);
        saveLists();
    };

    function available(item) {
        return state.roots.some((r) => r.id === item.rootId && r.available);
    }

    NS.findSuggestions = () => {
        const seen = new Set();
        return lists.pinned.concat(lists.recent).filter(available).filter((x) => {
            const key = P.keyOf(x.rootId, x.path);
            if (seen.has(key)) return false;
            seen.add(key);
            return true;
        }).slice(0, 12).map((x) => ({ rootId: x.rootId, rootName: P.rootName(x.rootId), path: x.path, name: x.name, kind: x.kind }));
    };

    function renderStart() {
        const host = document.querySelector('[data-fx-start-lists]');
        if (!host) return;
        const block = (title, items, iconName) => (items.length
            ? `<section class="fx-start-list"><h3>${P.icon(iconName)}<span>${P.escapeHtml(title)}</span></h3><ul>${items.map((x) => `<li><button type="button" class="fx-start-item" data-root="${x.rootId}" data-path="${P.escapeHtml(x.path)}"><span class="fx-node-icon fx-kind-${P.escapeHtml(x.kind)}" aria-hidden="true">${P.icon('file-text')}</span><span class="fx-start-name">${P.escapeHtml(x.name)}</span><span class="fx-start-path">${P.escapeHtml(`${P.rootName(x.rootId)}/${P.parentPath(x.path)}`)}</span></button></li>`).join('')}</ul></section>`
            : '');
        host.innerHTML = block(text['Pin.Title'], lists.pinned.filter(available), 'star') + block(text['Recent.Title'], lists.recent.filter(available).slice(0, 8), 'history');
    }

    document.querySelector('[data-fx-start-lists]')?.addEventListener('click', (event) => {
        const item = event.target.closest('[data-path]');
        if (item) P.openFile(Number(item.dataset.root), item.dataset.path);
    });

    // ------------------------------------------------------------------ document menu (…)

    const moreButton = document.querySelector('[data-fx-more]');
    moreButton?.addEventListener('click', () => {
        const tab = P.activeTab();
        if (!tab?.doc) return;
        const items = [
            { label: isPinned(tab) ? text['Pin.Unpin'] : text['Pin.Pin'], icon: 'star', run: () => togglePin(tab) }
        ];
        if (tab.doc.kind === 'markdown') {
            items.push({ label: text['Toc.Toggle'], icon: 'list-tree', run: () => toggleToc(tab) });
            items.push({ label: text['Export.Html'], icon: 'download', run: () => exportHtml(tab) });
        }
        items.push({ label: text['Export.Pdf'], icon: 'printer', run: () => printDoc(tab) });
        items.push('separator');
        if (config.canOpenLocal) {
            items.push({ label: text['Info.OpenDefault'], icon: 'external-link', run: () => P.openOutside(tab, false) });
            items.push({ label: text['Menu.Reveal'], icon: 'folder-open', run: () => P.openOutside(tab, true) });
        }
        items.push({ label: text['Menu.ShowInTree'], icon: 'folder-tree', run: () => P.showInTree(tab) });
        items.push({ label: text['Menu.CopyPath'], icon: 'clipboard-paste', run: () => P.copyText(P.fullPath(tab.rootId, tab.path)) });
        items.push({ label: text['Menu.CopyFolderPath'], icon: 'clipboard-paste', run: () => P.copyText(P.fullPath(tab.rootId, P.parentPath(tab.path))) });
        P.showMenu(moreButton, items);
    });

    function printDoc(tab) {
        if (tab.view?.print) tab.view.print();
        else window.print();
    }

    // ------------------------------------------------------------------ table of contents

    function toggleToc(tab) {
        tab.toc = !tab.toc;
        P.showDoc(tab);
    }

    function renderToc(tab) {
        if (!tab.toc || tab.mode === 'edit' || tab.doc?.kind !== 'markdown') return;
        const article = els.body.querySelector('.fx-md');
        if (!article) return;
        const items = NS.markdown.headings(article);
        const nav = document.createElement('nav');
        nav.className = 'fx-toc';
        nav.setAttribute('aria-label', text['Toc.Title']);
        nav.innerHTML = `<p class="fx-toc-title">${P.escapeHtml(text['Toc.Title'])}</p>`
            + (items.length
                ? `<ul>${items.map((h) => `<li class="fx-toc-l${h.level}"><a href="#${P.escapeHtml(h.id)}" data-toc="${P.escapeHtml(h.id)}">${P.escapeHtml(h.text)}</a></li>`).join('')}</ul>`
                : `<p class="fx-toc-empty">${P.escapeHtml(text['Toc.Empty'])}</p>`);
        nav.addEventListener('click', (event) => {
            const link = event.target.closest('[data-toc]');
            if (!link) return;
            event.preventDefault();
            document.getElementById(link.dataset.toc)?.scrollIntoView({ block: 'start' });
        });
        els.body.classList.add('has-toc');
        els.body.prepend(nav);
    }

    // ------------------------------------------------------------------ checks (lint)

    function yamlError(value) {
        if (!window.jsyaml) return null;
        try {
            window.jsyaml.loadAll(value, () => {});
            return null;
        } catch (error) {
            return { line: (error.mark?.line ?? 0) + 1, column: (error.mark?.column ?? 0) + 1 };
        }
    }

    function jsonError(value) {
        try {
            JSON.parse(value.replace(/^﻿/, ''));
            return null;
        } catch (error) {
            const message = String(error.message || '');
            let match = /line (\d+) column (\d+)/i.exec(message);
            if (match) return { line: Number(match[1]), column: Number(match[2]) };
            match = /position (\d+)/i.exec(message);
            if (!match) return { line: 1, column: 1 };
            const before = value.slice(0, Number(match[1])).split('\n');
            return { line: before.length, column: before[before.length - 1].length + 1 };
        }
    }

    /** Relative links and images of a Markdown text: [{ target, line }]. */
    function markdownLinks(source, path) {
        const found = [];
        const pattern = /!?\[[^\]]*\]\(\s*<?([^)\s>]+)>?(?:\s+"[^"]*")?\s*\)/g;
        source.split('\n').forEach((line, index) => {
            let match;
            while ((match = pattern.exec(line))) {
                const target = NS.markdown.resolveRelative(path, match[1]);
                if (target !== null && target !== '') found.push({ target, line: index + 1, href: match[1] });
            }
        });
        return found;
    }

    async function lint(tab) {
        const doc = tab.doc;
        if (!doc || typeof doc.text !== 'string' || doc.plain) return null;
        const value = NS.currentText ? NS.currentText(tab) : doc.text;
        const ext = NS.viewers.extension(doc.name);
        if (doc.kind === 'json') {
            const error = jsonError(value);
            return error ? { kind: 'json', ...error } : { kind: 'ok' };
        }
        if (ext === 'yml' || ext === 'yaml') {
            const error = yamlError(value);
            return error ? { kind: 'yaml', ...error } : { kind: 'ok' };
        }
        if (doc.kind === 'markdown') {
            const links = markdownLinks(value, tab.path);
            if (!links.length) return { kind: 'ok' };
            try {
                const missing = await P.request(urls.missing, { root: tab.rootId, paths: links.map((l) => l.target) });
                const broken = links.filter((l) => missing.includes(l.target));
                return broken.length ? { kind: 'links', broken } : { kind: 'ok' };
            } catch (error) {
                return null;
            }
        }
        return null;
    }

    let lintTimer = 0;
    let lintSeq = 0;

    function scheduleLint(tab) {
        window.clearTimeout(lintTimer);
        lintTimer = window.setTimeout(async () => {
            const seq = ++lintSeq;
            const result = await lint(tab);
            if (seq !== lintSeq || P.activeTab() !== tab) return;
            tab.lint = result;
            showLint(tab);
        }, 400);
    }

    function showLint(tab) {
        els.status.querySelector('.fx-lint')?.remove();
        const result = tab.lint;
        if (!result) return;
        const element = document.createElement(result.kind === 'ok' ? 'span' : 'button');
        element.className = 'fx-lint' + (result.kind === 'ok' ? ' is-ok' : ' is-problem');
        if (result.kind === 'ok') {
            element.innerHTML = `${P.icon('circle-check', 14)}<span>${P.escapeHtml(text['Lint.Ok'])}</span>`;
        } else {
            element.type = 'button';
            const label = result.kind === 'links'
                ? P.fmt(text['Lint.Links'], P.number(result.broken.length))
                : P.fmt(result.kind === 'json' ? text['Lint.Json'] : text['Lint.Yaml'], result.line, result.column);
            element.innerHTML = `${P.icon('triangle-alert', 14)}<span>${P.escapeHtml(label)}</span>`;
            element.addEventListener('click', () => {
                if (result.kind === 'links') {
                    P.showMenu(element, result.broken.slice(0, 15).map((l) => ({
                        label: P.fmt(text['Lint.LinkItem'], l.line, l.href),
                        icon: 'link',
                        run: () => NS.goToLine?.(tab, l.line)
                    })));
                } else {
                    NS.goToLine?.(tab, result.line);
                }
            });
        }
        els.status.querySelector('.fx-autosave')?.before(element) ?? els.status.appendChild(element);
        if (result.kind === 'links') markBrokenLinks(result.broken);
    }

    /** Marks links and images of the rendered Markdown whose target is missing. */
    function markBrokenLinks(broken) {
        const targets = new Set(broken.map((l) => l.target));
        els.body.querySelectorAll('.fx-md a[title]').forEach((link) => {
            if (targets.has(link.getAttribute('title'))) link.classList.add('fx-broken');
        });
        els.body.querySelectorAll('.fx-md img').forEach((img) => {
            try {
                const path = new URL(img.src).searchParams.get('path');
                if (path && targets.has(path)) img.classList.add('fx-broken');
            } catch (error) {
                // Not one of ours.
            }
        });
    }

    NS.afterStatus = (tab) => {
        if (tab.lint && tab.lintFor === (NS.currentText ? NS.currentText(tab) : tab.doc?.text)) {
            showLint(tab);
            return;
        }
        tab.lintFor = NS.currentText ? NS.currentText(tab) : tab.doc?.text;
        scheduleLint(tab);
    };

    NS.afterRender = (tab) => {
        renderToc(tab);
        if (tab.lint?.kind === 'links') markBrokenLinks(tab.lint.broken);
    };

    // ------------------------------------------------------------------ paste an image into Markdown

    document.addEventListener('paste', async (event) => {
        const tab = P.activeTab();
        if (!tab || tab.mode !== 'edit' || tab.doc?.kind !== 'markdown' || !event.target.closest?.('.fx-editor-host')) return;
        const file = Array.from(event.clipboardData?.items || []).find((item) => item.kind === 'file' && item.type.startsWith('image/'))?.getAsFile();
        if (!file) return;
        event.preventDefault();
        const ext = (file.type.split('/')[1] || 'png').replace('jpeg', 'jpg').replace(/[^a-z0-9]/g, '');
        const stamp = new Date().toISOString().replace(/[-:]/g, '').replace('T', '-').slice(0, 15);
        const name = `image-${stamp}.${ext}`;
        const folder = P.parentPath(tab.path);
        const form = new FormData();
        form.append('root', String(tab.rootId));
        form.append('folder', folder);
        form.append('overwrite', 'false');
        form.append('files', file, name);
        form.append('paths', `assets/${name}`);
        try {
            const response = await fetch(urls.upload, { method: 'POST', headers: { RequestVerificationToken: config.token || '', 'X-Requested-With': 'XMLHttpRequest' }, body: form });
            const result = await response.json().catch(() => null);
            if (!response.ok || !result?.success) throw new Error(result?.message || text['Paste.Failed']);
            NS.insertText?.(`![${text['Md.ImageText']}](assets/${name})`);
            P.notify(P.fmt(text['Paste.Done'], `assets/${name}`), 'success');
        } catch (error) {
            P.notify(error.message || text['Paste.Failed'], 'error');
        }
    }, true);

    // ------------------------------------------------------------------ export Markdown as HTML

    function lightTokens() {
        const values = {};
        Array.prototype.forEach.call(document.styleSheets, (sheet) => {
            let rules;
            try {
                rules = sheet.cssRules;
            } catch (error) {
                return;
            }
            Array.prototype.forEach.call(rules || [], (rule) => {
                if (rule.selectorText !== ':root' || !rule.style) return;
                for (let i = 0; i < rule.style.length; i++) {
                    const name = rule.style[i];
                    if (name.startsWith('--')) values[name] = rule.style.getPropertyValue(name).trim();
                }
            });
        });
        return Object.keys(values).map((name) => `${name}: ${values[name]};`).join('\n');
    }

    /** The Markdown rules of files.css (.fx-md, callouts, code colours), copied into the file. */
    function markdownRules() {
        const out = [];
        Array.prototype.forEach.call(document.styleSheets, (sheet) => {
            if (!(sheet.href || '').includes('/css/files.css')) return;
            Array.prototype.forEach.call(sheet.cssRules || [], (rule) => {
                if (rule.selectorText && /\.fx-md|\.fx-callout|\.fx-frontmatter|\.fx-table-wrap/.test(rule.selectorText) && !/fx-pre-copy/.test(rule.selectorText)) {
                    out.push(rule.cssText);
                }
            });
        });
        return out.join('\n');
    }

    function exportHtml(tab) {
        const article = document.createElement('article');
        article.className = 'fx-md';
        NS.markdown.render(article, NS.currentText ? NS.currentText(tab) : tab.doc.text, {
            path: tab.path, text, rawUrl: (path) => new URL(P.rawUrl(tab.rootId, path), window.location.href).href, openPath: () => {}
        });
        article.querySelectorAll('.fx-pre-copy').forEach((b) => b.remove());
        article.querySelectorAll('img').forEach((img) => { img.removeAttribute('loading'); });
        const title = tab.doc.name.replace(/\.[^.]+$/, '');
        const html = `<!doctype html>
<html lang="${P.escapeHtml(document.documentElement.lang || 'vi')}">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>${P.escapeHtml(title)}</title>
<style>
:root {
${lightTokens()}
}
body { background: var(--surface); color: var(--text); font-family: var(--font-sans); margin: 0; padding: 32px 24px; }
main { margin: 0 auto; max-width: 920px; }
${markdownRules()}
@media print { body { padding: 0; } }
</style>
</head>
<body>
<main>
${article.outerHTML}
</main>
</body>
</html>
`;
        const blob = new Blob([html], { type: 'text/html;charset=utf-8' });
        const link = document.createElement('a');
        link.href = URL.createObjectURL(blob);
        link.download = `${title}.html`;
        document.body.appendChild(link);
        link.click();
        window.setTimeout(() => {
            URL.revokeObjectURL(link.href);
            link.remove();
        }, 1000);
        P.notify(P.fmt(text['Export.Done'], `${title}.html`), 'success');
    }

    // ------------------------------------------------------------------ hidden files and filter

    const options = document.querySelector('[data-fx-options]');
    const optionsToggle = document.querySelector('[data-fx-options-toggle]');
    const hiddenBox = document.querySelector('[data-fx-show-hidden]');
    const filterSelect = document.querySelector('[data-fx-filter]');
    const filterChip = document.querySelector('[data-fx-filter-chip]');

    function renderFilterChip() {
        if (!filterChip) return;
        const kind = state.filter || '';
        filterChip.hidden = !kind && !state.showHidden;
        const parts = [];
        if (kind) parts.push(P.fmt(text['Filter.Showing'], text['Kind.' + kind] || kind));
        if (state.showHidden) parts.push(text['Filter.Hidden']);
        filterChip.querySelector('span').textContent = parts.join(' · ');
    }

    optionsToggle?.addEventListener('click', () => {
        options.hidden = !options.hidden;
        optionsToggle.setAttribute('aria-expanded', String(!options.hidden));
        if (!options.hidden) hiddenBox?.focus();
    });
    // Closed on click (not mousedown): closing moves the controls below it, which must not eat the click.
    document.addEventListener('click', (event) => {
        if (options && !options.hidden && !options.contains(event.target) && !optionsToggle.contains(event.target)) {
            options.hidden = true;
            optionsToggle.setAttribute('aria-expanded', 'false');
        }
    });
    if (hiddenBox) hiddenBox.checked = state.showHidden;
    hiddenBox?.addEventListener('change', () => {
        P.setShowHidden(hiddenBox.checked);
        renderFilterChip();
    });
    filterSelect?.addEventListener('change', () => {
        state.filter = filterSelect.value;
        P.applyFilter();
        renderFilterChip();
    });
    filterChip?.querySelector('button')?.addEventListener('click', () => {
        state.filter = '';
        if (filterSelect) filterSelect.value = '';
        P.applyFilter();
        if (state.showHidden) {
            if (hiddenBox) hiddenBox.checked = false;
            P.setShowHidden(false);
        }
        renderFilterChip();
    });

    // ------------------------------------------------------------------ search in files

    const search = {
        dialog: document.getElementById('fx-search'),
        form: document.querySelector('[data-fx-search-form]'),
        input: document.querySelector('[data-fx-search-input]'),
        matchCase: document.querySelector('[data-fx-search-case]'),
        results: document.querySelector('[data-fx-search-results]'),
        summary: document.querySelector('[data-fx-search-summary]')
    };

    function openSearch() {
        if (!search.dialog) return;
        P.moveToBody('fx-search');
        search.dialog.addEventListener('shown.bs.modal', () => {
            search.input.focus();
            search.input.select();
        }, { once: true });
        P.modal('fx-search').show();
    }

    let searchSeq = 0;
    async function runSearch() {
        const query = search.input.value.trim();
        if (query.length < 2) {
            search.summary.textContent = text['Search.TooShort'];
            search.results.innerHTML = '';
            return;
        }
        const seq = ++searchSeq;
        search.summary.textContent = text['Find.Searching'];
        search.results.innerHTML = '';
        try {
            const answer = await P.request(urls.search, null, { q: query, matchCase: search.matchCase.checked, hidden: state.showHidden });
            if (seq !== searchSeq) return;
            const groups = new Map();
            answer.matches.forEach((m) => {
                const key = P.keyOf(m.rootId, m.path);
                if (!groups.has(key)) groups.set(key, []);
                groups.get(key).push(m);
            });
            search.summary.textContent = answer.matches.length
                ? P.fmt(answer.truncated ? text['Search.SummaryMore'] : text['Search.Summary'], P.number(answer.matches.length), P.number(groups.size))
                : text['Search.None'];
            search.results.innerHTML = Array.from(groups.values()).map((items) => {
                const first = items[0];
                return `<li class="fx-search-file"><p class="fx-search-head"><span class="fx-node-icon" aria-hidden="true">${P.icon('file-text')}</span><strong>${P.escapeHtml(first.name)}</strong><span class="fx-search-path">${P.escapeHtml(`${first.rootName}/${P.parentPath(first.path)}`)}</span></p><ul>`
                    + items.map((m) => {
                        const before = P.escapeHtml(m.text.slice(0, m.offset));
                        const hit = P.escapeHtml(m.text.slice(m.offset, m.offset + query.length));
                        const after = P.escapeHtml(m.text.slice(m.offset + query.length));
                        return `<li><button type="button" class="fx-search-hit" data-root="${m.rootId}" data-path="${P.escapeHtml(m.path)}" data-line="${m.line}"><span class="fx-search-line">${P.number(m.line)}</span><code>${before}<mark>${hit}</mark>${after}</code></button></li>`;
                    }).join('') + '</ul></li>';
            }).join('');
        } catch (error) {
            if (seq === searchSeq) search.summary.textContent = error.message;
        }
    }

    search.form?.addEventListener('submit', (event) => {
        event.preventDefault();
        runSearch();
    });
    search.results?.addEventListener('click', (event) => {
        const hit = event.target.closest('[data-path]');
        if (!hit) return;
        P.modal('fx-search').hide();
        P.openFile(Number(hit.dataset.root), hit.dataset.path, { line: Number(hit.dataset.line) });
    });
    document.querySelector('[data-fx-search-open]')?.addEventListener('click', openSearch);

    // ------------------------------------------------------------------ Ctrl+K commands, shortcuts

    const palette = window.Toolbox?.palette;
    if (palette) {
        const add = (id, labelKey, iconName, run) => palette.add({ id: 'files-' + id, label: text[labelKey], icon: iconName, keywords: text['Palette.Keywords'], run });
        add('find', 'Palette.Find', 'search', () => { els.find.focus(); });
        add('search', 'Palette.Search', 'file-text', openSearch);
        add('new-file', 'Palette.NewFile', 'file-plus', () => document.querySelector('[data-fx-new-file]')?.click());
        add('new-folder', 'Palette.NewFolder', 'folder-plus', () => document.querySelector('[data-fx-new-folder]')?.click());
        add('add-root', 'Palette.AddRoot', 'folder-tree', () => document.querySelector('.fx-side [data-fx-add-root], [data-fx-welcome] [data-fx-add-root]')?.click());
        add('collapse', 'Palette.Collapse', 'chevrons-down-up', () => document.querySelector('[data-fx-collapse]')?.click());
        add('toggle-tree', 'Palette.ToggleTree', 'panel-left', () => P.toggleSide());
        add('show-in-tree', 'Palette.ShowInTree', 'folder-tree', () => P.showInTree(P.activeTab()));
        if (config.canOpenLocal) {
            add('open-default', 'Palette.OpenDefault', 'external-link', () => { const tab = P.activeTab(); if (tab) P.openOutside(tab, false); });
            add('reveal', 'Palette.Reveal', 'folder-open', () => { const tab = P.activeTab(); if (tab) P.openOutside(tab, true); });
        }
        add('hidden', 'Palette.Hidden', 'eye', () => { if (hiddenBox) { hiddenBox.checked = !hiddenBox.checked; hiddenBox.dispatchEvent(new Event('change')); } });
        add('pin', 'Palette.Pin', 'star', () => { const tab = P.activeTab(); if (tab) togglePin(tab); });
        add('toc', 'Palette.Toc', 'list-tree', () => { const tab = P.activeTab(); if (tab?.doc?.kind === 'markdown') toggleToc(tab); });
        add('export', 'Palette.Export', 'download', () => { const tab = P.activeTab(); if (tab?.doc?.kind === 'markdown') exportHtml(tab); });
        add('print', 'Palette.Print', 'printer', () => { const tab = P.activeTab(); if (tab?.doc) printDoc(tab); });
        if (document.querySelector('[data-fx-trash]')) add('trash', 'Palette.Trash', 'trash-2', () => document.querySelector('[data-fx-trash]')?.click());
    }

    const keys = window.Toolbox?.keys;
    if (keys) {
        keys.add({ keys: 'mod+shift+f', label: text['Keys.Search'], inInputs: true, run: openSearch });
        keys.add({ keys: 'mod+shift+p', label: text['Keys.Palette'], inInputs: true, run: () => palette?.open() });
    }

    // ------------------------------------------------------------------ a file opened from Explorer (desktop)

    async function openFromQuery() {
        const params = new URLSearchParams(window.location.search);
        const path = params.get('open');
        if (!path || !config.isDesktop) return;
        window.history.replaceState({}, '', window.location.pathname);
        try {
            let answer = await P.request(urls.openPath, { path, addRoot: false });
            if (!answer.rootId) {
                const ok = await window.Toolbox.confirm({
                    title: text['OpenPath.Title'],
                    message: P.fmt(text['OpenPath.Message'], path, answer.folder),
                    okText: text['OpenPath.Ok'],
                    tone: 'primary'
                });
                if (!ok) return;
                answer = await P.request(urls.openPath, { path, addRoot: true });
                await P.loadRoots();
            }
            await P.openFile(answer.rootId, answer.path);
        } catch (error) {
            P.notify(error.message, 'error');
        }
    }

    const ready = NS.onReady;
    NS.onReady = () => {
        ready?.();
        renderStart();
        renderFilterChip();
        openFromQuery();
    };
    renderStart();
})(window, document);
