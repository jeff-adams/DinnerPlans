# Dinner Plans

| Components | .Net Project
|---|---|
| Meal API | Azure Function
| Menu API | Azure Function
| MenuPlanner | Azure Function
| UI | Blazor Web App

### Azure Resources
- Azure Function
- Azure Storage Account
- Azure Keyvault?

---

### Meal Table
| Key | Value Type |
|---|---|
| Id | Guid |
| Name | string |
| Catagories | string[] |
| Seasons | string[] |
| Recipe | string |
| Rating | int |
| Priority | int |

### Menu Table
| Key | Value Type |
|---|---|
| Date | DateTime |
| Meal | Guid |
| RemovedMeal | Guid |

### Option Table
| Key | Value Type |
|---|---|
| Seasons | string[] |
| Catagories | string[] |

---

## API Functions

### Meal
#### Get Meal By ID
> GET api/meal/{id}
#### Get All Meals
> GET api/meal
#### Create Meal
> PUT api/meal
#### Update Meal
> POST api/meal/{id}
#### Delete Meal
> DELETE api/meal/{id}

### Menu
#### Get Menu By Date Range
> GET api/menu/begin={DateTime}&end={DateTime}
#### Create Menu
> PUT api/menu
#### Update Menu
> POST api/menu/{DateTime}
#### Delete Menu
> DELETE api/menu/{DateTime}