using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Extensions;
using Eu.EDelivery.AS4.Model.Common;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Steps.Deliver;
using Eu.EDelivery.AS4.Strategies.Sender;
using Eu.EDelivery.AS4.Strategies.Uploader;
using Eu.EDelivery.AS4.UnitTests.Common;
using Eu.EDelivery.AS4.UnitTests.Extensions;
using Eu.EDelivery.AS4.UnitTests.Model;
using Eu.EDelivery.AS4.UnitTests.Repositories;
using Eu.EDelivery.AS4.UnitTests.Strategies.Uploader;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RetryReliability = Eu.EDelivery.AS4.Entities.RetryReliability;

namespace Eu.EDelivery.AS4.UnitTests.Steps.Deliver;

public class GivenUploadAttachmentsStepFacts : GivenDatastoreFacts
{
    [Fact]
    public async Task ThrowsWhenUploadingAttachmentsFailed()
    {
        // Arrange
        var saboteurProvider = new Mock<IAttachmentUploaderProvider>();
        saboteurProvider
            .Setup(p => p.Get(It.IsAny<string>()))
            .Throws(new Exception("Failed to get Uploader"));

        var sut = new UploadAttachmentsStep(
            NullLogger<UploadAttachmentsStep>.Instance,
            saboteurProvider.Object,
            Default.NewMarkForRetryService(this));

        // Act / Assert
        await Assert.ThrowsAnyAsync<Exception>(
            async () => await sut.ExecuteAsync(await CreateAS4MessageWithAttachmentAsync(), CancellationToken.None));
    }

    [Theory]
    [ClassData(typeof(UploadRetryData))]
    public async Task RetriesUploadingWhenUploaderReturnsRetryableFailResult(UploadRetry input)
    {
        // Arrange
        var id = "deliver-" + Guid.NewGuid();
        var im = InsertInMessage(id);

        var r = RetryReliability.CreateForInMessage(
            refToInMessageId: im.Id,
            maxRetryCount: input.MaxRetryCount,
            retryInterval: default,
            type: RetryType.Delivery);
        r.CurrentRetryCount = input.CurrentRetryCount;
        GetDataStoreContext.InsertRetryReliability(r);

        var a = new FilledAttachment();
        var userMessage = new FilledUserMessage(id, a.Id);
        var as4Msg = AS4Message.Create(userMessage);
        as4Msg.AddAttachment(a);

        var fixture = await PrepareAS4MessageForDeliveryAsync(as4Msg, CreateReceivingPModeWithPayloadMethod());

        var stub = CreateStubAttachmentUploader(fixture.DeliverMessage!.Message.MessageInfo, input.UploadResult);

        // Act
        await CreateUploadStep(stub).ExecuteAsync(fixture, CancellationToken.None);

        // Assert
        GetDataStoreContext.AssertInMessage(id, actual =>
        {
            Assert.NotNull(actual);
            Assert.Equal(input.ExpectedStatus, actual.Status.ToEnum<InStatus>());
            Assert.Equal(input.ExpectedOperation, actual.Operation);
        });
    }

    private static async Task<MessagingContext> PrepareAS4MessageForDeliveryAsync(AS4Message msg, ReceivingProcessingMode pmode)
    {
        var entity = new InMessage(msg.GetPrimaryMessageId() ?? throw new InvalidOperationException());
        entity.SetPModeInformation(pmode);

        return await Default.DeliverMessageTransformer
            .TransformAsync(new ReceivedEntityMessage(entity, msg.ToStream(), msg.ContentType), CancellationToken.None);
    }

