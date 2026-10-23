// Quick links (Views/QuickLinks/Index.cshtml), on top of Content/js/list-page.js: opening a
// link counts it, copy the address, drag (or Alt+Up/Down) to reorder inside a block, the
// add/edit dialog with address completion and a duplicate check, and the keyboard.
(function (window, document) {
    'use strict';

    const page = document.querySelector('[data-link-page]');
    if (!page) {
        return;
    }

    const links = readJson('link-data') || [];
    const text = readJson('link-text') || {};
    const byId = new Map(links.map(function (link) { return [String(link.id), link]; }));
    const canReorder = page.dataset.canReorder === 'true';

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

    function notify(message, type) {
        if (window.Toolbox?.notify) {
            window.Toolbox.notify(message, type);
        }
    }

    function isTyping(target) {
        return target instanceof HTMLElement
            && (target.isContentEditable || /^(INPUT|TEXTAREA|SELECT)$/.test(target.tagName));
    }

    function token() {
        return page.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    }

    function post(url, params) {
        return fetch(url, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
                'RequestVerificationToken': token(),
                'X-Requested-With': 'XMLHttpRequest'
            },
            body: params
        });
    }

    function linkOf(element) {
        const row = element.closest('[data-link-row]');
        return row ? byId.get(row.dataset.id) : null;
    }

    // ------------------------------------------------------------ open / copy

    // The link opens in a new tab by itself; this only counts it and refreshes the row.
    async function markOpened(link) {
        try {
            const response = await post(page.dataset.openedUrl, new URLSearchParams({ id: String(link.id) }));
            if (!response.ok) {
                return;
            }

            const data = await response.json();
            const meta = page.querySelector('[data-link-row][data-id="' + link.id + '"] [data-link-opened]');
            if (meta) {
                meta.textContent = data.openedText;
            }
        } catch (error) {
            // Opening worked; a missed count is not worth an error.
        }
    }

    function openLink(link) {
        window.open(link.url, '_blank', 'noopener');
        markOpened(link);
    }

    async function copyUrl(link) {
        try {
            await navigator.clipboard.writeText(link.url);
            notify(format(text.copied, link.title), 'success');
        } catch (error) {
            notify(text.copyFailed, 'error');
        }
    }

    page.addEventListener('click', function (event) {
        const link = linkOf(event.target);
        if (!link) {
            return;
        }

        if (event.target.closest('[data-link-open]')) {
            markOpened(link);
        } else if (event.target.closest('[data-link-copy]')) {
            copyUrl(link);
        } else if (event.target.closest('[data-link-edit]')) {
            openEdit(link);
        } else if (!event.target.closest('form, button, a, .link-grip')) {
            openLink(link);
        }
    });

    // A middle click opens the link in a background tab, which counts too.
    page.addEventListener('auxclick', function (event) {
        const link = event.button === 1 && event.target.closest('[data-link-open]') ? linkOf(event.target) : null;
        if (link) {
            markOpened(link);
        }
    });

    // ------------------------------------------------------------ reorder

    async function saveOrder(list) {
        const ids = Array.from(list.querySelectorAll('[data-link-row]')).map(function (row) { return row.dataset.id; });
        const body = new URLSearchParams();
        ids.forEach(function (id) { body.append('ids', id); });
        try {
            const response = await post(page.dataset.reorderUrl, body);
            if (!response.ok) {
                throw new Error(String(response.status));
            }
        } catch (error) {
            notify(text.reorderFailed, 'error');
        }
    }

    if (canReorder) {
        let dragged = null;

        page.addEventListener('dragstart', function (event) {
            const row = event.target.closest?.('[data-link-row]');
            if (!row) {
                return;
            }
            dragged = row;
            row.classList.add('is-dragging');
            event.dataTransfer.effectAllowed = 'move';
            event.dataTransfer.setData('text/plain', row.dataset.id);
        });

        // Rows move only inside their own block (pinned, or one group).
        page.addEventListener('dragover', function (event) {
            const row = event.target.closest?.('[data-link-row]');
            if (!dragged || !row || row === dragged || row.parentElement !== dragged.parentElement) {
                return;
            }
            event.preventDefault();
            const box = row.getBoundingClientRect();
            const after = event.clientY > box.top + box.height / 2;
            row.parentElement.insertBefore(dragged, after ? row.nextSibling : row);
        });

        page.addEventListener('dragend', function () {
            if (!dragged) {
                return;
            }
            const list = dragged.parentElement;
            dragged.classList.remove('is-dragging');
            dragged = null;
            saveOrder(list);
        });
    }

    // Alt+Up/Down: the same move from the keyboard.
    function moveRow(row, direction) {
        const sibling = direction < 0 ? row.previousElementSibling : row.nextElementSibling;
        if (!sibling) {
            return;
        }
        row.parentElement.insertBefore(row, direction < 0 ? sibling : sibling.nextSibling);
        row.focus();
        saveOrder(row.parentElement);
        notify(format(text.moved, byId.get(row.dataset.id)?.title || ''), 'info');
    }

    // ------------------------------------------------------------ add / edit form

    const formModal = document.querySelector('[data-link-form]');
    const form = formModal.querySelector('[data-link-form-element]');
    const field = function (name) { return form.querySelector('[data-field="' + name + '"]'); };
    const urlHint = form.querySelector('[data-url-hint]');
    const urlHintDefault = urlHint.textContent;
    let checkTimer = null;
    let checkSeq = 0;
    let groupBox = null;

    function setError(input, message) {
        const error = document.getElementById(input.id + '-error');
        if (message) {
            input.setAttribute('aria-invalid', 'true');
        } else {
            input.removeAttribute('aria-invalid');
        }
        if (error) {
            error.textContent = message || '';
        }
    }

    function setUrlHint(message, warn) {
        urlHint.textContent = message || urlHintDefault;
        urlHint.classList.toggle('is-warning', Boolean(warn));
    }

    // Asks the server whether the address is valid (after completing "https://") and whether
    // another link already has it. A duplicate is a warning; saving is still allowed.
    async function checkUrl(complete) {
        const input = field('url');
        const value = input.value.trim();
        const seq = ++checkSeq;
        if (!value) {
            setUrlHint('');
            return;
        }

        try {
            const response = await fetch(page.dataset.checkUrl + '?url=' + encodeURIComponent(value)
                + (field('id').value ? '&id=' + encodeURIComponent(field('id').value) : ''), {
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            });
            const data = await response.json();
            if (seq !== checkSeq) {
                return;
            }

            if (!data.valid) {
                setError(input, formModal.dataset.urlText);
                setUrlHint('');
                return;
            }

            setError(input, '');
            const completed = complete && data.url !== value;
            if (completed) {
                input.value = data.url;
            }
            setUrlHint(data.duplicate || (completed ? text.urlCompleted : ''), Boolean(data.duplicate));

            // An empty title takes the address's host, which the user can still change.
            if (complete && !field('title').value.trim()) {
                try {
                    field('title').value = new URL(data.url).hostname.replace(/^www\./, '');
                } catch (error) {
                    // A file address has no host; leave the title for the user.
                }
            }
        } catch (error) {
            // The server check is a convenience; the save checks the address again.
        }
    }

    field('url').addEventListener('input', function () {
        setError(field('url'), '');
        window.clearTimeout(checkTimer);
        checkTimer = window.setTimeout(function () { checkUrl(false); }, 400);
    });
    field('url').addEventListener('change', function () {
        window.clearTimeout(checkTimer);
        checkUrl(true);
    });
    field('title').addEventListener('input', function () { setError(field('title'), ''); });

    function setFormMode(editing) {
        form.action = editing ? formModal.dataset.editAction : formModal.dataset.createAction;
        formModal.querySelector('[data-link-form-title]').textContent = editing ? formModal.dataset.editTitle : formModal.dataset.createTitle;
        formModal.querySelector('[data-link-form-submit]').textContent = editing ? formModal.dataset.editSubmit : formModal.dataset.createSubmit;
    }

    function fillForm(link) {
        field('id').value = link ? link.id : '';
        field('url').value = link ? link.url : '';
        field('title').value = link ? link.title : '';
        const group = link ? (link.groupName || '') : currentGroupName();
        if (groupBox) {
            groupBox.value(group);
        } else {
            field('groupName').value = group;
        }
        ['url', 'title', 'groupName'].forEach(function (name) { setError(field(name), ''); });
        setUrlHint('');
        setFormMode(Boolean(link));
    }

    // A new link starts in the group being looked at.
    function currentGroupName() {
        const active = page.querySelector('.tbx-filter-chips .tbx-chip.is-selected');
        const params = new URLSearchParams(window.location.search);
        return params.get('group') && params.get('group') !== '_none' && active ? active.firstElementChild.textContent.trim() : '';
    }

    function openEdit(link) {
        fillForm(link);
        window.bootstrap.Modal.getOrCreateInstance(formModal).show();
    }

    formModal.addEventListener('show.bs.modal', function (event) {
        if (event.relatedTarget) {
            fillForm(null);
        }
    });
    formModal.addEventListener('shown.bs.modal', function () {
        field('url').focus();
    });

    form.addEventListener('submit', function (event) {
        let firstInvalid = null;
        ['url', 'title'].forEach(function (name) {
            const input = field(name);
            const empty = !input.value.trim();
            if (empty) {
                setError(input, formModal.dataset.requiredText);
                firstInvalid = firstInvalid || input;
            }
        });
        if (firstInvalid) {
            event.preventDefault();
            firstInvalid.focus();
        }
    });

    // The group box suggests the groups in use (Kendo AutoComplete); typing a new name makes a new group.
    const $ = window.jQuery;
    if ($ && $.fn && $.fn.kendoAutoComplete) {
        let groups = [];
        try {
            groups = JSON.parse(field('groupName').dataset.groups || '[]');
        } catch (error) {
            groups = [];
        }
        groupBox = $(field('groupName')).kendoAutoComplete({
            dataSource: groups,
            filter: 'contains',
            minLength: 0,
            clearButton: false
        }).data('kendoAutoComplete');
        field('groupName').addEventListener('focus', function () {
            if (!field('groupName').value) {
                groupBox.search('');
            }
        });
    }

    // ------------------------------------------------------------ sort and keyboard

    const sort = page.querySelector('[data-link-sort]');
    sort?.querySelector('select').addEventListener('change', function () {
        sort.querySelectorAll('input[type="hidden"]').forEach(function (input) {
            input.disabled = !input.value;
        });
        sort.submit();
    });

    // Up/Down move between links, Enter opens, C copies the address, E edits, Alt+Up/Down
    // reorders. "/" and "N" come from list-page.js.
    document.addEventListener('keydown', function (event) {
        if (event.ctrlKey || event.metaKey || isTyping(event.target) || document.querySelector('.modal.show')) {
            return;
        }

        const rows = Array.from(page.querySelectorAll('[data-link-row]'));
        if (!rows.length) {
            return;
        }

        const active = document.activeElement?.closest?.('[data-link-row]');
        const key = event.key;

        if (event.altKey) {
            if (active && canReorder && (key === 'ArrowUp' || key === 'ArrowDown')) {
                event.preventDefault();
                moveRow(active, key === 'ArrowUp' ? -1 : 1);
            }
            return;
        }

        if (key === 'ArrowDown' || key === 'ArrowUp') {
            event.preventDefault();
            const index = active ? rows.indexOf(active) : -1;
            const next = rows[Math.min(rows.length - 1, Math.max(0, index + (key === 'ArrowDown' ? 1 : -1)))] || rows[0];
            next.focus();
            return;
        }

        const link = active ? byId.get(active.dataset.id) : null;
        if (!link) {
            return;
        }

        if (key === 'Enter' && document.activeElement === active) {
            event.preventDefault();
            openLink(link);
        } else if (key === 'c' || key === 'C') {
            event.preventDefault();
            copyUrl(link);
        } else if (key === 'e' || key === 'E') {
            event.preventDefault();
            openEdit(link);
        }
    });
})(window, document);
