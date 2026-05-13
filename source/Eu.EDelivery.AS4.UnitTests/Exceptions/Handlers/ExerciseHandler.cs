using System.Text;
using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Exceptions;
using Eu.EDelivery.AS4.Model.Internal;

namespace Eu.EDelivery.AS4.UnitTests.Exceptions.Handlers;

public static class ExerciseHandler
{
    /// <summary>
    /// Exercises the transform exception.
    /// </summary>
    /// <param name="handler">The handler.</param>
    /// <param name="createContext">The create context.</param>
    /// <param name="contents">The contents.</param>
    /// <param name="exception">The exception.</param>
    /// <returns></returns>
    internal static async Task<MessagingContext> ExerciseTransformException(
        this IAgentExceptionHandler handler,
        Func<DatastoreContext> createContext,
        string contents,
        Exception exception)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(contents));
        stream.Position = 0;
        var receivedMessage = new ReceivedMessage(stream);

        return await handler.HandleTransformationExceptionAsync(exception, receivedMessage, CancellationToken.None);
    }
}
