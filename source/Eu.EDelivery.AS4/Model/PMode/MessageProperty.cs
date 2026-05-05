namespace Eu.EDelivery.AS4.Model.PMode;

public sealed class MessageProperty
{
    public required string Name { get; set; }

    public required string Value { get; set; }

    public string? Type { get; set; }
}
