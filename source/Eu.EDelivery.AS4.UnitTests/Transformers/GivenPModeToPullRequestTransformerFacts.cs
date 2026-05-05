using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Serialization;
using Eu.EDelivery.AS4.Transformers;
using Eu.EDelivery.AS4.UnitTests.Model.PMode;
using Microsoft.Extensions.Logging.Abstractions;

namespace Eu.EDelivery.AS4.UnitTests.Transformers;

/// <summary>
/// Testing <see cref="PModeToPullRequestTransformer" />
/// </summary>
public class GivenPModeToPullRequestTransformerFacts
{
    /// <summary>
    /// Gets the received message source.
    /// </summary>
    /// <value>
    /// The received message source.
    /// </value>
    public static IEnumerable<object[]> ReceivedMessageSource
    {
        get
        {
            yield return new object[] { new ReceivedMessage(underlyingStream: Stream.Null) };
        }
    }

    [Theory]
    [MemberData(nameof(ReceivedMessageSource))]
    public async Task FailsWithNoPullConfigurationSection(ReceivedMessage receivedMessage)
    {
        // Arrange
        var transformer = new PModeToPullRequestTransformer(NullLogger<PModeToPullRequestTransformer>.Instance, Default.SendingProcessingModeValidator, Default.IdentifierFactory);

        // Act / Assert
        await Assert.ThrowsAnyAsync<Exception>(
            () => transformer.TransformAsync(receivedMessage, CancellationToken.None));
    }

    [Fact]
    public async Task SucceedsWithAValidPullConfiguration()
    {
        // Arrange
        const string ExpectedMpc = "expected-mpc";
        var expectedSendingPMode = CreateAnonymousSendingPModeWith(ExpectedMpc);
        var receivedMessage = new ReceivedMessage(await AS4XmlSerializer.ToStreamAsync(expectedSendingPMode, CancellationToken.None));

        var transformer = new PModeToPullRequestTransformer(NullLogger<PModeToPullRequestTransformer>.Instance, Default.SendingProcessingModeValidator, Default.IdentifierFactory);

        // Act
        using var context = await transformer.TransformAsync(receivedMessage, CancellationToken.None);
        // Assert
        Assert.NotNull(context.AS4Message);
        Assert.True(context.AS4Message.IsPullRequest);

        var actualSignalMessage = context.AS4Message.FirstSignalMessage as PullRequest;
        Assert.NotNull(actualSignalMessage);
        Assert.Equal(ExpectedMpc, actualSignalMessage.Mpc);
        Assert.NotNull(context.SendingPMode);
        Assert.Equal(expectedSendingPMode.Id, context.SendingPMode.Id);
        Assert.Equal(MessagingContextMode.PullReceive, context.Mode);
    }

    private static SendingProcessingMode CreateAnonymousSendingPModeWith(string expectedMpc)
    {
        var expectedSendingPMode = ValidSendingPModeFactory.Create("expected-id");
        expectedSendingPMode.MessagePackaging.Mpc = expectedMpc;

        return expectedSendingPMode;
    }
}
