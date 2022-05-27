using System;
using Azure;
using Azure.Data.Tables;

namespace DinnerPlansAPI;

public class MealEntity : ITableEntity
{
    public string PartitionKey { get; set; }
    public string RowKey { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
    public string Id { get => RowKey; set => RowKey = value;}
    public string Name { get; set; }
    public string Catagories { get; set; }
    public string Seasons { get; set; }
    public string Recipe { get; set; }
    public int Rating { get; set; }
    public DateTime LastOnMenu { get; set; }
    public DateTime NextOnMenu { get; set; }
}