using Eu.EDelivery.AS4.Entities;
using Eu.EDelivery.AS4.UnitTests.Strategies.Sender;
using Microsoft.EntityFrameworkCore;

namespace Eu.EDelivery.AS4.UnitTests.Receivers;
internal class SaboteurDatastoreContextFactory : IDbContextFactory<DatastoreContext>
{
    public DatastoreContext CreateDbContext() => throw new SaboteurException("Sabotage datastore creation");
}
