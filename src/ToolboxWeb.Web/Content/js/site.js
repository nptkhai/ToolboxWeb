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

    const timer = document.querySelector('[data-focus-timer]');
    if (timer) {
        let remaining = Number(timer.dataset.minutes || '25') * 60;
        let intervalId = null;
        const display = timer.querySelector('[data-timer-display]');
        const minutesInput = document.querySelector('[name="DurationMinutes"]');

        function render() {
            const minutes = Math.floor(remaining / 60).toString().padStart(2, '0');
            const seconds = (remaining % 60).toString().padStart(2, '0');
            if (display) display.textContent = `${minutes}:${seconds}`;
        }

        // The save button only turns on when a countdown has actually finished.
        const saveButton = document.querySelector('[data-timer-save]');

        function setSaveEnabled(enabled) {
            if (saveButton) saveButton.disabled = !enabled;
        }

        function resetFromInput() {
            window.clearInterval(intervalId);
            intervalId = null;
            remaining = Math.max(1, Number(minutesInput?.value || '25')) * 60;
            setSaveEnabled(false);
            render();
        }

        timer.querySelector('[data-timer-start]')?.addEventListener('click', function () {
            if (intervalId || remaining <= 0) return;
            intervalId = window.setInterval(function () {
                remaining -= 1;
                render();
                if (remaining <= 0) {
                    window.clearInterval(intervalId);
                    intervalId = null;
                    setSaveEnabled(true);
                    window.Toolbox?.notify?.(timer.dataset.finishedMessage, 'success');
                }
            }, 1000);
        });

        timer.querySelector('[data-timer-pause]')?.addEventListener('click', function () {
            window.clearInterval(intervalId);
            intervalId = null;
        });

        timer.querySelector('[data-timer-reset]')?.addEventListener('click', resetFromInput);
        minutesInput?.addEventListener('change', resetFromInput);
        render();
    }

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
            script.onerror = function () { reject(new Error('Failed to load ' + src)); };
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
        document.head.appendChild(link);
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
            });

        return notificationWidgetPromise;
    }

    window.Toolbox = window.Toolbox || {};
    window.Toolbox.notify = function (message, type) {
        if (!message) return;
        const safeType = type || 'info';
        getNotificationWidget().then(function (notification) {
            notification.hide();
            notification.show(buildNotificationContent(message, safeType), safeType);
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
