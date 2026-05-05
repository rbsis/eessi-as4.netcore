using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Services.Journal;
using Eu.EDelivery.AS4.Steps;

namespace Eu.EDelivery.AS4.UnitTests.Steps;

public class GivenStepExecutionerFacts
{
    [Fact]
    public async Task CollectsJournalsThroughoutExecution()
    {
        // Arrange
        var sut = new StepExecutioner(
            normalPipeline:
            [
                new AddJournalLogEntryStep1(),
                new AddJournalLogEntryStep2(),
                new AddJournalLogEntryReceiptStep1(),
                new AddJournalLogEntryStep3(),
            ],
            errorPipeline:
            [
                new AddJournalLogEntryStep4()
            ],
            Default.LogExceptionHandler);

        var userMessage = new UserMessage($"user-{Guid.NewGuid()}");

        // Act
        var result = await sut.ExecuteStepsAsync(
            new MessagingContext(
                AS4Message.Create(userMessage),
                MessagingContextMode.Unknown), CancellationToken.None);

        // Assert
        Assert.Collection(
            result.Journal.First(j => j.EbmsMessageId == userMessage.MessageId).LogEntries,
            e => Assert.Equal("Log entry 1", e),
            e => Assert.Equal("Log entry 2", e),
            e => Assert.Equal("Log entry 3", e),
            e => Assert.Equal("Log entry 4", e));

        Assert.Collection(
            result.Journal.First(j => j.RefToMessageId == userMessage.MessageId).LogEntries,
            e => Assert.Equal("Log entry 1", e));
    }
}

public class AddJournalLogEntryStep1 : IStep
{
    public Task<StepResult> ExecuteAsync(MessagingContext messagingContext, CancellationToken cancellation) => StepResult
        .Success(messagingContext)
        .WithJournalAsync(
            JournalLogEntry.CreateFrom(
                messagingContext.AS4Message!,
                "Log entry 1"));
}

public class AddJournalLogEntryStep2 : IStep
{
    public Task<StepResult> ExecuteAsync(MessagingContext messagingContext, CancellationToken cancellation) => StepResult
        .Success(messagingContext)
        .WithJournalAsync(
            JournalLogEntry.CreateFrom(
                messagingContext.AS4Message!,
                "Log entry 2"));
}

public class AddJournalLogEntryStep3 : IStep
{
    public Task<StepResult> ExecuteAsync(MessagingContext messagingContext, CancellationToken cancellation) => StepResult
        .Failed(messagingContext)
        .WithJournalAsync(
            JournalLogEntry.CreateFrom(
                messagingContext.AS4Message!,
                "Log entry 3"));
}

public class AddJournalLogEntryStep4 : IStep
{
    public Task<StepResult> ExecuteAsync(MessagingContext messagingContext, CancellationToken cancellation) => StepResult
        .Success(messagingContext)
        .WithJournalAsync(
            JournalLogEntry.CreateFrom(
                messagingContext.AS4Message!,
                "Log entry 4"));
}

public class AddJournalLogEntryReceiptStep1 : IStep
{
    public Task<StepResult> ExecuteAsync(MessagingContext messagingContext, CancellationToken cancellation) => StepResult
        .Success(messagingContext)
        .WithJournalAsync(
            JournalLogEntry.CreateFrom(
                AS4Message.Create(new Receipt(
                    $"receipt-{Guid.NewGuid()}",
                    messagingContext.AS4Message!.PrimaryMessageUnit!.MessageId)),
                "Log entry 1"));
}
