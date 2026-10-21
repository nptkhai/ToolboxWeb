// SQL Server connection form shared by Schema compare and SQL capture
// (Views/Shared/_SqlConnectionForm.cshtml). Every query stays inside the form, so a page can
// hold several forms (source and target).
(function (window, document) {
    'use strict';

    function token(panel) {
        const input = panel.closest('form')?.querySelector('input[name="__RequestVerificationToken"]')
            || document.querySelector('input[name="__RequestVerificationToken"]');
        return input ? input.value : '';
    }

    function init(panel, options) {
        const settings = options || {};
        const $ = window.jQuery;
        const field = (name) => panel.querySelector('[data-field="' + name + '"]');
        const statusNode = panel.querySelector('[data-sqlconn-status]');
        const testButton = panel.querySelector('[data-sqlconn-test]');
        const databaseInput = field('database');
        let combo = null;

        function changed() {
            settings.onChange?.();
        }

        if (databaseInput && $ && $.fn && $.fn.kendoComboBox) {
            combo = $(databaseInput).kendoComboBox({
                dataSource: [],
                filter: 'contains',
                suggest: true,
                clearButton: true,
                change: changed
            }).data('kendoComboBox');
        }

        function setStatus(text, tone) {
            if (!statusNode) {
                return;
            }

            statusNode.textContent = text || '';
            statusNode.classList.remove('ok', 'error');
            if (tone) {
                statusNode.classList.add(tone);
            }
        }

        function read() {
            const value = function (name) {
                if (name === 'database' && combo) {
                    return String(combo.value() || combo.text() || '').trim();
                }

                const node = field(name);
                if (!node) {
                    return '';
                }

                return node.type === 'checkbox' ? node.checked : node.value.trim();
            };

            return {
                server: value('server'),
                database: value('database'),
                username: value('username'),
                password: value('password'),
                trustServerCertificate: value('trustServerCertificate'),
                encrypt: true
            };
        }

        function set(name, value) {
            const node = field(name);
            if (!node || value === null || value === undefined) {
                return;
            }

            if (node.type === 'checkbox') {
                node.checked = Boolean(value);
            } else if (name === 'database' && combo) {
                combo.value(String(value));
            } else {
                node.value = String(value);
            }
        }

        async function test() {
            const text = panel.dataset;
            settings.beforeTest?.();
            setStatus(text.msgTesting);
            if (testButton) {
                testButton.disabled = true;
            }

            try {
                const response = await fetch(text.testUrl, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': token(panel),
                        'X-Requested-With': 'XMLHttpRequest'
                    },
                    body: JSON.stringify(read())
                });
                if (!response.ok) {
                    throw new Error(response.status + ' ' + response.statusText);
                }

                const result = await response.json();
                if (!result.success) {
                    setStatus(result.message || text.msgNetwork, 'error');
                    return null;
                }

                const data = result.data || {};
                if (combo) {
                    const current = combo.value();
                    combo.setDataSource(data.databases || []);
                    if (current) {
                        combo.value(current);
                    }
                }

                // A page can replace the default "Connected" line, e.g. to explain a missing permission.
                const custom = settings.onTested?.(data);
                if (custom) {
                    setStatus(custom.text, custom.tone);
                } else {
                    setStatus((text.msgConnected || '')
                        .replace('{0}', data.serverVersion || '')
                        .replace('{1}', window.Toolbox?.format?.number((data.databases || []).length, 0) ?? (data.databases || []).length), 'ok');
                }

                return data;
            } catch (error) {
                setStatus(text.msgNetwork, 'error');
                return null;
            } finally {
                if (testButton) {
                    testButton.disabled = false;
                }
            }
        }

        panel.querySelectorAll('[data-field]').forEach(function (node) {
            if (node !== databaseInput || !combo) {
                node.addEventListener(node.type === 'checkbox' ? 'change' : 'input', changed);
            }
        });
        testButton?.addEventListener('click', test);

        return { panel, read, set, test, setStatus };
    }

    window.ToolboxSqlConnection = { init };
})(window, document);
