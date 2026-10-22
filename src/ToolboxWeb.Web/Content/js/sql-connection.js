// SQL Server connection form shared by Schema compare and SQL capture
// (Views/Shared/_SqlConnectionForm.cshtml). Every query stays inside the form, so a page can
// hold several forms (source and target).
// Options: onChange, beforeTest, onTested(data) -> { text, tone } | undefined, and recent: true
// to offer the connections tested before (server, account, database; never the password).
(function (window, document) {
    'use strict';

    // Shared by every form and page: a server used as the source today is often tomorrow's target.
    const RECENT_KEY = 'toolbox.sqlConnection.recent.v1';
    const RECENT_MAX = 6;

    function readRecent() {
        try {
            const list = JSON.parse(window.localStorage.getItem(RECENT_KEY) || '[]');
            return Array.isArray(list) ? list.filter(function (entry) { return entry && entry.server; }) : [];
        } catch (error) {
            return [];
        }
    }

    function writeRecent(list) {
        try {
            window.localStorage.setItem(RECENT_KEY, JSON.stringify(list.slice(0, RECENT_MAX)));
        } catch (error) {
            // Storage can be off (private window); the list is a convenience only.
        }

        // Every form on the page shows the same list, so they all redraw.
        document.dispatchEvent(new CustomEvent('toolbox:sqlconn-recent'));
    }

    function recentKey(entry) {
        return [entry.server, entry.username, entry.database].map(function (part) {
            return String(part || '').trim().toLowerCase();
        }).join('|');
    }

    function escapeHtml(value) {
        return String(value === null || value === undefined ? '' : value)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
    }

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
        const trustInput = field('trustServerCertificate');
        const trustWarning = panel.querySelector('[data-sqlconn-trust-warning]');
        const recentBox = panel.querySelector('[data-sqlconn-recent]');
        const recentList = panel.querySelector('[data-sqlconn-recent-list]');
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
                syncTrustWarning();
            } else if (name === 'database' && combo) {
                combo.value(String(value));
            } else {
                node.value = String(value);
            }
        }

        // Trusting the certificate skips the check that the server is the real one, so the
        // risk is spelled out next to the box whenever it is ticked.
        function syncTrustWarning() {
            if (trustWarning && trustInput) {
                trustWarning.hidden = !trustInput.checked;
            }
        }

        function renderRecent() {
            if (!settings.recent || !recentBox || !recentList) {
                return;
            }

            const list = readRecent();
            recentBox.hidden = list.length === 0;
            const removeText = recentList.dataset.msgRemove || '';
            const icon = window.Toolbox?.icon ? window.Toolbox.icon('x') : '&times;';
            recentList.innerHTML = list.map(function (entry, index) {
                const name = entry.server + (entry.database ? ' / ' + entry.database : '');
                return '<li class="sqlconn-recent-item">'
                    + '<button type="button" class="dropdown-item" data-recent-pick="' + index + '">'
                    + '<span class="sqlconn-recent-name">' + escapeHtml(name) + '</span>'
                    + '<span class="sqlconn-recent-meta">' + escapeHtml(entry.username || '') + '</span>'
                    + '</button>'
                    + '<button type="button" class="btn btn-sm tbx-icon-button" data-recent-remove="' + index + '"'
                    + ' aria-label="' + escapeHtml(removeText + ' ' + name) + '" title="' + escapeHtml(removeText) + '">'
                    + icon + '</button></li>';
            }).join('');
        }

        function remember() {
            if (!settings.recent) {
                return;
            }

            const current = read();
            if (!current.server) {
                return;
            }

            const entry = {
                server: current.server,
                username: current.username,
                database: current.database,
                trustServerCertificate: current.trustServerCertificate
            };
            const key = recentKey(entry);
            // A test without a database followed by one with it is the same connection.
            const loose = recentKey({ server: entry.server, username: entry.username, database: '' });
            const list = readRecent().filter(function (other) {
                const otherKey = recentKey(other);
                return otherKey !== key && (entry.database === '' || otherKey !== loose);
            });
            list.unshift(entry);
            writeRecent(list);
        }

        function pickRecent(entry) {
            const before = read();
            set('server', entry.server);
            set('username', entry.username || '');
            set('database', entry.database || '');
            set('trustServerCertificate', Boolean(entry.trustServerCertificate));
            // The password is never stored; keep it only when it still belongs to the same login.
            if (before.server.toLowerCase() !== String(entry.server).toLowerCase()
                || before.username.toLowerCase() !== String(entry.username || '').toLowerCase()) {
                set('password', '');
            }
            setStatus('');
            changed();
            field('password')?.focus();
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
                remember();
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
        trustInput?.addEventListener('change', syncTrustWarning);
        syncTrustWarning();

        recentList?.addEventListener('click', function (event) {
            const pick = event.target.closest('[data-recent-pick]');
            const remove = event.target.closest('[data-recent-remove]');
            const list = readRecent();
            if (pick && list[pick.dataset.recentPick]) {
                pickRecent(list[pick.dataset.recentPick]);
            } else if (remove) {
                // Removing an entry keeps the menu open so several can go in a row.
                event.stopPropagation();
                list.splice(Number(remove.dataset.recentRemove), 1);
                writeRecent(list);
            }
        });
        document.addEventListener('toolbox:sqlconn-recent', renderRecent);
        renderRecent();

        // For a page that swaps two forms: the database list and the status line move with the values.
        function databases() {
            return combo ? combo.dataSource.data().toJSON() : [];
        }

        function setDatabases(list) {
            if (combo) {
                const current = combo.value();
                combo.setDataSource(list || []);
                combo.value(current);
            }
        }

        function status() {
            if (!statusNode) {
                return { text: '', tone: null };
            }

            const tone = statusNode.classList.contains('ok') ? 'ok' : (statusNode.classList.contains('error') ? 'error' : null);
            return { text: statusNode.textContent, tone: tone };
        }

        return { panel, read, set, test, setStatus, status, remember, databases, setDatabases };
    }

    window.ToolboxSqlConnection = { init };
})(window, document);
