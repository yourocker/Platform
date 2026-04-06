(function () {
    function hasSelect2() {
        return typeof window.jQuery !== 'undefined' &&
            typeof window.jQuery.fn !== 'undefined' &&
            typeof window.jQuery.fn.select2 === 'function';
    }

    function normalizeConfig(config) {
        const optionsConfig = config && typeof config === 'object' ? config : {};
        return {
            onChange: typeof optionsConfig.onChange === 'function'
                ? optionsConfig.onChange
                : null
        };
    }

    function buildSelect2Options(select) {
        const defaultMatcher = window.jQuery.fn.select2.defaults?.defaults?.matcher;
        const select2Options = {
            theme: 'bootstrap-5',
            width: '100%',
            language: {
                noResults: () => 'Ничего не найдено',
                searching: () => 'Поиск...',
                inputTooShort: () => 'Введите текст для поиска'
            },
            minimumResultsForSearch: 0,
            closeOnSelect: !select.multiple,
            matcher: function (params, data) {
                if (data?.element instanceof HTMLOptionElement &&
                    (data.element.disabled || data.element.hidden)) {
                    return null;
                }

                if (typeof defaultMatcher === 'function') {
                    return defaultMatcher(params, data);
                }

                return data;
            },
            templateResult: function (data) {
                if (data?.element instanceof HTMLOptionElement &&
                    (data.element.disabled || data.element.hidden)) {
                    return null;
                }

                return data?.text ?? '';
            }
        };

        const placeholderFromData = select.dataset.searchPlaceholder || '';
        const placeholderFromFirstOption = (!select.multiple && select.options.length > 0)
            ? select.options[0].textContent
            : '';
        const placeholder = (placeholderFromData || placeholderFromFirstOption || '').trim();
        if (placeholder.length > 0) {
            select2Options.placeholder = placeholder;
        }

        if (!select.multiple) {
            select2Options.allowClear = !select.required;
        }

        const parentModal = select.closest('.modal');
        if (parentModal) {
            select2Options.dropdownParent = window.jQuery(parentModal);
        }

        return select2Options;
    }

    function bindOnChange($select, select, onChange) {
        $select.off('change.crmSearchableSelectObserver');

        if (onChange) {
            $select.on('change.crmSearchableSelectObserver', () => onChange(select));
        }
    }

    function initSingleSelect(select, config) {
        const { onChange } = normalizeConfig(config);
        const $select = window.jQuery(select);

        if (!$select.hasClass('select2-hidden-accessible')) {
            $select.select2(buildSelect2Options(select));
        }

        bindOnChange($select, select, onChange);
    }

    function initSearchableSelects(root, config) {
        if (!hasSelect2()) {
            return;
        }

        (root || document).querySelectorAll('select.searchable-select').forEach(select => {
            initSingleSelect(select, config);
        });
    }

    function refreshSearchableSelect(select, config) {
        if (!hasSelect2() || !select) {
            return;
        }

        const $select = window.jQuery(select);
        if (!$select.hasClass('select2-hidden-accessible')) {
            initSingleSelect(select, config);
            return;
        }

        const instance = $select.data('select2');
        const wasOpen = typeof instance?.isOpen === 'function' && instance.isOpen();
        const activeSelect = wasOpen
            ? document.querySelector('.select2-container--open .select2-search__field')
            : null;
        const searchTerm = activeSelect instanceof HTMLInputElement ? activeSelect.value : '';

        $select.off('change.crmSearchableSelectObserver');
        $select.select2('destroy');
        initSingleSelect(select, config);

        if (wasOpen) {
            window.requestAnimationFrame(() => {
                $select.select2('open');

                const reopenedSearch = document.querySelector('.select2-container--open .select2-search__field');
                if (reopenedSearch instanceof HTMLInputElement) {
                    reopenedSearch.focus();
                    reopenedSearch.value = searchTerm;
                    reopenedSearch.dispatchEvent(new Event('input', { bubbles: true }));
                    reopenedSearch.dispatchEvent(new KeyboardEvent('keyup', { bubbles: true, key: 'Backspace' }));
                }
            });
        }
    }

    window.crmSearchableSelect = {
        hasSelect2,
        initSearchableSelects,
        refreshSearchableSelect
    };
})();
