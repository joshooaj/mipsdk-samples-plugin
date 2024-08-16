using SCExport.Background;
using SCExport.Client;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using VideoOS.Platform;
using VideoOS.Platform.Background;
using VideoOS.Platform.Client;

namespace SCExport
{
    /// <summary>
    /// This sample show how to perform small AVI and DB exports from the side panel in the Smart Client.
    /// </summary>
    public class SCExportDefinition : PluginDefinition
	{
		private static Image _treeNodeImage;

		internal static Guid SCExportPluginId = new Guid("afc84565-3e6a-4a3f-aed5-51976f50bfd1");
		internal static Guid SCExportSidePanel = new Guid("250b2a9a-7384-4aba-a855-33f2523e8a54");
		internal static Guid SCExportBackgroundPlugin = new Guid("67a8b670-fc49-42af-8b12-d56e60f6c49d");

        #region Private fields

        private List<BackgroundPlugin> _backgroundPlugins = new List<BackgroundPlugin>();
        private List<SidePanelPlugin> _sidePanelPlugins = new List<SidePanelPlugin>();

        #endregion

        #region Initialization

        /// <summary>
        /// Load resources 
        /// </summary>
        static SCExportDefinition()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string name = assembly.GetName().Name;

            System.IO.Stream pluginStream = assembly.GetManifestResourceStream(name + ".Resources.SCExport.bmp");
            if (pluginStream != null)
                _treeNodeImage = System.Drawing.Image.FromStream(pluginStream);
        }


        /// <summary>
        /// Get the icon for the plugin
        /// </summary>
        internal static Image TreeNodeImage
        {
            get { return _treeNodeImage; }
        }

        #endregion

        /// <summary>
        /// This method is called when the environment is up and running.
        /// Registration of Messages via RegisterReceiver can be done at this point.
        /// </summary>
        public override void Init()
        {
            _sidePanelPlugins.Add(new SCExportSidePanelPlugin());
            _backgroundPlugins.Add(new SCExportBackgroundPlugin());
        }

        /// <summary>
        /// The main application is about to be in an undetermined state, either logging off or exiting.
        /// You can release resources at this point, it should match what you acquired during Init, so additional call to Init() will work.
        /// </summary>
        public override void Close()
        {
            _sidePanelPlugins.Clear();
            _backgroundPlugins.Clear();
        }

        #region Identification Properties

        /// <summary>
        /// Gets the unique id identifying this plugin component
        /// </summary>
        public override Guid Id
        {
            get
            {
                return SCExportPluginId;
            }
        }

        /// <summary>
        /// This Guid can be defined on several different IPluginDefinitions with the same value,
        /// and will result in a combination of this top level ProductNode for several plugins.
        /// Set to Guid.Empty if no sharing is enabled.
        /// </summary>
        public override Guid SharedNodeId
        {
            get
            {
                return PluginSamples.Common.SampleTopNode;
            }
        }

        /// <summary>
        /// Define name of top level Tree node - e.g. A product name
        /// </summary>
        public override string Name
        {
            get { return "SCExport"; }
        }

        /// <summary>
        /// Top level name
        /// </summary>
        public override string SharedNodeName
        {
            get { return PluginSamples.Common.SampleNodeName; }
        }

        /// <summary>
        /// Your company name
        /// </summary>
        public override string Manufacturer
        {
            get
            {
                return PluginSamples.Common.ManufacturerName;
            }
        }

        /// <summary>
        /// Version of this plugin.
        /// </summary>
        public override string VersionString
        {
            get
            {
                return "2.0.0.0";
            }
        }

        /// <summary>
        /// Icon to be used on top level - e.g. a product or company logo
        /// </summary>
        public override System.Drawing.Image Icon
        {
            get { return VideoOS.Platform.UI.Util.ImageList.Images[VideoOS.Platform.UI.Util.PluginIx]; }
        }

        #endregion


        #region Administration properties

        /// <summary>
        /// A user control to display when the administrator clicks on the top TreeNode
        /// </summary>
        public override UserControl GenerateUserControl()
        {
            return new UserControl();
        }

        /// <summary>
        /// This property can be set to true, to be able to display your own help UserControl on the entire panel.
        /// When this is false - a standard top and left side is added by the system.
        /// </summary>
        public override bool UserControlFillEntirePanel
        {
            get { return false; }
        }
        #endregion

        #region Client related methods and properties

        /// <summary> 
        /// An extension plugin to add to the side panel of the Smart Client.
        /// </summary>
        public override List<SidePanelPlugin> SidePanelPlugins
        {
            get { return _sidePanelPlugins; }
        }

        /// <summary>
        /// Create and returns the background task.
        /// </summary>
        public override List<VideoOS.Platform.Background.BackgroundPlugin> BackgroundPlugins
        {
            get { return _backgroundPlugins; }
        }

        #endregion
    }
}
