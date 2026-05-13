using Eu.EDelivery.AS4.Model.Common;

namespace Eu.EDelivery.AS4.UnitTests.Model.SubmitModel;

/// <summary>
/// Testing <see cref="CollaborationInfo" />
/// </summary>
public class GivenCollaborationInfoFacts
{
    public class GivenValidArguments : GivenCollaborationInfoFacts
    {
        [Fact]
        public void ThenCollaborationInfoHasDefaults()
        {
            // Act
            var collaborationInfo = new CollaborationInfo();

            // Assert
            Assert.Null(collaborationInfo.AgreementRef);
            Assert.Null(collaborationInfo.Action);
        }

        [Fact]
        public void ThenTwoCollaborationInfosAreEqual()
        {
            // Arrange
            var collaborationInfoA = CreateCollaborationInfo();
            var collaborationInfoB = collaborationInfoA;

            // Act
            var isEqual = collaborationInfoA.Equals(collaborationInfoB);

            // Assert
            Assert.True(isEqual);
        }

        [Fact]
        public void ThenTwoCollaborationInfosAreEqualForObject()
        {
            // Arrange
            var collaborationInfoA = CreateCollaborationInfo();
            var collaborationInfoB = CreateCollaborationInfo();

            // Act
            var isEqual = collaborationInfoA.Equals((object)collaborationInfoB);

            // Assert
            Assert.True(isEqual);
        }

        [Fact]
        public void ThenTwoCollaborationInfosAreEqualForProperties()
        {
            // Arrange
            var collaborationInfoA = CreateCollaborationInfo();
            var collaborationInfoB = CreateCollaborationInfo();

            // Act
            var isEqual = collaborationInfoA.Equals(collaborationInfoB);

            // Assert
            Assert.True(isEqual);
        }

        [Fact]
        public void ThenTwoCollaborationInfosAreNotEqualForAction()
        {
            // Arrange
            var collaborationInfoA = CreateCollaborationInfo();
            var collaborationInfoB = CreateCollaborationInfo();
            collaborationInfoB.Action = "not-equal";

            // Act
            var isEqual = collaborationInfoA.Equals(collaborationInfoB);

            // Assert
            Assert.False(isEqual);
        }

        [Fact]
        public void ThenTwoCollaborationInfosAreNotEqualForAgreementRef()
        {
            // Arrange
            var collaborationInfoA = CreateCollaborationInfo();
            var collaborationInfoB = CreateCollaborationInfo();
            collaborationInfoB.AgreementRef = new Agreement { Value = "not-equal" };

            // Act
            var isEqual = collaborationInfoA.Equals(collaborationInfoB);

            // Assert
            Assert.False(isEqual);
        }

        [Fact]
        public void ThenTwoCollaborationInfosAreNotEqualForConversationId()
        {
            // Arrange
            var collaborationInfoA = CreateCollaborationInfo();
            var collaborationInfoB = CreateCollaborationInfo();
            collaborationInfoB.ConversationId = "not-equal";

            // Act
            var isEqual = collaborationInfoA.Equals(collaborationInfoB);

            // Assert
            Assert.False(isEqual);
        }

        [Fact]
        public void ThenTwoCollaborationInfosAreNotEqualForService()
        {
            // Arrange
            var collaborationInfoA = CreateCollaborationInfo();
            var collaborationInfoB = CreateCollaborationInfo();
            collaborationInfoB.Service = new Service { Value = "not-equal" };

            // Act
            var isEqual = collaborationInfoA.Equals(collaborationInfoB);

            // Assert
            Assert.False(isEqual);
        }
    }

    public class GivenInvalidArguments : GivenCollaborationInfoFacts
    {
        [Fact]
        public void ThenTwoCollaborationInfosAreNotEqualForNull()
        {
            // Arrange
            var collaborationInfoA = CreateCollaborationInfo();
            CollaborationInfo? collaborationInfoB = null;

            // Act
            var isEqual = collaborationInfoA.Equals(collaborationInfoB);

            // Assert
            Assert.False(isEqual);
        }
    }

    protected static CollaborationInfo CreateCollaborationInfo()
    {
        return new CollaborationInfo
        {
            Action = "shared-action",
            ConversationId = "shared-conversation-id",
            Service = new Service(),
            AgreementRef = new Agreement()
        };
    }
}
