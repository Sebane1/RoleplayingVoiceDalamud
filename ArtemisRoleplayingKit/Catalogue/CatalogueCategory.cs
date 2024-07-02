using Dalamud.Interface.Internal;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System.Collections.Generic;

namespace RoleplayingVoiceDalamud.Catalogue {
    public class CatalogueCategory {
        public string Name { get; set; }
        private List<CatalogueItem> catalogueItems = new List<CatalogueItem>();
        private Dictionary<string, CatalogueItem> catalogueItemDictionary = new Dictionary<string, CatalogueItem>();
        private IDalamudPluginInterface _pluginInterface;
        private ITextureProvider _textureProvider;
        List<ISharedImmediateTexture> _images = new List<ISharedImmediateTexture>();
        List<ISharedImmediateTexture> _variantImages = new List<ISharedImmediateTexture>();
        private int _selectedIndex;

        public CatalogueCategory(IDalamudPluginInterface pluginInterface, ITextureProvider textureProvider) {
            _pluginInterface = pluginInterface;
            _textureProvider = textureProvider;
        }

        public int PageNumber { get; set; }
        public List<CatalogueItem> CatalogueItems { get => catalogueItems; set => catalogueItems = value; }
        public List<ISharedImmediateTexture> Images { get => _images; set => _images = value; }
        public List<ISharedImmediateTexture> VariantImages { get => _variantImages; set => _variantImages = value; }
        public int SelectedIndex { get => _selectedIndex; set => _selectedIndex = value; }

        public void Add(CatalogueItem item) {
            if (!catalogueItemDictionary.ContainsKey(item.EquipObject.Name)) {
                catalogueItemDictionary[item.EquipObject.Name] = item;
                catalogueItems.Add(item);
            } else {
                catalogueItemDictionary[item.EquipObject.Name].Variants.Add(item);
            }
        }

        public void RefreshCurrentImages() {
            _images.Clear();
            int index = PageNumber * 3 * 3;
            for (int i = 0; i < 3 * 3; i++) {
                if (index < CatalogueItems.Count) {
                    try {
                        _images.Add(_textureProvider.GetFromFile(CatalogueItems[index++].ImagePath));
                    } catch {

                    }
                } else {
                    break;
                }
            }
        }
        public void SelectItem(int index) {
            _variantImages.Clear();
            _selectedIndex = index;
            foreach (CatalogueItem catalogueItem in CatalogueItems[index].Variants) {
                _variantImages.Add(_textureProvider.GetFromFile(catalogueItem.ImagePath));
            }
        }
    }
}
