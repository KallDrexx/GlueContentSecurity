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
using FlatRedBall.Glue.Plugins.ExportedInterfaces.CommandInterfaces;
using FlatRedBall.Glue.VSHelpers.Projects;
using System.Reflection;
using FlatRedBall.IO;

namespace GlueContentSecurity.Controls
{
    public partial class MainControl : UserControl
    {
        private const string CONTENT_HASH_XML_FILENAME = "contentHashes.xml";
        private const string CONTENT_KEY_FILENAME = "contentKey.xml";

        private string _projectDirectory;
        private string _projectContentDirectory;
        private IProjectCommands _projectCommands;

        public MainControl(IProjectCommands projectCommands)
        {
            InitializeComponent();

            _projectDirectory = ProjectManager.ProjectRootDirectory + "\\";
            _projectContentDirectory = ProjectManager.ContentDirectory + "\\";
            _projectCommands = projectCommands;
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

            UpdateSavedInfo();
        }

        public void RemoveReferencedFile(string name)
        {
            if (CheckIfFileSecured(name))
                lstSecuredFiles.Items.Remove(name);

            UpdateSavedInfo();
        }

        public void ReactToFileChanged(string name)
        {
            foreach (string trackedFile in lstSecuredFiles.Items)
            {
                if (name.EndsWith(trackedFile))
                {
                    UpdateSavedInfo();
                    return;
                }
            }
        }

        private void MainControl_Load(object sender, EventArgs e)
        {
            this.Dock = DockStyle.Fill;
            lstSecuredFiles.Sorted = true;

            LoadPreviousData();
        }

        private void btnGenerateKeys_Click(object sender, EventArgs e)
        {
            using (var key = new RSACryptoServiceProvider(1024))
            {
                txtPublicKey.Text = key.ToXmlString(true);
            }

            UpdateSavedInfo();
        }

        private void btnRemove_Click(object sender, EventArgs e)
        {
            if (lstSecuredFiles.SelectedItem == null)
                return;

            lstSecuredFiles.Items.Remove(lstSecuredFiles.SelectedItem);
            UpdateSavedInfo();
        }

        private void UpdateSavedInfo()
        {
            GeneratePrivateKeyStore();
            GenerateFileHashXml();
        }

        private void GeneratePrivateKeyStore()
        {
            var xml = new XDocument(
                        new XElement("KeyInfo",
                            new XElement("KeyXml", txtPublicKey.Text)));
            xml.Save(_projectDirectory + CONTENT_KEY_FILENAME);
        }

        private void GenerateFileHashXml()
        {
            // If no items are tracked, don't create the hash file
            if (lstSecuredFiles.Items.Count == 0 && !File.Exists(_projectContentDirectory + CONTENT_HASH_XML_FILENAME))
                return;                

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

                // Make sure the client code is added to the project
                LoadEmbeddedClientCode(key);
            }

            // Save the xml to a file
            xmlDocument.Save(_projectContentDirectory + CONTENT_HASH_XML_FILENAME);

            // Add it the content project
            _projectCommands.AddContentFileToProject(_projectContentDirectory + CONTENT_HASH_XML_FILENAME);
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

        private void LoadPreviousData()
        {
            txtPublicKey.Text = string.Empty;
            lstSecuredFiles.Items.Clear();

            if (!File.Exists(_projectDirectory + CONTENT_KEY_FILENAME))
            {
                // Generate a new hash since one doesn't exist
                btnGenerateKeys_Click(null, null);
            }
            else
            {
                // Load the key data
                var xml = XDocument.Load(_projectDirectory + CONTENT_KEY_FILENAME);
                txtPublicKey.Text = xml.Descendants("KeyXml").Select(x => x.Value).FirstOrDefault();
            }

            if (File.Exists(_projectContentDirectory + CONTENT_HASH_XML_FILENAME))
            {
                // Load the previously saved file hashes
                var xml = XDocument.Load(_projectContentDirectory + CONTENT_HASH_XML_FILENAME);
                foreach (var node in xml.Descendants("File"))
                {
                    var path = node.Attribute("Path");
                    if (path != null)
                    {
                        lstSecuredFiles.Items.Add(path.Value);
                    }
                }
            }

            // Update all the saved files, in case any hashes changed
            UpdateSavedInfo();
        }

        private void LoadEmbeddedClientCode(RSACryptoServiceProvider key)
        {
            const string PROJECT_NAME = "GlueContentSecurity.ClientCode";
            const string KEY_TOKEN = "[publicKeyToken]";
            string[] codeFileNames = { "ClientHashVerifier.cs", "ClientVerificationResult.cs", "GlueSecuritySignatureMismatchException.cs" };

            // Load the contents of the embedded files
            var assembly = Assembly.GetExecutingAssembly();
            foreach (string file in codeFileNames)
            {
                using (var stream = assembly.GetManifestResourceStream(string.Concat(PROJECT_NAME, ".", file)))
                using (var reader = new StreamReader(stream))
                {
                    string contents = reader.ReadToEnd();
                    contents = contents.Replace(KEY_TOKEN, key.ToXmlString(false));
                    SaveClientCodeToProject(contents, file);
                }
            }
        }

        private void SaveClientCodeToProject(string contents, string filename)
        {
            string destinationDirectory = ProjectManager.ProjectBase.Directory + "/GlueContentSecurity/";
            FileManager.SaveText(contents, destinationDirectory + filename);
            ProjectManager.CodeProjectHelper.AddFileToCodeProjectIfNotAlreadyAdded(ProjectManager.ProjectBase, destinationDirectory + filename);
        }
    }
}
