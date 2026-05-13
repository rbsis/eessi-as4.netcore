using System.Security.Cryptography.Xml;
using System.Text.RegularExpressions;
using System.Xml;

namespace Eu.EDelivery.AS4.Security.Transforms;

/// <summary>
/// Transformation to Sign the Attachments
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S1075:URIs should not be hardcoded", Justification = "<Pending>")]
public class AttachmentSignatureTransform : Transform
{
    public const string Url = "http://docs.oasis-open.org/wss/oasis-wss-SwAProfile-1.1" + "#Attachment-Content-Signature-Transform";

    private readonly Type[] _inputTypes = [typeof(Stream)];
    private readonly Type[] _outputTypes = [typeof(Stream)];
    private Stream? _inputStream;

    /// <summary>
    /// Gets an array of types that are valid inputs to the LoadInput(System.Object) method.
    /// </summary>
    public override Type[] InputTypes => _inputTypes;

    /// <summary>
    /// Gets an array of types that are possible outputs from the GetOutput methods.
    /// </summary>
    public override Type[] OutputTypes => _outputTypes;

    /// <summary>
    /// Content Type of the Transformed Attachment to determine if the Attachment is a .xml Document
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AttachmentSignatureTransform"/> class. 
    /// Creates an instance with no known content type.
    /// </summary>
    public AttachmentSignatureTransform()
    {
        Algorithm = Url;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AttachmentSignatureTransform"/> class. 
    /// Creates an instance with a specified MIME content type.
    /// </summary>
    /// <param name="contentType">
    /// </param>
    public AttachmentSignatureTransform(string contentType) : this()
    {
        ContentType = contentType;
    }

    /// <summary>
    /// Returns the output of the current transform.
    /// </summary>
    /// <returns></returns>
    public override object GetOutput() => GetOutput(typeof(Stream));

    /// <summary>
    /// Returns the output of the current transform.
    /// </summary>
    /// <param name="type"></param>
    /// <exception cref="ArgumentException"></exception>
    /// <returns></returns>
    public override object GetOutput(Type type)
    {
        if (!_outputTypes.Contains(type))
        {
            throw new ArgumentException(@"Output object is not of a supported type", nameof(type));
        }

        return IsXmlContentType(ContentType)
            ? UseXmlDocumentTransform(type)
            : _inputStream ?? throw new InvalidOperationException("InputStream not set");
    }

    private static bool IsXmlContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return false;

        if (contentType.StartsWith("text/xml", StringComparison.InvariantCultureIgnoreCase))
            return true;

        return contentType.StartsWith("application/xml", StringComparison.InvariantCultureIgnoreCase) ||
               Regex.IsMatch(contentType, "(application|image)/.*\\+xml.*", RegexOptions.IgnoreCase);
    }

    private object UseXmlDocumentTransform(Type type)
    {
        if (_inputStream == null)
        {
            throw new InvalidOperationException("InputStream not set");
        }

        var innerTransform = new XmlDsigExcC14NTransform();
        innerTransform.LoadInput(_inputStream);

        return innerTransform.GetOutput(type);
    }

    /// <summary>
    /// Parses the specified System.Xml.XmlNodeList object as transform-specific content of a
    /// <Transform /> element and configures the internal state of the
    /// current System.Security.Cryptography.Xml.Transform object to match the <Transform /> element.
    /// </summary>
    /// <param name="nodeList"></param>
    public override void LoadInnerXml(XmlNodeList nodeList)
    {
    }

    /// <summary>
    /// Loads the specified input into the current System.Security.Cryptography.Xml.Transform object.
    /// </summary>
    /// <param name="obj"></param>
    /// <exception cref="ArgumentException"></exception>
    public override void LoadInput(object obj)
    {
        if (obj is not Stream streamObject)
            throw new ArgumentException(@"Input object is not of a supported type", nameof(obj));

        _inputStream = streamObject;
    }

    /// <summary>
    /// Returns an XML representation of the parameters of the System.Security.Cryptography.Xml.Transform object that are
    /// suitable to be included as sub elements of an XMLDSIG <Transform /> element.
    /// </summary>
    /// <returns></returns>
    protected override XmlNodeList? GetInnerXml() => null;
}
