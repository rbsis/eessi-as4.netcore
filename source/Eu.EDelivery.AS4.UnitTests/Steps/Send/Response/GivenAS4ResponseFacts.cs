using System.Net;
using Eu.EDelivery.AS4.Http.Response;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.UnitTests.Model;
using Moq;

namespace Eu.EDelivery.AS4.UnitTests.Steps.Send.Response;

/// <summary>
/// Testing <see cref="AS4Response"/>
/// </summary>
public class GivenAS4ResponseFacts
{
    [Fact]
    public void GetsEmptyAS4MessageForEmptyHttpContentType()
    {
        // Arrange
        var response = CreateWebResponseWithContentType(string.Empty);

        // Act
        var result = CreateAS4ResponseWith(response).ReceivedAS4Message;

        // Assert
        Assert.True(result.IsEmpty);
    }

    [Fact]
    public void TestEmptyResponse()
    {
        // Arrange
        var expectedContentType = string.Empty;

        // Act
        var actualResponse = CreateWebResponseWithContentType(expectedContentType);

        // Assert
        Assert.Equal(expectedContentType, actualResponse.ContentType);
    }

    private static HttpWebResponse CreateWebResponseWithContentType(string contentType)
    {
        var stubResponse = new Mock<HttpWebResponse>();
        stubResponse.Setup(r => r.ContentType).Returns(contentType);

        return stubResponse.Object;
    }

    private static IAS4Response CreateAS4ResponseWith(HttpWebResponse webResponse)
    {
        return Default.AS4ResponseFactory.Create(
            requestMessage: new EmptyMessagingContext
            {
                SendingPMode = new SendingProcessingMode(),
                ReceivingPMode = new ReceivingProcessingMode()
            },
            webResponse: webResponse,
            cancellation: CancellationToken.None).Result;
    }
}
