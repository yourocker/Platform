using Microsoft.EntityFrameworkCore;

namespace Core.Specifications
{
    public static class SpecificationEvaluator
    {
        public static IQueryable<TEntity> GetQuery<TEntity>(
            IQueryable<TEntity> inputQuery, 
            ISpecification<TEntity> spec) where TEntity : class
        {
            var query = inputQuery;

            // 1. Применяем фильтры (Where)
            if (spec.Criteria != null)
            {
                foreach (var criterion in spec.Criteria)
                {
                    query = query.Where(criterion);
                }
            }

            // 2. Применяем Includes (JOIN)
            foreach (var include in spec.Includes)
            {
                query = query.Include(include);
            }

            // 3. Применяем сортировку
            if (spec.OrderBy != null)
            {
                query = query.OrderBy(spec.OrderBy);
            }
            else if (spec.OrderByDescending != null)
            {
                query = query.OrderByDescending(spec.OrderByDescending);
            }

            // 4. Применяем пагинацию (LIMIT / OFFSET)
            if (spec.IsPagingEnabled)
            {
                query = query.Skip(spec.Skip).Take(spec.Take);
            }

            return query;
        }
    }
}