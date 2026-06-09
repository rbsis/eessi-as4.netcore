using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using Eu.EDelivery.AS4.Exceptions;
using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Model.Internal;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Resources;
using Eu.EDelivery.AS4.Security.Encryption;
using Eu.EDelivery.AS4.Serialization;
using Eu.EDelivery.AS4.UnitTests.Extensions;
using Eu.EDelivery.AS4.UnitTests.Model;
using Eu.EDelivery.AS4.UnitTests.Resources;
using Eu.EDelivery.AS4.Xml;
using Microsoft.Extensions.Logging.Abstractions;
using static Eu.EDelivery.AS4.UnitTests.Properties.Resources;
using AgreementReference = Eu.EDelivery.AS4.Model.Core.AgreementReference;
using Error = Eu.EDelivery.AS4.Model.Core.Error;
using NonRepudiationInformation = Eu.EDelivery.AS4.Model.Core.NonRepudiationInformation;
using Party = Eu.EDelivery.AS4.Model.Core.Party;
using PartyId = Eu.EDelivery.AS4.Model.Core.PartyId;
using Property = FsCheck.Property;
using Receipt = Eu.EDelivery.AS4.Model.Core.Receipt;
using Reference = Eu.EDelivery.AS4.Model.Core.Reference;
using Service = Eu.EDelivery.AS4.Model.Core.Service;
using SignalMessage = Eu.EDelivery.AS4.Model.Core.SignalMessage;
using UserMessage = Eu.EDelivery.AS4.Model.Core.UserMessage;

namespace Eu.EDelivery.AS4.UnitTests.Serialization;

/// <summary>
/// Testing <see cref="SoapEnvelopeSerializer" />
/// </summary>
public class GivenSoapEnvelopeSerializerFacts
{
    /// <summary>
    /// Testing if the serializer succeeds
    /// </summary>
    public class GivenSoapEnvelopeSerializerSucceeds : GivenSoapEnvelopeSerializerFacts
    {
        private const string ServiceNamespace = "http://docs.oasis-open.org/ebxml-msg/ebms/v3.0/ns/core/200704/service";
        private const string ActionNamespace = "http://docs.oasis-open.org/ebxml-msg/ebms/v3.0/ns/core/200704/test";

