using Eu.EDelivery.AS4.Factories;
using Eu.EDelivery.AS4.Mappings.PMode;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.TestUtils.Stubs;

namespace Eu.EDelivery.AS4.UnitTests.Mappings.PMode;

/// <summary>
/// Testing <see cref="SendingPModeMap.ResolveService" />
/// </summary>
public class SendingPModeMapResolveServiceFacts
{
    public class GivenValidArguments : SendingPModeMapResolveServiceFacts
    {
        private readonly SendingPModeMap _sut = new(new IdentifierFactory(StubConfig.Default));

        [Fact]
        public void ThenResolverGetsDefaultService()
        {
            // Arrange
            var pmode = new SendingProcessingMode();

            // Act
            var service = _sut.ResolveService(pmode);

            // Assert
            Assert.Equal(AS4.Model.Core.Service.TestService, service);
        }

        [Fact]
        public void ThenResolverGetService()
        {
            // Arrange
            var pmode = CreateDefaultSendingPMode();

            // Act
            var actual = _sut.ResolveService(pmode);

            // Assert
            var expected = pmode.MessagePackaging.CollaborationInfo!.Service;
            Assert.Equal(expected.Value, actual.Value);
            Assert.Equal(Maybe.Just(expected.Type!), actual.Type);
        }

        private static SendingProcessingMode CreateDefaultSendingPMode() => new()
        {
            MessagePackaging =
            {
                CollaborationInfo = new()
                {
                    Service = new()
                    {
                        Value = "name",
                        Type = "type"
                    }
                }
            }
        };
    }
}
