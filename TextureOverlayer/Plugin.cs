using System;
using System.Collections.Generic;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using System.Linq;
using System.Reflection;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using OtterGui.Log;
using Penumbra.Api.Api;
using Penumbra.Api.IpcSubscribers;
using TextureOverlayer.Interop;
using TextureOverlayer.Textures;
using TextureOverlayer.Utils;
using TextureOverlayer.Windows;



namespace TextureOverlayer;

public class Plugin : IDalamudPlugin
{

    
    private const string CommandName = "/to";
    


    public readonly WindowSystem WindowSystem = new("TextureOverlayer");
    //private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }
    private WarningWindow WarningWindow { get; init; }
    private ItemPicker ItemPicker { get; init; }
    



    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        MainWindow = new MainWindow(this);
        ItemPicker = new ItemPicker(this);
        
       //PluginListInvalidationKind kind,  IEnumerable<string> affectedInternalNames 
        void OnActivePluginsChanged(IActivePluginsChangedEventArgs args) => UpdateActivePluginState(args.Kind, args.AffectedInternalNames);

        void UpdateActivePluginState(PluginListInvalidationKind kind,  IEnumerable<string> affectedInternalNames)
        {
            if (kind == PluginListInvalidationKind.Loaded && affectedInternalNames.Contains("Penumbra"))
            {
                Init(pluginInterface);
                pluginInterface.ActivePluginsChanged -= OnActivePluginsChanged;
            }
            
            
        }
            
        if (pluginInterface.InstalledPlugins.All(p => p.InternalName != "Penumbra"))
        {
            WindowSystem.AddWindow(WarningWindow);
            return;
        }else if (pluginInterface.InstalledPlugins.Any(p => p is { InternalName: "Penumbra", IsLoaded: true }))
        {
            Init(pluginInterface);
        }
        else
        {
            pluginInterface.ActivePluginsChanged += OnActivePluginsChanged;
        }
        
        

        
        
    


    }

    public void Init(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Service>();
        Service.Configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Service.penumbraApi = new PenumbraIpc(pluginInterface);
        // you might normally want to embed resources and load them from the manifest stream
        try
        {
            Service.penumbraApi.Modlist = new GetModList(pluginInterface).Invoke();
        }
        catch (Exception e)
        {

            WarningWindow.Draw();
            Console.WriteLine(e);
            return;
        }

        Service.DataService = new DataService();
        
        //ConfigWindow = new ConfigWindow(this);

        //WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(ItemPicker);


        Service.Configuration.ModRootDirectory = Service.penumbraApi.GetModDirectory();
        Service.Configuration.PluginFolder = Service.penumbraApi.setupFolderStructure();



        
        Service.CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Opens the main Texture Overlayer interface"
        });

        pluginInterface.UiBuilder.Draw += DrawUI;

        // This adds a button to the plugin installer entry of this plugin which allows
        // to toggle the display status of the configuration ui
        //pluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;

        // Adds another button that is doing the same but for the main ui of the plugin
        pluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;

        Service.TextureManager = new TextureManager(Service.DataManager, new Logger(), Service.TextureProvider, pluginInterface.UiBuilder);
        
        // Add a simple message to the log with level set to information
        // Use /xllog to open the log window in-game
        // Example Output: 00:57:54.959 | INF | [TextureOverlayer] ===A cool log message from Sample Plugin===
        Service.Log.Information($"===A cool log message from {pluginInterface.Manifest.Name}===");
        Service.CacheService = new CacheService();
        var existingConfs = Directory.GetFiles(Service.Configuration.PluginFolder, "*.json");
        foreach (var path in existingConfs)
        {
            Service.DataService.AllCombinations.Add(Service.DataService.ReadConfig(path));
        }
        
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();

        //ConfigWindow.Dispose();
        MainWindow.Dispose();

        Service.CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args)
    {
        // in response to the slash command, just toggle the display status of our main ui
        ToggleMainUI();
    }

    private void DrawUI() => WindowSystem.Draw();

    //public void ToggleConfigUI() => ConfigWindow.Toggle();
    public void ToggleMainUI() => MainWindow.Toggle();
    public void ToggleModUI() => ItemPicker.Toggle();
}