        private static readonly XmlSchemaSet _soap12Schemas;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S3963:\"static\" fields should be initialized inline", Justification = "<Pending>")]
        static GivenSoapEnvelopeSerializerSucceeds()
        {
            var schemas = new XmlSchemaSet();
            using (var stringReader = new StringReader(Schemas.xml))
            {
                var schema = XmlSchema.Read(stringReader, (sender, args) => { });
                schemas.Add(schema!);
            }
            using (var stringReader = new StringReader(Schemas.Soap12))
            {
                var schema = XmlSchema.Read(stringReader, (sender, args) => { });
                schemas.Add(schema!);
            }

            _soap12Schemas = schemas;
        }

        [Fact]
        public async Task PredifinedBizTalkSampleFailsToDeserializeBecauseOfMissingBody()
        {
            using var input = new MemoryStream(Encoding.UTF8.GetBytes(BizTalkUserMessage));
            var sut = new SoapEnvelopeSerializer();
            await Assert.ThrowsAsync<InvalidMessageException>(
                () => sut.DeserializeAsync(input, Constants.ContentTypes.Soap, CancellationToken.None));
        }

        [Fact]
        public void ThenMpcAttributeIsCorrectlySerialized()
        {
            var userMessage = new UserMessage("some-message-id", "the-specified-mpc");
            var as4Message = AS4Message.Create(userMessage);

            using var messageStream = new MemoryStream();
            var sut = new SoapEnvelopeSerializer();

            // Act
            sut.Serialize(as4Message, messageStream);

            // Assert
            messageStream.Position = 0;
            var xmlDocument = new XmlDocument();
            xmlDocument.Load(messageStream);

            var userMessageNode = xmlDocument.SelectSingleNode("//*[local-name()='UserMessage']");
            Assert.NotNull(userMessageNode);
            Assert.Equal(userMessage.Mpc, userMessageNode?.Attributes?["mpc"]?.InnerText);
        }

        [Fact]
        public async Task ThenDeserializeAS4MessageSucceedsAsync()
        {
            // Arrange
            using var memoryStream = AnonymousAS4UserMessage().ToStream();
            // Act
            var message = await DeserializeAsSoap(memoryStream);

            // Assert
            Assert.Single(message.UserMessages);
        }

        [Fact]
        public async Task ThenParseUserMessageCollaborationInfoCorrectly()
        {
            using var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(Samples.UserMessage));
            // Act
            var message = await DeserializeAsSoap(memoryStream);

            // Assert
            var userMessage = message.UserMessages.First();
            Assert.Equal(ServiceNamespace, userMessage.CollaborationInfo.Service.Value);
            Assert.Equal(ActionNamespace, userMessage.CollaborationInfo.Action);
            Assert.Equal("eu:edelivery:as4:sampleconversation", userMessage.CollaborationInfo.ConversationId);
        }

        [Fact]
        public async Task ThenParseUserMessagePropertiesParsedCorrectlyAsync()
        {
            using var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(Samples.UserMessage));
            // Act
            var message = await DeserializeAsSoap(memoryStream);

            // Assert
            var userMessage = message.UserMessages.First();
            Assert.NotNull(message);
            Assert.Single(message.UserMessages);
            Assert.Equal(1472800326948, userMessage.Timestamp.ToUnixTimeMilliseconds());
        }

        [Fact]
        public async Task ThenParseUserMessageReceiverCorrectly()
        {
            using var memoryStream = new MemoryStream(Encoding.UTF32.GetBytes(Samples.UserMessage));
            // Act
            var message = await DeserializeAsSoap(memoryStream);

            // Assert
            var userMessage = message.UserMessages.First();
            var receiverId = userMessage.Receiver.PartyIds.First().Id;
            Assert.Equal("org:holodeckb2b:example:company:B", receiverId);
            Assert.Equal("Receiver", userMessage.Receiver.Role);
        }

        [Fact]
        public async Task ThenParseUserMessageSenderCorrectly()
        {
            using var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(Samples.UserMessage));
            // Act
            var message = await DeserializeAsSoap(memoryStream);

            // Assert
            var userMessage = message.UserMessages.First();
            Assert.Equal("org:eu:europa:as4:example", userMessage.Sender.PartyIds.First().Id);
            Assert.Equal("Sender", userMessage.Sender.Role);
        }

        private static Task<AS4Message> DeserializeAsSoap(Stream str)
        {
            return new SoapEnvelopeSerializer().DeserializeAsync(str, Constants.ContentTypes.Soap, CancellationToken.None);
        }

        [Fact]
        public async Task AS4UserMessageValidatesWithXsdSchema()
        {
            // Arrange
            var userMessage = AnonymousAS4UserMessage();

            // Act / Assert
            await TestValidEbmsMessageEnvelopeFrom(userMessage);
        }

        [Fact]
        public async Task AS4NRRReceiptValidatesWithXsdSchema()
        {
            // Arrange
            var receiptMessage = AS4Message.Create(new FilledNRReceipt());

            // Act / Assert
            await TestValidEbmsMessageEnvelopeFrom(receiptMessage);
        }

        [Fact]
        public async Task AS4MultiHopReceiptValidatesWithXsdSchema()
        {
            using var messageStream = new MemoryStream(as4_multihop_message);
            // Arrange
            var receiptMessage = await new MimeMessageSerializer(NullLogger<MimeMessageSerializer>.Instance, new SoapEnvelopeSerializer()).DeserializeAsync(
                input: messageStream,
                contentType: "multipart/related; boundary=\"=-M/sMGEhQK8RBNg/21Nf7Ig==\";\ttype=\"application/soap+xml\"",
                cancellation: CancellationToken.None);

            // Act / Assert
            await TestValidEbmsMessageEnvelopeFrom(receiptMessage);
        }

        [Fact]
        public async Task AS4ErrorValidatesWithXsdSchema()
        {
            // Arrange
            var errorMessage = AS4Message.Create(
                new Error($"error-{Guid.NewGuid()}", $"user-{Guid.NewGuid()}"));

            // Act / Assert
            await TestValidEbmsMessageEnvelopeFrom(errorMessage);
        }

        private static async Task TestValidEbmsMessageEnvelopeFrom(AS4Message message)
        {
            using var targetStream = new MemoryStream();
            // Act
            await new SoapEnvelopeSerializer().SerializeAsync(message, targetStream);

            // Assert
            var envelope = LoadInEnvelopeDocument(targetStream);
            Assert.True(IsValidEbmsEnvelope(envelope));
        }

        private static XmlDocument LoadInEnvelopeDocument(Stream targetStream)
        {
            targetStream.Position = 0;

            var envelope = new XmlDocument();
            envelope.Load(targetStream);

            return envelope;
        }

        private static bool IsValidEbmsEnvelope(XmlDocument envelopeDocument)
        {
            envelopeDocument.Schemas = _soap12Schemas;

            return ValidateEnvelope(envelopeDocument);
        }

        private static bool ValidateEnvelope(XmlDocument envelopeDocument)
        {
            var isValid = true;
            envelopeDocument.Validate((sender, args) =>
            {
                isValid = false;
            });

            return isValid;
        }

        [Fact]
        public void TestInvalidEnvelopeMissingBody()
        {
            // Arrange
            var doc = new XmlDocument();
            doc.LoadXml(Samples.UserMessage);
            doc.Schemas = _soap12Schemas;

            var envelopeNode = doc.SelectEbmsNode("/s12:Envelope");
            var bodyNode = doc.SelectEbmsNode("/s12:Envelope/s12:Body");
            envelopeNode.RemoveChild(bodyNode);

            // Act
            var valid = true;
            doc.Validate((sender, args) => valid = false);

            // Assert
            Assert.False(valid);
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Blocker Code Smell", "S2699:Tests should include assertions", Justification = "<Pending>")]
    public class AS4MessageSerializeFacts : GivenAS4MessageFacts
    {
        [CustomProperty]
        public Property ThenMessageUnitsAreSerializedInCorrectOrder(
            NonEmptyArray<MessageUnit> messageUnits)
        {
            // Arrange
            var as4 = AS4Message.Create(messageUnits.Get);

            // Act
            var doc = SerializeSoapMessage(as4);

            //Assert
            var actual =
                doc.SelectEbmsNode("/s12:Envelope/s12:Header/eb:Messaging")
                   .ChildNodes
                   .Cast<XmlNode>()
                   .Select(n => n.LocalName);


            var expected = messageUnits.Get.Select(u => u switch
            {
                SignalMessage => "SignalMessage",
                UserMessage => "UserMessage",
                _ => "Unknown"
            });
            return expected
                .SequenceEqual(actual)
                .Label($"{string.Join(", ", expected)} != {string.Join(", ", actual)}");
        }

        [CustomProperty]
        public Property ThenServiceHasOnlyTypeWhenDefined(
            Guid value,
            Maybe<Guid> type)
        {
            // Arrange
            var user = new UserMessage(
                $"user-{Guid.NewGuid()}",
                new AS4.Model.Core.CollaborationInfo(
                    agreement: new AgreementReference("agreement"),
                    service: new Service(
                        value.ToString(),
                        type.Select(t => t.ToString())),
                    action: "action",
                    conversationId: "conversation"));

            // Act
            var doc = SerializeSoapMessage(AS4Message.Create(user));

            // Assert
            var serviceNode = doc.UnsafeSelectEbmsNode(
                "/s12:Envelope/s12:Header/eb:Messaging/eb:UserMessage/eb:CollaborationInfo/eb:Service");

            var serviceTypeAttr = serviceNode?.Attributes?["type"];

            return (serviceNode?.FirstChild?.Value == value.ToString())
                .Label("Equal value")
                .And(serviceTypeAttr == null && type == Maybe<Guid>.Nothing)
                .Label("No service type present")
                .Or(type.Select(t => t.ToString() == serviceTypeAttr?.Value).GetOrElse(false))
                .Label("Equal service type");
        }

        [CustomProperty]
        public Property ThenAgreementReferenceIsPresentWhenDefined(
            Maybe<Guid> value,
            Maybe<Guid> type)
        {
            // Arrange
            var a = value.Select(x =>
                new AgreementReference(
                    value: x.ToString(),
                    type: type.Select(t => t.ToString()),
                    pmodeId: Maybe<string>.Nothing));

            var user = new UserMessage(
                $"user-{Guid.NewGuid()}",
                new AS4.Model.Core.CollaborationInfo(
                    a, Service.TestService, Constants.Namespaces.TestAction, "1"));

            // Act
            var doc = SerializeSoapMessage(AS4Message.Create(user));

            // Assert
            var agreementNode = doc.UnsafeSelectEbmsNode(
                "/s12:Envelope/s12:Header/eb:Messaging/eb:UserMessage/eb:CollaborationInfo/eb:AgreementRef");

            var agreementTypeAttr = agreementNode?.Attributes?["type"];

            var noAgreementTagProp =
                (agreementNode == null && value == Maybe<Guid>.Nothing)
                .Label("No agreement tag");

            var equalValueProp =
                value.Select(v => v.ToString() == agreementNode?.InnerText)
                     .GetOrElse(false)
                     .Label("Equal agreement value");

            var noTypeProp =
                (agreementTypeAttr == null && type == Maybe<Guid>.Nothing)
                .Label("No agreement type");

            var equalTypeProp =
                type.Select(t => t.ToString() == agreementTypeAttr?.Value)
                    .GetOrElse(false)
                    .Label("Equal agreement type");

            return noAgreementTagProp.Or(equalValueProp.And(noTypeProp).Or(equalTypeProp));
        }

        [Fact]
        public void ThenPayloadInfoIsPresentWhenDefined()
        {
            // Arrange
            var user = new UserMessage(
                $"user-{Guid.NewGuid()}",
                new AS4.Model.Core.PartInfo("cid:earth"));

            // Act
            var doc = SerializeSoapMessage(AS4Message.Create(user));

            // Assert
            var payloadInfoTag = doc.UnsafeSelectEbmsNode(
                "/s12:Envelope/s12:Header/eb:Messaging/eb:UserMessage/eb:PayloadInfo");

            Assert.NotNull(payloadInfoTag);
            var partInfoTag = payloadInfoTag.FirstChild;
            Assert.Equal("cid:earth", partInfoTag?.Attributes?["href"]?.Value);
        }

        [Property]
        public static void ThenErrorDetailIsPresentWhenDefined()
        {
            // Arrange
            var error = new Error(
                $"error-{Guid.NewGuid()}",
                $"user-{Guid.NewGuid()}",
                ErrorLine.FromErrorResult(new ErrorResult("sample error", ErrorAlias.ConnectionFailure)));

            // Act
            var doc = SerializeSoapMessage(AS4Message.Create(error));

            // Assert
            var errorTag = doc.SelectEbmsNode(
                "/s12:Envelope/s12:Header/eb:Messaging/eb:SignalMessage/eb:Error");

            const string Expected =
                "<eb:Error " +
                    "category=\"Communication\" " +
                    "errorCode=\"EBMS:0005\" " +
                    "severity=\"FAILURE\" " +
                    "shortDescription=\"ConnectionFailure\" " +
                    "xmlns:eb=\"http://docs.oasis-open.org/ebxml-msg/ebms/v3.0/ns/core/200704/\">" +
                    "<eb:ErrorDetail>sample error</eb:ErrorDetail>" +
                "</eb:Error>";

            Assert.Equal(Expected, errorTag.OuterXml);
        }


        [Property]
        public static void ThenSerializeWithoutAttachmentsReturnsSoapMessage(Guid mpc)
        {
            // Act
            var userMessage = CreateUserMessage();
            var message = BuildAS4Message(mpc.ToString(), userMessage);

            using var soapStream = new MemoryStream();
            var document = SerializeSoapMessage(message, soapStream);
            var envelopeElement = document.DocumentElement;

            // Assert
            Assert.NotNull(envelopeElement);
            Assert.Equal(Constants.Namespaces.Soap12, envelopeElement.NamespaceURI);
        }

        [Property]
        public static void ThenPullRequestCorrectlySerialized(Guid mpc)
        {
            // Arrange
            var userMessage = CreateUserMessage();

            var message = BuildAS4Message(mpc.ToString(), userMessage);

            // Act
            using var soapStream = new MemoryStream();
            var document = SerializeSoapMessage(message, soapStream);

            // Assert
            var mpcAttribute = GetMpcAttribute(document);
            Assert.NotNull(mpcAttribute);
            Assert.Equal(mpc.ToString(), mpcAttribute.Value);
        }

        private static XmlAttribute? GetMpcAttribute(XmlDocument document)
        {
            const string Node = "/s12:Envelope/s12:Header/eb:Messaging/eb:SignalMessage/eb:PullRequest";
            var attributes = document.UnsafeSelectEbmsNode(Node)?.Attributes;

            return attributes?.Cast<XmlAttribute>().FirstOrDefault(x => x.Name == "mpc");
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

        [Fact]
        public void ThenXmlDocumentContainsOneMessagingHeader()
        {
            // Arrange
            using var memoryStream = new MemoryStream();
            var dummyMessage = AnonymousAS4UserMessage();

            // Act
            new SoapEnvelopeSerializer().Serialize(dummyMessage, memoryStream);

            // Assert
            AssertXmlDocumentContainsMessagingTag(memoryStream);
        }

        private static void AssertXmlDocumentContainsMessagingTag(Stream stream)
        {
            stream.Position = 0;
            using var reader = new XmlTextReader(stream);
            var document = new XmlDocument();
            document.Load(reader);
            var nodeList = document.GetElementsByTagName("eb:Messaging");
            Assert.Equal(1, nodeList.Count);
        }
    }

    private static AS4Message AnonymousAS4UserMessage()
    {
        return AS4Message.Create(CreateAnonymousUserMessage());
    }

    private static UserMessage CreateAnonymousUserMessage()
    {
        return new UserMessage(
            "message-Id",
            new Party("Sender", new PartyId(Guid.NewGuid().ToString())),
            new Party("Receiver", new PartyId(Guid.NewGuid().ToString())));
    }
}

public class GivenMultiHopSoapEnvelopeSerializerSucceeds
{
    [Fact]
    public async Task DeserializeMultihopSignalMessage()
    {
        // Arrange
        const string ContentType = "multipart/related; boundary=\"=-M/sMGEhQK8RBNg/21Nf7Ig==\";\ttype=\"application/soap+xml\"";
        var messageString = Encoding.UTF8.GetString(as4_multihop_message).Replace((char)0x1F, ' ');
        var messageContent = Encoding.UTF8.GetBytes(messageString);
        using var messageStream = new MemoryStream(messageContent);
        var serializer = new MimeMessageSerializer(NullLogger<MimeMessageSerializer>.Instance, new SoapEnvelopeSerializer());

        // Act
        var actualMessage = await serializer.DeserializeAsync(messageStream, ContentType, CancellationToken.None);

        // Assert
        Assert.True(actualMessage.IsSignalMessage);
    }

    [Fact]
    public void MultihopUserMessageCreatedWhenSpecifiedInPMode()
    {
        // Arrange
        var as4Message = CreateAS4MessageWithPMode(CreateMultiHopPMode());

        // Act
        var doc = AS4XmlSerializer.ToSoapEnvelopeDocument(as4Message);

        // Assert
        AssertUserMessageMultihopHeaders(doc);
    }

    [Fact]
    public async Task MultihopUserMessageStillContainsMultihopHeadersWhenSerializeDeserializedMessage()
    {
        // Arrange
        var input = new MemoryStream(as4_multihop_usermessage);
        const string ContentType =
            "multipart/related; boundary=\"=-AAB+iUI3phXyeG3w4aGnFA==\";\ttype=\"application/soap+xml\"";

        var sut = Default.SerializerProvider.Get(ContentType);
        var deserialized = await sut.DeserializeAsync(input, ContentType, CancellationToken.None);

        // Act
        var doc = AS4XmlSerializer.ToSoapEnvelopeDocument(deserialized);

        // Assert
        AssertUserMessageMultihopHeaders(doc);
    }

    private static void AssertUserMessageMultihopHeaders(XmlDocument doc)
    {
        var messagingNode = doc.UnsafeSelectEbmsNode("/s12:Envelope/s12:Header/eb:Messaging") as XmlElement;

        Assert.NotNull(messagingNode);
        Assert.Equal(Constants.Namespaces.EbmsNextMsh, messagingNode.GetAttribute("role", Constants.Namespaces.Soap12));
        Assert.True(XmlConvert.ToBoolean(messagingNode.GetAttribute("mustUnderstand", Constants.Namespaces.Soap12)));
    }

    [Fact]
    public async Task ReceiptMessageForMultihopUserMessageIsMultihop()
    {
        var as4Message = await CreateReceivedAS4Message(CreateMultiHopPMode());

        var receipt = Receipt.CreateFor($"receipt-{Guid.NewGuid()}", as4Message.FirstUserMessage!, as4Message.IsMultiHopMessage);

        var doc = AS4XmlSerializer.ToSoapEnvelopeDocument(AS4Message.Create(receipt));

        // Following elements should be present:
        // - To element in the wsa namespace
        // - Action element in the wsa namespace
        // - UserElement in the multihop namespace.
        AssertToElement(doc);
        Assert.True(ContainsActionElement(doc));
        Assert.True(ContainsUserMessageElement(doc));
        AssertUserMessageMessagingElement(as4Message, doc);

        AssertIfSenderAndReceiverAreReversed(as4Message, doc);
    }

    private static void AssertUserMessageMessagingElement(AS4Message as4Message, XmlNode doc)
    {
        AssertMessagingElement(doc);

        var actualRefToMessageId = DeserializeMessagingHeader(doc)?
            .MessageUnits?
            .Cast<AS4.Xml.SignalMessage>()
            .First()
            .MessageInfo
            .RefToMessageId;

        var expectedUserMessageId = as4Message.FirstUserMessage?.MessageId;

        Assert.NotNull(expectedUserMessageId);
        Assert.Equal(expectedUserMessageId, actualRefToMessageId);
    }

    [Fact]
    public async Task ErrorMessageForMultihopUserMessageIsMultihop()
    {
        // Arrange
        var expectedAS4Message = await CreateReceivedAS4Message(CreateMultiHopPMode());

        var error = Error.CreateFor($"error-{Guid.NewGuid()}", expectedAS4Message.FirstUserMessage!, userMessageSendViaMultiHop: true);

        // Act
        var document = AS4XmlSerializer.ToSoapEnvelopeDocument(AS4Message.Create(error));

        // Following elements should be present:
        // - To element in the wsa namespace
        // - Action element in the wsa namespace
        // - UserElement in the multihop namespace.
        AssertToElement(document);
        Assert.True(ContainsActionElement(document));
        Assert.True(ContainsUserMessageElement(document));

        AssertMessagingElement(document);
        AssertIfSenderAndReceiverAreReversed(expectedAS4Message, document);
    }

    private static void AssertToElement(XmlNode doc)
    {
        var toAddressing =
            doc.SelectEbmsNode("/s12:Envelope/s12:Header/wsa:To");

        Assert.Equal(Constants.Namespaces.ICloud, toAddressing.InnerText);
    }

    [Fact]
    public async Task CanDeserializeAndReSerializeMultiHopReceipt()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(multihopreceipt));
        var multihopReceipt =
            await Default.SerializerProvider.Get(Constants.ContentTypes.Soap)
                                    .DeserializeAsync(
                                        stream,
                                        Constants.ContentTypes.Soap,
                                        CancellationToken.None);

        Assert.NotNull(multihopReceipt);
        Assert.NotNull(multihopReceipt.FirstSignalMessage);
        Assert.True(
            multihopReceipt.FirstSignalMessage.IsMultihopSignal,
            "Should have multihop routing information");

        // Serialize the Deserialized receipt again, and make sure the RoutingInput element is present and correct.
        var doc = AS4XmlSerializer.ToSoapEnvelopeDocument(multihopReceipt);

        var routingInput = doc.UnsafeSelectEbmsNode("/s12:Envelope/s12:Header/mh:RoutingInput");

        Assert.NotNull(routingInput);
    }

    [Fact]
    public async Task ReceiptMessageForNonMultiHopMessageIsNotMultiHop()
    {
        var as4Message = await CreateReceivedAS4Message(CreateNonMultiHopPMode());

        var receipt = Receipt.CreateFor($"receipt-{Guid.NewGuid()}", as4Message.FirstUserMessage!, as4Message.IsMultiHopMessage);

        var doc = AS4XmlSerializer.ToSoapEnvelopeDocument(AS4Message.Create(receipt));

        // No MultiHop related elements may be present:
        // - No Action element in the wsa namespace
        // - No UserElement in the multihop namespace.
        // - No RoutingInput node
        Assert.False(ContainsActionElement(doc));
        Assert.False(ContainsUserMessageElement(doc));
        Assert.Null(doc.UnsafeSelectEbmsNode("/s12:Envelope/s12:Header/mh:RoutingInput"));
    }

    private static bool ContainsUserMessageElement(XmlNode doc)
    {
        return doc.UnsafeSelectEbmsNode("/s12:Envelope/s12:Header/mh:RoutingInput/mh:UserMessage") != null;
    }

    private static bool ContainsActionElement(XmlNode doc)
    {
        return doc.UnsafeSelectEbmsNode("/s12:Envelope/s12:Header/wsa:Action") != null;
    }

    private static void AssertMessagingElement(XmlNode doc)
    {
        var messaging = DeserializeMessagingHeader(doc);
        Assert.NotNull(messaging);
        Assert.True(messaging.mustUnderstand1);
        Assert.Equal(Constants.Namespaces.EbmsNextMsh, messaging.role);
    }

    private static Messaging? DeserializeMessagingHeader(XmlNode doc)
    {
        var messagingNode = doc.UnsafeSelectEbmsNode("/s12:Envelope/s12:Header/eb:Messaging");
        Assert.NotNull(messagingNode);

        var s = new XmlSerializer(typeof(Messaging), SoapEnvelopeSerializer.SoapEnvelopeBuilder.MessagingAttributeOverrides);
        return s.Deserialize(new XmlNodeReader(messagingNode)) as Messaging;
    }

    private static void AssertIfSenderAndReceiverAreReversed(AS4Message expectedAS4Message, XmlNode doc)
    {
        var routingInputNode = doc.UnsafeSelectEbmsNode("/s12:Envelope/s12:Header/mh:RoutingInput");
        Assert.NotNull(routingInputNode);
        var routingInput = AS4XmlSerializer.FromString<RoutingInput>(routingInputNode.OuterXml);
        Assert.NotNull(routingInput);

        var actualUserMessage = routingInput.UserMessage;
        var expectedUserMessage = expectedAS4Message.FirstUserMessage;

        Assert.NotNull(expectedUserMessage);
        Assert.Equal(expectedUserMessage.Sender.Role, actualUserMessage.PartyInfo.To.Role);
        Assert.Equal(
            expectedUserMessage.Sender.PartyIds.First().Id,
            actualUserMessage.PartyInfo.To.PartyId.First().Value);
        Assert.Equal(expectedUserMessage.Receiver.Role, actualUserMessage.PartyInfo.From.Role);
        Assert.Equal(
            expectedUserMessage.Receiver.PartyIds.First().Id,
            actualUserMessage.PartyInfo.From.PartyId.First().Value);
    }

    private static AS4Message CreateAS4MessageWithPMode(SendingProcessingMode pmode)
    {
        var sender = new Party("sender", new PartyId("senderId"));
        var receiver = new Party("rcv", new PartyId("receiverId"));

        return AS4Message.Create(new UserMessage(Guid.NewGuid().ToString(), sender, receiver), pmode);
    }

    private static async Task<AS4Message> CreateReceivedAS4Message(SendingProcessingMode sendPMode)
    {

        var message = CreateAS4Message(sendPMode);
        var context = new MessagingContext(message, MessagingContextMode.Receive) { SendingPMode = sendPMode };

        var serializer = Default.SerializerProvider.Get(message.ContentType);

        // Serialize and deserialize the AS4 Message to simulate a received message.
        using var stream = new MemoryStream();
        await serializer.SerializeAsync(context.AS4Message!, stream, CancellationToken.None);
        stream.Position = 0;

        return await serializer.DeserializeAsync(stream, message.ContentType, CancellationToken.None);
    }

    private static AS4Message CreateAS4Message(SendingProcessingMode sendPMode)
    {
        var sender = new Party("sender", new PartyId("senderId"));
        var receiver = new Party("rcv", new PartyId("receiverId"));

        return AS4Message.Create(new UserMessage(Guid.NewGuid().ToString(), sender, receiver), sendPMode);
    }

    private static SendingProcessingMode CreateMultiHopPMode()
    {
        return new SendingProcessingMode { Id = "multihop-pmode", MessagePackaging = { IsMultiHop = true } };
    }

    private static SendingProcessingMode CreateNonMultiHopPMode()
    {
        return new SendingProcessingMode { Id = "multihop-pmode", MessagePackaging = { IsMultiHop = false } };
    }
}

