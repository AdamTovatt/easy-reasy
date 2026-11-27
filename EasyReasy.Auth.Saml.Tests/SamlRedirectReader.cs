using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace EasyReasy.Auth.Saml.Tests
{
    /// <summary>
    /// Reads an HTTP-Redirect binding URL as an identity provider does: its query parameters, and the AuthnRequest inflated
    /// from <c>SAMLRequest</c>.
    /// </summary>
    internal sealed class SamlRedirectReader
    {
        public const string ProtocolNamespace = "urn:oasis:names:tc:SAML:2.0:protocol";

        public SamlRedirectReader(string redirectUrl)
        {
            Url = new Uri(redirectUrl);
            Query = QueryHelpers.ParseQuery(Url.Query);
            byte[] compressed = Convert.FromBase64String(Query["SAMLRequest"].ToString());
            using MemoryStream input = new MemoryStream(compressed);
            using DeflateStream inflate = new DeflateStream(input, CompressionMode.Decompress);
            using StreamReader reader = new StreamReader(inflate, Encoding.UTF8);
            AuthnRequest = new XmlDocument();
            AuthnRequest.LoadXml(reader.ReadToEnd());
        }

        public Uri Url { get; }

        public Dictionary<string, StringValues> Query { get; }

        public XmlDocument AuthnRequest { get; }

        public XmlElement Root => AuthnRequest.DocumentElement!;

        public string RequestId => Root.GetAttribute("ID");

        public XmlElement? Child(string localName)
        {
            return Root[localName, ProtocolNamespace];
        }
    }
}
