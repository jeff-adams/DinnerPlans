using System;
using Azure;
using Azure.Data.Tables;

namespace DinnerPlansAPI;

public class RuleEntity : ITableEntity
{
    public string PartitionKey { get; set; }
    public string RowKey { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
    public string Start { get; set; }
    public string End { get; set; }
    public string Catagories { get; set; }
}