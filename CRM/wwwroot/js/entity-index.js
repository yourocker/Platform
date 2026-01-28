document.addEventListener('DOMContentLoaded', function () {
    initColumnVisibility();
    initClickableRows();
    initColumnDragAndDrop();
});

// --- 1. Управление колонками (Visibility) ---
function initColumnVisibility() {
    const STORAGE_KEY_PREFIX = 'crm_cols_';
    const storageKey = STORAGE_KEY_PREFIX + window.location.pathname.replace(/\//g, '_');
    const checkboxes = document.querySelectorAll('.col-chk');
    if (checkboxes.length === 0) return;

    let savedCols = null;
    try { savedCols = JSON.parse(localStorage.getItem(storageKey)); } catch (e) { }

    if (savedCols) {
        checkboxes.forEach(chk => {
            const shouldCheck = savedCols.includes(chk.value);
            chk.checked = shouldCheck;
            toggleColumn(chk.value, shouldCheck);
        });
    }

    checkboxes.forEach(chk => {
        chk.addEventListener('change', function () {
            toggleColumn(this.value, this.checked);
            const activeCols = Array.from(checkboxes).filter(i => i.checked).map(i => i.value);
            localStorage.setItem(storageKey, JSON.stringify(activeCols));
        });
    });
}

function toggleColumn(className, isVisible) {
    document.querySelectorAll('.' + className).forEach(el => {
        el.style.display = isVisible ? '' : 'none';
    });
}

// --- 2. Кликабельные строки ---
function initClickableRows() {
    const rows = document.querySelectorAll('.crm-clickable-row');
    rows.forEach(row => {
        row.addEventListener('click', function (e) {
            // Игнорируем клики по ссылкам, кнопкам, инпутам и специальным ячейкам
            if (e.target.closest('a, button, input, label, .no-row-click')) return;

            const selection = window.getSelection();
            if (selection.toString().length > 0) return;

            const url = this.dataset.href;
            if (url) window.location.href = url;
        });
    });
}

// --- 3. Drag & Drop Колонок ---
function initColumnDragAndDrop() {
    const table = document.getElementById('entityTable');
    if (!table) return;

    const theadRow = table.querySelector('thead tr');
    const tbody = table.querySelector('tbody');
    const STORAGE_KEY_ORDER = 'crm_col_order_' + window.location.pathname.replace(/\//g, '_');

    restoreColumnOrder();

    if (typeof Sortable !== 'undefined') {
        Sortable.create(theadRow, {
            animation: 150,

            // ВАЖНО: Таскаем только за иконку-ручку
            handle: '.crm-col-handle',

            filter: '.actions',
            onEnd: function (evt) {
                reorderBodyRows(evt.oldIndex, evt.newIndex);
                saveColumnOrder();
            }
        });
    }

    function reorderBodyRows(oldIndex, newIndex) {
        const rows = tbody.querySelectorAll('tr');
        rows.forEach(row => {
            const cells = Array.from(row.children);
            const movedCell = cells[oldIndex];

            if (newIndex >= cells.length) {
                row.appendChild(movedCell);
            } else {
                const referenceNode = cells[newIndex];
                if (oldIndex < newIndex) {
                    row.insertBefore(movedCell, referenceNode.nextSibling);
                } else {
                    row.insertBefore(movedCell, referenceNode);
                }
            }
        });
    }

    function saveColumnOrder() {
        const headers = Array.from(theadRow.children);
        const order = headers.map(th => th.getAttribute('data-name')).filter(n => n);
        localStorage.setItem(STORAGE_KEY_ORDER, JSON.stringify(order));
    }

    function restoreColumnOrder() {
        let savedOrder = null;
        try { savedOrder = JSON.parse(localStorage.getItem(STORAGE_KEY_ORDER)); } catch(e){}

        if (!savedOrder || !Array.isArray(savedOrder)) return;

        // Переупорядочиваем Header
        const currentHeaders = Array.from(theadRow.children);
        const headerMap = {};
        currentHeaders.forEach(th => headerMap[th.getAttribute('data-name')] = th);

        const frag = document.createDocumentFragment();

        savedOrder.forEach(name => {
            if (headerMap[name]) {
                frag.appendChild(headerMap[name]);
                delete headerMap[name];
            }
        });

        Object.values(headerMap).forEach(th => frag.appendChild(th));
        theadRow.appendChild(frag);

        // Переупорядочиваем Body согласно новому порядку хедера
        const newHeaderOrder = Array.from(theadRow.children).map(th => th.getAttribute('data-name'));
        const rows = tbody.querySelectorAll('tr');

        rows.forEach(row => {
            const cellMap = {};
            Array.from(row.children).forEach(td => cellMap[td.getAttribute('data-name')] = td);

            const rowFrag = document.createDocumentFragment();
            newHeaderOrder.forEach(name => {
                if (cellMap[name]) {
                    rowFrag.appendChild(cellMap[name]);
                    delete cellMap[name];
                }
            });
            Object.values(cellMap).forEach(td => rowFrag.appendChild(td));
            row.appendChild(rowFrag);
        });
    }
}