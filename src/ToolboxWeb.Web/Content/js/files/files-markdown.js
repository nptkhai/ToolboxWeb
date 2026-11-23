// Markdown for "Files & folders" (window.ToolboxFiles.markdown). markdown-it turns the text into
// HTML, DOMPurify cleans it (the file may come from anywhere: no scripts, frames, forms, styles
// or event handlers), then the page adds what GitHub readers expect: callouts ("> [!NOTE]"),
// checklists, heading anchors, a Copy button on code blocks, tables that scroll, links to other
// files of the same root opened in Toolkit, and images of the root shown through /Files/Raw.
// Every block element carries data-line (its first source line) for scrolling with the editor.
(function (window, document) {
    'use strict';

    const NS = window.ToolboxFiles = window.ToolboxFiles || {};
    const CALLOUTS = ['note', 'tip', 'important', 'warning', 'caution'];
    const CALLOUT_ICONS = { note: 'info', tip: 'circle-check', important: 'circle-alert', warning: 'triangle-alert', caution: 'ban' };
    let parser = null;

    function escapeHtml(value) {
        return String(value ?? '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
    }

    function highlight(code, lang) {
        const hljs = window.hljs;
        const language = (lang || '').trim().toLowerCase();
        if (hljs && language && hljs.getLanguage(language)) {
            try {
                return hljs.highlight(code, { language, ignoreIllegals: true }).value;
            } catch (error) {
                // Shown plain below.
            }
        }
        return escapeHtml(code);
    }

    // data-line on every block that has a source position.
    function lineNumbers(md) {
        md.core.ruler.push('fx_lines', (state) => {
            state.tokens.forEach((token) => {
                if (token.map && token.nesting !== -1 && token.block) {
                    token.attrSet('data-line', String(token.map[0]));
                }
            });
        });
        const defaultFence = md.renderer.rules.fence;
        md.renderer.rules.fence = function (tokens, index, options, env, self) {
            const token = tokens[index];
            const lang = (token.info || '').trim().split(/\s+/)[0];
            const line = token.map ? ` data-line="${token.map[0]}"` : '';
            const label = lang ? ` data-lang="${escapeHtml(lang)}"` : '';
            return `<pre class="fx-pre"${line}${label}><code class="hljs">${highlight(token.content, lang)}</code></pre>\n`;
        };
        md.renderer.rules.code_block = function (tokens, index) {
            const token = tokens[index];
            const line = token.map ? ` data-line="${token.map[0]}"` : '';
            return `<pre class="fx-pre"${line}><code class="hljs">${escapeHtml(token.content)}</code></pre>\n`;
        };
        return defaultFence;
    }

    // "- [ ] task" and "- [x] done".
    function taskLists(md) {
        md.core.ruler.after('inline', 'fx_tasks', (state) => {
            const tokens = state.tokens;
            for (let i = 2; i < tokens.length; i++) {
                const inline = tokens[i];
                if (inline.type !== 'inline' || tokens[i - 1].type !== 'paragraph_open' || tokens[i - 2].type !== 'list_item_open') {
                    continue;
                }
                const match = /^\[([ xX])\]\s+/.exec(inline.content);
                const first = inline.children && inline.children[0];
                if (!match || !first || first.type !== 'text' || !/^\[([ xX])\]\s+/.test(first.content)) {
                    continue;
                }
                first.content = first.content.replace(/^\[([ xX])\]\s+/, '');
                const box = new state.Token('html_inline', '', 0);
                box.content = `<input type="checkbox" class="fx-task-box" disabled${match[1].trim() ? ' checked' : ''}> `;
                inline.children.unshift(box);
                tokens[i - 2].attrJoin('class', 'fx-task');
            }
        });
    }

    function md() {
        if (parser || !window.markdownit) {
            return parser;
        }
        parser = window.markdownit({ html: true, linkify: true, typographer: false, breaks: false });
        lineNumbers(parser);
        taskLists(parser);
        return parser;
    }

    function slug(text, used) {
        let base = String(text).toLowerCase().normalize('NFKD').replace(/[̀-ͯ]/g, '').replace(/đ/g, 'd')
            .replace(/[^a-z0-9\s-]/g, '').trim().replace(/\s+/g, '-') || 'section';
        let id = base;
        let n = 1;
        while (used.has(id)) {
            id = `${base}-${n++}`;
        }
        used.add(id);
        return id;
    }

    /** "docs/a.md" + "../img/x.png" -> "img/x.png"; null when it leaves the root or is not relative. */
    function resolveRelative(fromFile, href) {
        if (!href || /^[a-z][a-z0-9+.-]*:/i.test(href) || href.startsWith('//') || href.startsWith('#')) {
            return null;
        }
        let target = href.split('#')[0].split('?')[0];
        try {
            target = decodeURIComponent(target);
        } catch (error) {
            // Kept as written.
        }
        const parts = target.startsWith('/') ? [] : String(fromFile || '').split('/').slice(0, -1);
        for (const part of target.split('/')) {
            if (!part || part === '.') continue;
            if (part === '..') {
                if (!parts.length) return null;
                parts.pop();
            } else {
                parts.push(part);
            }
        }
        return parts.join('/');
    }

    function callouts(root, text) {
        root.querySelectorAll('blockquote').forEach((quote) => {
            const first = quote.firstElementChild;
            if (!first || first.tagName !== 'P') return;
            const match = /^\s*\[!(\w+)\]\s*/.exec(first.textContent || '');
            const kind = match && match[1].toLowerCase();
            if (!kind || !CALLOUTS.includes(kind)) return;
            // Remove the marker from the first text node (it may be followed by a line break).
            const walker = document.createTreeWalker(first, NodeFilter.SHOW_TEXT);
            const node = walker.nextNode();
            if (node) node.textContent = node.textContent.replace(/^\s*\[!\w+\]\s*/, '');
            if (first.firstChild && first.firstChild.nodeName === 'BR') first.firstChild.remove();
            if (!first.textContent.trim() && !first.querySelector('img')) first.remove();
            quote.classList.add('fx-callout', `fx-callout-${kind}`);
            const title = document.createElement('p');
            title.className = 'fx-callout-title';
            title.innerHTML = (window.Toolbox?.icon ? window.Toolbox.icon(CALLOUT_ICONS[kind], 16) : '') + `<span>${escapeHtml(text[`Md.Callout.${kind}`] || kind)}</span>`;
            quote.insertBefore(title, quote.firstChild);
        });
    }

    /**
     * Renders <paramref name="text"/> into <paramref name="container"/>. ctx: { path (the file),
     * rawUrl(path), openPath(path, anchor), text }.
     */
    function render(container, source, ctx) {
        const parserInstance = md();
        const text = ctx.text || {};
        if (!parserInstance || !window.DOMPurify) {
            container.textContent = source;
            return;
        }

        // YAML front matter ("---" … "---" on the first lines) becomes small labels above the text;
        // its lines are kept as blank lines so data-line still matches the source.
        const front = /^---[ \t]*\r?\n([\s\S]*?)\r?\n---[ \t]*(?:\r?\n|$)/.exec(source);
        let body = source;
        const fields = [];
        if (front && front[1].split('\n').length <= 60) {
            front[1].split(/\r?\n/).forEach((line) => {
                line.split(/\s+\|\s+/).forEach((part) => {
                    const match = /^\s*([^:#][^:]*?)\s*:\s*(.*)$/.exec(part);
                    if (match && match[2].trim()) fields.push([match[1].trim(), match[2].trim().replace(/^["']|["']$/g, '')]);
                });
            });
            body = '\n'.repeat(front[0].split('\n').length - 1) + source.slice(front[0].length);
        }

        const html = parserInstance.render(body);
        const clean = window.DOMPurify.sanitize(html, {
            ADD_ATTR: ['data-line', 'data-lang', 'target'],
            FORBID_TAGS: ['style', 'form', 'button', 'textarea', 'select', 'iframe', 'object', 'embed', 'video', 'audio', 'link', 'meta', 'base'],
            FORBID_ATTR: ['style', 'srcset', 'formaction'],
            ALLOW_DATA_ATTR: false
        });
        container.innerHTML = clean;
        if (fields.length) {
            const list = document.createElement('dl');
            list.className = 'fx-frontmatter';
            list.dataset.line = '0';
            list.innerHTML = fields.slice(0, 20).map(([key, value]) => `<div><dt>${escapeHtml(key)}</dt><dd>${escapeHtml(value)}</dd></div>`).join('');
            container.insertBefore(list, container.firstChild);
        }

        // Only the checklist boxes stay as inputs.
        container.querySelectorAll('input').forEach((input) => {
            if (input.type !== 'checkbox') {
                input.remove();
                return;
            }
            input.disabled = true;
            input.setAttribute('aria-label', input.checked ? (text['Md.TaskDone'] || '') : (text['Md.TaskOpen'] || ''));
        });

        callouts(container, text);

        const used = new Set();
        container.querySelectorAll('h1, h2, h3, h4, h5, h6').forEach((heading) => {
            heading.id = 'fx-h-' + slug(heading.textContent, used);
        });

        container.querySelectorAll('table').forEach((table) => {
            const wrap = document.createElement('div');
            wrap.className = 'fx-table-wrap';
            table.parentNode.insertBefore(wrap, table);
            wrap.appendChild(table);
        });

        container.querySelectorAll('pre.fx-pre').forEach((pre) => {
            const button = document.createElement('button');
            button.type = 'button';
            button.className = 'btn btn-sm fx-pre-copy';
            button.innerHTML = (window.Toolbox?.icon ? window.Toolbox.icon('copy', 14) : '') + `<span>${escapeHtml(text['Md.CopyCode'])}</span>`;
            button.addEventListener('click', async () => {
                // NS.copyPlain (files.js) also works on plain http, where navigator.clipboard is missing.
                const value = pre.querySelector('code')?.textContent || '';
                const done = NS.copyPlain ? await NS.copyPlain(value)
                    : await (navigator.clipboard?.writeText(value).then(() => true, () => false) ?? false);
                window.Toolbox?.notify(text[done ? 'Md.Copied' : 'Doc.CopyFailed'], done ? 'success' : 'error');
            });
            pre.appendChild(button);
        });

        container.querySelectorAll('img[src]').forEach((img) => {
            const target = resolveRelative(ctx.path, img.getAttribute('src'));
            if (target !== null) {
                img.src = ctx.rawUrl(target);
            }
            img.loading = 'lazy';
        });

        container.querySelectorAll('a[href]').forEach((link) => {
            const href = link.getAttribute('href');
            if (href.startsWith('#')) {
                link.addEventListener('click', (event) => {
                    const id = decodeURIComponent(href.slice(1)).toLowerCase();
                    const heading = Array.from(container.querySelectorAll('[id^="fx-h-"]')).find((h) => h.id === 'fx-h-' + id || h.id === 'fx-h-' + slug(id, new Set()));
                    if (heading) {
                        event.preventDefault();
                        heading.scrollIntoView({ block: 'start' });
                    }
                });
                return;
            }
            const target = resolveRelative(ctx.path, href);
            if (target !== null) {
                link.addEventListener('click', (event) => {
                    event.preventDefault();
                    ctx.openPath(target, href.includes('#') ? href.split('#')[1] : null);
                });
                link.title = target;
                return;
            }
            link.target = '_blank';
            link.rel = 'noopener noreferrer';
        });
    }

    /** Headings for a table of contents: [{ level, text, id, line }]. */
    function headings(container) {
        return Array.from(container.querySelectorAll('h1, h2, h3, h4')).map((h) => ({
            level: Number(h.tagName.charAt(1)),
            text: h.textContent.trim(),
            id: h.id,
            line: Number(h.dataset.line || 0)
        }));
    }

    NS.markdown = { render, headings, resolveRelative };
})(window, document);
