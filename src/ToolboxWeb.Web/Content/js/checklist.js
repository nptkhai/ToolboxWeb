// To-do list (stage 5.6): edit in the shared modal, keyboard on rows, select several, folded
// descriptions, the sort order applying itself, and the "Undo" bar. Server side:
// ChecklistController; markup: Views/Checklist/Index.cshtml.
(function (window, document) {
    'use strict';

    const page = document.querySelector('[data-todo-page]');
    if (!page) {
        return;
    }

    // ------------------------------------------------------------------ edit / create modal
    // list-page.js moved the modal to <body>, so it is looked up on the document.
    const modal = document.querySelector('[data-todo-form]');
    const form = modal?.querySelector('[data-todo-form-element]');
    const field = function (name) { return form?.querySelector('[data-todo-field="' + name + '"]'); };

    function setDue(iso) {
        field('due').value = iso || '';
        const picker = window.jQuery?.('#todo-due').data('kendoDatePicker');
        const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(iso || '');
        const date = match ? new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3])) : null;
        if (picker) {
            picker.value(date);
        } else {
            const input = document.getElementById('todo-due');
            if (input) {
                input.value = iso || '';
            }
        }
    }

    // Fills the modal for a new task (data null) or for editing one row's task.
    function fill(data) {
        const editing = !!data;
        form.action = editing ? modal.dataset.editAction : modal.dataset.createAction;
        modal.querySelector('[data-todo-form-title]').textContent = editing ? modal.dataset.editTitle : modal.dataset.createTitle;
        form.querySelector('[data-todo-form-submit]').textContent = editing ? modal.dataset.editSubmit : modal.dataset.createSubmit;
        field('id').value = editing ? data.id : '';
        field('title').value = editing ? data.title : '';
        field('description').value = editing ? (data.description || '') : '';
        field('priority').value = editing ? String(data.priority) : '2';
        setDue(editing ? data.due : '');
        form.querySelector('#todo-title-error').textContent = '';
        field('title').removeAttribute('aria-invalid');
    }

    function openForm(data) {
        if (!modal || !form || !window.bootstrap) {
            return;
        }

        fill(data);
        window.bootstrap.Modal.getOrCreateInstance(modal).show();
    }

    // "New task" (toolbar, empty state) opens the modal through data-bs-toggle: start it empty.
    // A reopen after a failed submit has no trigger and keeps what was typed.
    modal?.addEventListener('show.bs.modal', function (event) {
        if (event.relatedTarget) {
            fill(null);
        }
    });
    modal?.addEventListener('shown.bs.modal', function () {
        field('title')?.focus();
    });

    function rowData(row) {
        try {
            return JSON.parse(row.dataset.edit || 'null');
        } catch {
            return null;
        }
    }

    page.addEventListener('click', function (event) {
        const edit = event.target.closest('[data-todo-edit]');
        if (edit) {
            openForm(rowData(edit.closest('[data-todo-row]')));
            return;
        }

        const desc = event.target.closest('[data-todo-desc]');
        if (desc) {
            const open = desc.getAttribute('aria-expanded') !== 'true';
            desc.setAttribute('aria-expanded', open ? 'true' : 'false');
        }
    });

    // ------------------------------------------------------------------ sort applies itself
    const sort = page.querySelector('[data-todo-sort]');
    sort?.querySelector('select')?.addEventListener('change', function () {
        // Empty filter and search stay out of the address.
        sort.querySelectorAll('input[type="hidden"]').forEach(function (input) {
            input.disabled = !input.value;
        });
        sort.submit();
    });

    // ------------------------------------------------------------------ select several
    const bulk = document.querySelector('[data-todo-bulk]');
    const modeButton = page.querySelector('[data-todo-select-mode]');
    const boxes = function () { return Array.from(page.querySelectorAll('[data-todo-select]')); };

    function setSelecting(on) {
        page.classList.toggle('is-selecting', on);
        if (modeButton) {
            modeButton.setAttribute('aria-pressed', on ? 'true' : 'false');
            modeButton.textContent = on ? modeButton.dataset.textOn : modeButton.dataset.textOff;
        }

        boxes().forEach(function (box) {
            box.tabIndex = on ? 0 : -1;
            if (!on) {
                box.checked = false;
            }
        });
        updateBulk();
    }

    function updateBulk() {
        const count = boxes().filter(function (box) { return box.checked; }).length;
        boxes().forEach(function (box) {
            box.closest('[data-todo-row]')?.classList.toggle('is-selected', box.checked);
        });
        if (!bulk) {
            return;
        }

        bulk.hidden = count === 0;
        const text = bulk.querySelector('[data-todo-bulk-count]');
        text.textContent = (text.dataset.template || '{0}').split('{0}').join(String(count));
        // The undo bar and the bulk bar share the bottom of the screen; the newer one wins.
        if (count > 0) {
            undo?.remove();
        }
    }

    modeButton?.addEventListener('click', function () {
        setSelecting(!page.classList.contains('is-selecting'));
    });
    page.addEventListener('change', function (event) {
        if (event.target.matches('[data-todo-select]')) {
            updateBulk();
        }
    });
    bulk?.querySelector('[data-todo-select-clear]')?.addEventListener('click', function () {
        setSelecting(false);
    });

    // ------------------------------------------------------------------ undo bar
    const undo = document.querySelector('[data-todo-undo]');
    if (undo) {
        undo.querySelector('[data-todo-undo-close]')?.addEventListener('click', function () {
            undo.remove();
        });
        // It stays long enough to read and act on, then steps aside.
        window.setTimeout(function () {
            undo.classList.add('is-leaving');
            window.setTimeout(function () { undo.remove(); }, 400);
        }, 12000);
    }

    // ------------------------------------------------------------------ keyboard on rows
    const rows = function () { return Array.from(page.querySelectorAll('[data-todo-row]')); };

    // Row keys (Toolbox.keys, Content/js/shortcuts.js) act on the row that has the focus.
    const keys = window.Toolbox?.keys;
    if (keys) {
        const focusedRow = function (event) {
            const row = event.target.closest?.('[data-todo-row]');
            return row && event.target === row ? row : null;
        };
        const onRow = function (handler) {
            return { when: focusedRow, run: function (event) { handler(focusedRow(event), event); } };
        };

        keys.add(Object.assign({ keys: ['arrowdown', 'arrowup', 'j', 'k'], label: keys.label('move'), display: ['↑ ↓'] }, onRow(function (row, event) {
            const list = rows();
            const down = event.key === 'ArrowDown' || event.key === 'j';
            list[list.indexOf(row) + (down ? 1 : -1)]?.focus();
        })));
        keys.add(Object.assign({ keys: 'x', label: keys.label('done') }, onRow(function (row) {
            row.querySelector('[data-todo-toggle]')?.click();
        })));
        keys.add(Object.assign({ keys: ['e', 'enter'], label: keys.label('edit') }, onRow(function (row) {
            openForm(rowData(row));
        })));
        keys.add(Object.assign({ keys: ['delete', 'backspace'], label: keys.label('delete'), display: ['Delete'] }, onRow(function (row) {
            row.querySelector('[data-todo-delete]')?.click();
        })));
        keys.add(Object.assign({ keys: 'space', label: keys.label('selectMany') }, onRow(function (row) {
            if (!page.classList.contains('is-selecting')) {
                setSelecting(true);
            }
            const box = row.querySelector('[data-todo-select]');
            box.checked = !box.checked;
            updateBulk();
        })));
        keys.add({
            keys: 'escape',
            label: keys.label('exitSelect'),
            hidden: true,
            when: function () { return page.classList.contains('is-selecting'); },
            run: function () { setSelecting(false); }
        });
    }

    // Opened from the command palette on one to-do (?focus=<id>): put the focus on its row.
    const focusId = new URLSearchParams(window.location.search).get('focus');
    if (focusId) {
        const target = rows().find(function (row) { return String(rowData(row)?.id) === focusId; });
        target?.focus();
        target?.scrollIntoView({ block: 'center' });
        const url = new URL(window.location.href);
        url.searchParams.delete('focus');
        window.history.replaceState(null, '', url.pathname + url.search + url.hash);
    }
})(window, document);
