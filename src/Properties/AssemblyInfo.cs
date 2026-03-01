using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using WinTak.Framework;

// General Information about an assembly
[assembly: AssemblyTitle("WinTakMeshtasticPlugin")]
[assembly: AssemblyDescription("Native WinTAK plugin for Meshtastic mesh network integration")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Echo6")]
[assembly: AssemblyProduct("WinTAK Meshtastic Plugin")]
[assembly: AssemblyCopyright("Copyright © 2026 Echo6")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// COM visibility
[assembly: ComVisible(false)]

// WPF theme resources
[assembly: ThemeInfo(
    ResourceDictionaryLocation.None,
    ResourceDictionaryLocation.SourceAssembly
)]

// Version information
[assembly: AssemblyVersion("0.1.0.0")]
[assembly: AssemblyFileVersion("0.1.0.0")]

// WinTAK Plugin Attributes
// These are required for WinTAK to recognize and load the plugin
[assembly: TakSdkVersion("5.0")]
[assembly: PluginName("Meshtastic")]
[assembly: PluginDescription("Native Meshtastic mesh network integration for WinTAK. Provides bidirectional communication with Meshtastic nodes, PLI tracking, channel-aware chat, and telemetry display.")]
[assembly: PluginIcon("/WinTakMeshtasticPlugin;component/Assets/meshtastic_icon.png")]
