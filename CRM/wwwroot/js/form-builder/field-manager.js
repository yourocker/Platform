import { FormApiClient } from './api.js';
import { layoutDesigner } from './designer.js';

export const fieldManager = {
    appId: null,
    fields: [],

    init(appId) {
        this.appId = appId;
    },

    async loadFields() {
        const showDeletedCheck = document.getElementById('showDeletedCheck');
        if (!showDeletedCheck) return;

        const showDeleted = showDeletedCheck.checked;
        const tbody = document.getElementById('fieldsTableBody');
        tbody.innerHTML = '<tr><td colspan="5" class="text-center">Загрузка...</td></tr>';

        try {
            this.fields = await FormApiClient.getFields(this.appId, showDeleted);
            this.renderTable(this.fields);

            // Уведомляем дизайнер, что поля обновились
            layoutDesigner.refreshPalette(this.fields);
        } catch (e) {
            tbody.innerHTML = `<tr><td colspan="5" class="text-danger">Ошибка: ${e.message}</td></tr>`;
        }
    },

    renderTable(fields) {
        const tbody = document.getElementById('fieldsTableBody');
        tbody.innerHTML = '';

        if (fields.length === 0) {
            tbody.innerHTML = '<tr><td colspan="5" class="text-center text-muted">Нет полей</td></tr>';
            return;
        }

        fields.forEach(f => {
            const tr = document.createElement('tr');
            if (f.isDeleted) tr.classList.add('table-danger', 'text-muted');

            let actionBtn = '';
            if (f.isDeleted) {
                actionBtn = `<button class="btn btn-sm btn-outline-success" onclick="fieldManager.restore('${f.id}')" title="Восстановить"><i class="bi bi-arrow-counterclockwise"></i></button>`;
            } else if (f.isSystem) {
                actionBtn = `<span class="text-muted small"><i class="bi bi-lock"></i> Системное</span>`;
            } else {
                actionBtn = `<button class="btn btn-sm btn-outline-danger" onclick="fieldManager.delete('${f.id}')" title="Удалить"><i class="bi bi-trash"></i></button>`;
            }

            tr.innerHTML = `
                <td>${f.label}</td>
                <td><code>${f.systemName}</code></td>
                <td>${f.dataType} ${f.isArray ? '<span class="badge bg-secondary">Arr</span>' : ''}</td>
                <td>${f.isRequired ? '<i class="bi bi-check-lg text-success"></i>' : ''}</td>
                <td class="text-end">${actionBtn}</td>
            `;
            tbody.appendChild(tr);
        });
    },

    async delete(id) {
        if (!confirm('Удалить поле?')) return;
        try {
            await FormApiClient.deleteField(id);
            this.loadFields();
        } catch (e) { alert(e.message); }
    },

    async restore(id) {
        try {
            await FormApiClient.restoreField(id);
            this.loadFields();
        } catch (e) { alert(e.message); }
    },

    openCreateModal() {
        const modalEl = document.getElementById('addFieldModal');
        const modal = new bootstrap.Modal(modalEl);
        modal.show();

        const form = modalEl.querySelector('form');
        // Клонируем для удаления старых event listeners
        const newForm = form.cloneNode(true);
        form.parentNode.replaceChild(newForm, form);

        newForm.addEventListener('submit', async (e) => {
            e.preventDefault();

            const formData = new FormData(newForm);
            const dto = {
                appDefinitionId: this.appId,
                label: formData.get('label'),
                systemName: formData.get('systemName'),
                dataType: parseInt(formData.get('dataType')),
                isRequired: formData.get('isRequired') === 'true',
                isArray: formData.get('isArray') === 'true'
            };

            const btn = newForm.querySelector('button[type="submit"]');
            const originalText = btn.innerHTML;
            btn.disabled = true;
            btn.innerHTML = 'Сохранение...';

            try {
                await FormApiClient.createField(dto);
                modal.hide();
                newForm.reset();
                this.loadFields();
            } catch (err) {
                alert('Ошибка: ' + err.message);
            } finally {
                btn.disabled = false;
                btn.innerHTML = originalText;
            }
        });
    }
};