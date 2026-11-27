(function () {
    const root = document.documentElement;
    const shell = document.querySelector('[data-shell]');
    const sidebar = document.getElementById('app-sidebar');
    const sidebarToggle = document.querySelector('[data-sidebar-toggle]');
    const narrowScreen = window.matchMedia('(max-width: 899.98px)');

    function store(key, value) {
        try {
            if (value === null) {
                localStorage.removeItem(key);
            } else {
                localStorage.setItem(key, value);
            }
        } catch {
            // Settings still apply for this page when storage is unavailable.
        }
    }

    function read(key) {
        try {
            return localStorage.getItem(key);
        } catch {
            return null;
        }
    }

    // Sidebar: collapses to icons on wide screens, slides in as a drawer on narrow screens.
    function syncSidebarState() {
        const collapsed = root.dataset.sidebar === 'collapsed';
        const drawerOpen = shell?.classList.contains('sidebar-open');
        sidebarToggle?.setAttribute('aria-expanded', String(narrowScreen.matches ? !!drawerOpen : !collapsed));
        // Icon-only links get a tooltip with their name.
        sidebar?.querySelectorAll('.side-nav-link').forEach(function (link) {
            if (collapsed && !narrowScreen.matches) {
                link.title = link.querySelector('.nav-text')?.textContent.trim() || '';
            } else {
                link.removeAttribute('title');
            }
        });
    }

    function openDrawer() {
        shell?.classList.add('sidebar-open');
        syncSidebarState();
        (sidebar?.querySelector('.side-nav-link.active') || sidebar?.querySelector('.side-nav-link'))?.focus();
    }

    function closeDrawer(returnFocus) {
        if (!shell?.classList.contains('sidebar-open')) {
            return;
        }

        shell.classList.remove('sidebar-open');
        syncSidebarState();
        if (returnFocus) {
            sidebarToggle?.focus();
        }
    }

    sidebarToggle?.addEventListener('click', function () {
        if (narrowScreen.matches) {
            if (shell?.classList.contains('sidebar-open')) {
                closeDrawer(false);
            } else {
                openDrawer();
            }
            return;
        }

        const collapsed = root.dataset.sidebar !== 'collapsed';
        if (collapsed) {
            root.dataset.sidebar = 'collapsed';
        } else {
            delete root.dataset.sidebar;
        }
        store('toolbox.sidebar', collapsed ? 'collapsed' : 'expanded');
        syncSidebarState();
    });

    document.querySelectorAll('[data-sidebar-close]').forEach(function (element) {
        element.addEventListener('click', function () { closeDrawer(true); });
    });

    document.addEventListener('keydown', function (event) {
        if (event.key === 'Escape') {
            closeDrawer(true);
        }
    });

    narrowScreen.addEventListener('change', function () {
        closeDrawer(false);
        syncSidebarState();
    });
    syncSidebarState();

    // A missing avatar file falls back to the initials rendered underneath the image.
    document.querySelectorAll('img[data-avatar-image]').forEach(function (img) {
        function dropImage() { img.remove(); }
        if (img.complete && img.naturalWidth === 0) {
            dropImage();
        } else {
            img.addEventListener('error', dropImage);
        }
    });

    // Theme: light, dark, or "system" (no stored choice; follows the OS and its changes).
    const systemDark = window.matchMedia('(prefers-color-scheme: dark)');

    function applyTheme() {
        const choice = read('toolbox.theme');
        const theme = choice === 'light' || choice === 'dark' ? choice : (systemDark.matches ? 'dark' : 'light');
        root.dataset.theme = theme;
        document.querySelectorAll('[data-theme-set]').forEach(function (button) {
            const value = button.getAttribute('data-theme-set');
            const pressed = value === 'system' ? choice !== 'light' && choice !== 'dark' : value === choice;
            button.setAttribute('aria-pressed', String(pressed));
        });
    }

    document.querySelectorAll('[data-theme-set]').forEach(function (button) {
        button.addEventListener('click', function () {
            const value = button.getAttribute('data-theme-set');
            store('toolbox.theme', value === 'system' ? null : value);
            applyTheme();
        });
    });
    // Before sign-in there is no avatar menu: one button flips between light and dark.
    document.querySelectorAll('[data-theme-toggle]').forEach(function (button) {
        button.addEventListener('click', function () {
            store('toolbox.theme', root.dataset.theme === 'dark' ? 'light' : 'dark');
            applyTheme();
        });
    });
    systemDark.addEventListener('change', applyTheme);
    applyTheme();

    // Show/hide password: a [data-password-toggle] button inside a .tbx-password wrapper
    // (Views/Shared/_PasswordToggle.cshtml) flips the input between password and text.
    document.addEventListener('click', function (event) {
        const button = event.target.closest('[data-password-toggle]');
        const input = button?.closest('.tbx-password')?.querySelector('input');
        if (!input) {
            return;
        }

        const show = input.type === 'password';
        input.type = show ? 'text' : 'password';
        button.setAttribute('aria-pressed', String(show));
        button.setAttribute('aria-label', show ? button.dataset.labelHide : button.dataset.labelShow);
        input.focus();
    });

    // Display settings on the account page (stored per browser).
    document.querySelectorAll('[data-display-setting]').forEach(function (input) {
        const key = input.getAttribute('data-display-setting');
        input.checked = read(key) === 'on';
        input.addEventListener('change', function () {
            store(key, input.checked ? 'on' : null);
            document.dispatchEvent(new CustomEvent('toolbox:background-changed'));
        });
    });

    document.querySelectorAll('[data-copy-target]').forEach(function (button) {
        button.addEventListener('click', async function () {
            const target = document.querySelector(button.getAttribute('data-copy-target'));
            if (!target) return;
            await navigator.clipboard.writeText(target.textContent || '');
            // A button with an icon keeps it and swaps only its [data-copy-label] text.
            const label = button.querySelector('[data-copy-label]') || button;
            const original = button.dataset.defaultText || label.textContent || '';
            const copiedText = button.dataset.copiedText || 'Copied';
            label.textContent = copiedText;
            setTimeout(function () { label.textContent = original || 'Copy'; }, 1200);
        });
    });

    function parseJson(value, fallback) {
        if (!value) {
            return fallback;
        }

        try {
            return JSON.parse(value);
        } catch {
            return fallback;
        }
    }

    function formatFileSize(bytes) {
        const size = Number(bytes || 0);
        if (!Number.isFinite(size) || size <= 0) {
            return '0 KB';
        }

        if (size < 1024) {
            return `${size} B`;
        }

        if (size < 1024 * 1024) {
            return `${(size / 1024).toFixed(size < 10240 ? 1 : 0)} KB`;
        }

        return `${(size / (1024 * 1024)).toFixed(size < 10 * 1024 * 1024 ? 1 : 0)} MB`;
    }

    function normalizeExtension(value) {
        const extension = String(value || '').trim().toLowerCase();
        if (!extension) {
            return '';
        }

        return extension.startsWith('.') ? extension : `.${extension}`;
    }

    function parseExtensionList(value) {
        return parseJson(value, [])
            .map(normalizeExtension)
            .filter(function (extension, index, items) {
                return !!extension && items.indexOf(extension) === index;
            });
    }

    function getFileNameExtension(name) {
        const fileName = String(name || '').trim();
        const lastDot = fileName.lastIndexOf('.');
        if (lastDot < 0) {
            return '';
        }

        return normalizeExtension(fileName.slice(lastDot));
    }

    function getFileExtension(name, type) {
        const fileName = String(name || '').trim();
        const parts = fileName.split('.');
        if (parts.length > 1) {
            return parts.pop().slice(0, 4).toUpperCase();
        }

        if (typeof type === 'string' && type.includes('/')) {
            return type.split('/').pop().slice(0, 4).toUpperCase();
        }

        return 'FILE';
    }

    function getFileIconName(name, type) {
        const extension = String(name || '')
            .split('.')
            .pop()
            .toLowerCase();

        switch (extension) {
            case 'pdf':
                return 'file-earmark-pdf';
            case 'doc':
            case 'docx':
                return 'file-earmark-word';
            case 'xls':
            case 'xlsx':
                return 'file-earmark-excel';
            case 'ppt':
            case 'pptx':
                return 'file-earmark-slides';
            case 'zip':
            case 'rar':
            case '7z':
                return 'file-earmark-zip';
            case 'jpg':
            case 'jpeg':
            case 'png':
            case 'gif':
            case 'webp':
            case 'bmp':
            case 'svg':
                return 'file-earmark-image';
            default:
                if (String(type || '').startsWith('image/')) {
                    return 'file-earmark-image';
                }

                return 'file-earmark';
        }
    }

    function getFileIconTone(iconName) {
        switch (iconName) {
            case 'file-earmark-pdf':
                return 'is-pdf';
            case 'file-earmark-word':
                return 'is-word';
            case 'file-earmark-excel':
                return 'is-excel';
            case 'file-earmark-slides':
                return 'is-slides';
            case 'file-earmark-zip':
                return 'is-zip';
            case 'file-earmark-image':
                return 'is-image';
            default:
                return 'is-default';
        }
    }

    function createUploadControl(root) {
        if (!root || root.dataset.uploadReady === 'true') {
            return null;
        }

        const input = root.querySelector('.tbx-upload-input');
        if (!input) {
            return null;
        }

        const uploadKind = root.dataset.uploadKind === 'image' ? 'image' : 'files';
        const isImage = uploadKind === 'image';
        const isMultiple = !isImage && input.hasAttribute('multiple');
        const listEl = root.querySelector('[data-upload-list]');
        const listWrapperEl = root.querySelector('[data-upload-list-wrapper]');
        const previewEl = root.querySelector('[data-upload-preview]');
        const previewWrapperEl = root.querySelector('[data-upload-preview-wrapper]');
        const dropzones = root.querySelectorAll('[data-upload-dropzone]');
        const triggers = root.querySelectorAll('[data-upload-trigger]');
        const clearTriggers = root.querySelectorAll('[data-upload-clear]');
        const clearAllTriggers = root.querySelectorAll('[data-upload-clear-all]');
        const removedInput = root.querySelector('.tbx-upload-removed');
        const feedbackEl = root.querySelector('[data-upload-feedback]');
        const iconSpritePath = root.dataset.iconSprite || '';
        const removeLabel = root.dataset.removeLabel || 'Remove';
        const emptyText = root.dataset.emptyText || 'No files selected.';
        const invalidTypeMessage = root.dataset.invalidTypeMessage || 'Invalid file type.';
        const allowedExtensions = parseExtensionList(root.dataset.allowedExtensions);
        const imagePlaceholderTitle = root.dataset.imagePlaceholderTitle || 'Choose image';
        const removedItems = [];

        let sequence = 0;
        let stateItems = parseJson(root.dataset.initialItems, []).map(function (item) {
            sequence += 1;
            return {
                key: item.id || item.key || `initial-${sequence}`,
                id: item.id || null,
                name: item.name || '',
                size: Number(item.size || 0),
                type: item.type || '',
                url: item.url || '',
                file: null,
                objectUrl: null,
                existing: true
            };
        });

        root.dataset.uploadReady = 'true';

        function syncRemovedInput() {
            if (removedInput) {
                removedInput.value = JSON.stringify(removedItems);
            }
        }

        function syncFileInput() {
            if (typeof DataTransfer === 'undefined') {
                return;
            }

            const transfer = new DataTransfer();
            stateItems.forEach(function (item) {
                if (item.file) {
                    transfer.items.add(item.file);
                }
            });
            input.files = transfer.files;
        }

        function showFeedback(message) {
            if (!feedbackEl) {
                return;
            }

            feedbackEl.textContent = message || '';
            feedbackEl.hidden = !message;
        }

        function clearFeedback() {
            showFeedback('');
        }

        function releaseItem(item) {
            if (item && item.objectUrl) {
                URL.revokeObjectURL(item.objectUrl);
                item.objectUrl = null;
            }
        }

        function trackRemoval(item) {
            if (!item || !item.existing) {
                return;
            }

            const token = item.id || item.name;
            if (!token || removedItems.includes(token)) {
                return;
            }

            removedItems.push(token);
        }

        function setDragState(active) {
            root.classList.toggle('is-dragover', active);
        }

        function isAllowedFile(file) {
            const extension = getFileNameExtension(file?.name);
            const matchesExtension = !allowedExtensions.length || allowedExtensions.includes(extension);
            if (!matchesExtension) {
                return false;
            }

            if (isImage) {
                return String(file?.type || '').startsWith('image/');
            }

            return true;
        }

        function buildIconElement(iconName, className) {
            const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
            svg.setAttribute('class', className);
            svg.setAttribute('aria-hidden', 'true');
            svg.setAttribute('focusable', 'false');
            svg.setAttribute('viewBox', '0 0 16 16');

            const use = document.createElementNS('http://www.w3.org/2000/svg', 'use');
            const href = `${iconSpritePath}#${iconName}`;
            use.setAttribute('href', href);
            use.setAttributeNS('http://www.w3.org/1999/xlink', 'xlink:href', href);
            svg.appendChild(use);

            return svg;
        }

        function renderFileList() {
            if (!listEl) {
                return;
            }

            listEl.innerHTML = '';

            if (!stateItems.length) {
                if (listWrapperEl) {
                    listWrapperEl.style.display = 'none';
                }

                clearAllTriggers.forEach(function (button) {
                    button.hidden = true;
                    button.disabled = true;
                });
                return;
            }

            if (listWrapperEl) {
                listWrapperEl.style.display = 'block';
            }

            clearAllTriggers.forEach(function (button) {
                button.hidden = false;
                button.disabled = false;
            });

            stateItems.forEach(function (item) {
                const row = document.createElement('div');
                row.className = 'tbx-upload-file-item';

                const iconName = getFileIconName(item.name, item.type);
                const icon = buildIconElement(iconName, `tbx-upload-file-icon ${getFileIconTone(iconName)}`);
                row.appendChild(icon);

                const titleEl = item.url
                    ? document.createElement('a')
                    : document.createElement('span');
                titleEl.className = 'tbx-upload-file-name';
                titleEl.textContent = item.name || emptyText;
                titleEl.title = item.name || '';

                if (item.url) {
                    titleEl.href = item.url;
                    titleEl.target = '_blank';
                    titleEl.rel = 'noreferrer noopener';
                }

                row.appendChild(titleEl);

                const meta = document.createElement('span');
                meta.className = 'tbx-upload-file-meta';
                meta.textContent = formatFileSize(item.size);
                row.appendChild(meta);

                const removeButton = document.createElement('button');
                removeButton.type = 'button';
                removeButton.className = 'tbx-upload-remove';
                removeButton.setAttribute('data-upload-remove', 'true');
                removeButton.setAttribute('aria-label', `${removeLabel}: ${item.name}`);
                removeButton.title = removeLabel;
                removeButton.appendChild(buildIconElement('x-lg', 'tbx-bi'));
                removeButton.addEventListener('click', function (event) {
                    event.preventDefault();
                    event.stopPropagation();
                    removeItem(item.key);
                });
                row.appendChild(removeButton);

                listEl.appendChild(row);
            });
        }

        function renderImage() {
            if (!previewEl) {
                return;
            }

            const item = stateItems[0] || null;
            root.classList.toggle('has-value', !!item);

            clearTriggers.forEach(function (button) {
                button.hidden = !item;
                button.disabled = !item;
            });

            if (previewWrapperEl) {
                previewWrapperEl.style.display = item ? 'block' : 'none';
            }

            if (!item) {
                previewEl.removeAttribute('src');
                return;
            }

            previewEl.src = item.objectUrl || item.url;
            previewEl.alt = item.name || imagePlaceholderTitle;
        }

        function render() {
            root.classList.toggle('has-data', stateItems.length > 0);

            if (isImage) {
                renderImage();
            } else {
                renderFileList();
            }

            syncRemovedInput();
            syncFileInput();
        }

        function createItemFromFile(file) {
            sequence += 1;
            return {
                key: `new-${sequence}`,
                id: null,
                name: file.name || '',
                size: Number(file.size || 0),
                type: file.type || '',
                url: '',
                file: file,
                objectUrl: isImage ? URL.createObjectURL(file) : null,
                existing: false
            };
        }

        function clearCurrent(markExistingAsRemoved) {
            stateItems.forEach(function (item) {
                if (markExistingAsRemoved) {
                    trackRemoval(item);
                }

                releaseItem(item);
            });

            stateItems = [];
        }

        function addFiles(fileList) {
            const acceptedFiles = Array.from(fileList || []).filter(function (file) {
                return !!file;
            });

            if (!acceptedFiles.length) {
                return;
            }

            const validFiles = acceptedFiles.filter(isAllowedFile);
            const invalidFiles = acceptedFiles.filter(function (file) {
                return !isAllowedFile(file);
            });

            if (invalidFiles.length) {
                showFeedback(invalidTypeMessage);
            } else {
                clearFeedback();
            }

            if (!validFiles.length) {
                input.value = '';
                syncFileInput();
                return;
            }

            if (isImage) {
                clearCurrent(true);
                stateItems = [createItemFromFile(validFiles[0])];
            } else {
                const nextItems = validFiles.map(createItemFromFile);
                if (isMultiple) {
                    stateItems = stateItems.concat(nextItems);
                } else {
                    clearCurrent(true);
                    stateItems = nextItems;
                }
            }

            render();
        }

        function removeItem(key) {
            const nextItems = [];

            stateItems.forEach(function (item) {
                if (item.key !== key) {
                    nextItems.push(item);
                    return;
                }

                trackRemoval(item);
                releaseItem(item);
            });

            stateItems = nextItems;
            render();
        }

        input.addEventListener('change', function () {
            addFiles(input.files);
        });

        triggers.forEach(function (trigger) {
            trigger.addEventListener('click', function () {
                input.value = '';
                input.click();
            });
        });

        clearTriggers.forEach(function (trigger) {
            trigger.addEventListener('click', function (event) {
                event.preventDefault();
                event.stopPropagation();
                clearFeedback();
                clearCurrent(true);
                render();
            });
        });

        clearAllTriggers.forEach(function (trigger) {
            trigger.addEventListener('click', function (event) {
                event.stopPropagation();
                clearFeedback();
                clearCurrent(true);
                render();
            });
        });

        dropzones.forEach(function (dropzone) {
            ['dragenter', 'dragover'].forEach(function (eventName) {
                dropzone.addEventListener(eventName, function (event) {
                    event.preventDefault();
                    setDragState(true);
                });
            });

            ['dragleave', 'dragend'].forEach(function (eventName) {
                dropzone.addEventListener(eventName, function (event) {
                    event.preventDefault();
                    setDragState(false);
                });
            });

            dropzone.addEventListener('drop', function (event) {
                event.preventDefault();
                setDragState(false);
                addFiles(event.dataTransfer?.files || []);
            });
        });

        render();

        return {
            getItems: function () {
                return stateItems.slice();
            },
            clear: function () {
                clearCurrent(true);
                render();
            }
        };
    }

    function initUploadControls(root) {
        (root || document).querySelectorAll('[data-upload-control]').forEach(function (element) {
            createUploadControl(element);
        });
    }

    window.ToolboxUploadControls = {
        init: createUploadControl,
        initAll: initUploadControls
    };

    initUploadControls(document);

    const NOTIFY_TITLES = {
        vi: { success: 'Thành công', error: 'Lỗi', warning: 'Cảnh báo', info: 'Thông tin' },
        en: { success: 'Success', error: 'Error', warning: 'Warning', info: 'Information' }
    };

    function escapeHtml(value) {
        const div = document.createElement('div');
        div.textContent = value === null || value === undefined ? '' : String(value);
        return div.innerHTML;
    }

    function buildNotificationContent(message, type) {
        const lang = (document.documentElement.lang || 'vi-VN').toLowerCase().startsWith('en') ? 'en' : 'vi';
        const title = NOTIFY_TITLES[lang][type] || NOTIFY_TITLES[lang].info;
        return '<div class="tbx-notify-title">' + escapeHtml(title) + '</div>' +
            '<div class="tbx-notify-desc">' + escapeHtml(message) + '</div>';
    }

    function loadScriptOnce(src) {
        return new Promise(function (resolve, reject) {
            if (document.querySelector('script[src="' + src + '"]')) {
                resolve();
                return;
            }
            const script = document.createElement('script');
            script.src = src;
            script.onload = function () { resolve(); };
            script.onerror = function () {
                // Removed so a later call (back online) loads it again instead of trusting a dead tag.
                script.remove();
                reject(new Error('Failed to load ' + src));
            };
            document.body.appendChild(script);
        });
    }

    function loadStyleOnce(href) {
        if (document.querySelector('link[href="' + href + '"]')) {
            return;
        }
        const link = document.createElement('link');
        link.rel = 'stylesheet';
        link.href = href;
        // Like a failed script: drop the tag so the next try (back online) loads it again.
        link.onerror = function () { link.remove(); };
        // Vendor styles go before vendor-sync.css, as in _Layout.cshtml, so the app's skin wins.
        const appSkin = document.querySelector('link[href*="/css/vendor-sync.css"]');
        if (appSkin) {
            appSkin.before(link);
        } else {
            document.head.appendChild(link);
        }
    }

    let notificationWidgetPromise = null;

    function getNotificationWidget() {
        if (notificationWidgetPromise) {
            return notificationWidgetPromise;
        }

        notificationWidgetPromise = Promise.resolve()
            .then(function () {
                if (typeof window.kendo === 'undefined' || !window.jQuery || !window.jQuery.fn.kendoNotification) {
                    loadStyleOnce('/vendor/kendo/icons/index.css');
                    loadStyleOnce('/vendor/kendo/theme/default-main.css');
                    // Placeholder for the commercial kendo-drawing package; see Views/Shared/_KendoScripts.cshtml.
                    window._progress_kendo_drawing = window._progress_kendo_drawing || {};
                    return loadScriptOnce('/vendor/kendo/js/kendo.ui.core.min.js');
                }
            })
            .then(function () {
                let host = document.getElementById('tbx-notification-host');
                if (!host) {
                    host = document.createElement('div');
                    host.id = 'tbx-notification-host';
                    host.className = 'tbx-notification-host';
                    document.body.appendChild(host);
                }

                const existing = window.jQuery(host).data('kendoNotification');
                if (existing) {
                    return existing;
                }

                const notificationIconTemplate = function (iconClass) {
                    return '<span class="k-notification-status"><span class="k-icon k-font-icon ' + iconClass + '"></span></span><div class="k-notification-content">#= content #</div>';
                };

                return window.jQuery(host).kendoNotification({
                    appendTo: host,
                    stacking: 'down',
                    autoHideAfter: 4500,
                    hideOnClick: false,
                    button: true,
                    templates: [
                        { type: 'info', template: notificationIconTemplate('k-i-question-circle') },
                        { type: 'success', template: notificationIconTemplate('k-i-check-circle') },
                        { type: 'warning', template: notificationIconTemplate('k-i-warning-triangle') },
                        { type: 'error', template: notificationIconTemplate('k-i-x-circle') }
                    ]
                }).data('kendoNotification');
            })
            .catch(function (error) {
                // Kendo could not load (offline): forget the failure so the next notice tries again.
                notificationWidgetPromise = null;
                throw error;
            });

        return notificationWidgetPromise;
    }

    // Without Kendo (it could not load, e.g. offline) a notice still shows, in the same host and
    // with the same look, and goes away on its own.
    const PLAIN_NOTICE_ICONS = { info: 'info', success: 'check', warning: 'triangle-alert', error: 'circle-alert' };

    function showPlainNotice(content, type) {
        let host = document.getElementById('tbx-notification-host');
        if (!host) {
            host = document.createElement('div');
            host.id = 'tbx-notification-host';
            host.className = 'tbx-notification-host';
            document.body.appendChild(host);
        }

        host.querySelectorAll('[data-plain-notice]').forEach(function (old) { old.remove(); });
        const notice = document.createElement('div');
        notice.className = 'k-notification k-notification-' + type;
        notice.setAttribute('role', type === 'error' || type === 'warning' ? 'alert' : 'status');
        notice.setAttribute('data-plain-notice', '');
        notice.innerHTML = '<span class="k-notification-status">' + window.Toolbox.icon(PLAIN_NOTICE_ICONS[type] || 'info', 16) + '</span>'
            + '<div class="k-notification-content">' + content + '</div>';
        host.appendChild(notice);
        window.setTimeout(function () { notice.remove(); }, 6000);
    }

    window.Toolbox = window.Toolbox || {};

    // An icon from the app sprite (<tbx-icon> on the server), for markup built in the browser.
    window.Toolbox.icon = function (name, size) {
        const px = size || 16;
        const sprite = document.querySelector('svg.tbx-icon use')?.getAttribute('href') || '';
        const base = sprite.split('#')[0] || '/vendor/lucide/lucide-sprite.svg';
        return '<svg class="tbx-icon" width="' + px + '" height="' + px + '" aria-hidden="true" focusable="false">'
            + '<use href="' + base + '#' + name + '"></use></svg>';
    };

    window.Toolbox.notify = function (message, type) {
        if (!message) return;
        const safeType = type || 'info';
        const content = buildNotificationContent(message, safeType);
        getNotificationWidget().then(function (notification) {
            notification.hide();
            document.querySelectorAll('[data-plain-notice]').forEach(function (old) { old.remove(); });
            notification.show(content, safeType);
        }).catch(function () {
            showPlainNotice(content, safeType);
        });
    };

    function initFlashNotification() {
        const flashDataEl = document.getElementById('tbx-flash-data');
        const flashItems = parseJson(flashDataEl?.textContent, []);
        if (!flashItems.length) {
            return;
        }

        getNotificationWidget().then(function (notification) {
            flashItems.forEach(function (item) {
                if (item && item.message) {
                    const type = item.type || 'info';
                    notification.hide();
                    notification.show(buildNotificationContent(item.message, type), type);
                }
            });
        });
    }

    initFlashNotification();

    // ------------------------------------------------------------------ confirmation dialog
    // Toolbox.confirm({ title, message, okText, tone }) resolves true when the user confirms.
    // tone is "danger" (default) or "primary". Falls back to window.confirm without Bootstrap.
    const confirmModalElement = document.getElementById('tbx-confirm');
    let resolveConfirm = null;

    function settleConfirm(value) {
        if (resolveConfirm) {
            const resolve = resolveConfirm;
            resolveConfirm = null;
            resolve(value);
        }
    }

    window.Toolbox.confirm = function (options) {
        const settings = options || {};
        if (!confirmModalElement || !window.bootstrap?.Modal) {
            return Promise.resolve(window.confirm(settings.message || ''));
        }

        settleConfirm(false);
        const titleEl = confirmModalElement.querySelector('[data-confirm-role="title"]');
        const messageEl = confirmModalElement.querySelector('[data-confirm-role="message"]');
        const okButton = confirmModalElement.querySelector('[data-confirm-role="ok"]');
        titleEl.textContent = settings.title || titleEl.dataset.defaultText;
        messageEl.textContent = settings.message || '';
        okButton.textContent = settings.okText || okButton.dataset.defaultText || okButton.textContent;
        okButton.classList.toggle('btn-danger', settings.tone !== 'primary');
        okButton.classList.toggle('btn-primary', settings.tone === 'primary');

        return new Promise(function (resolve) {
            resolveConfirm = resolve;
            window.bootstrap.Modal.getOrCreateInstance(confirmModalElement).show();
        });
    };

    if (confirmModalElement) {
        const titleDefault = confirmModalElement.querySelector('[data-confirm-role="title"]');
        titleDefault.dataset.defaultText = titleDefault.textContent;
        confirmModalElement.querySelector('[data-confirm-role="ok"]').addEventListener('click', function () {
            settleConfirm(true);
            window.bootstrap.Modal.getOrCreateInstance(confirmModalElement).hide();
        });
        confirmModalElement.addEventListener('hidden.bs.modal', function () {
            settleConfirm(false);
        });
    }

    // Declarative use: <button type="submit" data-confirm="Xóa “X”?" data-confirm-title="..."
    // data-confirm-ok="Xóa">. The attribute may also sit on the <form>.
    document.addEventListener('submit', function (event) {
        const form = event.target;
        if (!(form instanceof HTMLFormElement) || form.dataset.confirmed === 'true') {
            return;
        }

        const source = event.submitter && event.submitter.hasAttribute('data-confirm')
            ? event.submitter
            : (form.hasAttribute('data-confirm') ? form : null);
        if (!source) {
            return;
        }

        event.preventDefault();
        const submitter = event.submitter;
        window.Toolbox.confirm({
            title: source.dataset.confirmTitle,
            message: source.dataset.confirm,
            okText: source.dataset.confirmOk,
            tone: source.dataset.confirmTone
        }).then(function (confirmed) {
            if (!confirmed) {
                return;
            }

            form.dataset.confirmed = 'true';
            if (submitter && typeof form.requestSubmit === 'function') {
                form.requestSubmit(submitter);
            } else {
                form.submit();
            }
        });
    });
})();

