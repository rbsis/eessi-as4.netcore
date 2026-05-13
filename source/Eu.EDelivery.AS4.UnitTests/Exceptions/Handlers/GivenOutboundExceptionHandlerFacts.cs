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
using Microsoft.Extensions.Logging.Abstractions;
using RetryReliability = Eu.EDelivery.AS4.Model.PMode.RetryReliability;

namespace Eu.EDelivery.AS4.UnitTests.Exceptions.Handlers;

public class GivenOutboundExceptionHandlerFacts : GivenDatastoreFacts
{
    private readonly string _expectedId = Guid.NewGuid().ToString();

    private readonly string _expectedBody = Guid.NewGuid().ToString();

    private readonly Exception _expectedException = new(Guid.NewGuid().ToString());

    private readonly OutboundExceptionHandler _sut;

    public GivenOutboundExceptionHandlerFacts()
    {
        _sut = new(
            NullLogger<OutboundExceptionHandler>.Instance,
            Default.NewExceptionService(this),
            Default.SerializerProvider);
    }

    [Property]
    public void SetRetryInfoWhenSendingPModeIsConfiguredForRetry(
        bool enabled,
        PositiveInt count,
        TimeSpan interval)
    {
        // Arrange
        ClearOutExceptions();

        var pmode = new SendingProcessingMode();

        pmode.ExceptionHandling.Reliability =
            new RetryReliability
            {
                IsEnabled = enabled,
                RetryCount = count.Get,
                RetryInterval = interval.ToString("G")
            };

        var entity = new OutMessage($"entity-{Guid.NewGuid()}");
        GetDataStoreContext.InsertOutMessage(entity);

        // Act
        _sut.HandleExecutionExceptionAsync(
            new Exception(),
            new MessagingContext(
                new ReceivedEntityMessage(entity),
                MessagingContextMode.Notify)
            {
                SendingPMode = pmode
            },
            CancellationToken.None)
           .GetAwaiter()
           .GetResult();

        // Assert
        GetDataStoreContext.AssertOutException(ex =>
        {
            Assert.NotNull(ex);
            Assert.Null(ex.MessageLocation);
            GetDataStoreContext.AssertRetryRelatedOutException(
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

    private void ClearOutExceptions()
    {
        using var ctx = GetDataStoreContext();
        ctx.OutExceptions.RemoveRange(ctx.OutExceptions);
        ctx.SaveChanges();
    }

    [Fact]
    public async Task InsertOutExceptionIfTransformException()
    {
        // Act
        var context = await _sut.ExerciseTransformException(GetDataStoreContext, _expectedBody, _expectedException);

        // Assert
        Assert.Same(_expectedException, context.Exception);
        GetDataStoreContext.AssertOutException(
            ex =>
            {
                Assert.NotNull(ex);
                Assert.True(ex.Exception.IndexOf(_expectedException.Message, StringComparison.CurrentCultureIgnoreCase) > -1, "Not equal message insterted");
            });
    }

    [Theory]
    [InlineData(true, Operation.ToBeNotified)]
    [InlineData(false, default(Operation))]
    public async Task InsertOutExceptionIfStepExecutionException(bool notifyProducer, Operation expected)
    {
        var context = SetupMessagingContextForOutMessage(_expectedId);

        context.ModifyContext(AS4Message.Create(new FilledUserMessage(_expectedId)));
        context.SendingPMode = new SendingProcessingMode { ExceptionHandling = { NotifyMessageProducer = notifyProducer } };

        await TestHandleExecutionException(
            expected,
            context,
            sut => sut.HandleExecutionExceptionAsync);
    }

    [Theory]
    [InlineData(true, Operation.ToBeNotified)]
    [InlineData(false, default(Operation))]
    public async Task InsertOutExceptionIfErrorException(bool notifyProducer, Operation expected)
    {
        var context = SetupMessagingContextForOutMessage(_expectedId);

        context.ModifyContext(AS4Message.Create(new FilledUserMessage(_expectedId)));
        context.SendingPMode = new SendingProcessingMode { ExceptionHandling = { NotifyMessageProducer = notifyProducer } };

        await TestHandleExecutionException(
            expected,
            context,
            sut => sut.HandleErrorExceptionAsync);
    }

    [Fact]
    public async Task InsertOutExceptionIfDeliverMessage()
    {
        var context = SetupMessagingContextForOutMessage(_expectedId);

        var deliverEnvelope =
            new DeliverMessageEnvelope(
                new DeliverMessage { MessageInfo = { MessageId = _expectedId } },
                "content-type",
                []);

        context.ModifyContext(deliverEnvelope);

        await TestHandleExecutionException(
            default,
            context,
            sut => sut.HandleExecutionExceptionAsync);
    }

    [Fact]
    public async Task InsertOutExceptionIfNotifyMessage()
    {
        var context = SetupMessagingContextForOutMessage(_expectedId);

        var notifyEnvelope = new EmptyNotifyEnvelope(_expectedId);

        context.ModifyContext(notifyEnvelope);

        await TestHandleExecutionException(
            default,
            context,
            sut => sut.HandleExecutionExceptionAsync);
    }

    private MessagingContext SetupMessagingContextForOutMessage(string ebmsMessageId)
    {
        // Arrange
        var message = new OutMessage(ebmsMessageId: ebmsMessageId);
        message.SetStatus(OutStatus.Sent);

        GetDataStoreContext.InsertOutMessage(message, withReceptionAwareness: false);

        var receivedMessage = new ReceivedEntityMessage(message, Stream.Null, string.Empty);

        var context = new MessagingContext(receivedMessage, MessagingContextMode.Unknown);

        return context;
    }

    private async Task TestHandleExecutionException(
        Operation expected,
        MessagingContext context,
        Func<IAgentExceptionHandler, Func<Exception, MessagingContext, CancellationToken, Task<MessagingContext>>> getExercise)
    {
        var exercise = getExercise(_sut);

        // Act
        await exercise(_expectedException, context, CancellationToken.None);

        // Assert
        GetDataStoreContext.AssertOutMessage(_expectedId, m =>
        {
            Assert.NotNull(m);
            Assert.Equal(OutStatus.Exception, m.Status.ToEnum<OutStatus>());
        });
        GetDataStoreContext.AssertOutException(_expectedId, exception =>
        {
            Assert.NotNull(exception);
            Assert.True(exception.Exception.IndexOf(_expectedException.Message, StringComparison.CurrentCultureIgnoreCase) > -1, "Message does not contain expected message");
            Assert.True(expected == exception.Operation, "Not equal 'Operation' inserted");
            Assert.True(exception.MessageLocation == null, "Inserted exception body is not empty");
        });
    }

}
