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

    async saveLayout({ formId, layoutJson, forceSave = false }) {
        return await fetch(`${this.baseUrl}/SaveLayout`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ formId, layoutJson, forceSave })
        });
    }
};
