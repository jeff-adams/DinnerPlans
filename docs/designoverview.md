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
| LastOnMenu | DateTime |
| NextOnMenu | DateTime |

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
---

# API Functions

## Meal
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

## Menu
#### Get Menu By Date Range
> GET api/menu
#### Get Today's Menu
> GET api/menu/today
#### Create Menu
> PUT api/menu
#### Update Menu
> POST api/menu
#### Delete Menu
> DELETE api/menu

---

[TableClient examples](https://medium.com/geekculture/using-the-new-c-azure-data-tables-sdk-with-azure-cosmos-db-786085ac8190)

HttpTrigger Function (Authentication) 
-> SPA w/ JWT 
-> some server w/ JWT 
-> REST API w/ API Key 
-> Azure Table Storage 