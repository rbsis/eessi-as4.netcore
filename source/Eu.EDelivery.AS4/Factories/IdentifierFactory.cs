using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Eu.EDelivery.AS4.Common;

namespace Eu.EDelivery.AS4.Factories;

/// <summary>
/// Factory to create Entity ID's
/// </summary>
public partial class IdentifierFactory : IIdentifierFactory
{
    private const string DefaultIdFormat = "{GUID}@{IPADDRESS}";

    private static readonly Regex _macroMatchRegex = MyRegex();
    private static readonly Dictionary<string, Func<string?>> _macros = new()
    {
        ["GUID"] = () => Guid.NewGuid().ToString(),
        ["MACHINENAME"] = Dns.GetHostName,
        ["IPADDRESS"] = GetHostIpAddress
    };

    private readonly IConfig _config;

    public IdentifierFactory(IConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// Generate ID with default format
    /// </summary>
    /// <returns></returns>
    public string Create()
    {
        if (!_config.IsInitialized)
        {
            return Create(DefaultIdFormat);
        }

        var defaultFormat = _config.EbmsMessageIdFormat;
        if (!string.IsNullOrEmpty(defaultFormat))
        {
            return Create(defaultFormat);
        }

        return Create(DefaultIdFormat);
    }

    /// <summary>
    /// Generate ID with given format
    /// </summary>
    /// <param name="idFormat"></param>
    /// <returns></returns>
    public static string Create(string idFormat)
    {
        if (idFormat.Length == 0)
        {
            throw new ArgumentException(@"idFormat is invalid.", nameof(idFormat));
        }

        var idBuilder = new StringBuilder(idFormat);

        idBuilder = _macroMatchRegex.Matches(idFormat).Cast<Match>()
            .Aggregate(idBuilder, ReplaceValueWithMacro);

        return idBuilder.ToString();
    }

    private static string? GetHostIpAddress()
    {
        var hostName = Dns.GetHostName();

        return Dns.GetHostEntry(hostName).AddressList
            .FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork)?
            .ToString();
    }

    private static StringBuilder ReplaceValueWithMacro(StringBuilder idBuilder, Match match)
    {
        var valueToReplace = match.Groups[0].Value;
        var macroName = match.Groups[1].Value;

        if (_macros.TryGetValue(macroName, out var value))
        {
            idBuilder = idBuilder.Replace(valueToReplace, value());
        }

        return idBuilder;
    }

    [GeneratedRegex(@"\{([^\}]+)\}")]
    private static partial Regex MyRegex();
}
