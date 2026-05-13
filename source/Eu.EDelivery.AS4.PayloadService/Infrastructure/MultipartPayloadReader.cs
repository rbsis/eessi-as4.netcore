using Eu.EDelivery.AS4.PayloadService.Models;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

namespace Eu.EDelivery.AS4.PayloadService.Infrastructure;

internal class MultipartPayloadReader : IDisposable
{
    private readonly Stream _contentStream;
    private readonly string _contentType;
    private bool _disposedValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="MultipartPayloadReader"/> class.
    /// </summary>
    private MultipartPayloadReader(Stream content, string contentType)
    {
        _contentStream = content;
        _contentType = contentType;
    }

    /// <summary>
    /// Try to create a <see cref="MultipartPayloadReader"/> instance.
    /// </summary>
    /// <param name="contentStream"></param>
    /// <param name="contentType"></param>
    /// <returns></returns>
    public static (bool success, MultipartPayloadReader? reader) TryCreate(Stream contentStream, string? contentType)
    {
        if (string.IsNullOrEmpty(contentType) || !IsMultipartContentType(contentType))
        {
            return (false, null);
        }

        return (true, new MultipartPayloadReader(contentStream, contentType));
    }

    private static bool IsMultipartContentType(string contentType) =>
        contentType.Contains("multipart/", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Start reading the HTTP Request
    /// </summary>
    /// <param name="onNextSection"></param>
    /// <returns></returns>
    public async Task StartReading(Func<Payload, Task> onNextSection)
    {
        var multipartReader = new MultipartReader(boundary: GetBoundary(_contentType), stream: _contentStream);
        var section = await multipartReader.ReadNextSectionAsync();

        while (section != null)
        {
            var fileName = GetFileContentDisposition(section);
            if (!string.IsNullOrEmpty(fileName))
            {
                await onNextSection(new Payload(section.Body, new PayloadMeta(fileName)));
            }

            section = await multipartReader.ReadNextSectionAsync();
        }
    }

    private static string GetBoundary(string contentType)
    {
        var boundary = HeaderUtilities.RemoveQuotes(MediaTypeHeaderValue.Parse(contentType).Boundary);

        if (string.IsNullOrWhiteSpace(boundary.Value))
        {
            throw new InvalidDataException("Missing content-type boundary.");
        }

        if (boundary.Length > 100)
        {
            throw new InvalidDataException($"Multipart boundary length limit {100} exceeded.");
        }

        return boundary.Value;
    }

    private static string? GetFileContentDisposition(MultipartSection? section)
    {
        if (section is null || !ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var contentDisposition))
        {
            return null;
        }

        if (!contentDisposition.DispositionType.Equals("form-data"))
        {
            return null;
        }

        if (!string.IsNullOrEmpty(contentDisposition.FileName.Value))
        {
            return contentDisposition.FileName.Value;
        }

        return contentDisposition.FileNameStar.Value;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _contentStream?.Dispose();
            }

            _disposedValue = true;
        }
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
