// Compare text (/text-compare.html, Views/TextCompare/Index.cshtml). Everything runs in this
// browser and nothing is posted to the server:
// - jsdiff (window.Diff) finds the changed lines, then the changed words inside a pair of lines;
// - highlight.js (window.hljs) colours code, once per side so multi-line comments stay right;
// - the last comparisons (at most 10) are kept in localStorage under config.storageKey;
// - "Copy" on either side puts that side's text on the clipboard; "Export report" downloads a
//   standalone HTML file built by Content/js/text-compare-report.js;
// - the language box is a Kendo ComboBox (a plain <select> when Kendo is missing);
// - every line shows by default; "Collapse unchanged lines" (remembered) folds long equal runs.
// Shortcuts go through Toolbox.keys (Ctrl/⌘+Enter compares, J / K step through the changes).
(function () {
    'use strict';

    const root = document.querySelector('[data-text-compare]');
    if (!root || !window.Diff) {
        return;
    }

    const config = readJson('text-compare-config') || {};
    const text = config.text || {};
    const MAX_RECENT = config.maxRecent || 10;
    const CONTEXT = 3;
    const MIN_FOLD = 4;
    const PREFS_KEY = 'toolbox.textCompare.prefs.v1';
    const narrowQuery = window.matchMedia('(max-width: 899.98px)');
    const languageIds = (config.languages || []).map(function (x) { return x.id; });
    const languageNames = {};
    (config.languages || []).forEach(function (x) { languageNames[x.id] = x.name; });

    // File extensions the "Detect" language recognises before guessing from the text.
    const EXTENSIONS = {
        sql: 'sql', cs: 'csharp', csx: 'csharp', js: 'javascript', mjs: 'javascript', cjs: 'javascript', jsx: 'javascript',
        ts: 'typescript', tsx: 'typescript', json: 'json', xml: 'xml', html: 'xml', htm: 'xml', cshtml: 'xml', razor: 'xml',
        config: 'xml', csproj: 'xml', props: 'xml', svg: 'xml', xaml: 'xml', resx: 'xml', css: 'css', scss: 'css', less: 'css',
        yml: 'yaml', yaml: 'yaml', ps1: 'powershell', psm1: 'powershell', sh: 'bash', bash: 'bash', py: 'python',
        java: 'java', md: 'markdown', ini: 'ini', toml: 'ini', env: 'ini', properties: 'ini', txt: 'plaintext', log: 'plaintext'
    };

    const els = {
        viewGroup: root.querySelector('[data-tc-view-group]'),
        viewButtons: root.querySelectorAll('[data-tc-view]'),
        language: root.querySelector('[data-tc-language]'),
        swap: root.querySelector('[data-tc-swap]'),
        collapseSame: root.querySelector('[data-tc-collapse-same]'),
        exportButton: root.querySelector('[data-tc-export]'),
        input: root.querySelector('[data-tc-input]'),
        inputBody: root.querySelector('[data-tc-input-body]'),
        inputSummary: root.querySelector('[data-tc-input-summary]'),
        collapse: root.querySelector('[data-tc-collapse]'),
        collapseText: root.querySelector('[data-tc-collapse-text]'),
        clear: root.querySelector('[data-tc-clear]'),
        compare: root.querySelector('[data-tc-compare]'),
        resultHead: root.querySelector('[data-tc-result-head]'),
        stats: root.querySelector('[data-tc-stats]'),
        note: root.querySelector('[data-tc-note]'),
        body: root.querySelector('[data-tc-body]'),
        nav: root.querySelector('[data-tc-nav]'),
        position: root.querySelector('[data-tc-position]'),
        prev: root.querySelector('[data-tc-prev]'),
        next: root.querySelector('[data-tc-next]'),
        recentList: root.querySelector('[data-tc-recent-list]'),
        recentEmpty: root.querySelector('[data-tc-recent-empty]'),
        recentClear: root.querySelector('[data-tc-recent-clear]')
    };

    const sides = {
        left: buildSide('left'),
        right: buildSide('right')
    };

    const state = {
        view: 'split',
        language: 'auto',
        collapse: false,
        result: null,
        expanded: new Set(),
        current: -1
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
        return String(template || '').replace(/\{(\d+)\}/g, function (match, i) {
            return args[i] === undefined ? match : args[i];
        });
    }

    function number(value) {
        return window.Toolbox?.format ? window.Toolbox.format.number(value, 0) : String(value);
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

    function normalize(value) {
        return String(value || '').replace(/\r\n?/g, '\n');
    }

    // Lines of a text; a final line break does not add an empty line.
    function splitLines(value) {
        if (!value) {
            return [];
        }

        const lines = value.split('\n');
        if (lines[lines.length - 1] === '') {
            lines.pop();
        }

        return lines;
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
            window.localStorage.setItem(PREFS_KEY, JSON.stringify({ view: state.view, language: state.language, collapse: state.collapse }));
        } catch (error) {
            // Private window or storage blocked: the choice just is not remembered.
        }
    }

    // ------------------------------------------------------------------ input sides

    function buildSide(key) {
        const el = root.querySelector('[data-tc-side="' + key + '"]');
        const side = {
            key: key,
            el: el,
            textarea: el.querySelector('[data-tc-text]'),
            count: el.querySelector('[data-tc-count]'),
            fileBox: el.querySelector('[data-tc-file]'),
            fileNameEl: el.querySelector('[data-tc-file-name]'),
            fileInput: el.querySelector('[data-tc-file-input]'),
            drop: el.querySelector('[data-tc-drop]'),
            file: ''
        };

        side.textarea.addEventListener('input', function () { updateCount(side); });
        el.querySelector('[data-tc-paste]').addEventListener('click', function () { paste(side); });
        el.querySelector('[data-tc-open]').addEventListener('click', function () { side.fileInput.click(); });
        el.querySelector('[data-tc-file-clear]').addEventListener('click', function () {
            setFile(side, '');
            side.textarea.focus();
        });
        side.fileInput.addEventListener('change', function () {
            if (side.fileInput.files && side.fileInput.files[0]) {
                readFile(side, side.fileInput.files[0]);
            }
            side.fileInput.value = '';
        });

        // Drag and drop a file onto the box.
        let depth = 0;
        side.drop.addEventListener('dragenter', function (event) {
            if (!hasFiles(event)) return;
            event.preventDefault();
            depth++;
            side.drop.classList.add('is-dropping');
        });
        side.drop.addEventListener('dragover', function (event) {
            if (!hasFiles(event)) return;
            event.preventDefault();
            event.dataTransfer.dropEffect = 'copy';
        });
        side.drop.addEventListener('dragleave', function () {
            depth = Math.max(0, depth - 1);
            if (!depth) side.drop.classList.remove('is-dropping');
        });
        side.drop.addEventListener('drop', function (event) {
            if (!hasFiles(event)) return;
            event.preventDefault();
            depth = 0;
            side.drop.classList.remove('is-dropping');
            const file = event.dataTransfer.files[0];
            if (file) readFile(side, file);
        });

        return side;
    }

    function hasFiles(event) {
        return Boolean(event.dataTransfer && Array.prototype.indexOf.call(event.dataTransfer.types || [], 'Files') >= 0);
    }

    function updateCount(side) {
        const value = normalize(side.textarea.value);
        side.count.textContent = fmt(text.counts, number(value.length), number(splitLines(value).length));
    }

    function setFile(side, name) {
        side.file = name || '';
        side.fileBox.hidden = !side.file;
        side.fileNameEl.textContent = side.file;
        side.fileBox.title = side.file ? fmt(text.fileName, side.file) : '';
    }

    function setSide(side, value, file) {
        side.textarea.value = value || '';
        setFile(side, file);
        updateCount(side);
    }

    function paste(side) {
        if (!navigator.clipboard || typeof navigator.clipboard.readText !== 'function') {
            notify(text.pasteFailed, 'warning');
            side.textarea.focus();
            return;
        }

        navigator.clipboard.readText().then(function (value) {
            setSide(side, value, '');
            side.textarea.focus();
        }).catch(function () {
            notify(text.pasteFailed, 'warning');
            side.textarea.focus();
        });
    }

    function readFile(side, file) {
        if (file.size > (config.maxFileBytes || 5242880)) {
            notify(fmt(text.fileTooLarge, file.name), 'warning');
            return;
        }

        const reader = new FileReader();
        reader.onload = function () {
            const value = String(reader.result || '');
            // A NUL character in the first part means a binary file (image, zip, exe…).
            if (value.slice(0, 8000).indexOf('\u0000') >= 0) {
                notify(fmt(text.fileNotText, file.name), 'warning');
                return;
            }

            setSide(side, value, file.name);
        };
        reader.onerror = function () {
            notify(fmt(text.fileReadFailed, file.name), 'error');
        };
        reader.readAsText(file);
    }

    // A short name for a side: the file name, else the first non-empty line, else "Original"/"Changed".
    function displayName(side, value) {
        if (side.file) {
            return side.file;
        }

        const first = splitLines(value).map(function (line) { return line.trim(); }).find(Boolean);
        if (!first) {
            return side.key === 'left' ? text.left : text.right;
        }

        return first.length > 40 ? first.slice(0, 39) + '…' : first;
    }

    // ------------------------------------------------------------------ language and colours

    function detectLanguage(leftValue, rightValue) {
        const files = [sides.right.file, sides.left.file];
        for (let i = 0; i < files.length; i++) {
            const match = /\.([A-Za-z0-9]+)$/.exec(files[i] || '');
            const found = match && EXTENSIONS[match[1].toLowerCase()];
            if (found && (found === 'plaintext' || languageIds.indexOf(found) >= 0)) {
                return found;
            }
        }

        if (!window.hljs) {
            return 'plaintext';
        }

        const sample = (rightValue || leftValue || '').slice(0, 4000);
        try {
            const guess = window.hljs.highlightAuto(sample, languageIds);
            return guess.language && guess.relevance >= 5 ? guess.language : 'plaintext';
        } catch (error) {
            return 'plaintext';
        }
    }

    // HTML of each line, with highlight.js spans closed at the end of a line and opened again
    // on the next one, so a block comment spanning lines keeps its colour.
    function highlightLines(lines, language) {
        if (!lines.length) {
            return [];
        }

        const joined = lines.join('\n');
        let html;
        if (language === 'plaintext' || !window.hljs || !window.hljs.getLanguage(language)) {
            html = escapeHtml(joined);
        } else {
            try {
                html = window.hljs.highlight(joined, { language: language, ignoreIllegals: true }).value;
            } catch (error) {
                html = escapeHtml(joined);
            }
        }

        const out = [];
        const open = [];
        let current = '';
        const re = /(<span[^>]*>)|(<\/span>)|(\n)|([^<\n]+)/g;
        let match;
        while ((match = re.exec(html))) {
            if (match[1]) {
                open.push(match[1]);
                current += match[1];
            } else if (match[2]) {
                open.pop();
                current += match[2];
            } else if (match[3]) {
                out.push(current + '</span>'.repeat(open.length));
                current = open.join('');
            } else {
                current += match[4];
            }
        }
        out.push(current + '</span>'.repeat(open.length));

        // The tokenizer cannot lose lines, but never let a mismatch shift the numbering.
        if (out.length !== lines.length) {
            return lines.map(escapeHtml);
        }

        return out;
    }

    const ENTITIES = { '&amp;': '&', '&lt;': '<', '&gt;': '>', '&quot;': '"', '&#x27;': "'", '&#39;': "'" };

    // A highlighted line as [{ text, cls }] pieces, text unescaped.
    function pieces(lineHtml) {
        const out = [];
        const stack = [];
        const re = /<span class="([^"]*)">|<\/span>|<span[^>]*>|([^<]+)/g;
        let match;
        while ((match = re.exec(lineHtml))) {
            if (match[1] !== undefined) {
                stack.push(match[1]);
            } else if (match[2] !== undefined) {
                out.push({
                    text: match[2].replace(/&(?:amp|lt|gt|quot|#x27|#39);/g, function (e) { return ENTITIES[e]; }),
                    cls: stack.join(' ')
                });
            } else if (match[0] === '</span>') {
                stack.pop();
            } else {
                stack.push('');
            }
        }

        return out;
    }

    // The highlighted line with the changed character ranges wrapped as well.
    function markLine(lineHtml, ranges) {
        if (!ranges.length) {
            return lineHtml;
        }

        let html = '';
        let pos = 0;
        let r = 0;
        pieces(lineHtml).forEach(function (piece) {
            let offset = 0;
            while (offset < piece.text.length) {
                const at = pos + offset;
                while (r < ranges.length && ranges[r][1] <= at) r++;
                const inMark = r < ranges.length && ranges[r][0] <= at;
                const boundary = r < ranges.length ? (inMark ? ranges[r][1] : ranges[r][0]) : Infinity;
                const end = Math.min(pos + piece.text.length, boundary) - pos;
                const part = piece.text.slice(offset, end);
                const classes = [piece.cls, inMark ? 'tc-mark' : ''].join(' ').trim();
                html += classes ? '<span class="' + classes + '">' + escapeHtml(part) + '</span>' : escapeHtml(part);
                offset = end;
            }
            pos += piece.text.length;
        });

        return html;
    }

    // ------------------------------------------------------------------ comparing

    // Changed words of a pair of lines: ranges on each side and whether the lines are alike
    // enough to read as one edited line rather than one removed and one added.
    function wordDiff(a, b) {
        if (a.length + b.length > 6000) {
            return { paired: true, left: [], right: [] };
        }

        const parts = window.Diff.diffWordsWithSpace(a, b);
        const left = [];
        const right = [];
        let li = 0;
        let ri = 0;
        let common = 0;
        parts.forEach(function (part) {
            const n = part.value.length;
            if (part.added) {
                right.push([ri, ri + n]);
                ri += n;
            } else if (part.removed) {
                left.push([li, li + n]);
                li += n;
            } else {
                common += part.value.replace(/\s+/g, '').length;
                li += n;
                ri += n;
            }
        });

        const meaningful = a.replace(/\s+/g, '').length + b.replace(/\s+/g, '').length;
        const paired = meaningful === 0 || (2 * common) / meaningful >= 0.4;
        return { paired: paired, left: paired ? left : [], right: paired ? right : [] };
    }

    function buildRows(leftLines, rightLines) {
        const a = leftLines.length ? leftLines.join('\n') + '\n' : '';
        const b = rightLines.length ? rightLines.join('\n') + '\n' : '';
        const changes = window.Diff.diffLines(a, b, { timeout: 8000 });
        if (!changes) {
            return null;
        }

        const rows = [];
        const stats = { added: 0, removed: 0, changed: 0 };
        let blockCount = 0;
        let li = 0;
        let ri = 0;
        let removed = [];
        let added = [];

        function flush() {
            if (!removed.length && !added.length) {
                return;
            }

            const block = blockCount++;
            const length = Math.max(removed.length, added.length);
            for (let i = 0; i < length; i++) {
                const row = { kind: 'change', block: block, l: i < removed.length ? removed[i] : null, r: i < added.length ? added[i] : null, paired: false };
                if (row.l !== null && row.r !== null) {
                    const words = wordDiff(leftLines[row.l], rightLines[row.r]);
                    row.paired = words.paired;
                    row.leftRanges = words.left;
                    row.rightRanges = words.right;
                }

                if (row.paired) {
                    stats.changed++;
                } else {
                    if (row.l !== null) stats.removed++;
                    if (row.r !== null) stats.added++;
                }

                rows.push(row);
            }

            removed = [];
            added = [];
        }

        changes.forEach(function (change) {
            const count = change.count ?? splitLines(change.value).length;
            if (change.removed) {
                for (let i = 0; i < count; i++) removed.push(li++);
            } else if (change.added) {
                for (let i = 0; i < count; i++) added.push(ri++);
            } else {
                flush();
                for (let i = 0; i < count; i++) rows.push({ kind: 'same', l: li++, r: ri++ });
            }
        });
        flush();

        return { rows: rows, stats: stats, blockCount: blockCount };
    }

    function compare(options) {
        const settings = options || {};
        const leftValue = normalize(sides.left.textarea.value);
        const rightValue = normalize(sides.right.textarea.value);
        if (!leftValue && !rightValue) {
            notify(text.needBoth, 'warning');
            sides.left.textarea.focus();
            return;
        }

        const leftLines = splitLines(leftValue);
        const rightLines = splitLines(rightValue);
        const maxLines = config.maxLines || 50000;
        if (leftLines.length > maxLines || rightLines.length > maxLines) {
            showError(fmt(text.tooLarge, number(maxLines), number(leftLines.length), number(rightLines.length)));
            return;
        }

        const big = leftLines.length + rightLines.length > 4000;
        if (big) {
            showWorking();
        }

        // Let the loading state paint before a long comparison holds the page.
        window.setTimeout(function () {
            run(leftValue, rightValue, leftLines, rightLines, settings);
        }, big ? 30 : 0);
    }

    function run(leftValue, rightValue, leftLines, rightLines, settings) {
        let built;
        try {
            built = buildRows(leftLines, rightLines);
        } catch (error) {
            built = null;
        }

        if (!built) {
            showError(text.tooDifferent);
            return;
        }

        const language = state.language === 'auto' ? detectLanguage(leftValue, rightValue) : state.language;
        const limit = config.highlightMaxLines || 5000;
        const colour = language !== 'plaintext' && leftLines.length <= limit && rightLines.length <= limit;
        const usedLanguage = colour ? language : 'plaintext';

        state.result = {
            left: { name: displayName(sides.left, leftValue), file: sides.left.file, text: leftValue, lines: leftLines, html: highlightLines(leftLines, usedLanguage) },
            right: { name: displayName(sides.right, rightValue), file: sides.right.file, text: rightValue, lines: rightLines, html: highlightLines(rightLines, usedLanguage) },
            rows: built.rows,
            stats: built.stats,
            blockCount: built.blockCount,
            language: language,
            noHighlight: language !== 'plaintext' && !colour
        };
        state.expanded = new Set();
        // Start on the first change, so J moves to the second one.
        state.current = built.blockCount ? 0 : -1;

        updateAutoLabel(state.language === 'auto' ? language : null);
        render();
        setInputCollapsed(true);
        remember();
        if (!settings.quiet) {
            root.querySelector('[data-tc-result]').scrollIntoView({ block: 'start', behavior: 'smooth' });
        }
    }

    function updateAutoLabel(found) {
        picker.setAutoText(found
            ? fmt(text.autoFound, found === 'plaintext' ? text.plain : (languageNames[found] || found))
            : text.auto);
    }

    // The language box: a Kendo ComboBox that filters as the user types (the app's preferred
    // control), or a plain <select> in the same place when Kendo did not load. value() is the
    // highlight.js id, "auto" or "plaintext"; onChange runs only for a real change.
    function buildLanguagePicker(input, onChange) {
        const items = [{ id: 'auto', name: text.auto }, { id: 'plaintext', name: text.plain }]
            .concat((config.languages || []).map(function (x) { return { id: x.id, name: x.name }; }));
        const known = function (id) { return items.some(function (x) { return x.id === id; }); };
        let current = 'auto';
        const $ = window.jQuery;

        if ($ && $.fn && typeof $.fn.kendoComboBox === 'function') {
            const combo = $(input).kendoComboBox({
                dataSource: items,
                dataTextField: 'name',
                dataValueField: 'id',
                filter: 'contains',
                suggest: true,
                clearButton: false,
                change: function () {
                    const value = combo.value();
                    // Text that matches no language: go back to the last choice.
                    if (!known(value)) {
                        combo.value(current);
                        return;
                    }
                    if (value !== current) {
                        current = value;
                        onChange(value);
                    }
                }
            }).data('kendoComboBox');
            combo.value(current);
            return {
                value: function () { return current; },
                set: function (id) {
                    if (known(id)) {
                        current = id;
                        combo.value(id);
                    }
                },
                setAutoText: function (label) {
                    const auto = combo.dataSource.data().find(function (x) { return x.id === 'auto'; });
                    if (auto && auto.name !== label) {
                        auto.set('name', label);
                    }
                    if (current === 'auto') {
                        combo.value('auto');
                    }
                }
            };
        }

        const select = document.createElement('select');
        select.className = 'form-select';
        select.id = input.id;
        items.forEach(function (x) {
            const option = document.createElement('option');
            option.value = x.id;
            option.textContent = x.name;
            select.appendChild(option);
        });
        input.replaceWith(select);
        select.addEventListener('change', function () {
            current = select.value;
            onChange(current);
        });
        return {
            value: function () { return current; },
            set: function (id) {
                if (known(id)) {
                    current = id;
                    select.value = id;
                }
            },
            setAutoText: function (label) {
                select.querySelector('option[value="auto"]').textContent = label;
            }
        };
    }

    // ------------------------------------------------------------------ result states

    function setActions(enabled) {
        els.exportButton.disabled = !enabled;
    }

    function showWorking() {
        els.resultHead.hidden = true;
        els.nav.hidden = true;
        setActions(false);
        els.body.innerHTML = '<div class="tc-working" role="status"><span class="visually-hidden">' + escapeHtml(text.compare) + '</span>'
            + '<span class="tbx-skeleton"></span><span class="tbx-skeleton"></span><span class="tbx-skeleton"></span><span class="tbx-skeleton"></span></div>';
    }

    function showError(message) {
        state.result = null;
        els.resultHead.hidden = true;
        els.nav.hidden = true;
        setActions(false);
        els.body.innerHTML = '<div class="tbx-error-state tc-error" role="alert">' + icon('circle-alert', 16) + '<span>' + escapeHtml(message) + '</span></div>';
    }

    function showEmpty() {
        state.result = null;
        els.resultHead.hidden = true;
        els.nav.hidden = true;
        setActions(false);
        els.body.innerHTML = '<div class="tbx-empty" role="status"><span class="tbx-empty-icon">' + icon('file-diff', 20) + '</span>'
            + '<p class="tbx-empty-text">' + escapeHtml(root.dataset.emptyText || '') + '</p></div>';
    }

    function effectiveView() {
        return narrowQuery.matches ? 'unified' : state.view;
    }

    function render() {
        const result = state.result;
        if (!result) {
            return;
        }

        setActions(true);
        els.resultHead.hidden = false;
        const stats = result.stats;
        els.stats.innerHTML =
            '<span class="tbx-pill is-success">' + escapeHtml(fmt(text.added, number(stats.added))) + '</span>'
            + '<span class="tbx-pill is-danger">' + escapeHtml(fmt(text.removed, number(stats.removed))) + '</span>'
            + '<span class="tbx-pill is-warning">' + escapeHtml(fmt(text.changed, number(stats.changed))) + '</span>';
        els.note.hidden = !result.noHighlight;
        els.note.textContent = result.noHighlight ? fmt(text.noHighlight, number(config.highlightMaxLines || 5000)) : '';

        if (!result.blockCount) {
            els.nav.hidden = true;
            els.body.innerHTML = '<div class="tc-identical" role="status">' + icon('circle-check', 20)
                + '<span>' + escapeHtml(text.identical) + '</span></div>';
            return;
        }

        els.nav.hidden = false;
        els.body.innerHTML = effectiveView() === 'split' ? renderSplit(result) : renderUnified(result);
        markCurrent();
    }

    // Rows and folds in display order. Every line shows unless "Collapse unchanged lines" is on;
    // then long runs of unchanged lines become one fold, keeping CONTEXT lines next to each
    // change. Folds opened on the page (state.expanded) show every line; the report ignores them.
    function layout(result, forReport) {
        const items = [];
        const rows = result.rows;
        if (!state.collapse) {
            return rows.map(function (row, index) { return { type: 'row', index: index }; });
        }

        let i = 0;
        while (i < rows.length) {
            if (rows[i].kind !== 'same') {
                items.push({ type: 'row', index: i });
                i++;
                continue;
            }

            let end = i;
            while (end < rows.length && rows[end].kind === 'same') end++;
            const head = i === 0 ? 0 : CONTEXT;
            const tail = end === rows.length ? 0 : CONTEXT;
            const hidden = end - i - head - tail;
            if (hidden < MIN_FOLD || (!forReport && state.expanded.has(i))) {
                for (let j = i; j < end; j++) items.push({ type: 'row', index: j });
            } else {
                for (let j = i; j < i + head; j++) items.push({ type: 'row', index: j });
                items.push({ type: 'fold', key: i, from: i + head, to: end - tail, count: hidden });
                for (let j = end - tail; j < end; j++) items.push({ type: 'row', index: j });
            }

            i = end;
        }

        return items;
    }

    function lineHtml(result, sideKey, row) {
        const index = sideKey === 'left' ? row.l : row.r;
        if (index === null || index === undefined) {
            return '';
        }

        const base = result[sideKey].html[index] || '';
        if (row.kind === 'change' && row.paired) {
            const cacheKey = sideKey === 'left' ? 'leftHtml' : 'rightHtml';
            if (row[cacheKey] === undefined) {
                row[cacheKey] = markLine(base, sideKey === 'left' ? row.leftRanges : row.rightRanges);
            }
            return row[cacheKey];
        }

        return base;
    }

    // One cell's look: class, sign and the line number, for the page and for the report.
    function cell(result, sideKey, row) {
        const index = sideKey === 'left' ? row.l : row.r;
        if (index === null || index === undefined) {
            return { cls: 'is-empty', sign: '', num: '', html: '' };
        }

        let cls = 'is-same';
        let sign = '';
        if (row.kind === 'change') {
            if (row.paired) {
                cls = sideKey === 'left' ? 'is-old' : 'is-new';
                sign = '~';
            } else {
                cls = sideKey === 'left' ? 'is-del' : 'is-add';
                sign = sideKey === 'left' ? '-' : '+';
            }
        }

        return { cls: cls, sign: sign, num: index + 1, html: lineHtml(result, sideKey, row) };
    }

    function foldHtml(item, colspan) {
        return '<tr class="tc-fold"><td colspan="' + colspan + '"><button type="button" class="tc-fold-button" data-fold="' + item.key + '"'
            + ' aria-label="' + escapeHtml(fmt(text.foldAction, number(item.count))) + '">'
            + icon('chevrons-up-down', 14) + '<span>' + escapeHtml(fmt(text.fold, number(item.count))) + '</span></button></td></tr>';
    }

    // A small button that copies one side's text, in the column heads of the result.
    function headCopyHtml(sideKey) {
        const label = sideKey === 'left' ? text.copyLeft : text.copyRight;
        return '<button type="button" class="tc-head-copy" data-tc-copy-side="' + sideKey + '" data-from-result="true"'
            + ' aria-label="' + escapeHtml(label) + '" title="' + escapeHtml(label) + '">' + icon('copy', 14) + '</button>';
    }

    function headHtml(result, sideKey, colspan) {
        const side = result[sideKey];
        return '<th colspan="' + colspan + '" class="tc-head is-' + sideKey + '"><span class="tc-dot is-' + sideKey + '" aria-hidden="true"></span>'
            + '<span class="tc-head-name">' + escapeHtml(side.name) + '</span>'
            + '<span class="tc-head-role">' + escapeHtml(sideKey === 'left' ? text.left : text.right) + '</span>'
            + '<span class="tc-head-count">' + escapeHtml(fmt(text.lines, number(side.lines.length))) + '</span>'
            + headCopyHtml(sideKey) + '</th>';
    }

    function codeCell(c, side) {
        return '<td class="tc-num ' + c.cls + '">' + c.num + '</td>'
            + '<td class="tc-sign ' + c.cls + '">' + c.sign + '</td>'
            + '<td class="tc-code ' + c.cls + ' is-' + side + '">' + (c.html || ' ') + '</td>';
    }

    function renderSplit(result) {
        let previousBlock = -1;
        const body = layout(result, false).map(function (item) {
            if (item.type === 'fold') {
                return foldHtml(item, 6);
            }

            const row = result.rows[item.index];
            const first = row.kind === 'change' && row.block !== previousBlock;
            if (row.kind === 'change') previousBlock = row.block;
            const attrs = row.kind === 'change'
                ? ' class="tc-row is-change" data-block="' + row.block + '"' + (first ? ' data-change="' + row.block + '"' : '')
                : ' class="tc-row"';
            return '<tr' + attrs + '>' + codeCell(cell(result, 'left', row), 'left') + codeCell(cell(result, 'right', row), 'right') + '</tr>';
        }).join('');

        return '<table class="tc-table is-split">'
            + '<colgroup><col class="tc-col-num" /><col class="tc-col-sign" /><col /><col class="tc-col-num" /><col class="tc-col-sign" /><col /></colgroup>'
            + '<thead><tr>' + headHtml(result, 'left', 3) + headHtml(result, 'right', 3) + '</tr></thead>'
            + '<tbody>' + body + '</tbody></table>';
    }

    // Inline view: the lines of a change as all removed (old) lines, then all added (new) lines.
    function unifiedLines(result, items) {
        const out = [];
        let k = 0;
        while (k < items.length) {
            const item = items[k];
            if (item.type === 'fold') {
                out.push({ fold: item });
                k++;
                continue;
            }

            const row = result.rows[item.index];
            if (row.kind === 'same') {
                out.push({ row: row, side: 'both' });
                k++;
                continue;
            }

            const blockRows = [];
            while (k < items.length && items[k].type === 'row' && result.rows[items[k].index].kind === 'change'
                && result.rows[items[k].index].block === row.block) {
                blockRows.push(result.rows[items[k].index]);
                k++;
            }

            let first = true;
            blockRows.forEach(function (r) {
                if (r.l !== null) { out.push({ row: r, side: 'left', block: r.block, first: first }); first = false; }
            });
            blockRows.forEach(function (r) {
                if (r.r !== null) { out.push({ row: r, side: 'right', block: r.block, first: first }); first = false; }
            });
        }

        return out;
    }

    function renderUnified(result) {
        const body = unifiedLines(result, layout(result, false)).map(function (line) {
            if (line.fold) {
                return foldHtml(line.fold, 4);
            }

            const row = line.row;
            if (line.side === 'both') {
                return '<tr class="tc-row"><td class="tc-num is-same">' + (row.l + 1) + '</td><td class="tc-num is-same">' + (row.r + 1) + '</td>'
                    + '<td class="tc-sign is-same"></td><td class="tc-code is-same">' + (result.left.html[row.l] || ' ') + '</td></tr>';
            }

            const c = cell(result, line.side, row);
            const attrs = ' class="tc-row is-change" data-block="' + line.block + '"' + (line.first ? ' data-change="' + line.block + '"' : '');
            return '<tr' + attrs + '><td class="tc-num ' + c.cls + '">' + (line.side === 'left' ? c.num : '') + '</td>'
                + '<td class="tc-num ' + c.cls + '">' + (line.side === 'right' ? c.num : '') + '</td>'
                + '<td class="tc-sign ' + c.cls + '">' + c.sign + '</td><td class="tc-code ' + c.cls + '">' + (c.html || ' ') + '</td></tr>';
        }).join('');

        return '<table class="tc-table is-unified">'
            + '<colgroup><col class="tc-col-num" /><col class="tc-col-num" /><col class="tc-col-sign" /><col /></colgroup>'
            + '<thead><tr><th colspan="4" class="tc-head is-both">'
            + '<span class="tc-dot is-left" aria-hidden="true"></span><span class="tc-head-name">' + escapeHtml(result.left.name) + '</span>'
            + '<span class="tc-head-count">' + escapeHtml(fmt(text.lines, number(result.left.lines.length))) + '</span>'
            + headCopyHtml('left')
            + '<span class="tc-head-arrow" aria-hidden="true">→</span>'
            + '<span class="tc-dot is-right" aria-hidden="true"></span><span class="tc-head-name">' + escapeHtml(result.right.name) + '</span>'
            + '<span class="tc-head-count">' + escapeHtml(fmt(text.lines, number(result.right.lines.length))) + '</span>'
            + headCopyHtml('right')
            + '</th></tr></thead><tbody>' + body + '</tbody></table>';
    }

    // ------------------------------------------------------------------ change navigation

    function markCurrent() {
        const result = state.result;
        if (!result || !result.blockCount) {
            return;
        }

        els.body.querySelectorAll('tr.is-current').forEach(function (row) { row.classList.remove('is-current'); });
        if (state.current >= 0) {
            els.body.querySelectorAll('tr[data-block="' + state.current + '"]').forEach(function (row) { row.classList.add('is-current'); });
        }

        const shown = state.current >= 0 ? state.current + 1 : 0;
        els.position.textContent = number(shown) + ' / ' + number(result.blockCount);
        els.position.setAttribute('aria-label', fmt(text.position, number(shown), number(result.blockCount)));
    }

    function step(delta) {
        const result = state.result;
        if (!result || !result.blockCount) {
            return;
        }

        const count = result.blockCount;
        state.current = state.current < 0
            ? (delta > 0 ? 0 : count - 1)
            : (state.current + delta + count) % count;
        markCurrent();
        const target = els.body.querySelector('tr[data-change="' + state.current + '"]');
        if (target) {
            target.scrollIntoView({ block: 'center', behavior: 'smooth' });
        }
    }

    // ------------------------------------------------------------------ copy and export

    // Copies one side's text as it is: from the box, or the compared text from the result head.
    function copySide(sideKey, fromResult) {
        const value = fromResult && state.result ? state.result[sideKey].text : sides[sideKey].textarea.value;
        const name = sideKey === 'left' ? text.left : text.right;
        if (!value) {
            notify(fmt(text.copyEmpty, name), 'warning');
            return;
        }

        const done = function () { notify(sideKey === 'left' ? text.copiedLeft : text.copiedRight, 'success'); };
        if (navigator.clipboard && typeof navigator.clipboard.writeText === 'function') {
            navigator.clipboard.writeText(value).then(done).catch(function () { legacyCopy(value) ? done() : notify(text.copyFailed, 'error'); });
        } else if (legacyCopy(value)) {
            done();
        } else {
            notify(text.copyFailed, 'error');
        }
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

    function exportReport() {
        const result = state.result;
        if (!result || !window.ToolboxTextCompareReport) {
            return;
        }

        // The report shows every line; long unchanged runs go into a block the reader can open.
        const chunks = [];
        let rows = [];
        layout(result, true).forEach(function (item) {
            if (item.type === 'fold') {
                if (rows.length) chunks.push({ rows: rows });
                rows = [];
                const hidden = [];
                for (let j = item.from; j < item.to; j++) hidden.push(reportRow(result, result.rows[j]));
                chunks.push({ fold: fmt(text.reportUnchanged, number(item.count)), rows: hidden });
            } else {
                rows.push(reportRow(result, result.rows[item.index]));
            }
        });
        if (rows.length) chunks.push({ rows: rows });

        const now = new Date();
        const created = window.Toolbox?.format ? window.Toolbox.format.dateTime(now) : now.toLocaleString();
        const file = window.ToolboxTextCompareReport.download({
            lang: document.documentElement.lang || 'vi',
            title: text.reportTitle,
            created: fmt(text.reportCreated, created),
            footer: text.reportFooter,
            identical: result.blockCount ? '' : text.identical,
            left: { name: result.left.name, role: text.left, count: fmt(text.lines, number(result.left.lines.length)) },
            right: { name: result.right.name, role: text.right, count: fmt(text.lines, number(result.right.lines.length)) },
            stats: [
                { tone: 'success', label: fmt(text.added, number(result.stats.added)) },
                { tone: 'danger', label: fmt(text.removed, number(result.stats.removed)) },
                { tone: 'warning', label: fmt(text.changed, number(result.stats.changed)) }
            ],
            chunks: chunks,
            fileName: fileName(result, now)
        });
        notify(fmt(text.exported, file), 'success');
    }

    function reportRow(result, row) {
        return { left: cell(result, 'left', row), right: cell(result, 'right', row), change: row.kind === 'change' };
    }

    function slug(value) {
        return String(value || '')
            .normalize('NFD').replace(/[̀-ͯ]/g, '')
            .replace(/đ/g, 'd').replace(/Đ/g, 'D')
            .replace(/[^A-Za-z0-9]+/g, '-')
            .replace(/^-+|-+$/g, '')
            .slice(0, 30)
            .replace(/-+$/, '') || 'text';
    }

    function fileName(result, now) {
        const pad = function (n) { return String(n).padStart(2, '0'); };
        const stamp = now.getFullYear() + pad(now.getMonth() + 1) + pad(now.getDate()) + '-' + pad(now.getHours()) + pad(now.getMinutes());
        return [slug(text.reportFileStem || 'compare'), slug(result.left.name), slug(result.right.name), stamp].join('-') + '.html';
    }

    // ------------------------------------------------------------------ recent comparisons

    function loadRecent() {
        try {
            const list = JSON.parse(window.localStorage.getItem(config.storageKey) || '[]');
            return Array.isArray(list)
                ? list.filter(function (x) { return x && x.left && x.right && typeof x.left.text === 'string' && typeof x.right.text === 'string'; })
                : [];
        } catch (error) {
            return [];
        }
    }

    function saveRecent(list) {
        try {
            if (list.length) {
                window.localStorage.setItem(config.storageKey, JSON.stringify(list));
            } else {
                window.localStorage.removeItem(config.storageKey);
            }
            return true;
        } catch (error) {
            return false;
        }
    }

    function remember() {
        const result = state.result;
        const max = config.maxSavedChars || 300000;
        if (result.left.text.length > max || result.right.text.length > max) {
            notify(text.recentNotSaved, 'info');
            return;
        }

        let list = loadRecent().filter(function (x) {
            return !(x.left.text === result.left.text && x.right.text === result.right.text);
        });
        list.unshift({
            id: Date.now(),
            at: new Date().toISOString(),
            left: { name: result.left.name, file: result.left.file, text: result.left.text },
            right: { name: result.right.name, file: result.right.file, text: result.right.text },
            language: state.language,
            stats: result.stats
        });
        list = list.slice(0, MAX_RECENT);

        // Browser storage full: drop the oldest until the list fits.
        let saved = saveRecent(list);
        while (!saved && list.length > 1) {
            list.pop();
            saved = saveRecent(list);
        }
        if (!saved) {
            notify(text.recentNotSaved, 'info');
        }

        renderRecent();
    }

    function recentLabel(entry) {
        return entry.left.name + ' ' + text.vs + ' ' + entry.right.name;
    }

    function recentTime(iso) {
        const date = new Date(iso);
        if (Number.isNaN(date.getTime()) || !window.Toolbox?.format) {
            return '';
        }

        const format = window.Toolbox.format;
        return format.instantDate(date) === format.instantDate(new Date()) ? format.time(date) : format.instantDate(date);
    }

    function renderRecent() {
        const list = loadRecent();
        const current = state.result;
        els.recentEmpty.hidden = list.length > 0;
        els.recentClear.hidden = list.length === 0;
        els.recentList.innerHTML = list.map(function (entry) {
            const label = recentLabel(entry);
            const stats = entry.stats || {};
            const when = window.Toolbox?.format ? window.Toolbox.format.dateTime(new Date(entry.at)) : entry.at;
            const title = when + ' · +' + (stats.added || 0) + ' −' + (stats.removed || 0) + ' ~' + (stats.changed || 0);
            const selected = current && current.left.text === entry.left.text && current.right.text === entry.right.text;
            return '<span class="tc-recent-item' + (selected ? ' is-selected' : '') + '">'
                + '<button type="button" class="tc-recent-open" data-recent-open="' + entry.id + '" title="' + escapeHtml(title) + '"'
                + ' aria-label="' + escapeHtml(fmt(text.recentOpen, label)) + '">'
                + '<span class="tc-recent-name">' + escapeHtml(entry.left.name) + '</span>'
                + '<span class="tc-recent-vs">' + escapeHtml(text.vs) + '</span>'
                + '<span class="tc-recent-name">' + escapeHtml(entry.right.name) + '</span>'
                + '<span class="tc-recent-time">' + escapeHtml(recentTime(entry.at)) + '</span></button>'
                + '<button type="button" class="tc-recent-remove" data-recent-remove="' + entry.id + '"'
                + ' aria-label="' + escapeHtml(fmt(text.recentRemove, label)) + '" title="' + escapeHtml(fmt(text.recentRemove, label)) + '">'
                + icon('x', 12) + '</button></span>';
        }).join('');
    }

    function openRecent(id) {
        const entry = loadRecent().find(function (x) { return String(x.id) === String(id); });
        if (!entry) {
            renderRecent();
            return;
        }

        setSide(sides.left, entry.left.text, entry.left.file || '');
        setSide(sides.right, entry.right.text, entry.right.file || '');
        if (entry.language) {
            picker.set(entry.language);
            state.language = picker.value();
            writePrefs();
        }
        compare();
    }

    function removeRecent(id) {
        saveRecent(loadRecent().filter(function (x) { return String(x.id) !== String(id); }));
        renderRecent();
    }

    function clearRecent() {
        const count = loadRecent().length;
        if (!count) {
            return;
        }

        const ask = window.Toolbox?.confirm
            ? window.Toolbox.confirm({ title: text.recentClearTitle, message: fmt(text.recentClearMessage, number(count)), okText: text.recentClear })
            : Promise.resolve(window.confirm(fmt(text.recentClearMessage, number(count))));
        ask.then(function (confirmed) {
            if (confirmed) {
                saveRecent([]);
                renderRecent();
            }
        });
    }

    // ------------------------------------------------------------------ input panel

    function setInputCollapsed(collapsed) {
        els.inputBody.hidden = collapsed;
        els.collapse.setAttribute('aria-expanded', collapsed ? 'false' : 'true');
        els.collapseText.textContent = collapsed ? text.expand : text.collapse;
        els.collapse.querySelector('use')?.setAttribute('href', (els.collapse.querySelector('use').getAttribute('href') || '').replace(/#.*$/, collapsed ? '#chevron-down' : '#chevron-up'));
        els.input.classList.toggle('is-collapsed', collapsed);
        els.inputSummary.hidden = !collapsed;
        if (collapsed) {
            els.inputSummary.textContent = fmt(text.inputSummary,
                number(splitLines(normalize(sides.left.textarea.value)).length),
                number(splitLines(normalize(sides.right.textarea.value)).length));
        }
    }

    function setView(view) {
        state.view = view === 'unified' ? 'unified' : 'split';
        els.viewButtons.forEach(function (button) {
            const on = button.dataset.tcView === state.view;
            button.setAttribute('aria-pressed', on ? 'true' : 'false');
            button.classList.toggle('active', on);
        });
    }

    // ------------------------------------------------------------------ wiring

    root.dataset.emptyText = root.querySelector('.tc-result-body .tbx-empty-text')?.textContent || '';

    const picker = buildLanguagePicker(els.language, function (value) {
        state.language = value;
        writePrefs();
        updateAutoLabel(null);
        if (state.result) {
            compare({ quiet: true });
        }
    });

    function setCollapse(on) {
        state.collapse = Boolean(on);
        els.collapseSame.setAttribute('aria-pressed', state.collapse ? 'true' : 'false');
        els.collapseSame.classList.toggle('active', state.collapse);
    }

    const prefs = readPrefs();
    setView(prefs.view);
    setCollapse(prefs.collapse === true);
    if (prefs.language) {
        picker.set(prefs.language);
    }
    state.language = picker.value();

    els.viewButtons.forEach(function (button) {
        button.addEventListener('click', function () {
            setView(button.dataset.tcView);
            writePrefs();
            render();
        });
    });

    els.collapseSame.addEventListener('click', function () {
        setCollapse(!state.collapse);
        state.expanded = new Set();
        writePrefs();
        render();
    });

    // Copy buttons: one in each input box, and one in each column head of the result.
    root.addEventListener('click', function (event) {
        const button = event.target.closest('[data-tc-copy-side]');
        if (button) {
            copySide(button.dataset.tcCopySide, button.dataset.fromResult === 'true');
        }
    });

    els.swap.addEventListener('click', function () {
        const left = { value: sides.left.textarea.value, file: sides.left.file };
        setSide(sides.left, sides.right.textarea.value, sides.right.file);
        setSide(sides.right, left.value, left.file);
        if (state.result) {
            compare({ quiet: true });
        }
    });

    els.clear.addEventListener('click', function () {
        setSide(sides.left, '', '');
        setSide(sides.right, '', '');
        updateAutoLabel(null);
        showEmpty();
        renderRecent();
        sides.left.textarea.focus();
    });

    els.collapse.addEventListener('click', function () {
        setInputCollapsed(!els.inputBody.hidden);
        if (!els.inputBody.hidden) {
            sides.left.textarea.focus();
        }
    });

    els.compare.addEventListener('click', function () { compare(); });
    els.exportButton.addEventListener('click', exportReport);
    els.prev.addEventListener('click', function () { step(-1); });
    els.next.addEventListener('click', function () { step(1); });

    els.body.addEventListener('click', function (event) {
        const fold = event.target.closest('[data-fold]');
        if (fold) {
            state.expanded.add(parseInt(fold.dataset.fold, 10));
            render();
        }
    });

    els.recentList.addEventListener('click', function (event) {
        const remove = event.target.closest('[data-recent-remove]');
        if (remove) {
            removeRecent(remove.dataset.recentRemove);
            return;
        }

        const open = event.target.closest('[data-recent-open]');
        if (open) {
            openRecent(open.dataset.recentOpen);
        }
    });
    els.recentClear.addEventListener('click', clearRecent);

    narrowQuery.addEventListener('change', render);

    if (window.Toolbox?.keys) {
        window.Toolbox.keys.add({
            keys: 'mod+enter',
            label: text.compare,
            inInputs: true,
            button: els.compare,
            run: function () { compare(); }
        });
        window.Toolbox.keys.add({
            keys: ['j', 'k'],
            label: window.Toolbox.keys.label('nextPrevious'),
            when: function () { return Boolean(state.result && state.result.blockCount); },
            run: function (event) { step(event.key.toLowerCase() === 'j' ? 1 : -1); }
        });
    }

    updateCount(sides.left);
    updateCount(sides.right);
    renderRecent();
})();
