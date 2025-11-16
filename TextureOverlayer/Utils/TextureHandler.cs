

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;
using FFXIVClientStructs;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.Havok.Common.Serialize.Util;
using Dalamud.Bindings.ImGui;
using Lumina.Excel;
using Newtonsoft.Json;
using Penumbra.Api.IpcSubscribers;
using TextureOverlayer.Interop;
using TextureOverlayer.Textures;

namespace TextureOverlayer.Utils;

public enum CombineOp
{
    LeftMultiply     = -4,
    LeftCopy         = -3,
    RightCopy        = -2,
    Invalid          = -1,
    Over             = 0,
    Under            = 1,
    RightMultiply    = 2,
    CopyChannels     = 3,
    SubtractChannels = 4,
    MultiplyChannels = 5,
    SoftLight        = 6,
}




public enum ResizeOp
{
    LeftOnly  = -2,
    RightOnly = -1,
    None      = 0,
    ToLeft    = 1,
    ToRight   = 2,
}

[Flags]
public enum Channels : byte
{
    Red   = 1,
    Green = 2,
    Blue  = 4,
    Alpha = 8,
}



/// <summary> Class that encapsulates all data needed to create the combination</summary>
/// <remarks></remarks>
[Serializable]
public class ImageCombination
{
    public String Name {get; set;}

    private bool enabled;
    String fileName = string.Empty;
    int position = -1;
    private (int width, int height) res;
    List<ImageLayer> layers = new List<ImageLayer>();
    public Dictionary<Guid, string> collection = new Dictionary<Guid, string>();
    public String _gamepath = string.Empty;
    
    
    [JsonIgnore]
    private List<CombinedTexture> _sandwich = new List<CombinedTexture>();
    [JsonIgnore]
    private CombinedTexture comboTex = new CombinedTexture(new Texture(), new Texture());
    [JsonIgnore]
    public int LoadState { get; set; } //0 == not loaded 1 == loading 2 == loaded


    public void AddRemoveCollection((Guid, string) Collection)
    {
        if (collection.ContainsKey(Collection.Item1))
        {
            if (Enabled)
            {
                Service.penumbraApi.RemoveTemporaryModCollection(this, Collection);
            }
            collection.Remove(Collection.Item1);
            Service.DataService.WriteConfig(this);
        }
        else
        {
            if (Enabled)
            {
                Service.penumbraApi.AddTemporaryModCollection(this, Collection);
            }
            collection.Add(Collection.Item1, Collection.Item2);
            Service.DataService.WriteConfig(this);
        }
        //Service.DataService.AdjustPenumbraSettings(this);
    }
    

    public ImageCombination(String displayName)
    {
        this.Name = displayName;
        enabled = false;
    }
    public String FileName
    {
        get => fileName;
        set => fileName = value;
    }



    public (int width, int height) Res
    {
        get => res;
        set => res = value;
    }

    public bool PathExists(string path)
    {
        foreach (var texture in layers)
        {
            if (texture.GetTexture().Path.Equals(path))
            {
                return true;
            }
        }
        return false;
            
    }
    public List<ImageLayer> Layers => layers;

    public bool Enabled
    {
        get => enabled;
        set
        {
            enabled = value;
            //Service.DataService.AdjustPenumbraSettings(this);
            if (enabled)
            {
                Service.penumbraApi.AddTemporaryMod(this);
            }
            else
            {
                Service.penumbraApi.RemoveTemporaryMod(this);
            }
        }
    }
    
    public void AddLayer(string path)
    {
        layers.Add(new ImageLayer(path, CombineOp.Over, ResizeOp.ToRight));
        Task.Run(() => {Compile();});
    }

    public void RemoveLayer(ImageLayer layer)
    {
        layers.Remove(layer);
        Task.Run(() => {Compile();});
    }
    

    

    public async Task Compile()
    {
        flushList(_sandwich);
        comboTex.Dispose();
        LoadState = 1;
        {
            if (layers.Count == 1)
            {
                comboTex = new CombinedTexture(layers[0].GetTexture(), new Texture());
                res = layers[0].GetTexture().BaseImage.Dimensions;

            }
            else if (layers.Count >= 2)
            {
                _sandwich.Add(new CombinedTexture(layers[0].GetTexture(), new Texture()));
                _sandwich[0].Update();
                for (var i = 1; i < layers.Count; ++i)
                {
                    if (layers[i]._enabled)
                    {
                        _sandwich[^1].GetCurrent().TextureWrap ??=
                            Service.TextureManager.LoadTextureWrap(_sandwich[^1].GetCurrent().RgbaPixels, res.width, res.height);
                        _sandwich.Add(new CombinedTexture(_sandwich[^1].GetCurrent(), layers[i].GetTexture()));
                        
                        await newStackOps(_sandwich[^1], layers[i]);
                    }

                }

                comboTex = _sandwich[^1];
            }

            LoadState = 2;
        }
    }


