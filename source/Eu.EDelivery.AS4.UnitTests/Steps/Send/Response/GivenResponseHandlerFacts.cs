using System.Net;
using System.Text;
using Eu.EDelivery.AS4.Http.Response;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Serialization;
using Eu.EDelivery.AS4.Steps;
using Eu.EDelivery.AS4.Steps.Send.Response;
using Eu.EDelivery.AS4.TestUtils.Stubs;
using Eu.EDelivery.AS4.UnitTests.Model;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using static Eu.EDelivery.AS4.UnitTests.Properties.Resources;

namespace Eu.EDelivery.AS4.UnitTests.Steps.Send.Response;

/// <summary>
/// Testing <see cref="TailResponseHandler"/>
/// </summary>
public class GivenResponseHandlerFacts
{
    public class GivenTailResponseHandlerFacts : GivenResponseHandlerFacts
    {
        [Fact]
        public async Task ThenHandlerReturnsFixedValue()
        {
            // Arrange
            var expectedResponse = CreateAnonymousAS4Response();
            var handler = new TailResponseHandler();

            // Act
            var actualResult = await handler.HandleResponseAsync(expectedResponse, CancellationToken.None);

            // Assert
            Assert.Equal(expectedResponse.ReceivedStream, actualResult.MessagingContext.ReceivedMessage);
            AssertNoChangeInPModes(expectedResponse, actualResult);
        }
    }

    public class GivenEmptyResponseHandlerFacts : GivenResponseHandlerFacts
    {
        [Fact]
        public async Task ThenHandlerReturnsSameResultedMessageIfStatusIsAccepted()
        {
            // Arrange
            var as4Response = CreateEmptyAS4ResponseWithStatus(HttpStatusCode.Accepted);
            var handler = new EmptyBodyResponseHandler(NullLogger<EmptyBodyResponseHandler>.Instance, nextHandler: CreateAnonymousNextHandler());

            // Act
            var actualResult = await handler.HandleResponseAsync(as4Response, CancellationToken.None);

            // Assert               
            Assert.False(actualResult.CanProceed);
            AssertNoChangeInPModes(as4Response, actualResult);
        }

        [Fact]
        public async Task ThenCannotProceedIfStatusIsErroneous()
        {
            // Arrange
            var as4Response = CreateEmptyAS4ResponseWithStatus(HttpStatusCode.InternalServerError);
            var handler = new EmptyBodyResponseHandler(NullLogger<EmptyBodyResponseHandler>.Instance, nextHandler: CreateAnonymousNextHandler());

            // Act
            var actualResult = await handler.HandleResponseAsync(as4Response, CancellationToken.None);

            // Assert               
            Assert.False(actualResult.CanProceed);
            AssertNoChangeInPModes(as4Response, actualResult);
        }

        private static IAS4Response CreateEmptyAS4ResponseWithStatus(HttpStatusCode statusCode)
        {
            var stubAS4Response = new Mock<IAS4Response>();
            var context = new MessagingContext(
                AS4Message.Create(new UserMessage($"user-{Guid.NewGuid()}")),
                MessagingContextMode.Send);

            stubAS4Response.Setup(r => r.OriginalRequest).Returns(context);
            stubAS4Response.Setup(r => r.StatusCode).Returns(statusCode);
            stubAS4Response.Setup(r => r.ReceivedAS4Message).Returns(AS4Message.Empty);
            stubAS4Response.SetupGet(r => r.ReceivedStream).Returns(new ReceivedMessage(Stream.Null));

            return stubAS4Response.Object;
        }

        [Fact]
        public async Task ThenNextHandlerGetsTheResponseIfAS4MessageIsReceived()
        {
            // Arrange
            var as4Message = AS4Message.Create(new Error($"error-{Guid.NewGuid()}", $"user-{Guid.NewGuid()}"));
            var as4Response = CreateAS4ResponseWithResultedMessage(as4Message);

            var spyHandler = new SpyAS4ResponseHandler();
            var handler = new EmptyBodyResponseHandler(NullLogger<EmptyBodyResponseHandler>.Instance, nextHandler: spyHandler);

            // Act
            await handler.HandleResponseAsync(as4Response, CancellationToken.None);

            // Assert
            Assert.True(spyHandler.IsCalled);
        }

        private static IAS4Response CreateAS4ResponseWithResultedMessage(AS4Message resultedMessage)
        {
            var stubAS4Response = new Mock<IAS4Response>();
            var context = new MessagingContext(
                AS4Message.Create(new UserMessage($"user-{Guid.NewGuid()}")),
                MessagingContextMode.Send);

            stubAS4Response.Setup(r => r.OriginalRequest).Returns(context);
            stubAS4Response.Setup(r => r.ReceivedAS4Message).Returns(resultedMessage);
            stubAS4Response.SetupGet(r => r.ReceivedStream).Returns(new ReceivedMessage(Stream.Null));

            return stubAS4Response.Object;
        }
    }

