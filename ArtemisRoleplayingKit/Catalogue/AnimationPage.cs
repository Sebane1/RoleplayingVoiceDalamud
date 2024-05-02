using Dalamud.Interface.Internal;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.Collections.Generic;

namespace RoleplayingVoiceDalamud.Catalogue {
    public class AnimpationPage {
        public string Name { get; set; }
        private List<CatalogueItem> catalogueItems = new List<CatalogueItem>();
        private Dictionary<string, CatalogueItem> catalogueItemDictionary = new Dictionary<string, CatalogueItem>();
        private DalamudPluginInterface _pluginInterface;
        List<IDalamudTextureWrap> _images = new List<IDalamudTextureWrap>();
        List<IDalamudTextureWrap> _variantImages = new List<IDalamudTextureWrap>();
        private int _selectedIndex;

        public AnimpationPage(DalamudPluginInterface pluginInterface) {
            _pluginInterface = pluginInterface;
        }

        public int PageNumber { get; set; }
        public List<CatalogueItem> CatalogueItems { get => catalogueItems; set => catalogueItems = value; }
        public List<IDalamudTextureWrap> Images { get => _images; set => _images = value; }
        public List<IDalamudTextureWrap> VariantImages { get => _variantImages; set => _variantImages = value; }
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
                        _images.Add(_pluginInterface.UiBuilder.LoadImage(CatalogueItems[index++].ImagePath));
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
                _variantImages.Add(_pluginInterface.UiBuilder.LoadImage(catalogueItem.ImagePath));
            }
        }
    }
}
