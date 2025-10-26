using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using Dalamud.Bindings.ImGui;
using Lumina.Excel.Sheets;
using TextureOverlayer.Interop;
using TextureOverlayer.Textures;
using TextureOverlayer.Utils;


namespace TextureOverlayer.Windows;

public class MainWindow : Window, IDisposable
{


    private Plugin Plugin;
    private static string name = string.Empty;
    private ImageCombination selectedCombination = null;
    private static int selectedIndex = -1;
    private String tempGamePath = string.Empty;

    private Vector4 transparent = new Vector4(0f, 0f, 0f, 0f);

    private Vector4 green = new Vector4(0, 204, 0, 1);

    private Vector4 red = new Vector4(220, 0, 0, 1);
    
    Task? task = null;
    
    CancellationTokenSource? cts = null;
    // We give this window a hidden ID using ##
    // So that the user will see "My Amazing Window" as window title,
    // but for ImGui the ID is "My Amazing Window##With a hidden ID"
    public MainWindow(Plugin plugin)
        : base("Texture Overlayer##With a hidden ID", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(650, 400),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        
        Service.FileDialogManager = new FileDialogManager();

        Plugin = plugin;
    }

    public void Dispose() { }

    public void TransparentButton()
    {

            ImGui.PushStyleColor(ImGuiCol.Button, transparent);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, transparent);
            ImGui.PushStyleColor(ImGuiCol.Button, transparent);

    }

    public void HelpMarker(string text)
    {

        using (Service.PluginInterface.UiBuilder.IconFontFixedWidthHandle.Push())
        {
            ImGui.TextDisabled(FontAwesomeIcon.QuestionCircle.ToIconString());;
        }

        if (ImGui.IsItemHovered())
        {
            using (var tip = ImRaii.Tooltip())
            {
                ImGui.PushTextWrapPos(ImGui.CalcTextSize(text.Substring(0, Math.Min(35, text.Length))).X);
                ImGui.TextUnformatted(text);
                ImGui.PopTextWrapPos();
            }
        }
    }
    public void PenumbraButton()
    {
        bool disableButton = selectedCombination.FileName == string.Empty ||
                            !Path.Exists(Service.Configuration.PluginFolder + "\\" + selectedCombination.FileName) ||
                            selectedCombination._gamepath == string.Empty || selectedCombination.collection.Count == 0;
        Vector4 _savedFile = (selectedCombination.FileName == string.Empty ||
                              !Path.Exists(Service.Configuration.PluginFolder + "\\" + selectedCombination.FileName)) ? red : green;
        Vector4 _pathSet = selectedCombination._gamepath == string.Empty ? red : green;
        Vector4 _collectionSet = selectedCombination.collection.Count == 0 ? red : green;
        
        if (disableButton)
        {
            ImGui.BeginDisabled();
        }
        if (ImGui.Button("Change redirect state"))
        {
            selectedCombination.Enabled = !selectedCombination.Enabled;
        }
        if (disableButton)
        {
            ImGui.EndDisabled();
            if(ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                using (var tool = ImRaii.Tooltip())
                {
                    ImGui.TextUnformatted("To set the Penumbra redirect you need to:\n");
                    using (ImRaii.PushColor(ImGuiCol.Text, _pathSet))
                    {
                        ImGui.BulletText("Set a proper game path\n");
                    }
                    using (ImRaii.PushColor(ImGuiCol.Text, _collectionSet))
                    {
                        ImGui.BulletText("Set at least one collection\n");
                    }
                    using (ImRaii.PushColor(ImGuiCol.Text, _savedFile))
                    {
                        ImGui.BulletText("Save the Texture\n");
                    }
                    
                }
            }

        }

        ImGui.SameLine();
        if (selectedCombination.Enabled)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, green);
            ImGui.TextUnformatted("Penumbra redirect enabled");
            using (Service.PluginInterface.UiBuilder.IconFontFixedWidthHandle.Push())
            {
                ImGui.SameLine();
                ImGui.Text(FontAwesomeIcon.Check.ToIconString());
            }

        }
        else
        {

            ImGui.PushStyleColor(ImGuiCol.Text, red);
            ImGui.TextUnformatted("Penumbra redirect disabled");
            using (Service.PluginInterface.UiBuilder.IconFontFixedWidthHandle.Push())
            {
                ImGui.SameLine();
                ImGui.Text(FontAwesomeIcon.Times.ToIconString());
            }

        }

        ImGui.PopStyleColor(1);
    }
    public void SelectableInput(String test){}

    public void ReloadFromFileButton()
    {
        bool _enabled = File.Exists(Service.Configuration.PluginFolder + "\\" + selectedCombination.Name + ".json");
        
        if (!_enabled){ImGui.BeginDisabled();}
        if (ImGui.Button("Reload from File"))
        {
            selectedCombination = Service.DataService.ReloadComboFromFile(selectedCombination);
            
        }
        if (!_enabled){ImGui.EndDisabled();}
    }
    public override void Draw()
    {
        Service.FileDialogManager.Draw();
        
        ImGui.BeginGroup();

        ImGui.TextUnformatted($"Combined Textures:");
        // Normally a BeginChild() would have to be followed by an unconditional EndChild(),
        // ImRaii takes care of this after the scope ends.
        // This works for all ImGui functions that require specific handling, examples are BeginTable() or Indent().
        using (var child = ImRaii.Child("ComboPicker", new Vector2(ImGui.GetContentRegionAvail().X * 0.15f, ImGui.GetContentRegionAvail().Y * 0.60f), true))
        {
            // Check if this child is drawing
            if (child.Success)
            {

                foreach (var combo in Service.DataService.AllCombinations)
                {

                    if (ImGui.Selectable(combo.Name))
                    {
                        selectedCombination = combo;
                        Task.Run(() => { selectedCombination.Compile();});
                        tempGamePath = combo._gamepath;
                    }


                }
            }
        }
        if (ImGui.Button("New Combined Texture"))
        {
            ImGui.OpenPopup("New Combined Texture##Window");

        }
        using(var popup = ImRaii.Popup("New Combined Texture##Window"))
        {
            if (popup)
            {
                ImGui.TextUnformatted("Texture nickname:");
                ImGui.InputText("", ref name, 255);
                if (ImGui.Button("Confirm"))
                {
                    if (!name.IsNullOrWhitespace() && !Service.DataService.AllCombinations.Any(p => p.Name == name))
                    {
                        Service.DataService.AddImageCombination(name);
                        ImGui.CloseCurrentPopup();
                    }

                }
            }

        }

        if (selectedCombination != null && selectedCombination.Layers.Count >= 1)
        {
            if (ImGui.Button("Save Texture Config"))
            {
                selectedCombination.FileName = Service.DataService.WriteTexFile(selectedCombination);
                Service.DataService.WriteConfig(selectedCombination);
                Task.Run(() => { selectedCombination.Compile();});
                if(selectedCombination.Enabled){Service.penumbraApi.RedrawAll();}
            }

            ReloadFromFileButton();
            if (ImGui.Button("Delete Selected Texture"))
            {
                var _name = selectedCombination.Name;

                Service.DataService.RemoveImageCombination(_name);
                selectedCombination = Service.DataService.AllCombinations.Any()
                                          ? Service.DataService.AllCombinations.First()
                                          : null;
            }
        }

        ImGui.EndGroup();
        

        ImGui.SameLine();
        ImGui.BeginGroup();
        //TODO: Make a load wheel and figure out how to not lock up the game while the compile process runs
        using (var previewer = ImRaii.Child("previewer"))
        {
            using (var imageContainer = ImRaii.Child("imageContainer", new Vector2(ImGui.GetContentRegionAvail().X * .75f, ImGui.GetContentRegionAvail().Y)))
            {
                if (selectedCombination != null && selectedCombination.Layers.Count >= 1 &&
                    selectedCombination.LoadState == 2)
                {
                    selectedCombination.CombinedTexture.Draw(Service.TextureManager,
                                                             new Vector2(
                                                                 (Math.Min(ImGui.GetContentRegionAvail().X,
                                                                           ImGui.GetContentRegionAvail().Y) * .75f),
                                                                 Math.Min(ImGui.GetContentRegionAvail().X,
                                                                          ImGui.GetContentRegionAvail().Y) * .75f));

                    if (ImGui.Button("Select Collection"))
                    {
                        ImGui.OpenPopup("Select Collection##Window");

                    }
                    ImGui.SameLine();
                    HelpMarker("Penumbra inheritance doesn't work. Manually select all collections you want.");

                    ImGui.SameLine();
                    PenumbraButton();
                    ImGui.SameLine();
                    HelpMarker("Reminder:\nyou need to save for changes to show up.");
                    
                    using (var popup = ImRaii.ContextPopupItem("Select Collection##Window"))
                    {
                        if (popup)
                        {
                            
                            foreach (var collection in Service.penumbraApi.GetCollections())
                            {
                                using(var color = ImRaii.PushColor(ImGuiCol.Text, (selectedCombination.collection.ContainsKey(collection.Key)? green : ImGui.GetStyle().Colors[(int) ImGuiCol.Text]))) {

                                    if (ImGui.Selectable(collection.Value, false, (ImGuiSelectableFlags)17))
                                    {
                                        selectedCombination.AddRemoveCollection((collection.Key, collection.Value));
                                    }

                                }
                                /*if (selectedCombination.collection.ContainsKey(collection.Key))
                                {
                                    using (Service.PluginInterface.UiBuilder.IconFontFixedWidthHandle.Push())
                                    {
                                        ImGui.SameLine(Service.PluginInterface.UiBuilder.DefaultFontSpec.SizePx  * (collection.Value.Length ) );
                                        ImGui.SetCursorPosX(ImGui.GetContentRegionMax().X- (Service.PluginInterface.UiBuilder.DefaultFontSpec.SizePx * 1f));
                                        ImGui.Text(FontAwesomeIcon.Check.ToIconString());
                                    }
                                }*/

                            }
                            ImGui.Separator();
                            if(ImGui.Selectable("Close"))   //new Vector2(ImGui.GetContentRegionAvail().X, Service.PluginInterface.UiBuilder.DefaultFontSpec.SizePx * ImGuiHelpers.GlobalScale * 1.5f))
                            {
                                ImGui.CloseCurrentPopup();
                            }


                        }

                    }


                    ImGui.TextUnformatted("Game path to replace:");
                    ImGui.InputText("##replacePath", ref tempGamePath, 128);
                    ImGui.SameLine();
                    if (ImGui.Button("Set"))
                    {
                        selectedCombination._gamepath = tempGamePath;
                    }



                }
                else
                {
                    ImGui.Image(
                        Service.TextureProvider
                               .GetFromManifestResource(Assembly.GetExecutingAssembly(),
                                                        "TextureOverlayer.Placeholder.png").GetWrapOrEmpty()
                               .Handle,
                        new Vector2((Math.Min(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y) * .75f),
                                    Math.Min(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y) * .75f));
                }

                ImGui.EndGroup();
  
            }
            ImGui.SameLine();
            ImGui.BeginGroup();
            if (selectedCombination != null && selectedCombination.Layers.Count >= 0)
            {
                
                using(var child = ImRaii.Child("imagestack",
                                                new Vector2(ImGui.GetContentRegionAvail().X,
                                                            ImGui.GetContentRegionAvail().Y * 0.50f), true))
                {
                    // Check if this child is drawing
                    //TODO: Make this drag and dropable for reordering layers and remove the eye from the base layer
                    //TODO: Flip this lol
                    if (child.Success)
                    {
                        foreach (var image in selectedCombination.Layers.AsEnumerable().Reverse())
                        {
                            //TODO: Make selectable in to text box on double click
                            if (ImGui.Selectable($"{image._friendlyName}", (selectedIndex == selectedCombination.Layers.IndexOf(image)), ImGuiSelectableFlags.AllowItemOverlap))
                            {
                                if (selectedIndex == selectedCombination.Layers.IndexOf(image))
                                {
                                    selectedIndex = -1;
                                }
                                else
                                {
                                    selectedIndex = selectedCombination.Layers.FindLastIndex(u => u.GetTexture().Path == image.GetTexture().Path);
                                }

                            }
                            ImGui.SameLine();
                            using (Service.PluginInterface.UiBuilder.IconFontFixedWidthHandle.Push()) {
                                ImGui.SetCursorPosX((ImGui.GetContentRegionMax().X * 0.95f)- Service.PluginInterface.UiBuilder.DefaultFontSpec.SizePx );
                                TransparentButton();
                                
                                if (ImGui.SmallButton($"{image.getEyecon()}##{image._friendlyName}"))
                                {
                                    image.FlipState();
                                    Task.Run(() => { selectedCombination.Compile();});
                                }
                                ImGui.PopStyleColor(3);
                            }
                        }
                        

                        
                        //TODO: Add opacity and image combination mode here
                    }
                    
                }




                ImGui.BeginChild("##LayerOps",
                                 new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y));
                if (selectedCombination.Layers.Count > selectedIndex && selectedIndex >= 0)
                {
                    
                    //up is down, down is up AAAAAAAAAAAAAAAAAAAAAAA
                    ImGui.BeginChild("##LayerMover",
                        new Vector2(ImGui.GetContentRegionAvail().X, Service.PluginInterface.UiBuilder.DefaultFontSpec.SizePx * 1.5f));
                    using (Service.PluginInterface.UiBuilder.IconFontFixedWidthHandle.Push()) {
                        

                        if(selectedCombination.Layers[selectedIndex] == selectedCombination.Layers.Last()){ImGui.BeginDisabled();}
                        if (ImGui.Button($"{FontAwesomeIcon.ArrowUp.ToIconString()}", new Vector2(ImGui.GetContentRegionAvail().X * .5f, ImGui.GetContentRegionAvail().Y)))
                        {
                            TextureHandler.Swap(selectedCombination.Layers, selectedIndex, selectedIndex + 1);
                            selectedIndex++;
                            Task.Run(() => { selectedCombination.Compile();});
                        }
                        if(selectedCombination.Layers[selectedIndex] == selectedCombination.Layers.Last()){ImGui.EndDisabled();}
                        if(selectedCombination.Layers[selectedIndex] == selectedCombination.Layers.First()){ImGui.BeginDisabled();}
                        ImGui.SameLine();
                        if (ImGui.Button($"{FontAwesomeIcon.ArrowDown.ToIconString()}",new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y)))
                        {
                            TextureHandler.Swap(selectedCombination.Layers, selectedIndex, selectedIndex - 1);
                            selectedIndex--;
                            Task.Run(() => { selectedCombination.Compile();});
                        }
                        if(selectedCombination.Layers[selectedIndex] == selectedCombination.Layers.First()){ImGui.EndDisabled();}
                        
                    }
                    ImGui.EndChild();
                    
                    
                    using (var combo = ImRaii.Combo("##operationPicker",
                                                    TextureHandler.ResizeOpLabels[
                                                        (int)selectedCombination.Layers[selectedIndex]._combineOp]))
                    {
                        if (combo.Success)
                        {
                            if (ImGui.Selectable(TextureHandler.ResizeOpLabels[0]))
                            {
                                selectedCombination.Layers[selectedIndex]._combineOp = CombineOp.Over;
                                Task.Run(() => { selectedCombination.Compile();});
                            }

                            if (ImGui.Selectable(TextureHandler.ResizeOpLabels[4]))
                            {
                                selectedCombination.Layers[selectedIndex]._combineOp = CombineOp.SubtractChannels;
                                Task.Run(() => { selectedCombination.Compile();});
                            }
                            if (ImGui.Selectable(TextureHandler.ResizeOpLabels[6]))
                            {
                                selectedCombination.Layers[selectedIndex]._combineOp = CombineOp.SoftLight;
                                Task.Run(() => { selectedCombination.Compile();});
                            }
                            /*if (ImGui.Selectable(TextureHandler.ResizeOpLabels[5]))
                            {
                                selectedCombination.Layers[selectedIndex]._combineOp = CombineOp.MultiplyChannels;
                                selectedCombination.Compile();
                            } */
                        }
                    }

                    if (ImGui.Button("Delete Layer"))
                    {
                        selectedCombination.RemoveLayer(selectedCombination.Layers[selectedIndex]);
                    }
                }

                
                //TODO: Add a library based on cached textures 
                //TODO: Fix penumbra interface
                ImGui.TextUnformatted("New Layer from:");

                /*if(ImGui.Button("Penumbra mod"))
                {
                    Service.DataService.SetSelectedCombo(selectedCombination.Name);
                    Plugin.ToggleModUI();
                }*/
                ImGui.SameLine();
                if(ImGui.Button("File"))
                {
                    
                    Service.FileDialogManager.OpenFileDialog("Select Image","Image Files {.png,.tga,.dds,.bmp,.tex}",
                                                             (success, path) =>
                                                             {
                                                                 if (success)
                                                                 {
                                                                     selectedCombination.AddLayer(path[0]);
                                                                     selectedCombination.LoadState = 0;
                                                                 }
                                                                 
                                                             }, 1, null );
                }

                ImGui.EndChild();
            }
        }
    }
}    

    
