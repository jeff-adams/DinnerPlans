using System;
using Azure;
using Azure.Data.Tables;

namespace DinnerPlansAPI;

public class MenuEntity : ITableEntity
{
    private DateTime date;
    
    public string PartitionKey { get; set; }
    public string RowKey { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
    public DateTime Date { get => date; set {date = value; RowKey = value.ToString("yyyy.MM.dd");}}
    public string MealId { get; set; }
    public string RemovedMealId { get; set; }
}