using System;
using System.Collections.Generic;
using Core.Entities.Platform;

namespace Core.UI.Grid
{
    public class GridConfig<T>
    {
        public List<GridColumn<T>> Columns { get; } = new();

        /// <summary>
        /// Добавить стандартную колонку
        /// </summary>
        public GridConfig<T> AddColumn(
            Func<T, object> provider, 
            string title, 
            string systemName, 
            GridColumnType type = GridColumnType.Text, 
            bool visible = true,
            string? sortKey = null)
        {
            Columns.Add(new GridColumn<T>
            {
                ValueProvider = provider,
                Title = title,
                SystemName = systemName,
                Type = type,
                VisibleByDefault = visible,
                SortKey = sortKey
            });
            return this;
        }

        /// <summary>
        /// Автоматически добавить все динамические поля
        /// </summary>
        public GridConfig<T> AddDynamicFields(List<AppFieldDefinition> fields)
        {
            if (fields == null) return this;

            foreach (var field in fields)
            {
                Columns.Add(new GridColumn<T>
                {
                    Title = field.Label,
                    SystemName = $"dyn-{field.SystemName}", // Префикс для уникальности
                    Type = GridColumnType.Dynamic,
                    DynamicKey = field.SystemName,
                    VisibleByDefault = true,
                    SortKey = field.SystemName
                });
            }
            return this;
        }

        /// <summary>
        /// Добавить колонку действий (справа)
        /// </summary>
        public GridConfig<T> AddActions()
        {
            Columns.Add(new GridColumn<T>
            {
                Title = "",
                SystemName = "actions",
                Type = GridColumnType.Actions,
                VisibleByDefault = true,
                SortKey = null
            });
            return this;
        }
    }
}