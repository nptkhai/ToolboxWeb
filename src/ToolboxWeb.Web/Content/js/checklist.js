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

    function isTyping(target) {
        return target && (target.isContentEditable || /^(input|textarea|select)$/i.test(target.tagName));
    }

    page.addEventListener('keydown', function (event) {
        const row = event.target.closest?.('[data-todo-row]');
        if (!row || event.target !== row || event.ctrlKey || event.metaKey || event.altKey || isTyping(event.target)) {
            return;
        }

        const list = rows();
        const index = list.indexOf(row);
        const key = event.key;
        if (key === 'ArrowDown' || key === 'ArrowUp' || key === 'j' || key === 'k') {
            event.preventDefault();
            const next = list[index + (key === 'ArrowDown' || key === 'j' ? 1 : -1)];
            next?.focus();
        } else if (key === 'x' || key === 'X') {
            event.preventDefault();
            row.querySelector('[data-todo-toggle]')?.click();
        } else if (key === 'e' || key === 'E' || key === 'Enter') {
            event.preventDefault();
            openForm(rowData(row));
        } else if (key === 'Delete' || key === 'Backspace') {
            event.preventDefault();
            row.querySelector('[data-todo-delete]')?.click();
        } else if (key === ' ') {
            event.preventDefault();
            if (!page.classList.contains('is-selecting')) {
                setSelecting(true);
            }

            const box = row.querySelector('[data-todo-select]');
            box.checked = !box.checked;
            updateBulk();
        } else if (key === 'Escape' && page.classList.contains('is-selecting')) {
            setSelecting(false);
        }
    });
})(window, document);
