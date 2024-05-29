using Dalamud.Interface.Internal;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Common.Math;
using ImGuiNET;
using ImGuiScene;
using OtterGui;
using RoleplayingMediaCore;
using RoleplayingMediaCore.Twitch;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Penumbra.Api;
using static RoleplayingVoice.PluginWindow;
using Vector2 = System.Numerics.Vector2;

namespace RoleplayingVoice {
    internal class NPCPersonalityWindow : Window {
        private DalamudPluginInterface _pluginInterface;
        private string[] npcItemNames = new string[] { };
        private List<CustomNpcCharacter> _customNpcCharacters = new List<CustomNpcCharacter>();
        private int _currentSelection = 0;
        private Dictionary<Guid, string> _currentGlamourerDesigns = new Dictionary<Guid, string>();
        BetterComboBox designList = new BetterComboBox("##savedDesigns", new string[0], 0, 100);
        Plugin _plugin;

        public Plugin Plugin { get => _plugin; set => _plugin = value; }
        public List<CustomNpcCharacter> CustomNpcCharacters { get => _customNpcCharacters; set => _customNpcCharacters = value; }

        public NPCPersonalityWindow(DalamudPluginInterface pluginInterface) :
            base("NPC Personality Window") {
            _pluginInterface = pluginInterface;
            _customNpcCharacters.Add(new CustomNpcCharacter());
            Size = new Vector2(550, 500);
        }
        public override void OnOpen() {
            base.OnOpen();
            RefreshDesignList();
        }
        public override void OnClose() {
            base.OnClose();
            RefreshDesignList();
        }
        public void RefreshDesignList() {
            _currentGlamourerDesigns = _plugin.GetGlamourerDesigns();
            var list = _currentGlamourerDesigns.Values.ToList();
            list.Sort();
            designList = new BetterComboBox("##savedDesigns", list.ToArray(), 0, 100);
            designList.OnSelectedIndexChanged += DesignList_OnSelectedIndexChanged;
        }

        private void DesignList_OnSelectedIndexChanged(object sender, EventArgs e) {
            if (_currentGlamourerDesigns != null && _currentGlamourerDesigns.Count > 0) {
                _customNpcCharacters[_currentSelection].NpcGlamourerAppearanceString = _currentGlamourerDesigns
                .ElementAt(_currentGlamourerDesigns.Values.IndexOf(designList.Contents[designList.SelectedIndex])).Key.ToString();
            }
        }

        public override void Draw() {
            try {
                if (_currentGlamourerDesigns.Count is 0) {
                    RefreshDesignList();
                }
                RefreshNPCItemNames();
                ImGui.BeginTable("##NpcTable", 2);
                ImGui.TableSetupColumn("Custom Npc", ImGuiTableColumnFlags.WidthFixed, 200);
                ImGui.TableSetupColumn("Custom Npc Configuration", ImGuiTableColumnFlags.WidthStretch, 300);
                ImGui.TableHeadersRow();
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                DrawListBox();
                ImGui.TableSetColumnIndex(1);
                DrawNPCConfigurator();
                ImGui.EndTable();
            } catch (Exception e) {
                Plugin.PluginLog.Warning(e, e.Message);
            }
        }
        public void LoadNPCCharacters(List<CustomNpcCharacter> customNpcCharacters) {
            if (customNpcCharacters.Count > 0) {
                _customNpcCharacters = customNpcCharacters;
            }
        }
        public void SaveNPCCharacters() {
            Plugin.Config.CustomNpcCharacters = _customNpcCharacters;
            Plugin.Config.Save();
        }

        private void DrawListBox() {
            ImGui.SetNextItemWidth(ImGui.GetColumnWidth());
            ImGui.ListBox("##NPCEditing", ref _currentSelection, npcItemNames, npcItemNames.Length, 25);

            if (ImGui.Button("+", new Vector2(35))) {
                _customNpcCharacters.Add(new CustomNpcCharacter());
                SaveNPCCharacters();
            }

            ImGui.SameLine();
            if (ImGui.Button("-", new Vector2(35))) {
                _customNpcCharacters.RemoveAt(_currentSelection);
                _currentSelection = 0;
                SaveNPCCharacters();
            }
        }

        private void DrawNPCConfigurator() {
            if (_currentGlamourerDesigns.Count > 0) {
                if (_customNpcCharacters.Count > 0) {
                    Guid guid = Guid.Empty;
                    Guid.TryParse(_customNpcCharacters[_currentSelection].NpcGlamourerAppearanceString, out guid);
                    if (_currentGlamourerDesigns.ContainsKey(guid)) {
                        var list = new List<string>();
                        list.AddRange(designList.Contents);
                        designList.SelectedIndex = list.IndexOf(_currentGlamourerDesigns[Guid.Parse(_customNpcCharacters[_currentSelection].NpcGlamourerAppearanceString)]);
                    }

                    ImGui.LabelText("##personalityLabel", "NPC Name");
                    ImGui.SetNextItemWidth(ImGui.GetColumnWidth());
                    ImGui.InputText("##NPCName", ref _customNpcCharacters[_currentSelection].NpcName, 255);

                    ImGui.LabelText("##miniontoreplaceLabel", "Minion To Replace");
                    ImGui.SetNextItemWidth(ImGui.GetColumnWidth());
                    ImGui.InputText("##MinionToReplace", ref _customNpcCharacters[_currentSelection].MinionToReplace, 255);

                    ImGui.LabelText("##glamourerLabel", "Glamourer Design Appearance");
                    designList.Width = (int)ImGui.GetColumnWidth();
                    designList.Draw();

                    ImGui.LabelText("##personality", "NPC Greeting");
                    ImGui.SetNextItemWidth(ImGui.GetColumnWidth());
                    ImGui.InputText("##Greeting", ref _customNpcCharacters[_currentSelection].NPCGreeting, 255);

                    ImGui.LabelText("##personality", "NPC Personality");
                    ImGui.SetNextItemWidth(ImGui.GetColumnWidth());
                    ImGui.InputText("NPC Personality", ref _customNpcCharacters[_currentSelection].NpcPersonality, 255);
                    ImGui.TextWrapped(_customNpcCharacters[_currentSelection].NpcPersonality);

                    if (ImGui.Button("Summon/Dismiss")) {
                        _plugin.MessageQueue.Enqueue("/minion " + @"""" + _customNpcCharacters[_currentSelection].MinionToReplace + @"""");
                    }
                }
            } else {
                ImGui.Text("Glamourer plugin was not detected! This is required to make Custom NPCs");
            }
        }

        public void RefreshNPCItemNames() {
            List<string> names = new List<string>();
            foreach (var item in _customNpcCharacters) {
                names.Add(item.NpcName);
            }
            if (_currentSelection >= names.Count) {
                _currentSelection = 0;
            }
            if (names.Count > 0) {
                npcItemNames = names.ToArray();
            } else {
                npcItemNames = new string[0];
            }
        }
    }
}