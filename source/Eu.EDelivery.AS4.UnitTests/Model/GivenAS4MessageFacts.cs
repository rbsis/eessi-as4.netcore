using System.Text;
using System.Xml;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Serialization;
using Eu.EDelivery.AS4.UnitTests.Extensions;
using MimeKit;

namespace Eu.EDelivery.AS4.UnitTests.Model;

/// <summary>
/// Testing <seealso cref="AS4Message" />
/// </summary>
public class GivenAS4MessageFacts
{

    public class Empty
    {
        [Fact]
        public void EmptyInstanceIsNotTheSameWithDifferentId()
        {
            // Arrange
            var expected =
                AS4Message.Create(new FilledUserMessage(), new SendingProcessingMode());

            // Act
            var actual = AS4Message.Empty;

            // Assert
            Assert.NotEqual(expected, actual);
        }

        [Fact]
        public void EmptyIsntanceReturnsExpected()
        {
            Assert.Equal(AS4Message.Empty, AS4Message.Empty);
        }
    }

    public class AddAttachments
    {
        [Property]
        public void ThenMessageRemainsSoapAfterAttachmentsAreRemoved(NonEmptyArray<Guid> ids)
        {
            // Arrange
            var sut = AS4Message.Empty;
            var attachments = ids.Get.Distinct().Select(i => new Attachment(i.ToString()));

            // Act / Assert
            Assert.All(attachments, a =>
            {
                sut.AddAttachment(a);
                Assert.NotEqual(Constants.ContentTypes.Soap, sut.ContentType);
            });

            Assert.All(attachments, a => sut.RemoveAttachment(a));
            Assert.Equal(Constants.ContentTypes.Soap, sut.ContentType);
        }
    }

    public class IsPulling
    {
        [Fact]
        public void IsTrueWhenSignalMessageIsPullRequest()
        {
            // Arrange
            var as4Message = AS4Message.Create(new PullRequest($"pr-{Guid.NewGuid()}", null));

            // Act
            var isPulling = as4Message.IsPullRequest;

            // Assert
            Assert.True(isPulling);
        }
    }

    public class AS4MessageDeserializeFacts
    {
        [Fact]
        public async Task ThenMessageUnitsAppearInTheSameOrderAsSerialized()
        {
            var serializer = new SoapEnvelopeSerializer();
            using var str = new MemoryStream(
                Encoding.UTF8.GetBytes(
                    Properties.Resources.as4_soap_user_receipt_message));
            var actual = await serializer
                .DeserializeAsync(str, Constants.ContentTypes.Soap, CancellationToken.None);

            Assert.IsType<Receipt>(actual.MessageUnits.First());
            Assert.IsType<UserMessage>(actual.MessageUnits.ElementAt(1));
            Assert.IsType<Receipt>(actual.MessageUnits.Last());
            Assert.Equal(
                Enumerable.Range(1, 3),
                actual.MessageUnits.Select(m => int.Parse(m.MessageId)));
        }
    }

    public class AS4MessageSerializeFacts : GivenAS4MessageFacts
    {
        [Theory]
        [InlineData("mpc")]
        public void ThenSerializeWithoutAttachmentsReturnsSoapMessage(string mpc)
        {
            // Act
            var userMessage = CreateUserMessage();
            var message = BuildAS4Message(mpc, userMessage);

            using var soapStream = new MemoryStream();
            var document = SerializeSoapMessage(message, soapStream);
            var envelopeElement = document.DocumentElement;

            // Assert
            Assert.NotNull(envelopeElement);
            Assert.Equal(Constants.Namespaces.Soap12, envelopeElement.NamespaceURI);
        }

        [Theory]
        [InlineData("mpc")]
        public void ThenPullRequestCorrectlySerialized(string mpc)
        {
            // Arrange
            var userMessage = CreateUserMessage();

            var message = BuildAS4Message(mpc, userMessage);

            // Act
            using var soapStream = new MemoryStream();
            var document = SerializeSoapMessage(message, soapStream);

            // Assert
            var mpcAttribute = GetMpcAttribute(document);
            Assert.NotNull(mpcAttribute);
            Assert.Equal(mpc, mpcAttribute.Value);
        }

        private static XmlAttribute? GetMpcAttribute(XmlDocument document)
        {
            const string Node = "/s12:Envelope/s12:Header/eb:Messaging/eb:SignalMessage/eb:PullRequest";
            var attributes = document.SelectEbmsNode(Node).Attributes;

            return attributes?.Cast<XmlAttribute>().FirstOrDefault(x => x.Name == "mpc");
        }

        [Theory]
        [InlineData("mpc")]
        public void ThenSerializeWithAttachmentsReturnsMimeMessage(string messageContents)
        {
            // Arrange
            var attachmentStream = new MemoryStream(Encoding.UTF8.GetBytes(messageContents));
            var attachment = new Attachment("attachment-id", attachmentStream, "text/plain");

            var userMessage = CreateUserMessage();

            var message = AS4Message.Create(userMessage);
            message.AddAttachment(attachment);

            // Act
            AssertMimeMessageIsValid(message);
        }

        private static void AssertMimeMessageIsValid(AS4Message message)
        {
            using var mimeStream = new MemoryStream();
            var mimeMessage = SerializeMimeMessage(message, mimeStream);
            var envelopeStream = mimeMessage.BodyParts.OfType<MimePart>().First().Content!.Open();
            var rawXml = new StreamReader(envelopeStream).ReadToEnd();

            // Assert
            Assert.NotNull(rawXml);
            Assert.Contains("Envelope", rawXml);
        }

        [Fact]
        public void ThenSaveToUserMessageCorrectlySerialized()
        {
            // Arrange
            var userMessage = CreateUserMessage();
            var message = AS4Message.Create(userMessage);

            // Act
            using var soapStream = new MemoryStream();
            var document = SerializeSoapMessage(message, soapStream);

            // Assert
            Assert.NotNull(document.DocumentElement);
            Assert.Contains("Envelope", document.DocumentElement.Name);
        }
    }

    protected static UserMessage CreateUserMessage()
    {
        return new UserMessage("message-id");
    }

    protected static XmlDocument SerializeSoapMessage(AS4Message message, MemoryStream soapStream)
    {
        ISerializer serializer = new SoapEnvelopeSerializer();
        serializer.Serialize(message, soapStream);

        soapStream.Position = 0;
        var document = new XmlDocument();
        document.Load(soapStream);

        return document;
    }

    protected static XmlDocument SerializeSoapMessage(AS4Message message)
    {
        using var soapStream = new MemoryStream();
        ISerializer serializer = new SoapEnvelopeSerializer();
        serializer.Serialize(message, soapStream);

        soapStream.Position = 0;
        var document = new XmlDocument();
        document.Load(soapStream);

        return document;
    }

    protected static MimeMessage SerializeMimeMessage(AS4Message message, MemoryStream mimeStream)
    {
        Default.MimeMessageSerializer.Serialize(message, mimeStream);

        mimeStream.Position = 0;

        return MimeMessage.Load(mimeStream);
    }

    protected static AS4Message BuildAS4Message(string mpc, UserMessage userMessage)
    {
        var as4Message = AS4Message.Create(userMessage);
        as4Message.AddMessageUnit(new PullRequest($"pr-{Guid.NewGuid()}", mpc));

        return as4Message;
    }
}
