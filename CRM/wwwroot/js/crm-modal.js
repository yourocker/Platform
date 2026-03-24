(function () {
    const MODAL_QUERY_KEY = 'modal';
    const MANAGED_PATH_PATTERNS = [
        /\/Create$/i,
        /\/Edit\/[^/]+$/i,
        /\/Details\/[^/]+$/i,
        /\/Delete\/[^/]+$/i
    ];

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

        function resetSizing() {
            modalDialog.style.removeProperty('--crm-modal-dialog-width');
            modalFrame.style.height = '520px';
        }

        function applyLayout(data) {
            const rawWidth = Number.parseFloat(data?.width);
            const rawHeight = Number.parseFloat(data?.height);
            const width = Number.isFinite(rawWidth) ? clamp(rawWidth, 420, Math.max(window.innerWidth - 32, 420)) : null;
            const height = Number.isFinite(rawHeight) ? clamp(rawHeight, 320, Math.max(window.innerHeight - 140, 320)) : null;

            if (width) {
                modalDialog.style.setProperty('--crm-modal-dialog-width', `${Math.round(width)}px`);
            }

            if (height) {
                modalFrame.style.height = `${Math.round(height)}px`;
            }
        }

        function open(input, options) {
            const url = toUrl(input);
            if (!url || !isSameOrigin(url)) {
                return false;
            }

            const openOptions = options && typeof options === 'object' ? { ...options } : {};
            const actionMeta = inferActionMeta(url, openOptions.title, openOptions.iconClass);

            state.options = openOptions;
            resetSizing();
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
                case 'crm-modal-layout':
                    applyLayout(data);
                    return;
                case 'crm-modal-title':
                    setTitle(data.title, data.iconClass);
                    return;
                case 'crm-entity-created':
                    handleEntityCreated(data);
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

        function detectPreferredWidth() {
            const explicitWidth = Number.parseFloat(document.body.dataset.crmModalWidth || '');
            if (Number.isFinite(explicitWidth) && explicitWidth > 0) {
                return explicitWidth;
            }

            const topLevelRows = Array.from(document.querySelectorAll(
                'main > .container-fluid > .row, ' +
                'main > .container > .row, ' +
                'main > .container-fluid > form > .row, ' +
                'main > .container > form > .row, ' +
                'main > form > .row'
            )).filter(element => element instanceof HTMLElement && element.offsetParent !== null);

            const gridFraction = topLevelRows.reduce((maxFraction, row) => {
                const rowFraction = Array.from(row.children)
                    .filter(child => child instanceof HTMLElement && child.offsetParent !== null)
                    .reduce((sum, child) => {
                        return sum + parseBootstrapColumnFraction(child.className);
                    }, 0);

                return Math.max(maxFraction, Math.min(rowFraction, 1));
            }, 0);

            if (gridFraction > 0) {
                return Math.round(1320 * gridFraction + 64);
            }

            const forms = Array.from(document.querySelectorAll('main form'))
                .filter(element => element instanceof HTMLElement && element.offsetParent !== null);
            const formWidth = forms.reduce((maxWidth, form) => Math.max(maxWidth, form.scrollWidth + 48), 0);
            if (formWidth > 0) {
                return formWidth;
            }

            const cards = Array.from(document.querySelectorAll('main .card'))
                .filter(element => element instanceof HTMLElement && element.offsetParent !== null);
            const cardWidth = cards.reduce((maxWidth, card) => {
                return Math.max(maxWidth, Math.ceil(card.getBoundingClientRect().width + 48));
            }, 0);

            return cardWidth || 960;
        }

        function detectPreferredHeight() {
            const body = document.body;
            const root = document.documentElement;
            const main = document.querySelector('main');

            return Math.max(
                body.scrollHeight,
                body.offsetHeight,
                root.scrollHeight,
                root.offsetHeight,
                main?.scrollHeight || 0
            );
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

        let layoutFrame = null;
        function queueLayoutUpdate() {
            if (layoutFrame !== null) {
                cancelAnimationFrame(layoutFrame);
            }

            layoutFrame = requestAnimationFrame(() => {
                layoutFrame = null;
                postToParent({
                    type: 'crm-modal-layout',
                    width: detectPreferredWidth(),
                    height: detectPreferredHeight()
                });
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
            publishTitle();
            queueLayoutUpdate();

            if (window.ResizeObserver) {
                const resizeObserver = new ResizeObserver(() => queueLayoutUpdate());
                resizeObserver.observe(document.body);
                resizeObserver.observe(document.documentElement);
            }

            if (window.MutationObserver) {
                const mutationObserver = new MutationObserver(() => queueLayoutUpdate());
                mutationObserver.observe(document.body, {
                    childList: true,
                    subtree: true,
                    attributes: true
                });
            }

            window.addEventListener('load', queueLayoutUpdate);
            window.addEventListener('resize', queueLayoutUpdate);
        });
    }

    initModalHost();
    initModalFrameBridge();
})();
