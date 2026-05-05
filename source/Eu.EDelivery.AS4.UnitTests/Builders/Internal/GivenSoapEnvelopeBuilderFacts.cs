using System.Xml;
using Eu.EDelivery.AS4.Serialization;
using Eu.EDelivery.AS4.UnitTests.Extensions;
using Eu.EDelivery.AS4.Xml;

namespace Eu.EDelivery.AS4.UnitTests.Builders.Internal;

/// <summary>
/// Testing the <see cref="SoapEnvelopeSerializer.SoapEnvelopeBuilder" />
/// </summary>
public class GivenSoapEnvelopeBuilderFacts
{
    private readonly SoapEnvelopeSerializer.SoapEnvelopeBuilder _builder;

    public GivenSoapEnvelopeBuilderFacts()
    {
        _builder = new SoapEnvelopeSerializer.SoapEnvelopeBuilder(envelopeDocument: null);
    }

    public class GivenValidArgumentsBuilder : GivenSoapEnvelopeBuilderFacts
    {
        [Fact]
        public void BuilderFailsToCreateWhenEnvelopeElementIsMissing()
        {
            Assert.Throws<NotSupportedException>(
                () => new SoapEnvelopeSerializer.SoapEnvelopeBuilder(new XmlDocument()));
        }

        [Fact]
        public void BuilderFailsToCreateWhenHeaderElementIsMissing()
        {
            var doc = new XmlDocument();
            var envelope = doc.CreateElement("s12", "Envelope", Constants.Namespaces.Soap12);
            doc.AppendChild(envelope);

            Assert.Throws<NotSupportedException>(
                () => new SoapEnvelopeSerializer.SoapEnvelopeBuilder(doc));
        }

        [Fact]
        public void BuilderFailsToCreateWhenBodyElementIsMissing()
        {
            var doc = new XmlDocument();
            var envelope = doc.CreateElement("s12", "Envelope", Constants.Namespaces.Soap12);
            envelope.AppendChild(doc.CreateElement("s12", "Header", Constants.Namespaces.Soap12));
            doc.AppendChild(envelope);

            Assert.Throws<NotSupportedException>(
                () => new SoapEnvelopeSerializer.SoapEnvelopeBuilder(doc));
        }

        [Theory]
        [InlineData(Constants.Namespaces.EbmsOneWayReceipt)]
        [InlineData(Constants.Namespaces.EbmsOneWayError)]
        public void ThenResultContainsAction(string action)
        {
            // Act
            var envelope = _builder.SetActionHeader(action).Build();

            // Assert
            Assert.NotNull(envelope);
            var actionNode = envelope.SelectEbmsNode("/s12:Envelope/s12:Header/wsa:Action");
            Assert.Equal(action, actionNode.InnerText);
        }

        [Fact]
        public void ThenBuilderStartsWithEmptyEnvelope()
        {
            // Act
            var envelope = _builder.Build();

            // Assert
            Assert.NotNull(envelope);
            var envelopeNode = envelope.SelectEbmsNode("/s12:Envelope");
            Assert.Empty(envelopeNode.ChildNodes);
        }

        [Fact]
        public void ThenResultContainsBody()
        {
            // Arrange
            var bodySecurityId = $"#body-{Guid.NewGuid()}";

            // Act
            var envelope =
                _builder.SetMessagingBody(bodySecurityId)
                        .Build();

            // Assert
            Assert.NotNull(envelope);
            var bodyNode = envelope.SelectEbmsNode("/s12:Envelope/s12:Body");
            Assert.Equal("s12:Body", bodyNode.Name);
            Assert.Equal(Constants.Namespaces.Soap12, bodyNode.NamespaceURI);
        }

        [Fact]
        public void ThenResultContainsEnvelope()
        {
            // Act
            var envelope = _builder.Build();

            // Assert
            Assert.NotNull(envelope);
            var envelopeNode = envelope.SelectEbmsNode("/s12:Envelope");
            Assert.Equal("s12:Envelope", envelopeNode.Name);
            Assert.Equal(Constants.Namespaces.Soap12, envelopeNode.NamespaceURI);
        }

        [Fact]
        public void ThenResultDoesntContainsHeader()
        {
            // Act
            var envelope = _builder.Build();

            // Assert
            Assert.NotNull(envelope);
            var headerNode = envelope.UnsafeSelectEbmsNode("/s12:Envelope/s12:Header");
            Assert.Null(headerNode);
        }

        [Fact]
        public void ThenResultContainsRoutingInput()
        {
            // Arrange
            var routingInput = new RoutingInput
            {
                UserMessage = new()
                {
                    MessageInfo = new() { MessageId = "MessageId", Timestamp = DateTime.Now },
                    CollaborationInfo = new()
                    {
                        Action = "Action",
                        ConversationId = "ConversationId",
                        Service = new() { Value = "Value" },
                    },
                    PartyInfo = new()
                    {
                        From = new()
                        {
                            PartyId = [new() { type = "type", Value = "Value" }],
                            Role = "Role"
                        },
                        To = new()
                        {
                            PartyId = [new() { type = "type", Value = "Value" }],
                            Role = "Role"
                        },
                    }
                }
            };

            // Act
            var envelope =
                _builder.SetRoutingInput(routingInput)
                        .Build();

            // Assert
            Assert.NotNull(envelope);
            envelope.SelectEbmsNode("/s12:Envelope/s12:Header/mh:RoutingInput");
        }

        [Fact]
        public void ThenResultContainsSecurityHeader()
        {
            // Arrange
            var securityNode = new XmlDocument().CreateNode(
                XmlNodeType.Element,
                "SecurityHeader",
                Constants.Namespaces.WssSecuritySecExt);

            // Act
            var envelope =
                _builder.SetSecurityHeader(securityNode)
                        .Build();

            // Assert
            Assert.NotNull(envelope);
            envelope.SelectEbmsNode("/s12:Envelope/s12:Header/wsse:SecurityHeader");
        }

        [Fact]
        public void ThenResultContainsTo()
        {
            // Arrange
            var to = new To { Role = Constants.Namespaces.ICloud, PartyId = [] };

            // Act
            var envelope =
                _builder.SetToHeader(to)
                        .Build();

            // Assert
            var toNode = envelope.SelectEbmsNode("/s12:Envelope/s12:Header/wsa:To");
            Assert.Equal(to.Role, toNode.InnerText);
        }
    }
}
