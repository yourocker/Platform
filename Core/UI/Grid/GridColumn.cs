using System;

namespace Core.UI.Grid
{
    public class GridColumn<T>
    {
        /// <summary>
        /// Заголовок колонки
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Функция для получения данных из объекта (x => x.FullName)
        /// </summary>
        public Func<T, object>? ValueProvider { get; set; }

        /// <summary>
        /// Системное имя для CSS класса (col-lastname) и сохранения настроек
        /// </summary>
        public string SystemName { get; set; } = string.Empty;

        /// <summary>
        /// Тип отображения
        /// </summary>
        public GridColumnType Type { get; set; } = GridColumnType.Text;

        /// <summary>
        /// Видимость по умолчанию
        /// </summary>
        public bool VisibleByDefault { get; set; } = true;

        /// <summary>
        /// Ключ для динамических полей (если Type == Dynamic)
        /// </summary>
        public string? DynamicKey { get; set; }
        
        /// <summary>
        /// Ключ сортировки
        /// </summary>
        public string? SortKey { get; set; }
    }
}