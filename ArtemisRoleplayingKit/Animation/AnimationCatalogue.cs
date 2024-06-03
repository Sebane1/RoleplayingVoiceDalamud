using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using ImGuiNET;
using RoleplayingVoiceDalamud.Catalogue;
using System;
using System.Collections.Generic;
using System.Linq;
using Vector2 = System.Numerics.Vector2;

namespace RoleplayingVoice {
    internal class AnimationCatalogue : Window {
        Dictionary<string, AnimationPage> _animationPages = new Dictionary<string, AnimationPage>();
        string _currentCategory = "All";
        private Plugin _plugin;
        int maxItemsPerPage = 20;
        int maxItemsPerCategoryPage = 6;
        int _categoryPage = 0;
        private int _currentSelection;
        private bool incognito;
        private Dictionary<string, Character> _characterList;

        public AnimationCatalogue(DalamudPluginInterface pluginInterface) : base("Animation Window") {
            SizeCondition = ImGuiCond.Always;
            Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.AlwaysAutoResize;
        }

        public Plugin Plugin { get => _plugin; set => _plugin = value; }
        public List<string> AnimationNames { get => _animationPages[_currentCategory].AnimationItems; }

        public void AddNewList(List<string> list) {
            _animationPages.Clear();
            foreach (var item in list) {
                if (!string.IsNullOrEmpty(item)) {
                    string preparedString = CategoryCleaner(item);
                    if (!string.IsNullOrEmpty(preparedString)) {
                        string category = "All";
                        AddItem(category, item);
                        if (preparedString.StartsWith("*")) {
                            category = preparedString.Split("*")[1].Replace(" ", "");
                            AddItem(category, item);
                        } else {
                            category = "Other";
                            AddItem(category, item);
                        }
                    }
                }
            }
        }

        public void AddItem(string category, string item) {
            if (!_animationPages.ContainsKey(category)) {
                _animationPages[category] = new AnimationPage();
            }
            _animationPages[category].Add(item);
        }
        public string CategoryCleaner(string item) {
            return item.Replace("[", "*").Replace("]", "*").Replace("(", "*")
                       .Replace(")", "*").Replace("[", "*").Replace("[", "*")
                       .Replace("~", "*").Replace("`", "*");
        }
        public override void Draw() {
            ImGui.BeginTable("##Animation Table", 2);
            ImGui.TableSetupColumn("Character List", ImGuiTableColumnFlags.WidthFixed, 200);
            ImGui.TableSetupColumn("Custom Animation Mods", ImGuiTableColumnFlags.WidthStretch, 300);
            ImGui.TableHeadersRow();
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            DrawObjectList();
            ImGui.TableSetColumnIndex(1);
            DrawAnimationMenu();
            ImGui.EndTable();
        }

        private void DrawObjectList() {
            if (ImGui.Button("Toggle Incognito", new Vector2(ImGui.GetColumnWidth(), 30))) {
                incognito = !incognito;
            }
            _characterList = Plugin.GetLocalCharacters(incognito);
            ImGui.SetNextItemWidth(ImGui.GetColumnWidth());
            ImGui.ListBox("##NPCEditing", ref _currentSelection, _characterList.Keys.ToArray(), _characterList.Count, 29);
        }

        private void DrawAnimationMenu() {
            if (_animationPages.Count > 0) {
                if (ImGui.Button("<")) {
                    if (_categoryPage > 0) {
                        _categoryPage--;
                    }
                }
                ImGui.SameLine();
                try {
                    int index = 0;
                    int selectionIndex = 0;
                    for (int y = 0; y < maxItemsPerCategoryPage; y++) {
                        if (index < _animationPages.Keys.Count) {
                            selectionIndex = _categoryPage * maxItemsPerCategoryPage + index;
                            string category = _animationPages.Keys.ElementAt(selectionIndex);
                            if (ImGui.Button(category)) {
                                _currentCategory = category;
                            }
                            ImGui.SameLine();
                            index++;
                        } else {
                            break;
                        }
                    }
                } catch (Exception ex) {
                    Plugin.PluginLog.Warning(ex, ex.Message);
                }
                if (ImGui.Button(">")) {
                    if (_categoryPage < (int)((float)_animationPages.Keys.Count / (float)maxItemsPerCategoryPage)) {
                        _categoryPage++;
                    }
                }
                ImGui.Dummy(new Vector2(1, 1));
                ImGui.Dummy(new Vector2(1, 1));
                try {
                    int index = 0;
                    int selectionIndex = 0;
                    for (int y = 0; y < maxItemsPerPage; y++) {
                        if (index < _animationPages[_currentCategory].AnimationItems.Count) {
                            selectionIndex = _animationPages[_currentCategory].PageNumber * maxItemsPerPage + index;
                            string animation = _animationPages[_currentCategory].AnimationItems[selectionIndex];
                            if (ImGui.Button(animation)) {
                                Plugin.DoAnimation(animation.ToLower(), 0, _characterList.ElementAt(_currentSelection).Value);
                            }
                            index++;
                        } else {
                            break;
                        }
                    }
                } catch (Exception ex) {
                    Plugin.PluginLog.Warning(ex, ex.Message);
                }
                ImGui.Dummy(new Vector2(10));
                if (ImGui.Button("Previous Page")) {
                    if (_animationPages[_currentCategory].PageNumber > 0) {
                        _animationPages[_currentCategory].PageNumber--;
                    }
                }
                ImGui.SameLine();
                if (ImGui.Button("Next Page")) {
                    if (_animationPages[_currentCategory].PageNumber <
                        (int)((float)_animationPages[_currentCategory].AnimationItems.Count / (float)maxItemsPerPage)) {
                        _animationPages[_currentCategory].PageNumber++;
                    }
                }
            } else {
                ImGui.Text("No valid animation mods detected!");
            }
        }
    }
}