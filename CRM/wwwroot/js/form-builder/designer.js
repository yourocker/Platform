import { FormApiClient } from './api.js';

export const layoutDesigner = {
    currentMode: null,
    layout: { nodes: [] },
    fields: [],
    config: null,

    draggedItem: null,
    currentNode: null, // Ссылка на узел, который мы редактируем

    // DOM Elements
    canvas: null,

    init(config) {
        this.config = config;
        this.canvas = document.getElementById('formCanvas');

        // Canvas Events
        this.canvas.addEventListener('dragover', e => this.onDragOver(e));
        this.canvas.addEventListener('drop', e => this.onDrop(e));

        // Palette Events
        document.querySelectorAll('.palette-item[data-type]').forEach(el => {
            if(el.parentElement.id !== 'paletteFieldsList') {
                el.addEventListener('dragstart', e => {
                    e.dataTransfer.effectAllowed = 'copy';
                    this.draggedItem = { source: 'palette', type: el.dataset.type };
                });
            }
        });
    },

    onDragOver(e) {
        e.preventDefault();
        e.dataTransfer.dropEffect = 'copy';
    },

    switchMode(mode) {
        if (this.currentMode) {
            this.config.layouts[this.currentMode] = JSON.parse(JSON.stringify(this.layout));
        }
        this.currentMode = mode;
        this.layout = this.config.layouts[mode] || { nodes: [] };
        if (!this.layout.nodes) this.layout.nodes = [];
        this.render();
    },

    refreshPalette(fields) {
        this.fields = fields.filter(f => !f.isDeleted);
        const container = document.getElementById('paletteFieldsList');
        if (!container) return;

        container.innerHTML = '';
        this.fields.forEach(f => {
            const div = document.createElement('div');
            div.className = 'palette-item d-flex justify-content-between align-items-center bg-white border mb-1 p-2 rounded shadow-sm';
            div.draggable = true;
            div.style.cursor = 'grab';
            div.innerHTML = `
                <div class="text-truncate" style="max-width: 140px;" title="${f.label}">
                    <i class="bi bi-grip-vertical text-muted me-1"></i> ${f.label}
                </div> 
                <span class="badge bg-secondary" style="font-size:0.65rem">${f.dataType}</span>
            `;

            div.addEventListener('dragstart', e => {
                this.draggedItem = {
                    source: 'palette',
                    type: 'field',
                    fieldId: f.id,
                    label: f.label
                };
            });
            container.appendChild(div);
        });
    },

    // --- RENDER ---
    render() {
        this.canvas.innerHTML = '';
        if (this.layout.nodes.length === 0) {
            this.canvas.innerHTML = `<div class="text-center text-muted py-5 border dashed drop-target-root bg-white">
                <i class="bi bi-box-arrow-in-down fs-1 text-primary opacity-50"></i>
                <h5 class="mt-3">Макет пуст</h5>
                <p>Перетащите элементы из левой панели</p>
            </div>`;
            const dropRoot = this.canvas.querySelector('.drop-target-root');
            this.setupDropZone(dropRoot, null);
            return;
        }

        this.layout.nodes.forEach((node, index) => {
            const el = this.createNodeElement(node, [index]);
            this.canvas.appendChild(el);
        });

        const bottomDrop = document.createElement('div');
        bottomDrop.className = 'py-4 text-center text-muted border-top border-2 border-dashed drop-zone-bottom mt-3';
        bottomDrop.innerHTML = '<small class="text-uppercase fw-bold text-primary">Добавить новую секцию вниз</small>';
        this.setupDropZone(bottomDrop, null);
        this.canvas.appendChild(bottomDrop);
    },

    createNodeElement(node, path) {
        const div = document.createElement('div');
        div.className = 'canvas-node mb-3 position-relative shadow-sm bg-white rounded border';
        div.dataset.path = path.join(',');

        // Header
        const header = document.createElement('div');
        header.className = 'node-header d-flex justify-content-between px-3 py-2 bg-light border-bottom rounded-top align-items-center grab-handle';
        header.style.cursor = 'move';
        header.draggable = true;

        header.addEventListener('dragstart', e => {
            e.stopPropagation();
            this.draggedItem = { source: 'canvas', path: path };
            e.dataTransfer.effectAllowed = 'move';
        });

        const content = document.createElement('div');
        content.className = 'node-content p-3';

        let title = '', icon = '';

        if (node.type === 'row') {
            title = 'Секция'; icon = 'bi-layout-three-columns';
            content.className += ' row g-3';
            if (node.columns) {
                node.columns.forEach((col, cIdx) => {
                    const colDiv = document.createElement('div');
                    const width = col.width || 12;
                    colDiv.className = `col-${width}`;
                    const innerZone = document.createElement('div');
                    innerZone.className = 'h-100 border dashed rounded p-2 bg-light drop-zone min-h-60';
                    this.setupDropZone(innerZone, [...path, 'columns', cIdx, 'children'].join(','));
                    if (col.children) {
                        col.children.forEach((child, childIdx) => {
                            innerZone.appendChild(this.createNodeElement(child, [...path, 'columns', cIdx, 'children', childIdx]));
                        });
                    }
                    colDiv.appendChild(innerZone);
                    content.appendChild(colDiv);
                });
            }
        }
        else if (node.type === 'group') {
            title = node.customLabel || node.title || 'Группа'; icon = 'bi-card-heading';
            content.className += ' drop-zone bg-white border rounded min-h-60';
            this.setupDropZone(content, [...path, 'children'].join(','));
            if (node.children) {
                node.children.forEach((child, i) => {
                    content.appendChild(this.createNodeElement(child, [...path, 'children', i]));
                });
            }
        }
        else if (node.type === 'tab-control') {
            title = 'Вкладки'; icon = 'bi-folder2-open';
            const ul = document.createElement('ul'); ul.className = 'nav nav-tabs mb-2';
            const tabContent = document.createElement('div'); tabContent.className = 'tab-content border border-top-0 p-3 bg-white';
            if(!node.tabs) node.tabs = [{title: 'Tab 1', children: []}];
            node.tabs.forEach((tab, tIdx) => {
                const li = document.createElement('li'); li.className = 'nav-item';
                li.innerHTML = `<a class="nav-link ${tIdx===0?'active':''}" data-bs-toggle="tab" href="#tab-${path.join('-')}-${tIdx}">${tab.title}</a>`;
                ul.appendChild(li);
                const pane = document.createElement('div'); pane.className = `tab-pane fade ${tIdx===0?'show active':''} drop-zone min-h-60`;
                pane.id = `tab-${path.join('-')}-${tIdx}`;
                this.setupDropZone(pane, [...path, 'tabs', tIdx, 'children'].join(','));
                if(tab.children) tab.children.forEach((child, cIdx) => pane.appendChild(this.createNodeElement(child, [...path, 'tabs', tIdx, 'children', cIdx])));
                tabContent.appendChild(pane);
            });
            content.appendChild(ul); content.appendChild(tabContent);
        }
        else if (node.type === 'field') {
            const fieldMeta = this.fields.find(f => f.id === node.fieldId);
            title = node.customLabel || (fieldMeta ? fieldMeta.label : 'Удаленное поле');
            icon = 'bi-input-cursor-text';
            const sysName = fieldMeta ? fieldMeta.systemName : '???';

            // Визуализация флагов
            let badges = '';
            if(node.isRequiredOverride) badges += '<i class="bi bi-asterisk text-danger ms-1" title="Обязательно"></i>';
            if(node.isReadOnly) badges += '<i class="bi bi-eye-slash-fill text-muted ms-1" title="Только чтение"></i>';
            if(node.allowCreate) badges += '<i class="bi bi-plus-square-fill text-success ms-1" title="Кнопка создания"></i>';
            if(node.allowLink) badges += '<i class="bi bi-box-arrow-up-right text-primary ms-1" title="Ссылка на объект"></i>';

            content.innerHTML = `
                <div class="d-flex align-items-center">
                    <label class="form-label mb-0 small text-muted me-2">${title} ${badges}</label>
                    <input type="text" class="form-control form-control-sm bg-light" disabled value="[${sysName}]">
                </div>
            `;
        }

        header.innerHTML = `
            <div class="fw-bold small text-uppercase"><i class="bi ${icon} me-2 text-primary"></i>${title}</div>
            <div class="d-flex align-items-center gap-2">
                <button class="btn btn-xs btn-light border" onclick="layoutDesigner.openSettings('${path.join(',')}')" title="Настроить"><i class="bi bi-gear-fill text-secondary"></i></button>
                <button class="btn btn-xs btn-light border" onclick="layoutDesigner.removeNode('${path.join(',')}')" title="Удалить"><i class="bi bi-trash-fill text-danger"></i></button>
            </div>
        `;

        div.appendChild(header);
        div.appendChild(content);
        return div;
    },

    setupDropZone(el, targetPathStr) {
        el.addEventListener('dragover', e => {
            e.preventDefault(); e.stopPropagation();
            el.classList.add('bg-primary', 'bg-opacity-10', 'border-primary');
        });
        el.addEventListener('dragleave', e => {
            e.preventDefault(); e.stopPropagation();
            el.classList.remove('bg-primary', 'bg-opacity-10', 'border-primary');
        });
        el.addEventListener('drop', e => {
            e.preventDefault(); e.stopPropagation();
            el.classList.remove('bg-primary', 'bg-opacity-10', 'border-primary');
            this.handleDrop(targetPathStr);
        });
    },

    handleDrop(targetPathStr) {
        if (!this.draggedItem) return;
        let targetArray = !targetPathStr ? this.layout.nodes : this.getNodeByPath(targetPathStr);
        if (!targetArray && targetPathStr) return;

        if (this.draggedItem.source === 'palette') {
            let newNode = null;
            if (this.draggedItem.type === 'row') newNode = { type: 'row', columns: [{ width: 6, children: [] }, { width: 6, children: [] }] };
            else if (this.draggedItem.type === 'group') newNode = { type: 'group', title: 'Группа', children: [] };
            else if (this.draggedItem.type === 'tab-control') newNode = { type: 'tab-control', tabs: [{title: 'Tab 1', children: []}, {title: 'Tab 2', children: []}] };
            else if (this.draggedItem.type === 'field') {
                newNode = {
                    type: 'field',
                    fieldId: this.draggedItem.fieldId,
                    customLabel: this.draggedItem.label,
                    isRequiredOverride: false,
                    isReadOnly: false,
                    allowCreate: false, // ТЗ 3.3
                    allowLink: false    // ТЗ 3.3
                };
            }
            if (newNode) targetArray.push(newNode);
        } else if (this.draggedItem.source === 'canvas') {
            const sourcePath = this.draggedItem.path;
            if (targetPathStr && targetPathStr.startsWith(sourcePath.join(','))) return;
            const item = this.extractNode(sourcePath);
            if (item) {
                targetArray = !targetPathStr ? this.layout.nodes : this.getNodeByPath(targetPathStr);
                targetArray.push(item);
            }
        }
        this.render();
        this.draggedItem = null;
    },

    getNodeByPath(pathStr) {
        const parts = pathStr.split(',');
        let currentObj = this.layout;
        for (let i = 0; i < parts.length; i++) {
            const key = parts[i];
            if (!isNaN(key)) { if (Array.isArray(currentObj)) currentObj = currentObj[parseInt(key)]; }
            else { if (currentObj) currentObj = currentObj[key]; }
            if (!currentObj) return null;
        }
        return Array.isArray(currentObj) ? currentObj : null;
    },

    extractNode(path) {
        const parts = [...path];
        const index = parseInt(parts.pop());
        const parentPath = parts.join(',');
        let parentArray = !parentPath ? this.layout.nodes : this.getNodeByPath(parentPath);
        if (parentArray && parentArray[index]) return parentArray.splice(index, 1)[0];
        return null;
    },

    // --- SETTINGS (ИСПРАВЛЕНО ТЗ 3.3) ---
    openSettings(pathStr) {
        const parts = pathStr.split(',');
        let node = this.layout.nodes[parts[0]];
        for(let i=1; i<parts.length; i++) {
            const key = parts[i];
            if(Array.isArray(node)) node = node[parseInt(key)];
            else node = node[key];
        }
        this.currentNode = node;

        // Общие
        document.getElementById('propLabel').value = node.customLabel || node.title || '';

        // Показать/Скрыть блоки
        document.getElementById('fieldProps').style.display = node.type === 'field' ? 'block' : 'none';
        document.getElementById('groupProps').style.display = node.type === 'group' ? 'block' : 'none';

        // Настройки ПОЛЯ (ТЗ 3.3)
        if (node.type === 'field') {
            document.getElementById('propRequired').checked = node.isRequiredOverride || false;
            document.getElementById('propReadOnly').checked = node.isReadOnly || false;

            // Настройки EntityLink (если есть)
            const elCreate = document.getElementById('propAllowCreate');
            const elLink = document.getElementById('propAllowLink');

            if(elCreate) elCreate.checked = node.allowCreate || false;
            if(elLink) elLink.checked = node.allowLink || false;
        }

        if (node.type === 'group') {
            document.getElementById('propCollapsed').checked = node.isCollapsed || false;
        }

        new bootstrap.Modal(document.getElementById('nodeSettingsModal')).show();
    },

    applySettings() {
        if (!this.currentNode) return;

        const label = document.getElementById('propLabel').value;
        if(this.currentNode.type === 'group' || this.currentNode.type === 'tab-control') this.currentNode.title = label;
        else this.currentNode.customLabel = label;

        if (this.currentNode.type === 'field') {
            this.currentNode.isRequiredOverride = document.getElementById('propRequired').checked;
            this.currentNode.isReadOnly = document.getElementById('propReadOnly').checked;

            // Сохранение настроек EntityLink
            const elCreate = document.getElementById('propAllowCreate');
            const elLink = document.getElementById('propAllowLink');
            if(elCreate) this.currentNode.allowCreate = elCreate.checked;
            if(elLink) this.currentNode.allowLink = elLink.checked;
        }

        if (this.currentNode.type === 'group') {
            this.currentNode.isCollapsed = document.getElementById('propCollapsed').checked;
        }

        this.render();
        bootstrap.Modal.getInstance(document.getElementById('nodeSettingsModal')).hide();
    },

    removeNode(pathStr) {
        if(!confirm('Удалить элемент?')) return;
        this.extractNode(pathStr.split(','));
        this.render();
    }
};