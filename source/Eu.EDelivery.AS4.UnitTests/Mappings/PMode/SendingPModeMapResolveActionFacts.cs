using Eu.EDelivery.AS4.Factories;
using Eu.EDelivery.AS4.Mappings.PMode;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.UnitTests.Common;

namespace Eu.EDelivery.AS4.UnitTests.Mappings.PMode;

/// <summary>
/// Testing <see cref="SendingPModeMap.ResolveAction" />
/// </summary>
public class SendingPModeMapResolveActionFacts
{
    public class GivenValidArguments : SendingPModeMapResolveActionFacts
    {
        private readonly SendingPModeMap _sut = new(new IdentifierFactory(StubConfig.Default));

        [Fact]
        public void ThenResolverGetsAction()
        {
            // Arrange
            var pmode = new SendingProcessingMode
            {
                MessagePackaging = { CollaborationInfo = new CollaborationInfo { Action = "action" } }
            };

            // Act
            var action = _sut.ResolveAction(pmode);

            // Assert
            Assert.Equal(pmode.MessagePackaging.CollaborationInfo.Action, action);
        }

        [Fact]
        public void ThenResolverGetsDefaultAction()
        {
            // Arrange
            var pmode = new SendingProcessingMode();

            // Act
            var action = _sut.ResolveAction(pmode);

            // Assert
            Assert.Equal(Constants.Namespaces.TestAction, action);
        }
    }
}
