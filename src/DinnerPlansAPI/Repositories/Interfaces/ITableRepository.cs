using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace DinnerPlansAPI.Repositories;

public interface ITableRepository<T>
{
    string DefaultPartitionKey { get; }
    Task<T> GetEntityAsync(string key);
    Task<T> GetEntityAsync(string partitionKey, string key);
    Task AddEntityAsync(T entity);
    Task UpdateEntityAsync(T entity);
    Task UpsertEntityAsync(T entity);
    Task DeleteEntityAsync(string key);
    Task DeleteEntityAsync(string partitionKey, string key);
    Task<IReadOnlyCollection<T>> QueryEntityAsync(Expression<Func<T, bool>> filter);
}