// Installed app (PWA) and deployments, in every run mode: registers the service worker
// (Content/sw.js, only over HTTPS or on this computer, as browsers require) and, every ten
// minutes and when the tab comes back, asks /app/version whether a new build is running. If so,
// a small bar offers to reload. The build of this page is in <meta name="toolkit-build">.
(function () {
    'use strict';

    const meta = document.querySelector('meta[name="toolkit-build"]');
    // A screen inside the shell's frame: the shell registers the service worker and checks for a new version.
    if (!meta || window.top !== window) {
        return;
    }
    const build = meta.content;
    // The offline page (stored by the service worker) shows itself in the language last used here.
    try { localStorage.setItem('toolbox.lang', document.documentElement.lang || ''); } catch (e) { }
    const secure = window.location.protocol === 'https:' || ['localhost', '127.0.0.1'].includes(window.location.hostname);

    if ('serviceWorker' in navigator && secure) {
        window.addEventListener('load', function () {
            navigator.serviceWorker.register('/sw.js?build=' + encodeURIComponent(build)).catch(function () {
                // Without it the app still works; it only cannot be installed or shown offline.
            });
        });
    }

    let shown = false;
    function showBar() {
        if (shown) {
            return;
        }
        shown = true;
        const bar = document.createElement('div');
        bar.className = 'tbx-update-bar';
        bar.setAttribute('role', 'status');
        bar.innerHTML = '<span class="tbx-update-bar-text"></span>'
            + '<button type="button" class="btn btn-primary btn-sm" data-update-reload></button>'
            + '<button type="button" class="btn btn-sm tbx-icon-button" data-update-close></button>';
        bar.querySelector('.tbx-update-bar-text').textContent = meta.dataset.textNew;
        bar.querySelector('[data-update-reload]').textContent = meta.dataset.textReload;
        const close = bar.querySelector('[data-update-close]');
        close.setAttribute('aria-label', meta.dataset.textClose);
        close.innerHTML = window.Toolbox && window.Toolbox.icon ? window.Toolbox.icon('x', 16) : '×';
        bar.querySelector('[data-update-reload]').addEventListener('click', function () { window.location.reload(); });
        close.addEventListener('click', function () { bar.remove(); });
        document.body.appendChild(bar);
    }

    async function check() {
        if (shown || document.visibilityState === 'hidden') {
            return;
        }
        try {
            const response = await fetch('/app/version', { cache: 'no-store', headers: { 'X-Requested-With': 'XMLHttpRequest' } });
            const data = response.ok ? await response.json() : null;
            if (data && data.build && data.build !== build) {
                showBar();
            }
        } catch (error) {
            // Offline or restarting: the next check tries again.
        }
    }

    window.setInterval(check, 10 * 60 * 1000);
    document.addEventListener('visibilitychange', function () {
        if (document.visibilityState === 'visible') {
            check();
        }
    });
})();

