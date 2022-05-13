﻿using Azure;
using Azure.Data.Tables;

namespace DinnerPlansCommon;

public record Menu(DateTime Date,
                   string MealId,
                   string RemovedMealId);

public record Option(string Seasons,
                     string Catagories);

public class MealEntity : ITableEntity
{
    public string? PartitionKey { get; set; }
    public string? RowKey { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
    public string? Id { get => RowKey; set => RowKey = value;}
    public string? Name { get; set; }
    public string? Catagories { get; set; }
    public string? Recipe { get; set; }
    public int Rating { get; set; }
    public int Priority { get; set; }
}
