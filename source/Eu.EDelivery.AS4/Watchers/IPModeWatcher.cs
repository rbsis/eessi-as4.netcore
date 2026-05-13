using Eu.EDelivery.AS4.Model.PMode;

namespace Eu.EDelivery.AS4.Watchers;

public interface IPModeWatcher<T> : IDisposable where T : class, IPMode
{
    bool ContainsPMode(string id);
    ConfiguredPMode? GetPModeEntry(string key);
    IEnumerable<IPMode> GetPModes();
    void Start();
    void Stop();
}
