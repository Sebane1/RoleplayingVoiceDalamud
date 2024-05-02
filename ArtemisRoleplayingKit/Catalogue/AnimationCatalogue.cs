using Dalamud.Interface.Internal;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Common.Math;
using ImGuiNET;
using ImGuiScene;
using LibVLCSharp.Shared;
using Lumina.Data;
using Lumina.Excel.GeneratedSheets;
using RoleplayingMediaCore;
using RoleplayingVoiceDalamud.Catalogue;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using static Penumbra.Api.Ipc;
using Vector2 = System.Numerics.Vector2;

namespace RoleplayingVoice {
    internal class AnimationCatalogue : Window {
        private System.Numerics.Vector2? windowSize;
        private Vector2? initialSize;
        private DalamudPluginInterface _pluginInterface;
        List<string> _animationNames = new List<string>();
        private Plugin _plugin;
        int pageNumber = 0;
        int maxItemsPerPage = 25;
        public AnimationCatalogue(DalamudPluginInterface pluginInterface) : base("Animation Window") {
            //IsOpen = true;
            windowSize = Size = new Vector2(400, 700);
            initialSize = Size;
            SizeCondition = ImGuiCond.Always;
            //Position = new Vector2(1920/2 + 250, 0);
            Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.AlwaysAutoResize;
            _pluginInterface = pluginInterface;
        }

        public Plugin Plugin { get => _plugin; set => _plugin = value; }
        public List<string> AnimationNames { get => _animationNames; set => _animationNames = value; }

        public override void Draw() {
            try {
                int index = 0;
                int selectionIndex = 0;
                for (int y = 0; y < maxItemsPerPage; y++) {
                    if (index < _animationNames.Count) {
                        selectionIndex = pageNumber * maxItemsPerPage + index;
                        string animation = _animationNames[selectionIndex];
                        if (ImGui.Button(animation)) {
                            Plugin.DoAnimation(animation.ToLower());
                        }
                        index++;
                    } else {
                        break;
                    }
                }
            } catch (Exception ex) {

            }
            ImGui.Dummy(new Vector2(10));
            if (ImGui.Button("Previous Page")) {
                if (pageNumber > 0) {
                    pageNumber--;
                }
            }
            ImGui.SameLine();
            if (ImGui.Button("Next Page")) {
                if (pageNumber < (int)((float)_animationNames.Count / (float)maxItemsPerPage)) {
                    pageNumber++;
                }
            }
        }
    }
}