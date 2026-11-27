// A screen kept inside the shell's frame (Infrastructure/Web/ScreenFrames.cs, Content/js/screens.js).
// Loaded by _Layout.cshtml after site.js and shortcuts.js and before the page's own scripts, so the
// page keeps calling the same Toolbox APIs while the shell does the app-wide parts:
// - moving to another page (links, the palette, "G then letter") switches screens in the shell;
// - Ctrl+K opens the shell's palette, and the page's own commands appear there while it shows;
// - the focus timer is the shell's one (window.ToolboxFocus), so there is one countdown and one chime;
// - while the screen is hidden, Toolbox.isScreenVisible() is false, so polling timers wait;
// - Toolbox.screenFrame lets the shell ask about unsaved work and tidy up before closing it.
(function (window, document) {
    'use strict';

    const Toolbox = window.Toolbox = window.Toolbox || {};
    let shell = null;
    try {
        shell = window.parent !== window && window.parent.Toolbox && window.parent.Toolbox.screens ? window.parent : null;
    } catch (error) {
        shell = null;
    }
    if (!shell) {
        // Not inside the shell (another frame, or the shell is gone): the page works on its own.
        return;
    }

    const screens = shell.Toolbox.screens;
    const disposers = [];
    let visible = true;
    let disposed = false;

    Toolbox.go = function (href, options) {
        screens.open(href, options);
    };

    Toolbox.isScreenVisible = function () {
        return visible && document.visibilityState === 'visible';
    };

    // Ctrl+K and the page's own commands live in the shell's palette.
    Toolbox.palette = {
        open: function () { shell.Toolbox.palette?.open(); },
        add: function (command) {
            const remove = screens.addCommand(window, command);
            disposers.push(remove);
            return remove;
        }
    };
    Toolbox.keys?.add({
        keys: 'mod+k',
        group: 'app',
        label: Toolbox.keys.label('palette'),
        inInputs: true,
        inModal: true,
        run: function () { shell.Toolbox.palette?.open(); }
    });

    // Moving between screens works with the keyboard inside a screen too (and shows in its "?").
    const screenText = screens.text || {};
    if (Toolbox.keys) {
        Toolbox.keys.add({ keys: 'alt+pagedown', group: 'app', label: screenText.KeyNext, inInputs: true, run: function () { screens.step(1); } });
        Toolbox.keys.add({ keys: 'alt+pageup', group: 'app', label: screenText.KeyPrevious, inInputs: true, run: function () { screens.step(-1); } });
        Toolbox.keys.add({
            keys: ['alt+1', 'alt+2', 'alt+3', 'alt+4', 'alt+5', 'alt+6', 'alt+7', 'alt+8', 'alt+9'],
            display: [Toolbox.keys.display('alt+1') + '…9'],
            group: 'app',
            label: screenText.KeyNth,
            inInputs: true,
            run: function (event) { screens.nth(Number(event.key)); }
        });
        Toolbox.keys.add({ keys: 'alt+shift+w', group: 'app', label: screenText.KeyClose, inInputs: true, run: function () { screens.closeActive(); } });
    }

    // A dialog of this screen: the shell dims around it (screens.js).
    function syncDialog() {
        try { screens.setDialog(window, Boolean(document.querySelector('.modal.show'))); } catch (error) { /* shell gone */ }
    }
    document.addEventListener('shown.bs.modal', syncDialog);
    document.addEventListener('hidden.bs.modal', syncDialog);
    disposers.push(function () {
        try { screens.setDialog(window, false); } catch (error) { /* shell gone */ }
    });

    // A form someone started filling in counts as unsaved work until it is sent, reset, or its
    // dialog is closed (closing the dialog throws the typing away, as it always did). Search and
    // filter forms (GET) and forms marked data-no-unsaved do not count.
    const typedForms = new Set();
    function formOf(target) {
        const form = target && (target.form || (target.closest && target.closest('form')));
        if (!form || form.hasAttribute('data-no-unsaved') || (form.method || 'get').toLowerCase() !== 'post') return null;
        if (target.type === 'search' || target.type === 'hidden') return null;
        return form;
    }
    function typed(event) {
        if (!event.isTrusted) return;
        const form = formOf(event.target);
        if (form) typedForms.add(form);
    }
    document.addEventListener('input', typed, true);
    document.addEventListener('change', typed, true);
    document.addEventListener('submit', function (event) { typedForms.delete(event.target); }, true);
    document.addEventListener('reset', function (event) { typedForms.delete(event.target); }, true);
    document.addEventListener('hidden.bs.modal', function (event) {
        typedForms.forEach(function (form) {
            if (event.target.contains(form)) typedForms.delete(form);
        });
    });
    Toolbox.pendingChanges?.add(function () {
        let count = 0;
        typedForms.forEach(function (form) {
            if (form.isConnected) count++;
            else typedForms.delete(form);
        });
        return count;
    });

    // One focus timer for the whole app: the shell's. A page that follows it (the timer page)
    // stops following when this screen closes or moves on.
    if (shell.ToolboxFocus) {
        const focus = shell.ToolboxFocus;
        window.ToolboxFocus = Object.assign({}, focus, {
            subscribe: function (fn) {
                const off = focus.subscribe(fn);
                disposers.push(off);
                return off;
            }
        });
    }

    // Links to another screen switch screens in the shell; a page that must be the whole window
    // (sign-in, activation, update) opens there.
    document.addEventListener('click', function (event) {
        if (event.defaultPrevented || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) {
            return;
        }
        const link = event.target.closest && event.target.closest('a[href]');
        if (!link || link.hasAttribute('download') || (link.target && link.target !== '_self') || link.hasAttribute('data-no-screen')) {
            return;
        }
        const kind = screens.classify(link.href, window.location.href);
        if (kind === 'screen') {
            event.preventDefault();
            Toolbox.go(link.href);
        } else if (kind === 'top') {
            event.preventDefault();
            shell.location.href = link.href;
        }
    });

    function dispose() {
        if (disposed) return;
        disposed = true;
        disposers.splice(0).forEach(function (fn) {
            try { fn(); } catch (error) { /* the shell may already be gone */ }
        });
    }

    Toolbox.screenFrame = {
        /** Shown (true) or hidden behind another screen (false); a screen shown again redraws its grids and charts. */
        setVisible: function (on) {
            if (visible === on) return;
            visible = on;
            document.dispatchEvent(new CustomEvent('toolbox:screen-visibility', { detail: { visible: on } }));
            if (on) window.dispatchEvent(new Event('resize'));
        },
        /** How many unsaved things the page reports (Toolbox.pendingChanges). */
        pendingChanges: function () {
            return Toolbox.pendingChanges ? Toolbox.pendingChanges.count() : 0;
        },
        /** Called by the shell before it removes the frame, and when the page goes away. */
        dispose: dispose
    };

    window.addEventListener('pagehide', dispose);
})(window, document);
