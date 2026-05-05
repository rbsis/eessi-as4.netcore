using Eu.EDelivery.AS4.Strategies.Uploader;
using Eu.EDelivery.AS4.UnitTests.Common;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Eu.EDelivery.AS4.UnitTests.Strategies.Uploader;

/// <summary>
/// Testing <see cref="GivenAttachmentUploaderProviderFacts" />
/// </summary>
public class GivenAttachmentUploaderProviderFacts
{
    private readonly AttachmentUploaderProvider _sut;

    private readonly FileAttachmentUploader _fileAttachmentUploader;
    private readonly EmailAttachmentUploader _emailAttachmentUploader;
    private readonly PayloadServiceAttachmentUploader _payloadServiceAttachmentUploader;

    public GivenAttachmentUploaderProviderFacts()
    {
        _fileAttachmentUploader = new(
            NullLogger<FileAttachmentUploader>.Instance);
        _emailAttachmentUploader = new(
            NullLogger<EmailAttachmentUploader>.Instance,
            StubConfig.Default);
        _payloadServiceAttachmentUploader = new(
            Substitute.For<IUploaderHttpClient>());

        _sut = new(
            _fileAttachmentUploader,
            _emailAttachmentUploader,
            _payloadServiceAttachmentUploader);
    }

    [Theory]
    [InlineData(FileAttachmentUploader.Key, typeof(FileAttachmentUploader))]
    [InlineData(EmailAttachmentUploader.Key, typeof(EmailAttachmentUploader))]
    [InlineData(PayloadServiceAttachmentUploader.Key, typeof(PayloadServiceAttachmentUploader))]
    public void AttachmentProviderGetsUploader(string expectedKey, Type expectedType)
    {
        // Act
        var actualUploader = _sut.Get(expectedKey);

        // Assert
        Assert.IsType(expectedType, actualUploader);
    }

    [Fact]
    public void FailsToGetUploaderIfNotUploaderIsRegisteredForType()
    {
        // Act / Assert
        Assert.ThrowsAny<Exception>(() => _sut.Get("not exsising key"));
    }

}
