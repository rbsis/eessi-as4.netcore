namespace Eu.EDelivery.AS4.Fe.Mappers;

public interface IMapper<in TSource, out TDestination>
{
    TDestination Map(TSource source);
}
