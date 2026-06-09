using System.Configuration;
using System.Xml.Linq;
using Eu.EDelivery.AS4.Common;
using Eu.EDelivery.AS4.Fe.Hash;
using Eu.EDelivery.AS4.Fe.Pmodes.Model;
using Eu.EDelivery.AS4.Fe.Settings;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Serialization;
using Microsoft.Extensions.Options;

namespace Eu.EDelivery.AS4.Fe.Pmodes;

/// <summary>
/// As4 PMode source
/// </summary>
/// <seealso cref="IAs4PmodeSource" />
public class As4PmodeSource : IAs4PmodeSource
{
    private readonly IConfig _config;
    private readonly PmodeSettings _settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="As4PmodeSource"/> class.
    /// </summary>
    /// <param name="config"></param>
    /// <param name="settings">The settings.</param>
    public As4PmodeSource(IConfig config, IOptions<PmodeSettings> settings)
    {
        _config = config;
        _settings = settings.Value;
    }

    /// <summary>
    /// Gets the receiving names.
    /// </summary>
    /// <returns></returns>
    public Task<IEnumerable<string>> GetReceivingNamesAsync(CancellationToken cancellationToken) => Task.Factory
        .StartNew(() => _config.GetReceivingPModes().Select(p => p.Id), cancellationToken);

    /// <summary>
    /// Gets the name of the receiving by.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<ReceivingBasePmode?> GetReceivingByNameAsync(string name, CancellationToken cancellationToken) => Task.Factory
        .StartNew(() =>
        {
            var pmode = SafeGetReceivingPMode(name);
            if (pmode != null)
            {
                return new ReceivingBasePmode
                {
                    Name = pmode.Id,
                    Type = PmodeType.Receiving,
                    Pmode = pmode,
                    Hash = AS4XmlSerializer.ToString(pmode).GetMd5Hash()
                };
            }

            return null;
        }, cancellationToken);

    private ReceivingProcessingMode? SafeGetReceivingPMode(string id)
    {
        try
        {
            return _config.GetReceivingPMode(id);
        }
        catch (Exception ex) when (ex is KeyNotFoundException || ex is ConfigurationErrorsException)
        {
            return null;
        }
    }

    /// <summary>
    /// Gets the sending names.
    /// </summary>
    /// <returns></returns>
    public Task<IEnumerable<string>> GetSendingNamesAsync(CancellationToken cancellationToken) => Task.Factory
        .StartNew(() => _config.GetSendingPModes().Select(p => p.Id), cancellationToken);

    /// <summary>
    /// Gets the name of the sending by.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<SendingBasePmode?> GetSendingByNameAsync(string name, CancellationToken cancellationToken) => Task.Factory
        .StartNew(() =>
        {
            var pmode = SafeGetSendingPMode(name);
            if (pmode != null)
            {
                return new SendingBasePmode
                {
                    Name = pmode.Id,
                    Type = PmodeType.Sending,
                    Pmode = pmode,
                    Hash = AS4XmlSerializer.ToString(pmode).GetMd5Hash()
                };
            }

            return null;
        }, cancellationToken);

    private SendingProcessingMode? SafeGetSendingPMode(string name)
    {
        try
        {
            return _config.GetSendingPMode(name);
        }
        catch (Exception ex) when (ex is KeyNotFoundException || ex is ConfigurationErrorsException)
        {
            return null;
        }
    }

    /// <summary>
    /// Creates the receiving.
    /// </summary>
    /// <param name="basePmode">The base pmode.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task CreateReceivingAsync(ReceivingBasePmode basePmode, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(basePmode.Name, nameof(basePmode.Name));

        var fileName = FilterOutInvalidFileNameChars(basePmode.Name);
        var pmodeFile = Path.Combine(_settings.ReceivingPmodeFolder, fileName + ".xml");

        if (File.Exists(pmodeFile))
        {
            pmodeFile = Path.Combine(_settings.ReceivingPmodeFolder, fileName + "-" + Guid.NewGuid() + ".xml");
        }

        var pmodeString = await AS4XmlSerializer.ToStringAsync(basePmode.Pmode, cancellationToken);
        File.WriteAllText(pmodeFile, pmodeString);
    }

    /// <summary>
    /// Deletes the receiving.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task DeleteReceivingAsync(string name, CancellationToken cancellationToken) => Task.Factory
        .StartNew(() =>
        {
            var path = _config.GetFileLocationForReceivingPMode(name);
            File.Delete(path);
        }, cancellationToken);

    /// <summary>
    /// Deletes the sending.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task DeleteSendingAsync(string name, CancellationToken cancellationToken) => Task.Factory
        .StartNew(() =>
        {
            var path = _config.GetFileLocationForSendingPMode(name);
            File.Delete(path);
        }, cancellationToken);

    /// <summary>
    /// Creates the sending.
    /// </summary>
    /// <param name="basePmode">The base pmode.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task CreateSendingAsync(SendingBasePmode basePmode, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(basePmode.Name, nameof(basePmode.Name));

        var fileName = FilterOutInvalidFileNameChars(basePmode.Name);
        var pmodeFile = Path.Combine(_settings.SendingPmodeFolder, fileName + ".xml");

        if (File.Exists(pmodeFile))
        {
            pmodeFile = Path.Combine(_settings.SendingPmodeFolder, fileName + "-" + Guid.NewGuid() + ".xml");
        }

        var pmodeString = await AS4XmlSerializer.ToStringAsync(basePmode.Pmode, cancellationToken);
        File.WriteAllText(pmodeFile, pmodeString);
    }

    private static string FilterOutInvalidFileNameChars(string basePmodeName) => Path
        .GetInvalidFileNameChars()
        .Aggregate(basePmodeName, (acc, c) => acc.Replace(c.ToString(), string.Empty));

    /// <summary>
    /// Gets the pmode number.
    /// </summary>
    /// <param name="pmodeString">The pmode string.</param>
    /// <returns></returns>
    public string? GetPmodeNumber(string pmodeString) =>
        XDocument.Parse(pmodeString).Root?.Descendants().FirstOrDefault(x => x.Name.LocalName == "Id")?.Value;

    /// <summary>
    /// Updates the sending.
    /// </summary>
    /// <param name="basePmode">The base pmode.</param>
    /// <param name="originalName">Name of the original.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task UpdateSendingAsync(SendingBasePmode basePmode, string originalName, CancellationToken cancellationToken)
    {
        await CreateSendingAsync(basePmode, cancellationToken);
        File.Delete(_config.GetFileLocationForSendingPMode(originalName));
    }

    /// <summary>
    /// Updates the receiving.
    /// </summary>
    /// <param name="basePmode">The base pmode.</param>
    /// <param name="originalName">Name of the original.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task UpdateReceivingAsync(ReceivingBasePmode basePmode, string originalName, CancellationToken cancellationToken)
    {
        await CreateReceivingAsync(basePmode, cancellationToken);
        File.Delete(_config.GetFileLocationForReceivingPMode(originalName));
    }
}
