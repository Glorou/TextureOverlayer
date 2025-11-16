using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Configuration;
using Dalamud.Plugin;
using OtterTex;
using Penumbra.Api.Enums;
using Penumbra.Api.Helpers;
using Penumbra.Api.IpcSubscribers;
using TextureOverlayer;
using TextureOverlayer.Textures;
using TextureOverlayer.Utils;

namespace TextureOverlayer.Interop
{
    internal class PenumbraIpc(IDalamudPluginInterface pluginInterface) : IDisposable
    {


        private IDalamudPluginInterface _pluginInterface { get; }

        private Dictionary<string, string> modList = new();
        private readonly GetModDirectory _getModDirectory = new GetModDirectory(pluginInterface);
        private readonly GetCollections _getCollections = new GetCollections(pluginInterface);
        private readonly AddTemporaryMod _addTemporaryMod = new AddTemporaryMod(pluginInterface);
        private readonly RemoveTemporaryMod _removeTemporaryMod = new RemoveTemporaryMod(pluginInterface);
        private readonly RedrawAll _redrawAll = new RedrawAll(pluginInterface);

        private readonly ApiVersion _apiVersion = new ApiVersion(pluginInterface);
        private readonly RedrawCollectionMembers _redrawCollection = new RedrawCollectionMembers(pluginInterface);

        private GetModPath _getModPath = new GetModPath(pluginInterface);
        public Dictionary<string, string> Modlist
        {

            get {return modList;} 
            set => this.modList = new GetModList(pluginInterface).Invoke();
        }

        public void RedrawAll()
        {
            _redrawAll.Invoke();
        }

        public (int, int) ApiVersion()
        {
            return _apiVersion.Invoke();
        }
        public void RedrawCollection(Guid Collection)
        {
            _redrawCollection.Invoke(Collection);
        }
        public String GetModDirectory()
        {
            return _getModDirectory.Invoke();
        }

        public Dictionary<Guid, string> GetCollections()
        {
            return _getCollections.Invoke();
        }


        public PenumbraApiEc AddTemporaryMod(ImageCombination texture)
        {
            List <PenumbraApiEc> results = new();
            foreach(var collection in texture.collection)
            {
                results.Add(_addTemporaryMod.Invoke(texture.Name +"TO", collection.Key, new Dictionary<string, string>{{texture._gamepath, Service.Configuration.PluginFolder +"\\"+ texture.FileName}}, string.Empty, 99));
            }
            
            _redrawAll.Invoke();
            return results.LastOrDefault();

        }

        public PenumbraApiEc RemoveTemporaryMod(ImageCombination texture)
        {
            List<PenumbraApiEc> results = new();
            foreach (var collection in texture.collection)
            {
                results.Remove(_removeTemporaryMod.Invoke(texture.Name + "TO", collection.Key, 99));
            }
            _redrawAll.Invoke();
            return results.LastOrDefault();
        }

        public PenumbraApiEc RemoveTemporaryModCollection(ImageCombination texture, (Guid, string) Collection)
        {
            var temp =  _removeTemporaryMod.Invoke(texture.Name +"TO", Collection.Item1, 99);
            _redrawAll.Invoke();
            return temp;
        }
        
        public PenumbraApiEc AddTemporaryModCollection(ImageCombination texture, (Guid, string) Collection)
        {
            var temp =  _addTemporaryMod.Invoke(texture.Name +"TO", Collection.Item1, new Dictionary<string, string>{{texture._gamepath, Service.Configuration.PluginFolder +"\\"+ texture.FileName}}, string.Empty, 99);
            return temp;
        }
        /*
        private readonly EventSubscriber<string> modDeletedEventSubscriber =
        ModDeleted.Subscriber(pluginInterface, Service.penumbraApi.UpdateModList);
        private readonly EventSubscriber<string, string> modMovedEventSubscriber =
        ModMoved.Subscriber(pluginInterface, Service.penumbraApi.UpdateModList);*/
        
            /* public  void UpdateModList(string empty)
             {
                 Modlist = Service.penumbraApi.GetModList();
             }
             public  void UpdateModList(string empty, string empty1)
             {
                 Modlist = Service.penumbraApi.GetModList();
             } */



            public List<String> GetTextureList(String modName)
            {


                var files = Directory.GetFiles($"{Service.penumbraApi.GetModDirectory().ToString()}\\{modName}" , "*.tex", SearchOption.AllDirectories).ToList();
           
                return files;
            }
       
            public String setupFolderStructure()
            {
                if (Directory.Exists(Service.penumbraApi.GetModDirectory().ToString() + "\\TextureOverlayer"))
                {
                    if(Directory.Exists(Service.penumbraApi.GetModDirectory().ToString() + "\\TextureOverlayer\\Raw"))
                    {
                        return Service.penumbraApi.GetModDirectory().ToString() + "\\TextureOverlayer";
                    }
                    else
                    {
                        Directory.CreateDirectory(Service.penumbraApi.GetModDirectory().ToString() + "\\TextureOverlayer\\Raw");
                        return Service.penumbraApi.GetModDirectory().ToString() + "\\TextureOverlayer";
                    }

                }
                else
                {
                    try
                    {
                        Directory.CreateDirectory(Service.penumbraApi.GetModDirectory().ToString() + "\\TextureOverlayer");
                        Directory.CreateDirectory(Service.penumbraApi.GetModDirectory().ToString() + "\\TextureOverlayer\\Raw");
                        return Service.penumbraApi.GetModDirectory().ToString() + "\\TextureOverlayer";
                    }
                    catch (Exception e)
                    {
                        Service.Log.Error(e.ToString());
                    }
            
                }

                return String.Empty;
            }

       
       
            public void Dispose()
            {
                /*modAddedEventSubscriber.Dispose();
                modDeletedEventSubscriber.Dispose();
                modMovedEventSubscriber.Dispose();*/
            }




    }
}
