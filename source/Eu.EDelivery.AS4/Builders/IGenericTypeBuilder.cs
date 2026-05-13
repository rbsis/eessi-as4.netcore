namespace Eu.EDelivery.AS4.Builders;

public interface IGenericTypeBuilder
{
    T Build<T>(string typeString) where T : class;
    T Build<T>(string typeString, params object?[] args) where T : class;
    bool CanResolveTypeThatImplements<T>(string? typeString);
}
