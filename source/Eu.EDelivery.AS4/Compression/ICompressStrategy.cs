using Eu.EDelivery.AS4.Model.Core;

namespace Eu.EDelivery.AS4.Compression;

public interface ICompressStrategy
{
    /// <summary>
    /// Compresses the Attachments that are part of this AS4 Message and
    /// modifies the Payload-info in the UserMessage to indicate that the attachment 
    /// is compressed.
    /// </summary>
    void CompressAttachments(AS4Message message);

    /// <summary>
    /// Decompresses the Attachments that are part of this AS4 Message.
    /// </summary>
    void DecompressAttachments(AS4Message message);
}
