using MongoDB.Driver;
using ProductAnalysisApp.Repositories.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace ProductAnalysisApp.Repositories.EFCore
{
    public class MongoRepositoryBase<T> : IRepositoryBase<T>
    {
        protected readonly IMongoCollection<T> _collection;

        public MongoRepositoryBase(IMongoCollection<T> collection)
        {
            _collection = collection;
        }

        public IQueryable<T> FindAll() => _collection.AsQueryable();

        public IQueryable<T> FindByCondition(Expression<Func<T, bool>> expression) => _collection.AsQueryable().Where(expression);

        public async Task CreateAsync(T entity) => await _collection.InsertOneAsync(entity);

        public async Task UpdateAsync(Expression<Func<T, bool>> filterExpression, T entity)
        {
            var filter = Builders<T>.Filter.Where(filterExpression);
            await _collection.ReplaceOneAsync(filter, entity);
        }

        public async Task DeleteAsync(Expression<Func<T, bool>> filterExpression)
        {
            var filter = Builders<T>.Filter.Where(filterExpression);
            await _collection.DeleteOneAsync(filter);
        }
    }
}
