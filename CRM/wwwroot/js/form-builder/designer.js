import { FormApiClient } from './api.js?v=20260319a';
import { fieldManager } from './field-manager.js?v=20260319a';

export const layoutDesigner = {
    layout: { nodes: [] },
    formId: null,
    appDefinitionId: null,
    formType: null,
    canvas: null,
    selectedPath: null,
    activeTabs: {},

    init(formId, layout, appDefinitionId, formType) {
        this.formId = formId || null;
        this.appDefinitionId = appDefinitionId;
        this.formType = formType;
        this.layout = this.normalizeLayout(layout || { nodes: [] });
        this.canvas = document.getElementById('formCanvas');
        this.selectedPath = null;
        if (!this.activeTabs) this.activeTabs = {};
        this.render();
        this.initSortable();
    },

    normalizeLayout(layout) {
        const sourceNodes = this.pick(layout, 'nodes', 'Nodes') || [];
        return { nodes: sourceNodes.map((node) => this.normalizeNode(node)).filter(Boolean) };
    },

    normalizeNode(node) {
        if (!node || !node.type) return null;

        const base = {
            type: node.type,
            CustomLabel: this.pick(node, 'CustomLabel', 'customLabel') || ''
        };

        if (node.type === 'field') {
            return {
                ...base,
                FieldId: this.pick(node, 'FieldId', 'fieldId')
            };
        }

        if (node.type === 'tabControl') {
            const tabs = this.pick(node, 'Tabs', 'tabs') || [];
            return {
                ...base,
                Tabs: tabs.map((tab) => this.normalizeNode({ ...tab, type: 'tab' })).filter(Boolean)
            };
        }

        if (node.type === 'tab') {
            const children = this.pick(node, 'Children', 'children') || [];
            return {
                ...base,
                Title: this.pick(node, 'Title', 'title') || 'Новая вкладка',
                Children: children.map((child) => this.normalizeNode(child)).filter(Boolean)
            };
        }

        if (node.type === 'group') {
            const children = this.pick(node, 'Children', 'children') || [];
            return {
                ...base,
                Title: this.pick(node, 'Title', 'title') || 'Группа',
                IsCollapsed: !!this.pick(node, 'IsCollapsed', 'isCollapsed'),
                Children: children.map((child) => this.normalizeNode(child)).filter(Boolean)
            };
        }

        if (node.type === 'row') {
            const columns = this.pick(node, 'Columns', 'columns') || [];
            return {
                ...base,
                Columns: columns.map((col) => this.normalizeNode({ ...col, type: 'column' })).filter(Boolean)
            };
        }

        if (node.type === 'column') {
            const children = this.pick(node, 'Children', 'children') || [];
            return {
                ...base,
                Width: Number(this.pick(node, 'Width', 'width')) || 12,
                Children: children.map((child) => this.normalizeNode(child)).filter(Boolean)
            };
        }

        return null;
    },

    pick(obj, ...keys) {
        for (const key of keys) {
            if (obj && Object.prototype.hasOwnProperty.call(obj, key)) {
                return obj[key];
            }
        }
        return undefined;
    },

    initSortable() {
        const paletteOptions = { group: { name: 'canvasTree', pull: 'clone', put: false }, sort: false, animation: 150, draggable: '.palette-field, .markup-item' };

        Sortable.create(document.getElementById('systemFieldsList'), paletteOptions);
        Sortable.create(document.getElementById('userFieldsList'), paletteOptions);
        Sortable.create(document.getElementById('markupPalette'), paletteOptions);

        this.rebindDropZones();
    },

    rebindDropZones() {
        this.canvas.querySelectorAll('.drop-zone').forEach((zone) => {
            if (zone._sortable) {
                zone._sortable.destroy();
            }

            zone._sortable = Sortable.create(zone, {
                group: 'canvasTree',
                animation: 150,
                draggable: '.canvas-node',
                filter: '.btn, .btn *, .tab-title, .tab-title *',
                preventOnFilter: false,
                onAdd: (e) => {
                    const path = this.parsePath(e.to.dataset.path);
                    const targetList = this.getListByPath(path);
                    if (!targetList) {
                        e.item.remove();
                        return;
                    }

                    if (e.from.id === 'systemFieldsList' || e.from.id === 'userFieldsList' || e.from.id === 'markupPalette') {
                        const node = this.createNodeByType(e.item.dataset.type, e.item.dataset.id);
                        if (!node) {
                            e.item.remove();
                            return;
                        }
                        targetList.splice(e.newIndex, 0, node);
                        e.item.remove();
                        this.render();
                        return;
                    }

                    this.syncFromDom();
                },
                onEnd: () => {
                    this.syncFromDom();
                }
            });
        });
    },

    createNodeByType(type, fieldId) {
        if (type === 'field') {
            if (!fieldId) return null;
            return { type: 'field', FieldId: fieldId, CustomLabel: '' };
        }

        if (type === 'tabControl') {
            return {
                type: 'tabControl',
                CustomLabel: '',
                Tabs: [{ type: 'tab', Title: 'Новая вкладка', CustomLabel: '', Children: [] }]
            };
        }

        if (type === 'tab') {
            return { type: 'tab', Title: 'Новая вкладка', CustomLabel: '', Children: [] };
        }

        if (type === 'group') {
            return { type: 'group', Title: 'Группа', IsCollapsed: false, CustomLabel: '', Children: [] };
        }

        if (type === 'row') {
            return {
                type: 'row',
                CustomLabel: '',
                Columns: [
                    { type: 'column', Width: 6, CustomLabel: '', Children: [] },
                    { type: 'column', Width: 6, CustomLabel: '', Children: [] }
                ]
            };
        }

        if (type === 'column') {
            return { type: 'column', Width: 12, CustomLabel: '', Children: [] };
        }

        return null;
    },

    render() {
        this.canvas.innerHTML = `
            <div class="drop-zone border rounded p-3 bg-white" data-path="nodes">
                ${this.renderNodes(this.layout.nodes, ['nodes'])}
            </div>`;

        this.rebindDropZones();
        this.renderSettingsPanel();
        fieldManager.renderPalette();
    },

    renderNodes(nodes, path) {
        return (nodes || []).map((node, i) => this.renderNode(node, [...path, i])).join('');
    },

    renderNode(node, path) {
        const pathValue = path.join('.');
        const isSelected = this.selectedPath === pathValue;
        const selectedClass = isSelected ? 'border-primary shadow-sm' : '';

        if (node.type === 'field') {
            const f = fieldManager.fields.find((x) => String(x.id) === String(node.FieldId));
            const title = node.CustomLabel || (f ? f.label : 'Удаленное поле');
            return `
                <div class="canvas-node field-node p-2 mb-2 border rounded bg-white d-flex align-items-center ${selectedClass}" data-type="field" data-field-id="${node.FieldId || ''}" data-path="${pathValue}">
                    <i class="bi bi-grip-vertical me-2 text-muted node-drag-handle"></i>
                    <span class="flex-grow-1" onclick="layoutDesigner.selectNode('${pathValue}')">${title}</span>
                    <button class="btn btn-sm btn-link text-secondary p-0 me-2" onclick="layoutDesigner.selectNode('${pathValue}')"><i class="bi bi-sliders"></i></button>
                    <button class="btn btn-sm btn-link text-danger p-0" onclick="layoutDesigner.removeByPath('${pathValue}')"><i class="bi bi-x-lg"></i></button>
                </div>`;
        }

        if (node.type === 'group') {
            const bodyClass = node.IsCollapsed ? 'd-none' : '';
            return `
                <div class="canvas-node group-node border rounded bg-white mb-3 ${selectedClass}" data-type="group" data-path="${pathValue}">
                    <div class="p-2 border-bottom d-flex align-items-center bg-warning-subtle">
                        <i class="bi bi-grip-vertical me-2 text-muted node-drag-handle"></i>
                        <strong class="flex-grow-1" onclick="layoutDesigner.selectNode('${pathValue}')">${node.Title || 'Группа'}</strong>
                        <button class="btn btn-sm btn-link text-secondary p-0 me-2" onclick="layoutDesigner.selectNode('${pathValue}')"><i class="bi bi-sliders"></i></button>
                        <button class="btn btn-sm btn-link text-danger p-0" onclick="layoutDesigner.removeByPath('${pathValue}')"><i class="bi bi-x-lg"></i></button>
                    </div>
                    <div class="p-2 ${bodyClass}">
                        <div class="drop-zone border rounded p-2" data-path="${pathValue}.Children">
                            ${this.renderNodes(node.Children, [...path, 'Children'])}
                        </div>
                    </div>
                </div>`;
        }

        if (node.type === 'column') {
            const colWidth = Math.min(Math.max(Number(node.Width) || 12, 1), 12);
            const columnTitle = (node.CustomLabel && String(node.CustomLabel).trim()) || `Колонка ${colWidth}/12`;
            return `
                <div class="canvas-node column-node col-${colWidth} p-1" data-type="column" data-path="${pathValue}">
                    <div class="border rounded bg-white h-100 ${selectedClass}">
                        <div class="p-2 border-bottom d-flex align-items-center">
                            <i class="bi bi-grip-vertical me-2 text-muted node-drag-handle"></i>
                            <span class="small flex-grow-1" onclick="layoutDesigner.selectNode('${pathValue}')">${columnTitle}</span>
                            <button class="btn btn-sm btn-link text-secondary p-0 me-2" onclick="layoutDesigner.selectNode('${pathValue}')"><i class="bi bi-sliders"></i></button>
                            <button class="btn btn-sm btn-link text-danger p-0" onclick="layoutDesigner.removeByPath('${pathValue}')"><i class="bi bi-x-lg"></i></button>
                        </div>
                        <div class="p-2">
                            <div class="drop-zone border rounded p-2" data-path="${pathValue}.Children">
                                ${this.renderNodes(node.Children, [...path, 'Children'])}
                            </div>
                        </div>
                    </div>
                </div>`;
        }

        if (node.type === 'row') {
            const columns = node.Columns || [];
            const rowTitle = (node.CustomLabel && String(node.CustomLabel).trim()) || 'Строка';
            return `
                <div class="canvas-node row-node border rounded bg-light p-2 mb-3 ${selectedClass}" data-type="row" data-path="${pathValue}">
                    <div class="d-flex align-items-center mb-2">
                        <i class="bi bi-grip-vertical me-2 text-muted node-drag-handle"></i>
                        <strong class="flex-grow-1" onclick="layoutDesigner.selectNode('${pathValue}')">${rowTitle}</strong>
                        <button class="btn btn-sm btn-link text-secondary p-0 me-2" onclick="layoutDesigner.selectNode('${pathValue}')"><i class="bi bi-sliders"></i></button>
                        <button class="btn btn-sm btn-link text-danger p-0" onclick="layoutDesigner.removeByPath('${pathValue}')"><i class="bi bi-x-lg"></i></button>
                    </div>
                    <div class="drop-zone row g-2 m-0" data-path="${pathValue}.Columns">
                        ${columns.map((column, index) => this.renderNode(column, [...path, 'Columns', index])).join('')}
                    </div>
                </div>`;
        }

        if (node.type === 'tab') {
            return `
                <div class="canvas-node tab-node border rounded bg-white mb-2 ${selectedClass}" data-type="tab" data-path="${pathValue}">
                    <div class="p-2 border-bottom d-flex align-items-center bg-info-subtle">
                        <i class="bi bi-grip-vertical me-2 text-muted node-drag-handle"></i>
                        <strong class="flex-grow-1" onclick="layoutDesigner.selectNode('${pathValue}')">${node.Title || 'Вкладка'}</strong>
                        <button class="btn btn-sm btn-link text-secondary p-0 me-2" onclick="layoutDesigner.selectNode('${pathValue}')"><i class="bi bi-sliders"></i></button>
                        <button class="btn btn-sm btn-link text-danger p-0" onclick="layoutDesigner.removeByPath('${pathValue}')"><i class="bi bi-x-lg"></i></button>
                    </div>
                    <div class="p-2">
                        <div class="drop-zone border rounded p-2" data-path="${pathValue}.Children">
                            ${this.renderNodes(node.Children, [...path, 'Children'])}
                        </div>
                    </div>
                </div>`;
        }

        if (node.type === 'tabControl') {
            const tabs = node.Tabs || [];
            const activeIndex = this.getActiveTabIndex(pathValue, tabs);
            const activeTab = tabs[activeIndex] || null;
            return `
                <div class="canvas-node tab-control-node mb-3 ${selectedClass}" data-type="tabControl" data-path="${pathValue}">
                    <div class="tab-designer-header d-flex align-items-center">
                        <i class="bi bi-grip-vertical me-2 text-muted node-drag-handle"></i>
                        <div class="drop-zone tab-strip flex-grow-1" data-path="${pathValue}.Tabs">
                            ${tabs.map((tab, index) => this.renderTabHeader(tab, pathValue, index, index === activeIndex)).join('')}
                        </div>
                        <button class="btn btn-sm btn-outline-primary ms-2" onclick="layoutDesigner.addTab('${pathValue}')" title="Добавить вкладку">+</button>
                        <button class="btn btn-sm btn-link text-danger ms-1" onclick="layoutDesigner.removeByPath('${pathValue}')" title="Удалить блок вкладок">
                            <i class="bi bi-x-lg"></i>
                        </button>
                    </div>
                    <div class="tab-designer-body">
                        <div class="drop-zone tab-content-zone" data-path="${pathValue}.Tabs.${activeIndex}.Children">
                            ${activeTab ? this.renderNodes(activeTab.Children, [...path, 'Tabs', activeIndex, 'Children']) : ''}
                            ${!activeTab ? '<div class="text-muted small">Добавьте вкладку</div>' : ''}
                        </div>
                    </div>
                </div>`;
        }

        return '';
    },

    syncFromDom() {
        const rootZone = this.canvas.querySelector('.drop-zone[data-path="nodes"]');
        if (!rootZone) return;

        this.layout.nodes = this.syncZone(rootZone);
        fieldManager.renderPalette();
        this.renderSettingsPanel();
    },

    syncZone(zone) {
        const nodes = [];
        Array.from(zone.children).forEach((el) => {
            if (!el.classList.contains('canvas-node')) return;
            nodes.push(this.syncNodeElement(el));
        });
        return nodes.filter(Boolean);
    },

    syncNodeElement(el) {
        const type = el.dataset.type;
        if (!type) return null;

        if (type === 'field') {
            const existing = this.getNodeByPath(el.dataset.path) || {};
            return {
                type,
                FieldId: el.dataset.fieldId,
                CustomLabel: existing.CustomLabel || ''
            };
        }

        if (type === 'group') {
            const existing = this.getNodeByPath(el.dataset.path) || {};
            const zone = el.querySelector(':scope .drop-zone[data-path$=".Children"]');
            return {
                type,
                Title: existing.Title || 'Группа',
                IsCollapsed: !!existing.IsCollapsed,
                CustomLabel: existing.CustomLabel || '',
                Children: zone ? this.syncZone(zone) : []
            };
        }

        if (type === 'tab') {
            const existing = this.getNodeByPath(el.dataset.path) || {};
            const zone = el.querySelector(':scope .drop-zone[data-path$=".Children"]');
            return {
                type,
                Title: existing.Title || 'Новая вкладка',
                CustomLabel: existing.CustomLabel || '',
                Children: zone ? this.syncZone(zone) : (existing.Children || [])
            };
        }

        if (type === 'tabControl') {
            const existing = this.getNodeByPath(el.dataset.path) || {};
            const zone = el.querySelector(':scope .drop-zone[data-path$=".Tabs"]');
            return {
                type,
                CustomLabel: existing.CustomLabel || '',
                Tabs: zone ? this.syncZone(zone).map((tab) => ({ ...tab, type: 'tab' })) : (existing.Tabs || [])
            };
        }

        if (type === 'column') {
            const existing = this.getNodeByPath(el.dataset.path) || {};
            const zone = el.querySelector(':scope .drop-zone[data-path$=".Children"]');
            return {
                type,
                Width: Number(existing.Width) || 12,
                CustomLabel: existing.CustomLabel || '',
                Children: zone ? this.syncZone(zone) : []
            };
        }

        if (type === 'row') {
            const existing = this.getNodeByPath(el.dataset.path) || {};
            const zone = el.querySelector(':scope .drop-zone[data-path$=".Columns"]');
            const columns = zone ? this.syncZone(zone).map((col) => ({ ...col, type: 'column' })) : [];
            return {
                type,
                CustomLabel: existing.CustomLabel || '',
                Columns: columns.length ? columns : (existing.Columns || [])
            };
        }

        return null;
    },

    parsePath(path) {
        return (path || '')
            .split('.')
            .filter((part) => part !== '')
            .map((part) => (String(Number(part)) === part ? Number(part) : part));
    },

    getListByPath(path) {
        if (!Array.isArray(path) || !path.length) return null;
        let current = this.layout;

        for (const segment of path) {
            if (current == null) return null;
            current = current[segment];
        }

        return Array.isArray(current) ? current : null;
    },

    getNodeByPath(pathValue) {
        const path = this.parsePath(pathValue);
        if (!path.length || path[0] !== 'nodes') return null;

        let current = this.layout;
        for (const segment of path) {
            if (current == null) return null;
            current = current[segment];
        }

        return current || null;
    },

    removeByPath(pathValue) {
        const path = this.parsePath(pathValue);
        if (path.length < 2) return;

        const index = path[path.length - 1];
        const listPath = path.slice(0, -1);
        const list = this.getListByPath(listPath);

        if (!Array.isArray(list) || typeof index !== 'number') return;
        list.splice(index, 1);

        if (this.selectedPath === pathValue) {
            this.selectedPath = null;
        }

        this.render();
    },

    selectNode(pathValue) {
        this.selectedPath = pathValue;
        this.render();
    },

    renderTabHeader(tab, controlPath, index, isActive) {
        const title = tab.Title || tab.CustomLabel || 'Вкладка';
        const activeClass = isActive ? 'is-active' : '';
        return `
            <div class="canvas-node tab-header ${activeClass}" data-type="tab" data-path="${controlPath}.Tabs.${index}">
                <span class="tab-title" onclick="layoutDesigner.selectTab('${controlPath}', ${index})">${title}</span>
                <button class="btn btn-sm btn-link text-danger p-0 ms-2" onclick="layoutDesigner.removeTab('${controlPath}', ${index})">
                    <i class="bi bi-x"></i>
                </button>
            </div>`;
    },

    getActiveTabIndex(controlPath, tabs) {
        const idx = this.activeTabs?.[controlPath];
        if (typeof idx === 'number' && idx >= 0 && idx < (tabs?.length || 0)) return idx;
        return 0;
    },

    selectTab(controlPath, index) {
        this.activeTabs[controlPath] = index;
        this.selectedPath = `${controlPath}.Tabs.${index}`;
        this.render();
    },

    addTab(controlPath) {
        const node = this.getNodeByPath(controlPath);
        if (!node || node.type !== 'tabControl') return;
        if (!node.Tabs) node.Tabs = [];
        node.Tabs.push({ type: 'tab', Title: 'Новая вкладка', CustomLabel: '', Children: [] });
        const newIndex = node.Tabs.length - 1;
        this.activeTabs[controlPath] = newIndex;
        this.selectedPath = `${controlPath}.Tabs.${newIndex}`;
        this.render();
    },

    removeTab(controlPath, index) {
        const node = this.getNodeByPath(controlPath);
        if (!node || node.type !== 'tabControl' || !Array.isArray(node.Tabs)) return;
        if (node.Tabs.length <= 1) return;
        node.Tabs.splice(index, 1);
        const nextIndex = Math.min(index, node.Tabs.length - 1);
        this.activeTabs[controlPath] = nextIndex;
        this.selectedPath = `${controlPath}.Tabs.${nextIndex}`;
        this.render();
    },

    renderSettingsPanel() {
        const panel = document.getElementById('nodeSettingsPanel');
        if (!panel) return;

        const node = this.selectedPath ? this.getNodeByPath(this.selectedPath) : null;
        if (!node) {
            panel.innerHTML = '<div class="text-muted small">Выберите элемент на холсте для настройки.</div>';
            return;
        }

        const lines = [`<div class="small text-muted mb-2">Тип: <strong>${node.type}</strong></div>`];

        if (['group', 'tab'].includes(node.type)) {
            lines.push(`
                <div class="mb-2">
                    <label class="form-label form-label-sm">Заголовок</label>
                    <input class="form-control form-control-sm" value="${node.Title || ''}" onchange="layoutDesigner.updateNodeSetting('Title', this.value)">
                </div>`);
        }

        if (node.type === 'group') {
            lines.push(`
                <div class="form-check form-switch mb-2">
                    <input class="form-check-input" type="checkbox" ${node.IsCollapsed ? 'checked' : ''} onchange="layoutDesigner.updateNodeSetting('IsCollapsed', this.checked)">
                    <label class="form-check-label small">Свернута по умолчанию</label>
                </div>`);
        }

        if (['row', 'column'].includes(node.type)) {
            lines.push(`
                <div class="mb-2">
                    <label class="form-label form-label-sm">Заголовок</label>
                    <input class="form-control form-control-sm" value="${node.CustomLabel || ''}" onchange="layoutDesigner.updateNodeSetting('CustomLabel', this.value)">
                </div>`);
        }

        if (node.type === 'column') {
            lines.push(`
                <div class="mb-2">
                    <label class="form-label form-label-sm">Ширина колонки (1-12)</label>
                    <input type="number" min="1" max="12" class="form-control form-control-sm" value="${node.Width || 12}" onchange="layoutDesigner.updateNodeSetting('Width', Number(this.value || 12))">
                </div>`);
        }

        if (!['row', 'column'].includes(node.type)) {
            lines.push(`
                <div class="mb-2">
                    <label class="form-label form-label-sm">CustomLabel</label>
                    <input class="form-control form-control-sm" value="${node.CustomLabel || ''}" onchange="layoutDesigner.updateNodeSetting('CustomLabel', this.value)">
                </div>`);
        }

        panel.innerHTML = lines.join('');
    },

    updateNodeSetting(prop, value) {
        if (!this.selectedPath) return;

        const node = this.getNodeByPath(this.selectedPath);
        if (!node) return;

        node[prop] = value;
        this.render();
    },

    async save() {
        try {
            const modalAppId = document.getElementById('builderModal')?.dataset?.appId || null;
            const appDefinitionId = this.appDefinitionId || window.formBuilder?.appId || modalAppId || null;
            const formType = this.formType || window.formBuilder?.currentMode || null;
            const formTypeMap = { Create: 0, Edit: 1, View: 2 };
            const formTypeValue = typeof formType === 'string' ? formTypeMap[formType] : (formType ?? 0);
            if (!this.formId && !appDefinitionId) {
                throw new Error('Не удалось определить сущность для сохранения формы.');
            }

            const response = await FormApiClient.saveLayout({
                formId: this.formId,
                appDefinitionId,
                formType: formTypeValue,
                layoutJson: JSON.stringify(this.layout)
            });

            if (!response.ok) {
                const errorText = await response.text();
                throw new Error(errorText || 'Ошибка сохранения макета');
            }

            const result = await response.json();

            const finalizeSave = (newFormId) => {
                if (newFormId && !this.formId) {
                    this.formId = newFormId;
                }
                if (window.formBuilder?.onLayoutSaved) {
                    window.formBuilder.onLayoutSaved({
                        formId: this.formId,
                        formType,
                        layoutJson: JSON.stringify(this.layout)
                    });
                }
            };

            if (result.warning) {
                const shouldForceSave = confirm(`${result.message}\n\nСохранить форму принудительно?`);
                if (!shouldForceSave) return;

                const forceResponse = await FormApiClient.saveLayout({
                    formId: this.formId,
                    appDefinitionId,
                    formType: formTypeValue,
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

                finalizeSave(forceResult.formId);
                alert(forceResult.message || 'Макет сохранен успешно');
                return;
            }

            if (!result.success) {
                throw new Error(result.message || 'Ошибка сохранения макета');
            }

            finalizeSave(result.formId);
            alert(result.message || 'Макет сохранен успешно');
        } catch (e) {
            alert(e.message);
        }
    }
};
