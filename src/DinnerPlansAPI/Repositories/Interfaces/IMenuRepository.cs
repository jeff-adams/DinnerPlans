using System;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace DinnerPlansAPI.Repositories;

public interface IMenuRepository
{
    Task<MenuEntity> GetMenuEntityAsync(string menuKey);
    Task<MenuEntity> AddMenuEntityAsync(MenuEntity menu);
    Task<MenuEntity> UpdateMenuEntityAsync(MenuEntity menu);
    Task<IEnumerable<MenuEntity>> QueryMenuEntityAsync(Func<MenuEntity, bool> filter);
}