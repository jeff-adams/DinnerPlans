using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace DinnerPlansAPI.Repositories;

public interface IMenuRepository
{
    Task<MenuEntity> GetMenuEntityAsync(string menuKey);
    Task AddMenuEntityAsync(MenuEntity menu);
    Task UpdateMenuEntityAsync(MenuEntity menu);
    Task<IReadOnlyCollection<MenuEntity>> QueryMenuEntityAsync(Expression<Func<MenuEntity, bool>> filter);
}