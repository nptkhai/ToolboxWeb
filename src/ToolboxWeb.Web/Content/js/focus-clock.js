// The focus timer, on every signed-in page (loaded by _Layout.cshtml). Its state lives in the
// browser (localStorage), so the countdown carries on through page changes and reloads, and
// every open tab shows the same time. While it runs, the header chip and the tab title show
// the time left. When a focus session ends, it is saved on its own (POST FocusTimer/Complete),
// a short chime plays and the browser shows a notification if the user allowed it; then a
// short or long break is ready. The timer page (Content/js/focus-timer.js) drives it through
// window.ToolboxFocus.
(function (window, document) {
    'use strict';

    const STATE_KEY = 'toolbox.focus.v1';
    const SETTINGS_KEY = 'toolbox.focus.settings.v1';
    const FINISHED_KEY = 'toolbox.focus.finished';
    const DEFAULTS = { focus: 25, short: 5, long: 15, longEvery: 4, sound: true, notify: false, goal: 120 };

    const chip = document.querySelector('[data-focus-chip]');
    const listeners = new Set();
    const baseTitle = document.title;
    let tickTimer = null;

    function read(key) {
        try {
            return JSON.parse(window.localStorage.getItem(key) || 'null');
        } catch (error) {
            return null;
        }
    }

    function write(key, value) {
        try {
            window.localStorage.setItem(key, JSON.stringify(value));
        } catch (error) {
            // Storage can be off (private window); the timer then lives in this page only.
        }
    }

    function settings() {
        return Object.assign({}, DEFAULTS, read(SETTINGS_KEY) || {});
    }

    function saveSettings(changes) {
        write(SETTINGS_KEY, Object.assign(settings(), changes));
        emit();
    }

    function minutesOf(phase) {
        const s = settings();
        return Math.min(240, Math.max(1, Number(phase === 'focus' ? s.focus : phase === 'long' ? s.long : s.short) || 1));
    }

    function idleState(phase, cycle) {
        return { phase: phase, status: 'idle', durationMs: minutesOf(phase) * 60000, remainingMs: minutesOf(phase) * 60000, cycle: cycle || 0 };
    }

    function state() {
        const saved = read(STATE_KEY);
        return saved && saved.phase ? saved : idleState('focus', 0);
    }

    function setState(next) {
        write(STATE_KEY, next);
        emit();
    }

    function remaining(s) {
        if (s.status === 'running') {
            return Math.max(0, s.endsAt - Date.now());
        }
        return s.remainingMs;
    }

    function clock(ms) {
        const total = Math.ceil(ms / 1000);
        const m = Math.floor(total / 60);
        const sec = total % 60;
        return String(m).padStart(2, '0') + ':' + String(sec).padStart(2, '0');
    }

    // ------------------------------------------------------------ actions

    function start() {
        const s = state();
        if (s.status === 'running') {
            return;
        }

        const left = s.status === 'paused' ? s.remainingMs : s.durationMs;
        setState(Object.assign({}, s, {
            status: 'running',
            endsAt: Date.now() + left,
            // The first start of a session identifies it; pausing does not change it.
            startedAt: s.status === 'paused' && s.startedAt ? s.startedAt : Date.now()
        }));
    }

    function pause() {
        const s = state();
        if (s.status !== 'running') {
            return;
        }
        setState(Object.assign({}, s, { status: 'paused', remainingMs: remaining(s) }));
    }

    function toggle() {
        if (state().status === 'running') {
            pause();
        } else {
            start();
        }
    }

    /** Back to a fresh session of the current phase; nothing is saved. */
    function reset() {
        const s = state();
        setState(Object.assign(idleState(s.phase, s.cycle), { todoId: s.todoId, note: s.note }));
    }

    /** A different phase, or a new length for the current one, while nothing runs. */
    function setPhase(phase) {
        const s = state();
        setState(Object.assign(idleState(phase, s.cycle), { todoId: s.todoId, note: s.note }));
    }

    function setMinutes(minutes) {
        const s = state();
        const key = s.phase === 'focus' ? 'focus' : s.phase;
        const changes = {};
        changes[key] = Math.min(240, Math.max(1, Math.round(Number(minutes) || 1)));
        write(SETTINGS_KEY, Object.assign(settings(), changes));
        if (s.status === 'idle') {
            setState(Object.assign(idleState(s.phase, s.cycle), { todoId: s.todoId, note: s.note }));
        } else {
            emit();
        }
    }

    function setLink(todoId, note) {
        const s = state();
        setState(Object.assign({}, s, { todoId: todoId || null, note: note || '' }));
    }

    /** Minutes of focus spent so far in the current session. */
    function spentMinutes() {
        const s = state();
        return s.phase === 'focus' ? Math.floor((s.durationMs - remaining(s)) / 60000) : 0;
    }

    /** Ends the current focus session early; saves the minutes spent when asked. */
    async function stop(save) {
        const s = state();
        const minutes = spentMinutes();
        let saved = false;
        if (save && s.phase === 'focus' && minutes >= 1) {
            saved = await saveSession(s, minutes);
        }
        setState(Object.assign(idleState('focus', s.cycle), { todoId: s.todoId, note: '' }));
        return saved;
    }

    // ------------------------------------------------------------ finishing

    async function saveSession(s, minutes) {
        const token = document.querySelector('[data-focus-token] input[name="__RequestVerificationToken"]');
        const body = new URLSearchParams({
            DurationMinutes: String(minutes),
            StartedAtMs: String(s.startedAt || Date.now() - minutes * 60000),
            Note: s.note || ''
        });
        if (s.todoId) {
            body.append('ChecklistItemId', String(s.todoId));
        }

        try {
            const response = await fetch(chip.dataset.completeUrl, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/x-www-form-urlencoded',
                    'RequestVerificationToken': token ? token.value : '',
                    'X-Requested-With': 'XMLHttpRequest'
                },
                body: body
            });
            if (!response.ok) {
                throw new Error(String(response.status));
            }
            window.dispatchEvent(new CustomEvent('toolbox:focus-saved', { detail: { minutes: minutes } }));
            return true;
        } catch (error) {
            notify(chip.dataset.textSaveFailed, 'error');
            return false;
        }
    }

    function chime() {
        const Context = window.AudioContext || window.webkitAudioContext;
        if (!Context) {
            return;
        }

        try {
            const audio = new Context();
            [0, 0.28, 0.56].forEach(function (offset, index) {
                const tone = audio.createOscillator();
                const gain = audio.createGain();
                tone.type = 'sine';
                tone.frequency.value = index === 2 ? 1046.5 : 880;
                gain.gain.setValueAtTime(0.0001, audio.currentTime + offset);
                gain.gain.exponentialRampToValueAtTime(0.25, audio.currentTime + offset + 0.02);
                gain.gain.exponentialRampToValueAtTime(0.0001, audio.currentTime + offset + 0.24);
                tone.connect(gain).connect(audio.destination);
                tone.start(audio.currentTime + offset);
                tone.stop(audio.currentTime + offset + 0.25);
            });
            window.setTimeout(function () { audio.close(); }, 1200);
        } catch (error) {
            // Some browsers block sound until the page was clicked; the notification still shows.
        }
    }

    function notify(message, type) {
        if (window.Toolbox && window.Toolbox.notify) {
            window.Toolbox.notify(message, type || 'success');
        }
    }

    function browserNotice(message) {
        if (!settings().notify || !('Notification' in window) || Notification.permission !== 'granted') {
            return;
        }
        try {
            new Notification('ToolboxWeb', { body: message, tag: 'toolbox-focus' });
        } catch (error) {
            // Notifications can be refused by the system even when the site may send them.
        }
    }

    async function finish(s) {
        // Every open tab reaches zero; the first one to claim the session does the work.
        const key = s.phase + ':' + s.startedAt;
        if (read(FINISHED_KEY) === key) {
            return;
        }
        write(FINISHED_KEY, key);

        const conf = settings();
        if (conf.sound) {
            chime();
        }

        if (s.phase === 'focus') {
            const minutes = Math.round(s.durationMs / 60000);
            const cycle = (s.cycle || 0) + 1;
            const next = cycle % Math.max(1, conf.longEvery) === 0 ? 'long' : 'short';
            setState(Object.assign(idleState(next, cycle), { todoId: s.todoId, note: '' }));
            const message = chip.dataset.textFocusDone.replace('{0}', minutes).replace('{1}', minutesOf(next));
            browserNotice(message);
            const saved = await saveSession(s, minutes);
            if (saved) {
                notify(message, 'success');
            }
        } else {
            const cycle = s.phase === 'long' ? 0 : s.cycle;
            setState(Object.assign(idleState('focus', cycle), { todoId: s.todoId, note: '' }));
            const message = chip.dataset.textBreakDone;
            browserNotice(message);
            notify(message, 'info');
        }
    }

    // ------------------------------------------------------------ display

    function emit() {
        const s = state();
        render(s);
        listeners.forEach(function (fn) { fn(s, remaining(s)); });
    }

    function render(s) {
        const left = remaining(s);
        const active = s.status === 'running' || s.status === 'paused';
        if (chip) {
            chip.hidden = !active;
            chip.classList.toggle('is-paused', s.status === 'paused');
            chip.classList.toggle('is-break', s.phase !== 'focus');
            chip.querySelector('[data-focus-chip-time]').textContent = clock(left);
            chip.querySelector('[data-focus-chip-phase]').textContent =
                s.phase === 'focus' ? chip.dataset.textFocus : s.phase === 'long' ? chip.dataset.textLong : chip.dataset.textShort;
        }
        document.title = s.status === 'running' ? clock(left) + ' · ' + baseTitle : baseTitle;
    }

    function tick() {
        const s = state();
        if (s.status === 'running' && remaining(s) <= 0) {
            finish(s);
            return;
        }
        render(s);
        listeners.forEach(function (fn) { fn(s, remaining(s)); });
    }

    function ensureTicking() {
        if (!tickTimer) {
            tickTimer = window.setInterval(tick, 250);
        }
    }

    // Another tab started, paused or finished the timer.
    window.addEventListener('storage', function (event) {
        if (event.key === STATE_KEY || event.key === SETTINGS_KEY) {
            emit();
        }
    });

    window.ToolboxFocus = {
        state: state,
        remaining: function () { return remaining(state()); },
        settings: settings,
        saveSettings: saveSettings,
        minutesOf: minutesOf,
        clock: clock,
        start: start,
        pause: pause,
        toggle: toggle,
        reset: reset,
        stop: stop,
        spentMinutes: spentMinutes,
        setPhase: setPhase,
        setMinutes: setMinutes,
        setLink: setLink,
        chime: chime,
        subscribe: function (fn) {
            listeners.add(fn);
            fn(state(), remaining(state()));
            return function () { listeners.delete(fn); };
        }
    };

    ensureTicking();
    render(state());
})(window, document);
