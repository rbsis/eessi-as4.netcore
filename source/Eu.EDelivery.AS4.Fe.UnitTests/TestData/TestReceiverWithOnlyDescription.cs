using System.ComponentModel;

namespace Eu.EDelivery.AS4.Fe.UnitTests.TestData;

[Description("TestReceiverWithOnlyDescription")]
public class TestReceiverWithOnlyDescription : ITestReceiver
{
    [Description("Name")]
    [Info("Name")]
    public required string Name { get; set; }

    [Info("Test", attributes: ["testattribute"])]
    public string? Test { get; set; }
}
