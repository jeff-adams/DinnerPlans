using System;
using Azure;
using Azure.Data.Tables;

namespace DinnerPlansAPI;

public class MenuEntity : ITableEntity
{
    public string PartitionKey { get; set; }
    public string RowKey { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
    public DateTime Date { get => DateTime.Parse(RowKey); set => RowKey = value.ToString("yyyy.MM.DD");}
    public string MealId { get; set; }
    public string RemovedMealId { get; set; }
}