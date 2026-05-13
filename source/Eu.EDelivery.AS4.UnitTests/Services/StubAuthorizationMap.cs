using Eu.EDelivery.AS4.Services.PullRequestAuthorization;

namespace Eu.EDelivery.AS4.UnitTests.Services;

internal class StubAuthorizationMapProvider : IPullAuthorizationMapProvider
{
    private readonly IEnumerable<PullRequestAuthorizationEntry> _authorizationEntries;

    /// <summary>
    /// Initializes a new instance of the <see cref="StubAuthorizationMapProvider"/> class.
    /// </summary>
    public StubAuthorizationMapProvider(IEnumerable<PullRequestAuthorizationEntry> entries)
    {
        _authorizationEntries = entries;
    }

    public IEnumerable<PullRequestAuthorizationEntry> RetrievePullRequestAuthorizationEntriesForMpc(string mpc) => [.. _authorizationEntries.Where(e => e.Mpc == mpc)];

    public IEnumerable<PullRequestAuthorizationEntry> GetPullRequestAuthorizationEntryOverview() => _authorizationEntries;

    public void SavePullRequestAuthorizationEntries(IEnumerable<PullRequestAuthorizationEntry> authorizationEntries)
    {
    }
}
