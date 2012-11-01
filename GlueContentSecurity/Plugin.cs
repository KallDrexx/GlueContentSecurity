using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using FlatRedBall.Glue;
using FlatRedBall.Glue.Plugins.ExportedInterfaces;
using FlatRedBall.Glue.Plugins.Interfaces;
using FlatRedBall.Glue.SaveClasses;
using FlatRedBall.Glue.Plugins;
using System.Windows.Forms;
using FlatRedBall.Glue.Controls;
using GlueContentSecurity.Controls;
using FlatRedBall.Glue.FormHelpers.PropertyGrids;
using FlatRedBall.Glue.GuiDisplay;

namespace GlueContentSecurity
{
    [Export(typeof(PluginBase))]
    public class Plugin : PluginBase
    {
        private TabControl _tabContainer;
        private PluginTab _tab;
        private MainControl _control;

        [Import("GlueProjectSave")]
        public GlueProjectSave GlueProjectSave
        {
            get;
            set;
        }

        [Import("GlueCommands")]
        public IGlueCommands GlueCommands
        {
            get;
            set;
        }
		
		[Import("GlueState")]
		public IGlueState GlueState
		{
		    get;
		    set;
        }

        public override string FriendlyName
        {
            get { return "Glue Content Security"; }
        }

        public override bool ShutDown(PluginShutDownReason reason)
        {
            // Do anything your plugin needs to do to shut down
            // or don't shut down and return false

            return true;
        }

        public override void StartUp()
        {
            InitializeCenterTabHandler = InitializeTab;
            AdjustDisplayedReferencedFile = AdjustDisplayedReferencedFileHandler;
            ReactToLoadedGlux = ProjectLoadedHandler;
        }

        public override Version Version
        {
            get { return new Version(1, 0); }
        }

        private void InitializeTab(TabControl tabControl)
        {
            // Store the tab container for later use
            _tabContainer = tabControl;
        }

        private void AdjustDisplayedReferencedFileHandler(ReferencedFileSave referencedFileSave, ReferencedFileSavePropertyGridDisplayer displayer)
        {
            string refName = referencedFileSave.Name;

            Func<object> getter = () => 
            { 
                return _control.CheckIfFileSecured(refName);
            };

            MemberChangeEventHandler setter = (sender, args) =>
            {
                bool value;
                bool.TryParse(args.Value.ToString(), out value);

                if (value)
                    _control.AddReferencedFile(refName);
                else
                    _control.RemoveReferencedFile(refName);
            };

            displayer.IncludeMember("Secure Content", typeof(bool), setter, getter, null, null);
        }

        private void ProjectLoadedHandler()
        {
            if (_tab != null)
                _tabContainer.Controls.Remove(_tab);

            _tab = new PluginTab();
            _tab.Text = "Content Security";
            _control = new MainControl(GlueCommands.ProjectCommands);
            _tab.Controls.Add(_control);
            _tabContainer.Controls.Add(_tab);
        }
    }
}
