using MimeKit;

namespace Eu.EDelivery.AS4.UnitTests.Steps.Repositories;

/// <summary>
/// Testing <see cref="MimeTypes" />
/// </summary>
public class GivenMimeTypeRepositoryFacts
{
    public class GivenValidArguments : GivenMimeTypeRepositoryFacts
    {
        [Fact]
        public void ThenGetsExtensionSucceedsWithValidMimeContentType()
        {
            // Arrange
            const string MimeContentType = "image/jpeg";

            // Act
            _ = MimeTypes.TryGetExtension(MimeContentType, out var extension);

            // Assert
            Assert.Equal(".jpg", extension);
        }
    }

    public class GivenInvalidArguments : GivenMimeTypeRepositoryFacts
    {
        [Fact]
        public void ThenGetsExtensionFailsWithInvalidMimeContentType()
        {
            // Arrange
            const string MimeContentType = "invalid/type";

            // Act
            _ = MimeTypes.TryGetExtension(MimeContentType, out var extension);

            // Assert
            Assert.Null(extension);
        }
    }
}
