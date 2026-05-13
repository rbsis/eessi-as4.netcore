using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Exceptions;
using Eu.EDelivery.AS4.Exceptions.Handlers;
using Eu.EDelivery.AS4.Extensions;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Deliver;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.UnitTests.Common;
using Eu.EDelivery.AS4.UnitTests.Model;
using Eu.EDelivery.AS4.UnitTests.Model.Notify;
using Eu.EDelivery.AS4.UnitTests.Repositories;
using RetryReliability = Eu.EDelivery.AS4.Model.PMode.RetryReliability;

namespace Eu.EDelivery.AS4.UnitTests.Exceptions.Handlers;

public class GivenInboundExceptionHandlerFacts : GivenDatastoreFacts
{
    private readonly string _expectedId = Guid.NewGuid().ToString();

    private readonly InboundExceptionHandler _sut;

    public GivenInboundExceptionHandlerFacts()
    {
        _sut = Default.NewInboundExceptionHandler(this);
    }

    [Property]
    public void SetRetryInfoWhenReceivingPModeIsConfiguredForRetry(
        bool enabled,
        PositiveInt count,
        TimeSpan interval)
    {
        // Arrange
        ClearInExceptions();
        var pmode = new ReceivingProcessingMode();

        pmode.ExceptionHandling.Reliability =
            new RetryReliability
            {
                IsEnabled = enabled,
                RetryCount = count.Get,
                RetryInterval = interval.ToString("G")
            };

        var entity = new InMessage($"entity-{Guid.NewGuid()}");
        GetDataStoreContext.InsertInMessage(entity);

        // Act
        _sut.HandleExecutionExceptionAsync(
            new Exception(),
            new MessagingContext(
                new ReceivedEntityMessage(entity),
                MessagingContextMode.Deliver)
            {
                ReceivingPMode = pmode
            },
            CancellationToken.None)
           .GetAwaiter()
           .GetResult();

        // Assert
        GetDataStoreContext.AssertInException(ex =>
        {
            Assert.NotNull(ex);
            Assert.Null(ex.MessageLocation);
            GetDataStoreContext.AssertRetryRelatedInException(
                ex.Id,
                rr =>
                {
                    Assert.True(
                        enabled == (0 == rr?.CurrentRetryCount),
                        "CurrentRetryCount != 0 when RetryReliability is enabled");
                    Assert.True(
                        enabled == (count.Get == rr?.MaxRetryCount),
                        enabled
                            ? $"Max retry count failed on enabled: {count.Get} != {rr?.MaxRetryCount}"
                            : $"Max retry count should be 0 on disabled but is {rr?.MaxRetryCount}");
                    Assert.True(
                        enabled == (interval == rr?.RetryInterval),
                        enabled
                            ? $"Retry interval failed on enabled: {interval:G} != {rr?.RetryInterval}"
                            : $"Retry interval should be 0:00:00 on disabled but is {rr?.RetryInterval}");
                });
        });
    }

    private void ClearInExceptions()
    {
        using var ctx = GetDataStoreContext();
        ctx.InExceptions.RemoveRange(ctx.InExceptions);
        ctx.SaveChanges();
    }

    [Fact]
    public async Task InsertInExceptionIfTransformException()
    {
        // Arrange
        string expectedBody = Guid.NewGuid().ToString(),
            expectedMessage = Guid.NewGuid().ToString();

        // Act
        await _sut.ExerciseTransformException(GetDataStoreContext, expectedBody, new Exception(expectedMessage));

        // Assert
        GetDataStoreContext.AssertInException(
            ex =>
            {
                Assert.NotNull(ex);
                Assert.True(ex.Exception.IndexOf(expectedMessage, StringComparison.CurrentCultureIgnoreCase) > -1);
            });
    }

    [Theory]
    [InlineData(true, Operation.ToBeNotified)]
    [InlineData(false, default(Operation))]
    public async Task InsertInExceptionIfErrorException(bool notifyConsumer, Operation expected)
    {
        await TestExecutionExceptionAsync(
            expected,
            ContextWithAS4UserMessage(_expectedId, notifyConsumer),
            sut => sut.HandleErrorExceptionAsync);
    }

    [Theory]
    [InlineData(true, Operation.ToBeNotified)]
    [InlineData(false, default(Operation))]
    public async Task InsertInExceptionIfExecutionException(bool notifyConsumer, Operation expected)
    {
        await TestExecutionExceptionAsync(
            expected,
            ContextWithAS4UserMessage(_expectedId, notifyConsumer),
            sut => sut.HandleExecutionExceptionAsync);
    }

    [Fact]
    public async Task InsertInExceptionWithDeliverMessage()
    {
        var envelope = new DeliverMessageEnvelope(
            new DeliverMessage { MessageInfo = { MessageId = _expectedId } },
            "content-type",
            []);

        await TestExecutionExceptionAsync(
            default,
            new MessagingContext(envelope),
            sut => sut.HandleExecutionExceptionAsync);
    }

    [Fact]
    public async Task InsertInExceptionWithNotifyMessage()
    {
        var envelope = new EmptyNotifyEnvelope(_expectedId);

        await TestExecutionExceptionAsync(
            default,
            new MessagingContext(envelope),
            sut => sut.HandleErrorExceptionAsync);
    }

    private async Task TestExecutionExceptionAsync(
        Operation expected,
        MessagingContext context,
        Func<IAgentExceptionHandler, Func<Exception, MessagingContext, CancellationToken, Task<MessagingContext>>> getExercise)
    {
        // Arrange
        var inMessage = new InMessage(ebmsMessageId: _expectedId);
        inMessage.SetStatus(InStatus.Received);

        GetDataStoreContext.InsertInMessage(inMessage);

        var exercise = getExercise(_sut);

        // Act
        await exercise(new Exception(), context, CancellationToken.None);

        // Assert
        GetDataStoreContext.AssertInMessage(_expectedId, m =>
        {
            Assert.NotNull(m);
            Assert.Equal(InStatus.Exception, m.Status.ToEnum<InStatus>());
        });
        GetDataStoreContext.AssertInException(_expectedId, ex =>
        {
            Assert.NotNull(ex);
            Assert.Equal(expected, ex.Operation);
            Assert.Null(ex.MessageLocation);
        });
    }

    private static MessagingContext ContextWithAS4UserMessage(string id, bool notifyConsumer) => new(
        AS4Message.Create(new FilledUserMessage(id)),
        MessagingContextMode.Receive)
    {
        ReceivingPMode = new ReceivingProcessingMode { ExceptionHandling = { NotifyMessageConsumer = notifyConsumer } }
    };
}
