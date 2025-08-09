using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace TextureOverlayer.Windows;

public class WarningWindow : Window, IDisposable
{

    // We give this window a constant ID using ###
    // This allows for labels being dynamic, like "{FPS Counter}fps###XYZ counter window",
    // and the window ID will always be "###XYZ counter window" for ImGui
    public WarningWindow(Plugin plugin) : base("A Wonderful Configuration Window###With a constant ID")
    {
        Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse;

        Size = new Vector2(232, 90);
        SizeCondition = ImGuiCond.Always;
        
    }

    public void Dispose() { }
    

    public override void Draw()
    {
        // can't ref a property, so use a local copy
        ImGui.TextUnformatted("You currently either do not have Penumbra installed, or it is not enabled\n please ");

    }
}
