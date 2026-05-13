using Eu.EDelivery.AS4.Common;
using Eu.EDelivery.AS4.Model.Common;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Model.Submit;
using Eu.EDelivery.AS4.Steps.Submit;
using Eu.EDelivery.AS4.UnitTests.Model.PMode;
using Microsoft.Extensions.Logging;
using Moq;
using NSubstitute;
using CollaborationInfo = Eu.EDelivery.AS4.Model.Common.CollaborationInfo;

namespace Eu.EDelivery.AS4.UnitTests.Steps.Submit;

/// <summary>
/// Testing <see cref="RetrieveSendingPModeStep" />
/// </summary>
public class GivenRetrieveSendingPModeStepFacts
{
    [Fact]
    public async Task FailsToRetrievePModeIfInvalidPMode()
    {
        // Arrange
        const string PModeId = "01-pmode";
        var internalMessage = new MessagingContext(GetStubSubmitMessage(PModeId));

        var invalidPMode = ValidSendingPModeFactory.Create(PModeId);
        invalidPMode.ReceiptHandling.NotifyMessageProducer = true;

        var sut = new RetrieveSendingPModeStep(
            Substitute.For<ILogger<RetrieveSendingPModeStep>>(),
            CreateStubConfigWithSendingPMode(invalidPMode),
            Default.SendingProcessingModeValidator);

        // Act / Assert
        await Assert.ThrowsAnyAsync<Exception>(() => sut.ExecuteAsync(internalMessage, CancellationToken.None));
    }

    private static SubmitMessage GetStubSubmitMessage(string pmodeId)
    {
        return new SubmitMessage
        {
            Collaboration = new CollaborationInfo { AgreementRef = new Agreement { PModeId = pmodeId } }
        };
    }

    private static IConfig CreateStubConfigWithSendingPMode(SendingProcessingMode pmode)
    {
        var stubConfig = new Mock<IConfig>();
        stubConfig.Setup(c => c.GetSendingPMode(It.IsAny<string>())).Returns(pmode);

        return stubConfig.Object;
    }
}
