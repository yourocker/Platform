using System.Linq.Expressions;

namespace Core.Specifications
{
    public interface ISpecification<T>
    {
        // Условия фильтрации (Where)
        List<Expression<Func<T, bool>>> Criteria { get; }
        
        // Связи для подгрузки (Include)
        List<Expression<Func<T, object>>> Includes { get; }
        
        // Сортировка
        Expression<Func<T, object>>? OrderBy { get; }
        Expression<Func<T, object>>? OrderByDescending { get; }
        
        // Пагинация
        int Take { get; }
        int Skip { get; }
        bool IsPagingEnabled { get; }
    }
}