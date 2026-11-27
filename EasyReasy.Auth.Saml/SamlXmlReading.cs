using System.Xml;

namespace EasyReasy.Auth.Saml
{
    /// <summary>
    /// How the package reads XML it is handed (a response, an identity provider's metadata): with no DTD and no resolver,
    /// so an entity can neither expand nor fetch anything.
    /// </summary>
    internal static class SamlXmlReading
    {
        /// <summary>
        /// Creates an <see cref="XmlReader"/> over <paramref name="xml"/> that refuses a DTD and resolves nothing.
        /// </summary>
        /// <param name="xml">The XML text.</param>
        /// <returns>The reader; dispose it.</returns>
        public static XmlReader CreateReader(string xml)
        {
            XmlReaderSettings settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            return XmlReader.Create(new StringReader(xml), settings);
        }
    }
}
