using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO;
using FlatRedBall.Glue;
using FlatRedBall.IO;
using FlatRedBall.Glue.Plugins;
using System.Windows.Forms;
using System.ComponentModel.Composition;

namespace GlueContentSecurity
{
    public class CodeBuildItemAdder
    {
        #region Fields

        List<string> mFilesToAdd = new List<string>();

        public string FolderInProject
        {
            get;
            set;
        }

        #endregion

        /// <summary>
        /// Adds the argument resourceName to the internal list.
        /// </summary>
        /// <param name="resourceName">The name of the resource.  This is usally in the format of
        /// ProjectNamespace.Folder.FileName.cs</param>
        public void Add(string resourceName)
        {
            mFilesToAdd.Add(resourceName);
        }

        public bool PerformAddAndSave(Assembly assembly)
        {
            bool succeeded = true;
            bool preserveCase = FileManager.PreserveCase;
            FileManager.PreserveCase = true;

            List<string> filesToAddToProject = new List<string>();

            foreach (string resourceName in mFilesToAdd)
            {
                succeeded = SaveAndAddResourceFileToProject(assembly, succeeded, filesToAddToProject, resourceName);

                if (!succeeded)
                {
                    break;
                }
            }

            if (succeeded)
            {
                // Add these files to the project and any synced project
                foreach (var file in filesToAddToProject)
                {
                    ProjectManager.CodeProjectHelper.AddFileToCodeProjectIfNotAlreadyAdded(ProjectManager.ProjectBase, file);
                }
            }

            FileManager.PreserveCase = preserveCase;

            if (succeeded)
            {
                ProjectManager.SaveProjects();
            }

            return succeeded;
        }

        private bool SaveAndAddResourceFileToProject(Assembly assembly, bool succeeded, List<string> filesToAddToProject, string resourceName)
        {
            try
            {

                string destinationDirectory = ProjectManager.ProjectBase.Directory + FolderInProject + "/";

                string completelyStripped = FileManager.RemoveExtension(resourceName);
                int lastDot = completelyStripped.LastIndexOf('.');
                completelyStripped = completelyStripped.Substring(lastDot + 1);

                string destination = destinationDirectory + completelyStripped + ".cs";
                Directory.CreateDirectory(destinationDirectory);

                filesToAddToProject.Add(destination);

                var names = assembly.GetManifestResourceNames();

                const int maxFailures = 6;
                int numberOfFailures = 0;
                while (true)
                {
                    try
                    {
                        FileManager.SaveEmbeddedResource(assembly, resourceName, destination);
                        break;
                    }
                    catch (Exception e)
                    {
                        numberOfFailures++;

                        if (numberOfFailures == maxFailures)
                        {
                            // failed - what do we do?
                            PluginManager.ReceiveOutput("Failed to copy over file " + resourceName + " because of the following error:\n" + e.ToString());
                            break;
                        }
                        else
                        {
                            System.Threading.Thread.Sleep(15);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                succeeded = false;

                MessageBox.Show("Could not copy the file " + resourceName + "\n\n" + e.ToString());
            }
            return succeeded;
        }

    }
}