// Desktop: "a new version is available" notice from the license server (_DesktopNotices).
// "Later" hides it until a newer version comes; a required release stays (no "Later" button).
(function () {
    'use strict';

    const notice = document.querySelector('[data-release-notice]');
    if (!notice) {
        return;
    }
    const version = notice.getAttribute('data-release-notice');
    const key = 'toolbox.release.dismissed';
    let dismissed = null;
    try { dismissed = localStorage.getItem(key); } catch (e) { }
    if (notice.getAttribute('data-release-mandatory') !== 'true') {
        notice.hidden = dismissed === version;
    }
    const later = notice.querySelector('[data-release-dismiss]');
    if (later) {
        later.addEventListener('click', function () {
            try { localStorage.setItem(key, version); } catch (e) { }
            notice.hidden = true;
        });
    }
})();

// Moving to another page, and kept screens (Infrastructure/Web/ScreenFrames.cs).
// Toolbox.go(href) is how the sidebar, the palette and "G then letter" move to a page: a plain
// navigation here, a switch of screens in the shell (screens.js) or from a screen inside it
// (screen-frame.js), which replace it. Toolbox.setKeepScreens(on) turns kept screens on or off for
// this browser (a cookie the server reads) and reloads the window to apply it.
(function () {
    'use strict';

    const Toolbox = window.Toolbox = window.Toolbox || {};
    const COOKIE = 'toolbox.keepAlive';
    const MAX_KEY = 'toolbox.screens.max';

    if (!Toolbox.go) {
        Toolbox.go = function (href) { window.location.href = href; };
    }

    // A page that must be the whole window (sign-in after the session ended): from a kept
    // screen it replaces the shell, not just the screen.
    Toolbox.goTop = function (href) {
        try {
            window.top.location.href = href;
        } catch (error) {
            window.location.href = href;
        }
    };

    // Timers that poll the server ask this first: false while the tab is in the background, and
    // while this page is a kept screen hidden behind another one (screen-frame.js replaces it).
    if (!Toolbox.isScreenVisible) {
        Toolbox.isScreenVisible = function () { return document.visibilityState === 'visible'; };
    }

    // Unsaved work a page reports (count of things that would be lost): a kept screen asks before
    // it is closed (screens.js). Toolbox.pendingChanges.add(fn) returns a function that removes it.
    if (!Toolbox.pendingChanges) {
        const sources = [];
        Toolbox.pendingChanges = {
            add: function (fn) {
                sources.push(fn);
                return function () { const at = sources.indexOf(fn); if (at >= 0) sources.splice(at, 1); };
            },
            count: function () {
                return sources.reduce(function (sum, fn) {
                    try { return sum + (Number(fn()) || 0); } catch (e) { return sum; }
                }, 0);
            }
        };
    }

    Toolbox.keepScreens = function () {
        return !/(?:^|;\s*)toolbox\.keepAlive=off(?:;|$)/.test(document.cookie);
    };

    Toolbox.setKeepScreens = function (on) {
        document.cookie = COOKIE + '=' + (on ? 'on' : 'off') + '; path=/; max-age=31536000; samesite=lax';
        window.top.location.reload();
    };

    /** The most screens kept at once, or 0 for no limit. */
    Toolbox.maxScreens = function () {
        let value = 0;
        try { value = parseInt(localStorage.getItem(MAX_KEY) || '0', 10); } catch (e) { }
        return Number.isFinite(value) && value > 0 ? value : 0;
    };

    // Account page > Display: the switch and the maximum (both per browser).
    const keep = document.querySelector('[data-keep-screens]');
    if (keep) {
        keep.checked = Toolbox.keepScreens();
        keep.addEventListener('change', function () {
            document.cookie = COOKIE + '=' + (keep.checked ? 'on' : 'off') + '; path=/; max-age=31536000; samesite=lax';
        });
    }
    const max = document.querySelector('[data-max-screens]');
    if (max) {
        const current = Toolbox.maxScreens();
        max.value = current ? String(current) : '';
        max.addEventListener('change', function () {
            const value = parseInt(max.value, 10);
            try {
                if (Number.isFinite(value) && value > 0) localStorage.setItem(MAX_KEY, String(Math.min(50, value)));
                else localStorage.removeItem(MAX_KEY);
            } catch (e) { }
            max.value = Toolbox.maxScreens() ? String(Toolbox.maxScreens()) : '';
        });
    }
})();
