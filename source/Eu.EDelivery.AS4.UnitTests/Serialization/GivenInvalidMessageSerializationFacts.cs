using Eu.EDelivery.AS4.Model.Core;
using static Eu.EDelivery.AS4.UnitTests.Properties.Resources;

namespace Eu.EDelivery.AS4.UnitTests.Serialization;

public class GivenInvalidMessageSerializationFacts
{
    [Fact]
    // ReSharper disable once InconsistentNaming
    public async Task CanDeserializeNRReceiptWithInvalidNRINamespace()
    {
        // Arrange
        const string ContentType =
            @"multipart/related;boundary=""NSMIMEBoundary__5c65cf85-89d2-4921-9029-63df49ef2fa2"";type=""application/soap+xml"";start=""<_7a711d7c-4d1c-4ce7-ab38-794a01b445e1>""";

        // Act
        var actual = await DeserializeAS4Message(Receipt_InvalidNRI_Namespace, ContentType);

        // Assert
        Assert.NotNull(actual);

        var receipt = actual.FirstSignalMessage as Receipt;
        Assert.NotNull(receipt);
        Assert.NotNull(receipt.NonRepudiationInformation);
        Assert.Empty(receipt.NonRepudiationInformation.MessagePartNRIReferences);
    }

    private static async Task<AS4Message> DeserializeAS4Message(byte[] contents, string contentType)
    {
        using var input = new MemoryStream(contents);
        return await Default.SerializerProvider
            .Get(contentType)
            .DeserializeAsync(input, contentType, CancellationToken.None);
    }
}
