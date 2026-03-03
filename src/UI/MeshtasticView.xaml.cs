using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WinTakMeshtasticPlugin.Models;
using WinTakMeshtasticPlugin.Plugin;

namespace WinTakMeshtasticPlugin.UI
{
    /// <summary>
    /// Code-behind for MeshtasticView.xaml.
    /// WPF UserControl for the Meshtastic dock pane content.
    /// </summary>
    public partial class MeshtasticView : UserControl
    {
        public MeshtasticView()
        {
            InitializeComponent();
        }

        private void OnNodeDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBox listBox && listBox.SelectedItem is NodeState selectedNode)
            {
                // Use module's ShowTelemetryWindow for consistency with map marker clicks
                var module = MeshtasticModule.Instance;
                if (module != null)
                {
                    module.ShowTelemetryWindow(selectedNode);
                }
                else
                {
                    // Fallback if module not available
                    var telemetryWindow = new TelemetryWindow(selectedNode)
                    {
                        Owner = Window.GetWindow(this)
                    };
                    telemetryWindow.Show();
                }
            }
        }
    }
}
