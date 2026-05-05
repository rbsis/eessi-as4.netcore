using System.Security.Cryptography.Xml;
using System.Xml;

namespace Eu.EDelivery.AS4.Security.Transforms;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S1075:URIs should not be hardcoded", Justification = "<Pending>")]
public class AttachmentCiphertextTransform : Transform
{
    public const string Url = "http://docs.oasis-open.org/wss/oasis-wss-SwAProfile-1.1#Attachment-Ciphertext-Transform";

    private Stream? _inputStream;

    public override Type[] InputTypes { get; } = [typeof(Stream)];
    public override Type[] OutputTypes { get; } = [typeof(Stream)];

    /// <summary>
    /// Initializes a new instance of the <see cref="AttachmentCiphertextTransform"/> class
    /// </summary>
    public AttachmentCiphertextTransform()
    {
        Algorithm = Url;
    }

    public override void LoadInnerXml(XmlNodeList nodeList) { }

    protected override XmlNodeList? GetInnerXml() => null;

    public override void LoadInput(object obj)
    {
        _inputStream = obj as Stream;
    }

    public override object GetOutput() => _inputStream ?? throw new InvalidOperationException("InputStream not set");

    public override object GetOutput(Type type) => _inputStream ?? throw new InvalidOperationException("InputStream not set");
}
