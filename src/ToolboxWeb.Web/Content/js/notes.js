/*
 * Notes page — progressive enhancement only.
 *
 * Everything that changes data is a normal form post that works without this file. This adds
 * the conveniences: relative times, copy/download/format tools, a save button that lights up
 * only when something changed (plus Ctrl+S and a leave-page warning), keyboard shortcuts, a
 * confirmation before deleting, and on-demand reveal of stored server passwords.
 */
(function () {
    'use strict';

    const root = document.getElementById('nv-root');
    if (!root) {
        return;
    }

    const config = JSON.parse(document.getElementById('nv-config').textContent);
    const messages = config.messages;
    const CODE_FORMATS = ['2', '3', '4', '5'];
    // Which editor a format is edited with; anything else uses the plain text box.
    const SLOT_BY_FORMAT = { '6': 'link', '7': 'server', '9': 'task' };
    const SECRET_TTL_MS = 30000;

    // Bootstrap puts the backdrop on <body>, but .app-shell has its own z-index, so a dialog left
    // inside it paints under the backdrop and cannot be clicked. Move the dialogs to <body>. The
    // host keeps .content-area so the site's form styles still apply; CSS gives it display: contents.
    const modalHost = document.createElement('div');
    modalHost.className = 'content-area nv-modal-host';
    document.querySelectorAll('.nv-modal').forEach(function (modal) {
        modalHost.appendChild(modal);
    });
    document.body.appendChild(modalHost);

    // ------------------------------------------------------------------ helpers

    function format(template) {
        const args = Array.prototype.slice.call(arguments, 1);
        return template.replace(/\{(\d+)\}/g, function (match, index) {
            return args[index] !== undefined ? args[index] : match;
        });
    }

    function antiforgeryToken() {
        const input = root.querySelector('input[name="__RequestVerificationToken"]');
        return input ? input.value : '';
    }

    function isTyping(target) {
        return target && (target.isContentEditable || /^(INPUT|TEXTAREA|SELECT)$/.test(target.tagName));
    }

    // navigator.clipboard needs a secure context; the site may be served over plain http on a
    // LAN address, so fall back to the old selection-based copy there.
    async function copyText(text) {
        if (navigator.clipboard && window.isSecureContext) {
            await navigator.clipboard.writeText(text);
            return;
        }

        const helper = document.createElement('textarea');
        helper.value = text;
        helper.setAttribute('readonly', '');
        helper.style.position = 'fixed';
        helper.style.opacity = '0';
        document.body.appendChild(helper);
        helper.select();
        const ok = document.execCommand('copy');
        document.body.removeChild(helper);
        if (!ok) {
            throw new Error('copy failed');
        }
    }

    // Briefly swaps a button's label (or tooltip, for icon-only buttons) to confirm an action.
    function flash(button, text, isError) {
        const label = button.querySelector('span:not(.visually-hidden)');
        const previousTitle = button.getAttribute('title');
        const previousLabel = label ? label.textContent : null;

        button.classList.toggle('is-done', !isError);
        if (label) {
            label.textContent = text;
        }
        button.setAttribute('title', text);

        window.setTimeout(function () {
            button.classList.remove('is-done');
            if (label) {
                label.textContent = previousLabel;
            }
            if (previousTitle === null) {
                button.removeAttribute('title');
            } else {
                button.setAttribute('title', previousTitle);
            }
        }, 1600);
    }

    function setStatus(block, text, kind) {
        const status = block && block.querySelector('[data-role="code-status"]');
        if (!status) {
            return;
        }

        status.textContent = text || '';
        status.classList.toggle('is-ok', kind === 'ok');
        status.classList.toggle('is-error', kind === 'error');
    }

    // Vietnamese-safe file name: strip accents (and đ), keep letters and digits.
    function fileSlug(title) {
        const slug = (title || '')
            .normalize('NFD')
            .replace(/[̀-ͯ]/g, '')
            .replace(/đ/g, 'd')
            .replace(/Đ/g, 'D')
            .replace(/[^A-Za-z0-9]+/g, '-')
            .replace(/^-+|-+$/g, '')
            .toLowerCase()
            .slice(0, 60);
        return slug || 'note';
    }

    // ------------------------------------------------------------------ relative time

    const relative = window.Intl && Intl.RelativeTimeFormat
        ? new Intl.RelativeTimeFormat(document.documentElement.lang || undefined, { numeric: 'auto' })
        : null;

    const UNITS = [
        ['year', 31536000],
        ['month', 2592000],
        ['week', 604800],
        ['day', 86400],
        ['hour', 3600],
        ['minute', 60]
    ];

    // The server writes the absolute time; this only replaces the text and leaves the
    // absolute value in the tooltip.
    function renderRelativeTimes() {
        if (!relative) {
            return;
        }

        const now = Date.now();
        root.querySelectorAll('time[data-relative]').forEach(function (node) {
            const at = Date.parse(node.getAttribute('datetime'));
            if (isNaN(at)) {
                return;
            }

            const seconds = Math.round((at - now) / 1000);
            const unit = UNITS.find(function (entry) { return Math.abs(seconds) >= entry[1]; });
            node.textContent = unit
                ? relative.format(Math.round(seconds / unit[1]), unit[0])
                : relative.format(0, 'minute');
        });
    }

    // ------------------------------------------------------------------ unsaved changes

    const tracked = [];
    let leavingOnPurpose = false;

    function snapshot(form) {
        const data = new FormData(form);
        data.delete('__RequestVerificationToken');
        return new URLSearchParams(data).toString();
    }

    function trackForm(form) {
        const save = form.querySelector('[data-save]');
        const entry = { form: form, initial: snapshot(form), dirty: false };

        function refresh() {
            entry.dirty = snapshot(form) !== entry.initial;
            if (save) {
                save.disabled = !entry.dirty;
            }
        }

        form.addEventListener('input', refresh);
        form.addEventListener('change', refresh);
        form.addEventListener('submit', function () {
            leavingOnPurpose = true;
        });

        tracked.push(entry);
        refresh();
    }

    function anyDirty() {
        return tracked.some(function (entry) { return entry.dirty; });
    }

    window.addEventListener('beforeunload', function (event) {
        if (!leavingOnPurpose && anyDirty()) {
            event.preventDefault();
            event.returnValue = messages.unsaved;
        }
    });

    // Ctrl+S saves the form being edited, or the main note form, instead of saving the web page.
    function saveCurrent() {
        const active = document.activeElement;
        let form = active && active.closest ? active.closest('form[data-dirty-track]') : null;
        if (!form) {
            form = root.querySelector('form.nv-edit');
        }

        const entry = tracked.find(function (candidate) { return candidate.form === form; });
        if (form && entry && entry.dirty) {
            form.requestSubmit();
        }
    }

    // ------------------------------------------------------------------ content tools

    const tools = {
        copy: async function (button, target) {
            try {
                await copyText(target.value);
                flash(button, messages.copied);
            } catch (error) {
                flash(button, messages.copyFailed, true);
            }
        },

        download: function (button, target) {
            const name = fileSlug(button.dataset.fileName) + '.' + (button.dataset.extension || 'txt');
            const blob = new Blob([target.value], { type: (button.dataset.mime || 'text/plain') + ';charset=utf-8' });
            const url = URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = url;
            link.download = name;
            document.body.appendChild(link);
            link.click();
            link.remove();
            window.setTimeout(function () { URL.revokeObjectURL(url); }, 1000);
        },

        'json-format': function (button, target) {
            const block = target.closest('[data-content-block]');
            try {
                const parsed = JSON.parse(target.value);
                target.value = JSON.stringify(parsed, null, 2);
                target.dispatchEvent(new Event('input', { bubbles: true }));
                setStatus(block, messages.jsonValid, 'ok');
            } catch (error) {
                setStatus(block, format(messages.jsonInvalid, error.message), 'error');
            }
        },

        'xml-check': function (button, target) {
            const block = target.closest('[data-content-block]');
            const documentNode = new DOMParser().parseFromString(target.value, 'application/xml');
            const problem = documentNode.getElementsByTagName('parsererror')[0];
            if (problem) {
                const detail = (problem.textContent || '').trim().split('\n')[0];
                setStatus(block, format(messages.xmlInvalid, detail), 'error');
            } else {
                setStatus(block, messages.xmlValid, 'ok');
            }
        }
    };

    // ------------------------------------------------------------------ server passwords

    const secretCache = new Map();

    // Each reveal is a POST that the server logs, so the value is kept briefly in memory to
    // avoid one log line per button press, and forgotten again after half a minute.
    async function fetchSecret(noteId) {
        const cached = secretCache.get(noteId);
        if (cached && Date.now() - cached.at < SECRET_TTL_MS) {
            return cached.value;
        }

        const body = new URLSearchParams({ id: noteId });
        const response = await fetch(config.urls.reveal, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
                'RequestVerificationToken': antiforgeryToken(),
                'X-Requested-With': 'XMLHttpRequest'
            },
            body: body,
            credentials: 'same-origin',
            cache: 'no-store'
        });

        if (!response.ok) {
            throw new Error(messages.networkError);
        }

        const result = await response.json();
        if (!result.success) {
            throw new Error(result.message || messages.networkError);
        }

        secretCache.set(noteId, { value: result.password, at: Date.now() });
        window.setTimeout(function () { secretCache.delete(noteId); }, SECRET_TTL_MS);
        return result.password;
    }

    // SqlConnectionStringBuilder quoting: a value containing ; ' " or edge spaces is wrapped
    // in double quotes, with inner double quotes doubled.
    function connectionValue(value) {
        if (/[;'"]|^\s|\s$/.test(value)) {
            return '"' + value.replace(/"/g, '""') + '"';
        }
        return value;
    }

    async function buildConnectionString(scope) {
        const fields = scope.closest('[data-server-fields]');
        const read = function (name) {
            const input = fields.querySelector('[data-field="' + name + '"]');
            return input ? input.value.trim() : '';
        };

        const host = read('host');
        if (!host) {
            throw new Error(messages.hostMissing);
        }

        const port = read('port');
        const parts = ['Server=' + connectionValue(port ? host + ',' + port : host)];
        const database = read('db');
        const user = read('user');

        if (database) {
            parts.push('Database=' + connectionValue(database));
        }
        if (user) {
            parts.push('User Id=' + connectionValue(user));
        }

        // A password typed but not yet saved wins over the stored one.
        const typed = fields.querySelector('[data-field="password"]');
        let password = typed && typed.value ? typed.value : '';
        if (!password && scope.dataset.hasPassword === 'true') {
            password = await fetchSecret(scope.dataset.noteId);
        }
        if (password) {
            parts.push('Password=' + connectionValue(password));
        }

        return parts.join(';') + ';';
    }

    const secretActions = {
        reveal: async function (button, scope) {
            const output = scope.querySelector('[data-role="secret-value"]');
            const label = button.querySelector('[data-role="reveal-label"]');
            const icon = button.querySelector('use');

            if (output.dataset.revealed === 'true') {
                hideSecret(output, label, icon);
                return;
            }

            output.textContent = await fetchSecret(scope.dataset.noteId);
            output.dataset.revealed = 'true';
            if (label) {
                label.textContent = messages.hide;
            }
            if (icon) {
                icon.setAttribute('href', '#nv-i-eye-off');
            }

            window.setTimeout(function () { hideSecret(output, label, icon); }, SECRET_TTL_MS);
        },

        copy: async function (button, scope) {
            await copyText(await fetchSecret(scope.dataset.noteId));
            flash(button, messages.passwordCopied);
        },

        connection: async function (button, scope) {
            await copyText(await buildConnectionString(scope));
            flash(button, messages.connectionCopied);
        }
    };

    function hideSecret(output, label, icon) {
        output.textContent = '••••••••';
        output.dataset.revealed = 'false';
        if (label) {
            label.textContent = messages.reveal;
        }
        if (icon) {
            icon.setAttribute('href', '#nv-i-eye');
        }
    }

    // ------------------------------------------------------------------ row editors

    // Link and checklist notes are edited as rows. Each row posts its own values, so adding or
    // removing one is pure DOM work: no ids to renumber, and the form still posts without script.
    function markChanged(node) {
        const form = node.closest('form');
        if (form) {
            form.dispatchEvent(new Event('input', { bubbles: true }));
        }
    }

    // The "open" button follows what is typed, and stays inert while that is not a web address.
    function syncRow(row) {
        const url = row.querySelector('[data-row-url]');
        const open = row.querySelector('[data-row-open]');
        if (!url || !open) {
            return;
        }

        const value = url.value.trim();
        if (/^https?:\/\/\S+$/i.test(value)) {
            open.setAttribute('href', value);
            open.removeAttribute('aria-disabled');
        } else {
            open.removeAttribute('href');
            open.setAttribute('aria-disabled', 'true');
        }
    }

    function addRow(button) {
        const rows = button.closest('[data-rows]');
        const template = rows.querySelector('[data-row-template]');
        const row = template.content.firstElementChild.cloneNode(true);

        template.before(row);
        syncRow(row);
        markChanged(row);

        const first = row.querySelector('input');
        if (first) {
            first.focus();
        }
    }

    // The last row is emptied rather than removed, so there is always somewhere to type.
    function removeRow(button) {
        const rows = button.closest('[data-rows]');
        const row = button.closest('.nv-row');

        if (rows.querySelectorAll('.nv-row').length <= 1) {
            row.querySelectorAll('input').forEach(function (input) { input.value = ''; });
            syncRow(row);
        } else {
            row.remove();
        }

        markChanged(rows);
    }

    async function copyRow(button) {
        const row = button.closest('.nv-row');
        const field = button.dataset.rowField === 'text'
            ? row.querySelector('.nv-row-text')
            : row.querySelector('[data-row-url]');
        const value = field ? field.value.trim() : '';

        if (!value) {
            flash(button, messages.copyFailed, true);
            return;
        }

        await copyText(value);
        flash(button, messages.copied);
    }

    // ------------------------------------------------------------------ delete confirmation

    const deleteModalElement = document.getElementById('nv-delete');
    let pendingDelete = null;

    function confirmDelete(form) {
        if (!deleteModalElement || !window.bootstrap) {
            return true;
        }

        const children = parseInt(form.dataset.children || '0', 10);
        const message = children > 0
            ? format(messages.deleteConfirmWithChildren, form.dataset.title, children)
            : format(messages.deleteConfirm, form.dataset.title);
        deleteModalElement.querySelector('[data-role="delete-message"]').textContent = message;

        pendingDelete = form;
        bootstrap.Modal.getOrCreateInstance(deleteModalElement).show();
        return false;
    }

    if (deleteModalElement) {
        deleteModalElement.querySelector('[data-role="delete-confirm"]').addEventListener('click', function () {
            if (pendingDelete) {
                leavingOnPurpose = true;
                // submit() skips the submit event, so this does not ask a second time.
                pendingDelete.submit();
            }
        });

        deleteModalElement.addEventListener('shown.bs.modal', function () {
            deleteModalElement.querySelector('[data-role="delete-confirm"]').focus();
        });
    }

    // ------------------------------------------------------------------ textarea sizing

    // Grows a textarea to its content (up to most of the screen) but never shrinks one the
    // user has dragged taller.
    function autosize(textarea) {
        const limit = Math.round(window.innerHeight * 0.7);
        if (textarea.scrollHeight > textarea.clientHeight) {
            textarea.style.height = Math.min(textarea.scrollHeight + 4, limit) + 'px';
        }
    }

    // ------------------------------------------------------------------ wiring

    document.addEventListener('click', function (event) {
        const tool = event.target.closest('[data-tool]');
        if (tool && tools[tool.dataset.tool]) {
            const target = document.querySelector(tool.dataset.target);
            if (target) {
                event.preventDefault();
                tools[tool.dataset.tool](tool, target);
            }
            return;
        }

        const addButton = event.target.closest('[data-add-row]');
        if (addButton) {
            event.preventDefault();
            addRow(addButton);
            return;
        }

        const removeButton = event.target.closest('[data-row-remove]');
        if (removeButton) {
            event.preventDefault();
            removeRow(removeButton);
            return;
        }

        const rowCopy = event.target.closest('[data-row-copy]');
        if (rowCopy) {
            event.preventDefault();
            copyRow(rowCopy).catch(function () { flash(rowCopy, messages.copyFailed, true); });
            return;
        }

        const secret = event.target.closest('[data-secret-action]');
        if (secret && secretActions[secret.dataset.secretAction]) {
            event.preventDefault();
            // A connection's row button sits outside its collapsed fields; they are still in the page.
            const child = secret.closest('.nv-child');
            const scope = secret.closest('[data-secret-scope]')
                || (child && child.querySelector('[data-secret-scope]'));
            secretActions[secret.dataset.secretAction](secret, scope).catch(function (error) {
                flash(secret, error.message || messages.networkError, true);
            });
        }
    });

    document.addEventListener('submit', function (event) {
        const form = event.target;
        if (form.matches && form.matches('form[data-confirm-delete]') && !confirmDelete(form)) {
            event.preventDefault();
        }
    });

    document.addEventListener('keydown', function (event) {
        const key = event.key;

        if ((event.ctrlKey || event.metaKey) && (key === 's' || key === 'S')) {
            event.preventDefault();
            saveCurrent();
            return;
        }

        if (event.ctrlKey || event.metaKey || event.altKey || isTyping(event.target)
            || document.querySelector('.modal.show')) {
            return;
        }

        if (key === '/') {
            const search = root.querySelector('[data-shortcut-target="search"]');
            if (search) {
                event.preventDefault();
                search.focus();
                search.select();
            }
        } else if ((key === 'n' || key === 'N') && window.bootstrap) {
            const modal = document.getElementById('nv-create');
            if (modal) {
                event.preventDefault();
                bootstrap.Modal.getOrCreateInstance(modal).show();
            }
        }
    });

    // In a Mixed group the editor follows the picked format. CSS shows the right block; this
    // switches the other blocks off so a hidden editor cannot overwrite what is being saved.
    root.querySelectorAll('select[data-format-switch]').forEach(function (select) {
        const form = select.closest('form');
        const textarea = form && form.querySelector('textarea.nv-content');
        const apply = function () {
            if (textarea) {
                textarea.classList.toggle('is-code', CODE_FORMATS.indexOf(select.value) >= 0);
            }

            const wanted = SLOT_BY_FORMAT[select.value] || 'content';
            form.querySelectorAll('[data-slot]').forEach(function (slot) {
                const off = slot.dataset.slot !== wanted;
                slot.querySelectorAll('input, select, textarea').forEach(function (field) {
                    field.disabled = off;
                });
            });
        };
        select.addEventListener('change', apply);
        apply();
    });

    root.querySelectorAll('.nv-row').forEach(syncRow);

    root.addEventListener('input', function (event) {
        if (event.target.matches('[data-row-url]')) {
            syncRow(event.target.closest('.nv-row'));
        }
    });

    root.querySelectorAll('form[data-dirty-track]').forEach(trackForm);

    document.querySelectorAll('textarea[data-autosize]').forEach(function (textarea) {
        autosize(textarea);
        textarea.addEventListener('input', function () { autosize(textarea); });
    });

    renderRelativeTimes();
    window.setInterval(renderRelativeTimes, 60000);

    // After saving a sub-note the redirect carries #child-N: open it and bring it into view.
    if (/^#child-\d+$/.test(window.location.hash)) {
        const child = document.getElementById(window.location.hash.slice(1));
        if (child) {
            const details = child.querySelector('details');
            if (details) {
                details.open = true;
            }
            child.scrollIntoView({ block: 'center' });
        }
    }

    // A create that failed server-side validation comes back with the dialog reopened.
    const createModal = document.getElementById('nv-create');
    if (createModal && createModal.dataset.openOnLoad === 'true' && window.bootstrap) {
        bootstrap.Modal.getOrCreateInstance(createModal).show();
    }

    if (createModal) {
        createModal.addEventListener('shown.bs.modal', function () {
            const title = createModal.querySelector('input[name="Title"]');
            if (title) {
                title.focus();
            }
        });
    }
})();