    public class GivenPullRequestResponseHandlerFacts : GivenResponseHandlerFacts
    {
        [Fact]
        public async Task ThenNextHandlerGetsResponseIfNotOriginatedFromPullRequest()
        {
            // Arrange
            var as4Response = CreateAnonymousAS4Response();

            var spyHandler = new SpyAS4ResponseHandler();
            var handler = new PullRequestResponseHandler(spyHandler, StubPiggyBackingService.Instance);

            // Act
            await handler.HandleResponseAsync(as4Response, CancellationToken.None);

            // Assert
            Assert.True(spyHandler.IsCalled);
        }

        [Fact]
        public async Task HandlerStopsExecutionIfResponseIsWarning()
        {
            // Arrange
            var stubAS4Response = await CreatePullRequestWarning();
            var sut = new PullRequestResponseHandler(CreateAnonymousNextHandler(), StubPiggyBackingService.Instance);

            // Act
            var actualResult = await sut.HandleResponseAsync(stubAS4Response, CancellationToken.None);

            // Assert
            Assert.False(actualResult.CanProceed);
            AssertNoChangeInPModes(stubAS4Response, actualResult);
        }

        private static async Task<IAS4Response> CreatePullRequestWarning()
        {
            var stubAS4Response = new Mock<IAS4Response>();
            var pullRequest = new MessagingContext(
                AS4Message.Create(new PullRequest($"pr-{Guid.NewGuid()}", "some-mpc")),
                MessagingContextMode.Send);

            stubAS4Response.Setup(r => r.OriginalRequest).Returns(pullRequest);
            stubAS4Response.Setup(r => r.ReceivedAS4Message).Returns(await PullResponseWarning());

            return stubAS4Response.Object;
        }

        private static async Task<AS4Message> PullResponseWarning()
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(as4_pullrequest_warning));
            var serializer = new SoapEnvelopeSerializer();
            return await serializer.DeserializeAsync(stream, Constants.ContentTypes.Soap, CancellationToken.None);
        }

        [Fact]
        public async Task ThenHandlerReturnsStoppedExecutionStepResult()
        {
            // Arrange
            var stubAS4Response = CreateResponseWith(
                request: new PullRequest($"pr-{Guid.NewGuid()}", "some-mpc"),
                response: Error.CreatePullRequestWarning($"error-{Guid.NewGuid()}"));

            var handler = new PullRequestResponseHandler(CreateAnonymousNextHandler(), StubPiggyBackingService.Instance);

            // Act
            var actualResult = await handler.HandleResponseAsync(stubAS4Response, CancellationToken.None);

            // Assert
            Assert.False(actualResult.CanProceed);
            AssertNoChangeInPModes(stubAS4Response, actualResult);
        }

        private static IAS4Response CreateResponseWith(SignalMessage request, SignalMessage response)
        {
            var stubAS4Response = new Mock<IAS4Response>();

            var context = new MessagingContext(
                AS4Message.Create(request),
                MessagingContextMode.Send)
            {
                SendingPMode = new SendingProcessingMode(),
                ReceivingPMode = new ReceivingProcessingMode()
            };

            stubAS4Response.Setup(r => r.OriginalRequest).Returns(context);
            stubAS4Response.Setup(r => r.ReceivedAS4Message).Returns(AS4Message.Create(response));

            return stubAS4Response.Object;
        }
    }

    private static IAS4ResponseHandler CreateAnonymousNextHandler()
    {
        return new TailResponseHandler();
    }

    private static IAS4Response CreateAnonymousAS4Response()
    {
        var stubResponse = new Mock<HttpWebResponse>();
        stubResponse.Setup(r => r.ContentType)
                    .Returns(Constants.ContentTypes.Soap);

        return Default.AS4ResponseFactory.Create(
            requestMessage: new EmptyMessagingContext
            {
                SendingPMode = new SendingProcessingMode(),
                ReceivingPMode = new ReceivingProcessingMode()
            },
            webResponse: stubResponse.Object,
            cancellation: CancellationToken.None).Result;
    }

    private static void AssertNoChangeInPModes(IAS4Response expected, StepResult actual)
    {
        Assert.Same(expected.OriginalRequest.SendingPMode, actual.MessagingContext.SendingPMode);
        Assert.Same(expected.OriginalRequest.ReceivingPMode, actual.MessagingContext.ReceivingPMode);
    }
}

