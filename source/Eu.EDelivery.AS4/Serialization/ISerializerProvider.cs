using Eu.EDelivery.AS4.Model.Core;

namespace Eu.EDelivery.AS4.Serialization;

public interface ISerializerProvider
{
    long DetermineMessageSize(AS4Message? message);
    ISerializer Get(string contentType);
}