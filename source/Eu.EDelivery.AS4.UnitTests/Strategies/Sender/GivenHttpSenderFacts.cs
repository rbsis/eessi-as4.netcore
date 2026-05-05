using System.Net;
using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Model.Deliver;
using Eu.EDelivery.AS4.Model.Notify;
using Eu.EDelivery.AS4.Strategies.Sender;
using Eu.EDelivery.AS4.TestUtils.Stubs;
using Eu.EDelivery.AS4.UnitTests.Strategies.Method;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging.Abstractions;
using MessageInfo = Eu.EDelivery.AS4.Model.Common.MessageInfo;

namespace Eu.EDelivery.AS4.UnitTests.Strategies.Sender;

/// <summary>
/// Testing <see cref="HttpSender"/>
/// </summary>
public class GivenHttpSenderFacts
{
    [Property]
    public Property DeliverReturnsExpectedAccordingToStatusCode(HttpStatusCode st)
    {
        return TestReturnsExpectedWithHttpStatusCode(
            st, sut => sut.SendAsync(CreateAnonymousDeliverEnvelope(), CancellationToken.None).GetAwaiter().GetResult());

    }

    [Property]
    public Property NotifyReturnsReturnsExpectedAccordingToStatusCode(HttpStatusCode st)
    {
        return TestReturnsExpectedWithHttpStatusCode(
            st, sut => sut.SendAsync(CreateAnonymousNotifyEnvelope(), CancellationToken.None).GetAwaiter().GetResult());
    }

    private static Property TestReturnsExpectedWithHttpStatusCode(HttpStatusCode st, Func<HttpSender, SendResult> act)
    {
        // Arrange
        var client = StubHttpClient.ThatReturns(st);
        var sut = new HttpSender(NullLogger<HttpSender>.Instance, client);
        sut.Configure(new LocationMethod("ignored location"));

        // Act
        var r = act(sut);

        // Assert
        var code = (int)st;
        var isFatal = r == SendResult.FatalFail;
        var isRetryable = r == SendResult.RetryableFail;
        var isSuccess = r == SendResult.Success;

        Assert.True(client.IsCalled, "Stub HTTP client isn't called");
        return isRetryable
            .Equals(code >= 500 || code == 408 || code == 429)
            .Or(isSuccess.Equals(code >= 200 && code <= 206))
            .Or(isFatal.Equals(code >= 400 && code < 500))
            .Classify(isSuccess, "Success with code: " + code)
            .Classify(isRetryable, "Retryable with code: " + code)
            .Classify(isFatal, "Fatal with code: " + code);
    }

    private static DeliverMessageEnvelope CreateAnonymousDeliverEnvelope() => new(new MessageInfo(), [], "text/plain");

    private static NotifyMessageEnvelope CreateAnonymousNotifyEnvelope() => new(new AS4.Model.Notify.MessageInfo(), default, [], "text/plain", typeof(InMessage));
}
