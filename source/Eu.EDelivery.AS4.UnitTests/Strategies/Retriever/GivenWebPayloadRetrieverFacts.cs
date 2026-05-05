using System.Net;
using Eu.EDelivery.AS4.Strategies.Retriever;

namespace Eu.EDelivery.AS4.UnitTests.Strategies.Retriever;

public class GivenWebPayloadRetrieverFacts
{
    const string Location = "http://ignored/path";

    [Fact]
    public async Task ThenDownloadPayloadSucceeds()
    {
        // Arrange
        const string ExpectedPayload = "message data!";
        var retriever = new HttpPayloadRetriever(new StubRetrieverHttpClient(ExpectedPayload));

        // Act
        using var streamReader = new StreamReader(await retriever.RetrievePayloadAsync(Location, CancellationToken.None));
        // Assert
        var actualPayload = await streamReader.ReadToEndAsync();
        Assert.Equal(ExpectedPayload, actualPayload);
    }

    [Fact]
    public async Task ThenDownloadFailedIfReturnCodeIsntSuccessful()
    {
        // Arrange
        var retriever = new HttpPayloadRetriever(new SaboteurRetrieverHttpClient(HttpStatusCode.BadGateway));

        // Act
        var actualPayload = await retriever.RetrievePayloadAsync(Location, CancellationToken.None);

        // Assert
        Assert.Equal(Stream.Null, actualPayload);
    }

    private class StubRetrieverHttpClient : IRetrieverHttpClient
    {
        private readonly string _expectedPayload;

        public StubRetrieverHttpClient(string expectedPayload)
        {
            _expectedPayload = expectedPayload;
        }

        public Task<HttpResponseMessage> GetPayloadAsync(string url, CancellationToken cancellation)
        {
            var response = new HttpResponseMessage { Content = new StringContent(_expectedPayload) };
            return Task.FromResult(response);
        }
    }

    private class SaboteurRetrieverHttpClient : IRetrieverHttpClient
    {
        private readonly HttpStatusCode _statusCode;

        public SaboteurRetrieverHttpClient(HttpStatusCode statusCode)
        {
            _statusCode = statusCode;
        }

        public Task<HttpResponseMessage> GetPayloadAsync(string url, CancellationToken cancellation)
        {
            var response = new HttpResponseMessage(_statusCode);
            return Task.FromResult(response);
        }
    }
}
