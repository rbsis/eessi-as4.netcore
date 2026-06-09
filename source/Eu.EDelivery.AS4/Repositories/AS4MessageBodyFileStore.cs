using Eu.EDelivery.AS4.Model.Core;
using Eu.EDelivery.AS4.Serialization;
using Eu.EDelivery.AS4.Streaming;
using Eu.EDelivery.AS4.Utilities;

namespace Eu.EDelivery.AS4.Repositories;

public class AS4MessageBodyFileStore : IAS4MessageBodyStore
{
    private readonly ISerializerProvider _serializerProvider;

    public AS4MessageBodyFileStore(ISerializerProvider serializerProvider)
    {
        _serializerProvider = serializerProvider;
    }

    /// <summary>
    /// Saves an AS4 Message instance to the filesystem.
    /// </summary>
    /// <remarks>The AS4 Message is being serialized to file.</remarks>
    /// <param name="location"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    public string SaveAS4Message(string location, AS4Message message)
    {
        var storeLocation = EnsureStoreLocation(location);
        var fileName = AssembleUniqueMessageLocation(storeLocation);

        if (!File.Exists(fileName))
        {
            SaveMessageToFile(message, fileName);
        }

        return $"file:///{fileName}";
    }

    /// <summary>
    /// Saves an AS4 Message Stream to the filesystem.
    /// </summary>
    /// <param name="location">The location where the AS4 message must be saved</param>
    /// <param name="as4MessageStream">A stream representing the AS4 message</param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public async Task<string> SaveAS4MessageStreamAsync(string location, Stream as4MessageStream, CancellationToken cancellation)
    {
        var storeLocation = EnsureStoreLocation(location);
        var fileName = AssembleUniqueMessageLocation(storeLocation);

        if (!File.Exists(fileName))
        {
            var sourceFile = GetFileStreamForSourceStream(as4MessageStream);

            if (sourceFile != null)
            {
                File.Copy(sourceFile.Name, fileName);
            }
            else
            {
                using var fs = FileUtils.OpenAsync(fileName, FileMode.Create, FileAccess.Write, FileOptions.SequentialScan);
                File.SetAttributes(fs.Name, FileAttributes.NotContentIndexed);

                await as4MessageStream.CopyToAsync(fs, cancellation);
            }
        }

        return $"file:///{fileName}";
    }

    private static FileStream? GetFileStreamForSourceStream(Stream s)
    {
        if (s is FileStream fs)
        {
            return fs;
        }

        if (s is VirtualStream vs && vs.UnderlyingStream is FileStream ufs)
        {
            return ufs;
        }

        return null;
    }

    private static string EnsureStoreLocation(string storeLocation)
    {
        var location = SubstringWithoutFileUri(storeLocation);

        if (!Directory.Exists(location))
        {
            Directory.CreateDirectory(location);
        }

        return location;
    }

    private static string AssembleUniqueMessageLocation(string storeLocation)
    {
        var fileName = Guid.NewGuid().ToString();

        return Path.Combine(storeLocation, $"{fileName}.as4");
    }

    /// <summary>
    /// Updates an existing file on the file-system with an updated version
    /// of the given AS4 Message instance.
    /// </summary>
    /// <param name="location"></param>
    /// <param name="message"></param>
    public void UpdateAS4Message(string location, AS4Message message)
    {
        ArgumentNullException.ThrowIfNull(location);

        var fileLocation = SubstringWithoutFileUri(location);

        if (!File.Exists(fileLocation))
        {
            throw new FileNotFoundException(
                $"The messagebody that must be updated could not be found at: {fileLocation}.");
        }

        SaveMessageToFile(message, fileLocation);
    }

    private void SaveMessageToFile(AS4Message message, string fileName)
    {
        using var content = File.Create(fileName);
        File.SetAttributes(fileName, FileAttributes.NotContentIndexed);

        var serializer = _serializerProvider.Get(message.ContentType);
        serializer.Serialize(message, content);
    }

    /// <summary>
    /// Loads a <see cref="Stream" /> at a given stored <paramref name="location" />.
    /// </summary>
    /// <param name="location">The location.</param>
    /// <returns></returns>
    /// <param name="cancellation"></param>
    public async Task<Stream?> LoadMessageBodyAsync(string? location, CancellationToken cancellation)
    {
        if (string.IsNullOrEmpty(location))
        {
            return null;
        }

        var fileLocation = SubstringWithoutFileUri(location);
        if (string.IsNullOrEmpty(fileLocation))
        {
            return null;
        }

        if (File.Exists(fileLocation))
        {
            using var fileStream = FileUtils.OpenReadAsync(fileLocation, options: FileOptions.SequentialScan);
            var virtualStream =
                VirtualStream.Create(
                    fileStream.CanSeek ? fileStream.Length : VirtualStream.ThresholdMax,
                    forAsync: true);

            await fileStream.CopyToAsync(virtualStream, cancellation);
            virtualStream.Position = 0;

            return virtualStream;
        }

        return null;
    }

    private static string SubstringWithoutFileUri(string location)
    {
        if (location.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
        {
            return location.Substring("file:///".Length);
        }

        return location;
    }
}
