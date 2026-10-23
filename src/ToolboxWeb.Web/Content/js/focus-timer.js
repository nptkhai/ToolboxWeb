// Focus timer page (Views/FocusTimer/Index.cshtml). The countdown is window.ToolboxFocus from
// Content/js/focus-clock.js; this file draws it as a ring, sets the phase and length, links
// the session to a to-do and a note, keeps the options, shows today's goal and the 7-day
// chart, and handles stop-early, note editing and the keyboard.
(function (window, document) {
    'use strict';

    const page = document.querySelector('[data-focus-page]');
    const focus = window.ToolboxFocus;
    if (!page || !focus) {
        return;
    }

    const text = page.dataset;
    const $ = window.jQuery;
    const bar = page.querySelector('[data-focus-ring-bar]');
    const RADIUS = 90;
    const CIRCUMFERENCE = 2 * Math.PI * RADIUS;
    bar.style.strokeDasharray = String(CIRCUMFERENCE);

    function readJson(id) {
        try {
            return JSON.parse(document.getElementById(id)?.textContent || 'null');
        } catch (error) {
            return null;
        }
    }

    function format(template) {
        const args = Array.prototype.slice.call(arguments, 1);
        return String(template || '').replace(/\{(\d+)\}/g, function (match, index) {
            return args[index] !== undefined ? args[index] : match;
        });
    }

    function number(value) {
        return window.Toolbox?.format?.number ? window.Toolbox.format.number(value, 0) : String(value);
    }

    function isTyping(target) {
        return target instanceof HTMLElement
            && (target.isContentEditable || /^(INPUT|TEXTAREA|SELECT)$/.test(target.tagName) || target.closest('.k-input'));
    }

    // ------------------------------------------------------------ controls

    const minutesInput = page.querySelector('[data-focus-minutes]');
    let minutesBox = null;
    if ($ && $.fn && $.fn.kendoNumericTextBox) {
        minutesBox = $(minutesInput).kendoNumericTextBox({
            min: 1, max: 240, step: 5, format: 'n0', decimals: 0,
            change: function () { focus.setMinutes(this.value() || 1); }
        }).data('kendoNumericTextBox');
    } else {
        minutesInput.addEventListener('change', function () { focus.setMinutes(minutesInput.value); });
    }

    function setMinutesField(value) {
        if (minutesBox) {
            minutesBox.value(value);
        } else {
            minutesInput.value = value;
        }
    }

    // The to-do picker lists open to-dos (Kendo ComboBox); the note is free text.
    const todos = readJson('focus-todo-data') || [];
    const todoInput = page.querySelector('[data-focus-todo]');
    const noteInput = page.querySelector('[data-focus-note]');
    let todoBox = null;
    if ($ && $.fn && $.fn.kendoComboBox) {
        todoBox = $(todoInput).kendoComboBox({
            dataSource: todos,
            dataTextField: 'title',
            dataValueField: 'id',
            filter: 'contains',
            suggest: true,
            clearButton: true,
            change: saveLink
        }).data('kendoComboBox');
    }

    function selectedTodo() {
        if (!todoBox) {
            return null;
        }
        const item = todoBox.dataItem();
        return item ? item.id : null;
    }

    function saveLink() {
        focus.setLink(selectedTodo(), noteInput.value);
    }

    noteInput.addEventListener('input', saveLink);

    // ------------------------------------------------------------ drawing

    const toggleButton = page.querySelector('[data-focus-toggle]');
    const stopButton = page.querySelector('[data-focus-stop]');
    let lastPhase = null;

    function phaseName(phase) {
        return page.querySelector('[data-phase="' + phase + '"]').textContent.trim();
    }

    function draw(s, left) {
        const progress = s.durationMs > 0 ? 1 - left / s.durationMs : 0;
        bar.style.strokeDashoffset = String(CIRCUMFERENCE * (1 - Math.min(1, Math.max(0, progress))));
        page.querySelector('[data-focus-time]').textContent = focus.clock(left);
        page.querySelector('[data-focus-phase-label]').textContent = phaseName(s.phase);
        // "Session 2 of 4": the focus session running now, or the one just finished during a break.
        const every = Math.max(1, Number(focus.settings().longEvery) || 4);
        const done = (s.cycle || 0) % every;
        const position = s.phase === 'focus' ? done + 1 : (done || every);
        page.querySelector('[data-focus-cycle]').textContent = format(text.textSession, number(position), number(every));

        page.classList.toggle('is-break', s.phase !== 'focus');
        page.classList.toggle('is-running', s.status === 'running');

        page.querySelectorAll('[data-phase]').forEach(function (button) {
            button.setAttribute('aria-pressed', button.dataset.phase === s.phase ? 'true' : 'false');
            button.disabled = s.status !== 'idle' && button.dataset.phase !== s.phase;
        });

        const label = s.status === 'running' ? text.textPause
            : s.status === 'paused' ? text.textResume
            : s.phase === 'focus' ? text.textStart : text.textStartBreak;
        toggleButton.querySelector('[data-focus-toggle-text]').textContent = label;
        toggleButton.classList.toggle('is-running', s.status === 'running');
        stopButton.hidden = s.phase !== 'focus' || s.status === 'idle';

        // Length and presets belong to a session that has not started.
        const idle = s.status === 'idle';
        const minutes = focus.minutesOf(s.phase);
        page.querySelectorAll('[data-preset]').forEach(function (button) {
            button.disabled = !idle || s.phase !== 'focus';
            button.setAttribute('aria-pressed', s.phase === 'focus' && Number(button.dataset.preset) === minutes ? 'true' : 'false');
        });
        if (minutesBox) {
            minutesBox.enable(idle);
        } else {
            minutesInput.disabled = !idle;
        }
        if (lastPhase !== s.phase + s.status) {
            setMinutesField(minutes);
            lastPhase = s.phase + s.status;
        }
    }

    // ------------------------------------------------------------ goal and chart

    function drawGoal() {
        const goal = Number(focus.settings().goal) || 0;
        const done = Number(text.todayMinutes) || 0;
        const box = page.querySelector('[data-focus-goal]');
        box.hidden = goal <= 0;
        if (goal <= 0) {
            return;
        }
        const ratio = Math.min(1, done / goal);
        page.querySelector('[data-focus-goal-fill]').style.width = (ratio * 100).toFixed(0) + '%';
        page.querySelector('[data-focus-goal-text]').textContent = done >= goal
            ? format(text.textGoalDone, number(goal))
            : format(text.textGoal, number(done), number(goal));
        box.classList.toggle('is-done', done >= goal);
    }

    let chart = null;
    const chartHost = page.querySelector('[data-focus-chart]');
    const chartLabel = chartHost ? chartHost.getAttribute('aria-label') : '';
    function drawChart() {
        const host = chartHost;
        const days = readJson('focus-chart-data') || [];
        if (!window.ToolboxCharts || !host) {
            return;
        }

        const build = function (t) {
            const goal = Number(focus.settings().goal) || 0;
            // Screen readers get the label and the minutes per day instead of ECharts' English text.
            const description = chartLabel + ': ' + days.map(function (d) {
                return d.label + ' ' + format(text.textMinutes, number(d.minutes));
            }).join(', ');
            return {
                aria: { enabled: true, label: { description: description } },
                grid: { left: 4, right: 4, top: 20, bottom: 4, containLabel: true },
                xAxis: {
                    type: 'category',
                    data: days.map(function (d) { return d.label; }),
                    axisLine: { lineStyle: { color: t.line } },
                    axisTick: { show: false },
                    axisLabel: { color: t.muted }
                },
                yAxis: {
                    type: 'value',
                    minInterval: 15,
                    // Room for the goal line even on days far below it.
                    max: function (range) { return Math.max(range.max, goal); },
                    splitLine: { lineStyle: { color: t.line } },
                    axisLabel: { color: t.muted }
                },
                tooltip: {
                    trigger: 'axis',
                    valueFormatter: function (value) { return format(text.textMinutes, number(value)); }
                },
                series: [{
                    type: 'bar',
                    data: days.map(function (d) { return d.minutes; }),
                    barMaxWidth: 28,
                    itemStyle: { color: t.accentBrand, borderRadius: [4, 4, 0, 0] },
                    markLine: goal > 0 ? {
                        symbol: 'none',
                        silent: true,
                        label: { show: false },
                        lineStyle: { color: t.success, type: 'dashed' },
                        data: [{ yAxis: goal }]
                    } : undefined
                }]
            };
        };

        if (chart) {
            chart.update(build);
        } else {
            chart = window.ToolboxCharts.create(host, build);
        }
    }

    // ------------------------------------------------------------ options

    function fillOptions() {
        const s = focus.settings();
        page.querySelectorAll('[data-setting]').forEach(function (input) {
            const value = s[input.dataset.setting];
            if (input.type === 'checkbox') {
                input.checked = Boolean(value);
            } else {
                input.value = value;
            }
        });
        updateNotifyHint();
    }

    function updateNotifyHint() {
        const hint = page.querySelector('[data-focus-notify-hint]');
        if ('Notification' in window && Notification.permission === 'denied') {
            hint.textContent = text.textNotifyBlocked;
        }
    }

    page.querySelectorAll('[data-setting]').forEach(function (input) {
        input.addEventListener('change', async function () {
            const changes = {};
            if (input.type === 'checkbox') {
                changes[input.dataset.setting] = input.checked;
                // The browser asks once; a refusal turns the box back off.
                if (input.dataset.setting === 'notify' && input.checked && 'Notification' in window && Notification.permission !== 'granted') {
                    const answer = await Notification.requestPermission();
                    if (answer !== 'granted') {
                        input.checked = false;
                        changes.notify = false;
                        updateNotifyHint();
                    }
                }
            } else {
                const value = Math.round(Number(input.value));
                const min = Number(input.min || 0);
                const max = Number(input.max || 999);
                changes[input.dataset.setting] = Math.min(max, Math.max(min, Number.isFinite(value) ? value : min));
                input.value = changes[input.dataset.setting];
            }
            focus.saveSettings(changes);
            const s = focus.state();
            if (s.status === 'idle' && (input.dataset.setting === 'short' || input.dataset.setting === 'long')) {
                focus.setPhase(s.phase);
            }
            drawGoal();
            drawChart();
        });
    });

    page.querySelector('[data-focus-test-sound]').addEventListener('click', focus.chime);

    // ------------------------------------------------------------ actions

    toggleButton.addEventListener('click', function () {
        saveLink();
        focus.toggle();
    });

    page.querySelector('[data-focus-reset]').addEventListener('click', focus.reset);

    page.querySelectorAll('[data-phase]').forEach(function (button) {
        button.addEventListener('click', function () {
            if (focus.state().status === 'idle') {
                focus.setPhase(button.dataset.phase);
            }
        });
    });

    page.querySelectorAll('[data-preset]').forEach(function (button) {
        button.addEventListener('click', function () {
            focus.setMinutes(Number(button.dataset.preset));
            setMinutesField(Number(button.dataset.preset));
        });
    });

    // The dialogs live outside the content area so its stacking does not put them under the backdrop.
    const stopModalElement = document.querySelector('[data-focus-stop-modal]');
    const noteModalElement = document.getElementById('focus-note-modal');
    document.body.append(stopModalElement, noteModalElement);

    // Stopping early asks what to do with the minutes already spent.
    const stopModal = window.bootstrap ? window.bootstrap.Modal.getOrCreateInstance(stopModalElement) : null;
    stopButton.addEventListener('click', function () {
        const minutes = focus.spentMinutes();
        const paragraph = stopModalElement.querySelector('[data-focus-stop-text]');
        const save = stopModalElement.querySelector('[data-focus-save-stop]');
        paragraph.textContent = minutes >= 1
            ? format(paragraph.dataset.template, number(minutes))
            : paragraph.dataset.templateShort;
        save.hidden = minutes < 1;
        save.textContent = format(save.dataset.template, number(minutes));
        stopModal?.show();
    });

    stopModalElement.querySelector('[data-focus-save-stop]').addEventListener('click', async function () {
        stopModal?.hide();
        // "queued": not stored yet, kept in the browser and sent again later (a notice says so).
        if (await focus.stop(true) === 'saved') {
            window.location.reload();
        }
    });

    stopModalElement.querySelector('[data-focus-discard]').addEventListener('click', function () {
        stopModal?.hide();
        focus.stop(false);
    });

    // A session saved on this page (when it ran out) shows up in the numbers and the history.
    // The reload waits for the chime, which a reload would cut off.
    window.addEventListener('toolbox:focus-saved', function () {
        const reloadWhenQuiet = function () {
            if (focus.isRinging()) {
                window.setTimeout(reloadWhenQuiet, 500);
            } else {
                window.location.reload();
            }
        };
        window.setTimeout(reloadWhenQuiet, 1500);
    });

    // Note editing in the history.
    page.addEventListener('click', function (event) {
        const button = event.target.closest('[data-focus-edit-note]');
        if (!button) {
            return;
        }
        noteModalElement.querySelector('[data-note-id]').value = button.dataset.id;
        noteModalElement.querySelector('[data-note-text]').value = button.dataset.note || '';
        window.bootstrap.Modal.getOrCreateInstance(noteModalElement).show();
    });
    noteModalElement.addEventListener('shown.bs.modal', function () {
        noteModalElement.querySelector('[data-note-text]').focus();
    });

    // Space starts or pauses, R resets.
    document.addEventListener('keydown', function (event) {
        if (event.ctrlKey || event.metaKey || event.altKey || isTyping(event.target) || document.querySelector('.modal.show')) {
            return;
        }
        if (event.key === ' ' && !(event.target instanceof HTMLButtonElement)) {
            event.preventDefault();
            toggleButton.click();
        } else if (event.key === 'r' || event.key === 'R') {
            event.preventDefault();
            focus.reset();
        }
    });

    // ------------------------------------------------------------ start

    const s0 = focus.state();
    if (todoBox && s0.todoId) {
        todoBox.value(String(s0.todoId));
    }
    noteInput.value = s0.note || '';
    fillOptions();
    drawGoal();
    drawChart();
    focus.subscribe(draw);

    // Opened from "Start 25 minutes" on the overview: set the length and start once, unless a
    // session is already going; then drop the flags so a reload does not start it again.
    if (page.dataset.autostart === 'true' || page.dataset.requestedMinutes) {
        const current = focus.state();
        if (current.status === 'idle') {
            if (current.phase !== 'focus') {
                focus.setPhase('focus');
            }
            if (page.dataset.requestedMinutes) {
                focus.setMinutes(Number(page.dataset.requestedMinutes));
            }
            if (page.dataset.autostart === 'true') {
                focus.start();
            }
        }
        const url = new URL(window.location.href);
        url.searchParams.delete('start');
        url.searchParams.delete('minutes');
        window.history.replaceState(null, '', url.pathname + url.search + url.hash);
    }
})(window, document);
