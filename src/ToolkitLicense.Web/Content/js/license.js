// Toolkit License admin app: theme, narrow-screen menu, copy buttons, confirmation modal and
// the license-term fields. No inline scripts in the pages; everything is wired from data-* attributes.
(function () {
    'use strict';

    function store(key, value) {
        try {
            if (value === null) { localStorage.removeItem(key); } else { localStorage.setItem(key, value); }
        } catch (e) { /* private window */ }
    }

    // Theme: the same localStorage key as the Toolkit app, so a browser keeps one choice.
    document.querySelectorAll('[data-theme-toggle]').forEach(function (button) {
        button.addEventListener('click', function () {
            const next = document.documentElement.dataset.theme === 'dark' ? 'light' : 'dark';
            document.documentElement.dataset.theme = next;
            store('toolbox.theme', next);
        });
    });

    // Narrow screens: the menu button slides the sidebar in; a click outside or Escape closes it.
    document.querySelectorAll('[data-nav-toggle]').forEach(function (button) {
        button.addEventListener('click', function (event) {
            event.stopPropagation();
            const open = document.body.classList.toggle('lic-nav-open');
            button.setAttribute('aria-expanded', open ? 'true' : 'false');
        });
    });
    document.addEventListener('click', function (event) {
        if (document.body.classList.contains('lic-nav-open') && !event.target.closest('.lic-sidebar')) {
            document.body.classList.remove('lic-nav-open');
        }
    });
    document.addEventListener('keydown', function (event) {
        if (event.key === 'Escape') { document.body.classList.remove('lic-nav-open'); }
    });

    // Copy: <button data-copy="#id"> copies the text of that element.
    document.querySelectorAll('[data-copy]').forEach(function (button) {
        button.addEventListener('click', function () {
            const source = document.querySelector(button.getAttribute('data-copy'));
            if (!source) { return; }
            const text = source.matches('ul, ol')
                ? Array.prototype.map.call(source.children, function (item) { return item.textContent.trim(); }).join('\n')
                : (source.value !== undefined ? source.value : source.textContent).trim();
            const done = function () {
                const label = button.querySelector('[data-copy-label]');
                if (!label) { return; }
                const before = label.textContent;
                label.textContent = button.getAttribute('data-copied') || before;
                setTimeout(function () { label.textContent = before; }, 1600);
            };
            if (navigator.clipboard && window.isSecureContext) {
                navigator.clipboard.writeText(text).then(done, function () { select(source); });
            } else {
                select(source);
                try { document.execCommand('copy'); done(); } catch (e) { /* the text stays selected */ }
            }
        });
    });

    function select(element) {
        const range = document.createRange();
        range.selectNodeContents(element);
        const selection = window.getSelection();
        selection.removeAllRanges();
        selection.addRange(range);
    }

    // Confirmation: a form with data-confirm="text" asks in the shared modal before posting.
    const modalElement = document.getElementById('confirm-modal');
    if (modalElement && window.bootstrap) {
        const modal = new window.bootstrap.Modal(modalElement);
        const text = modalElement.querySelector('[data-confirm-text]');
        const ok = modalElement.querySelector('[data-confirm-ok]');
        let pending = null;
        document.querySelectorAll('form[data-confirm]').forEach(function (form) {
            form.addEventListener('submit', function (event) {
                if (form.dataset.confirmed === 'true') { return; }
                event.preventDefault();
                pending = form;
                text.textContent = form.getAttribute('data-confirm');
                ok.textContent = form.getAttribute('data-confirm-button') || ok.getAttribute('data-default');
                ok.classList.toggle('btn-danger', form.hasAttribute('data-confirm-danger'));
                ok.classList.toggle('btn-primary', !form.hasAttribute('data-confirm-danger'));
                modal.show();
            });
        });
        ok.addEventListener('click', function () {
            if (!pending) { return; }
            pending.dataset.confirmed = 'true';
            modal.hide();
            if (pending.requestSubmit) { pending.requestSubmit(); } else { pending.submit(); }
        });
        modalElement.addEventListener('hidden.bs.modal', function () {
            if (pending && pending.dataset.confirmed !== 'true') { pending = null; }
        });
    }

    // License term: the date field only applies to "until a date".
    document.querySelectorAll('[data-term]').forEach(function (group) {
        const date = group.querySelector('[data-term-date]');
        const sync = function () {
            const chosen = group.querySelector('input[type=radio]:checked');
            const on = chosen && chosen.value === 'Date';
            date.disabled = !on;
            date.required = on;
        };
        group.addEventListener('change', sync);
        sync();
    });

    // "New customer" and the customer list exclude each other.
    document.querySelectorAll('[data-customer-pick]').forEach(function (form) {
        const select = form.querySelector('select[name=customerId]');
        const name = form.querySelector('input[name=newCustomerName]');
        if (!select || !name) { return; }
        name.addEventListener('input', function () { select.disabled = name.value.trim() !== ''; });
    });
})();