    [JsonIgnore]
    public CombinedTexture CombinedTexture => comboTex;

    public async Task newStackOps(CombinedTexture tex, ImageLayer layer)
    {
        tex.Update();
        tex.setOps(layer);
        var waiting = tex.CombineImage();
        return;
    }

    private void flushList(List<CombinedTexture> list)
    {
        foreach (var item in list)
        {
            item.Dispose();
        }
        list.Clear();
    }
    
}



/// <summary>
/// 
/// </summary>
[Serializable]
public class ImageLayer
{



    public Matrix4x4 _multiplier  = Matrix4x4.Identity;
    public Vector4   _constant    = Vector4.Zero;
    public int       _offsetX;
    public int       _offsetY;
    public CombineOp _combineOp    = CombineOp.Over;
    public ResizeOp  _resizeOp     = ResizeOp.None;
    public Channels  _copyChannels = Channels.Red | Channels.Green | Channels.Blue | Channels.Alpha;


    public Blake3.Hash _fileHash; //TODO: this isnt working, like what the sigma
    public bool _enabled = true;
    public String _friendlyName = string.Empty;
    
    [JsonIgnore]
    private Texture? _texture;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="_path"></param>
    /// <param name="combineOp"></param>
    /// <param name="resizeOp"></param>
    /// <param name="enabled"></param>
    public ImageLayer(String _path, CombineOp combineOp, ResizeOp resizeOp, bool enabled = true)
    {

        Texture texture = new Texture();
        _fileHash = Service.CacheService.TryGetCache(texture, _path);
        this._texture = texture;
        this._combineOp = combineOp;
        this._resizeOp = resizeOp;
        _enabled = enabled;
        _friendlyName = _path.Split('\\').Last().Split(".").First();
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="_fileHash"></param>
    /// <param name="combineOp"></param>
    /// <param name="resizeOp"></param>
    /// <param name="enabled"></param>
    
    [JsonConstructor]
    public ImageLayer(Blake3.Hash _fileHash, CombineOp combineOp, ResizeOp resizeOp, bool enabled = true)
    {
        this._fileHash = _fileHash;
        Texture texture = new Texture();
        Service.CacheService.TryGetCache(texture, _fileHash);
        this._texture = texture;
        this._combineOp = combineOp;
        this._resizeOp = resizeOp;
        _enabled = enabled;
    }

    /// <summary>
    /// Turns layer on or off
    /// </summary>
    public void FlipState()
    {
        _enabled = !_enabled;
        
    }
    
    /// <summary>
    /// Gets the FontAwesome String for the eye
    /// </summary>
    /// <returns></returns>
    public String getEyecon()
    {
        return _enabled ? FontAwesomeIcon.Eye.ToIconString() : FontAwesomeIcon.EyeSlash.ToIconString();
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public Texture GetTexture() => _texture;


    //TODO: make a file fetch function that will check that the file exists, if not requery to check if it still exists at all
} 



public static class TextureHandler
{
    /// <summary>
    /// 
    /// </summary>
    public static readonly IReadOnlyList<string> ResizeOpLabels = new string[]
    {
        "Overlay",
        "Under",
        "Right Multiply",
        "Copy Channels",
        "Subtract",
        "Multiply",
        "Soft Light"
    };
    /// <summary>
    /// 
    /// </summary>
    /// <param name="list"></param>
    /// <param name="indexA"></param>
    /// <param name="indexB"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static IList<T> Swap<T>(IList<T> list, int indexA, int indexB)
    {
        (list[indexA], list[indexB]) = (list[indexB], list[indexA]);
        return list;
    }

   /* public static nint GetImGuiHandle(ImageCombination file)
        => GetImGuiHandle(file.FileName);*/


/*    public static nint GetImGuiHandle(ImageCombination combo)  //need to add some cache shit here
    {
        try
        {
            LazyCombine(combo);

            return combinedTexture.GetCenter().TextureWrap.ImGuiHandle;
        }
        catch (Exception e)
        {
            Service.Log.Error(e.ToString());
        }
        
        return 0;
    }*/
    

    
}
