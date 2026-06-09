namespace Eu.EDelivery.AS4.Fe.Settings;

public class ApplicationSettings
{
    public bool ShowStackTraceInExceptions { get; set; }
    public Dictionary<string, string>? Modules { get; set; }
    public required string SettingsXml { get; set; }
    public required string Runtime { get; set; }
}
