import { fieldManager } from './field-manager.js';
import { layoutDesigner } from './designer.js';

const formBuilder = {
    currentMode: 'Create',
    formsByMode: {
        Create: { formId: null, layoutJson: '{"nodes":[]}' },
        Edit: { formId: null, layoutJson: '{"nodes":[]}' },
        View: { formId: null, layoutJson: '{"nodes":[]}' }
    },

    async init(appId, formId, layoutJson) {
        this.appId = appId;
        fieldManager.init(appId);
        await fieldManager.loadFields();

        if (typeof formId === 'object' && formId !== null) {
            this.formsByMode = {
                ...this.formsByMode,
                ...formId
            };
        } else {
            this.formsByMode[this.currentMode] = {
                formId,
                layoutJson
            };
        }

        if (typeof layoutJson === 'string' && ['Create', 'Edit', 'View'].includes(layoutJson)) {
            this.currentMode = layoutJson;
        }

        this.switchMode(this.currentMode);
    },

    parseLayout(layoutJson) {
        let layout = { nodes: [] };
        try {
            if (layoutJson && layoutJson !== "") {
                layout = typeof layoutJson === 'string' ? JSON.parse(layoutJson) : layoutJson;
            }
        } catch (e) {
            console.error('Ошибка парсинга Layout:', e);
        }

        return layout;
    },

    switchMode(mode) {
        if (!['Create', 'Edit', 'View'].includes(mode)) {
            return;
        }

        this.currentMode = mode;
        const modeForm = this.formsByMode[mode] || { formId: null, layoutJson: '{"nodes":[]}' };
        const parsedLayout = this.parseLayout(modeForm.layoutJson);

        const selector = document.getElementById('formSelector');
        if (selector) {
            const optionToSelect = Array.from(selector.options).find((option) => option.dataset.mode === mode);
            if (optionToSelect) {
                selector.value = optionToSelect.value;
            }
        }

        const radio = document.getElementById(`mode${mode}`);
        if (radio) {
            radio.checked = true;
        }

        layoutDesigner.init(modeForm.formId, parsedLayout);
    },

    switchForm(formId) {
        const selector = document.getElementById('formSelector');
        const selectedOption = selector?.options[selector.selectedIndex];
        const selectedMode = selectedOption?.dataset?.mode;

        if (selectedMode && this.formsByMode[selectedMode]) {
            this.formsByMode[selectedMode].formId = formId || null;
            this.switchMode(selectedMode);
            return;
        }

        const mode = Object.keys(this.formsByMode).find((key) => {
            const currentFormId = this.formsByMode[key]?.formId;
            return String(currentFormId || '') === String(formId || '');
        });

        if (mode) {
            this.switchMode(mode);
        }
    },

    saveCurrentLayout() {
        layoutDesigner.save();
    }
};

// Глобальный доступ для onclick и инициализации
window.formBuilder = formBuilder;
window.fieldManager = fieldManager;
window.layoutDesigner = layoutDesigner;
