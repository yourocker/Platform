/* * Form Designer Engine v1.0 
 * Отвечает за Drag&Drop, редактирование свойств и сборку JSON-схемы.
 */

document.addEventListener("DOMContentLoaded", function () {
    const canvasRoot = document.getElementById("canvas-root");
    const paletteStructure = document.getElementById("palette-structure");
    const paletteFields = document.getElementById("palette-fields");
    const propertiesPanel = document.getElementById("properties-panel");
    const btnSave = document.getElementById("btn-save-layout");

    // Храним текущий выбранный элемент для редактирования свойств
    let activeElement = null;

    // --- 1. ИНИЦИАЛИЗАЦИЯ DRAG & DROP (SortableJS) ---

    // Конфигурация для ПАЛИТРЫ (откуда берем)
    const paletteConfig = {
        group: {
            name: 'designer',
            pull: 'clone', // Клонируем элементы при перетаскивании
            put: false     // Нельзя бросать обратно в палитру
        },
        sort: false, // В палитре порядок не меняем
        animation: 150,
        ghostClass: 'bg-light'
    };

    // Инициализируем палитры
    if (paletteStructure) new Sortable(paletteStructure, paletteConfig);
    if (paletteFields) new Sortable(paletteFields, paletteConfig);

    // Функция инициализации контейнеров на ХОЛСТЕ (куда бросаем)
    // Она рекурсивная, так как мы будем создавать новые группы/вкладки динамически
    function initCanvasSortable(element) {
        new Sortable(element, {
            group: 'designer',
            animation: 150,
            ghostClass: 'bg-light',
            fallbackOnBody: true,
            swapThreshold: 0.65,

            // Обработчик добавления элемента (из палитры или другой группы)
            onAdd: function (evt) {
                const item = evt.item;

                // Если элемент пришел из палитры, он еще "сырой". 
                // Превращаем его в полноценный компонент холста.
                if (!item.classList.contains('canvas-element')) {
                    transformPaletteItemToCanvasElement(item);
                }
            }
        });
    }

    // Запускаем Sortable для корневой зоны холста
    if (canvasRoot) initCanvasSortable(canvasRoot);


    // --- 2. ЛОГИКА ТРАНСФОРМАЦИИ ЭЛЕМЕНТОВ ---

    function transformPaletteItemToCanvasElement(item) {
        // Читаем данные, которые мы заложили в data-атрибуты в HTML (Index.cshtml)
        const type = item.dataset.type;
        const fieldName = item.dataset.fieldName || "";
        const isSystem = item.dataset.isSystem || "False";

        // Берем текст заголовка из палитры (очищаем от иконок и лишних пробелов)
        // В палитре структура: <icon> <span>Label</span> <small>...</small>
        // Пробуем найти span, если нет - берем весь текст
        let labelSpan = item.querySelector('span');
        let label = labelSpan ? labelSpan.innerText.trim() : item.innerText.trim();

        // Генерируем уникальный ID для элемента DOM
        const uniqueId = 'el-' + Math.random().toString(36).substr(2, 9);

        // Превращаем DOM-элемент в компонент холста
        item.id = uniqueId;
        item.className = "canvas-element"; // Этот класс помечает элемент как часть схемы
        item.dataset.id = uniqueId;

        // Сохраняем исходные данные в dataset, чтобы потом собрать JSON
        item.dataset.type = type;
        item.dataset.fieldName = fieldName;
        item.dataset.isSystem = isSystem;
        item.dataset.label = label; // Текущий заголовок храним здесь

        // Формируем HTML внутренности элемента в зависимости от типа
        let innerHTML = "";
        let hasNestedContainer = false;

        if (type === "tab-control") {
            innerHTML = `
                <div class="element-header"><i class="bi bi-folder2-open"></i> <span class="el-label">Блок вкладок</span></div>
                <div class="canvas-nested-area" data-area-type="tabs">
                    <div class="text-muted small text-center p-2">Перетащите сюда "Вкладку"</div>
                </div>
            `;
            hasNestedContainer = true;
        }
        else if (type === "tab") {
            // Вкладка сама по себе (обычно кладется внутрь tab-control)
            // Но в текущей палитре у нас нет отдельной "Вкладки", мы можем использовать Группу как вкладку 
            // или доработать палитру. Пока используем Group как универсальный контейнер.
        }
        else if (type === "group") {
            innerHTML = `
                <div class="element-header">
                    <i class="bi bi-card-heading"></i> <span class="el-label">${label}</span>
                    <div class="element-actions">
                         <i class="bi bi-x-lg text-danger" onclick="removeCanvasElement('${uniqueId}')"></i>
                    </div>
                </div>
                <div class="canvas-nested-area p-2 bg-white border" style="min-height: 50px;"></div>
            `;
            hasNestedContainer = true;
        }
        else if (type === "row") {
            innerHTML = `
                <div class="element-header"><i class="bi bi-layout-three-columns"></i> <span class="el-label">Колонки</span>
                    <div class="element-actions">
                         <i class="bi bi-x-lg text-danger" onclick="removeCanvasElement('${uniqueId}')"></i>
                    </div>
                </div>
                <div class="row g-2 canvas-row-area">
                    <div class="col-6 p-2 border border-dashed canvas-nested-area"></div>
                    <div class="col-6 p-2 border border-dashed canvas-nested-area"></div>
                </div>
            `;
            // Здесь 2 вложенные зоны
        }
        else {
            // Обычное поле, Текст или Связанный список
            let icon = "bi-input-cursor-text";
            if (type === "related-list") icon = "bi-list-ul";
            if (type === "text") icon = "bi-fonts";

            innerHTML = `
                <div class="element-content d-flex justify-content-between align-items-center">
                    <div>
                        <i class="bi ${icon} me-2 text-muted"></i>
                        <span class="el-label fw-bold">${label}</span>
                        ${fieldName ? `<code class="ms-2 text-muted small">${fieldName}</code>` : ''}
                    </div>
                    <div class="element-actions">
                        <i class="bi bi-x-lg text-danger" onclick="removeCanvasElement('${uniqueId}')"></i>
                    </div>
                </div>
            `;
        }

        item.innerHTML = innerHTML;

        // Если есть вложенные контейнеры, инициализируем их
        if (hasNestedContainer) {
            const nestedAreas = item.querySelectorAll('.canvas-nested-area');
            nestedAreas.forEach(area => initCanvasSortable(area));
        } else if (type === "row") {
            // Для строк инициализируем колонки
            const cols = item.querySelectorAll('.canvas-nested-area');
            cols.forEach(col => initCanvasSortable(col));
        }

        // Добавляем обработчик клика для свойств
        item.addEventListener('click', function(e) {
            e.stopPropagation(); // Чтобы не выделялся родительский контейнер
            selectElement(item);
        });

        // Сразу выбираем новый элемент
        selectElement(item);
    }

    // Глобальная функция удаления (вызывается из onclick в HTML)
    window.removeCanvasElement = function(id) {
        const el = document.getElementById(id);
        if (el) {
            el.remove();
            activeElement = null;
            propertiesPanel.innerHTML = '<p class="text-muted text-center mt-3">Элемент удален</p>';
        }
    };


    // --- 3. ПАНЕЛЬ СВОЙСТВ ---

    function selectElement(el) {
        // Снимаем класс selected с предыдущего
        if (activeElement) activeElement.classList.remove("selected-element");

        activeElement = el;
        activeElement.classList.add("selected-element"); // CSS класс для синей рамки

        renderProperties(el);
    }

    function renderProperties(el) {
        const type = el.dataset.type;
        const currentLabel = el.dataset.label;
        const fieldName = el.dataset.fieldName;

        // Базовый HTML свойств
        let html = `<h6 class="border-bottom pb-2 mb-3">Настройки элемента</h6>`;

        // Общее свойство: Заголовок (Label)
        html += `
            <div class="mb-3">
                <label class="form-label small text-muted">Заголовок</label>
                <input type="text" class="form-control" id="prop-label" value="${currentLabel}">
            </div>
        `;

        // Специфичные свойства
        if (type === 'field' || type === 'related-list') {
            const isSystem = el.dataset.isSystem === "True";

            html += `
                <div class="mb-3">
                    <label class="form-label small text-muted">Системное имя</label>
                    <input type="text" class="form-control form-control-sm bg-light" value="${fieldName}" readonly>
                    <div class="form-text small">${isSystem ? 'Системное свойство (C#)' : 'Динамическое поле (БД)'}</div>
                </div>
            `;

            // Чекбокс ReadOnly (храним состояние в dataset, если нет - false)
            const isReadOnly = el.dataset.readonly === "true";

            html += `
                <div class="form-check form-switch">
                    <input class="form-check-input" type="checkbox" id="prop-readonly" ${isReadOnly ? 'checked' : ''}>
                    <label class="form-check-label" for="prop-readonly">Только чтение</label>
                </div>
            `;
        }

        if (type === 'group') {
            // Можно добавить настройку "Свернута по умолчанию"
        }

        propertiesPanel.innerHTML = html;

        // НАВЕШИВАЕМ ОБРАБОТЧИКИ ИЗМЕНЕНИЙ (Live Update)

        // 1. Изменение заголовка
        const inputLabel = document.getElementById('prop-label');
        if (inputLabel) {
            inputLabel.addEventListener('input', function() {
                const newVal = this.value;
                el.dataset.label = newVal; // Обновляем данные
                // Обновляем визуал на холсте
                const labelSpan = el.querySelector('.el-label');
                if (labelSpan) labelSpan.innerText = newVal;
            });
        }

        // 2. Изменение ReadOnly
        const checkReadOnly = document.getElementById('prop-readonly');
        if (checkReadOnly) {
            checkReadOnly.addEventListener('change', function() {
                el.dataset.readonly = this.checked;
            });
        }
    }


    // --- 4. СБОРКА JSON И СОХРАНЕНИЕ ---

    btnSave.addEventListener('click', function() {
        // Собираем дерево рекурсивно
        const layoutData = buildJsonFromDom(canvasRoot);

        console.log("Собранный Layout:", layoutData);

        // Получаем параметры из URL
        const urlParams = new URLSearchParams(window.location.search);
        const entityId = urlParams.get('entityId');
        const formId = urlParams.get('formId'); // Может быть null

        saveLayoutToServer(entityId, formId, layoutData);
    });

    function buildJsonFromDom(container) {
        const result = [];

        // Ищем только непосредственных детей, которые являются элементами холста
        // (container.children включает и всякие вспомогательные элементы Sortable, поэтому фильтруем по классу)
        const children = Array.from(container.children).filter(node => node.classList.contains('canvas-element'));

        children.forEach(el => {
            const nodeData = {
                id: el.dataset.id,
                type: el.dataset.type,
                // Свойства UI
                props: {
                    label: el.dataset.label,
                    readonly: el.dataset.readonly === "true"
                },
                children: []
            };

            // Специфичные данные полей
            if (nodeData.type === 'field' || nodeData.type === 'related-list') {
                nodeData.props.fieldName = el.dataset.fieldName;
                nodeData.props.isSystem = el.dataset.isSystem === "True";
            }

            // Рекурсивный обход вложенных контейнеров
            // Ищем все зоны внутри элемента (например, внутри группы или колонок)
            const nestedAreas = el.querySelectorAll(':scope > .canvas-nested-area, :scope > .canvas-row-area > div');

            // Если зон несколько (например, колонки), нам нужно как-то их различать в JSON.
            // В текущей упрощенной схеме мы просто сливаем всех детей в один массив children.
            // (Для полноценной сетки нужно усложнять структуру JSON, добавляя Columns).
            // Пока сделаем плоский список детей.

            nestedAreas.forEach(area => {
                const nestedChildren = buildJsonFromDom(area);
                nodeData.children = nodeData.children.concat(nestedChildren);
            });

            result.push(nodeData);
        });

        return result;
    }

    async function saveLayoutToServer(entityId, formId, layout) {
        const btnOriginalText = btnSave.innerHTML;
        btnSave.disabled = true;
        btnSave.innerHTML = '<span class="spinner-border spinner-border-sm"></span> Сохранение...';

        try {
            const payload = {
                entityId: entityId,
                formId: formId,
                layoutJson: JSON.stringify({
                    version: "1.0",
                    layout: layout
                })
            };

            const response = await fetch('/FormDesigner/SaveConfig', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify(payload)
            });

            if (response.ok) {
                // Успех
                alert("Макет успешно сохранен!");
                // Если создавали новую форму, хорошо бы перезагрузить страницу с новым ID, 
                // но это решит контроллер (редирект или JSON ответ).
                const data = await response.json();
                if (data.redirectUrl) {
                    window.location.href = data.redirectUrl;
                }
            } else {
                const errText = await response.text();
                alert("Ошибка сохранения: " + errText);
            }

        } catch (e) {
            console.error(e);
            alert("Ошибка сети или скрипта.");
        } finally {
            btnSave.disabled = false;
            btnSave.innerHTML = btnOriginalText;
        }
    }

});