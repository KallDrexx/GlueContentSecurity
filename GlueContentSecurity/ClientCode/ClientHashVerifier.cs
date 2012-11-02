using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.IO;
using System.Xml;
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;

namespace GlueContentSecurity.ClientCode
{
    public class ClientHashVerifier
    {
        /// <summary>
        /// Verifies all defined files have not been tampered with
        /// </summary>
        /// <exception cref="GlueSecuritySignatureMismatchException">Thrown when verification fails any game content</exception>
        public void VerifyContent()
        {
            const string XML_PUBLIC_KEY = "[publicKeyToken]";
            const string CONTENT_XML_LOCATIONS = "\\Content\\contentHashes.xml";

            // Load the content hash xml
            var doc = new XmlDocument();
            try { doc.Load(AppDomain.CurrentDomain.BaseDirectory + CONTENT_XML_LOCATIONS); }
            catch (FileNotFoundException)
            {
                throw new GlueSecuritySignatureMismatchException(null, false, true);
            }

            var key = new RSACryptoServiceProvider(1024);
            key.FromXmlString(XML_PUBLIC_KEY);

            var signedXml = new SignedXml(doc);
            XmlNodeList nodeList = doc.GetElementsByTagName("Signature");
            signedXml.LoadXml((XmlElement)nodeList[0]);

            if (!signedXml.CheckSignature(key))
                throw new GlueSecuritySignatureMismatchException(null, true, false);

            // Load into an XDocument
            var xdoc = XDocument.Parse(doc.OuterXml);

            // Find all the file nodes
            var mismatches = new List<ClientVerificationResult>();
            foreach (var node in xdoc.Descendants("File"))
            {
                var pathAttr = node.Attribute("Path");
                var hashAttr = node.Attribute("Hash");
                if (pathAttr != null)
                {
                    try
                    {
                        string hash = ComputeMd5Hash(
                            string.Concat(AppDomain.CurrentDomain.BaseDirectory, "\\Content\\", pathAttr.Value));

                        if (hashAttr == null || hash != hashAttr.Value)
                        {
                            mismatches.Add(new ClientVerificationResult
                            {
                                FilePath = pathAttr.Value,
                                ResultType = ClientVerificationResultType.IncorrectHash
                            });

                            continue;
                        }
                    }
                    catch (FileNotFoundException)
                    {
                        mismatches.Add(new ClientVerificationResult
                        {
                            FilePath = pathAttr.Value,
                            ResultType = ClientVerificationResultType.FileMissing
                        });
                    }
                }
            }

            if (mismatches.Count > 0)
                throw new GlueSecuritySignatureMismatchException(mismatches, false, false);
        }

        private string ComputeMd5Hash(string file)
        {
            //MD5 hash provider for computing the hash of the file
            var md5 = new MD5CryptoServiceProvider();

            //open the file
            var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 8192);

            //calculate the files hash
            md5.ComputeHash(stream);

            //close our stream
            stream.Close();

            //byte array of files hash
            byte[] hash = md5.Hash;

            //string builder to hold the results
            StringBuilder sb = new StringBuilder();

            //loop through each byte in the byte array
            foreach (byte b in hash)
            {
                //format each byte into the proper value and append
                //current value to return value
                sb.Append(string.Format("{0:X2}", b));
            }

            //return the MD5 hash of the file
            return sb.ToString();
        }
    }
}
