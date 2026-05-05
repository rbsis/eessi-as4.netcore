using Eu.EDelivery.AS4.Mappings.PMode;
using Eu.EDelivery.AS4.Model.PMode;
using AgreementReference = Eu.EDelivery.AS4.Model.PMode.AgreementReference;

namespace Eu.EDelivery.AS4.UnitTests.Mappings.PMode;

/// <summary>
/// Testing <see cref="SendingPModeMap.ResolveAgreementReference" />
/// </summary>
public class SendingPModeMapResolveAgreementReferenceFacts
{
    public class GivenValidArguments : SendingPModeMapResolveAgreementReferenceFacts
    {
        private static AgreementReference CreateDefaultAgreementRef() => new()
        {
            Value = "name",
            Type = "type",
            PModeId = "pmode-id"
        };

        [Fact]
        public void ThenResolverGetsAgreementRef()
        {
            // Arrange
            var pmode = CreateSendingPMode(includePMode: false);

            // Act
            var agreementRef = SendingPModeMap.ResolveAgreementReference(pmode).UnsafeGet;

            // Assert
            var pmodeRef = pmode.MessagePackaging.CollaborationInfo!.AgreementReference;
            Assert.Equal(pmodeRef.Value, agreementRef.Value);
            Assert.Equal(Maybe.Just(pmodeRef.Type!), agreementRef.Type);
            Assert.NotEqual(Maybe.Just(pmode.Id), agreementRef.PModeId);
        }

        [Fact]
        public void ThenResolverGetsAgreementRefWithPModeId()
        {
            // Arrange
            var pmode = CreateSendingPMode(includePMode: true);

            // Act
            var agreementRef = SendingPModeMap.ResolveAgreementReference(pmode).UnsafeGet;

            // Assert
            var pmodeRef = pmode.MessagePackaging.CollaborationInfo!.AgreementReference;
            Assert.Equal(pmodeRef.Value, agreementRef.Value);
            Assert.Equal(Maybe.Just(pmodeRef.Type!), agreementRef.Type);
            Assert.Equal(Maybe.Just(pmode.Id), agreementRef.PModeId);
        }

        private static SendingProcessingMode CreateSendingPMode(bool includePMode) => new()
        {
            Id = "pmode-id",
            MessagePackaging =
            {
                IncludePModeId = includePMode,
                CollaborationInfo = new()
                {
                    AgreementReference = CreateDefaultAgreementRef()
                }
            }
        };
    }
}
