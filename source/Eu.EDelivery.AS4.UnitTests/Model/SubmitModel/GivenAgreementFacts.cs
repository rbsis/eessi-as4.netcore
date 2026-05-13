using Eu.EDelivery.AS4.Model.Common;

namespace Eu.EDelivery.AS4.UnitTests.Model.SubmitModel;

/// <summary>
/// Testing <see cref="Agreement" />
/// </summary>
public class GivenAgreementFacts
{
    public class GivenValidArguments : GivenAgreementFacts
    {
        [Theory]
        [InlineData("shared-value", "shared-type", "shared-pmode-id")]
        public void ThenTwoAgreementsAreEqual(string sharedValue, string sharedType, string sharedPModeId)
        {
            // Arrange
            var agreementA = CreateAgreement(sharedValue, sharedType, sharedPModeId);
            var agreementB = agreementA;

            // Act
            var isEqual = agreementA.Equals(agreementB);

            // Assert
            Assert.True(isEqual);
        }

        [Theory]
        [InlineData("shared-value", "shared-type", "shared-pmode-id")]
        public void ThenTwoAgreementsAreEqualForObject(string sharedValue, string sharedType, string sharedPModeId)
        {
            // Arrange
            var agreementA = CreateAgreement(sharedValue, sharedType, sharedPModeId);
            var agreementB = CreateAgreement(sharedValue, sharedType, sharedPModeId);

            // Act
            var isEqual = agreementA.Equals((object)agreementB);

            // Assert
            Assert.True(isEqual);
        }

        [Theory]
        [InlineData("shared-value", "shared-type", "shared-pmode-id")]
        public void ThenTwoAgreementsAreEqualForProperties(
            string sharedValue,
            string sharedType,
            string sharedPModeId)
        {
            // Arrange
            var agreementA = CreateAgreement(sharedValue, sharedType, sharedPModeId);
            var agreementB = CreateAgreement(sharedValue, sharedType, sharedPModeId);

            // Act
            var isEqual = agreementA.Equals(agreementB);

            // Assert
            Assert.True(isEqual);
        }

        [Theory]
        [InlineData("shared-value", "shared-type", "shared-pmode-id")]
        public void ThenTwoAgreementsAreNotEqualForPModeid(
            string sharedValue,
            string sharedType,
            string sharedPModeId)
        {
            // Arrange
            var agreementA = CreateAgreement(sharedValue, sharedType, sharedPModeId);
            var agreementB = CreateAgreement(sharedValue, sharedType, "not-equal");

            // Act
            var isEqual = agreementA.Equals(agreementB);

            // Assert
            Assert.False(isEqual);
        }

        [Theory]
        [InlineData("shared-value", "shared-type", "shared-pmode-id")]
        public void ThenTwoAgreementsAreNotEqualForType(string sharedValue, string sharedType, string sharedPModeId)
        {
            // Arrange
            var agreementA = CreateAgreement(sharedValue, sharedType, sharedPModeId);
            var agreementB = CreateAgreement(sharedValue, "not-equal", sharedPModeId);

            // Act
            var isEqual = agreementA.Equals(agreementB);

            // Assert
            Assert.False(isEqual);
        }

        [Theory]
        [InlineData("shared-value", "shared-type", "shared-pmode-id")]
        public void ThenTwoAgreementsAreNotEqualForValue(
            string sharedValue,
            string sharedType,
            string sharedPModeId)
        {
            // Arrange
            var agreementA = CreateAgreement(sharedValue, sharedType, sharedPModeId);
            var agreementB = CreateAgreement("not-equal", sharedType, sharedPModeId);

            // Act
            var isEqual = agreementA.Equals(agreementB);

            // Assert
            Assert.False(isEqual);
        }
    }

    public class GivenInvalidAgruments : GivenAgreementFacts
    {
        [Theory]
        [InlineData("shared-value", "shared-type", "shared-pmode-id")]
        public void ThenTwoAgreementsAreNotEqualForNull(string sharedValue, string sharedType, string sharedPModeId)
        {
            // Arrange
            var agreementA = CreateAgreement(sharedValue, sharedType, sharedPModeId);
            Agreement? agreementB = null;

            // Act
            var isEqual = agreementA.Equals(agreementB);

            // Assert
            Assert.False(isEqual);
        }
    }

    protected static Agreement CreateAgreement(string value, string type, string pmodeId)
    {
        return new Agreement { Value = value, RefType = type, PModeId = pmodeId };
    }
}
