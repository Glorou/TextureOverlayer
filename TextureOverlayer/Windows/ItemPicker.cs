using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Lumina;
using Lumina.Excel.Sheets;
using Lumina.Data.Files;
using Lumina.Models.Materials;
using TextureOverlayer.Textures;
using TextureOverlayer.Utils;
using Texture = TextureOverlayer.Textures.Texture;


namespace TextureOverlayer.Windows;

public class ItemPicker : Window, IDisposable
{


    private Plugin Plugin;

    //private readonly TextureManager _textures = Service.TextureManager;
    //private readonly Texture preview = new();
    public string previewPath = string.Empty;
    public Texture previewTexture;


    // We give this window a hidden ID using ##
    // So that the user will see "My Amazing Window" as window title,
    // but for ImGui the ID is "My Amazing Window##With a hidden ID"
    public ItemPicker(Plugin plugin)
        : base("Select a Texture##ModSelector")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 330),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };


        Plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {

        // Do not use .Text() or any other formatted function like TextWrapped(), or SetTooltip().
        // These expect formatting parameter if any part of the text contains a "%", which we can't
        // provide through our bindings, leading to a Crash to Desktop.
        // Replacements can be found in the ImGuiHelpers Class

        ImGui.TextUnformatted($"Penumbra has {Service.penumbraApi.Modlist.Count} mods");
        ImGui.Spacing();

        // Normally a BeginChild() would have to be followed by an unconditional EndChild(),
        // ImRaii takes care of this after the scope ends.
        // This works for all ImGui functions that require specific handling, examples are BeginTable() or Indent().


        using (var child = ImRaii.Child("SomeChildWithAScrollbar",
                                        new Vector2((ImGui.GetContentRegionAvail().X * 0.70f),
                                                    ImGui.GetContentRegionAvail().Y), false))
        {

            // Check if this child is drawing
            if (child.Success)
            {


                foreach (var mod in Service.penumbraApi.Modlist.Values)
                {

                    ImGui.PushID(mod);
                    if (ImGui.TreeNodeEx($"{mod}"))
                    {
                        var fileArray = Service.penumbraApi.GetTextureList(
                            Service.penumbraApi.Modlist.FirstOrDefault(x => x.Value == mod).Key);
                        foreach (var file in fileArray)
                        {
                            ImGui.TextUnformatted(
                                $"{file.Remove(0, ("D:\\Games\\FFXIV\\Penumbra").Length + Service.penumbraApi.Modlist.FirstOrDefault(x => x.Value == mod).Key.Length)}\n");
                            if (ImGui.Button($"Select##{file}"))
                            {
                                previewPath = file;
                                Service.TextureManager.LoadTex(file);
                            }
                        }


                        if (fileArray.Count == 0)
                        {
                            ImGui.TextUnformatted($"No textures found for {mod}");
                        }




                        ImGui.TreePop();
                    }

                    ImGui.PopID();

                }

                ImGui.TreePop();





            }
        }

        ImGui.SameLine();
        using (var previewer = ImRaii.Child("previewer"))
        {

            if (previewPath != string.Empty)
            {
                if (!previewTexture.IsLoaded)
                {
                    previewTexture.Load(Service.TextureManager, previewPath);
                }
                else
                {

                    ImGui.Image(previewTexture.TextureWrap.Handle,
                                new Vector2((ImGui.GetContentRegionAvail().X), (ImGui.GetContentRegionAvail().X)));
                    if (ImGui.Button($"Confirm Texture"))
                    {
                        if (!Service.DataService.GetImageCombination(Service.DataService.GetSelectedCombo())
                                    .PathExists(previewPath))
                        {
                            Service.DataService.GetImageCombination(Service.DataService.GetSelectedCombo())
                                   .AddLayer(previewPath);
                            Service.DataService.GetImageCombination(Service.DataService.GetSelectedCombo()).LoadState =
                                0;
                        }

                    }

                }

            }
        }
    }
}
