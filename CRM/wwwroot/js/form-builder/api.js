export const FormApiClient = {
    baseUrl: '/api/FormConfig',

    async getFields(appId, includeDeleted = false) {
        const response = await fetch(`${this.baseUrl}/GetFields?appId=${appId}&includeDeleted=${includeDeleted}`);
        if (!response.ok) throw new Error('Ошибка загрузки полей');
        return await response.json();
    },

    async createField(data) {
        const response = await fetch(`${this.baseUrl}/CreateField`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(data)
        });
        if (!response.ok) {
            const err = await response.text();
            throw new Error(err || 'Ошибка создания поля');
        }
        return await response.json();
    },

    async updateField(data) {
        const response = await fetch(`${this.baseUrl}/UpdateField`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(data)
        });
        if (!response.ok) {
            const err = await response.text();
            throw new Error(err || 'Ошибка обновления поля');
        }
        return await response.json();
    },

    async deleteField(id) {
        const response = await fetch(`${this.baseUrl}/DeleteField?id=${id}`, { method: 'POST' });
        if (!response.ok) throw new Error('Ошибка удаления');
        return response;
    },

    async restoreField(id) {
        const response = await fetch(`${this.baseUrl}/RestoreField?id=${id}`, { method: 'POST' });
        if (!response.ok) throw new Error('Ошибка восстановления');
        return response;
    },

    async createForm(data) {
        const response = await fetch(`${this.baseUrl}/CreateForm`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(data)
        });
        if (!response.ok) {
            const err = await response.text();
            throw new Error(err || 'Ошибка создания формы');
        }
        return await response.json();
    },

    async deleteForm(id) {
        const response = await fetch(`${this.baseUrl}/DeleteForm?id=${id}`, { method: 'POST' });
        if (!response.ok) {
            const err = await response.text();
            throw new Error(err || 'Ошибка удаления формы');
        }
        return response;
    },

    async renameForm(data) {
        const response = await fetch(`${this.baseUrl}/RenameForm`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(data)
        });
        if (!response.ok) {
            const err = await response.text();
            throw new Error(err || 'Ошибка переименования формы');
        }
        return response;
    },

    async setDefaultForm(id) {
        const response = await fetch(`${this.baseUrl}/SetDefaultForm?id=${id}`, { method: 'POST' });
        if (!response.ok) {
            const err = await response.text();
            throw new Error(err || 'Ошибка назначения основной формы');
        }
        return response;
    },

    async saveLayout({ formId, layoutJson, forceSave = false, appDefinitionId, formType }) {
        return await fetch(`${this.baseUrl}/SaveLayout`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ formId, appDefinitionId, formType, layoutJson, forceSave })
        });
    }
};
