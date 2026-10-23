// Behavior of the list page template (docs/LIST_PAGE_TEMPLATE.md), shared by list screens:
// dialogs marked data-list-modal move to <body>, "/" focuses the search box and "N" opens the
// primary modal (registered with Toolbox.keys, Content/js/shortcuts.js), "?new=1" in the address
// opens the primary modal once (the command palette's "New …" commands), a modal marked
// data-open-on-load reopens after a failed submit, and [data-date-picker] inputs become
// Kendo DatePickers that post an ISO date through a hidden field.
(function (window, document) {
    'use strict';

    const root = document.querySelector('[data-list-page]');
    if (!root) {
        return;
    }

    // Bootstrap puts the backdrop on <body>, but .app-shell has its own z-index, so a dialog left
    // inside it paints under the backdrop. Move the page's dialogs to <body>; the host keeps
    // .content-area so the form styles still apply (display: contents in components.css).
    const modals = document.querySelectorAll('.modal[data-list-modal]');
    if (modals.length) {
        const host = document.createElement('div');
        host.className = 'content-area tbx-modal-host';
        modals.forEach(function (modal) {
            host.appendChild(modal);
        });
        document.body.appendChild(host);
    }

    const keys = window.Toolbox?.keys;
    const search = root.querySelector('[data-list-search]');
    const newButton = root.querySelector('[data-list-new]');
    if (keys && search) {
        keys.add({ keys: '/', label: keys.label('search'), button: search, run: function () { search.focus(); search.select(); } });
    }
    if (keys && newButton) {
        keys.add({ keys: 'n', label: keys.label('new'), button: newButton, run: function () { newButton.click(); } });
    }

    function pad2(value) {
        return String(value).padStart(2, '0');
    }

    function isoDate(date) {
        return date instanceof Date && !Number.isNaN(date.getTime())
            ? date.getFullYear() + '-' + pad2(date.getMonth() + 1) + '-' + pad2(date.getDate())
            : '';
    }

    function parseIso(value) {
        const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(value || '');
        return match ? new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3])) : null;
    }

    // The visible input has no name; the hidden field always holds yyyy-MM-dd, so the server
    // reads the same value whatever the display format of the UI culture.
    document.querySelectorAll('[data-date-picker]').forEach(function (input) {
        const hidden = document.querySelector(input.getAttribute('data-date-picker'));
        if (!hidden) {
            return;
        }

        const $ = window.jQuery;
        if ($ && $.fn && $.fn.kendoDatePicker) {
            const locale = window.Toolbox?.format?.locale;
            if (locale && window.kendo.cultures?.[locale]) {
                window.kendo.culture(locale);
            }

            const picker = $(input).kendoDatePicker({
                format: window.Toolbox?.format?.datePattern || 'dd/MM/yyyy',
                value: parseIso(hidden.value),
                change: function () {
                    hidden.value = isoDate(this.value());
                }
            }).data('kendoDatePicker');
            // Typing a date and submitting without leaving the box still counts.
            input.closest('form')?.addEventListener('submit', function () {
                hidden.value = isoDate(picker.value());
            });
            return;
        }

        input.type = 'date';
        input.value = hidden.value;
        input.addEventListener('change', function () {
            hidden.value = input.value;
        });
    });

    const pending = document.querySelector('.modal[data-open-on-load="true"]');
    if (pending && window.bootstrap) {
        window.bootstrap.Modal.getOrCreateInstance(pending).show();
    } else if (newButton && new URLSearchParams(window.location.search).get('new') === '1') {
        // Opened from the command palette ("New …"): open the form once, then drop the flag so a
        // reload does not open it again.
        const url = new URL(window.location.href);
        url.searchParams.delete('new');
        window.history.replaceState(null, '', url.pathname + url.search + url.hash);
        window.setTimeout(function () { newButton.click(); }, 0);
    }
})(window, document);
