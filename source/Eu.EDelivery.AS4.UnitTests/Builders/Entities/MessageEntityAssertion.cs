using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.Mappings.Core;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Serialization;

namespace Eu.EDelivery.AS4.UnitTests.Builders.Entities;

public static class MessageEntityAssertion
{
    /// <summary>
    /// Asserts the party information.
    /// </summary>
    /// <param name="expected">The expected.</param>
    /// <param name="actual">The actual.</param>
    public static void AssertPartyInfo(AS4Message expected, MessageEntity actual)
    {
        Func<Party?, string?> getPartyId = p => p?.PartyIds.First().Id;
        Assert.Equal(getPartyId(expected.FirstUserMessage?.Sender), actual.FromParty);
        Assert.Equal(getPartyId(expected.FirstUserMessage?.Receiver), actual.ToParty);
    }

    /// <summary>
    /// Asserts the collaboration information.
    /// </summary>
    /// <param name="expected">The expected.</param>
    /// <param name="actual">The actual.</param>
    public static void AssertCollaborationInfo(AS4Message expected, MessageEntity actual)
    {
        var expectedCollaboration = expected.FirstUserMessage?.CollaborationInfo;
        Assert.Equal(expectedCollaboration?.Action, actual.Action);
        Assert.Equal(expectedCollaboration?.ConversationId, actual.ConversationId);
        Assert.Equal(expectedCollaboration?.Service.Value, actual.Service);
    }

    /// <summary>
    /// Asserts the meta information.
    /// </summary>
    /// <param name="expected">The expected.</param>
    /// <param name="actual">The actual.</param>
    public static void AssertUserMessageMetaInfo(AS4Message expected, MessageEntity actual)
    {
        Assert.Equal(expected.FirstUserMessage?.IsTest, actual.IsTest);
        Assert.Equal(expected.FirstUserMessage?.IsDuplicate, actual.IsDuplicate);
    }

    /// <summary>
    /// Asserts the signal message meta information.
    /// </summary>
    /// <param name="expected">The expected.</param>
    /// <param name="actual">The actual.</param>
    public static void AssertSignalMessageMetaInfo(AS4Message expected, MessageEntity actual)
    {
        Assert.Equal(expected.FirstSignalMessage?.IsDuplicate, actual.IsDuplicate);
    }

    /// <summary>
    /// Asserts the SOAP envelope.
    /// </summary>
    /// <param name="expected">The expected.</param>
    /// <param name="actual">The actual.</param>
    public static void AssertSoapEnvelope(UserMessage expected, MessageEntity actual)
    {
        var xmlRepresentation = AS4XmlSerializer.ToString(UserMessageMap.Convert(expected));

        Assert.Equal(xmlRepresentation, actual.SoapEnvelope);
    }
}
