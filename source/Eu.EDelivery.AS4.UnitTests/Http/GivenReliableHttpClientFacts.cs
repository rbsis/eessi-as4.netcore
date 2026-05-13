using Eu.EDelivery.AS4.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Eu.EDelivery.AS4.UnitTests.Http;

public class GivenReliableHttpClientFacts
{

    public class Request
    {
        [Fact]
        public void CreatePostRequest()
        {
            // Arrange
            var sut = new ReliableHttpClient(
               NullLogger<ReliableHttpClient>.Instance,
               Default.SerializerProvider,
               Default.AS4ResponseFactory);

            const string ExpectedUrl = "http://valid/url";
            const string ExpectedType = "application/json";

            // Act
            var request = sut.CreateRequest(ExpectedUrl, ExpectedType) as AS4HttpRequest;

            // Assert
            Assert.NotNull(request);
            Assert.Equal(ExpectedUrl, request.Url);
            Assert.Equal(ExpectedType, request.ContentType);
        }
    }
}
