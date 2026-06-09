using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.Extensions.Options;

namespace Eu.EDelivery.AS4.Fe.Settings;

public class FileSettingsSource : ISettingsSource
{
    private readonly string _settingsPath;

    private static readonly XmlWriterSettings DefaultXmlWriterSettings = new()
    {
        Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        Indent = true,
    };

    public FileSettingsSource(IOptions<ApplicationSettings> appSettings)
    {
        ArgumentException.ThrowIfNullOrEmpty(appSettings.Value.SettingsXml, nameof(appSettings.Value.SettingsXml));

        _settingsPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            appSettings.Value.SettingsXml);
    }

    public Task<Model.Internal.Settings?> GetAsync(CancellationToken cancellationToken) => Task.Factory
        .StartNew(() =>
        {
            using var reader = new FileStream(_settingsPath, FileMode.Open);
            var xml = new XmlSerializer(typeof(Model.Internal.Settings));
            return xml.Deserialize(reader) as Model.Internal.Settings;
        }, cancellationToken);

    public Task SaveAsync(Model.Internal.Settings settings, CancellationToken cancellationToken) => Task.Factory
        .StartNew(() =>
        {
            var xmlSerializer = new XmlSerializer(typeof(Model.Internal.Settings));
            using var output = new FileStream(_settingsPath, FileMode.Create);
            using var xmlWriter = XmlWriter.Create(output, DefaultXmlWriterSettings);
            xmlSerializer.Serialize(xmlWriter, settings);
        }, cancellationToken);
}
