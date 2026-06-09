using System.Text;
using Eu.EDelivery.AS4.Common;
using Eu.EDelivery.AS4.Exceptions;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Streaming;
using Eu.EDelivery.AS4.TestUtils.Stubs;
using Eu.EDelivery.AS4.Transformers;
using Eu.EDelivery.AS4.UnitTests.Extensions;
using Eu.EDelivery.AS4.UnitTests.Streaming;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Eu.EDelivery.AS4.UnitTests.Transformers;

public class GivenReceiveMessageTransformerFacts
{
    [Fact]
    public async Task ThrowsInvalidMessageWhenIncomingStreamIsntAS4Message()
    {
        // Arrange
        Stream str = new MemoryStream(
            Encoding.UTF8.GetBytes(
                "<root>This is definitly not an AS4Message!</root>"));

        var incoming = new ReceivedMessage(str, Constants.ContentTypes.Soap);
        var sut = new ReceiveMessageTransformer(NullLogger<ReceiveMessageTransformer>.Instance, StubConfig.Default, Default.IdentifierFactory, Default.SerializerProvider);

        // Act / Assert
        await Assert.ThrowsAsync<InvalidMessageException>(
            () => sut.TransformAsync(incoming, CancellationToken.None));
    }

    [CustomProperty]
    public void ThrowsInvalidMessageWhenReceivingSignalMessageWhileHavingAReceivingPModeConfigured(SignalMessage s)
    {
        // Arrange
        var receipt = AS4Message.Create(s);
        var incoming = new ReceivedMessage(receipt.ToStream(), Constants.ContentTypes.Soap);

        var sut = new ReceiveMessageTransformer(NullLogger<ReceiveMessageTransformer>.Instance, StubConfig.Default, Default.IdentifierFactory, Default.SerializerProvider);
        sut.Configure(new Dictionary<string, string> { [ReceiveMessageTransformer.ReceivingPModeKey] = "pmode-id" });

        // Act / Assert
        Assert.Throws<InvalidMessageException>(
            () => sut.TransformAsync(incoming, CancellationToken.None).GetAwaiter().GetResult());

    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WrapsIntoVirtualStreamIfCantSeek(bool canSeek)
    {
        // Arrange
        var stub = new StubStream(canSeek, AS4Message.Empty.ToStream());
        var sut = new ReceiveMessageTransformer(NullLogger<ReceiveMessageTransformer>.Instance, StubConfig.Default, Default.IdentifierFactory, Default.SerializerProvider);

        // Act
        var result = await sut.TransformAsync(new ReceivedMessage(stub, Constants.ContentTypes.Soap), CancellationToken.None);

        // Assert
        Assert.NotNull(result.ReceivedMessage);
        Assert.True(
            result.ReceivedMessage.UnderlyingStream is VirtualStream != canSeek,
            "Incoming stream isn't wrapped in 'VirtualStream'");
    }


    [Theory]
    [InlineData("none-existing-id")]
    public async Task ReturnsWithErrorPModeNotFoundWhenReceivePModeIsNotDefined(string id)
    {
        // Arrange
        var stub = new Mock<IConfig>();
        stub.Setup(c => c.GetReceivingPModes())
            .Returns([new ReceivingProcessingMode { Id = "existing-id" }]);

        var sut = new ReceiveMessageTransformer(NullLogger<ReceiveMessageTransformer>.Instance, stub.Object, Default.IdentifierFactory, Default.SerializerProvider);
        sut.Configure(
            new Dictionary<string, string>
            { [ReceiveMessageTransformer.ReceivingPModeKey] = id });

        var msg = new ReceivedMessage(
            AS4Message.Empty.ToStream(),
            Constants.ContentTypes.Soap);

        // Act
        var actual = await sut.TransformAsync(msg, CancellationToken.None);

        // Assert
        var primaryMessageUnit = actual.AS4Message?.MessageUnits.First();
        Assert.IsType<Error>(primaryMessageUnit);
        var error = (Error)primaryMessageUnit;

        Assert.Equal(
            ErrorAlias.ProcessingModeMismatch,
            error.ErrorLines.First().ShortDescription);
    }

    [Theory]
    [InlineData("existing-id")]
    [InlineData(null)]
    public async Task AddsReceivePModeWhenPModeSettingIsDefined(string? id)
    {
        // Arrange
        var stub = new Mock<IConfig>();
        stub.Setup(c => c.GetReceivingPModes())
            .Returns([new ReceivingProcessingMode { Id = "existing-id" }]);

        var sut = new ReceiveMessageTransformer(NullLogger<ReceiveMessageTransformer>.Instance, stub.Object, Default.IdentifierFactory, Default.SerializerProvider);
        sut.Configure(
            new Dictionary<string, string>
            {
                [ReceiveMessageTransformer.ReceivingPModeKey] = string.Empty
            });

        var msg = new ReceivedMessage(AS4Message.Empty.ToStream(), Constants.ContentTypes.Soap);

        // Act
        var result = await sut.TransformAsync(msg, CancellationToken.None);

        // Assert
        var expectedNotConfiguredPMode = result.ReceivingPMode == null;
        var expectedConfiguredPMode = result.ReceivingPMode?.Id == id;
        Assert.True(expectedNotConfiguredPMode || expectedConfiguredPMode);
    }
}
