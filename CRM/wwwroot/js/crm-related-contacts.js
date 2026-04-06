(function () {
    function entityCodesMatch(left, right) {
        const normalize = value => String(value || '').trim().replace(/s$/i, '').toLowerCase();
        return normalize(left) && normalize(left) === normalize(right);
    }

    function ensureSelectOption(select, value, text, selectAfterCreate) {
        if (!select || !value) {
            return;
        }

        let option = Array.from(select.options).find(item => item.value === String(value));
        if (!option) {
            option = document.createElement('option');
            option.value = String(value);
            select.appendChild(option);
        }

        option.textContent = text || value;

        if (selectAfterCreate) {
            select.value = String(value);
            if (typeof window.jQuery !== 'undefined' && window.jQuery(select).hasClass('select2-hidden-accessible')) {
                window.jQuery(select).trigger('change.select2');
            }
        }
    }

    function buildDetailsUrl(template, id) {
        return String(template || '').replace('__ID__', encodeURIComponent(String(id || '')));
    }

    function initRelatedContactsField(container) {
        if (!container || container.dataset.crmRelatedContactsInitialized === 'true') {
            return;
        }

        const templateId = container.dataset.templateId;
        const template = templateId ? document.getElementById(templateId) : null;
        const addButtonId = container.dataset.addButtonId;
        const addButton = addButtonId ? document.getElementById(addButtonId) : null;
        const primaryInputId = container.dataset.primaryInputId;
        const primaryInput = primaryInputId ? document.getElementById(primaryInputId) : null;
        const detailsUrlTemplate = container.dataset.detailUrlTemplate || '';
        const createUrl = container.dataset.createUrl || '';
        const createTitle = container.dataset.createTitle || 'Новый контакт';
        const openTitle = container.dataset.openTitle || 'Контакт';
        const fieldName = container.dataset.fieldName || 'ContactIds';

        if (!template || !addButton) {
            return;
        }

        container.dataset.crmRelatedContactsInitialized = 'true';

        function dispatchChanged() {
            container.dispatchEvent(new CustomEvent('crm:related-contacts-change', {
                bubbles: true
            }));
        }

        function getRows() {
            return Array.from(container.querySelectorAll('[data-crm-related-contact-row="true"]'));
        }

        function getRowSelect(row) {
            return row?.querySelector('[data-crm-related-contact-select="true"]') || null;
        }

        function getRowPrimaryButton(row) {
            return row?.querySelector('[data-crm-related-contact-primary="true"]') || null;
        }

        function getRowValue(row) {
            return String(getRowSelect(row)?.value || '');
        }

        function isPrimaryRow(row) {
            return row?.dataset.crmPrimary === 'true';
        }

        function setPrimaryRow(targetRow) {
            getRows().forEach(row => {
                row.dataset.crmPrimary = targetRow && row === targetRow ? 'true' : 'false';
            });
        }

        function updateSelectName(select) {
            if (!select) {
                return;
            }

            if (String(select.value || '').trim()) {
                select.name = fieldName;
            } else {
                select.removeAttribute('name');
            }
        }

        function refreshSelectUi(select) {
            if (!select) {
                return;
            }

            if (window.crmSearchableSelect?.refreshSearchableSelect) {
                window.crmSearchableSelect.refreshSearchableSelect(select, {
                    onChange: changedSelect => handleSelectChange(changedSelect)
                });
                return;
            }

            if (typeof window.jQuery !== 'undefined') {
                const $select = window.jQuery(select);
                if ($select.hasClass('select2-hidden-accessible')) {
                    $select.trigger('change.select2');
                }
            }
        }

        function clearSelectValue(select) {
            if (!select) {
                return;
            }

            select.value = '';
            updateSelectName(select);
            refreshSelectUi(select);
        }

        function synchronizePrimaryInput() {
            const rows = getRows();
            const selectedRows = rows.filter(row => !!getRowValue(row));
            const currentPrimaryValue = String(primaryInput?.value || '');

            let primaryRow = selectedRows.find(row => isPrimaryRow(row));
            if (!primaryRow && currentPrimaryValue) {
                primaryRow = selectedRows.find(row => getRowValue(row) === currentPrimaryValue) || null;
            }

            if (!primaryRow && selectedRows.length > 0) {
                primaryRow = selectedRows[0];
            }

            setPrimaryRow(primaryRow || null);

            if (primaryInput) {
                primaryInput.value = primaryRow ? getRowValue(primaryRow) : '';
                primaryInput.dispatchEvent(new Event('change', { bubbles: true }));
            }

            return primaryRow;
        }

        function refreshRowState(row, rowCount) {
            if (!row) {
                return;
            }

            const value = getRowValue(row);
            const openButton = row.querySelector('[data-crm-related-contact-open="true"]');
            const removeButton = row.querySelector('[data-crm-related-contact-remove="true"]');
            const primaryButton = getRowPrimaryButton(row);
            const primaryIcon = primaryButton?.querySelector('i');
            const isOnlyEmptyRow = rowCount === 1 && !value;
            const isPrimary = Boolean(value) && isPrimaryRow(row);

            if (openButton) {
                openButton.disabled = !value;
            }

            if (removeButton) {
                removeButton.classList.toggle('d-none', isOnlyEmptyRow);
                removeButton.disabled = false;
                removeButton.title = 'Удалить контакт';
            }

            if (primaryButton) {
                primaryButton.disabled = !value;
                primaryButton.classList.toggle('is-active', isPrimary);
                primaryButton.setAttribute('aria-pressed', isPrimary ? 'true' : 'false');
                primaryButton.title = !value
                    ? 'Выберите контакт'
                    : isPrimary
                        ? 'Основной контакт'
                        : 'Сделать основным';
            }

            if (primaryIcon) {
                primaryIcon.className = isPrimary ? 'bi bi-star-fill' : 'bi bi-star';
            }
        }

        function refreshAvailableOptions() {
            getRows().forEach(row => {
                const select = getRowSelect(row);
                const currentValue = String(select?.value || '');
                if (!select) {
                    return;
                }

                const reservedValues = new Set(
                    getRows()
                        .filter(otherRow => otherRow !== row)
                        .map(otherRow => getRowValue(otherRow))
                        .filter(Boolean));

                Array.from(select.options).forEach(option => {
                    if (!option.value) {
                        option.disabled = false;
                        option.hidden = false;
                        return;
                    }

                    const shouldHide = reservedValues.has(String(option.value)) && option.value !== currentValue;
                    option.disabled = shouldHide;
                    option.hidden = shouldHide;
                });

                refreshSelectUi(select);
            });
        }

        function refreshRows() {
            const rows = getRows();
            if (rows.length === 0) {
                appendRow();
                return;
            }

            const seenValues = new Set();
            rows.forEach(row => {
                const select = getRowSelect(row);
                const value = getRowValue(row);

                if (value) {
                    if (seenValues.has(value)) {
                        clearSelectValue(select);
                    } else {
                        seenValues.add(value);
                    }
                }

                updateSelectName(select);
            });

            synchronizePrimaryInput();

            rows.forEach(row => {
                refreshRowState(row, rows.length);
            });

            refreshAvailableOptions();
        }

        function appendRow(selectedValue, selectedText, isPrimary) {
            const fragment = template.content.cloneNode(true);
            container.appendChild(fragment);
            const row = container.lastElementChild;
            const select = getRowSelect(row);

            if (row) {
                row.dataset.crmPrimary = isPrimary ? 'true' : 'false';
            }

            if (select && selectedValue) {
                ensureSelectOption(select, selectedValue, selectedText, true);
            }

            if (row) {
                window.crmSearchableSelect?.initSearchableSelects(row, {
                    onChange: changedSelect => handleSelectChange(changedSelect)
                });
            }

            refreshRows();
            return row;
        }

        function handleSelectChange(select) {
            if (!select) {
                return;
            }

            const row = select.closest('[data-crm-related-contact-row="true"]');
            const selectedValue = String(select.value || '');
            const duplicateSelect = getRows()
                .map(otherRow => getRowSelect(otherRow))
                .find(otherSelect => otherSelect !== select && String(otherSelect?.value || '') === selectedValue && selectedValue);

            if (duplicateSelect) {
                clearSelectValue(select);
            }

            if (!selectedValue && row && isPrimaryRow(row)) {
                row.dataset.crmPrimary = 'false';
            }

            updateSelectName(select);
            refreshRows();
            dispatchChanged();
        }

        container.addEventListener('change', function (event) {
            const select = event.target.closest('[data-crm-related-contact-select="true"]');
            if (!select || !container.contains(select)) {
                return;
            }

            handleSelectChange(select);
        });

        container.addEventListener('click', function (event) {
            const button = event.target.closest('button');
            if (!button || !container.contains(button)) {
                return;
            }

            const row = button.closest('[data-crm-related-contact-row="true"]');
            const select = getRowSelect(row);

            if (button.matches('[data-crm-related-contact-primary="true"]')) {
                if (!row || !getRowValue(row)) {
                    return;
                }

                setPrimaryRow(row);
                refreshRows();
                dispatchChanged();
                return;
            }

            if (button.matches('[data-crm-related-contact-open="true"]')) {
                if (!select?.value || !window.crmModalHost?.open) {
                    return;
                }

                const url = buildDetailsUrl(detailsUrlTemplate, select.value);
                if (!url) {
                    return;
                }

                window.crmModalHost.open(url, {
                    title: openTitle,
                    iconClass: 'bi bi-box-arrow-up-right me-2',
                    size: 'xl',
                    reloadOnComplete: false
                });
                return;
            }

            if (button.matches('[data-crm-related-contact-create="true"]')) {
                if (!window.crmModalHost?.open || !createUrl || !select) {
                    return;
                }

                window.crmModalHost.open(createUrl, {
                    title: createTitle,
                    iconClass: 'bi bi-person-plus me-2',
                    size: 'lg',
                    reloadOnComplete: false,
                    onEntityCreated: data => {
                        if (!entityCodesMatch(data?.entityCode, 'Contact')) {
                            return false;
                        }

                        ensureSelectOption(select, data.id, data.name, true);
                        if (row && !String(primaryInput?.value || '').trim()) {
                            row.dataset.crmPrimary = 'true';
                        }

                        updateSelectName(select);
                        refreshRows();
                        dispatchChanged();
                        return true;
                    }
                });
                return;
            }

            if (button.matches('[data-crm-related-contact-remove="true"]')) {
                if (!row) {
                    return;
                }

                row.remove();
                refreshRows();
                dispatchChanged();
            }
        });

        addButton.addEventListener('click', function () {
            appendRow();
            dispatchChanged();
        });

        window.crmSearchableSelect?.initSearchableSelects(container, {
            onChange: changedSelect => handleSelectChange(changedSelect)
        });
        refreshRows();
    }

    function initRelatedContactsFields(root) {
        (root || document).querySelectorAll('[data-crm-related-contacts="true"]').forEach(initRelatedContactsField);
    }

    document.addEventListener('DOMContentLoaded', function () {
        initRelatedContactsFields(document);
    });

    window.crmRelatedContacts = {
        init: initRelatedContactsFields
    };
})();
