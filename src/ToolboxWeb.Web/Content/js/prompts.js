// Prompt list (Views/PromptTemplates/Index.cshtml), on top of Content/js/list-page.js:
// copy (counts one use), fill {{variables}} then copy, the full prompt in a side panel, the
// create/edit dialog with counters and a variable check, and the keyboard.
(function (window, document) {
    'use strict';

    const page = document.querySelector('[data-prompt-page]');
    if (!page) {
        return;
    }

    const prompts = readJson('prompt-data') || [];
    const text = readJson('prompt-text') || {};
    const byId = new Map(prompts.map(function (prompt) { return [String(prompt.id), prompt]; }));
    const VARIABLE = /\{\{\s*([A-Za-z0-9_]+)\s*\}\}/g;

    function readJson(id) {
        const node = document.getElementById(id);
        try {
            return node ? JSON.parse(node.textContent) : null;
        } catch (error) {
            return null;
        }
    }

    function format(template) {
        const args = Array.prototype.slice.call(arguments, 1);
        return String(template || '').replace(/\{(\d+)\}/g, function (match, index) {
            return args[index] !== undefined ? args[index] : match;
        });
    }

    function number(value) {
        return window.Toolbox?.format?.number ? window.Toolbox.format.number(value, 0) : String(value);
    }

    function notify(message, type) {
        if (window.Toolbox?.notify) {
            window.Toolbox.notify(message, type);
        }
    }

    // ------------------------------------------------------------ variables

    /** Names used as {{name}}, in order of first use. */
    function variablesIn(value) {
        const names = [];
        String(value || '').replace(VARIABLE, function (match, name) {
            if (names.indexOf(name) < 0) {
                names.push(name);
            }
            return match;
        });
        return names;
    }

    /** "{{name}}: what it is" lines of the declared variables, as name -> description. */
    function declaredVariables(value) {
        const map = new Map();
        String(value || '').split('\n').forEach(function (line) {
            const match = /\{\{\s*([A-Za-z0-9_]+)\s*\}\}\s*:?\s*(.*)$/.exec(line);
            if (match && !map.has(match[1])) {
                map.set(match[1], match[2].trim());
            }
        });
        return map;
    }

    // AGENTS.md: a prompt lists the same variable names it uses. Says which side is off.
    function mismatch(content, inputVariables) {
        const used = variablesIn(content);
        const declared = Array.from(declaredVariables(inputVariables).keys());
        const undeclared = used.filter(function (name) { return declared.indexOf(name) < 0; });
        const unused = declared.filter(function (name) { return used.indexOf(name) < 0; });
        const lines = [];
        if (undeclared.length) {
            lines.push(format(text.mismatchUndeclared, undeclared.map(wrap).join(', ')));
        }
        if (unused.length) {
            lines.push(format(text.mismatchUnused, unused.map(wrap).join(', ')));
        }
        return lines;
    }

    function wrap(name) {
        return '{{' + name + '}}';
    }

    // ------------------------------------------------------------ copy / use

    function rowOf(id) {
        return page.querySelector('[data-prompt-row][data-id="' + id + '"]');
    }

    async function writeClipboard(value) {
        try {
            await navigator.clipboard.writeText(value);
            return true;
        } catch (error) {
            notify(text.copyFailed, 'error');
            return false;
        }
    }

    // Copying is using: the count goes up in the background and the row shows it.
    async function markUsed(prompt) {
        const token = page.querySelector('input[name="__RequestVerificationToken"]');
        const body = new URLSearchParams({ id: String(prompt.id) });
        try {
            const response = await fetch(page.dataset.markUsedUrl, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/x-www-form-urlencoded',
                    'RequestVerificationToken': token ? token.value : '',
                    'X-Requested-With': 'XMLHttpRequest'
                },
                body: body
            });
            if (!response.ok) {
                return;
            }

            const data = await response.json();
            prompt.usedText = data.usedText;
            prompt.lastUsed = data.lastUsed;
            const meta = rowOf(prompt.id)?.querySelector('[data-prompt-used]');
            if (meta) {
                meta.firstChild.textContent = data.usedText;
                meta.querySelector('.prompt-last-used').textContent = data.lastUsed ? ' · ' + data.lastUsed : '';
            }
            if (current === prompt) {
                fillDetail(prompt);
            }
        } catch (error) {
            // The copy itself worked; a missed count is not worth an error.
        }
    }

    async function copyPrompt(prompt) {
        if (await writeClipboard(prompt.content)) {
            notify(format(text.copied, prompt.name), 'success');
            markUsed(prompt);
        }
    }

    // ------------------------------------------------------------ detail panel

    const panelElement = document.getElementById('prompt-detail');
    const panel = window.bootstrap ? window.bootstrap.Offcanvas.getOrCreateInstance(panelElement) : null;
    let current = null;

    function setDetail(name, value) {
        const node = panelElement.querySelector('[data-detail="' + name + '"]');
        node.textContent = value || '';
        const section = panelElement.querySelector('[data-detail-section="' + name + '"]');
        if (section) {
            section.hidden = !value;
        }
        if (name === 'description') {
            node.hidden = !value;
        }
    }

    function fillDetail(prompt) {
        setDetail('name', prompt.name);
        setDetail('category', prompt.categoryText);
        setDetail('used', prompt.usedText + (prompt.lastUsed ? ' · ' + prompt.lastUsed : ''));
        setDetail('description', prompt.description);
        setDetail('inputVariables', prompt.inputVariables);
        setDetail('outputFormat', prompt.outputFormat);
        setDetail('content', prompt.content);

        const warnings = mismatch(prompt.content, prompt.inputVariables);
        const box = panelElement.querySelector('[data-detail="mismatch"]');
        box.hidden = warnings.length === 0;
        box.innerHTML = '';
        if (warnings.length) {
            box.insertAdjacentHTML('beforeend', window.Toolbox?.icon ? window.Toolbox.icon('triangle-alert') : '');
            const list = document.createElement('div');
            warnings.forEach(function (line) {
                const p = document.createElement('p');
                p.textContent = line;
                list.appendChild(p);
            });
            box.appendChild(list);
        }

        panelElement.querySelector('[data-detail-action="fill"]').hidden = variablesIn(prompt.content).length === 0;
    }

    function showDetail(prompt) {
        current = prompt;
        page.querySelectorAll('[data-prompt-row]').forEach(function (row) {
            row.classList.toggle('is-selected', row.dataset.id === String(prompt.id));
        });
        fillDetail(prompt);
        panel?.show();
    }

    panelElement.addEventListener('click', function (event) {
        const action = event.target.closest('[data-detail-action]')?.dataset.detailAction;
        if (!action || !current) {
            return;
        }

        if (action === 'copy') {
            copyPrompt(current);
        } else if (action === 'fill') {
            openFill(current);
        } else if (action === 'edit') {
            panel?.hide();
            openEdit(current);
        }
    });

    panelElement.addEventListener('hidden.bs.offcanvas', function () {
        page.querySelectorAll('[data-prompt-row].is-selected').forEach(function (row) { row.classList.remove('is-selected'); });
    });

    // ------------------------------------------------------------ fill variables

    const fillModalElement = document.getElementById('prompt-fill');
    const fillFields = fillModalElement.querySelector('[data-fill-fields]');
    const fillPreview = fillModalElement.querySelector('[data-fill-preview]');
    let filling = null;

    function filledContent() {
        const values = {};
        fillFields.querySelectorAll('[data-variable]').forEach(function (input) {
            values[input.dataset.variable] = input.value;
        });
        return {
            text: filling.content.replace(VARIABLE, function (match, name) {
                return values[name] ? values[name] : match;
            }),
            missing: Object.keys(values).filter(function (name) { return !values[name]; }).length
        };
    }

    function renderFillPreview() {
        const result = filledContent();
        fillPreview.textContent = result.text;
        fillModalElement.querySelector('[data-fill-missing]').textContent = result.missing ? format(text.fillMissing, number(result.missing)) : '';
    }

    function openFill(prompt) {
        filling = prompt;
        const declared = declaredVariables(prompt.inputVariables);
        fillModalElement.querySelector('[data-fill-title]').textContent = format(text.fillTitle, prompt.name);
        fillFields.innerHTML = '';
        variablesIn(prompt.content).forEach(function (name, index) {
            const id = 'prompt-fill-' + index;
            const field = document.createElement('div');
            field.className = 'tbx-field';
            const label = document.createElement('label');
            label.className = 'tbx-field-label prompt-mono';
            label.htmlFor = id;
            label.textContent = wrap(name);
            const input = document.createElement('textarea');
            input.className = 'form-control';
            input.id = id;
            input.rows = 2;
            input.dataset.variable = name;
            field.appendChild(label);
            field.appendChild(input);
            const hint = declared.get(name);
            if (hint) {
                const hintNode = document.createElement('span');
                hintNode.className = 'tbx-field-hint';
                hintNode.id = id + '-hint';
                hintNode.textContent = hint;
                input.setAttribute('aria-describedby', hintNode.id);
                field.appendChild(hintNode);
            }
            fillFields.appendChild(field);
        });
        renderFillPreview();
        window.bootstrap.Modal.getOrCreateInstance(fillModalElement).show();
    }

    fillFields.addEventListener('input', renderFillPreview);
    fillModalElement.addEventListener('shown.bs.modal', function () {
        fillFields.querySelector('textarea')?.focus();
    });
    fillModalElement.querySelector('[data-fill-copy]').addEventListener('click', async function () {
        if (!filling) {
            return;
        }
        if (await writeClipboard(filledContent().text)) {
            notify(format(text.fillDone, filling.name), 'success');
            markUsed(filling);
            window.bootstrap.Modal.getOrCreateInstance(fillModalElement).hide();
        }
    });

    // ------------------------------------------------------------ create / edit form

    const formModal = document.querySelector('[data-prompt-form]');
    const form = formModal.querySelector('[data-prompt-form-element]');
    const field = function (name) { return form.querySelector('[data-field="' + name + '"]'); };

    function setFormMode(editing) {
        form.action = editing ? formModal.dataset.editAction : formModal.dataset.createAction;
        formModal.querySelector('[data-prompt-form-title]').textContent = editing ? formModal.dataset.editTitle : formModal.dataset.createTitle;
        formModal.querySelector('[data-prompt-form-submit]').textContent = editing ? formModal.dataset.editSubmit : formModal.dataset.createSubmit;
    }

    function clearErrors() {
        form.querySelectorAll('.tbx-field-error').forEach(function (node) { node.textContent = ''; });
        form.querySelectorAll('[aria-invalid]').forEach(function (node) { node.removeAttribute('aria-invalid'); });
    }

    function fillForm(prompt) {
        field('id').value = prompt ? prompt.id : '';
        field('name').value = prompt ? prompt.name : '';
        field('category').value = prompt ? prompt.category : field('category').options[0].value;
        field('description').value = prompt ? (prompt.description || '') : '';
        field('inputVariables').value = prompt ? (prompt.inputVariables || '') : '';
        field('outputFormat').value = prompt ? (prompt.outputFormat || '') : '';
        field('content').value = prompt ? prompt.content : '';
        clearErrors();
        setFormMode(Boolean(prompt));
        updateFormHints();
    }

    function openEdit(prompt) {
        fillForm(prompt);
        window.bootstrap.Modal.getOrCreateInstance(formModal).show();
    }

    // The primary button and the empty-state action open a blank form; a form reopened after a
    // failed save keeps what was typed.
    formModal.addEventListener('show.bs.modal', function (event) {
        if (event.relatedTarget) {
            fillForm(null);
        }
    });
    formModal.addEventListener('shown.bs.modal', function () {
        field('name').focus();
    });
    // A page opened to edit (after "Duplicate") drops ?editId once the dialog closes, so a
    // reload does not open it again.
    formModal.addEventListener('hidden.bs.modal', function () {
        const url = new URL(window.location.href);
        if (url.searchParams.has('editId')) {
            url.searchParams.delete('editId');
            window.history.replaceState(null, '', url.pathname + url.search + url.hash);
        }
    });

    function updateFormHints() {
        form.querySelectorAll('[data-count-max]').forEach(function (input) {
            const hint = form.querySelector('[data-count-for="' + input.id + '"]');
            if (hint) {
                hint.textContent = format(text.count, number(input.value.length), number(Number(input.dataset.countMax)));
                hint.classList.toggle('is-near', input.value.length > Number(input.dataset.countMax) * 0.9);
            }
        });

        const content = field('content').value;
        const lines = content ? content.split('\n').length : 0;
        form.querySelector('[data-content-stats]').textContent =
            format(text.stats, number(content.length), number(lines), number(variablesIn(content).length));

        const warnings = mismatch(content, field('inputVariables').value);
        const box = form.querySelector('[data-form-mismatch]');
        box.hidden = warnings.length === 0;
        box.textContent = warnings.join(' ');
    }

    form.addEventListener('input', updateFormHints);
    form.addEventListener('submit', function (event) {
        // The required fields are checked here too, so the dialog does not round-trip for them.
        let firstInvalid = null;
        ['name', 'content'].forEach(function (name) {
            const input = field(name);
            const error = document.getElementById(input.id + '-error');
            const empty = !input.value.trim();
            if (empty) {
                input.setAttribute('aria-invalid', 'true');
            } else {
                input.removeAttribute('aria-invalid');
            }
            error.textContent = empty ? formModal.dataset.requiredText : '';
            firstInvalid = firstInvalid || (empty ? input : null);
        });
        if (firstInvalid) {
            event.preventDefault();
            firstInvalid.focus();
        }
    });
    updateFormHints();

    // ------------------------------------------------------------ rows and keyboard

    function promptOf(element) {
        const row = element.closest('[data-prompt-row]');
        return row ? byId.get(row.dataset.id) : null;
    }

    page.querySelectorAll('[data-prompt-row]').forEach(function (row) {
        const prompt = byId.get(row.dataset.id);
        if (!prompt) {
            return;
        }
        row.querySelector('[data-prompt-fill]').hidden = variablesIn(prompt.content).length === 0;
        row.querySelector('[data-prompt-mismatch]').hidden = mismatch(prompt.content, prompt.inputVariables).length === 0;
    });

    page.addEventListener('click', function (event) {
        const prompt = promptOf(event.target);
        if (!prompt) {
            return;
        }

        if (event.target.closest('[data-prompt-copy]')) {
            copyPrompt(prompt);
        } else if (event.target.closest('[data-prompt-fill]')) {
            openFill(prompt);
        } else if (event.target.closest('[data-prompt-edit]')) {
            openEdit(prompt);
        } else if (!event.target.closest('form, button, a')) {
            showDetail(prompt);
        }
    });

    // The sort list applies itself; empty values stay out of the address.
    const sort = page.querySelector('[data-prompt-sort]');
    sort?.querySelector('select').addEventListener('change', function () {
        sort.querySelectorAll('input[type="hidden"]').forEach(function (input) {
            input.disabled = !input.value;
        });
        sort.submit();
    });

    // Shortcuts (Toolbox.keys, Content/js/shortcuts.js): ↑ ↓ move between rows, Enter opens the
    // prompt, C copies, E edits. "/" and "N" come from list-page.js.
    const keys = window.Toolbox?.keys;
    if (keys) {
        const allRows = function () { return Array.from(page.querySelectorAll('[data-prompt-row]')); };
        const activeRow = function () {
            return document.activeElement?.closest?.('[data-prompt-row]') || page.querySelector('[data-prompt-row].is-selected');
        };
        const activePrompt = function () { const row = activeRow(); return row ? byId.get(row.dataset.id) : null; };

        keys.add({
            keys: ['arrowdown', 'arrowup'],
            label: keys.label('move'),
            display: ['↑ ↓'],
            when: function () { return allRows().length > 0; },
            run: function (event) {
                const rows = allRows();
                const index = rows.indexOf(activeRow());
                const next = rows[Math.min(rows.length - 1, Math.max(0, index + (event.key === 'ArrowDown' ? 1 : -1)))] || rows[0];
                next.focus();
                if (panelElement.classList.contains('show')) {
                    showDetail(byId.get(next.dataset.id));
                }
            }
        });
        keys.add({
            keys: 'enter',
            label: keys.label('open'),
            when: function () { const row = activeRow(); return Boolean(row && document.activeElement === row); },
            run: function () { showDetail(activePrompt()); }
        });
        keys.add({ keys: 'c', label: keys.label('copy'), when: activePrompt, run: function () { copyPrompt(activePrompt()); } });
        keys.add({ keys: 'e', label: keys.label('edit'), when: activePrompt, run: function () { openEdit(activePrompt()); } });
    }

    // ------------------------------------------------------------ restore built-in prompts

    // "Restore" stays off until at least one prompt is ticked.
    const restore = document.querySelector('[data-prompt-restore]');
    const restoreSubmit = restore?.querySelector('[data-restore-submit]');
    if (restoreSubmit) {
        const syncRestore = function () {
            restoreSubmit.disabled = !restore.querySelector('input[type="checkbox"]:checked');
        };
        restore.addEventListener('change', syncRestore);
        syncRestore();
    }

    // Opened from the command palette on one prompt (?open=<id>): show it in the side panel.
    const openId = new URLSearchParams(window.location.search).get('open');
    if (openId && byId.has(openId)) {
        showDetail(byId.get(openId));
        const url = new URL(window.location.href);
        url.searchParams.delete('open');
        window.history.replaceState(null, '', url.pathname + url.search + url.hash);
    }
})(window, document);
