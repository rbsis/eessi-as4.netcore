using System.ComponentModel.DataAnnotations;

namespace Eu.EDelivery.AS4.Receivers;

public sealed class DatastoreReceiverSettings
{
    [Required(AllowEmptyStrings = false)]
    public required string TableName { get; set; }
    [Required(AllowEmptyStrings = false)]
    public required string Filter { get; set; }
    [Required(AllowEmptyStrings = false)]
    public required string UpdateField { get; set; }
    [Required(AllowEmptyStrings = false)]
    public required string UpdateValue { get; set; }
    public TimeSpan PollingInterval { get; set; } = DefaultPollingInterval;
    public int TakeRows { get; set; } = DefaultTakeRows;

    public const int DefaultTakeRows = 20;
    public static readonly TimeSpan DefaultPollingInterval = TimeSpan.FromSeconds(3);

    public string DisplayString => $"FROM {TableName} WHERE {Filter} LIMIT {TakeRows}";
}
