using Eu.EDelivery.AS4.Exceptions;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Security.Signing;
using Eu.EDelivery.AS4.Steps.Receive;
using Eu.EDelivery.AS4.UnitTests.Common;
using Eu.EDelivery.AS4.UnitTests.Model;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging.Abstractions;

namespace Eu.EDelivery.AS4.UnitTests.Steps.Receive;

public class GivenCreateAS4ErrorStepFacts : GivenDatastoreFacts
{
    [Property]
    public Property CreatesErrorForEachBundledUserMessage(bool isMultiHop)
    {
        return Prop.ForAll(
            Gen.Fresh(() => new UserMessage($"user-{Guid.NewGuid()}"))
               .NonEmptyListOf()
               .ToArbitrary(),
            userMessages =>
            {
                // Arrange
                var fixture = AS4Message.Create(
                    userMessages,
                    new SendingProcessingMode { MessagePackaging = { IsMultiHop = isMultiHop } });
                IEnumerable<string> fixtureMessageIds = fixture.MessageIds;

                // Act
                var result =
                    CreateErrorStep()
                        .ExecuteAsync(new MessagingContext(fixture, MessagingContextMode.Receive), CancellationToken.None)
                        .GetAwaiter()
                        .GetResult();

                // Assert
                var errorMessage = result.MessagingContext.AS4Message;
                Assert.NotNull(errorMessage);
                Assert.All(
                    errorMessage.MessageUnits,
                    messageUnit =>
                    {
                        Assert.IsType<Error>(messageUnit);
                        var error = (Error)messageUnit;
                        Assert.Contains(error.RefToMessageId, fixtureMessageIds);

                        var expectedId =
                            Maybe.Just(error.RefToMessageId)
                                 .Where(_ => isMultiHop);

                        var actualId =
                            error.MultiHopRouting
                                 .Select(r => r.MessageInfo?.MessageId);

                        Assert.Equal(expectedId, actualId);
                    });
            });
    }

    [Fact]
    public async Task SkipsCreateErrorWhenAS4MessageIsEmpty()
    {
        // Arrange
        var fixture = new MessagingContext(AS4Message.Empty, MessagingContextMode.Receive);

        // Act
        var result = await CreateErrorStep()
            .ExecuteAsync(fixture, CancellationToken.None);

        // Assert
        Assert.Equal(fixture, result.MessagingContext);
    }

    [Fact]
    public async Task CreatesErrorBasedOnErrorResultInformation()
    {
        // Arrange
        var as4Message = CreateFilledAS4Message();
        var fixture = new MessagingContext(
            as4Message,
            MessagingContextMode.Unknown)
        {
            ErrorResult = new ErrorResult("error", ErrorAlias.ConnectionFailure),
            ReceivingPMode = new ReceivingProcessingMode()
        };

        // Act
        var result = await CreateErrorStep().ExecuteAsync(fixture, CancellationToken.None);

        // Assert
        Assert.NotNull(result.MessagingContext.AS4Message);
        var error = result.MessagingContext.AS4Message.FirstSignalMessage as Error;

        Assert.NotNull(error);
        Assert.Equal("message-id", error.RefToMessageId);
        Assert.Equal(ErrorCode.Ebms0005, error.ErrorLines.First().ErrorCode);
    }

    [Fact]
    public async Task CreatesErrorWithSameSigningIdAsReceivedUserMessage()
    {
        // Arrange
        var as4Message = CreateFilledAS4Message();
        as4Message.SigningId = new SigningId("header-id", "body-id");

        var fixture = new MessagingContext(
            as4Message,
            MessagingContextMode.Unknown)
        {
            ReceivingPMode = new ReceivingProcessingMode()
        };

        // Act
        var result = await CreateErrorStep().ExecuteAsync(fixture, CancellationToken.None);

        // Assert
        Assert.NotNull(result.MessagingContext.AS4Message);
        Assert.Equal(as4Message.SigningId, result.MessagingContext.AS4Message.SigningId);
    }

    [Fact]
    public async Task CreatesMultiHopErrorIfReceivedUserMessageIsMultiHop()
    {
        // Arrange
        var ctx = new MessagingContext(
            AS4Message.Create(
                new UserMessage($"user-{Guid.NewGuid()}"),
                new SendingProcessingMode { MessagePackaging = { IsMultiHop = true } }),
            MessagingContextMode.Receive)
        {
            ReceivingPMode = new ReceivingProcessingMode()
        };

        // Act
        var actual = await ExerciseCreateError(ctx);

        // Assert
        Assert.IsType<Error>(actual.PrimaryMessageUnit);
        Assert.True(actual.IsMultiHopMessage, "Is not multi-hop message");
    }

    private static AS4Message CreateFilledAS4Message() => AS4Message.Create(new FilledUserMessage());

    private CreateAS4ErrorStep CreateErrorStep() => new(
        NullLogger<CreateAS4ErrorStep>.Instance,
        Default.NewDatastoreRepository(this),
        Default.IdentifierFactory);

    private async Task<AS4Message> ExerciseCreateError(MessagingContext ctx)
    {
        var sut = CreateErrorStep();
        var result = await sut.ExecuteAsync(ctx, CancellationToken.None);

        return result.MessagingContext.AS4Message ?? throw new InvalidOperationException();
    }
}
