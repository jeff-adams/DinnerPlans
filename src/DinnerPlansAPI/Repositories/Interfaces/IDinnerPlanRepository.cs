using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Azure.Data.Tables;

namespace DinnerPlansAPI.Repositories;

public interface IDinnerPlanRepository<T>
{
    Task<T> GetEntityAsync(string key);
    Task AddEntityAsync(T entity);
    Task UpdateEntityAsync(T entity);
    Task<IReadOnlyCollection<T>> QueryEntityAsync(Expression<Func<T, bool>> filter);
}