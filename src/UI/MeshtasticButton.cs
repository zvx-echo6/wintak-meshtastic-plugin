using System.ComponentModel.Composition;
using WinTak.Framework.Docking;
using WinTak.Framework.Tools;

namespace WinTakMeshtasticPlugin.UI
{
    /// <summary>
    /// Ribbon bar button to open the Meshtastic dockable panel.
    /// Appears in WinTAK's ribbon bar with the Meshtastic icon.
    /// </summary>
    [Button(
        typeof(MeshtasticButton),
        "Meshtastic",
        LargeImagePath = "/WinTakMeshtasticPlugin;component/Assets/meshtastic_icon.png",
        SmallImagePath = "/WinTakMeshtasticPlugin;component/Assets/meshtastic_icon_24.png")]
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
            // Activate the dock pane (shows it if hidden, focuses it if visible)
            _dockingManager.ActivateDockPane(MeshtasticDockPane.Id);
        }
    }
}
