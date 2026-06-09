using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Steps.Submit;
using Eu.EDelivery.AS4.TestUtils.Stubs;
using Eu.EDelivery.AS4.UnitTests.Model.PMode;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Eu.EDelivery.AS4.UnitTests.Steps.Submit;

/// <summary>
/// Testing <see cref="CreateDefaultAS4MessageStep"/>
/// </summary>
public class GivenCreateDefaultAS4MessageStepFacts : PseudoConfig
{
    [Property]
    public void CreatesExpectedMessageFromPMode(NonEmptyString id)
    {
        // Arrange
        var message = AS4Message.Empty;
        var attachment = new Attachment(id.Get);
        message.AddAttachment(attachment);

        var sut = new CreateDefaultAS4MessageStep(
            Substitute.For<ILogger<CreateDefaultAS4MessageStep>>(),
            config: this,
            Default.SendingPModeMap);

        // Act
        var result = sut.ExecuteAsync(
            new MessagingContext(message, MessagingContextMode.Unknown), CancellationToken.None)
                .GetAwaiter()
                .GetResult();

        // Assert
        Assert.NotNull(result.MessagingContext.AS4Message);
        Assert.Collection(
            result.MessagingContext.AS4Message.UserMessages,
            m => Assert.Contains(attachment.Id, m.PayloadInfo.First().Href));
    }

    /// <summary>
    /// Retrieve the PMode from the Global Settings
    /// </summary>
    /// <param name="id"></param>
    /// <exception cref="Exception"></exception>
    /// <returns></returns>
    public override SendingProcessingMode GetSendingPMode(string id)
    {
        var pmode = ValidSendingPModeFactory.Create();
        pmode.MessagePackaging.MessageProperties = [];

        return pmode;
    }
}
