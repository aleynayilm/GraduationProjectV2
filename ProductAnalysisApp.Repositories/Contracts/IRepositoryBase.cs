using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace ProductAnalysisApp.Repositories.Contracts
{
    public interface IRepositoryBase<T>
    {
        IQueryable<T> FindAll();
        IQueryable<T> FindByCondition(Expression<Func<T, bool>> expression);
        Task CreateAsync(T entity);
        Task UpdateAsync(Expression<Func<T, bool>> filterExpression, T entity);
        Task DeleteAsync(Expression<Func<T, bool>> filterExpression);
    }
}
