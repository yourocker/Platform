import { fieldManager } from './field-manager.js';
import { layoutDesigner } from './designer.js';

const formBuilder = {
    async init(appId, formId, layoutJson) {
        fieldManager.init(appId);
        await fieldManager.loadFields();

        let layout = { nodes: [] };
        try {
            if (layoutJson && layoutJson !== "") {
                layout = JSON.parse(layoutJson);
            }
        } catch (e) { console.error("Ошибка парсинга Layout:", e); }

        layoutDesigner.init(formId, layout);
    },

    saveCurrentLayout() {
        layoutDesigner.save();
    }
};

// Глобальный доступ для onclick и инициализации
window.formBuilder = formBuilder;
window.fieldManager = fieldManager;
window.layoutDesigner = layoutDesigner;