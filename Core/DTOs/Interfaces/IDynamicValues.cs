using System.Collections.Generic;

namespace Core.DTOs.Interfaces
{
    public interface IDynamicValues
    {
        // Словарь для хранения значений динамических полей (вместо JSON строки)
        Dictionary<string, object> DynamicValues { get; set; }
    }
}