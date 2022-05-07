# Dinner Plans

| Components | .Net Project
|---|---|
| Meal/Menu API | ASP.Net Minimal API
| Photo Uploader | Azure Function
| UI | Blazor Web App

### Azure Resources
- Azure Web App
- Azure Function
- Azure Storage Account
- Azure Keyvault

### Meal
| Key | Value Type |
|---|---|
| Id | Guid |
| Name | string |
| Catagory | string |
| Recipe | string |
| PhotoUrl | string |
| Rating | int |
| AmountManuallyChanged | int |

### Menu
| Key | Value Type |
|---|---|
| Date | DateTime |
| Meal | Guid |
