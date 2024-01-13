using Dalamud.Interface.Internal;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Common.Math;
using ImGuiNET;
using ImGuiScene;
using LibVLCSharp.Shared;
using Lumina.Data;
using RoleplayingMediaCore;
using RoleplayingVoiceDalamud.Catalogue;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using static Penumbra.Api.Ipc;
using Vector2 = System.Numerics.Vector2;

namespace RoleplayingVoice {
    internal class CatalogueWindow : Window {
        private System.Numerics.Vector2? windowSize;
        private Vector2? initialSize;
        private DalamudPluginInterface _pluginInterface;
        private string _cataloguePath;
        private string _currentCategory;
        Dictionary<string, CatalogueCategory> categories = new Dictionary<string, CatalogueCategory>();
        private Plugin _plugin;
        public CatalogueWindow(DalamudPluginInterface pluginInterface) : base("Catalogue Window") {
            //IsOpen = true;
            windowSize = Size = new Vector2(1000, 1000);
            initialSize = Size;
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
            if (categories.Count == 0) {
                ScanCatalogue();
            }
            foreach (var item in categories.Keys) {
                if (ImGui.Button(item)) {
                    _currentCategory = item;
                }
                ImGui.SameLine();
            }
            ImGui.Dummy(new Vector2(1, 1));
            if (!string.IsNullOrEmpty(_currentCategory)) {
                var category = categories[_currentCategory];
                int index = 0;
                int selectionIndex = 0;
                for (int y = 0; y < 3; y++) {
                    for (int x = 0; x < 3; x++) {
                        if (index < category.Images.Count) {
                            if (ImGui.ImageButton(category.Images[index].ImGuiHandle, new Vector2(250, 250))) {
                                selectionIndex = category.PageNumber * 9 + index;
                                Plugin.WearOutfit(category.CatalogueItems[selectionIndex].EquipObject);
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
                                Plugin.WearOutfit(category.CatalogueItems[category.SelectedIndex].Variants[index].EquipObject);
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
        }
    }
}