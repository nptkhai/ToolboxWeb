// Component hub: the catalog filter and the interactive samples on the Foundations tab.
(function () {
    'use strict';

    // ------------------------------------------------------------------ catalog filter
    document.querySelectorAll('[data-catalog]').forEach(function (root) {
        const search = root.querySelector('[data-catalog-search]');
        const chips = Array.from(root.querySelectorAll('[data-catalog-status]'));
        const rows = Array.from(root.querySelectorAll('[data-catalog-row]'));
        const table = root.querySelector('[data-catalog-table]');
        const empty = root.querySelector('[data-catalog-empty]');
        const count = root.querySelector('[data-catalog-count]');
        let status = 'all';

        function normalize(value) {
            // Accent- and punctuation-insensitive, like the Notes search.
            return (value || '').normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase()
                .replace(/\u0111/g, 'd').replace(/[^\p{L}\p{N}]+/gu, ' ').trim();
        }

        function apply() {
            const term = normalize(search?.value);
            let visible = 0;
            rows.forEach(function (row) {
                const matches = (status === 'all' || row.dataset.status === status)
                    && (!term || normalize(row.dataset.search).includes(term));
                row.hidden = !matches;
                if (matches) {
                    visible += 1;
                }
            });

            if (table) table.hidden = visible === 0;
            if (empty) empty.hidden = visible !== 0;
            if (count) {
                count.textContent = (count.dataset.template || '{0} / {1}')
                    .replace('{0}', visible)
                    .replace('{1}', rows.length);
            }
        }

        chips.forEach(function (chip) {
            chip.addEventListener('click', function () {
                status = chip.dataset.catalogStatus;
                chips.forEach(function (other) {
                    other.setAttribute('aria-pressed', String(other === chip));
                });
                apply();
            });
        });

        search?.addEventListener('input', apply);

        root.querySelector('[data-catalog-clear]')?.addEventListener('click', function () {
            if (search) search.value = '';
            chips[0]?.click();
            search?.focus();
        });
    });

    // ------------------------------------------------------------------ foundations samples
    document.querySelectorAll('[data-foundation-chips]').forEach(function (group) {
        const chips = Array.from(group.querySelectorAll('.tbx-chip'));
        chips.forEach(function (chip) {
            chip.addEventListener('click', function () {
                chips.forEach(function (other) {
                    other.setAttribute('aria-pressed', String(other === chip));
                });
            });
        });
    });

    document.querySelectorAll('[data-foundation-toast]').forEach(function (button) {
        button.addEventListener('click', function () {
            window.Toolbox?.notify(button.dataset.message, 'success');
        });
    });

    document.querySelectorAll('[data-foundation-confirm]').forEach(function (button) {
        button.addEventListener('click', function () {
            window.Toolbox?.confirm({
                title: button.dataset.title,
                message: button.dataset.message,
                okText: button.dataset.ok
            }).then(function (confirmed) {
                if (confirmed) {
                    window.Toolbox?.notify(button.dataset.done, 'info');
                }
            });
        });
    });
})();