public class GivenReceiptSerializationSucceeds : GivenSoapEnvelopeSerializerFacts
{
    [Fact]
    public void ThenNonRepudiationInfoElementBelongsToCorrectNamespace()
    {
        var receipt = CreateReceiptWithNonRepudiationInfo();

        var as4Message = AS4Message.Create(receipt);

        var document = AS4XmlSerializer.ToSoapEnvelopeDocument(as4Message);

        var node = document.UnsafeSelectEbmsNode("/s12:Envelope/s12:Header/eb:Messaging/eb:SignalMessage/eb:Receipt/ebbp:NonRepudiationInformation");

        Assert.NotNull(node);
        Assert.Equal(Constants.Namespaces.EbmsXmlSignals, node.NamespaceURI);
    }

    [Fact]
    public void ThenRelatedUserMessageElementBelongsToCorrectNamespace()
    {
        var receipt = CreateReceiptWithRelatedUserMessageInfo();

        var as4Message = AS4Message.Create(receipt);

        var document = AS4XmlSerializer.ToSoapEnvelopeDocument(as4Message);

        var node = document.UnsafeSelectEbmsNode("/s12:Envelope/s12:Header/eb:Messaging/eb:SignalMessage/eb:Receipt/ebbp:UserMessage");

        Assert.NotNull(node);
        Assert.Equal(Constants.Namespaces.EbmsXmlSignals, node.NamespaceURI);
    }

