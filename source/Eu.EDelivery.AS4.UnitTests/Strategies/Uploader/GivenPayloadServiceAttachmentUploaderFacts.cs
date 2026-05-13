using Eu.EDelivery.AS4.Model.Common;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Strategies.Uploader;
using Eu.EDelivery.AS4.UnitTests.Strategies.Method;

namespace Eu.EDelivery.AS4.UnitTests.Strategies.Uploader;

public class GivenPayloadServiceAttachmentUploaderFacts
{
    [Fact]
    public async Task ThenUploadAttachmentSucceeds()
    {
        // Arrange
        var expectedResult = CreateAnonymousUploadResult();
        var uploader = new PayloadServiceAttachmentUploader(new StubUploaderHttpClient(expectedResult));
        uploader.Configure(new LocationMethod("not-empty"));

        // Act
        var actualResult = await uploader.UploadAsync(CreateAnonymousAttachment(), new MessageInfo(), CancellationToken.None);

        // Assert
        Assert.Equal(expectedResult, actualResult);
    }

    [Fact]
    public async Task ThenUploadAttachmentFailsIfPayloadServiceIsNotRunning()
    {
        var uploader = new PayloadServiceAttachmentUploader(new SaboteurUploaderHttpClient());

        await Assert.ThrowsAnyAsync<Exception>(() => uploader.UploadAsync(CreateAnonymousAttachment(), new MessageInfo(), CancellationToken.None));
    }

    private static UploadResult CreateAnonymousUploadResult()
    {
        return UploadResult.SuccessWithIdAndUrl(payloadId: "ignored payload id", downloadUrl: "ignored download url");
    }

    private static Attachment CreateAnonymousAttachment()
    {
        return new Attachment(Stream.Null, "text/plain");
    }

    private class StubUploaderHttpClient : IUploaderHttpClient
    {
        private readonly UploadResult _expectedResult;

        public StubUploaderHttpClient(UploadResult expectedResult)
        {
            _expectedResult = expectedResult;
        }

        public Task<UploadResult?> PostAttachmentAsync(string url, Attachment attachment, CancellationToken cancellation)
        {
            return Task.FromResult<UploadResult?>(_expectedResult);
        }
    }

    private class SaboteurUploaderHttpClient : IUploaderHttpClient
    {
        public Task<UploadResult?> PostAttachmentAsync(string url, Attachment attachment, CancellationToken cancellation)
        {
            throw new NotImplementedException();
        }
    }
}
