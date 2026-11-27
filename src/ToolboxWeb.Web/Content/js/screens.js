// Kept screens: the shell (Views/Shared/ScreenShell.cshtml, Infrastructure/Web/ScreenFrames.cs).
// Every screen opened from the sidebar, the palette, "G then letter" or a link runs in its own frame
// and stays as it was while another one shows; the strip above lists them (× or a middle click
// closes one; right click for "close others", "close all", "reload"). The address bar and Back
// follow the screen on show; the list of screens is remembered in this browser, and a remembered
// screen loads only when it is shown again. A screen hidden behind another is paused
// (screen-frame.js). With a maximum set (account page), opening one more asks which to close.
(function (window, document) {
    'use strict';

    const Toolbox = window.Toolbox = window.Toolbox || {};
    const config = readJson('tbx-screens-config') || {};
    const pages = (readJson('tbx-command-data') || {}).pages || [];
    const text = config.text || {};
    const els = {
        tabs: document.querySelector('[data-screen-tabs]'),
        host: document.querySelector('[data-screen-host]'),
        empty: document.querySelector('[data-screen-empty]'),
        title: document.querySelector('.topbar-title'),
        crumbs: document.querySelector('.topbar-breadcrumb'),
        limit: document.getElementById('tbx-screen-limit')
    };
    if (!els.tabs || !els.host) {
        return;
    }
    // A phone-narrow window gets plain pages. The first visit had no "narrow" cookie yet; the
    // layout has just set it, so loading the same address again gives the plain page (only when
    // the cookie was stored, so a browser that blocks cookies does not reload over and over).
    if (window.matchMedia('(max-width: 899.98px)').matches && /(?:^|;\s*)toolbox\.narrow=1(?:;|$)/.test(document.cookie)) {
        window.location.replace(window.location.href);
        return;
    }
    // Like the palette, the dialog lives outside the app shell so its backdrop does not cover it.
    if (els.limit) document.body.appendChild(els.limit);

    // Pages that must be the whole window: the shell never holds them in a frame.
    const TOP_ONLY = ['/activate.html', '/update.html', '/offline.html', '/code-playground/run.html', '/identity/account/login', '/identity/account/logout'];
    const screens = [];
    let active = null;
    let sequence = 0;

    function readJson(id) {
        try {
            return JSON.parse(document.getElementById(id)?.textContent || 'null');
        } catch (error) {
            return null;
        }
    }

    function fmt(template, ...values) {
        return values.reduce((result, value, index) => result.split(`{${index}}`).join(String(value)), String(template || ''));
    }

    function escapeHtml(value) {
        return String(value ?? '').replace(/[&<>"']/g, (ch) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[ch]));
    }

    function icon(name, size) {
        return Toolbox.icon ? Toolbox.icon(name, size || 14) : '';
    }

    function toUrl(href) {
        try {
            return new URL(href, window.location.href);
        } catch (error) {
            return null;
        }
    }

    /** "/files.html?open=x" — what a screen is opened with and what the address bar shows. */
    function relative(href) {
        const url = toUrl(href);
        return url ? url.pathname + url.search + url.hash : href;
    }

    function pathOf(href) {
        const url = toUrl(href);
        return url ? url.pathname.toLowerCase() : '';
    }

    /**
     * What following a link should do: "screen" (open or switch to a screen), "top" (a page that
     * must be the whole window), or "none" (leave it: another site, a download, an anchor on the
     * same page, a file or an API address).
     */
    function classify(href, from) {
        const url = toUrl(href);
        if (!url || url.origin !== window.location.origin) return 'none';
        const path = url.pathname.toLowerCase();
        if (TOP_ONLY.some((p) => path === p || path.startsWith(p + '/'))) return 'top';
        const isPage = path.endsWith('.html') || path.startsWith('/identity/account/manage');
        if (!isPage) return 'none';
        if (from) {
            const current = toUrl(from);
            if (current && current.pathname.toLowerCase() === path && current.search === url.search && url.hash) return 'none';
        }
        return 'screen';
    }

    function pageFor(href) {
        const path = pathOf(href);
        return pages.find((page) => pathOf(page.href) === path) || null;
    }

    // ------------------------------------------------------------------ remembered screens

    function save() {
        const value = {
            screens: screens.map((s) => ({ url: s.url, title: s.title })),
            active: active ? active.url : null
        };
        try {
            window.localStorage.setItem(config.stateKey, JSON.stringify(value));
        } catch (error) {
            // Private window or full storage: the screens are still kept for this visit.
        }
    }

    function load() {
        try {
            return JSON.parse(window.localStorage.getItem(config.stateKey) || 'null');
        } catch (error) {
            return null;
        }
    }

    // ------------------------------------------------------------------ the strip

    function makeScreen(url, title) {
        const page = pageFor(url);
        const screen = {
            id: `s${++sequence}`,
            url: relative(url),
            title: title || page?.label || relative(url),
            icon: page?.icon || 'file-text',
            group: page?.group || '',
            pageKey: page?.key || null,
            frame: null,
            el: null,
            loading: false
        };
        const el = document.createElement('div');
        el.className = 'tbx-screen-tab';
        el.dataset.screen = screen.id;
        el.innerHTML = `<button type="button" class="tbx-screen-tab-open" role="tab" aria-selected="false" id="tbx-screen-tab-${screen.id}">`
            + `<span class="tbx-screen-tab-icon" aria-hidden="true"></span><span class="tbx-screen-tab-title"></span></button>`
            + `<button type="button" class="tbx-screen-tab-close" aria-label="${escapeHtml(text.Close)}" title="${escapeHtml(text.Close)}">${icon('x', 14)}</button>`;
        screen.el = el;
        renderTab(screen);
        return screen;
    }

    function renderTab(screen) {
        screen.el.querySelector('.tbx-screen-tab-icon').innerHTML = icon(screen.icon, 14);
        screen.el.querySelector('.tbx-screen-tab-title').textContent = screen.title;
        screen.el.title = screen.title;
        screen.el.classList.toggle('is-loading', screen.loading);
        const open = screen.el.querySelector('.tbx-screen-tab-open');
        open.setAttribute('aria-selected', String(screen === active));
        open.tabIndex = screen === active ? 0 : -1;
        if (screen.frame) open.setAttribute('aria-controls', `tbx-screen-frame-${screen.id}`);
        screen.el.classList.toggle('is-active', screen === active);
    }

    function insertTab(screen, after) {
        if (after && after.el.parentNode === els.tabs) after.el.after(screen.el);
        else els.tabs.appendChild(screen.el);
    }

    function screenOf(element) {
        const holder = element && element.closest('[data-screen]');
        return holder ? screens.find((s) => s.id === holder.dataset.screen) : null;
    }

    // ------------------------------------------------------------------ frames

    function frameWindow(screen) {
        try {
            return screen.frame ? screen.frame.contentWindow : null;
        } catch (error) {
            return null;
        }
    }

    function frameToolbox(screen) {
        try {
            return frameWindow(screen)?.Toolbox || null;
        } catch (error) {
            return null;
        }
    }

    function ensureFrame(screen) {
        if (screen.frame) return;
        const frame = document.createElement('iframe');
        frame.className = 'tbx-screen-frame';
        frame.id = `tbx-screen-frame-${screen.id}`;
        frame.title = screen.title;
        frame.setAttribute('role', 'tabpanel');
        frame.setAttribute('aria-labelledby', `tbx-screen-tab-${screen.id}`);
        frame.hidden = screen !== active;
        frame.addEventListener('load', () => onFrameLoad(screen));
        screen.frame = frame;
        screen.loading = true;
        renderTab(screen);
        frame.src = screen.url;
        els.host.appendChild(frame);
    }

    function onFrameLoad(screen) {
        if (!screen.frame) return;
        screen.loading = false;
        let doc = null;
        let url = null;
        try {
            doc = screen.frame.contentDocument;
            const location = screen.frame.contentWindow.location;
            url = location.pathname + location.search + location.hash;
        } catch (error) {
            doc = null;
        }
        if (!doc || !url || url === 'blank') {
            renderTab(screen);
            return;
        }
        const root = doc.documentElement;
        // One of the app's pages that is not a screen here (the session ended and sign-in shows,
        // the license needs activating, an update is required): it takes the whole window.
        if (root && root.dataset.backgroundMode && root.dataset.embedded !== 'true') {
            window.location.href = url;
            return;
        }
        const page = pageFor(url);
        const title = doc.querySelector('main[data-screen-title]')?.dataset.screenTitle
            || (doc.title || '').replace(new RegExp(`\\s*-\\s*${escapeRegExp(config.appName || '')}$`), '')
            || screen.title;
        screen.url = url;
        screen.title = title;
        screen.icon = page?.icon || screen.icon;
        screen.group = page ? page.group : screen.group;
        screen.pageKey = page?.key || screen.pageKey;
        screen.frame.title = title;
        copyLook(root);
        frameToolbox(screen)?.screenFrame?.setVisible(screen === active);
        renderTab(screen);
        if (screen === active) {
            updateChrome();
            replaceHistory();
        }
        save();
    }

    function escapeRegExp(value) {
        return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    }

    /** The theme and the background setting follow the shell into every screen. */
    function copyLook(target) {
        if (!target) return;
        const source = document.documentElement;
        ['theme', 'background'].forEach((key) => {
            if (source.dataset[key]) target.dataset[key] = source.dataset[key];
            else delete target.dataset[key];
        });
    }

    new MutationObserver(() => {
        screens.forEach((screen) => {
            try { copyLook(screen.frame?.contentDocument?.documentElement); } catch (error) { /* loading */ }
        });
    }).observe(document.documentElement, { attributes: true, attributeFilter: ['data-theme', 'data-background'] });

    // A focus session saved by the shell's timer: the timer screen shows it.
    window.addEventListener('toolbox:focus-saved', (event) => {
        screens.forEach((screen) => {
            const win = frameWindow(screen);
            try { win?.dispatchEvent(new win.CustomEvent('toolbox:focus-saved', { detail: event.detail })); } catch (error) { /* loading */ }
        });
    });

    // ------------------------------------------------------------------ showing a screen

    function updateChrome() {
        const screen = active;
        els.empty.hidden = Boolean(screen);
        // The focus timer puts the time in front while it runs (focus-clock.js); keep it there.
        const time = (document.title.match(/^\d{2,}:\d{2} · /) || [''])[0];
        document.title = time + (screen ? `${screen.title} - ${config.appName}` : config.appName);
        if (els.title) els.title.textContent = screen ? screen.title : config.appName;
        if (els.crumbs) {
            els.crumbs.querySelectorAll('.topbar-crumb, .topbar-crumb-sep').forEach((el) => el.remove());
            if (screen && screen.group) {
                els.crumbs.insertAdjacentHTML('afterbegin', `<span class="topbar-crumb">${escapeHtml(screen.group)}</span>${Toolbox.icon ? Toolbox.icon('chevron-right', 16).replace('class="tbx-icon"', 'class="tbx-icon topbar-crumb-sep"') : ''}`);
            }
        }
        document.querySelectorAll('.side-nav-link').forEach((link) => {
            const on = Boolean(screen) && pathOf(link.href) === pathOf(screen.url);
            link.classList.toggle('active', on);
            if (on) link.setAttribute('aria-current', 'page');
            else link.removeAttribute('aria-current');
        });
        // Forms of the shell that come back to the page (the language switch) come back to the
        // screen on show, not to the address the shell was first opened with.
        if (screen) {
            document.querySelectorAll('form input[type="hidden"][name="returnUrl"]').forEach((input) => { input.value = screen.url; });
        }
    }

    function historyState() {
        return { screen: active ? active.id : null };
    }

    function replaceHistory() {
        if (active) window.history.replaceState(historyState(), '', active.url);
    }

    function show(screen, options) {
        const opts = options || {};
        const previous = active;
        if (previous && previous !== screen) {
            if (previous.frame) previous.frame.hidden = true;
            frameToolbox(previous)?.screenFrame?.setVisible(false);
        }
        active = screen;
        screens.forEach(renderTab);
        if (screen) {
            ensureFrame(screen);
            screen.frame.hidden = false;
            frameToolbox(screen)?.screenFrame?.setVisible(true);
            if (opts.history === 'push' && previous !== screen) window.history.pushState(historyState(), '', screen.url);
            else if (opts.history !== 'none') replaceHistory();
            if (opts.focus) {
                // Keys go to the screen (its shortcuts), not to the strip.
                window.setTimeout(() => { try { screen.frame.contentWindow.focus(); } catch (error) { /* loading */ } }, 0);
            }
        }
        updateChrome();
        save();
    }

    /**
     * Opens a page as a screen, or shows the screen that already has it. A different query
     * (?new=1, ?open=…) sends that screen to the new address.
     */
    async function open(href, options) {
        const opts = options || {};
        const kind = classify(href);
        if (kind !== 'screen') {
            window.location.href = href;
            return;
        }
        const url = relative(href);
        const existing = screens.find((s) => pathOf(s.url) === pathOf(url));
        if (existing) {
            const target = toUrl(url);
            const current = toUrl(existing.url);
            if (target && current && target.search && target.search !== current.search) {
                existing.url = url;
                if (existing.frame) {
                    try { existing.frame.contentWindow.location.href = url; } catch (error) { existing.frame.src = url; }
                }
            }
            show(existing, { history: opts.history || 'push', focus: true });
            return;
        }
        const max = Toolbox.maxScreens ? Toolbox.maxScreens() : 0;
        if (max && screens.length >= max) {
            const page = pageFor(url);
            const freed = await askToClose(page?.label || url, screens.length - max + 1);
            if (!freed) return;
        }
        const screen = makeScreen(url);
        insertTab(screen, active);
        screens.splice(active ? screens.indexOf(active) + 1 : screens.length, 0, screen);
        show(screen, { history: opts.history || 'push', focus: true });
    }

    async function canClose(screen) {
        let pending = 0;
        try {
            pending = frameToolbox(screen)?.screenFrame?.pendingChanges() || 0;
        } catch (error) {
            pending = 0;
        }
        if (!pending || !Toolbox.confirm) return true;
        return Toolbox.confirm({
            title: text.UnsavedTitle,
            message: fmt(text.UnsavedMessage, screen.title),
            okText: text.UnsavedOk,
            tone: 'danger'
        });
    }

    async function close(screen, options) {
        if (!screen) return false;
        if (!(options && options.force) && !(await canClose(screen))) return false;
        try { frameToolbox(screen)?.screenFrame?.dispose(); } catch (error) { /* gone */ }
        const at = screens.indexOf(screen);
        screens.splice(at, 1);
        screen.frame?.remove();
        screen.frame = null;
        screen.el.remove();
        if (active === screen) {
            active = null;
            const next = screens[at] || screens[at - 1] || null;
            if (next) show(next, { history: 'replace', focus: true });
            else {
                updateChrome();
                save();
            }
        } else {
            save();
        }
        return true;
    }

    async function closeMany(list) {
        for (const screen of list) {
            if (!(await close(screen))) return false;
        }
        return true;
    }

    function reload(screen) {
        if (!screen) return;
        if (screen.frame) {
            screen.loading = true;
            renderTab(screen);
            try { screen.frame.contentWindow.location.reload(); } catch (error) { screen.frame.src = screen.url; }
        } else {
            show(screen, { focus: true });
        }
    }

    // ------------------------------------------------------------------ over the maximum

    function askToClose(newTitle, need) {
        if (!els.limit || !window.bootstrap) return Promise.resolve(false);
        const modal = window.bootstrap.Modal.getOrCreateInstance(els.limit);
        const list = els.limit.querySelector('[data-screen-limit-list]');
        const ok = els.limit.querySelector('[data-screen-limit-ok]');
        const needEl = els.limit.querySelector('[data-screen-limit-need]');
        els.limit.querySelector('[data-screen-limit-title]').textContent = fmt(text.LimitTitle, screens.length);
        els.limit.querySelector('[data-screen-limit-message]').textContent = fmt(text.LimitMessage, newTitle);
        list.innerHTML = screens.map((screen) => {
            let pending = 0;
            try { pending = frameToolbox(screen)?.screenFrame?.pendingChanges() || 0; } catch (error) { pending = 0; }
            return `<li><label class="form-check"><input class="form-check-input" type="checkbox" value="${screen.id}">`
                + `<span class="form-check-label">${icon(screen.icon, 14)} ${escapeHtml(screen.title)}`
                + `${pending ? ` <span class="tbx-screen-unsaved">· ${escapeHtml(text.Unsaved)}</span>` : ''}</span></label></li>`;
        }).join('');
        const refresh = () => {
            const chosen = list.querySelectorAll('input:checked').length;
            ok.disabled = chosen < need;
            needEl.textContent = chosen < need ? fmt(text.LimitNeed, need - chosen) : '';
        };
        list.onchange = refresh;
        refresh();
        return new Promise((resolve) => {
            let decided = false;
            ok.onclick = async () => {
                const chosen = Array.from(list.querySelectorAll('input:checked')).map((input) => screens.find((s) => s.id === input.value)).filter(Boolean);
                decided = true;
                modal.hide();
                resolve(await closeMany(chosen));
            };
            els.limit.addEventListener('hidden.bs.modal', () => { if (!decided) resolve(false); }, { once: true });
            modal.show();
        });
    }

    // ------------------------------------------------------------------ strip interactions

    els.tabs.addEventListener('click', (event) => {
        const screen = screenOf(event.target);
        if (!screen) return;
        if (event.target.closest('.tbx-screen-tab-close')) close(screen);
        else show(screen, { history: 'push', focus: true });
    });

    // Middle click closes; the press is cancelled so Windows browsers do not start auto-scrolling.
    let middleOn = null;
    els.tabs.addEventListener('mousedown', (event) => {
        if (event.button !== 1) return;
        middleOn = screenOf(event.target);
        if (middleOn) event.preventDefault();
    });
    els.tabs.addEventListener('mouseup', (event) => {
        if (event.button !== 1) return;
        const screen = screenOf(event.target);
        if (screen && screen === middleOn) {
            event.preventDefault();
            close(screen);
        }
        middleOn = null;
    });
    els.tabs.addEventListener('auxclick', (event) => {
        if (event.button === 1 && screenOf(event.target)) event.preventDefault();
    });

    els.tabs.addEventListener('keydown', (event) => {
        const screen = screenOf(event.target);
        if (!screen) return;
        const at = screens.indexOf(screen);
        let next = null;
        if (event.key === 'ArrowRight') next = screens[(at + 1) % screens.length];
        else if (event.key === 'ArrowLeft') next = screens[(at - 1 + screens.length) % screens.length];
        else if (event.key === 'Home') next = screens[0];
        else if (event.key === 'End') next = screens[screens.length - 1];
        else if (event.key === 'Delete') {
            event.preventDefault();
            close(screen);
            return;
        } else if (event.key === 'ContextMenu' || (event.key === 'F10' && event.shiftKey)) {
            event.preventDefault();
            const box = screen.el.getBoundingClientRect();
            showMenu(screen, box.left, box.bottom);
            return;
        }
        if (next) {
            event.preventDefault();
            show(next, { history: 'push' });
            next.el.querySelector('.tbx-screen-tab-open').focus();
        }
    });

    // Right click: close, close others, close all, reload.
    let menu = null;
    function hideMenu() {
        menu?.remove();
        menu = null;
    }

    function showMenu(screen, x, y) {
        hideMenu();
        menu = document.createElement('div');
        menu.className = 'tbx-screen-menu';
        menu.setAttribute('role', 'menu');
        menu.setAttribute('aria-label', text.Menu);
        const items = [
            ['reload', 'refresh-cw', text.Reload, () => reload(screen)],
            ['close', 'x', text.Close, () => close(screen)],
            ['others', 'x', text.CloseOthers, () => closeMany(screens.filter((s) => s !== screen))],
            ['all', 'x', text.CloseAll, () => closeMany(screens.slice())]
        ];
        menu.innerHTML = items.map(([key, name, label]) => `<button type="button" role="menuitem" data-action="${key}">${icon(name, 14)}<span>${escapeHtml(label)}</span></button>`).join('');
        menu.addEventListener('click', (event) => {
            const button = event.target.closest('[data-action]');
            if (!button) return;
            const item = items.find((i) => i[0] === button.dataset.action);
            hideMenu();
            item?.[3]();
        });
        menu.addEventListener('keydown', (event) => {
            const buttons = Array.from(menu.querySelectorAll('button'));
            const at = buttons.indexOf(document.activeElement);
            if (event.key === 'Escape') {
                hideMenu();
                screen.el.querySelector('.tbx-screen-tab-open').focus();
            } else if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
                event.preventDefault();
                buttons[(at + (event.key === 'ArrowDown' ? 1 : -1) + buttons.length) % buttons.length].focus();
            }
        });
        document.body.appendChild(menu);
        const width = menu.offsetWidth;
        menu.style.left = `${Math.max(8, Math.min(x, window.innerWidth - width - 8))}px`;
        menu.style.top = `${y + 4}px`;
        menu.querySelector('button').focus();
    }

    els.tabs.addEventListener('contextmenu', (event) => {
        const screen = screenOf(event.target);
        if (!screen) return;
        event.preventDefault();
        showMenu(screen, event.clientX, event.clientY);
    });
    document.addEventListener('mousedown', (event) => {
        if (menu && !menu.contains(event.target)) hideMenu();
    });
    window.addEventListener('blur', hideMenu);

    // ------------------------------------------------------------------ links and history

    // Links of the shell itself (sidebar, brand, focus chip, account menu) open screens.
    document.addEventListener('click', (event) => {
        if (event.defaultPrevented || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
        const link = event.target.closest && event.target.closest('a[href]');
        if (!link || link.hasAttribute('download') || (link.target && link.target !== '_self') || link.hasAttribute('data-no-screen')) return;
        if (classify(link.href) !== 'screen') return;
        event.preventDefault();
        // The narrow-screen drawer closes like it does after a normal link.
        document.querySelector('[data-sidebar-close]')?.dispatchEvent(new MouseEvent('click', { bubbles: true }));
        open(link.href);
    });

    window.addEventListener('popstate', (event) => {
        const id = event.state && event.state.screen;
        const byId = id ? screens.find((s) => s.id === id) : null;
        const url = window.location.pathname + window.location.search + window.location.hash;
        const target = byId || screens.find((s) => pathOf(s.url) === pathOf(url));
        if (target) show(target, { history: 'none' });
        else open(url, { history: 'replace' });
    });

    // ------------------------------------------------------------------ for the screens and the palette

    function screenOfWindow(win) {
        return screens.find((s) => frameWindow(s) === win) || null;
    }

    Toolbox.go = (href, options) => { open(href, options); };
    Toolbox.screens = {
        open,
        classify,
        /** A command of one screen in Ctrl+K: listed only while that screen shows. */
        addCommand(win, command) {
            if (!Toolbox.palette || !Toolbox.palette.add) return () => {};
            return Toolbox.palette.add(Object.assign({}, command, {
                id: `screen-${Math.random().toString(36).slice(2, 8)}-${command.id}`,
                when: () => {
                    const screen = active;
                    return Boolean(screen) && frameWindow(screen) === win;
                }
            }));
        },
        active: () => active,
        list: () => screens.slice(),
        close: (target) => close(typeof target === 'string' ? screens.find((s) => s.id === target) : target),
        screenOfWindow,
        text,
        /** Next (+1) or previous (-1) screen of the strip, round. */
        step(delta) {
            if (!screens.length) return;
            const at = active ? screens.indexOf(active) : -1;
            show(screens[(at + delta + screens.length) % screens.length], { history: 'push', focus: true });
        },
        /** The Nth screen of the strip (1-based); 9 is the last one, like browsers. */
        nth(n) {
            const screen = n >= 9 ? screens[screens.length - 1] : screens[n - 1];
            if (screen) show(screen, { history: 'push', focus: true });
        },
        closeActive: () => close(active),
        /** A screen opened or closed a dialog: the shell around it dims too. */
        setDialog(win, open) {
            const screen = screenOfWindow(win);
            if (!screen) return;
            screen.dialog = open;
            syncDim();
        }
    };

    // ------------------------------------------------------------------ dialogs of a screen

    // While the screen on show has a dialog open, the sidebar, the header and the strip are
    // dimmed and locked like the screen itself, so the dialog reads as the window's. A click on
    // the dimmed part closes the dialog when the dialog itself allows that.
    const backdrop = document.createElement('div');
    backdrop.className = 'modal-backdrop fade tbx-screen-backdrop';
    backdrop.hidden = true;
    (document.querySelector('[data-shell]') || document.body).appendChild(backdrop);

    function syncDim() {
        const on = Boolean(active && active.dialog);
        document.documentElement.classList.toggle('tbx-screen-dimmed', on);
        backdrop.hidden = !on;
        window.requestAnimationFrame(() => backdrop.classList.toggle('show', on));
    }

    backdrop.addEventListener('click', () => {
        const win = active && frameWindow(active);
        try {
            const modal = win?.document.querySelector('.modal.show');
            if (modal && modal.dataset.bsBackdrop !== 'static') win.bootstrap?.Modal.getInstance(modal)?.hide();
        } catch (error) { /* gone */ }
    });

    // ------------------------------------------------------------------ keys

    // The help (?) lists the shortcuts of the screen on show (it shows it inside the screen).
    Toolbox.helpFor = () => {
        const tb = frameToolbox(active);
        if (!tb || !tb.keys) return false;
        try {
            tb.keys.showHelp();
            active.frame.contentWindow.focus();
            return true;
        } catch (error) {
            return false;
        }
    };

    if (Toolbox.keys) {
        Toolbox.keys.add({ keys: 'alt+pagedown', group: 'app', label: text.KeyNext, inInputs: true, run: () => Toolbox.screens.step(1) });
        Toolbox.keys.add({ keys: 'alt+pageup', group: 'app', label: text.KeyPrevious, inInputs: true, run: () => Toolbox.screens.step(-1) });
        Toolbox.keys.add({
            keys: ['alt+1', 'alt+2', 'alt+3', 'alt+4', 'alt+5', 'alt+6', 'alt+7', 'alt+8', 'alt+9'],
            display: [Toolbox.keys.display('alt+1') + '…9'],
            group: 'app',
            label: text.KeyNth,
            inInputs: true,
            run: (event) => Toolbox.screens.nth(Number(event.key))
        });
        Toolbox.keys.add({ keys: 'alt+shift+w', group: 'app', label: text.KeyClose, inInputs: true, run: () => Toolbox.screens.closeActive() });
    }

    // The shell's own commands.
    if (Toolbox.palette && Toolbox.palette.add) {
        Toolbox.palette.add({ id: 'screens-reload', label: text.PaletteReload, icon: 'refresh-cw', run: () => reload(active) });
        Toolbox.palette.add({ id: 'screens-close-others', label: text.PaletteCloseOthers, icon: 'x', run: () => closeMany(screens.filter((s) => s !== active)) });
        Toolbox.palette.add({ id: 'screens-close-all', label: text.PaletteCloseAll, icon: 'x', run: () => closeMany(screens.slice()) });
    }

    // Closing the window or reloading it with unsaved work in a kept screen asks first.
    window.addEventListener('beforeunload', (event) => {
        const pending = screens.some((screen) => {
            try { return (frameToolbox(screen)?.screenFrame?.pendingChanges() || 0) > 0; } catch (error) { return false; }
        });
        if (pending) {
            event.preventDefault();
            event.returnValue = '';
        }
    });

    // ------------------------------------------------------------------ start

    (function start() {
        const here = relative(window.location.href);
        const saved = load();
        const seen = new Set();
        (saved && Array.isArray(saved.screens) ? saved.screens : []).forEach((item) => {
            if (!item || typeof item.url !== 'string' || classify(item.url) !== 'screen') return;
            const path = pathOf(item.url);
            if (seen.has(path)) return;
            seen.add(path);
            // A page of the app takes its name from the menu, in the language now chosen.
            const screen = makeScreen(item.url, pageFor(item.url)?.label || item.title);
            screens.push(screen);
            els.tabs.appendChild(screen.el);
        });
        // The address that was opened is the screen to show (a remembered one keeps its place).
        let current = screens.find((s) => pathOf(s.url) === pathOf(here));
        if (current) current.url = here;
        else {
            current = makeScreen(here);
            screens.push(current);
            els.tabs.appendChild(current.el);
        }
        show(current, { history: 'replace' });
    })();
})(window, document);
