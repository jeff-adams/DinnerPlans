using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace DinnerPlansAPI.Repositories;

public interface ITableRepository<T>
{
    string PartitionKey { get; }
    Task<T> GetEntityAsync(string key);
    Task AddEntityAsync(T entity);
    Task UpdateEntityAsync(T entity);
    Task UpsertEntityAsync(T entity);
    Task DeleteEntityAsync(string key);
    Task<IReadOnlyCollection<T>> QueryEntityAsync(Expression<Func<T, bool>> filter);
}