    [Theory]
    [ClassData(typeof(UploadRetryData))]
    public async Task AllAttachmentsShouldSucceedOrFail(UploadRetry input)
    {
        // Arrange
        var id = "deliver-" + Guid.NewGuid();
        var im = InsertInMessage(id);

        var r = RetryReliability.CreateForInMessage(
            refToInMessageId: im.Id,
            maxRetryCount: input.MaxRetryCount,
            retryInterval: default,
            type: RetryType.Delivery);
        r.CurrentRetryCount = input.CurrentRetryCount;
        GetDataStoreContext.InsertRetryReliability(r);


        var a1 = new FilledAttachment("attachment-1");
        var a2 = new FilledAttachment("attachment-2");
        var userMessage = new FilledUserMessage(id, a1.Id, a2.Id);

        var as4Msg = AS4Message.Create(userMessage);
        as4Msg.AddAttachment(a1);
        as4Msg.AddAttachment(a2);

        var fixture = await PrepareAS4MessageForDeliveryAsync(as4Msg, CreateReceivingPModeWithPayloadMethod());

        var stub = new Mock<IAttachmentUploader>();
        stub.Setup(s => s.UploadAsync(a1, fixture.DeliverMessage!.Message.MessageInfo, CancellationToken.None))
            .ReturnsAsync(input.UploadResult);
        stub.Setup(s => s.UploadAsync(a2, fixture.DeliverMessage!.Message.MessageInfo, CancellationToken.None))
            .ReturnsAsync(
                input.UploadResult.Status == SendResult.Success
                    ? UploadResult.FatalFail
                    : UploadResult.RetryableFail);

        // Act
        await CreateUploadStep(stub.Object).ExecuteAsync(fixture, CancellationToken.None);

        // Assert
        GetDataStoreContext.AssertInMessage(id, actual =>
        {
            Assert.NotNull(actual);
            var op = actual.Operation;
            Assert.NotEqual(Operation.Delivered, op);
            var st = actual.Status.ToEnum<InStatus>();
            Assert.NotEqual(InStatus.Delivered, st);

            var operationToBeRetried = Operation.ToBeRetried == op;
            var uploadResultCanBeRetried =
                input.UploadResult.Status == SendResult.RetryableFail
                && input.CurrentRetryCount < input.MaxRetryCount;

            Assert.True(
                operationToBeRetried == uploadResultCanBeRetried,
                "InMessage should update Operation=ToBeDelivered");

            var messageSetToException = Operation.DeadLettered == op && InStatus.Exception == st;
            var exhaustRetries =
                input.CurrentRetryCount == input.MaxRetryCount
                || input.UploadResult.Status != SendResult.RetryableFail;

            Assert.True(
                messageSetToException == exhaustRetries,
                $"{messageSetToException} != {exhaustRetries} InMessage should update Operation=DeadLettered, Status=Exception");
        });
    }

    private InMessage InsertInMessage(string id)
    {
        var inMsg = new InMessage(id);
        inMsg.SetStatus(InStatus.Received);
        inMsg.Operation = Operation.Delivering;

        return GetDataStoreContext.InsertInMessage(inMsg);
    }

    private static IAttachmentUploader CreateStubAttachmentUploader(MessageInfo m, UploadResult r)
    {
        var stub = new Mock<IAttachmentUploader>();
        stub.Setup(s => s.UploadAsync(It.IsAny<Attachment>(), m, CancellationToken.None))
            .ReturnsAsync(r);

        return stub.Object;
    }

    [Fact]
    public async Task UpdateWithAttachmentLocationWhenUploadingAttachmentsSucceeds()
    {
        // Arrange
        const string ExpectedLocation = "http://path/to/download/attachment";
        var stubUploader = new StubAttachmentUploader(ExpectedLocation);

        // Act
        var result = await CreateUploadStep(stubUploader)
            .ExecuteAsync(await CreateAS4MessageWithAttachmentAsync(), CancellationToken.None);

        // Assert
        Assert.NotNull(result.MessagingContext.DeliverMessage);
        Assert.Collection(
            result.MessagingContext.DeliverMessage.Message.Payloads,
            p => Assert.Equal(ExpectedLocation, p.Location));
    }

    private static async Task<MessagingContext> CreateAS4MessageWithAttachmentAsync()
    {
        const string AttachmentId = "attachment-id";

        var userMessage = new UserMessage(Guid.NewGuid().ToString(), new PartInfo("cid:" + AttachmentId));
        var as4Message = AS4Message.Create(userMessage);
        as4Message.AddAttachment(new Attachment(AttachmentId, Stream.Null, "text/plain"));
        var pMode = CreateReceivingPModeWithPayloadMethod();

        return await PrepareAS4MessageForDeliveryAsync(as4Message, pMode);
    }

    private static ReceivingProcessingMode CreateReceivingPModeWithPayloadMethod() => new()
    {
        MessageHandling =
        {
            Item = new AS4.Model.PMode.Deliver
            {
                PayloadReferenceMethod = { Type = "FILE" }
            }
        }
    };

    /// <summary>
    /// Creates the upload step.
    /// </summary>
    /// <param name="uploader">The uploader.</param>
    /// <returns></returns>
    private UploadAttachmentsStep CreateUploadStep(IAttachmentUploader uploader) => new(
        NullLogger<UploadAttachmentsStep>.Instance,
        new StubAttachmentUploaderProvider(uploader),
        Default.NewMarkForRetryService(this));
}
