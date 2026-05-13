using Eu.EDelivery.AS4.Services.PullRequestAuthorization;

namespace Eu.EDelivery.AS4.UnitTests.Services;

public class GivenFileAuthorizationMapProviderFacts
{
    [Fact]
    public void CanRetrieveAuthorizationEntriesForMpc()
    {
        // Arrange
        var entries = new[]
        {
            new PullRequestAuthorizationEntry("mpc1", "abcdefgh", true),
            new PullRequestAuthorizationEntry("mpc2", "ijklmnop", false)
        };

        var fileName = GenerateAuthorizationMapFile(entries);

        try
        {
            var provider = new FilePullAuthorizationMapProvider(fileName);

            var retrievedEntries = provider.RetrievePullRequestAuthorizationEntriesForMpc("mpc1");

            Assert.NotNull(retrievedEntries);
            Assert.Single(retrievedEntries);
            Assert.Equal(entries.First(e => e.Mpc == "mpc1"), retrievedEntries.First());
        }
        finally
        {
            File.Delete(fileName);
        }
    }

    private static string GenerateAuthorizationMapFile(PullRequestAuthorizationEntry[] entries)
    {
        var fileName = Path.GetTempFileName();

        var provider = new FilePullAuthorizationMapProvider(fileName);

        provider.SavePullRequestAuthorizationEntries(entries);

        return fileName;
    }
}
