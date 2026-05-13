using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Model.Deliver;
using Eu.EDelivery.AS4.Model.Notify;
using Eu.EDelivery.AS4.Strategies.Sender;
using Eu.EDelivery.AS4.UnitTests.Strategies.Method;
using Microsoft.Extensions.Logging.Abstractions;
using MessageInfo = Eu.EDelivery.AS4.Model.Common.MessageInfo;

namespace Eu.EDelivery.AS4.UnitTests.Strategies.Sender;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S3881:\"IDisposable\" should be implemented correctly", Justification = "<Pending>")]
public class GivenFileSenderFacts : IDisposable
{
    private static readonly string _expectedDirectoryPath = Directory.GetCurrentDirectory();
    private static readonly string _expectedFileName = Path.Combine(_expectedDirectoryPath, AnonymousDeliverMessage().Message.MessageInfo.MessageId + ".xml");

    [Fact]
    public async Task StoresFileOnFileSystemIfDeliverMessage()
    {
        // Arrange
        var sut = new FileSender(NullLogger<FileSender>.Instance);
        sut.Configure(new LocationMethod(_expectedDirectoryPath));

        // Act
        var r = await sut.SendAsync(AnonymousDeliverMessage(), CancellationToken.None);

        // Assert
        Assert.Equal(SendResult.Success, r);
        Assert.True(File.Exists(_expectedFileName));
    }

    [Fact]
    public async Task DeliverReturnsRetryableIfFileIsInUse()
    {
        // Arrange
        var sut = new FileSender(NullLogger<FileSender>.Instance);
        sut.Configure(new LocationMethod(_expectedDirectoryPath));

        using (new FileStream(_expectedFileName, FileMode.Create))
        {
            // Act
            var r = await sut.SendAsync(AnonymousDeliverMessage(), CancellationToken.None);

            // Assert
            Assert.Equal(SendResult.RetryableFail, r);
        }
    }

    private static DeliverMessageEnvelope AnonymousDeliverMessage()
    {
        return new DeliverMessageEnvelope(
            messageInfo: new MessageInfo("message-id", "mpc"),
            deliverMessage: [],
            contentType: "text/plain");
    }

    [Fact]
    public async Task StoresFileOnFileSystemIfNotifyMessage()
    {
        // Arrange
        var sut = new FileSender(NullLogger<FileSender>.Instance);
        sut.Configure(new LocationMethod(_expectedDirectoryPath));

        // Act
        var r = await sut.SendAsync(AnonymousNotifyMessage(), CancellationToken.None);

        // Assert
        Assert.Equal(SendResult.Success, r);
        Assert.True(File.Exists(_expectedFileName));
    }

    [Fact]
    public async Task NotifyReturnsRetryableIfFileIsInUse()
    {
        // Arrange
        var sut = new FileSender(NullLogger<FileSender>.Instance);
        sut.Configure(new LocationMethod(_expectedDirectoryPath));

        using (new FileStream(_expectedFileName, FileMode.Create))
        {
            // Act
            var r = await sut.SendAsync(AnonymousNotifyMessage(), CancellationToken.None);

            // Assert
            Assert.Equal(SendResult.RetryableFail, r);
        }
    }

    private static NotifyMessageEnvelope AnonymousNotifyMessage() => new(
        messageInfo: new AS4.Model.Notify.MessageInfo { MessageId = "message-id" },
        statusCode: default,
        notifyMessage: [],
        contentType: "text/plain",
        entityType: typeof(InMessage));

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        File.Delete(_expectedFileName);
    }
}
