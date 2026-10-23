// Command palette (Ctrl/⌘+K or the search button in the header), for signed-in pages.
// It lists recent picks, the app's pages and quick commands, filtered as you type (ignoring
// case and Vietnamese accents), and from two letters on also searches your notes, to-dos,
// quick links and prompts (GET Search/Mine). ↑ ↓ move, Enter runs, Esc closes. Pages and
// commands come from #tbx-command-data (Views/Shared/_CommandPalette.cshtml).
(function (window, document) {
    'use strict';

    const modalElement = document.getElementById('tbx-palette');
    const config = readJson('tbx-command-data');
    if (!modalElement || !config || !window.Toolbox?.keys) {
        return;
    }

    const RECENT_KEY = 'toolbox.palette.recent.v1';
    const RECENT_MAX = 5;
    const text = config.text || {};
    const input = modalElement.querySelector('[data-palette-input]');
    const list = modalElement.querySelector('[data-palette-list]');
    const keys = window.Toolbox.keys;
    let options = [];
    let active = 0;
    let searchTimer = 0;
    let searchToken = 0;
    let dataState = { query: '', status: 'idle', items: [] };

    function readJson(id) {
        try {
            return JSON.parse(document.getElementById(id)?.textContent || 'null');
        } catch (error) {
            return null;
        }
    }

    function escapeHtml(value) {
        return String(value ?? '').replace(/[&<>"']/g, function (ch) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[ch];
        });
    }

    /** Lower case without accents, đ → d, so "ghi chu" finds "Ghi chú". */
    function fold(value) {
        return String(value || '').normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/đ/gi, 'd').toLowerCase();
    }

    function matches(query, fields) {
        const haystack = fold(fields.join(' '));
        return fold(query).split(/\s+/).filter(Boolean).every(function (term) { return haystack.indexOf(term) >= 0; });
    }

    /** Matches on the name first: name starts with it > name contains it > only a keyword matches. */
    function rank(query, option) {
        const label = fold(option.label);
        const q = fold(query);
        return label.indexOf(q) === 0 ? 0 : matches(query, [option.label]) ? 1 : 2;
    }

    function ranked(query, items) {
        return items
            .map(function (option, index) { return { option: option, score: rank(query, option), index: index }; })
            .sort(function (a, b) { return a.score - b.score || a.index - b.index; })
            .map(function (x) { return x.option; });
    }

    function icon(name) {
        return window.Toolbox.icon ? window.Toolbox.icon(name, 16) : '';
    }

    // ------------------------------------------------------------ recent picks

    function readRecent() {
        try {
            const value = JSON.parse(window.localStorage.getItem(RECENT_KEY) || '[]');
            return Array.isArray(value) ? value : [];
        } catch (error) {
            return [];
        }
    }

    function remember(option) {
        const entry = { type: option.type, id: option.id, label: option.label, detail: option.detail || '', icon: option.icon, href: option.href || null, external: Boolean(option.external), kind: option.kind || null };
        const next = [entry].concat(readRecent().filter(function (x) { return !(x.type === entry.type && x.id === entry.id); })).slice(0, RECENT_MAX);
        try {
            window.localStorage.setItem(RECENT_KEY, JSON.stringify(next));
        } catch (error) {
            // Storage off: the palette simply has no "recent" group.
        }
    }

    // ------------------------------------------------------------ options

    const KIND_ICONS = { note: 'sticky-note', todo: 'list-checks', link: 'link', prompt: 'message-square-text' };

    function pageOptions() {
        return (config.pages || []).map(function (page) {
            return { type: 'page', id: page.key, label: page.label, detail: page.group, icon: page.icon, href: page.href, go: page.go };
        });
    }

    function commandOptions() {
        const focus = window.ToolboxFocus;
        return (config.commands || []).map(function (command) {
            let label = command.label;
            if (command.action === 'focus' && focus) {
                const status = focus.state().status;
                label = status === 'running' ? text.focusPause : status === 'paused' ? text.focusResume : text.focusStart;
            }
            return { type: 'command', id: command.id, label: label, icon: command.icon, href: command.href || null, action: command.action || null, keywords: command.keywords || '' };
        });
    }

    function dataOptions() {
        return dataState.items.map(function (item) {
            return {
                type: 'data', id: item.kind + ':' + item.id, kind: item.kind, itemId: item.id, label: item.title,
                detail: (text.kind?.[item.kind] || item.kind) + (item.detail ? ' · ' + item.detail : ''),
                icon: KIND_ICONS[item.kind] || 'file-text', href: item.url, external: item.external
            };
        });
    }

    function build() {
        const query = input.value.trim();
        const groups = [];
        if (!query) {
            const recent = readRecent();
            if (recent.length) groups.push([text.recent, recent]);
            groups.push([text.pages, pageOptions()]);
            groups.push([text.commands, commandOptions()]);
        } else {
            const pages = ranked(query, pageOptions().filter(function (o) { return matches(query, [o.label, o.detail]); }));
            const commands = ranked(query, commandOptions().filter(function (o) { return matches(query, [o.label, o.keywords]); }));
            if (pages.length) groups.push([text.pages, pages]);
            if (commands.length) groups.push([text.commands, commands]);
            groups.push([text.data, dataOptions(), true]);
        }
        render(groups, query);
    }

    function render(groups, query) {
        options = [];
        let html = '';
        groups.forEach(function (group) {
            const [title, items, isData] = group;
            let body = '';
            items.forEach(function (option) {
                const index = options.push(option) - 1;
                body += '<div class="tbx-palette-option" role="option" id="tbx-palette-opt-' + index + '" data-index="' + index + '" aria-selected="false">'
                    + '<span class="tbx-palette-icon">' + icon(option.icon) + '</span>'
                    + '<span class="tbx-palette-text"><span class="tbx-palette-label">' + escapeHtml(option.label) + '</span>'
                    + (option.detail ? '<span class="tbx-palette-detail">' + escapeHtml(option.detail) + '</span>' : '') + '</span>'
                    + (option.go ? '<kbd class="tbx-kbd">' + escapeHtml(keys.display('g ' + option.go)) + '</kbd>' : '')
                    + (option.external ? icon('external-link') : '')
                    + '</div>';
            });
            if (isData) {
                body += dataStatusHtml(query, items.length);
            }
            if (body) {
                html += '<div class="tbx-palette-group" role="group" aria-label="' + escapeHtml(title) + '"><p class="tbx-palette-group-title" aria-hidden="true">' + escapeHtml(title) + '</p>' + body + '</div>';
            }
        });
        if (!options.length && !html) {
            html = '<p class="tbx-palette-state">' + escapeHtml(text.noResults) + '</p>';
        }
        list.innerHTML = html;
        setActive(Math.min(active, Math.max(0, options.length - 1)));
    }

    function dataStatusHtml(query, count) {
        if (query.length < (config.minQuery || 2)) {
            return '<p class="tbx-palette-state">' + escapeHtml(text.typeMore) + '</p>';
        }
        if (dataState.query !== query || dataState.status === 'loading') {
            return '<p class="tbx-palette-state" role="status"><span class="tbx-skeleton"></span>' + escapeHtml(text.searching) + '</p>';
        }
        if (dataState.status === 'error') {
            return '<p class="tbx-palette-state is-error" role="alert">' + icon('circle-alert') + escapeHtml(text.searchFailed) + '</p>';
        }
        return count ? '' : '<p class="tbx-palette-state">' + escapeHtml(text.noResults) + '</p>';
    }

    function setActive(index) {
        active = index;
        list.querySelectorAll('[role="option"]').forEach(function (element) {
            const on = Number(element.dataset.index) === index;
            element.setAttribute('aria-selected', on ? 'true' : 'false');
            element.classList.toggle('is-active', on);
            if (on) {
                element.scrollIntoView({ block: 'nearest' });
            }
        });
        if (options.length) {
            input.setAttribute('aria-activedescendant', 'tbx-palette-opt-' + index);
        } else {
            input.removeAttribute('aria-activedescendant');
        }
    }

    // ------------------------------------------------------------ data search

    function searchData() {
        const query = input.value.trim();
        window.clearTimeout(searchTimer);
        if (query.length < (config.minQuery || 2)) {
            dataState = { query: query, status: 'idle', items: [] };
            return;
        }
        if (dataState.query === query && dataState.status !== 'error') {
            return;
        }
        dataState = { query: query, status: 'loading', items: [] };
        const token = ++searchToken;
        searchTimer = window.setTimeout(async function () {
            try {
                const response = await fetch(config.searchUrl + '?q=' + encodeURIComponent(query), {
                    headers: { 'X-Requested-With': 'XMLHttpRequest' },
                    credentials: 'same-origin'
                });
                if (!response.ok || !(response.headers.get('content-type') || '').includes('application/json')) {
                    throw new Error(String(response.status));
                }
                const result = await response.json();
                if (token === searchToken) {
                    dataState = { query: query, status: 'done', items: result.items || [] };
                    build();
                }
            } catch (error) {
                if (token === searchToken) {
                    dataState = { query: query, status: 'error', items: [] };
                    build();
                }
            }
        }, 180);
    }

    // ------------------------------------------------------------ running an option

    function postLinkOpened(id) {
        const token = document.querySelector('[data-focus-token] input[name="__RequestVerificationToken"]');
        if (!config.linkOpenedUrl || !token) {
            return;
        }
        fetch(config.linkOpenedUrl, {
            method: 'POST',
            credentials: 'same-origin',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'RequestVerificationToken': token.value, 'X-Requested-With': 'XMLHttpRequest' },
            body: new URLSearchParams({ id: String(id) })
        }).catch(function () { /* counting is a nicety; the link still opens */ });
    }

    function runAction(action) {
        if (action === 'focus' && window.ToolboxFocus) {
            const focus = window.ToolboxFocus;
            const state = focus.state();
            if (state.status === 'idle' && state.phase !== 'focus') {
                focus.setPhase('focus');
            }
            focus.toggle();
        } else if (action.indexOf('theme:') === 0) {
            document.querySelector('[data-theme-set="' + action.slice(6) + '"]')?.click();
        } else if (action.indexOf('culture:') === 0) {
            document.querySelector('form [name="culture"][value="' + action.slice(8) + '"]')?.click();
        } else if (action === 'shortcuts') {
            keys.showHelp();
        } else if (action === 'sign-out') {
            document.querySelector('form[action*="Logout"] button[type="submit"]')?.click();
        }
    }

    function choose(option, newTab) {
        if (!option) {
            return;
        }
        remember(option);
        const modal = window.bootstrap.Modal.getOrCreateInstance(modalElement);
        modal.hide();

        if (option.action) {
            // After the palette has closed, so a dialog the action opens is not stacked on it.
            modalElement.addEventListener('hidden.bs.modal', function () { runAction(option.action); }, { once: true });
            return;
        }
        if (option.kind === 'link' || option.external) {
            if (option.kind === 'link' && option.itemId) {
                postLinkOpened(option.itemId);
            }
            window.open(option.href, '_blank', 'noopener');
            return;
        }
        if (option.href) {
            if (newTab) {
                window.open(option.href, '_blank', 'noopener');
            } else {
                window.location.href = option.href;
            }
        }
    }

    // ------------------------------------------------------------ wiring

    function open() {
        if (document.querySelector('.modal.show') && !modalElement.classList.contains('show')) {
            return;
        }
        input.value = '';
        active = 0;
        dataState = { query: '', status: 'idle', items: [] };
        build();
        window.bootstrap.Modal.getOrCreateInstance(modalElement).show();
    }

    // The palette lives outside the app shell so the backdrop does not cover it.
    document.body.appendChild(modalElement);
    modalElement.addEventListener('shown.bs.modal', function () { input.focus(); });

    input.addEventListener('input', function () {
        active = 0;
        searchData();
        build();
    });

    input.addEventListener('keydown', function (event) {
        if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
            event.preventDefault();
            if (options.length) {
                setActive((active + (event.key === 'ArrowDown' ? 1 : -1) + options.length) % options.length);
            }
        } else if (event.key === 'Enter') {
            event.preventDefault();
            choose(options[active], event.ctrlKey || event.metaKey);
        } else if (event.key === '?' && !input.value) {
            event.preventDefault();
            window.bootstrap.Modal.getOrCreateInstance(modalElement).hide();
            modalElement.addEventListener('hidden.bs.modal', keys.showHelp, { once: true });
        }
    });

    list.addEventListener('mousemove', function (event) {
        const element = event.target.closest('[role="option"]');
        if (element && Number(element.dataset.index) !== active) {
            setActive(Number(element.dataset.index));
        }
    });

    list.addEventListener('click', function (event) {
        const element = event.target.closest('[role="option"]');
        if (element) {
            choose(options[Number(element.dataset.index)], event.ctrlKey || event.metaKey);
        }
    });

    document.querySelectorAll('[data-palette-open]').forEach(function (button) {
        button.addEventListener('click', open);
    });

    keys.add({
        keys: 'mod+k',
        group: 'app',
        label: keys.label('palette'),
        inInputs: true,
        // Also while a dialog is open, so the browser's own Ctrl+K never fires on the page;
        // pressed again it closes the palette.
        inModal: true,
        button: document.querySelector('[data-palette-open]'),
        run: function () {
            if (modalElement.classList.contains('show')) {
                window.bootstrap.Modal.getOrCreateInstance(modalElement).hide();
            } else {
                open();
            }
        }
    });
})(window, document);
