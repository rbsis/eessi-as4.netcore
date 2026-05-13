using System.Collections.Concurrent;
using System.Runtime.Caching;
using System.Xml.Serialization;
using Eu.EDelivery.AS4.Common;
using Eu.EDelivery.AS4.Model.PMode;
using Eu.EDelivery.AS4.Validators;
using FluentValidation;
using Microsoft.Extensions.Logging;
using static Eu.EDelivery.AS4.Properties.Resources;

namespace Eu.EDelivery.AS4.Watchers;

/// <summary>
/// Watcher to check if there's a new <see cref="SendingProcessingMode"/>/<see cref="ReceivingProcessingMode"/> available
/// </summary>
/// <typeparam name="T">PMode type that's either a <see cref="SendingProcessingMode"/> or a <see cref="ReceivingProcessingMode"/></typeparam>
/// TODO: moves the initial pmode loading to a factory method instead of overloading the ctor of this type.
internal class PModeWatcher<T> : IPModeWatcher<T> where T : class, IPMode
{
    private readonly ConcurrentDictionary<string, ConfiguredPMode> _pmodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _filePModeIdMap = new();

    private readonly FileSystemWatcher _watcher;

    private readonly ILogger<PModeWatcher<T>> _logger;
    private readonly IValidator<T> _pmodeValidator;

    private static readonly XmlSerializer _xmlSerializer = new(typeof(T));

