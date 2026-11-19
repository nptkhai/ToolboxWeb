// The "Export report" file of the text compare (Content/js/text-compare.js): one standalone
// HTML page that opens in any browser without Toolkit, prints well (or saves as PDF) and keeps
// the side-by-side view with the changed words marked. Long unchanged runs sit in a
// <details> block the reader can open.
//
// Colours are not written here: the light-theme custom properties of the app (tokens.css,
// the :root rule) are copied into the file when it is made, so the report follows the design
// tokens and the source holds no colour literals.
(function (window, document) {
    'use strict';

    function escapeHtml(value) {
        return String(value ?? '')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    // Every custom property declared on ":root" (light theme) in the page's own stylesheets.
    function lightTokens() {
        const values = {};
        Array.prototype.forEach.call(document.styleSheets, function (sheet) {
            let rules;
            try {
                rules = sheet.cssRules;
            } catch (error) {
                return;
            }

            Array.prototype.forEach.call(rules || [], function (rule) {
                if (rule.selectorText !== ':root' || !rule.style) {
                    return;
                }

                for (let i = 0; i < rule.style.length; i++) {
                    const name = rule.style[i];
                    if (name.indexOf('--') === 0) {
                        values[name] = rule.style.getPropertyValue(name).trim();
                    }
                }
            });
        });

        return Object.keys(values).map(function (name) { return name + ':' + values[name] + ';'; }).join('');
    }

    const STYLE = [
        '*{box-sizing:border-box}',
        'body{margin:0;background:var(--bg);color:var(--text);font-family:var(--font-sans);font-size:14px;line-height:1.5;-webkit-print-color-adjust:exact;print-color-adjust:exact}',
        'main{max-width:1400px;margin:0 auto;padding:24px 16px}',
        'h1{font-size:20px;font-weight:600;margin:0 0 4px}',
        '.meta{color:var(--muted);margin:0 0 16px}',
        '.card{background:var(--surface);border:1px solid var(--line);border-radius:8px;overflow:hidden;margin-bottom:16px}',
        '.summary{display:flex;flex-wrap:wrap;gap:8px;align-items:center;padding:12px 16px;border-bottom:1px solid var(--line)}',
        '.pill{border-radius:4px;font-size:12px;font-weight:600;padding:2px 8px}',
        '.pill.success{background:var(--success-tint);color:var(--success)}',
        '.pill.danger{background:var(--danger-tint);color:var(--danger)}',
        '.pill.warning{background:var(--warning-tint);color:var(--warning)}',
        '.names{display:grid;grid-template-columns:1fr 1fr;border-bottom:1px solid var(--line);background:var(--surface-2)}',
        '.names div{padding:8px 16px;font-weight:600;overflow-wrap:anywhere}',
        '.names div+div{border-left:1px solid var(--line)}',
        '.names small{color:var(--muted);font-weight:400;margin-left:8px}',
        '.dot{display:inline-block;width:8px;height:8px;border-radius:50%;margin-right:8px;vertical-align:middle}',
        '.dot.left{background:var(--danger)}.dot.right{background:var(--success)}',
        'table{width:100%;border-collapse:collapse;table-layout:fixed;font-family:var(--font-mono);font-size:12px}',
        'col.num{width:48px}col.sign{width:20px}',
        'td{padding:1px 6px;vertical-align:top;white-space:pre-wrap;overflow-wrap:anywhere}',
        'td.num{color:var(--muted);text-align:right;user-select:none}',
        'td.sign{text-align:center;color:var(--muted);user-select:none}',
        'td.code.is-left{border-right:1px solid var(--line)}',
        '.is-del,.is-old{background:var(--danger-tint)}',
        '.is-add,.is-new{background:var(--success-tint)}',
        'td.sign.is-del,td.sign.is-old{color:var(--danger)}td.sign.is-add,td.sign.is-new{color:var(--success)}',
        'td.sign.is-old,td.sign.is-new{color:var(--warning)}',
        '.is-empty{background:var(--surface-2)}',
        '.is-old .tc-mark{background:color-mix(in srgb,var(--danger) 26%,var(--surface));border-radius:2px}',
        '.is-new .tc-mark{background:color-mix(in srgb,var(--success) 26%,var(--surface));border-radius:2px}',
        'details{border-top:1px solid var(--line);border-bottom:1px solid var(--line)}',
        'summary{cursor:pointer;background:var(--surface-2);color:var(--muted);font-size:12px;padding:4px 16px;text-align:center}',
        '.identical{padding:24px 16px;color:var(--success);font-weight:600}',
        'footer{color:var(--muted);font-size:12px}',
        '.hljs-keyword,.hljs-selector-tag,.hljs-built_in,.hljs-literal,.hljs-meta .hljs-keyword{color:var(--syntax-keyword)}',
        '.hljs-string,.hljs-regexp,.hljs-template-string,.hljs-symbol{color:var(--syntax-string)}',
        '.hljs-comment,.hljs-quote,.hljs-meta{color:var(--syntax-comment)}',
        '.hljs-number,.hljs-variable.constant_{color:var(--syntax-number)}',
        '.hljs-type,.hljs-title.class_,.hljs-class .hljs-title{color:var(--syntax-type)}',
        '.hljs-attr,.hljs-attribute,.hljs-name,.hljs-tag,.hljs-selector-class,.hljs-selector-id,.hljs-property{color:var(--syntax-attr)}',
        '.hljs-title,.hljs-title.function_,.hljs-section{color:var(--syntax-title)}',
        '.hljs-emphasis{font-style:italic}.hljs-strong{font-weight:600}',
        '@media print{main{padding:0}details:not([open]){display:none}.card{border:0}}'
    ].join('\n');

    function cellHtml(c, side) {
        return '<td class="num ' + c.cls + '">' + c.num + '</td>'
            + '<td class="sign ' + c.cls + '">' + escapeHtml(c.sign) + '</td>'
            + '<td class="code ' + c.cls + ' is-' + side + '">' + (c.html || ' ') + '</td>';
    }

    function tableHtml(rows) {
        return '<table><colgroup><col class="num"><col class="sign"><col><col class="num"><col class="sign"><col></colgroup><tbody>'
            + rows.map(function (row) { return '<tr>' + cellHtml(row.left, 'left') + cellHtml(row.right, 'right') + '</tr>'; }).join('')
            + '</tbody></table>';
    }

    function build(model) {
        const sideHtml = function (side, key) {
            return '<div><span class="dot ' + key + '"></span>' + escapeHtml(side.name)
                + '<small>' + escapeHtml(side.role) + ' · ' + escapeHtml(side.count) + '</small></div>';
        };

        const body = model.identical
            ? '<div class="identical">' + escapeHtml(model.identical) + '</div>'
            : model.chunks.map(function (chunk) {
                return chunk.fold
                    ? '<details><summary>' + escapeHtml(chunk.fold) + '</summary>' + tableHtml(chunk.rows) + '</details>'
                    : tableHtml(chunk.rows);
            }).join('');

        return '<!doctype html>\n<html lang="' + escapeHtml(model.lang) + '"><head><meta charset="utf-8">'
            + '<meta name="viewport" content="width=device-width, initial-scale=1">'
            + '<title>' + escapeHtml(model.title + ': ' + model.left.name + ' / ' + model.right.name) + '</title>'
            + '<style>:root{' + lightTokens() + '}\n' + STYLE + '</style></head><body><main>'
            + '<h1>' + escapeHtml(model.title) + '</h1>'
            + '<p class="meta">' + escapeHtml(model.created) + '</p>'
            + '<div class="card"><div class="summary">'
            + model.stats.map(function (s) { return '<span class="pill ' + s.tone + '">' + escapeHtml(s.label) + '</span>'; }).join('')
            + '</div><div class="names">' + sideHtml(model.left, 'left') + sideHtml(model.right, 'right') + '</div>'
            + body + '</div>'
            + '<footer>' + escapeHtml(model.footer) + '</footer>'
            + '</main></body></html>';
    }

    // Builds the file and hands it to the browser as a download; returns the file name.
    function download(model) {
        const blob = new Blob([build(model)], { type: 'text/html;charset=utf-8' });
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = model.fileName;
        document.body.appendChild(link);
        link.click();
        link.remove();
        window.setTimeout(function () { URL.revokeObjectURL(url); }, 10000);
        return model.fileName;
    }

    window.ToolboxTextCompareReport = { build: build, download: download };
})(window, document);
