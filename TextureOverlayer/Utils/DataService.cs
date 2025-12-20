using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Threading.Tasks;
using Dalamud.Utility;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OtterTex;
using TextureOverlayer.Textures;

namespace TextureOverlayer.Utils;




//TODO: Copy entire class when selected, so we dont have to compare to the file? maybe?
public class DataService
{
    private List<ImageCombination> _allCombinations = new List<ImageCombination>();

    public List<ImageCombination> AllCombinations => _allCombinations;
    private string selectedCombination= string.Empty;
    public bool AddImageCombination(string displayName )
    {
        try
        {
            _allCombinations.Add(new ImageCombination(displayName));
            return true;
        }
        catch (Exception e)
        {
            Service.Log.Error("Uh oh fucky wucky!!");
            return false;
        }
    }

    public ImageCombination? GetImageCombination(String name)
    {
        return _allCombinations.Find(x => x.Name == name);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public bool RemoveImageCombination(String name)
    {
        try
        {
            var temp = _allCombinations.Find(x => x.Name == name);
            temp.CombinedTexture.Dispose();
            foreach(var layer in temp.Layers)
            {
                layer.GetTexture().Dispose();
            }
            _allCombinations.RemoveAll(x => x.Name == name);
            if (File.Exists(Service.Configuration.PluginFolder + "\\" + name + ".json"))
            {
                File.Delete(Service.Configuration.PluginFolder + "\\" + name + ".json");
            }

            if (temp.Enabled)
            {
                foreach (var collection in temp.collection)
                {
                    Service.penumbraApi.RemoveTemporaryMod(temp);
                }

            }
            if (File.Exists(Service.Configuration.PluginFolder + "\\" + name + ".tex" ) )
            {
                File.Delete(Service.Configuration.PluginFolder + "\\" + name + ".tex");
            }
            return true;
        }catch (Exception e)
        {
            Service.Log.Error("Uh oh fucky wucky!!");
            return false;
        }
        
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="name"></param>
    public void SetSelectedCombo(string name)
    {
        ImageCombination temp;
        if (selectedCombination != name)
        {
             temp = GetImageCombination(selectedCombination);
            if (temp != null)
            {
                /*foreach (var layer in temp.Layers)
                {
                    layer.Dispose();
                }*/
            }
        }
        selectedCombination = name;
        
        GetImageCombination(name);


    }

    public String GetSelectedCombo() => selectedCombination;

    public void ClearSelectedCombo() => selectedCombination = String.Empty;

    public class HashConverter : JsonConverter<Blake3.Hash>
    {
        public override void WriteJson(JsonWriter writer, Blake3.Hash value, JsonSerializer serializer)
        {
            writer.WriteValue(System.Convert.ToBase64String(value.AsSpan()));
        }

        public override Blake3.Hash ReadJson(JsonReader reader, Type objectType, Blake3.Hash existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            Blake3.Hash hash = new Blake3.Hash();
            ReadOnlySpan<byte> ros = new ReadOnlySpan<byte>();
            ros = Convert.FromBase64String((String)reader.Value);
            hash.CopyFromBytes(ros);

            return hash;
        }
    }
    
    
    //TODO: Figure out a better more robust naming scheme for this 
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="combination"></param>
    public void WriteConfig(ImageCombination combination)
    {
        var json = JsonConvert.SerializeObject(combination, Formatting.Indented, new HashConverter());
        FilesystemUtil.WriteAllTextSafe(Service.Configuration.PluginFolder + $"\\{combination.Name}.json", json);
        
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="combination"></param>
    /// <returns></returns>
    public ImageCombination ReloadComboFromFile(ImageCombination combination)
    {
        var _path = Service.Configuration.PluginFolder + "\\" + combination.Name + ".json";

        var temp = ReadConfig(_path);
        RemoveImageCombination(combination.Name);
        _allCombinations.Add(temp);
        WriteConfig(temp);
        Task.Run(() => { temp.Compile();});
        return temp;
    }

    
    //TODO: Add support for BC compression
    /// <summary>
    /// 
    /// </summary>
    /// <param name="combination"></param>
    /// <returns></returns>
    public string WriteTexFile(ImageCombination combination)
    {
        Service.TextureManager.SaveAs(CombinedTexture.TextureSaveType.AsIs, false, true,
                                      combination.CombinedTexture.GetCurrent().BaseImage,
                                      Service.Configuration.PluginFolder + $"\\{combination.Name}.tex",
                                      combination.CombinedTexture.GetCurrent().RgbaPixels, combination.Res.width, combination.Res.height);
        return combination.Name + ".tex";
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public ImageCombination ReadConfig(String path)
    {

            var settings = new JsonSerializerSettings();
            settings.Converters.Add(new HashConverter());
            using StreamReader reader = new(path);
            string json = reader.ReadToEnd();
            JObject imageCombName = JObject.Parse(json);
            JToken comboName = imageCombName["Name"];
            var tempCombo = new ImageCombination(comboName.ToString());
            JsonConvert.PopulateObject(json,tempCombo,settings);
            return tempCombo;
    }

    /*public void AdjustPenumbraSettings(ImageCombination combination)
    {
        string json = File.ReadAllText(Service.Configuration.PluginFolder + "\\" + combination.Name + ".json");
        dynamic jsonObj = Newtonsoft.Json.JsonConvert.DeserializeObject(json);
        jsonObj["collections"] = combination.collection;
        jsonObj["Enabled"] = combination.Enabled;
        string output = Newtonsoft.Json.JsonConvert.SerializeObject(jsonObj, Newtonsoft.Json.Formatting.Indented);
        File.WriteAllText(Service.Configuration.PluginFolder + "\\" + combination.Name + ".json", output);
    }*/
    

    //TODO: Refactor for use here - only single mod at a time, dont bother caching for now
            //TODO: We only really need the file name, the mod its from (lazily match to accomodate helio fucked up names) and the settings associated
           /* public  void LoadSelection( String item, out SelectedPenumbraMod loaded ) {
            loaded = new();
            var files = new Dictionary<string, List<(string, string)>>();

            var baseModPath = Service.penumbraApi.GetModDirectory();
            if( string.IsNullOrEmpty( baseModPath ) ) return;

            try {
                var modPath = Path.Join( baseModPath, item );
                loaded.Meta = JsonConvert.DeserializeObject<PenumbraMeta>( File.ReadAllText( Path.Join( modPath, "meta.json" ) ) );

                var modFiles = Directory.GetFiles( modPath ).Where( x => x.EndsWith( ".json" ) && !x.EndsWith( "meta.json" ) );
                foreach( var modFile in modFiles ) {
                    try {
                        var modFileName = Path.GetFileName( modFile ).Replace( ".json", "" );
                        if( modFileName == "default_mod" ) {
                            var mod = JsonConvert.DeserializeObject<PenumbraModStruct>( File.ReadAllText( modFile ) );
                            if( mod.Files != null ) {
                                var defaultFiles = new List<(string, string)>();
                                AddToFiles( mod?.Files, defaultFiles, modPath );
                                files["default_mod"] = defaultFiles;
                            }
                        }
                        else {
                            var group = JsonConvert.DeserializeObject<PenumbraGroupStruct>( File.ReadAllText( modFile ) );
                            if( group.Options != null ) {
                                foreach( var option in group.Options.Where( x => x.Files != null ) ) {
                                    var optionFiles = new List<(string, string)>();
                                    AddToFiles( option?.Files, optionFiles, modPath );
                                    files[$"{group.Name} / {option.Name}"] = optionFiles;
                                }
                            }
                        }
                    }
                    catch( Exception e ) {
                        Dalamud.Error( e, modFile );
                    }
                }
            }
            catch( Exception e ) {
                Dalamud.Error( e, "Error reading Penumbra mods" );
            }

            loaded.SourceFiles = files.ToDictionary(
                x => x.Key,
                x => x.Value.Where( y => Path.Exists( y.Item2 ) ).Select( y => $"{y.Item1}|{y.Item2}" ).ToList() // actually use local path
            );

            loaded.ReplaceFiles = files.ToDictionary(
                x => x.Key,
                x => x.Value.Select( y => y.Item1 ).ToList()
            );
        }
    
    public List<ImageCombination> GetImageList()
        => _allCombinations;*/


}
