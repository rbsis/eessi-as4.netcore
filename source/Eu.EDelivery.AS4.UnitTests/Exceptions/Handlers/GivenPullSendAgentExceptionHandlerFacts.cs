using System.Text;
using Eu.EDelivery.AS4.Exceptions;
using Eu.EDelivery.AS4.Exceptions.Handlers;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.Submit;
using Eu.EDelivery.AS4.UnitTests.Common;
using Eu.EDelivery.AS4.UnitTests.Repositories;

namespace Eu.EDelivery.AS4.UnitTests.Exceptions.Handlers;

public class GivenPullSendAgentExceptionHandlerFacts : GivenDatastoreFacts
{
    private readonly string _expectedMessage = Guid.NewGuid().ToString();

    private readonly PullSendAgentExceptionHandler _sut;

    public GivenPullSendAgentExceptionHandlerFacts()
    {
        _sut = new(Default.NewOutboundExceptionHandler(this));
    }

    [Fact]
    public async Task InsertInExceptionIfHandlingTransformException()
    {
        // Arrange
        var expectedBody = Encoding.UTF8.GetBytes("serialize me!");

        using (var stream = new MemoryStream(expectedBody))
        {
            // Act
            await _sut.HandleTransformationExceptionAsync(
                new Exception(_expectedMessage),
                new ReceivedMessage(stream),
                CancellationToken.None);
        }
        // Assert
        GetDataStoreContext.AssertOutException(
            ex =>
            {
                Assert.NotNull(ex);
                Assert.Equal(_expectedMessage, ex.Exception);
            });
    }

    [Fact]
    public async Task InsertOutExceptionIfHandlingExecutionExceptionDuringReceive()
    {
        await TestExecutionException(
            async sut =>
                await sut.HandleExecutionExceptionAsync(
                    new Exception(_expectedMessage),
                    new MessagingContext(AS4Message.Empty, MessagingContextMode.Receive),
                    CancellationToken.None),
            assertLocation: Assert.Null);
    }

    [Fact]
    public async Task InsertOutExcceptionIfHandlingExcutionExceptionDuringSubmit()
    {
        await TestExecutionException(
            async sut =>
                await sut.HandleExecutionExceptionAsync(
                    new Exception(_expectedMessage),
                    new MessagingContext(new SubmitMessage()),
                    CancellationToken.None),
            assertLocation: Assert.NotNull);
    }

    [Fact]
    public async Task InsertOutExceptionIfHandlingErrorExceptionDuringReceive()
    {
        await TestExecutionException(
            async sut =>
                await sut.HandleErrorExceptionAsync(
                    new Exception(_expectedMessage),
                    new MessagingContext(AS4Message.Empty, MessagingContextMode.Receive),
                    CancellationToken.None),
            assertLocation: Assert.Null);
    }

    [Fact]
    public async Task InsertOutExceptionIfHandlingErrorExceptionDuringSubmit()
    {
        await TestExecutionException(
            async sut =>
                await sut.HandleErrorExceptionAsync(
                    new Exception(_expectedMessage),
                    new MessagingContext(new SubmitMessage()),
                    CancellationToken.None),
            assertLocation: Assert.NotNull);
    }

    private async Task TestExecutionException(
        Func<IAgentExceptionHandler, Task<MessagingContext>> act,
        Action<string?> assertLocation)
    {
        // Act
        await act(_sut);

        // Assert            
        GetDataStoreContext.AssertOutException(ex =>
        {
            Assert.NotNull(ex);
            Assert.True(ex.Exception.IndexOf(_expectedMessage, StringComparison.CurrentCultureIgnoreCase) > -1);
            assertLocation(ex.MessageLocation);
        });
    }
}
