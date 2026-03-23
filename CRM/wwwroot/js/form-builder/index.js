import { fieldManager } from './field-manager.js?v=20260319a';
import { layoutDesigner } from './designer.js?v=20260319a';
import { FormApiClient } from './api.js?v=20260319a';

const EMPTY_LAYOUT = '{"nodes":[]}';
const MODE_KEYS = ['Create', 'Edit', 'View'];
const FORM_TYPE_MAP = { Create: 0, Edit: 1, View: 2 };

const formBuilder = {
    currentMode: 'Create',
    appId: null,
    pendingSave: false,
    formsByMode: {
        Create: { forms: [], selectedFormId: null },
        Edit: { forms: [], selectedFormId: null },
        View: { forms: [], selectedFormId: null }
    },

    async init(appId, forms, initialMode) {
        this.appId = appId;
        this.ensureApiClient();
        fieldManager.init(appId);
        await fieldManager.loadFields();

        this.seedForms(forms);

        if (MODE_KEYS.includes(initialMode)) {
            this.currentMode = initialMode;
        }

        this.bindCreateFormModal();
        this.bindRenameFormModal();
        this.switchMode(this.currentMode);
    },

    ensureApiClient() {
        if (!FormApiClient || typeof FormApiClient !== 'object') return;
        const baseUrl = FormApiClient.baseUrl || '/api/FormConfig';

        if (typeof FormApiClient.createForm !== 'function') {
            FormApiClient.createForm = async (data) => {
                const response = await fetch(`${baseUrl}/CreateForm`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(data)
                });
                if (!response.ok) {
                    const err = await response.text();
                    throw new Error(err || 'Ошибка создания формы');
                }
                return await response.json();
            };
        }

        if (typeof FormApiClient.renameForm !== 'function') {
            FormApiClient.renameForm = async (data) => {
                const response = await fetch(`${baseUrl}/RenameForm`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(data)
                });
                if (!response.ok) {
                    const err = await response.text();
                    throw new Error(err || 'Ошибка переименования формы');
                }
                return await response.json();
            };
        }

        if (typeof FormApiClient.deleteForm !== 'function') {
            FormApiClient.deleteForm = async (id) => {
                const response = await fetch(`${baseUrl}/DeleteForm?id=${id}`, { method: 'POST' });
                if (!response.ok) {
                    const err = await response.text();
                    throw new Error(err || 'Ошибка удаления формы');
                }
                return response;
            };
        }

        if (typeof FormApiClient.setDefaultForm !== 'function') {
            FormApiClient.setDefaultForm = async (id) => {
                const response = await fetch(`${baseUrl}/SetDefaultForm?id=${id}`, { method: 'POST' });
                if (!response.ok) {
                    const err = await response.text();
                    throw new Error(err || 'Ошибка назначения основной формы');
                }
                return response;
            };
        }
    },

    seedForms(forms) {
        MODE_KEYS.forEach((mode) => {
            this.formsByMode[mode] = { forms: [], selectedFormId: null };
        });

        if (Array.isArray(forms)) {
            forms.forEach((form) => {
                if (!MODE_KEYS.includes(form.type)) return;
                this.formsByMode[form.type].forms.push({
                    id: form.id,
                    name: form.name,
                    type: form.type,
                    isDefault: !!form.isDefault,
                    layoutJson: form.layoutJson || EMPTY_LAYOUT
                });
            });
        }

        MODE_KEYS.forEach((mode) => {
            this.ensureSelectedForm(mode);
        });
    },

    bindCreateFormModal() {
        const form = document.getElementById('createFormForm');
        if (!form) return;
        form.addEventListener('submit', async (e) => {
            e.preventDefault();
            const formData = new FormData(form);
            const name = String(formData.get('name') || '').trim();
            if (!name) return;
            await this.createForm(name);
            form.reset();
            this.hideModal('createFormModal');
        });
    },

    bindRenameFormModal() {
        const form = document.getElementById('renameFormForm');
        if (!form) return;
        form.addEventListener('submit', async (e) => {
            e.preventDefault();
            const currentForm = this.getSelectedForm();
            if (!currentForm) return;
            const formData = new FormData(form);
            const name = String(formData.get('name') || '').trim();
            if (!name) return;
            await this.renameCurrentForm(name);
            this.hideModal('renameFormModal');
        });
    },

    parseLayout(layoutJson) {
        let layout = { nodes: [] };
        try {
            if (layoutJson && layoutJson !== '') {
                layout = typeof layoutJson === 'string' ? JSON.parse(layoutJson) : layoutJson;
            }
        } catch (e) {
            console.error('Ошибка парсинга Layout:', e);
        }

        return layout;
    },

    normalizeMode(mode) {
        if (typeof mode === 'number') return MODE_KEYS[mode] || 'Create';
        if (MODE_KEYS.includes(mode)) return mode;
        return 'Create';
    },

    getModeData(mode = this.currentMode) {
        return this.formsByMode[mode] || { forms: [], selectedFormId: null };
    },

    getSelectedForm(mode = this.currentMode) {
        const modeData = this.getModeData(mode);
        if (!modeData.selectedFormId) return null;
        return modeData.forms.find((f) => String(f.id) === String(modeData.selectedFormId)) || null;
    },

    ensureSelectedForm(mode) {
        const modeData = this.getModeData(mode);
        if (modeData.selectedFormId && modeData.forms.some((f) => String(f.id) === String(modeData.selectedFormId))) {
            return this.getSelectedForm(mode);
        }

        const defaultForm = modeData.forms.find((f) => f.isDefault);
        const fallback = defaultForm || modeData.forms[0] || null;
        modeData.selectedFormId = fallback ? fallback.id : null;
        return fallback;
    },

    renderFormSelector() {
        const selector = document.getElementById('formSelector');
        if (!selector) return;

        const modeData = this.getModeData(this.currentMode);
        selector.innerHTML = '';

        if (modeData.forms.length === 0) {
            const emptyOption = document.createElement('option');
            emptyOption.textContent = 'Нет форм';
            emptyOption.value = '';
            selector.appendChild(emptyOption);
            selector.disabled = true;
        } else {
            selector.disabled = false;
            modeData.forms.forEach((form) => {
                const option = document.createElement('option');
                const defaultLabel = form.isDefault ? ' ★' : '';
                option.value = form.id;
                option.textContent = `${form.name}${defaultLabel}`;
                selector.appendChild(option);
            });

            if (!modeData.selectedFormId) {
                this.ensureSelectedForm(this.currentMode);
            }
            selector.value = modeData.selectedFormId || '';
        }

        this.updateActionButtons();
    },

    updateActionButtons() {
        const setDefaultBtn = document.getElementById('setDefaultBtn');
        const deleteFormBtn = document.getElementById('deleteFormBtn');
        const renameFormBtn = document.getElementById('renameFormBtn');
        const currentForm = this.getSelectedForm();

        if (setDefaultBtn) {
            setDefaultBtn.disabled = !currentForm || currentForm.isDefault;
        }
        if (deleteFormBtn) {
            deleteFormBtn.disabled = !currentForm || currentForm.isDefault;
        }
        if (renameFormBtn) {
            renameFormBtn.disabled = !currentForm;
        }
    },

    switchMode(mode) {
        if (!MODE_KEYS.includes(mode)) return;
        this.currentMode = mode;

        const radio = document.getElementById(`mode${mode}`);
        if (radio) {
            radio.checked = true;
        }

        const selectedForm = this.ensureSelectedForm(mode);
        this.renderFormSelector();

        if (selectedForm) {
            const parsedLayout = this.parseLayout(selectedForm.layoutJson);
            layoutDesigner.init(selectedForm.id, parsedLayout, this.appId, mode);
        } else {
            layoutDesigner.init(null, { nodes: [] }, this.appId, mode);
        }
    },

    switchForm(formId) {
        const modeData = this.getModeData(this.currentMode);
        if (!modeData) return;

        modeData.selectedFormId = formId || null;
        const selectedForm = this.ensureSelectedForm(this.currentMode);
        this.renderFormSelector();

        if (selectedForm) {
            const parsedLayout = this.parseLayout(selectedForm.layoutJson);
            layoutDesigner.init(selectedForm.id, parsedLayout, this.appId, this.currentMode);
        } else {
            layoutDesigner.init(null, { nodes: [] }, this.appId, this.currentMode);
        }
    },

    async createForm(name) {
        try {
            const typeValue = FORM_TYPE_MAP[this.currentMode] ?? 0;
            const result = await FormApiClient.createForm({
                appDefinitionId: this.appId,
                name,
                type: typeValue
            });

            const modeData = this.getModeData(this.currentMode);
            const isDefault = modeData.forms.length === 0;
            const newForm = {
                id: result.id,
                name,
                type: this.currentMode,
                isDefault,
                layoutJson: EMPTY_LAYOUT
            };

            modeData.forms.push(newForm);
            modeData.selectedFormId = newForm.id;
            this.renderFormSelector();
            if (this.pendingSave) {
                this.pendingSave = false;
                layoutDesigner.formId = newForm.id;
                layoutDesigner.formType = this.currentMode;
                layoutDesigner.save();
            } else {
                layoutDesigner.init(newForm.id, this.parseLayout(newForm.layoutJson), this.appId, this.currentMode);
            }
        } catch (e) {
            alert(e.message || 'Ошибка создания формы');
        }
    },

    async deleteCurrentForm() {
        const currentForm = this.getSelectedForm();
        if (!currentForm) return;
        if (currentForm.isDefault) {
            alert('Нельзя удалить основную форму.');
            return;
        }
        if (!confirm(`Удалить форму "${currentForm.name}"?`)) return;

        try {
            await FormApiClient.deleteForm(currentForm.id);
            const modeData = this.getModeData(this.currentMode);
            modeData.forms = modeData.forms.filter((f) => String(f.id) !== String(currentForm.id));
            modeData.selectedFormId = null;
            this.switchMode(this.currentMode);
        } catch (e) {
            alert(e.message || 'Ошибка удаления формы');
        }
    },

    async setDefaultCurrentForm() {
        const currentForm = this.getSelectedForm();
        if (!currentForm || currentForm.isDefault) return;
        try {
            await FormApiClient.setDefaultForm(currentForm.id);
            const modeData = this.getModeData(this.currentMode);
            modeData.forms.forEach((f) => {
                f.isDefault = String(f.id) === String(currentForm.id);
            });
            modeData.selectedFormId = currentForm.id;
            this.renderFormSelector();
        } catch (e) {
            alert(e.message || 'Ошибка назначения основной формы');
        }
    },

    openCreateFormModal() {
        this.showModal('createFormModal');
    },

    openRenameFormModal() {
        const currentForm = this.getSelectedForm();
        if (!currentForm) return;
        const modalEl = document.getElementById('renameFormModal');
        if (!modalEl) return;
        const input = modalEl.querySelector('input[name="name"]');
        if (input) input.value = currentForm.name;
        this.showModal('renameFormModal');
    },

    async renameCurrentForm(name) {
        const currentForm = this.getSelectedForm();
        if (!currentForm) return;
        try {
            await FormApiClient.renameForm({ id: currentForm.id, name });
            currentForm.name = name;
            this.renderFormSelector();
        } catch (e) {
            alert(e.message || 'Ошибка переименования формы');
        }
    },

    showModal(id) {
        const modalEl = document.getElementById(id);
        if (!modalEl || !window.bootstrap?.Modal) return;

        const openCount = document.querySelectorAll('.modal.show').length;
        const baseZIndex = 1050 + openCount * 20;
        modalEl.style.zIndex = baseZIndex + 10;

        const modal = window.bootstrap.Modal.getOrCreateInstance(modalEl, {
            backdrop: 'static',
            focus: true,
            keyboard: true
        });
        modal.show();

        const backdrops = document.querySelectorAll('.modal-backdrop');
        const lastBackdrop = backdrops[backdrops.length - 1];
        if (lastBackdrop) {
            lastBackdrop.style.zIndex = baseZIndex;
        }
    },

    hideModal(id) {
        const modalEl = document.getElementById(id);
        if (!modalEl || !window.bootstrap?.Modal) return;
        const modal = window.bootstrap.Modal.getInstance(modalEl);
        modal?.hide();
    },

    onLayoutSaved({ formId, formType, layoutJson }) {
        const mode = this.normalizeMode(formType);
        const modeData = this.getModeData(mode);
        let target = modeData.forms.find((f) => String(f.id) === String(formId));

        if (!target && formId) {
            const isDefault = modeData.forms.length === 0 || !modeData.forms.some((f) => f.isDefault);
            target = {
                id: formId,
                name: 'Основная форма',
                type: mode,
                isDefault,
                layoutJson: layoutJson || EMPTY_LAYOUT
            };
            modeData.forms.push(target);
        }

        if (target) {
            target.layoutJson = layoutJson || EMPTY_LAYOUT;
            modeData.selectedFormId = target.id;
        }

        if (this.currentMode === mode) {
            this.renderFormSelector();
        }
    },

    saveCurrentLayout() {
        const currentForm = this.getSelectedForm();
        if (!currentForm) {
            this.pendingSave = true;
            this.openCreateFormModal();
            return;
        }
        layoutDesigner.save();
    }
};

// Глобальный доступ для onclick и инициализации
window.formBuilder = formBuilder;
window.fieldManager = fieldManager;
window.layoutDesigner = layoutDesigner;
