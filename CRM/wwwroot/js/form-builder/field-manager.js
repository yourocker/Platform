import { FormApiClient } from './api.js';

export const fieldManager = {
    appId: null,
    fields: [],

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
                    isRequired: form.querySelector('[name="isRequired"]').checked,
                    isArray: form.querySelector('[name="isArray"]').checked
                };
                try {
                    await FormApiClient.createField(data);
                    bootstrap.Modal.getInstance(document.getElementById('addFieldModal')).hide();
                    form.reset();
                    await this.loadFields();
                    window.layoutDesigner.render();
                } catch (err) { alert(err.message); }
            };
        }
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
        const modal = new bootstrap.Modal(document.getElementById('addFieldModal'));
        modal.show();
    },

    filterPalette(query) {
        const q = query.toLowerCase();
        document.querySelectorAll('.palette-field').forEach(el => {
            el.style.display = el.innerText.toLowerCase().includes(q) ? 'flex' : 'none';
        });
    }
};