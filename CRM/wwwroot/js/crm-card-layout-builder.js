(function () {
    const DEFAULT_SECTION_TITLE = 'Новый раздел';
    const ENTITY_LINK_TYPE = 'EntityLink';
    const SELECT_TYPE = 'Select';
    const BOOLEAN_TYPE = 'Boolean';
    const FILE_TYPE = 'File';
    const TEXT_TYPE = 'Text';
    const NUMBER_TYPE = 'Number';
    const MONEY_TYPE = 'Money';
    const DATE_TYPE = 'Date';
    const DATETIME_TYPE = 'DateTime';
    const CARD_LAYOUT_STATE_URL = '/api/CrmCardLayouts/GetState';
    const CARD_LAYOUT_SAVE_URL = '/api/CrmCardLayouts/Save';
    const FORM_CONFIG_CREATE_FIELD_URL = '/api/FormConfig/CreateField';

    function toLower(value) {
        return String(value || '').trim().toLowerCase();
    }

    function entityCodesMatch(left, right) {
        const normalize = value => String(value || '').trim().replace(/s$/i, '').toLowerCase();
        return normalize(left) && normalize(left) === normalize(right);
    }

    function buildIdentity(kind, key, fieldId) {
        return toLower(kind) === 'dynamic' && fieldId
            ? `dynamic:${String(fieldId).toLowerCase()}`
            : `system:${String(key || '').trim().toLowerCase()}`;
    }

    function parseJsonScript(scriptId) {
        const script = document.getElementById(scriptId);
        if (!script) {
            return null;
        }

        try {
            return JSON.parse(script.textContent || '{}');
        } catch (error) {
            console.error('Не удалось прочитать payload layout карточки', error);
            return null;
        }
    }

    function ensureArray(value) {
        return Array.isArray(value) ? value : [];
    }

    function ensureObject(value) {
        return value && typeof value === 'object' ? value : {};
    }

    function createSectionId() {
        if (window.crypto && typeof window.crypto.randomUUID === 'function') {
            return window.crypto.randomUUID().replace(/-/g, '');
        }

        return `section_${Date.now()}_${Math.random().toString(16).slice(2)}`;
    }

    function normalizeSectionTitle(value) {
        const title = String(value || '').trim();
        return title || DEFAULT_SECTION_TITLE;
    }

    function createUniqueSectionId(seenIds, proposedId) {
        let nextId = String(proposedId || '').trim();
        if (!nextId || seenIds.has(nextId)) {
            do {
                nextId = createSectionId();
            } while (seenIds.has(nextId));
        }

        seenIds.add(nextId);
        return nextId;
    }

    function sanitizeLayout(layout) {
        const schema = ensureObject(layout);
        const seenSectionIds = new Set();
        const sections = [];

        ensureArray(schema.sections).forEach(section => {
            const title = normalizeSectionTitle(section?.title);
            const items = ensureArray(section?.items)
                .map(item => {
                    const kind = toLower(item?.kind) === 'dynamic' ? 'dynamic' : 'system';
                    const key = item?.key ? String(item.key) : null;
                    const fieldId = item?.fieldId ? String(item.fieldId) : null;
                    const identity = buildIdentity(kind, key, fieldId);

                    if ((kind === 'dynamic' && !fieldId) || identity === 'system:') {
                        return null;
                    }

                    return {
                        kind,
                        key,
                        fieldId
                    };
                })
                .filter(Boolean);

            if (items.length === 0 && title === DEFAULT_SECTION_TITLE) {
                return;
            }

            sections.push({
                id: createUniqueSectionId(seenSectionIds, section?.id),
                title,
                items
            });
        });

        return { sections };
    }

    function dispatchFormMutation(source) {
        const form = source?.closest('form');
        if (!form) {
            return;
        }

        form.dispatchEvent(new Event('change', { bubbles: true }));
    }

    function readAntiforgeryToken() {
        return document.querySelector('input[name="__RequestVerificationToken"]')?.value
            || window.parent?.document?.querySelector('input[name="__RequestVerificationToken"]')?.value
            || '';
    }

    function escapeHtml(value) {
        return String(value ?? '')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    function createOptionMarkup(options, selectedValue, placeholder) {
        const parts = [];
        parts.push(`<option value="">${escapeHtml(placeholder || '-- Выберите --')}</option>`);

        ensureArray(options).forEach(option => {
            const value = String(option?.value || '');
            const text = String(option?.text || option?.label || value);
            const isSelected = value && value === String(selectedValue || '');
            parts.push(`<option value="${escapeHtml(value)}" ${isSelected ? 'selected' : ''}>${escapeHtml(text)}</option>`);
        });

        if (selectedValue && !ensureArray(options).some(option => String(option?.value || '') === String(selectedValue))) {
            parts.push(`<option value="${escapeHtml(selectedValue)}" selected>${escapeHtml(selectedValue)}</option>`);
        }

        return parts.join('');
    }

    function initSearchableWithin(root, onChange) {
        window.crmSearchableSelect?.initSearchableSelects(root, {
            onChange: select => {
                dispatchFormMutation(select);
                if (typeof onChange === 'function') {
                    onChange(select);
                }
            }
        });
    }

    function refreshSearchableWithin(root, onChange) {
        Array.from(root?.querySelectorAll?.('select.searchable-select') || []).forEach(select => {
            window.crmSearchableSelect?.refreshSearchableSelect(select, {
                onChange: changedSelect => {
                    dispatchFormMutation(changedSelect);
                    if (typeof onChange === 'function') {
                        onChange(changedSelect);
                    }
                }
            });
        });
    }

    function refreshDynamicFieldRemoveButtons(systemName) {
        document.querySelectorAll(`[data-dynamic-field-remove="${systemName}"]`).forEach(button => {
            const container = document.getElementById(`container_${systemName}`);
            if (!container) {
                return;
            }

            const items = Array.from(container.querySelectorAll(`[data-dynamic-field-item="${systemName}"]`));
            button.classList.toggle('d-none', items.length <= 1);
        });
    }

    function buildDynamicFieldControlHtml(field, value, state) {
        const formKey = `DynamicProps[${field.systemName}]`;
        const requiredAttr = field.isRequired && !field.isArray ? 'required' : '';
        const createUrl = state.dynamicFieldCreateUrls[field.systemName] || '';
        const lookupOptions = state.lookupData[field.systemName] || field.selectOptions || [];
        let controlMarkup = '';

        switch (field.dataType) {
            case ENTITY_LINK_TYPE:
            case SELECT_TYPE:
                controlMarkup = `
                    <select name="${escapeHtml(formKey)}"
                            class="form-control searchable-select"
                            data-search-placeholder="Поиск..."
                            ${requiredAttr}>
                        ${createOptionMarkup(lookupOptions, value, '-- Выберите --')}
                    </select>`;
                break;
            case NUMBER_TYPE:
                controlMarkup = `<input type="number" step="any" name="${escapeHtml(formKey)}" value="${escapeHtml(String(value || '').replace(',', '.'))}" class="form-control" ${requiredAttr} />`;
                break;
            case MONEY_TYPE:
                controlMarkup = `<input type="number" step="0.01" name="${escapeHtml(formKey)}" value="${escapeHtml(String(value || '').replace(',', '.'))}" class="form-control" placeholder="0.00" ${requiredAttr} />`;
                break;
            case DATE_TYPE:
                controlMarkup = `<input type="date" name="${escapeHtml(formKey)}" value="${escapeHtml(value || '')}" class="form-control" ${requiredAttr} />`;
                break;
            case DATETIME_TYPE:
                controlMarkup = `<input type="datetime-local" name="${escapeHtml(formKey)}" value="${escapeHtml(value || '')}" class="form-control" ${requiredAttr} />`;
                break;
            case FILE_TYPE:
                controlMarkup = `<input type="file" name="${escapeHtml(formKey)}" class="form-control" ${requiredAttr} />`;
                break;
            case BOOLEAN_TYPE:
                controlMarkup = `
                    <div class="form-check form-switch mt-2">
                        <input type="hidden" name="${escapeHtml(formKey)}" value="false" />
                        <input class="form-check-input" type="checkbox" name="${escapeHtml(formKey)}" value="true" ${String(value || '').toLowerCase() === 'true' ? 'checked' : ''} />
                        <label class="form-check-label">Да</label>
                    </div>`;
                break;
            case TEXT_TYPE:
                controlMarkup = `<textarea name="${escapeHtml(formKey)}" class="form-control" rows="3" ${requiredAttr}>${escapeHtml(value || '')}</textarea>`;
                break;
            default:
                controlMarkup = `<input type="text" name="${escapeHtml(formKey)}" value="${escapeHtml(value || '')}" class="form-control" ${requiredAttr} />`;
                break;
        }

        const dynamicCreateButton = field.dataType === ENTITY_LINK_TYPE && field.targetEntityCode && createUrl
            ? `<button type="button"
                        class="btn btn-outline-secondary ms-2"
                        title="Создать связанный объект"
                        data-crm-dynamic-create="true"
                        data-create-url="${escapeHtml(createUrl)}"
                        data-target-entity-code="${escapeHtml(field.targetEntityCode)}"
                        data-title="${escapeHtml(field.label)}">
                    <i class="bi bi-plus-lg"></i>
               </button>`
            : '';

        return `
            <div class="dynamic-field-control">
                ${controlMarkup}
                ${dynamicCreateButton}
                ${field.isArray ? `
                    <button type="button"
                            class="btn btn-outline-danger dynamic-field-remove"
                            data-dynamic-field-remove="${escapeHtml(field.systemName)}"
                            onclick="removeDynamicFieldItem(this, '${escapeHtml(field.systemName)}')">
                        <i class="bi bi-x"></i>
                    </button>` : ''}
            </div>`;
    }

    function buildDynamicFieldRowHtml(field, value, state) {
        return `
            <div class="mb-2 dynamic-field-item" data-dynamic-field-item="${escapeHtml(field.systemName)}">
                ${buildDynamicFieldControlHtml(field, value, state)}
            </div>`;
    }

    function buildDynamicFieldMarkup(field, state) {
        const addMoreButton = field.isArray
            ? `<button type="button"
                        class="btn btn-sm btn-outline-secondary mt-1"
                        onclick="addDynamicField('${escapeHtml(field.systemName)}', '${escapeHtml(field.dataType)}')">
                    <i class="bi bi-plus-lg"></i> Добавить еще
               </button>`
            : '';

        return `
            <div class="crm-card-layout-field">
                <label class="form-label text-muted fw-bold">
                    ${escapeHtml(field.label)}
                    ${field.isRequired ? '<span class="text-danger">*</span>' : ''}
                </label>
                <div id="container_${escapeHtml(field.systemName)}" class="dynamic-field-container">
                    ${buildDynamicFieldRowHtml(field, '', state)}
                </div>
                ${addMoreButton}
            </div>`;
    }

    function registerDynamicField(field, state) {
        if (!field?.systemName) {
            return;
        }

        state.fieldsBySystemName[field.systemName] = field;
    }

    function bindDynamicCreateButtons(scope, state) {
        Array.from(scope?.querySelectorAll?.('[data-crm-dynamic-create="true"]') || []).forEach(button => {
            if (!(button instanceof HTMLButtonElement) || button.dataset.crmDynamicCreateBound === 'true') {
                return;
            }

            button.dataset.crmDynamicCreateBound = 'true';
            button.addEventListener('click', function () {
                const item = button.closest('[data-dynamic-field-item]');
                const selectElement = item?.querySelector('select[name], input[list]');
                const createUrl = String(button.dataset.createUrl || '').trim();
                const targetEntityCode = String(button.dataset.targetEntityCode || '').trim();

                if (!createUrl || !targetEntityCode || !window.crmModalHost?.open) {
                    return;
                }

                window.crmModalHost.open(createUrl, {
                    title: button.dataset.title ? `Новый объект: ${button.dataset.title}` : 'Новый связанный объект',
                    iconClass: 'bi bi-plus-circle me-2',
                    size: 'lg',
                    reloadOnComplete: false,
                    onEntityCreated: data => {
                        if (!entityCodesMatch(data?.entityCode, targetEntityCode)) {
                            return false;
                        }

                        if (selectElement instanceof HTMLSelectElement) {
                            let option = Array.from(selectElement.options).find(item => item.value === String(data.id));
                            if (!option) {
                                option = document.createElement('option');
                                option.value = String(data.id);
                                selectElement.appendChild(option);
                            }

                            option.textContent = data.name || data.id;
                            selectElement.value = String(data.id);
                            selectElement.dispatchEvent(new Event('change', { bubbles: true }));
                        }

                        dispatchFormMutation(selectElement || button);
                        return true;
                    }
                });
            });
        });
    }

    function buildDynamicItemElement(field, state) {
        registerDynamicField(field, state);

        const item = document.createElement('div');
        item.className = 'crm-card-layout__item';
        item.dataset.itemKind = 'dynamic';
        item.dataset.fieldId = String(field.id || '');
        item.dataset.itemKey = '';
        item.dataset.itemIdentity = buildIdentity('dynamic', '', field.id);
        item.dataset.itemLabel = field.label || item.dataset.itemIdentity;
        item.innerHTML = `
            <div class="crm-card-layout-builder-toolbar">
                <span class="crm-card-layout-builder-handle" title="Переместить">
                    <i class="bi bi-grip-vertical"></i>
                </span>
                <button type="button"
                        class="btn btn-sm btn-link text-danger p-0"
                        title="Скрыть поле"
                        data-crm-card-layout-remove-item="true"
                        data-item-kind="dynamic"
                        data-item-key=""
                        data-field-id="${escapeHtml(String(field.id || ''))}">
                    <i class="bi bi-eye-slash"></i>
                </button>
            </div>
            ${buildDynamicFieldMarkup(field, state)}`;

        bindDynamicCreateButtons(item, state);
        refreshDynamicFieldRemoveButtons(field.systemName);
        return item;
    }

    async function deleteExistingFileInternal(id, entityCode, propertyName, filePath, wrapperId) {
        const token = readAntiforgeryToken();
        const formData = new FormData();
        formData.append('id', id);
        formData.append('entityCode', entityCode);
        formData.append('propertyName', propertyName);
        formData.append('filePath', filePath);

        const response = await fetch('/files/delete-property', {
            method: 'POST',
            body: formData,
            headers: token ? { RequestVerificationToken: token } : {}
        });

        const payload = await response.json().catch(() => null);
        if (!response.ok || payload?.success === false) {
            throw new Error(payload?.message || 'Не удалось удалить файл.');
        }

        const wrapper = document.getElementById(wrapperId);
        wrapper?.querySelector('.d-flex.align-items-center')?.remove();
        dispatchFormMutation(wrapper || document.body);
    }

    function initGlobalDynamicFieldHelpers() {
        if (window.addDynamicField && window.removeDynamicFieldItem && window.deleteExistingFile) {
            return;
        }

        window.removeDynamicFieldItem = function (button, systemName) {
            const item = button?.closest('.dynamic-field-item');
            const container = document.getElementById(`container_${systemName}`);
            item?.remove();

            if (container && container.querySelectorAll(`[data-dynamic-field-item="${systemName}"]`).length === 0) {
                const registry = window.crmCardLayoutDynamicFieldRegistry || {};
                const field = registry[systemName];
                if (field) {
                    container.insertAdjacentHTML('beforeend', buildDynamicFieldRowHtml(field, '', {
                        lookupData: window.crmCardLayoutLookupRegistry || {},
                        dynamicFieldCreateUrls: window.crmCardLayoutCreateUrlRegistry || {}
                    }));
                    initSearchableWithin(container);
                    bindDynamicCreateButtons(container, {
                        dynamicFieldCreateUrls: window.crmCardLayoutCreateUrlRegistry || {}
                    });
                }
            }

            refreshDynamicFieldRemoveButtons(systemName);
            dispatchFormMutation(container || button);
        };

        window.addDynamicField = function (systemName) {
            const container = document.getElementById(`container_${systemName}`);
            const registry = window.crmCardLayoutDynamicFieldRegistry || {};
            const field = registry[systemName];
            if (!container || !field) {
                return;
            }

            container.insertAdjacentHTML('beforeend', buildDynamicFieldRowHtml(field, '', {
                lookupData: window.crmCardLayoutLookupRegistry || {},
                dynamicFieldCreateUrls: window.crmCardLayoutCreateUrlRegistry || {}
            }));
            initSearchableWithin(container);
            bindDynamicCreateButtons(container, {
                dynamicFieldCreateUrls: window.crmCardLayoutCreateUrlRegistry || {}
            });
            refreshDynamicFieldRemoveButtons(systemName);
            dispatchFormMutation(container);
        };

        window.deleteExistingFile = function (id, entityCode, propertyName, filePath, wrapperId) {
            if (!window.confirm('Удалить этот файл?')) {
                return;
            }

            deleteExistingFileInternal(id, entityCode, propertyName, filePath, wrapperId)
                .catch(error => window.alert(error instanceof Error ? error.message : 'Не удалось удалить файл.'));
        };
    }

    function createSectionElement(section) {
        const element = document.createElement('section');
        element.className = 'card border-0 shadow-sm mb-3 crm-card-layout__section';
        element.dataset.sectionId = section.id;
        element.innerHTML = `
            <div class="card-body p-4">
                <div class="crm-card-layout__section-header">
                    <div class="crm-card-layout__section-title">${escapeHtml(normalizeSectionTitle(section.title))}</div>
                    <div class="crm-card-layout__section-tools">
                        <button type="button"
                                class="btn btn-sm btn-link text-secondary p-0"
                                title="Выбрать раздел"
                                data-crm-card-layout-select-section="true"
                                data-section-id="${escapeHtml(section.id)}">
                            <i class="bi bi-cursor"></i>
                        </button>
                        <button type="button"
                                class="btn btn-sm btn-link text-secondary p-0"
                                title="Переименовать раздел"
                                data-crm-card-layout-rename-section="true"
                                data-section-id="${escapeHtml(section.id)}">
                            <i class="bi bi-pencil-square"></i>
                        </button>
                        <span class="crm-card-layout-builder-handle" title="Переместить раздел">
                            <i class="bi bi-grip-vertical"></i>
                        </span>
                        <button type="button"
                                class="btn btn-sm btn-link text-danger p-0"
                                title="Удалить раздел"
                                data-crm-card-layout-remove-section="true"
                                data-section-id="${escapeHtml(section.id)}">
                            <i class="bi bi-trash"></i>
                        </button>
                    </div>
                </div>
                <div class="crm-card-layout__items"
                     data-crm-card-layout-items="true"
                     data-section-id="${escapeHtml(section.id)}"></div>
            </div>`;
        return element;
    }

    function normalizeField(field) {
        if (!field || !field.id) {
            return null;
        }

        return {
            id: String(field.id),
            label: String(field.label || ''),
            systemName: String(field.systemName || ''),
            dataType: String(field.dataType || ''),
            isRequired: Boolean(field.isRequired),
            isArray: Boolean(field.isArray),
            targetEntityCode: field.targetEntityCode ? String(field.targetEntityCode) : '',
            sortOrder: Number(field.sortOrder || 0),
            selectOptions: ensureArray(field.selectOptions).map(option => ({
                value: String(option?.value || ''),
                label: String(option?.label || option?.text || option?.value || '')
            }))
        };
    }

    function normalizeStatePayload(payload) {
        const normalized = payload && typeof payload === 'object' ? payload : {};
        return {
            appDefinitionId: String(normalized.appDefinitionId || ''),
            pipelineId: String(normalized.pipelineId || ''),
            entityCode: String(normalized.entityCode || ''),
            layout: sanitizeLayout(normalized.layout),
            palette: ensureArray(normalized.palette).map(item => ({
                identity: String(item?.identity || buildIdentity(item?.kind, item?.key, item?.fieldId)),
                kind: String(item?.kind || ''),
                key: item?.key ? String(item.key) : '',
                fieldId: item?.fieldId ? String(item.fieldId) : '',
                label: String(item?.label || ''),
                isBuiltIn: Boolean(item?.isBuiltIn)
            })),
            fields: ensureArray(normalized.fields).map(normalizeField).filter(Boolean),
            lookupData: Object.fromEntries(
                Object.entries(ensureObject(normalized.lookupData)).map(([key, options]) => [
                    key,
                    ensureArray(options).map(option => ({
                        value: String(option?.value || ''),
                        text: String(option?.text || option?.label || option?.value || '')
                    }))
                ])
            ),
            dynamicFieldCreateUrls: Object.fromEntries(
                Object.entries(ensureObject(normalized.dynamicFieldCreateUrls)).map(([key, value]) => [key, String(value || '')])
            ),
            definitions: ensureArray(normalized.definitions)
        };
    }

    function resetCreateFieldForm(container) {
        if (!container) {
            return;
        }

        container.querySelectorAll('input, select, textarea').forEach(field => {
            if (!(field instanceof HTMLInputElement || field instanceof HTMLSelectElement || field instanceof HTMLTextAreaElement)) {
                return;
            }

            if (field instanceof HTMLInputElement) {
                switch (field.type) {
                    case 'checkbox':
                    case 'radio':
                        field.checked = false;
                        break;
                    default:
                        field.value = '';
                        break;
                }

                return;
            }

            if (field instanceof HTMLSelectElement) {
                field.selectedIndex = 0;
                return;
            }

            field.value = '';
        });
    }

    function initRoot(root) {
        if (!root || root.dataset.crmCardLayoutInitialized === 'true') {
            return;
        }

        const prefix = root.dataset.prefix || 'crm-layout';
        const payload = normalizeStatePayload(parseJsonScript(`${prefix}-crm-card-layout-payload`));
        const sectionsContainer = root.querySelector('[data-crm-card-layout-sections="true"]');
        const stash = root.querySelector('[data-crm-card-layout-stash="true"]');
        const builder = root.querySelector('[data-crm-card-layout-builder="true"]');
        const paletteContainer = root.querySelector('[data-crm-card-layout-palette="true"]');
        const selectedSectionLabel = root.querySelector('[data-crm-card-layout-selected-section-label="true"]');
        const layoutToggleButton = root.querySelector('[data-crm-card-layout-toggle="true"]');
        const createFieldForm = document.getElementById('crmCardCreateFieldForm');
        const createFieldModalElement = document.getElementById('crmCardCreateFieldModal');
        const dataTypeSelect = document.getElementById('crmCardFieldDataTypeSelect');
        const targetWrapper = document.getElementById('crmCardTargetEntityWrapper');
        const targetSelect = document.getElementById('crmCardTargetEntitySelect');
        const optionsWrapper = document.getElementById('crmCardSelectOptionsWrapper');
        const optionsContainer = document.getElementById('crmCardSelectOptionsContainer');

        if (!sectionsContainer || !stash || !builder || !paletteContainer) {
            return;
        }

        const state = {
            root,
            prefix,
            sectionsContainer,
            stash,
            builder,
            paletteContainer,
            selectedSectionLabel,
            layoutToggleButton,
            createFieldForm,
            createFieldModalElement,
            dataTypeSelect,
            targetWrapper,
            targetSelect,
            optionsWrapper,
            optionsContainer,
            appDefinitionId: payload.appDefinitionId,
            pipelineId: payload.pipelineId,
            entityCode: payload.entityCode,
            palette: payload.palette,
            fields: payload.fields,
            fieldsById: {},
            fieldsBySystemName: {},
            lookupData: payload.lookupData,
            dynamicFieldCreateUrls: payload.dynamicFieldCreateUrls,
            definitions: payload.definitions,
            selectedSectionId: '',
            savedLayoutJson: '',
            sectionSortable: null,
            itemSortables: [],
            createFieldModal: createFieldModalElement && window.bootstrap?.Modal
                ? new window.bootstrap.Modal(createFieldModalElement)
                : null
        };

        state.fields.forEach(field => {
            state.fieldsById[field.id] = field;
            registerDynamicField(field, state);
        });

        window.crmCardLayoutDynamicFieldRegistry = Object.assign(window.crmCardLayoutDynamicFieldRegistry || {}, state.fieldsBySystemName);
        window.crmCardLayoutLookupRegistry = Object.assign(window.crmCardLayoutLookupRegistry || {}, state.lookupData);
        window.crmCardLayoutCreateUrlRegistry = Object.assign(window.crmCardLayoutCreateUrlRegistry || {}, state.dynamicFieldCreateUrls);

        function getCurrentPipelineId() {
            const pipelineInput = document.querySelector('select[name="PipelineId"], input[name="PipelineId"]');
            return String(pipelineInput?.value || state.pipelineId || '');
        }

        function getSectionElements() {
            return Array.from(sectionsContainer.querySelectorAll('[data-section-id]'));
        }

        function getItemElements() {
            return Array.from(root.querySelectorAll('.crm-card-layout__item'));
        }

        function findItemByIdentity(identity) {
            return getItemElements().find(item => item.dataset.itemIdentity === identity) || null;
        }

        function getSelectedSectionElement() {
            return state.selectedSectionId
                ? sectionsContainer.querySelector(`[data-section-id="${CSS.escape(state.selectedSectionId)}"]`)
                : null;
        }

        function getItemsContainer(sectionId) {
            const section = sectionId
                ? sectionsContainer.querySelector(`[data-section-id="${CSS.escape(sectionId)}"]`)
                : null;
            return section?.querySelector('[data-crm-card-layout-items="true"]') || null;
        }

        function syncEmptySectionState() {
            getSectionElements().forEach(section => {
                const items = section.querySelectorAll('.crm-card-layout__item');
                section.classList.toggle('is-empty', items.length === 0);
                section.classList.toggle('is-selected', section.dataset.sectionId === state.selectedSectionId);
            });
        }

        function ensureSelectedSection() {
            const sections = getSectionElements();
            if (sections.length === 0) {
                state.selectedSectionId = '';
                if (selectedSectionLabel) {
                    selectedSectionLabel.textContent = 'Сначала добавьте раздел, затем поля';
                }
                return;
            }

            if (!state.selectedSectionId || !sections.some(section => section.dataset.sectionId === state.selectedSectionId)) {
                state.selectedSectionId = sections[0].dataset.sectionId || '';
            }

            if (selectedSectionLabel) {
                const activeSection = getSelectedSectionElement();
                const title = activeSection?.querySelector('.crm-card-layout__section-title')?.textContent?.trim() || 'Не выбран';
                selectedSectionLabel.textContent = `Выбран раздел: ${title}`;
            }

            syncEmptySectionState();
        }

        function serializeLayout() {
            return sanitizeLayout({
                sections: getSectionElements().map(section => ({
                    id: section.dataset.sectionId,
                    title: normalizeSectionTitle(section.querySelector('.crm-card-layout__section-title')?.textContent),
                    items: Array.from(section.querySelectorAll(':scope [data-crm-card-layout-items="true"] > .crm-card-layout__item')).map(item => ({
                        kind: item.dataset.itemKind,
                        key: item.dataset.itemKey || null,
                        fieldId: item.dataset.fieldId || null
                    }))
                }))
            });
        }

        function rememberSavedLayout(layout) {
            state.savedLayoutJson = JSON.stringify(sanitizeLayout(layout || serializeLayout()));
        }

        function refreshPalette() {
            const placedIdentities = new Set(
                getSectionElements().flatMap(section =>
                    Array.from(section.querySelectorAll(':scope [data-crm-card-layout-items="true"] > .crm-card-layout__item'))
                        .map(item => item.dataset.itemIdentity)
                        .filter(Boolean))
            );

            paletteContainer.innerHTML = '';

            state.palette.forEach(item => {
                const paletteItem = document.createElement('div');
                const isPlaced = placedIdentities.has(item.identity);
                const canAdd = !isPlaced && !!state.selectedSectionId;
                paletteItem.className = `crm-card-layout__palette-item${isPlaced ? ' is-placed' : ''}`;
                paletteItem.innerHTML = `
                    <span>${escapeHtml(item.label)}</span>
                    <button type="button"
                            class="btn btn-sm ${isPlaced ? 'btn-light border' : 'btn-outline-primary'}"
                            data-crm-card-layout-add-item="true"
                            data-item-identity="${escapeHtml(item.identity)}"
                            ${canAdd ? '' : 'disabled'}>
                        ${isPlaced ? 'В карточке' : 'Добавить'}
                    </button>`;
                paletteContainer.appendChild(paletteItem);
            });
        }

        function initSortables() {
            if (typeof window.Sortable === 'undefined') {
                return;
            }

            state.itemSortables.forEach(sortable => sortable.destroy());
            state.itemSortables = [];

            if (state.sectionSortable) {
                state.sectionSortable.destroy();
            }

            state.sectionSortable = window.Sortable.create(sectionsContainer, {
                animation: 150,
                handle: '.crm-card-layout__section-header .crm-card-layout-builder-handle',
                draggable: '.crm-card-layout__section',
                filter: 'button, button *, input, select, textarea, a',
                preventOnFilter: false,
                onEnd: () => {
                    ensureSelectedSection();
                    refreshPalette();
                }
            });

            getSectionElements().forEach(section => {
                const itemsContainer = section.querySelector('[data-crm-card-layout-items="true"]');
                if (!itemsContainer) {
                    return;
                }

                const sortable = window.Sortable.create(itemsContainer, {
                    group: 'crm-card-layout-items',
                    animation: 150,
                    handle: '.crm-card-layout-builder-toolbar .crm-card-layout-builder-handle',
                    draggable: '.crm-card-layout__item',
                    filter: 'button, button *, input, select, textarea, a',
                    preventOnFilter: false,
                    onAdd: () => {
                        syncEmptySectionState();
                        refreshPalette();
                    },
                    onEnd: () => {
                        syncEmptySectionState();
                        refreshPalette();
                    }
                });

                state.itemSortables.push(sortable);
            });
        }

        function moveAllItemsToStash() {
            getSectionElements().forEach(section => {
                const itemsContainer = section.querySelector('[data-crm-card-layout-items="true"]');
                if (!itemsContainer) {
                    return;
                }

                Array.from(itemsContainer.children).forEach(item => {
                    if (item.classList.contains('crm-card-layout__item')) {
                        stash.appendChild(item);
                    }
                });
            });
        }

        function findOrCreateItem(identity) {
            const existing = findItemByIdentity(identity);
            if (existing) {
                return existing;
            }

            const [kind, value] = String(identity || '').split(':');
            if (toLower(kind) !== 'dynamic' || !value) {
                return null;
            }

            const field = state.fieldsById[String(value)];
            if (!field) {
                return null;
            }

            const item = buildDynamicItemElement(field, state);
            stash.appendChild(item);
            return item;
        }

        function applyLayout(layout) {
            const schema = sanitizeLayout(layout);
            moveAllItemsToStash();
            sectionsContainer.innerHTML = '';

            ensureArray(schema.sections).forEach(section => {
                const sectionId = String(section?.id || createSectionId());
                const sectionTitle = normalizeSectionTitle(section?.title);
                const sectionElement = createSectionElement({ id: sectionId, title: sectionTitle });
                const itemsContainer = sectionElement.querySelector('[data-crm-card-layout-items="true"]');
                sectionsContainer.appendChild(sectionElement);

                ensureArray(section?.items).forEach(item => {
                    const identity = buildIdentity(item?.kind, item?.key, item?.fieldId);
                    const itemElement = findOrCreateItem(identity);
                    if (itemElement && itemsContainer) {
                        itemsContainer.appendChild(itemElement);
                    }
                });
            });

            ensureSelectedSection();
            initSortables();
            refreshPalette();
            refreshSearchableWithin(sectionsContainer);
            window.crmRelatedContacts?.init(sectionsContainer);
        }

        function addSection(title) {
            const section = createSectionElement({
                id: createSectionId(),
                title: normalizeSectionTitle(title)
            });
            sectionsContainer.appendChild(section);
            state.selectedSectionId = section.dataset.sectionId || '';
            ensureSelectedSection();
            initSortables();
            refreshPalette();
        }

        function hideItem(item) {
            if (!item) {
                return;
            }

            stash.appendChild(item);
            syncEmptySectionState();
            refreshPalette();
        }

        function addPaletteItem(identity) {
            if (!identity || !state.selectedSectionId) {
                return;
            }

            const targetContainer = getItemsContainer(state.selectedSectionId);
            const item = findOrCreateItem(identity);
            if (!targetContainer || !item) {
                return;
            }

            targetContainer.appendChild(item);
            syncEmptySectionState();
            refreshPalette();
            refreshSearchableWithin(item);
            window.crmRelatedContacts?.init(item);
        }

        async function loadPipelineState(pipelineId) {
            const normalizedPipelineId = String(pipelineId || '').trim();
            if (!normalizedPipelineId) {
                return null;
            }

            const response = await fetch(`${CARD_LAYOUT_STATE_URL}?pipelineId=${encodeURIComponent(normalizedPipelineId)}`, {
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'
                }
            });

            const payload = await response.json().catch(() => null);
            if (!response.ok || payload?.success === false) {
                throw new Error(payload?.message || 'Не удалось загрузить конфигурацию карточки.');
            }

            return normalizeStatePayload(payload);
        }

        async function reloadStateForPipeline(pipelineId) {
            const nextState = await loadPipelineState(pipelineId);
            if (!nextState) {
                return;
            }

            state.pipelineId = nextState.pipelineId;
            state.entityCode = nextState.entityCode;
            state.palette = nextState.palette;
            state.lookupData = nextState.lookupData;
            state.dynamicFieldCreateUrls = nextState.dynamicFieldCreateUrls;

            nextState.fields.forEach(field => {
                state.fieldsById[field.id] = field;
                state.fieldsBySystemName[field.systemName] = field;
            });

            window.crmCardLayoutDynamicFieldRegistry = Object.assign(window.crmCardLayoutDynamicFieldRegistry || {}, state.fieldsBySystemName);
            window.crmCardLayoutLookupRegistry = Object.assign(window.crmCardLayoutLookupRegistry || {}, state.lookupData);
            window.crmCardLayoutCreateUrlRegistry = Object.assign(window.crmCardLayoutCreateUrlRegistry || {}, state.dynamicFieldCreateUrls);

            applyLayout(nextState.layout);
            rememberSavedLayout(nextState.layout);
        }

        async function saveLayout() {
            const layout = serializeLayout();
            const response = await fetch(CARD_LAYOUT_SAVE_URL, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'X-Requested-With': 'XMLHttpRequest'
                },
                body: JSON.stringify({
                    pipelineId: getCurrentPipelineId(),
                    layoutJson: JSON.stringify(layout)
                })
            });

            const payload = await response.json().catch(() => null);
            if (!response.ok || payload?.success === false) {
                throw new Error(payload?.message || 'Не удалось сохранить layout карточки.');
            }

            rememberSavedLayout(payload?.layout ? payload.layout : layout);
            window.alert('Layout карточки сохранён.');
        }

        function resetCreateFieldOptions() {
            if (optionsContainer) {
                optionsContainer.innerHTML = '';
            }
        }

        function addCreateFieldOptionRow(label, value) {
            if (!optionsContainer) {
                return;
            }

            const row = document.createElement('div');
            row.className = 'd-flex align-items-center gap-2';
            row.innerHTML = `
                <input type="hidden" name="optionValues" value="${escapeHtml(value || '')}" />
                <input type="text" name="optionLabels" class="form-control form-control-sm" placeholder="Название пункта" value="${escapeHtml(label || '')}" />
                <button type="button" class="btn btn-sm btn-outline-danger">
                    <i class="bi bi-x-lg"></i>
                </button>`;
            row.querySelector('button')?.addEventListener('click', () => row.remove());
            optionsContainer.appendChild(row);
        }

        function toggleCreateFieldConfigSections() {
            const dataType = Number.parseInt(String(dataTypeSelect?.value || ''), 10);
            const isEntityLink = dataType === 7;
            const isSelect = dataType === 9;

            if (targetWrapper && targetSelect) {
                targetWrapper.classList.toggle('d-none', !isEntityLink);
                targetSelect.required = isEntityLink;
                if (!isEntityLink) {
                    targetSelect.value = '';
                }
            }

            if (optionsWrapper) {
                optionsWrapper.classList.toggle('d-none', !isSelect);
                if (isSelect && optionsContainer && optionsContainer.children.length === 0) {
                    addCreateFieldOptionRow('', '');
                }
                if (!isSelect) {
                    resetCreateFieldOptions();
                }
            }
        }

        async function handleCreateFieldSubmit(event) {
            event.preventDefault();
            if (!createFieldForm) {
                return;
            }

            const request = {
                appDefinitionId: state.appDefinitionId,
                label: String(createFieldForm.querySelector('[name="label"]')?.value || '').trim(),
                dataType: Number.parseInt(String(createFieldForm.querySelector('[name="dataType"]')?.value || ''), 10),
                targetEntityCode: String(createFieldForm.querySelector('[name="targetEntityCode"]')?.value || '').trim() || null,
                isRequired: createFieldForm.querySelector('[name="isRequired"]')?.checked === true,
                isArray: createFieldForm.querySelector('[name="isArray"]')?.checked === true,
                selectOptions: ensureArray(Array.from(createFieldForm.querySelectorAll('#crmCardSelectOptionsContainer > div')).map(row => ({
                    value: row.querySelector('[name="optionValues"]')?.value || '',
                    label: row.querySelector('[name="optionLabels"]')?.value || ''
                }))).filter(option => String(option.label || '').trim().length > 0)
            };

            if (!request.label) {
                const labelField = createFieldForm.querySelector('[name="label"]');
                if (labelField instanceof HTMLInputElement || labelField instanceof HTMLTextAreaElement) {
                    labelField.focus();
                }

                throw new Error('Укажите заголовок поля.');
            }

            const response = await fetch(FORM_CONFIG_CREATE_FIELD_URL, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'X-Requested-With': 'XMLHttpRequest'
                },
                body: JSON.stringify(request)
            });

            const payload = await response.json().catch(() => null);
            if (!response.ok || payload?.success === false || !payload?.id) {
                throw new Error(payload?.message || 'Не удалось создать поле.');
            }

            await reloadStateForPipeline(getCurrentPipelineId());

            const createdField = state.fieldsById[String(payload.id)];
            if (createdField) {
                const identity = buildIdentity('dynamic', '', createdField.id);
                if (!state.palette.some(item => item.identity === identity)) {
                    state.palette.push({
                        identity,
                        kind: 'dynamic',
                        key: '',
                        fieldId: createdField.id,
                        label: createdField.label,
                        isBuiltIn: false
                    });
                }

                const item = findOrCreateItem(identity) || buildDynamicItemElement(createdField, state);
                stash.appendChild(item);
                if (state.selectedSectionId) {
                    addPaletteItem(identity);
                }
            }

            refreshPalette();
            resetCreateFieldForm(createFieldForm);
            resetCreateFieldOptions();
            toggleCreateFieldConfigSections();
            state.createFieldModal?.hide();
        }

        root.addEventListener('click', async function (event) {
            const button = event.target.closest('button');
            if (!button || !root.contains(button)) {
                return;
            }

            if (button.matches('[data-crm-card-layout-toggle="true"]')) {
                root.classList.toggle('is-builder-active');
                ensureSelectedSection();
                initSortables();
                refreshPalette();
                return;
            }

            if (button.matches('[data-crm-card-layout-add-section="true"]')) {
                const title = window.prompt('Название нового раздела:', DEFAULT_SECTION_TITLE);
                if (title === null) {
                    return;
                }

                addSection(title);
                return;
            }

            if (button.matches('[data-crm-card-layout-select-section="true"]')) {
                const section = button.closest('[data-section-id]');
                state.selectedSectionId = String(section?.dataset.sectionId || button.dataset.sectionId || '');
                ensureSelectedSection();
                refreshPalette();
                return;
            }

            if (button.matches('[data-crm-card-layout-rename-section="true"]')) {
                const section = button.closest('[data-section-id]');
                const titleElement = section?.querySelector('.crm-card-layout__section-title');
                if (!section || !titleElement) {
                    return;
                }

                const nextTitle = window.prompt('Новое название раздела:', normalizeSectionTitle(titleElement.textContent));
                if (nextTitle === null) {
                    return;
                }

                titleElement.textContent = normalizeSectionTitle(nextTitle);
                ensureSelectedSection();
                return;
            }

            if (button.matches('[data-crm-card-layout-remove-section="true"]')) {
                const section = button.closest('[data-section-id]');
                if (!section) {
                    return;
                }

                if (!window.confirm('Удалить раздел? Поля из него останутся доступными в палитре.')) {
                    return;
                }

                const itemsContainer = section.querySelector('[data-crm-card-layout-items="true"]');
                Array.from(itemsContainer?.children || []).forEach(item => {
                    if (item.classList.contains('crm-card-layout__item')) {
                        stash.appendChild(item);
                    }
                });
                section.remove();
                ensureSelectedSection();
                initSortables();
                refreshPalette();
                return;
            }

            if (button.matches('[data-crm-card-layout-remove-item="true"]')) {
                hideItem(button.closest('.crm-card-layout__item'));
                return;
            }

            if (button.matches('[data-crm-card-layout-add-item="true"]')) {
                addPaletteItem(String(button.dataset.itemIdentity || ''));
                return;
            }

            if (button.matches('[data-crm-card-layout-save="true"]')) {
                try {
                    button.disabled = true;
                    await saveLayout();
                } catch (error) {
                    window.alert(error instanceof Error ? error.message : 'Не удалось сохранить layout карточки.');
                } finally {
                    button.disabled = false;
                }
                return;
            }

            if (button.matches('[data-crm-card-layout-cancel="true"]')) {
                const savedLayout = JSON.parse(state.savedLayoutJson || '{"sections":[]}');
                applyLayout(savedLayout);
                rememberSavedLayout(savedLayout);
                return;
            }

            if (button.matches('[data-crm-card-layout-open-create-field="true"]')) {
                state.createFieldModal?.show();
                return;
            }

            if (button.matches('[data-crm-card-add-select-option="true"]')) {
                addCreateFieldOptionRow('', '');
                return;
            }

            if (button.matches('[data-crm-card-create-field-submit="true"]')) {
                handleCreateFieldSubmit(new Event('submit')).catch(error => {
                    window.alert(error instanceof Error ? error.message : 'Не удалось создать поле.');
                });
            }
        });

        if (dataTypeSelect) {
            dataTypeSelect.addEventListener('change', toggleCreateFieldConfigSections);
            toggleCreateFieldConfigSections();
        }

        createFieldModalElement?.addEventListener('click', function (event) {
            const button = event.target.closest('button');
            if (!button || !createFieldModalElement.contains(button)) {
                return;
            }

            if (button.matches('[data-crm-card-add-select-option="true"]')) {
                addCreateFieldOptionRow('', '');
                return;
            }

            if (button.matches('[data-crm-card-create-field-submit="true"]')) {
                handleCreateFieldSubmit(new Event('submit')).catch(error => {
                    window.alert(error instanceof Error ? error.message : 'Не удалось создать поле.');
                });
            }
        });

        createFieldModalElement?.addEventListener('hidden.bs.modal', function () {
            resetCreateFieldForm(createFieldForm);
            resetCreateFieldOptions();
            toggleCreateFieldConfigSections();
        });

        const pipelineField = document.querySelector('select[name="PipelineId"]');
        if (pipelineField instanceof HTMLSelectElement) {
            pipelineField.addEventListener('change', function () {
                reloadStateForPipeline(pipelineField.value).catch(error => {
                    window.alert(error instanceof Error ? error.message : 'Не удалось обновить layout для выбранной воронки.');
                });
            });
        }

        initSearchableWithin(sectionsContainer);
        window.crmRelatedContacts?.init(sectionsContainer);
        rememberSavedLayout(serializeLayout());
        ensureSelectedSection();
        refreshPalette();
        initSortables();
        initGlobalDynamicFieldHelpers();
        root.dataset.crmCardLayoutInitialized = 'true';
    }

    document.addEventListener('DOMContentLoaded', function () {
        document.querySelectorAll('[data-crm-card-layout-root="true"]').forEach(initRoot);
    });

    window.crmCardLayoutBuilder = {
        init(root) {
            initRoot(root || document.querySelector('[data-crm-card-layout-root="true"]'));
        }
    };
})();
