(function () {
    const MODAL_QUERY_KEY = 'modal';
    const MODAL_SIZE_PRESETS = {
        sm: { width: 560, height: 360 },
        md: { width: 760, height: 440 },
        lg: { width: 960, height: 620 },
        xl: { width: 1160, height: 760 }
    };
    const MANAGED_PATH_PATTERNS = [
        /\/Create$/i,
        /\/Edit\/[^/]+$/i,
        /\/Details\/[^/]+$/i,
        /\/Delete\/[^/]+$/i
    ];
    const SAVE_SUCCESS_MESSAGE = 'Изменения сохранены';
    const SAVE_SUCCESS_DURATION_MS = 1000;

    function toUrl(input) {
        try {
            if (input instanceof URL) {
                return new URL(input.toString(), window.location.origin);
            }

            return new URL(String(input || ''), window.location.origin);
        } catch {
            return null;
        }
    }

    function ensureModalFlag(input) {
        const url = toUrl(input);
        if (!url) {
            return '';
        }

        url.searchParams.set(MODAL_QUERY_KEY, 'true');
        return url.toString();
    }

    function isSameOrigin(url) {
        return Boolean(url) && url.origin === window.location.origin;
    }

    function isManagedUrl(input) {
        const url = toUrl(input);
        if (!url || !isSameOrigin(url)) {
            return false;
        }

        const normalizedPath = url.pathname.replace(/\/+$/, '');
        return MANAGED_PATH_PATTERNS.some(pattern => pattern.test(normalizedPath));
    }

    function startsWithAny(text, variants) {
        return variants.some(variant => text.startsWith(variant));
    }

    function inferActionMeta(input, fallbackTitle, fallbackIconClass) {
        const url = toUrl(input);
        const pathname = url?.pathname.replace(/\/+$/, '') ?? '';
        const fallback = String(fallbackTitle || '').trim();

        if (/\/Create$/i.test(pathname)) {
            return {
                title: fallback || 'Создание',
                iconClass: fallbackIconClass || 'bi bi-plus-circle me-2'
            };
        }

        if (/\/Edit\/[^/]+$/i.test(pathname)) {
            return {
                title: fallback || 'Редактирование',
                iconClass: fallbackIconClass || 'bi bi-pencil-square me-2'
            };
        }

        if (/\/Details\/[^/]+$/i.test(pathname)) {
            return {
                title: fallback || 'Просмотр',
                iconClass: fallbackIconClass || 'bi bi-eye me-2'
            };
        }

        if (/\/Delete\/[^/]+$/i.test(pathname)) {
            return {
                title: fallback || 'Удаление',
                iconClass: fallbackIconClass || 'bi bi-trash me-2'
            };
        }

        return {
            title: fallback || 'Форма',
            iconClass: fallbackIconClass || 'bi bi-window me-2'
        };
    }

    function readLinkTitle(link) {
        if (!link) {
            return '';
        }

        return String(link.dataset.crmModalTitle || link.title || link.textContent || '').trim();
    }

    function isTruthy(value) {
        const normalized = String(value || '').trim().toLowerCase();
        return normalized === 'true' || normalized === '1';
    }

    function clamp(value, min, max) {
        return Math.min(Math.max(value, min), max);
    }

    function parseBootstrapColumnFraction(className) {
        const matches = String(className || '').match(/\bcol(?:-(?:sm|md|lg|xl|xxl))?-(\d{1,2})\b/g);
        if (!matches || matches.length === 0) {
            return 0;
        }

        let fraction = 0;
        matches.forEach(match => {
            const parts = match.split('-');
            const lastPart = parts[parts.length - 1];
            const value = Number.parseInt(lastPart, 10);
            if (!Number.isFinite(value) || value <= 0 || value > 12) {
                return;
            }

            fraction = Math.max(fraction, value / 12);
        });

        return fraction;
    }

    function normalizeModalSize(value) {
        const normalized = String(value || '').trim().toLowerCase();
        return Object.prototype.hasOwnProperty.call(MODAL_SIZE_PRESETS, normalized) ? normalized : null;
    }

    function inferActionSize(input) {
        const url = toUrl(input);
        const pathname = url?.pathname.replace(/\/+$/, '') ?? '';

        if (/\/Delete\/[^/]+$/i.test(pathname)) {
            return 'md';
        }

        if (/\/Details\/[^/]+$/i.test(pathname)) {
            return 'xl';
        }

        return 'lg';
    }

    function initModalHost() {
        const modalEl = document.getElementById('globalCreateModal');
        const modalDialog = document.getElementById('globalCreateModalDialog');
        const modalTitle = document.getElementById('globalCreateModalTitle');
        const modalFrame = document.getElementById('globalCreateModalFrame');

        if (!modalEl || !modalDialog || !modalTitle || !modalFrame || window.self !== window.top || !window.bootstrap?.Modal) {
            return;
        }

        const modal = new window.bootstrap.Modal(modalEl);
        const state = {
            options: null
        };

        function setTitle(title, iconClass) {
            const safeTitle = String(title || '').trim() || 'Форма';
            const safeIconClass = String(iconClass || '').trim() || 'bi bi-window me-2';

            modalTitle.innerHTML = '';
            const icon = document.createElement('i');
            icon.className = safeIconClass;
            modalTitle.appendChild(icon);
            modalTitle.append(safeTitle);
        }

        function applySizing(size) {
            const normalizedSize = normalizeModalSize(size) || 'lg';
            const preset = MODAL_SIZE_PRESETS[normalizedSize];

            modalDialog.dataset.crmModalSize = normalizedSize;
            modalDialog.style.setProperty('--crm-modal-dialog-width', `${preset.width}px`);
            modalFrame.style.height = `${preset.height}px`;
        }

        function resetSizing() {
            applySizing('lg');
        }

        function open(input, options) {
            const url = toUrl(input);
            if (!url || !isSameOrigin(url)) {
                return false;
            }

            const openOptions = options && typeof options === 'object' ? { ...options } : {};
            const actionMeta = inferActionMeta(url, openOptions.title, openOptions.iconClass);
            const size = normalizeModalSize(openOptions.size) || inferActionSize(url);

            state.options = openOptions;
            applySizing(size);
            setTitle(actionMeta.title, actionMeta.iconClass);
            modalFrame.src = ensureModalFlag(url);
            modal.show();
            return true;
        }

        function openLink(link) {
            if (!link) {
                return false;
            }

            return open(link.href, {
                title: readLinkTitle(link),
                iconClass: link.dataset.crmModalIcon
            });
        }

        function close() {
            modal.hide();
        }

        function reloadParent() {
            if (state.options?.reloadOnComplete === false) {
                return;
            }

            window.location.reload();
        }

        function handleEntityCreated(data) {
            let handled = false;

            if (typeof state.options?.onEntityCreated === 'function') {
                handled = state.options.onEntityCreated(data) === true;
            }

            close();

            if (!handled) {
                reloadParent();
            }
        }

        function ensureSaveSuccessToast() {
            let toast = document.getElementById('crmModalSaveSuccessToast');
            if (toast) {
                return toast;
            }

            toast = document.createElement('div');
            toast.id = 'crmModalSaveSuccessToast';
            toast.className = 'crm-save-success-toast';
            toast.setAttribute('aria-live', 'polite');
            toast.setAttribute('aria-atomic', 'true');
            toast.innerHTML = `
                <div class="crm-save-success-toast__icon" aria-hidden="true">
                    <i class="bi bi-check-lg"></i>
                </div>
                <div class="crm-save-success-toast__text">${SAVE_SUCCESS_MESSAGE}</div>
            `;

            document.body.appendChild(toast);
            return toast;
        }

        function showSaveSuccessToast() {
            const toast = ensureSaveSuccessToast();
            toast.classList.remove('is-visible');

            window.requestAnimationFrame(() => {
                toast.classList.add('is-visible');
            });

            window.clearTimeout(showSaveSuccessToast.hideTimer);
            showSaveSuccessToast.hideTimer = window.setTimeout(() => {
                toast.classList.remove('is-visible');
            }, SAVE_SUCCESS_DURATION_MS);
        }

        function handleEntityUpdated(data) {
            if (typeof state.options?.onEntityUpdated === 'function') {
                state.options.onEntityUpdated(data);
            }

            close();
            showSaveSuccessToast();

            window.setTimeout(() => {
                reloadParent();
            }, SAVE_SUCCESS_DURATION_MS);
        }

        function handleNavigation(data) {
            const url = toUrl(data?.url);
            if (!url || !isSameOrigin(url)) {
                close();
                reloadParent();
                return;
            }

            if (isManagedUrl(url)) {
                modalFrame.src = ensureModalFlag(url);
                return;
            }

            close();

            if (typeof state.options?.onNavigateOut === 'function') {
                state.options.onNavigateOut(data, url);
                return;
            }

            reloadParent();
        }

        function canHandleLink(link) {
            if (!link || link.dataset.crmModal === 'off' || link.dataset.bsToggle || link.hasAttribute('download')) {
                return false;
            }

            const target = link.getAttribute('target');
            if (target && target !== '_self') {
                return false;
            }

            if (isTruthy(link.dataset.crmModal)) {
                return true;
            }

            return isManagedUrl(link.href);
        }

        document.addEventListener('click', event => {
            if (event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
                return;
            }

            const link = event.target.closest('a[href]');
            if (!canHandleLink(link)) {
                return;
            }

            event.preventDefault();
            openLink(link);
        });

        window.addEventListener('message', event => {
            if (event.origin !== window.location.origin || !modalEl.classList.contains('show')) {
                return;
            }

            const data = event.data;
            if (!data || typeof data !== 'object') {
                return;
            }

            switch (data.type) {
                case 'crm-modal-close':
                    close();
                    return;
                case 'crm-modal-config':
                    applySizing(data.size);
                    return;
                case 'crm-modal-title':
                    setTitle(data.title, data.iconClass);
                    return;
                case 'crm-entity-created':
                    handleEntityCreated(data);
                    return;
                case 'crm-entity-updated':
                    handleEntityUpdated(data);
                    return;
                case 'crm-modal-navigate':
                    handleNavigation(data);
                    return;
                case 'crm-modal-refresh':
                    close();
                    reloadParent();
                    return;
                default:
                    return;
            }
        });

        modalEl.addEventListener('hidden.bs.modal', () => {
            if (typeof state.options?.onClose === 'function') {
                state.options.onClose();
            }

            modalFrame.removeAttribute('src');
            resetSizing();
            state.options = null;

            if (document.querySelector('.modal.show')) {
                setTimeout(() => document.body.classList.add('modal-open'), 0);
            }
        });

        window.crmModalHost = {
            open,
            openLink,
            close,
            canHandleLink,
            canHandleUrl: isManagedUrl,
            ensureModalFlag
        };
    }

    function initModalFrameBridge() {
        if (!document.body || !isTruthy(document.body.dataset.crmModalPage)) {
            return;
        }

        const postToParent = payload => {
            if (window.parent && window.parent !== window) {
                window.parent.postMessage(payload, window.location.origin);
            }
        };

        function ensureHiddenModalInputs() {
            document.querySelectorAll('form').forEach(form => {
                if (form.querySelector(`input[name="${MODAL_QUERY_KEY}"]`)) {
                    return;
                }

                const input = document.createElement('input');
                input.type = 'hidden';
                input.name = MODAL_QUERY_KEY;
                input.value = 'true';
                form.appendChild(input);
            });
        }

        function isCloseIntent(link) {
            if (!link) {
                return false;
            }

            if (isTruthy(link.dataset.crmModalClose)) {
                return true;
            }

            const text = String(link.textContent || '').trim().toLowerCase();
            return startsWithAny(text, ['отмена', 'назад', 'к списку']) ||
                   Boolean(link.querySelector('.bi-arrow-left, .bi-arrow-left-short, .bi-x-lg, .bi-x'));
        }

        function detectModalSize() {
            const explicitSize = normalizeModalSize(document.body.dataset.crmModalSize);
            if (explicitSize) {
                return explicitSize;
            }

            return inferActionSize(window.location.href);
        }

        function detectTitle() {
            const rawTitle = String(document.title || '').trim();
            const cleanTitle = rawTitle.replace(/\s*-\s*Звено CRM\s*$/i, '').trim();
            const pathname = window.location.pathname.replace(/\/+$/, '');

            if (/\/Create$/i.test(pathname)) {
                return {
                    title: cleanTitle || 'Создание',
                    iconClass: 'bi bi-plus-circle me-2'
                };
            }

            if (/\/Edit\/[^/]+$/i.test(pathname)) {
                return {
                    title: cleanTitle || 'Редактирование',
                    iconClass: 'bi bi-pencil-square me-2'
                };
            }

            if (/\/Details\/[^/]+$/i.test(pathname)) {
                return {
                    title: cleanTitle || 'Просмотр',
                    iconClass: 'bi bi-eye me-2'
                };
            }

            return {
                title: cleanTitle || 'Форма',
                iconClass: 'bi bi-window me-2'
            };
        }

        function publishConfig() {
            postToParent({
                type: 'crm-modal-config',
                size: detectModalSize()
            });
        }

        function publishTitle() {
            const titleMeta = detectTitle();
            postToParent({
                type: 'crm-modal-title',
                title: titleMeta.title,
                iconClass: titleMeta.iconClass
            });
        }

        document.addEventListener('click', event => {
            const link = event.target.closest('a[href]');
            if (!link) {
                return;
            }

            if (link.hasAttribute('download') || link.getAttribute('target') === '_blank' || link.dataset.bsToggle) {
                return;
            }

            const url = toUrl(link.href);
            if (!url || !isSameOrigin(url)) {
                return;
            }

            if (isCloseIntent(link)) {
                event.preventDefault();
                postToParent({ type: 'crm-modal-close' });
                return;
            }

            if (link.dataset.crmModal === 'off') {
                return;
            }

            if (isManagedUrl(url) || isTruthy(url.searchParams.get(MODAL_QUERY_KEY))) {
                event.preventDefault();
                window.location.href = ensureModalFlag(url);
            }
        });

        document.addEventListener('DOMContentLoaded', () => {
            ensureHiddenModalInputs();
            publishConfig();
            publishTitle();
        });
    }

    initModalHost();
    initModalFrameBridge();
})();
