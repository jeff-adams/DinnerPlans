using System;
using Azure;
using Azure.Data.Tables;

namespace DinnerPlansAPI;

public class SpecialDateEntity : ITableEntity
{
    public string PartitionKey { get; set; }
    public string RowKey { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
    public string MealId { get; set; }
}