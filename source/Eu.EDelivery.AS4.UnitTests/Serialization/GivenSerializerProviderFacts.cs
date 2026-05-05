using Eu.EDelivery.AS4.Serialization;

namespace Eu.EDelivery.AS4.UnitTests.Serialization;

public class GivenSerializerProviderFacts
{
    [Theory]
    [InlineData(Constants.ContentTypes.Mime, typeof(MimeMessageSerializer))]
    [InlineData(Constants.ContentTypes.Soap, typeof(SoapEnvelopeSerializer))]
    public void ThenCanProvideSerializer(string contentType, Type expectedType)
    {
        var serializer = Default.SerializerProvider.Get(contentType);

        Assert.NotNull(serializer);
        Assert.IsType(expectedType, serializer);
    }
}
