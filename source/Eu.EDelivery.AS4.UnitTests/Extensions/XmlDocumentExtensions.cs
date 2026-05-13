using System.Xml;

namespace Eu.EDelivery.AS4.UnitTests.Extensions;

/// <summary>
/// Extensions for the <see cref="XmlDocument"/>.
/// </summary>
public static class XmlDocumentExtensions
{
    private static readonly XmlNamespaceManager _namespaceManager = new(new NameTable());

    /// <summary>
    /// Initializes static members of the <see cref="XmlDocumentExtensions"/> class.
    /// </summary>
    static XmlDocumentExtensions()
    {
        _namespaceManager.AddNamespace("s12", Constants.Namespaces.Soap12);
        _namespaceManager.AddNamespace("eb", Constants.Namespaces.EbmsXmlCore);
        _namespaceManager.AddNamespace("ebbp", Constants.Namespaces.EbmsXmlSignals);
        _namespaceManager.AddNamespace("mh", Constants.Namespaces.EbmsMultiHop);
        _namespaceManager.AddNamespace("wsa", Constants.Namespaces.Addressing);
        _namespaceManager.AddNamespace("wsse", Constants.Namespaces.WssSecuritySecExt);
        _namespaceManager.AddNamespace("wsu", Constants.Namespaces.WssSecurityUtility);
        _namespaceManager.AddNamespace("dsig", Constants.Namespaces.XmlDsig);
    }

    /// <summary>
    /// Selects the XML node.
    /// </summary>
    /// <param name="xmlDocument">The XML document.</param>
    /// <param name="xpath">The xpath.</param>
    /// <returns></returns>
    public static XmlNode? UnsafeSelectEbmsNode(this XmlDocument xmlDocument, string xpath)
    {
        return xmlDocument.SelectSingleNode(xpath, _namespaceManager);
    }

    /// <summary>
    /// Selects the XML node.
    /// </summary>
    /// <param name="node"></param>
    /// <param name="xpath"></param>
    /// <returns></returns>
    public static XmlNode? UnsafeSelectEbmsNode(this XmlNode node, string xpath)
    {
        return node.SelectSingleNode(xpath, _namespaceManager);
    }

    /// <summary>
    /// Asserts on the presence of a XPath query selection on the specified <paramref name="node"/>.
    /// </summary>
    /// <param name="node"></param>
    /// <param name="xpath"></param>
    /// <returns></returns>
    public static XmlNodeList SelectEbmsNodes(this XmlNode node, string xpath)
    {
        var result = node.SelectNodes(xpath, _namespaceManager);
        Assert.True(
            result != null,
            $"XPath query: \n\n {xpath} \n\n doesn't have a result on: \n\n {node.OuterXml}");

        return result;
    }

    /// <summary>
    /// Asserts on the presence of a XPath query selection on the specified <paramref name="node"/>.
    /// </summary>
    /// <param name="node"></param>
    /// <param name="xpath"></param>
    /// <returns></returns>
    public static XmlNode SelectEbmsNode(this XmlNode node, string xpath)
    {
        var result = UnsafeSelectEbmsNode(node, xpath);
        Assert.True(
            result != null,
            $"XPath query: \n\n {xpath} \n\n doesn't have a result on: \n\n {node.OuterXml}");

        return result;
    }

    /// <summary>
    /// Asserts of the presence of a specified <paramref name="name"/> and <paramref name="value"/> in the XML attribute.
    /// </summary>
    /// <param name="a"></param>
    /// <param name="name"></param>
    /// <param name="value"></param>
    public static void AssertEbmsAttribute(this XmlAttribute a, string name, string value)
    {
        Assert.Equal(name, a.Name);
        Assert.Equal(value, a.Value);
    }
}