    private static readonly string _pModeName = typeof(T).Name switch
    {
        nameof(SendingProcessingMode) => "SendingPMode",
        nameof(ReceivingProcessingMode) => "ReceivingPMode",
        _ => "PMode"
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="PModeWatcher{T}" /> class
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="validator">The validator to use when retrieving <see cref="IPMode"/> implementations.</param>
    public PModeWatcher(
        ILogger<PModeWatcher<T>> logger,
        IValidator<T> validator)
    {
        _logger = logger;
        _pmodeValidator = validator;

        var path = Path.Combine(Config.ApplicationPath, configurationfolder, sendpmodefolder);
        _watcher = new FileSystemWatcher(path, "*.xml") { IncludeSubdirectories = true };
        _watcher.Changed += OnChanged;
        _watcher.Created += OnCreated;
        _watcher.Deleted += OnDeleted;
        _watcher.NotifyFilter =
            NotifyFilters.LastAccess
            | NotifyFilters.LastWrite
            | NotifyFilters.FileName
            | NotifyFilters.DirectoryName;

        RetrievePModes(_watcher.Path);
    }

    /// <summary>
    /// Start watching for pmodes.
    /// </summary>
    public void Start() => _watcher.EnableRaisingEvents = true;

    /// <summary>
    /// Stop watching for pmodes
    /// </summary>
    public void Stop() => _watcher.EnableRaisingEvents = false;

    /// <summary>
    /// Verify if the Watcher contains a <see cref="IPMode"/> for a given <paramref name="id"/>.
    /// </summary>
    /// <param name="id">Id for which the verification is done.</param>
    /// <returns>A value indicating whether or not there exists a <see cref="IPMode"/> for this <paramref name="id"/>.</returns>
    public bool ContainsPMode(string id) => _pmodes.ContainsKey(id);

    /// <summary>
    /// Gets the <see cref="ConfiguredPMode"/> entry for a given <paramref name="key"/>.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <exception cref="ArgumentException">The specified PMode key is invalid. - key</exception>
    public ConfiguredPMode? GetPModeEntry(string key)
    {
        _pmodes.TryGetValue(key, out var configuredPMode);
        return configuredPMode;
    }

    /// <summary>
    /// Gets the p modes cached inside the watcher.
    /// </summary>
    /// <returns></returns>
    public IEnumerable<IPMode> GetPModes() => _pmodes.Values.Select(p => p.PMode);

    private void RetrievePModes(string pmodeFolder)
    {
        var startDir = new DirectoryInfo(pmodeFolder);
        var files = TryGetFiles(startDir);

        foreach (var file in files)
        {
            AddOrUpdateConfiguredPMode(file.FullName);
        }
    }

    private IEnumerable<FileInfo> TryGetFiles(DirectoryInfo startDir)
    {
        try
        {
            return startDir.GetFiles("*.xml", SearchOption.AllDirectories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occured while trying to get {PModeName} files: {Message}", _pModeName, ex.Message);
            return [];
        }
    }

    private void OnCreated(object sender, FileSystemEventArgs e)
    {
        AddOrUpdateConfiguredPMode(Path.GetFullPath(e.FullPath));
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        AddOrUpdateConfiguredPMode(Path.GetFullPath(e.FullPath));
    }

    private void OnDeleted(object sender, FileSystemEventArgs e)
    {
        var key = _pmodes.FirstOrDefault(p => p.Value.Filename.Equals(e.FullPath)).Key;

        if (key != null)
        {
            _logger.LogTrace("Remove {PModeName} with Id: {Key}", _pModeName, key);
            _pmodes.TryRemove(key, out _);
        }
    }

    private readonly object __cacheLock = new();

    private void AddOrUpdateConfiguredPMode(string fullPath)
    {
        lock (__cacheLock)
        {
            if (_fileEventCache.Contains(fullPath))
            {
                _logger.LogTrace("{PModeName} {FullPath} has already been handled.", _pModeName, fullPath);
                return;
            }

            _fileEventCache.Add(fullPath, fullPath, _policy);
        }

        var pmode = TryDeserialize(fullPath);
        if (pmode == null)
        {
            _logger.LogWarning("File at: \'{FullPath}\' cannot be converted to a {PModeName} because the XML in the file isn\'t valid.", fullPath, _pModeName);

            // Since the PMode that we expect in this file is invalid, it
            // must be removed from our cache.
            RemoveLocalPModeFromCache(fullPath);
            return;
        }

        var pmodeValidation = _pmodeValidator.Validate(pmode);
        if (!pmodeValidation.IsValid)
        {
            _logger.LogWarning("Invalid {PModeName} at: \'{FullPath}\'", _pModeName, fullPath);
            foreach (var errorMessage in pmodeValidation.GetValidationErrors())
            {
                _logger.LogError(errorMessage);
            }

            // Since the PMode that we expect isn't valid according to the validator, it
            // must be removed from our cache.
            RemoveLocalPModeFromCache(fullPath);
            return;
        }

        var configuredPMode = new ConfiguredPMode(fullPath, pmode);

        if (_pmodes.ContainsKey(pmode.Id))
        {
            _logger.LogWarning("Existing PMode {PModeId} will be overwritten with PMode from {FullPath}", pmode.Id, fullPath);
        }
        else
        {
            _logger.LogTrace("Add new {PModeName} with Id: {PModeId}", _pModeName, pmode.Id);
        }

        _pmodes.AddOrUpdate(pmode.Id, configuredPMode, (key, value) => configuredPMode);
        _filePModeIdMap.AddOrUpdate(fullPath, pmode.Id, (key, value) => pmode.Id);
    }

    //// cache which keeps track of the date and time a PMode file was last handled by the FileSystemWatcher.
    //// Due to an issue with FileSystemWatcher, events can be triggered multiple times for the same operation on the 
    //// same file.

    private readonly MemoryCache _fileEventCache = MemoryCache.Default;

    private readonly CacheItemPolicy _policy = new() { SlidingExpiration = TimeSpan.FromMilliseconds(500) };

    private T? TryDeserialize(string path)
    {
        try
        {
            var retryCount = 0;
            while (IsFileLocked(path) && retryCount < 10)
            {
                // Wait till the file lock is released ...
                System.Threading.Thread.Sleep(50);
                retryCount++;
            }

            void UnknownElement(object? sender, XmlElementEventArgs e) => OnUnknownXmlElement(e, path);

            using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
            _xmlSerializer.UnknownElement += UnknownElement;
            var result = _xmlSerializer.Deserialize(fileStream) as T;
            _xmlSerializer.UnknownElement -= UnknownElement;

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occured while deserializing {PModeName} at {Path}", _pModeName, path);

            return null;
        }
    }

    private void OnUnknownXmlElement(XmlElementEventArgs e, string path)
    {
        if (e.Element.LocalName == "SendingPMode"
            && e.ObjectBeingDeserialized is ReplyHandling)
        {
            var message = "ReceivingPMode at {Path} still has a ReplyHandling.SendingPMode element." + Environment.NewLine
                + "SendingPModes are not used anymore for responding to AS4 messages. "
                + "Please upgrade your PMode by executing the script ./scripts/copy-responsepmode-to-receivingpmode.ps1." + Environment.NewLine
                + "For more information see the wiki section: \"Remove Sending PMode as responding PMode\"";
            _logger.LogWarning(message, path);
        }
        else
        {
            var message = "Unknown XML element found while deserializing the {PModeName} -> {LocalName} "
                + "at {Path} ({LineNumber},{LinePosition})." + Environment.NewLine
                + "Expected elements:" + Environment.NewLine
                + $" - {e.ExpectedElements.Replace(", ", Environment.NewLine + " - ")}";
            _logger.LogWarning(message,
                _pModeName,
                e.Element.LocalName,
                path,
                e.LineNumber,
                e.LinePosition);
        }
    }


    private void RemoveLocalPModeFromCache(string fullPath)
    {
        if (_filePModeIdMap.TryGetValue(fullPath, out var pmodeId))
        {
            _pmodes.TryRemove(pmodeId, out _);
            _filePModeIdMap.TryRemove(fullPath, out _);
        }
    }

    private static bool IsFileLocked(string path)
    {
        try
        {
            using (File.Open(path, FileMode.Open, FileAccess.Read))
            {
                return false;
            }
        }
        catch (IOException)
        {
            return true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _pmodes.Clear();
            _filePModeIdMap.Clear();
            _watcher?.Dispose();
        }
    }

}