    private static Receipt CreateReceiptWithNonRepudiationInfo()
    {
        var nnri = new[]
        {
            new System.Security.Cryptography.Xml.Reference
            {
                Uri = $"uri-{Guid.NewGuid()}",
                TransformChain = new TransformChain(),
                DigestMethod = $"digestmethod-{Guid.NewGuid()}",
                DigestValue = [1, 2, 3]
            }
        };

        return new Receipt(
            $"receipt-{Guid.NewGuid()}",
            $"user-{Guid.NewGuid()}",
            new NonRepudiationInformation(
                nnri.Select(Reference.CreateFromReferenceElement)));
    }

    private static Receipt CreateReceiptWithRelatedUserMessageInfo()
    {
        var ebmsMessageId = $"user-{Guid.NewGuid()}";
        var userMessage = new UserMessage(ebmsMessageId);

        return Receipt.CreateFor($"receipt-{Guid.NewGuid()}", userMessage);
    }
}

public class GivenReserializationFacts
{
    [CustomProperty]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Blocker Code Smell", "S2699:Tests should include assertions", Justification = "<Pending>")]
    public Property RedeserializeResultInSameMessageUnitsOrder(NonEmptyArray<MessageUnit> messageUnits)
    {
        // Arrange
        var start = AS4Message.Create(messageUnits.Get);

        static string ToName(MessageUnit u) => u switch
        {
            SignalMessage => "SignalMessage",
            UserMessage => "UserMessage",
            _ => "Unknown"
        };

        var expected = messageUnits.Get.Select(ToName);

        // Act
        var end = Default.SerializerProvider
            .SerializeDeserializeAsync(start, CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        // Assert
        var actual = end.MessageUnits.Select(ToName);
        return expected
            .SequenceEqual(actual)
            .Label($"{string.Join(", ", expected)} != {string.Join(", ", actual)}");
    }

    [Fact]
    public async Task ReserializedMessageHasUntouchedSoapEnvelope()
    {
        var deserializedAS4Message = await DeserializeToAS4Message(
            rssbus_message,
            @"multipart/related;boundary=""NSMIMEBoundary__e5cfd617-6cec-4276-b190-23f0b25d9d4d"";type=""application/soap+xml"";start=""<_7a711d7c-4d1c-4ce7-ab38-794a01b445e1>""");

        var reserializedAS4Message = await Default.SerializerProvider
            .SerializeDeserializeAsync(deserializedAS4Message, CancellationToken.None);

        Assert.NotNull(deserializedAS4Message.EnvelopeDocument);
        Assert.NotNull(reserializedAS4Message.EnvelopeDocument);
        Assert.Equal(
            deserializedAS4Message.EnvelopeDocument.OuterXml,
            reserializedAS4Message.EnvelopeDocument.OuterXml);
    }

    [Fact]
    public async Task CanDeserializeEncryptAndSerializeSignedMessageWithUntouchedMessagingHeader()
    {
        // Arrange: retrieve an existing signed AS4 Message and encrypt it. 
        //          Serialize it again to inspect the Soap envelope of the modified message.
        var deserializedAS4Message =
            await DeserializeToAS4Message(
                signed_holodeck_message,
                @"multipart/related;boundary=""MIMEBoundary_bcb27a6f984295aa9962b01ef2fb3e8d982de76d061ab23f""");

        var originalSecurityHeader = deserializedAS4Message.SecurityHeader.GetXml()?.CloneNode(deep: true);

        var encryptionCertificate = new X509Certificate2(certificate_as4, certificate_password);

        // Act: Encrypt the message
        deserializedAS4Message.Encrypt(
            new KeyEncryptionConfiguration(encryptionCertificate),
            DataEncryptionConfiguration.Default);

        // Assert: the soap envelope of the encrypted message should not be equal to the
        //         envelope of the original message since there should be modifications in
        //         the security header.
        Assert.NotNull(originalSecurityHeader);
        Assert.NotNull(deserializedAS4Message.EnvelopeDocument);
        Assert.NotEqual(
            originalSecurityHeader.OuterXml,
            deserializedAS4Message.EnvelopeDocument.OuterXml);

        // Serialize it again; the Soap envelope should remain intact, besides
        // some changes that have been made to the security header.
        var reserializedAS4Message = await Default.SerializerProvider
            .SerializeDeserializeAsync(deserializedAS4Message, CancellationToken.None);

        // Assert: The soap envelopes of both messages should be equal if the 
        //         SecurityHeader is not taken into consideration.

        RemoveSecurityHeaderFromMessageEnvelope(reserializedAS4Message);
        RemoveSecurityHeaderFromMessageEnvelope(deserializedAS4Message);

        Assert.NotNull(deserializedAS4Message.EnvelopeDocument);
        Assert.NotNull(reserializedAS4Message.EnvelopeDocument);
        Assert.Equal(
            reserializedAS4Message.EnvelopeDocument.OuterXml,
            deserializedAS4Message.EnvelopeDocument.OuterXml);
    }

    private static async Task<AS4Message> DeserializeToAS4Message(byte[] content, string contentType)
    {
        // Note that the stream cannot be disposed here, since the AS4Message needs to
        // keep an open reference to it so that it can access the attachments.
        var stream = new MemoryStream(content);

        var serializer = Default.SerializerProvider.Get(contentType);

        return await serializer.DeserializeAsync(stream, contentType, CancellationToken.None);
    }

    private static void RemoveSecurityHeaderFromMessageEnvelope(AS4Message as4Message)
    {
        var headerNode = as4Message.EnvelopeDocument?.UnsafeSelectEbmsNode("/s12:Envelope/s12:Header");
        Assert.NotNull(headerNode);

        var securityHeader = as4Message.EnvelopeDocument?.UnsafeSelectEbmsNode("/s12:Envelope/s12:Header/wsse:Security");
        Assert.NotNull(securityHeader);

        headerNode.RemoveChild(securityHeader);
    }
}
