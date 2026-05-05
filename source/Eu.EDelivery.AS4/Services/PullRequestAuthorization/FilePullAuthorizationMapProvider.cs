using System.Xml.Serialization;

namespace Eu.EDelivery.AS4.Services.PullRequestAuthorization;

public class FilePullAuthorizationMapProvider : IPullAuthorizationMapProvider
{
    // TODO: use a FileSystemWatcher to determine if the file needs to be reloaded.

    private readonly string _authorizationMapFile;

    private bool _authorizationMapChanged;
    private IEnumerable<PullRequestAuthorizationEntry>? _entries;

    /// <summary>
    /// Initializes a new instance of the <see cref="FilePullAuthorizationMapProvider"/> class.
    /// </summary>
    public FilePullAuthorizationMapProvider(string authorizationMapFile)
    {
        _authorizationMapFile = authorizationMapFile;
    }

    public IEnumerable<PullRequestAuthorizationEntry> GetPullRequestAuthorizationEntryOverview()
    {
        RefreshCacheIfNecessary();

        return [.. _entries!];
    }

    public IEnumerable<PullRequestAuthorizationEntry> RetrievePullRequestAuthorizationEntriesForMpc(string mpc)
    {
        RefreshCacheIfNecessary();

        return [.. _entries!.Where(e => StringComparer.InvariantCulture.Equals(e.Mpc, mpc))];
    }

    private void RefreshCacheIfNecessary()
    {
        if (_authorizationMapChanged || _entries == null)
        {
            _entries = RetrievePullRequestEntriesFromFile(_authorizationMapFile);
            _authorizationMapChanged = false;
        }
    }

    public void SavePullRequestAuthorizationEntries(IEnumerable<PullRequestAuthorizationEntry> authorizationEntries)
    {
        var entries = new List<AuthorizationEntry>();

        foreach (var entry in authorizationEntries)
        {
            entries.Add(new AuthorizationEntry { Mpc = entry.Mpc, CertificateThumbPrint = entry.CertificateThumbprint, Allowed = entry.Allowed });
        }

        var map = new PullRequestAuthorizationMap { AuthorizationEntries = entries.ToArray() };

        using var fs = new FileStream(_authorizationMapFile, FileMode.Create, FileAccess.Write);
        var s = new XmlSerializer(typeof(PullRequestAuthorizationMap));
        s.Serialize(fs, map);
    }

    private static IEnumerable<PullRequestAuthorizationEntry> RetrievePullRequestEntriesFromFile(string fileName)
    {
        if (!File.Exists(fileName))
        {
            yield break;
        }

        using var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read);
        var s = new XmlSerializer(typeof(PullRequestAuthorizationMap));
        var map = (PullRequestAuthorizationMap?)s.Deserialize(fs);
        if (map == null)
        {
            yield break;
        }

        foreach (var entry in map.AuthorizationEntries)
        {
            yield return new PullRequestAuthorizationEntry(entry.Mpc, entry.CertificateThumbPrint, entry.Allowed);
        }
    }

    #region Inner classes for xml - serialization

    [Serializable]
    [XmlType(AnonymousType = true, Namespace = "eu:edelivery:as4")]
    [XmlRoot("PullRequestAuthorizationMap")]
    public class PullRequestAuthorizationMap
    {
        [XmlArray("AuthorizationEntries")]
        [XmlArrayItem("Authorization")]
        public required AuthorizationEntry[] AuthorizationEntries { get; set; }
    }

    [Serializable]
    [XmlType(AnonymousType = true, Namespace = "eu:edelivery:as4")]
    [XmlRoot("Authorization")]
    public class AuthorizationEntry
    {
        [XmlAttribute(AttributeName = "mpc")]
        public required string Mpc { get; set; }
        [XmlAttribute(AttributeName = "certificatethumbprint")]
        public required string CertificateThumbPrint { get; set; }
        [XmlAttribute(AttributeName = "allowed")]
        public bool Allowed { get; set; }
    }

    #endregion
}
