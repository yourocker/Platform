import { FormApiClient } from './api.js?v=20260325b';

export const fieldManager = {
    appId: null,
    fields: [],
    entityLinkType: 7,
    selectType: 9,

    init(appId) {
        this.appId = appId;
        this.bindEvents();
    },

    bindEvents() {
        const form = document.getElementById('createFieldForm');
        if (form) {
            form.onsubmit = async (e) => {
                e.preventDefault();
                const formData = new FormData(form);
                const data = {
                    appDefinitionId: this.appId,
                    label: formData.get('label'),
                    dataType: parseInt(formData.get('dataType')),
                    targetEntityCode: formData.get('targetEntityCode') || null,
                    selectOptions: this.collectSelectOptions(form),
                    isRequired: form.querySelector('[name="isRequired"]').checked,
                    isArray: form.querySelector('[name="isArray"]').checked
                };
                try {
                    await FormApiClient.createField(data);
                    bootstrap.Modal.getInstance(document.getElementById('addFieldModal')).hide();
                    this.resetCreateFieldForm(form);
                    await this.loadFields();
                    window.layoutDesigner.render();
                } catch (err) { alert(err.message); }
            };
        }

        const modalElement = document.getElementById('addFieldModal');
        if (modalElement) {
            modalElement.addEventListener('hidden.bs.modal', () => {
                if (form) {
                    this.resetCreateFieldForm(form);
                }
            });
        }

        this.toggleFieldConfigSections();
    },

    async loadFields() {
        try {
            this.fields = await FormApiClient.getFields(this.appId);
            this.renderPalette();
        } catch (e) { console.error("Ошибка API:", e); }
    },

    renderPalette() {
        const sysList = document.getElementById('systemFieldsList');
        const userList = document.getElementById('userFieldsList');
        if (!sysList || !userList) return;

        sysList.innerHTML = '';
        userList.innerHTML = '';

        this.fields.sort((a, b) => a.label.localeCompare(b.label)).forEach(f => {
            const isPlaced = !!document.querySelector(`[data-field-id="${f.id}"]`);
            const el = document.createElement('div');
            el.className = `palette-field ${isPlaced ? 'is-placed' : ''}`;
            el.dataset.id = f.id;
            el.dataset.type = 'field';
            el.innerHTML = `<i class="bi bi-grip-vertical me-2 opacity-50"></i>${f.label}`;

            (f.isSystem ? sysList : userList).appendChild(el);
        });
    },

    openCreateFieldModal() {
        const form = document.getElementById('createFieldForm');
        if (form) {
            this.resetCreateFieldForm(form);
        }
        const modal = new bootstrap.Modal(document.getElementById('addFieldModal'));
        modal.show();
    },

    filterPalette(query) {
        const q = query.toLowerCase();
        document.querySelectorAll('.palette-field').forEach(el => {
            el.style.display = el.innerText.toLowerCase().includes(q) ? 'flex' : 'none';
        });
    },

    collectSelectOptions(form) {
        return Array.from(form.querySelectorAll('#builderSelectOptionsContainer > div'))
            .map(row => ({
                value: row.querySelector('[name="optionValues"]')?.value || '',
                label: row.querySelector('[name="optionLabels"]')?.value || ''
            }))
            .filter(option => option.label.trim().length > 0);
    },

    addSelectOptionRow(label = '', value = '') {
        const container = document.getElementById('builderSelectOptionsContainer');
        if (!container) return;

        const row = document.createElement('div');
        row.className = 'd-flex align-items-center gap-2';

        const valueInput = document.createElement('input');
        valueInput.type = 'hidden';
        valueInput.name = 'optionValues';
        valueInput.value = value;

        const labelInput = document.createElement('input');
        labelInput.type = 'text';
        labelInput.name = 'optionLabels';
        labelInput.className = 'form-control form-control-sm';
        labelInput.placeholder = 'Название пункта';
        labelInput.value = label;

        const removeButton = document.createElement('button');
        removeButton.type = 'button';
        removeButton.className = 'btn btn-sm btn-outline-danger';
        removeButton.innerHTML = '<i class="bi bi-x-lg"></i>';
        removeButton.addEventListener('click', () => row.remove());

        row.appendChild(valueInput);
        row.appendChild(labelInput);
        row.appendChild(removeButton);
        container.appendChild(row);
    },

    toggleFieldConfigSections() {
        const dataTypeSelect = document.getElementById('builderFieldDataTypeSelect');
        const targetWrapper = document.getElementById('builderTargetEntityWrapper');
        const targetSelect = document.getElementById('builderTargetEntitySelect');
        const optionsWrapper = document.getElementById('builderSelectOptionsWrapper');
        const optionsContainer = document.getElementById('builderSelectOptionsContainer');
        if (!dataTypeSelect || !targetWrapper || !targetSelect || !optionsWrapper || !optionsContainer) return;

        const selectedType = parseInt(dataTypeSelect.value, 10);
        const isEntityLink = selectedType === this.entityLinkType;
        const isSelect = selectedType === this.selectType;

        targetWrapper.style.display = isEntityLink ? 'block' : 'none';
        targetSelect.required = isEntityLink;
        if (!isEntityLink) {
            targetSelect.value = '';
        }

        optionsWrapper.style.display = isSelect ? 'block' : 'none';
        if (isSelect) {
            if (optionsContainer.children.length === 0) {
                this.addSelectOptionRow();
            }
        } else {
            optionsContainer.innerHTML = '';
        }
    },

    resetCreateFieldForm(form) {
        form.reset();
        const optionsContainer = form.querySelector('#builderSelectOptionsContainer');
        if (optionsContainer) {
            optionsContainer.innerHTML = '';
        }
        this.toggleFieldConfigSections();
    }
};
