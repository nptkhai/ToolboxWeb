// Keyboard shortcuts for every signed-in page (loaded by _Layout.cshtml after site.js).
// window.Toolbox.keys is the one place that listens for shortcut keys: the layout registers
// the app-wide ones (Ctrl/⌘+K, ?, G then a letter) and each page script registers its own with
// Toolbox.keys.add(...). The dispatcher skips keys typed into a field or pressed while a dialog
// is open (unless the shortcut says otherwise), the help dialog (?) and the "shortcuts" line
// under a page ([data-shortcuts-line]) list what is registered, and a shortcut tied to a button
// puts a small key hint on it. Texts come from #tbx-command-data (Views/Shared/_CommandPalette).
(function (window, document) {
    'use strict';

    const Toolbox = window.Toolbox = window.Toolbox || {};
    const config = readJson('tbx-command-data') || {};
    const text = config.text || {};
    const isMac = /Mac|iPhone|iPad/.test(window.navigator.platform || window.navigator.userAgent || '');
    const entries = [];
    const GO_TIMEOUT_MS = 1500;
    let goPending = 0;

    function readJson(id) {
        try {
            return JSON.parse(document.getElementById(id)?.textContent || 'null');
        } catch (error) {
            return null;
        }
    }

    function isTyping(target) {
        return target instanceof HTMLElement
            && (target.isContentEditable
                || /^(INPUT|TEXTAREA|SELECT)$/.test(target.tagName)
                || Boolean(target.closest('.k-input, .k-picker, .tabulator-editing')));
    }

    // "mod+k", "alt+arrowup", "?", "g d" (a sequence), "space".
    function parse(combo) {
        return String(combo).toLowerCase().split(' ').map(function (step) {
            const parts = step.split('+');
            const key = parts.pop();
            return {
                key: key === 'space' ? ' ' : key,
                mod: parts.indexOf('mod') >= 0,
                alt: parts.indexOf('alt') >= 0,
                shift: parts.indexOf('shift') >= 0
            };
        });
    }

    function stepMatches(step, event) {
        // Some keyboard layouts (and test drivers) report Shift+/ as "/" with Shift held: that is "?".
        const key = event.key === '/' && event.shiftKey ? '?' : (event.key || '').toLowerCase();
        return key === step.key
            && step.mod === (event.ctrlKey || event.metaKey)
            && step.alt === event.altKey
            && (!step.shift || event.shiftKey);
    }

    const KEY_NAMES = {
        arrowup: '↑', arrowdown: '↓', arrowleft: '←', arrowright: '→', enter: 'Enter', escape: 'Esc',
        delete: 'Delete', backspace: 'Backspace', ' ': 'Space', '/': '/', '?': '?'
    };

    /** How a combo is written for people: "Ctrl K" or "⌘ K", "G D", "↑". */
    function display(combo) {
        return parse(combo).map(function (step) {
            const parts = [];
            if (step.mod) parts.push(isMac ? '⌘' : 'Ctrl');
            if (step.alt) parts.push(isMac ? '⌥' : 'Alt');
            if (step.shift) parts.push('Shift');
            parts.push(KEY_NAMES[step.key] || step.key.toUpperCase());
            return parts.join(' ');
        }).join(' ');
    }

    /** The ARIA form of a combo for aria-keyshortcuts: "Control+K Meta+K". */
    function ariaKeys(combo) {
        const steps = parse(combo);
        if (steps.length > 1) {
            return null;
        }
        const step = steps[0];
        const key = step.key === ' ' ? 'Space' : (KEY_NAMES[step.key] && step.key.length > 1 ? step.key.charAt(0).toUpperCase() + step.key.slice(1) : step.key.toUpperCase());
        const base = (step.alt ? 'Alt+' : '') + (step.shift ? 'Shift+' : '') + key;
        return step.mod ? 'Control+' + base + ' Meta+' + base : base;
    }

    /**
     * Registers a shortcut. def: { keys: "n" | ["arrowdown", "j"], label, group: "page" (default),
     * "app" or "go", run(event), when(event) → bool, inInputs, inModal, button: element, hidden }.
     * Returns a function that removes it.
     */
    function add(def) {
        const entry = Object.assign({ group: 'page' }, def);
        entry.keys = [].concat(def.keys);
        entry.steps = entry.keys.map(parse);
        entries.push(entry);
        if (entry.button) {
            hint(entry.button, entry.keys[0]);
        }
        refreshLines();
        return function () {
            const index = entries.indexOf(entry);
            if (index >= 0) {
                entries.splice(index, 1);
                refreshLines();
            }
        };
    }

    // A small key hint on the button or search box the shortcut presses (hidden on touch screens).
    function hint(element, combo) {
        if (!(element instanceof HTMLElement) || element.querySelector(':scope > .tbx-kbd')) {
            return;
        }
        const aria = ariaKeys(combo);
        if (aria) {
            element.setAttribute('aria-keyshortcuts', aria);
        }
        const kbd = document.createElement('kbd');
        kbd.className = 'tbx-kbd';
        kbd.setAttribute('aria-hidden', 'true');
        kbd.textContent = display(combo);
        if (/^(INPUT|TEXTAREA|SELECT)$/.test(element.tagName)) {
            // A field cannot hold the hint; it sits over the right end of the field instead.
            kbd.classList.add('tbx-kbd--field');
            element.parentElement?.classList.add('tbx-kbd-host');
            element.insertAdjacentElement('afterend', kbd);
        } else {
            element.appendChild(kbd);
        }
    }

    function applicable(entry, event) {
        if (!entry.inInputs && isTyping(event.target)) return false;
        if (!entry.inModal && document.querySelector('.modal.show')) return false;
        return !entry.when || entry.when(event);
    }

    function run(entry, event) {
        event.preventDefault();
        entry.run(event);
    }

    document.addEventListener('keydown', function (event) {
        if (event.defaultPrevented || event.isComposing || event.repeat && !/^arrow/i.test(event.key)) {
            return;
        }

        // Second key of a "G then letter" sequence.
        if (goPending && Date.now() - goPending < GO_TIMEOUT_MS) {
            goPending = 0;
            const next = entries.find(function (entry) {
                return entry.steps.some(function (steps) { return steps.length === 2 && stepMatches(steps[1], event); })
                    && applicable(entry, event);
            });
            if (next) {
                run(next, event);
                return;
            }
        }
        goPending = 0;

        const entry = entries.find(function (candidate) {
            return candidate.steps.some(function (steps) { return steps.length === 1 && stepMatches(steps[0], event); })
                && applicable(candidate, event);
        });
        if (entry) {
            run(entry, event);
            return;
        }

        // First key of a sequence: wait for the second.
        const starts = entries.some(function (candidate) {
            return candidate.steps.some(function (steps) { return steps.length === 2 && stepMatches(steps[0], event); })
                && applicable(candidate, event);
        });
        if (starts) {
            goPending = Date.now();
        }
    });

    // ------------------------------------------------------------ help (?) and page lines

    const helpElement = document.getElementById('tbx-shortcuts');

    function rowHtml(entry) {
        const keys = (entry.display || entry.keys).map(function (combo) {
            return '<kbd>' + escapeHtml(entry.display ? combo : display(combo)) + '</kbd>';
        }).join(' ');
        return '<li><span class="tbx-shortcut-keys">' + keys + '</span><span>' + escapeHtml(entry.label) + '</span></li>';
    }

    function escapeHtml(value) {
        return String(value ?? '').replace(/[&<>"']/g, function (ch) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[ch];
        });
    }

    function visible(group) {
        return entries.filter(function (entry) { return entry.group === group && entry.label && !entry.hidden; });
    }

    function showHelp() {
        if (!helpElement || !window.bootstrap) {
            return;
        }
        const sections = [
            ['app', text.helpApp],
            ['page', text.helpPage],
            ['go', text.helpGo]
        ];
        helpElement.querySelector('[data-shortcuts-body]').innerHTML = sections.map(function (section) {
            const list = visible(section[0]);
            return list.length
                ? '<section><h3 class="tbx-shortcuts-title">' + escapeHtml(section[1]) + '</h3><ul class="tbx-shortcut-list">' + list.map(rowHtml).join('') + '</ul></section>'
                : '';
        }).join('');
        window.bootstrap.Modal.getOrCreateInstance(helpElement).show();
    }

    // The one-line reminder under a page: its own shortcuts, then "? all shortcuts".
    function refreshLines() {
        const lines = document.querySelectorAll('[data-shortcuts-line]');
        if (!lines.length) {
            return;
        }
        const parts = visible('page').map(function (entry) {
            return (entry.display || entry.keys.map(display)).join('/') + ' ' + entry.label.toLowerCase();
        });
        parts.push('? ' + (text.allShortcuts || ''));
        lines.forEach(function (line) {
            line.textContent = (text.shortcutsPrefix ? text.shortcutsPrefix + ' ' : '') + parts.join(' · ');
        });
    }

    Toolbox.keys = {
        add: add,
        isTyping: isTyping,
        display: display,
        showHelp: showHelp,
        isMac: isMac,
        /** A shared label for a page shortcut ("search", "new", "edit", …). */
        label: function (name) { return (config.labels || {})[name] || name; }
    };

    // ------------------------------------------------------------ app-wide shortcuts

    add({ keys: '?', group: 'app', label: Toolbox.keys.label('help'), display: ['?'], run: showHelp });

    (config.pages || []).forEach(function (pageItem) {
        if (pageItem.go) {
            add({
                keys: 'g ' + pageItem.go,
                group: 'go',
                label: pageItem.label,
                run: function () { window.location.href = pageItem.href; }
            });
        }
    });

    document.addEventListener('DOMContentLoaded', refreshLines);
})(window, document);
