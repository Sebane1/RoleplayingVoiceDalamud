using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ImGuiNET;
using RoleplayingVoiceDalamud.Catalogue;
using System;
using System.Collections.Generic;
using System.IO;
using Vector2 = System.Numerics.Vector2;

namespace RoleplayingVoice {
    internal class CatalogueWindow : Window {
        private DalamudPluginInterface _pluginInterface;
        private string _cataloguePath;
        private string _currentCategory;
        Dictionary<string, CatalogueCategory> categories = new Dictionary<string, CatalogueCategory>();
        private Plugin _plugin;
        public CatalogueWindow(DalamudPluginInterface pluginInterface) : base("Catalogue Window (Alpha)") {
            //IsOpen = true;
            //windowSize = Size = new Vector2(1000, 1000);
            //initialSize = Size;
            SizeCondition = ImGuiCond.Always;
            //Position = new Vector2(1920/2 + 250, 0);
            Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.AlwaysAutoResize;
            _pluginInterface = pluginInterface;
        }
        public void ScanCatalogue() {
            categories.Clear();
            foreach (string file in Directory.EnumerateFiles(CataloguePath)) {
                SendToCategory(new CatalogueItem(file));
            }
            foreach (var catalogueCategory in categories) {
                catalogueCategory.Value.RefreshCurrentImages();
            }
        }
        public override void OnOpen() {
            base.OnOpen();
            ScanCatalogue();
        }
        public void SendToCategory(CatalogueItem item) {
            if (!categories.ContainsKey(item.EquipObject.Type.ToString())) {
                categories[item.EquipObject.Type.ToString()] = new CatalogueCategory(_pluginInterface);
                categories[item.EquipObject.Type.ToString()].Add(item);
            } else {
                categories[item.EquipObject.Type.ToString()].Add(item);
            }
        }
        public string CataloguePath {
            get => _cataloguePath;
            set {
                _cataloguePath = value;
            }
        }

        public Plugin Plugin { get => _plugin; set => _plugin = value; }

        public override void Draw() {
            if (categories.Count > 0) {
                foreach (var item in categories.Keys) {
                    if (ImGui.Button(item)) {
                        _currentCategory = item;
                    }
                    ImGui.SameLine();
                }
                ImGui.Dummy(new Vector2(1, 1));
                if (ImGui.Button("Reset Mod Selections", new Vector2(200, 30))) {
                    PenumbraAndGlamourerHelperFunctions.CleanSlate(Guid.Empty, _plugin.ModelMods.Keys, _plugin.ModelDependancyMods.Keys);
                }

                if (!string.IsNullOrEmpty(_currentCategory)) {
                    var category = categories[_currentCategory];
                    int index = 0;
                    int selectionIndex = 0;
                    for (int y = 0; y < 3; y++) {
                        for (int x = 0; x < 3; x++) {
                            if (index < category.Images.Count) {
                                if (ImGui.ImageButton(category.Images[index].ImGuiHandle, new Vector2(250, 250))) {
                                    selectionIndex = category.PageNumber * 9 + index;
                                    PenumbraAndGlamourerHelperFunctions.WearOutfit(category.CatalogueItems[selectionIndex].EquipObject, Guid.Empty, 0, _plugin.ModelMods.Keys);
                                    category.SelectItem(selectionIndex);
                                }
                                index++;
                                if (x < 2 && index < category.Images.Count) {
                                    ImGui.SameLine();
                                }
                            } else {
                                break;
                            }
                        }
                    }
                    index = 0;
                    if (category.VariantImages.Count > 0) {
                        ImGui.LabelText("##labelCatalogueWindow", "Variants Of Selected Category Item");
                    }
                    for (int y = 0; y < Math.Ceiling(category.VariantImages.Count / 7f); y++) {
                        for (int x = 0; x < 7; x++) {
                            if (index < category.VariantImages.Count) {
                                if (ImGui.ImageButton(categories[_currentCategory].VariantImages[index].ImGuiHandle, new Vector2(100, 100))) {
                                    PenumbraAndGlamourerHelperFunctions.WearOutfit(category.CatalogueItems[category.SelectedIndex].Variants[index].EquipObject, Guid.Empty, 0, _plugin.ModelMods.Keys);
                                }
                                index++;
                                if (x < 6 && index < categories[_currentCategory].VariantImages.Count) {
                                    ImGui.SameLine();
                                } else {
                                    break;
                                }
                            }
                        }
                    }
                    if (ImGui.Button("Previous Page")) {
                        if (categories[_currentCategory].PageNumber > 0) {
                            categories[_currentCategory].PageNumber--;
                            categories[_currentCategory].RefreshCurrentImages();
                        }
                    }
                    ImGui.SameLine();
                    if (ImGui.Button("Next Page")) {
                        if (categories[_currentCategory].PageNumber < (int)((float)categories[_currentCategory].CatalogueItems.Count / 9f)) {
                            categories[_currentCategory].PageNumber++;
                            categories[_currentCategory].RefreshCurrentImages();
                        }
                    }
                }
            } else {
                ImGui.Text("There are currently no mods in your image catalogue.\r\n" +
                    "You have the option to have images taken of all your mods for future ease of finding and picking your outfits!\r\n" +
                    "\r\n\r\n" +
                    "Please note that this process will require taking photos of all your clothing mods and could take a while.\r\n" +
                    "Position your camera at an angle that has your entire character in view, and make sure any UI is out of the way.\r\n\r\n" +
                    "Be aware that this catalogue system automatically sets your clothing mods active or inactive and their priorities when in use.\r\n" +

                    "For the moment, you will need to assign a brand new collection with only body dependancies enabled to use scan your mods.");
            }
            if (ImGui.Button("Scan And Catalogue Mods", new Vector2(ImGui.CalcItemWidth(), 50))) {
                _plugin.StartCatalogingItems();
                IsOpen = false;
            }
        }
    }
}