import { FormApiClient } from './api.js';
import { fieldManager } from './field-manager.js';

export const layoutDesigner = {
    layout: { nodes: [] },
    formId: null,
    canvas: null,

    init(formId, layout) {
        this.formId = formId;
        this.layout = layout || { nodes: [] };
        this.canvas = document.getElementById('formCanvas');
        this.initSortable();
        this.render();
    },

    initSortable() {
        const _this = this;
        const options = { group: { name: 'canvas', pull: 'clone', put: false }, sort: false, animation: 150 };

        Sortable.create(document.getElementById('systemFieldsList'), options);
        Sortable.create(document.getElementById('userFieldsList'), options);
        Sortable.create(document.getElementById('markupPalette'), options);

        Sortable.create(this.canvas, {
            group: 'canvas',
            animation: 150,
            draggable: '.canvas-node',
            onAdd: (e) => {
                const node = { type: e.item.dataset.type };
                if (node.type === 'field') node.fieldId = e.item.dataset.id;
                _this.layout.nodes.splice(e.newIndex, 0, node);
                e.item.remove();
                _this.render();
            },
            onEnd: () => this.syncFromDom()
        });
    },

    render() {
        this.canvas.innerHTML = '';
        this.layout.nodes.forEach((node, i) => {
            if (node.type === 'field') {
                const f = fieldManager.fields.find(x => x.id === node.fieldId);
                this.canvas.insertAdjacentHTML('beforeend', `
                    <div class="canvas-node field-node p-2 mb-2 border rounded bg-white d-flex align-items-center" data-field-id="${node.fieldId}">
                        <i class="bi bi-grip-vertical me-2 text-muted node-drag-handle"></i>
                        <span class="flex-grow-1">${f ? f.label : 'Удаленное поле'}</span>
                        <i class="bi bi-x-lg text-danger cursor-pointer" onclick="layoutDesigner.remove(${i})"></i>
                    </div>`);
            }
        });
        fieldManager.renderPalette();
    },

    syncFromDom() {
        const newNodes = [];
        this.canvas.querySelectorAll('.canvas-node').forEach(el => {
            const fid = el.dataset.fieldId;
            if (fid) newNodes.push({ type: 'field', fieldId: fid });
        });
        this.layout.nodes = newNodes;
        fieldManager.renderPalette();
    },

    remove(index) {
        this.layout.nodes.splice(index, 1);
        this.render();
    },

    async save() {
        try {
            const response = await FormApiClient.saveLayout({
                formId: this.formId,
                layoutJson: JSON.stringify(this.layout)
            });

            if (!response.ok) {
                const errorText = await response.text();
                throw new Error(errorText || 'Ошибка сохранения макета');
            }

            const result = await response.json();

            if (result.warning) {
                const shouldForceSave = confirm(`${result.message}\n\nСохранить форму принудительно?`);
                if (!shouldForceSave) return;

                const forceResponse = await FormApiClient.saveLayout({
                    formId: this.formId,
                    layoutJson: JSON.stringify(this.layout),
                    forceSave: true
                });

                if (!forceResponse.ok) {
                    const forceErrorText = await forceResponse.text();
                    throw new Error(forceErrorText || 'Ошибка принудительного сохранения макета');
                }

                const forceResult = await forceResponse.json();
                if (!forceResult.success) {
                    throw new Error(forceResult.message || 'Ошибка принудительного сохранения макета');
                }

                alert(forceResult.message || 'Макет сохранен успешно');
                return;
            }

            if (!result.success) {
                throw new Error(result.message || 'Ошибка сохранения макета');
            }

            alert(result.message || "Макет сохранен успешно");
        } catch (e) { alert(e.message); }
    }
};
