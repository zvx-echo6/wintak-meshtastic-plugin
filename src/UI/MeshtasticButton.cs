using System.ComponentModel.Composition;
using WinTak.Framework.Docking;
using WinTak.Framework.Tools;
using WinTak.Framework.Tools.Attributes;

namespace WinTakMeshtasticPlugin.UI
{
    /// <summary>
    /// Ribbon bar button to open the Meshtastic dockable panel.
    /// Appears in WinTAK's ribbon bar with the Meshtastic icon.
    /// </summary>
    [Button(
        "MeshtasticButton",
        "Meshtastic",
        LargeImage = "meshtastic_icon.png",
        SmallImage = "meshtastic_icon_24.png",
        ResourceFileType = typeof(MeshtasticButton))]
    [Export]
    public class MeshtasticButton : Button
    {
        private readonly IDockingManager _dockingManager;

        /// <summary>
        /// MEF constructor with dependency injection.
        /// IDockingManager is provided by WinTAK for managing dockable panels.
        /// </summary>
        [ImportingConstructor]
        public MeshtasticButton(IDockingManager dockingManager)
        {
            _dockingManager = dockingManager;
        }

        /// <summary>
        /// Called when the button is clicked in the ribbon bar.
        /// Activates the Meshtastic dock pane.
        /// </summary>
        protected override void OnClick()
        {
            // Get the dock pane and activate it (shows it if hidden, focuses it if visible)
            var dockPane = _dockingManager.GetDockPane(MeshtasticDockPane.Id);
            dockPane?.Activate();
        }
    }
}
