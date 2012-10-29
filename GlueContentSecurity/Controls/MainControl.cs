using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Security.Cryptography;
using System.Xml.Linq;
using System.IO;
using System.Security.Cryptography.Xml;
using System.Xml;
using FlatRedBall.Glue;

namespace GlueContentSecurity.Controls
{
    public partial class MainControl : UserControl
    {
        private string _projectContentDirectory;

        public MainControl(string projectContentDirectory)
        {
            InitializeComponent();

            _projectContentDirectory = projectContentDirectory;
            if (!_projectContentDirectory.EndsWith("\\"))
                _projectContentDirectory += "\\";
        }

        public bool CheckIfFileSecured(string name)
        {
            for (int x = 0; x < lstSecuredFiles.Items.Count; x++)
                if (lstSecuredFiles.Items[x].ToString().Equals(name.Trim(), StringComparison.OrdinalIgnoreCase))
                    return true;

            return false;
        }

        public void AddReferencedFile(string name)
        {
            if (!CheckIfFileSecured(name))
                lstSecuredFiles.Items.Add(name);

            GenerateFileHashXml();
        }

        public void RemoveReferencedFile(string name)
        {
            if (CheckIfFileSecured(name))
                lstSecuredFiles.Items.Remove(name);

            GenerateFileHashXml();
        }

        private void MainControl_Load(object sender, EventArgs e)
        {
            this.Dock = DockStyle.Fill;
            lstSecuredFiles.Sorted = true;
            btnGenerateKeys_Click(null, null);
        }

        private void btnGenerateKeys_Click(object sender, EventArgs e)
        {
            using (var key = new RSACryptoServiceProvider(1024))
            {
                txtPublicKey.Text = key.ToXmlString(true);
            }
        }

        private void GenerateFileHashXml()
        {
            var xml = new XDocument();
            var root = new XElement("FileHashes");

            for (int x = 0; x < lstSecuredFiles.Items.Count; x++)
            {
                string file = lstSecuredFiles.Items[x].ToString();
                root.Add(
                    new XElement("File",
                        new XAttribute("Path", file),
                        new XAttribute("Hash", ComputeMd5Hash(_projectContentDirectory + file))
                    )
                );
            }

            xml.Add(root);

            // Convert from an XDocument to XmlDocument for processing
            var xmlDocument = new XmlDocument();
            using (var reader = xml.CreateReader())
                xmlDocument.Load(reader);

            // Sign the xml
            using (var key = new RSACryptoServiceProvider(1024))
            {
                key.FromXmlString(txtPublicKey.Text);
                var signedXml = new SignedXml(xmlDocument);
                signedXml.SigningKey = key;

                var reference = new Reference { Uri = "" };
                reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
                signedXml.AddReference(reference);

                signedXml.ComputeSignature();
                var signature = signedXml.GetXml();
                xmlDocument.DocumentElement.AppendChild(xmlDocument.ImportNode(signature, true));
                if (xmlDocument.FirstChild is XmlDeclaration)
                    xmlDocument.RemoveChild(xmlDocument.FirstChild);
            }

            // Save the xml to a file
            xmlDocument.Save(string.Concat(_projectContentDirectory, "contentHashes.xml"));
        }

        /// <summary>
        /// method for getting a files MD5 hash, say for
        /// a checksum operation
        /// </summary>
        /// <param name="file">the file we want the has from</param>
        /// <returns></returns>
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
