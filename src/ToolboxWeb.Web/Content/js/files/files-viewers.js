// Viewers of "Files & folders" (window.ToolboxFiles.viewers): one per kind of file read by
// GET /Files/Read. render(body, doc, ctx) fills the tab's body and returns
// { copyText, stats: { lines, words, chars }, destroy() }.
// - markdown: files-markdown.js; json: a tree that folds (error with line and column);
// - csv: Papa Parse + Tabulator (only the rows on screen are drawn);
// - image / pdf: the file itself through /Files/Raw (the browser's own PDF viewer);
// - text: line numbers and highlight.js colours (plain above 1 MB);
// - office, binary, too large: the file's details with "Open with…" (desktop) or Download.
(function (window, document) {
    'use strict';

    const NS = window.ToolboxFiles = window.ToolboxFiles || {};
    const HIGHLIGHT_LIMIT = 1024 * 1024;
    const CSV_LIMIT = 50000;
    const LANGUAGES = {
        js: 'javascript', mjs: 'javascript', cjs: 'javascript', jsx: 'javascript', ts: 'typescript', tsx: 'typescript',
        json: 'json', jsonc: 'json', css: 'css', scss: 'scss', less: 'less', html: 'xml', htm: 'xml', xml: 'xml', svg: 'xml',
        xaml: 'xml', csproj: 'xml', props: 'xml', targets: 'xml', config: 'xml', resx: 'xml', cshtml: 'xml', razor: 'xml',
        cs: 'csharp', vb: 'vbnet', sql: 'sql', py: 'python', rb: 'ruby', php: 'php', java: 'java', kt: 'kotlin', go: 'go',
        rs: 'rust', c: 'c', h: 'c', cpp: 'cpp', hpp: 'cpp', swift: 'swift', lua: 'lua', pl: 'perl', r: 'r',
        sh: 'bash', bash: 'bash', zsh: 'bash', ps1: 'powershell', psm1: 'powershell', bat: 'shell', cmd: 'shell',
        yml: 'yaml', yaml: 'yaml', ini: 'ini', toml: 'ini', env: 'ini', properties: 'ini', editorconfig: 'ini', gitignore: 'ini',
        md: 'markdown', markdown: 'markdown', graphql: 'graphql', gql: 'graphql', diff: 'diff', patch: 'diff', makefile: 'makefile'
    };

    function escapeHtml(value) {
        return String(value ?? '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
    }

    function icon(name, size) {
        return window.Toolbox?.icon ? window.Toolbox.icon(name, size || 16) : '';
    }

    function fmt(template) {
        const args = Array.prototype.slice.call(arguments, 1);
        return String(template || '').replace(/\{(\d+)\}/g, (m, i) => (args[i] === undefined ? m : args[i]));
    }

    function number(value) {
        return window.Toolbox?.format ? window.Toolbox.format.number(value, 0) : String(value);
    }

    function textStats(text) {
        const value = text || '';
        return {
            lines: value ? value.split('\n').length : 0,
            words: (value.match(/[^\s]+/g) || []).length,
            chars: value.length
        };
    }

    function extension(name) {
        const lower = String(name || '').toLowerCase();
        if (lower === 'makefile' || lower === 'dockerfile') return lower === 'makefile' ? 'makefile' : 'bash';
        const dot = lower.lastIndexOf('.');
        return dot >= 0 ? lower.slice(dot + 1) : '';
    }

    function languageOf(name) {
        return LANGUAGES[extension(name)] || null;
    }

    // ------------------------------------------------------------------ text

    function codeBlock(text, name, plain) {
        const lines = (text || '').split('\n');
        if (lines.length && lines[lines.length - 1] === '' && lines.length > 1) lines.pop();
        const gutter = lines.map((_, i) => i + 1).join('\n');
        let body = escapeHtml(lines.join('\n'));
        const language = languageOf(name);
        if (!plain && language && window.hljs?.getLanguage(language) && (text || '').length <= HIGHLIGHT_LIMIT) {
            try {
                body = window.hljs.highlight(lines.join('\n'), { language, ignoreIllegals: true }).value;
            } catch (error) {
                // Plain text.
            }
        }
        return `<div class="fx-code"><pre class="fx-gutter" aria-hidden="true">${gutter}</pre><pre class="fx-code-text"><code class="hljs">${body}</code></pre></div>`;
    }

    function renderText(body, doc) {
        body.innerHTML = codeBlock(doc.text, doc.name, doc.plain);
        return { copyText: doc.text, stats: textStats(doc.text) };
    }

    // ------------------------------------------------------------------ markdown

    function renderMarkdown(body, doc, ctx) {
        const article = document.createElement('article');
        article.className = 'fx-md';
        body.innerHTML = '';
        body.appendChild(article);
        if (doc.plain) {
            body.innerHTML = codeBlock(doc.text, doc.name, true);
        } else {
            NS.markdown.render(article, doc.text, {
                path: doc.path,
                text: ctx.text,
                rawUrl: (path) => ctx.rawUrl(path),
                openPath: (path, anchor) => ctx.openPath(path, anchor)
            });
        }
        return { copyText: doc.text, stats: textStats(doc.text), article };
    }

    // ------------------------------------------------------------------ json

    /** Line and column of a JSON.parse error, from "position N" when the browser gives it. */
    function jsonErrorPosition(text, error) {
        const message = String(error && error.message || '');
        let match = /line (\d+) column (\d+)/i.exec(message);
        if (match) return { line: Number(match[1]), column: Number(match[2]) };
        match = /position (\d+)/i.exec(message);
        if (!match) return null;
        const index = Number(match[1]);
        const before = text.slice(0, index).split('\n');
        return { line: before.length, column: before[before.length - 1].length + 1 };
    }

    function jsonNode(key, value, depth, text) {
        const keyHtml = key === null ? '' : `<span class="fx-json-key">${escapeHtml(key)}</span><span class="fx-json-colon">: </span>`;
        if (value !== null && typeof value === 'object') {
            const isArray = Array.isArray(value);
            const entries = isArray ? value.map((v, i) => [String(i), v]) : Object.entries(value);
            const summary = isArray ? fmt(text['Json.Items'], number(entries.length)) : fmt(text['Json.Keys'], number(entries.length));
            const open = depth < 2 ? ' open' : '';
            const children = entries.slice(0, 5000).map(([k, v]) => `<li>${jsonNode(isArray ? null : k, v, depth + 1, text)}</li>`).join('');
            return `<details class="fx-json-node"${open}><summary>${keyHtml}<span class="fx-json-brace">${isArray ? '[' : '{'}</span><span class="fx-json-count">${escapeHtml(summary)}</span><span class="fx-json-brace">${isArray ? ']' : '}'}</span></summary><ul>${children}</ul></details>`;
        }
        const type = value === null ? 'null' : typeof value;
        const shown = type === 'string' ? `"${escapeHtml(value)}"` : escapeHtml(String(value));
        return `<span class="fx-json-leaf">${keyHtml}<span class="fx-json-${type}">${shown}</span></span>`;
    }

    function renderJson(body, doc, ctx) {
        const text = ctx.text;
        let parsed;
        let failure = null;
        const source = (doc.text || '').replace(/^﻿/, '');
        try {
            parsed = JSON.parse(source);
        } catch (error) {
            failure = jsonErrorPosition(source, error) || { line: 0, column: 0 };
        }

        if (failure || doc.plain) {
            body.innerHTML = (failure ? `<div class="alert alert-danger fx-inline-alert" role="alert">${icon('triangle-alert')}<span>${escapeHtml(fmt(text['Json.Error'], failure.line, failure.column))}</span></div>` : '')
                + codeBlock(doc.text, doc.name, doc.plain);
            if (failure && failure.line) {
                const gutter = body.querySelector('.fx-gutter');
                const lines = gutter.textContent.split('\n');
                gutter.innerHTML = lines.map((n) => (Number(n) === failure.line ? `<mark>${n}</mark>` : n)).join('\n');
                setTimeout(() => {
                    const lineHeight = parseFloat(getComputedStyle(gutter).lineHeight) || 20;
                    body.scrollTop = Math.max(0, (failure.line - 5) * lineHeight);
                }, 0);
            }
            return { copyText: doc.text, stats: textStats(doc.text), invalid: Boolean(failure) };
        }

        body.innerHTML = `<div class="fx-json-bar"><button type="button" class="btn btn-sm btn-outline-secondary" data-fx-json-toggle>${escapeHtml(text['Json.ShowText'])}</button></div><div class="fx-json" data-fx-json-tree>${jsonNode(null, parsed, 0, text)}</div><div data-fx-json-text hidden>${codeBlock(doc.text, doc.name, false)}</div>`;
        const toggle = body.querySelector('[data-fx-json-toggle]');
        toggle.addEventListener('click', () => {
            const tree = body.querySelector('[data-fx-json-tree]');
            const raw = body.querySelector('[data-fx-json-text]');
            const showText = raw.hidden;
            raw.hidden = !showText;
            tree.hidden = showText;
            toggle.textContent = showText ? text['Json.ShowTree'] : text['Json.ShowText'];
        });
        return { copyText: doc.text, stats: textStats(doc.text) };
    }

    // ------------------------------------------------------------------ csv

    function renderCsv(body, doc, ctx) {
        const text = ctx.text;
        if (!window.Papa || !window.Tabulator || doc.plain) {
            return renderText(body, doc);
        }
        const result = window.Papa.parse((doc.text || '').replace(/^﻿/, ''), {
            skipEmptyLines: 'greedy',
            delimitersToGuess: [',', ';', '\t', '|'],
            preview: CSV_LIMIT + 1
        });
        const rows = result.data || [];
        if (!rows.length) {
            body.innerHTML = `<div class="tbx-empty" role="status"><span class="tbx-empty-icon">${icon('table', 20)}</span><p class="tbx-empty-text">${escapeHtml(text['Csv.Empty'])}</p></div>`;
            return { copyText: doc.text, stats: textStats(doc.text) };
        }
        const header = rows[0];
        const width = Math.max(...rows.slice(0, 200).map((r) => r.length));
        const data = rows.slice(1, CSV_LIMIT + 1).map((row, index) => {
            const item = { _n: index + 1 };
            for (let c = 0; c < width; c++) item['c' + c] = row[c] ?? '';
            return item;
        });
        const numeric = [];
        for (let c = 0; c < width; c++) {
            const sample = data.slice(0, 200).map((r) => r['c' + c]).filter((v) => v !== '');
            numeric[c] = sample.length > 0 && sample.every((v) => /^-?[\d\s.,]+%?$/.test(v));
        }
        const truncated = rows.length > CSV_LIMIT + 1;
        body.innerHTML = `<p class="fx-csv-meta">${escapeHtml(fmt(text['Csv.Rows'], number(data.length), number(width)))}${truncated ? ' · ' + escapeHtml(fmt(text['Csv.Truncated'], number(CSV_LIMIT))) : ''}</p><div class="fx-csv" data-fx-csv></div>`;
        const columns = [{ title: '#', field: '_n', width: 64, hozAlign: 'center', headerSort: false, frozen: true }];
        for (let c = 0; c < width; c++) {
            columns.push({
                title: header[c] !== undefined && header[c] !== '' ? String(header[c]) : String(c + 1),
                field: 'c' + c,
                headerHozAlign: 'center',
                hozAlign: numeric[c] ? 'right' : 'left',
                minWidth: 90,
                formatter: 'plaintext',
                tooltip: true
            });
        }
        const table = new window.Tabulator(body.querySelector('[data-fx-csv]'), {
            data,
            columns,
            layout: width > 6 ? 'fitData' : 'fitColumns',
            height: '100%',
            renderVertical: 'virtual',
            columnHeaderVertAlign: 'middle'
        });
        return { copyText: doc.text, stats: textStats(doc.text), destroy: () => table.destroy() };
    }

    // ------------------------------------------------------------------ image, pdf, others

    function renderImage(body, doc, ctx) {
        body.innerHTML = `<figure class="fx-image"><img alt="${escapeHtml(doc.name)}" src="${escapeHtml(ctx.rawUrl(doc.path))}" /><figcaption data-fx-image-size></figcaption></figure>`;
        const img = body.querySelector('img');
        img.addEventListener('load', () => {
            body.querySelector('[data-fx-image-size]').textContent = fmt(ctx.text['Image.Size'], number(img.naturalWidth), number(img.naturalHeight));
        });
        return { copyText: null, stats: null };
    }

    function renderPdf(body, doc, ctx) {
        body.innerHTML = `<iframe class="fx-pdf" title="${escapeHtml(doc.name)}" src="${escapeHtml(ctx.rawUrl(doc.path))}"></iframe>`;
        const frame = body.querySelector('iframe');
        return { copyText: null, stats: null, print: () => frame.contentWindow?.print() };
    }

    function renderInfo(body, doc, ctx) {
        const text = ctx.text;
        const reason = doc.tooLarge ? text['Info.TooLarge'] : doc.kind === 'office' ? text['Info.Office'] : text['Info.Binary'];
        const icons = { office: 'file-text', binary: 'file-archive' };
        const action = ctx.isDesktop
            ? `<button type="button" class="btn btn-primary" data-fx-open-default>${icon('external-link')}<span>${escapeHtml(text['Info.OpenDefault'])}</span></button>`
            : `<a class="btn btn-primary" href="${escapeHtml(ctx.rawUrl(doc.path, true))}">${icon('download')}<span>${escapeHtml(text['Info.Download'])}</span></a>`;
        body.innerHTML = `<div class="tbx-empty fx-info" role="status"><span class="tbx-empty-icon">${icon(icons[doc.kind] || 'file', 20)}</span><p class="tbx-empty-text fx-empty-title">${escapeHtml(doc.name)}</p><p class="tbx-empty-text">${escapeHtml(reason)}</p>${action}</div>`;
        body.querySelector('[data-fx-open-default]')?.addEventListener('click', () => ctx.openDefault(doc));
        return { copyText: null, stats: null };
    }

    function render(body, doc, ctx) {
        body.classList.remove('is-fill');
        if (doc.tooLarge || doc.kind === 'office' || doc.kind === 'binary') return renderInfo(body, doc, ctx);
        switch (doc.kind) {
            case 'markdown': return renderMarkdown(body, doc, ctx);
            case 'json': return renderJson(body, doc, ctx);
            case 'csv': body.classList.add('is-fill'); return renderCsv(body, doc, ctx);
            case 'image': return renderImage(body, doc, ctx);
            case 'pdf': body.classList.add('is-fill'); return renderPdf(body, doc, ctx);
            default: return renderText(body, doc);
        }
    }

    NS.viewers = { render, languageOf, textStats, extension };
})(window, document);
