import { FormApiClient } from './api.js';
import { fieldManager } from './field-manager.js';
import { layoutDesigner } from './designer.js';

// Главный контроллер
const formBuilder = {
    init() {
        const data = window.formConfigData;
        if (!data) return;

        fieldManager.init(data.appId);
        layoutDesigner.init(data);

        // Начальная загрузка
        fieldManager.loadFields();
        layoutDesigner.switchMode('Create');
    },

    async saveCurrentLayout() {
        if (layoutDesigner.currentMode) {
            layoutDesigner.config.layouts[layoutDesigner.currentMode] = layoutDesigner.layout;
        }

        const mode = layoutDesigner.currentMode;
        const formId = window.formConfigData.formIds[mode];
        const layout = layoutDesigner.layout;

        const btn = document.querySelector('#builderModal .btn-primary');
        const oldHtml = btn.innerHTML;
        btn.disabled = true;
        btn.innerHTML = '<span class="spinner-border spinner-border-sm"></span>';

        try {
            const res = await FormApiClient.saveLayout(formId, JSON.stringify(layout));
            if (res.ok) {
                const status = document.getElementById('saveStatus');
                status.innerText = 'Сохранено';
                status.classList.add('show', 'text-success');
                setTimeout(() => status.classList.remove('show'), 2000);
            } else {
                alert('Ошибка сохранения');
            }
        } catch (e) {
            alert(e.message);
        } finally {
            btn.disabled = false;
            btn.innerHTML = oldHtml;
        }
    }
};

// === EXPOSE TO GLOBAL SCOPE ===
// Чтобы работали onclick="..." в HTML
window.formBuilder = formBuilder;
window.fieldManager = fieldManager;
window.layoutDesigner = layoutDesigner;

// Запуск
formBuilder.init();