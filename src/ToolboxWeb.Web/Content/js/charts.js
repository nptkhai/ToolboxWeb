// Shared chart layer on Apache ECharts (vendor/echarts, Apache-2.0). Every chart in the app goes
// through here so it reads its colors from tokens.css, follows the light/dark theme, and resizes
// with its box. Load after echarts.min.js.
(function (window, document) {
    'use strict';

    const root = document.documentElement;
    const charts = new Set();

    // Base tokens are plain colors ECharts can read; derived tokens use color-mix(), so they are
    // resolved through a probe element into rgb().
    function color(name) {
        const probe = document.createElement('span');
        probe.style.color = 'var(' + name + ')';
        probe.style.display = 'none';
        document.body.appendChild(probe);
        const value = getComputedStyle(probe).color;
        probe.remove();
        return value;
    }

    function tokens() {
        return {
            text: color('--text'),
            muted: color('--muted'),
            line: color('--line'),
            surface: color('--surface'),
            surface2: color('--surface-2'),
            accent: color('--accent'),
            accentBrand: color('--accent-brand'),
            success: color('--success'),
            warning: color('--warning'),
            danger: color('--danger'),
            font: getComputedStyle(document.body).fontFamily,
            fontSize: 12
        };
    }

    // Defaults every chart shares: no animation flourish, token text, a calm grid.
    function base(t) {
        return {
            animationDuration: 300,
            textStyle: { color: t.text, fontFamily: t.font, fontSize: t.fontSize },
            grid: { left: 8, right: 8, top: 16, bottom: 4, containLabel: true },
            tooltip: {
                backgroundColor: t.surface,
                borderColor: t.line,
                textStyle: { color: t.text, fontFamily: t.font, fontSize: 13 },
                extraCssText: 'box-shadow: var(--shadow-popup); border-radius: 6px;'
            },
            aria: { enabled: true }
        };
    }

    /**
     * Draws a chart in el. build(tokens) returns the ECharts option; it runs again when the theme
     * changes, so colors never go stale. Returns { update(build), dispose() }.
     */
    function create(el, build) {
        if (!window.echarts || !el) {
            return null;
        }

        const instance = window.echarts.init(el, null, { renderer: 'svg' });
        const entry = { instance, build };
        const draw = function () {
            const t = tokens();
            instance.setOption(Object.assign(base(t), entry.build(t)), true);
        };

        const resizer = typeof ResizeObserver === 'function'
            ? new ResizeObserver(function () { instance.resize(); })
            : null;
        resizer?.observe(el);

        entry.draw = draw;
        charts.add(entry);
        draw();

        return {
            update: function (nextBuild) {
                entry.build = nextBuild || entry.build;
                draw();
            },
            dispose: function () {
                resizer?.disconnect();
                charts.delete(entry);
                instance.dispose();
            }
        };
    }

    function redrawAll() {
        charts.forEach(function (entry) { entry.draw(); });
    }

    new MutationObserver(redrawAll).observe(root, { attributes: true, attributeFilter: ['data-theme'] });
    window.matchMedia?.('(prefers-color-scheme: dark)').addEventListener?.('change', redrawAll);

    window.ToolboxCharts = { create, tokens };
})(window, document);
