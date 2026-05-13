using MimeKit.IO;

namespace Eu.EDelivery.AS4.Streaming;

public static class StreamUtilities
{
    /// <summary>
    /// Resets the Position of the given Stream to 0.
    /// </summary>
    /// <remarks>This method takes care of special streams like NonCloseableStream and FilteredStream instances
    /// which are not seekable and thus cannot simply reset their Position to 0.</remarks>
    /// <param name="stream"></param>
    public static void MovePositionToStreamStart(this Stream stream)
    {
        var streamToWorkOn = stream.GetStreamToWorkOn();
        if (!streamToWorkOn.CanSeek)
        {
            throw new InvalidOperationException("Unable to reset the Stream Position.  Stream is not seekable.");
        }

        if (streamToWorkOn.Position != 0)
        {
            streamToWorkOn.Position = 0;
        }
    }

    /// <summary>
    /// Tries to determine the length of the stream.
    /// </summary>
    /// <param name="stream"></param>
    /// <remarks>If the specified <paramref name="stream"/> is not seekable, -1 is returned.</remarks>
    /// <returns></returns>
    public static long GetStreamSize(this Stream stream)
    {
        var streamToWorkOn = stream.GetStreamToWorkOn();
        if (!streamToWorkOn.CanSeek)
        {
            return -1;
        }

        return streamToWorkOn.Length;
    }

    private static Stream GetStreamToWorkOn(this Stream stream)
    {
        var streamToWorkOn = stream;

        if (streamToWorkOn is NonCloseableStream ncs)
        {
            streamToWorkOn = ncs.InnerStream;
        }
        if (streamToWorkOn is FilteredStream fs)
        {
            streamToWorkOn = fs.Source;
        }

        return streamToWorkOn;
    }
}
