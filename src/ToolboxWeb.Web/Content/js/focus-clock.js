// The focus timer, on every signed-in page (loaded by _Layout.cshtml). Its state lives in the
// browser (localStorage), so the countdown carries on through page changes and reloads, and
// every open tab shows the same time. While it runs, the header chip and the tab title show
// the time left. When a focus session ends, it is saved on its own (POST FocusTimer/Complete),
// a chime plays (a few seconds, stopped by any click or key) and the browser shows a notification
// if the user allowed it; then a short or long break is ready. A session that cannot be saved
// (no connection, expired sign-in) waits in localStorage and is sent again until the server has
// it. The timer page (Content/js/focus-timer.js) drives it through window.ToolboxFocus.
(function (window, document) {
    'use strict';

    const STATE_KEY = 'toolbox.focus.v1';
    const SETTINGS_KEY = 'toolbox.focus.settings.v1';
    const FINISHED_KEY = 'toolbox.focus.finished';
    const UNSAVED_KEY = 'toolbox.focus.unsaved.v1';
    const RETRY_KEY = 'toolbox.focus.retrying';
    const RETRY_EVERY_MS = 30000;
    // A session found finished long after its end (the browser was closed) is saved quietly.
    const LATE_MS = 2 * 60000;
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

    /**
     * Ends the current focus session early; keeps the minutes spent when asked. Resolves to
     * "saved", "queued" (kept in the browser to be sent again) or false (nothing to keep).
     */
    async function stop(save) {
        const s = state();
        const minutes = spentMinutes();
        let result = false;
        if (save && s.phase === 'focus' && minutes >= 1) {
            result = await keepSession(sessionOf(s, minutes, Date.now()));
        }
        setState(Object.assign(idleState('focus', s.cycle), { todoId: s.todoId, note: '' }));
        return result;
    }

    // ------------------------------------------------------------ finishing

    /** What the server needs to store one session; endedAt is when it really ended. */
    function sessionOf(s, minutes, endedAt) {
        return {
            minutes: minutes,
            startedAt: s.startedAt || endedAt - minutes * 60000,
            endedAt: endedAt,
            note: s.note || '',
            todoId: s.todoId || null
        };
    }

    async function postSession(item) {
        const token = document.querySelector('[data-focus-token] input[name="__RequestVerificationToken"]');
        const body = new URLSearchParams({
            DurationMinutes: String(item.minutes),
            StartedAtMs: String(item.startedAt),
            EndedAtMs: String(item.endedAt),
            Note: item.note || ''
        });
        if (item.todoId) {
            body.append('ChecklistItemId', String(item.todoId));
        }

        const response = await fetch(chip.dataset.completeUrl, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
                'RequestVerificationToken': token ? token.value : '',
                'X-Requested-With': 'XMLHttpRequest'
            },
            body: body
        });
        const json = (response.headers.get('content-type') || '').includes('application/json');
        // The server read the session and refused it (out of range): sending it again cannot help.
        if (response.status === 400 && json) {
            return 'rejected';
        }
        // A sign-in page answers with HTML; only the JSON answer counts as stored.
        if (!response.ok || !json) {
            throw new Error(String(response.status));
        }
        return 'saved';
    }

    function unsaved() {
        const list = read(UNSAVED_KEY);
        return Array.isArray(list) ? list : [];
    }

    function queue(item) {
        const list = unsaved().filter(function (x) { return x.startedAt !== item.startedAt; });
        list.push(item);
        write(UNSAVED_KEY, list);
    }

    function unqueue(item) {
        write(UNSAVED_KEY, unsaved().filter(function (x) { return x.startedAt !== item.startedAt; }));
    }

    /** Sends a session; when that fails it waits in the browser and goes again later. */
    async function keepSession(item) {
        try {
            if (await postSession(item) === 'rejected') {
                return false;
            }
            window.dispatchEvent(new CustomEvent('toolbox:focus-saved', { detail: { minutes: item.minutes } }));
            return 'saved';
        } catch (error) {
            queue(item);
            notify(chip.dataset.textSaveFailed, 'warning');
            return 'queued';
        }
    }

    // Sessions waiting from an earlier failure. One tab sends them at a time; the server also
    // ignores a session it already has (same start), so a repeat does no harm.
    let retrying = false;
    async function retryUnsaved() {
        const list = unsaved();
        const lastTry = Number(read(RETRY_KEY)) || 0;
        if (retrying || !list.length || Date.now() - lastTry < RETRY_EVERY_MS / 2) {
            return;
        }

        retrying = true;
        write(RETRY_KEY, Date.now());
        let sent = 0;
        try {
            for (const item of list) {
                const result = await postSession(item);
                unqueue(item);
                if (result === 'saved') {
                    sent += item.minutes;
                }
            }
        } catch (error) {
            // Still offline or signed out; the next round tries again.
        } finally {
            retrying = false;
        }

        if (sent) {
            notify(fillText(chip.dataset.textSaved, sent), 'success');
            window.dispatchEvent(new CustomEvent('toolbox:focus-saved', { detail: { minutes: sent } }));
        }
    }

    function fillText(template, value) {
        return String(template || '').replace('{0}', value);
    }

    // The chime: three rising notes, played four times over about seven seconds so it is not
    // missed. Any click or key press stops it early.
    const CHIME_NOTES = [784, 988, 1175];
    const CHIME_ROUNDS = 4;
    const CHIME_ROUND_SECONDS = 1.8;
    let ringing = null;

    function stopChime() {
        if (!ringing) {
            return;
        }
        const audio = ringing;
        ringing = null;
        document.removeEventListener('pointerdown', stopChime, true);
        document.removeEventListener('keydown', stopChime, true);
        audio.close().catch(function () { /* already closed */ });
    }

    function chime() {
        const Context = window.AudioContext || window.webkitAudioContext;
        if (!Context) {
            return;
        }

        stopChime();
        try {
            const audio = new Context();
            const start = audio.currentTime + 0.05;
            for (let round = 0; round < CHIME_ROUNDS; round++) {
                CHIME_NOTES.forEach(function (frequency, index) {
                    const at = start + round * CHIME_ROUND_SECONDS + index * 0.32;
                    const length = index === CHIME_NOTES.length - 1 ? 0.9 : 0.4;
                    const tone = audio.createOscillator();
                    const gain = audio.createGain();
                    tone.type = 'sine';
                    tone.frequency.value = frequency;
                    gain.gain.setValueAtTime(0.0001, at);
                    gain.gain.exponentialRampToValueAtTime(0.3, at + 0.02);
                    gain.gain.exponentialRampToValueAtTime(0.0001, at + length);
                    tone.connect(gain).connect(audio.destination);
                    tone.start(at);
                    tone.stop(at + length + 0.02);
                });
            }

            ringing = audio;
            // Registered after this tick, so the click that started a test chime does not stop it.
            window.setTimeout(function () {
                if (ringing === audio) {
                    document.addEventListener('pointerdown', stopChime, true);
                    document.addEventListener('keydown', stopChime, true);
                }
            }, 0);
            window.setTimeout(function () {
                if (ringing === audio) {
                    stopChime();
                }
            }, (CHIME_ROUNDS * CHIME_ROUND_SECONDS + 1) * 1000);
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
        // Found finished long after the end (the browser was closed): no sound, no notice.
        const late = Date.now() - s.endsAt > LATE_MS;
        if (conf.sound && !late) {
            chime();
        }

        if (s.phase === 'focus') {
            const minutes = Math.round(s.durationMs / 60000);
            const cycle = (s.cycle || 0) + 1;
            const next = cycle % Math.max(1, conf.longEvery) === 0 ? 'long' : 'short';
            setState(Object.assign(idleState(next, cycle), { todoId: s.todoId, note: '' }));
            const message = chip.dataset.textFocusDone.replace('{0}', minutes).replace('{1}', minutesOf(next));
            if (!late) {
                browserNotice(message);
            }
            // Stored with the time it really ended, not the time this page noticed.
            if (await keepSession(sessionOf(s, minutes, s.endsAt)) === 'saved') {
                notify(message, 'success');
            }
        } else {
            const cycle = s.phase === 'long' ? 0 : s.cycle;
            setState(Object.assign(idleState('focus', cycle), { todoId: s.todoId, note: '' }));
            const message = chip.dataset.textBreakDone;
            if (!late) {
                browserNotice(message);
                notify(message, 'info');
            }
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

    // Back online: send what is waiting at once instead of at the next round.
    window.addEventListener('online', function () {
        write(RETRY_KEY, 0);
        retryUnsaved();
    });

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
        stopChime: stopChime,
        isRinging: function () { return ringing !== null; },
        unsavedCount: function () { return unsaved().length; },
        subscribe: function (fn) {
            listeners.add(fn);
            fn(state(), remaining(state()));
            return function () { listeners.delete(fn); };
        }
    };

    ensureTicking();
    render(state());
    retryUnsaved();
    window.setInterval(retryUnsaved, RETRY_EVERY_MS);
})(window, document);
