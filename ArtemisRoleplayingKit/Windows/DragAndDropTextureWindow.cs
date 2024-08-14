using Dalamud.Interface.DragDrop;
using Dalamud.Interface.Internal;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Common.Math;
using ImGuiNET;
using ImGuiScene;
using FFXIVClientStructs.Havok;
using System;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Vector2 = System.Numerics.Vector2;
using FFXIVLooseTextureCompiler;
using FFXIVLooseTextureCompiler.PathOrganization;
using System.Collections.Generic;
using System.Threading.Tasks;
using Penumbra.Api;
using FFXIVLooseTextureCompiler.Export;
using FFXIVLooseTextureCompiler.Racial;
using RoleplayingVoiceDalamud.Glamourer;
using System.Threading;
using Ktisis.Structs;
using Ktisis.Structs.Actor;
using Ktisis.Structs.Bones;
using Ktisis.Structs.Extensions;
using RoleplayingVoiceDalamud;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using Microsoft.VisualBasic.Devices;
using static Anamnesis.Files.PoseFile;
using Bone = Ktisis.Structs.Bones.Bone;
using FFXIVLooseTextureCompiler.ImageProcessing;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Objects.Enums;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using ICharacter = Dalamud.Game.ClientState.Objects.Types.ICharacter;
using RoleplayingVoiceDalamud.Catalogue;
using LooseTextureCompilerCore;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using static FFXIVLooseTextureCompiler.ImageProcessing.ImageManipulation;
using LooseTextureCompilerCore.Racial;

namespace RoleplayingVoice {
    internal class DragAndDropTextureWindow : Window {
        IDalamudTextureWrap textureWrap;
        private IDalamudPluginInterface _pluginInterface;
        private readonly IDragDropManager _dragDropManager;
        private readonly MemoryStream _blank;
        Plugin plugin;
        private ImGuiWindowFlags _defaultFlags;
        private ImGuiWindowFlags _dragAndDropFlags;
        private TextureProcessor _textureProcessor;
        private string _exportStatus;
        private bool _lockDuplicateGeneration;
        private object _currentMod;
        private CharacterCustomization _currentCustomization;
        private string[] _choiceTypes;
        private string[] _bodyNames;
        private string[] _bodyNamesSimplified;
        private string[] _genders;
        private string[] _faceTypes;
        private string[] _faceParts;
        private string[] _faceScales;
        private ITextureProvider _textureProvider;
        private BodyDragPart bodyDragPart;
        private bool _alreadyLoadingFrame;
        private byte[] _nextFrameToLoad;
        private IDalamudTextureWrap _frameToLoad;
        private byte[] _lastLoadedFrame;
        private Bone _closestBone;
        private Vector2 _cursorPosition;

        List<string> _alreadyAddedBoneList = new List<string>();
        List<Tuple<string, float>> boneSorting = new List<Tuple<string, float>>();
        public Plugin Plugin { get => plugin; set => plugin = value; }

        public DragAndDropTextureWindow(IDalamudPluginInterface pluginInterface, IDragDropManager dragDropManager, ITextureProvider textureProvider) :
            base("DragAndDropTexture", ImGuiWindowFlags.NoFocusOnAppearing
                | ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoMouseInputs | ImGuiWindowFlags.NoInputs
                | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoTitleBar, true) {
            _defaultFlags = ImGuiWindowFlags.NoFocusOnAppearing
                | ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoMouseInputs | ImGuiWindowFlags.NoInputs
                | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoTitleBar;
            _dragAndDropFlags = ImGuiWindowFlags.NoFocusOnAppearing
                | ImGuiWindowFlags.NoBringToFrontOnFocus
                | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoTitleBar;
            IsOpen = true;
            Size = new Vector2(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);
            SizeCondition = ImGuiCond.None;
            _pluginInterface = pluginInterface;
            Position = new Vector2(0, 0);
            AllowClickthrough = true;
            _dragDropManager = dragDropManager;
            _blank = new MemoryStream();
            Bitmap none = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);
            Graphics graphics = Graphics.FromImage(none);
            graphics.Clear(Color.Transparent);
            none.Save(_blank, ImageFormat.Png);
            _blank.Position = 0;
            // This will be used for underlay textures.
            // The user will need to download a mod pack with the following path until there is a better way to acquire underlay assets.
            string underlayTexturePath = "";
            // This should reference the xNormal install no matter where its been installed.
            // If this path is not found xNormal reliant functions will be disabled until xNormal is installed.
            _xNormalPath = @"C:\ProgramData\Microsoft\Windows\Start Menu\Programs\xNormal\3.19.3\xNormal (x64).lnk";
            _textureProcessor = new TextureProcessor(underlayTexturePath);
            _textureProcessor.OnStartedProcessing += TextureProcessor_OnStartedProcessing;
            _textureProcessor.OnLaunchedXnormal += TextureProcessor_OnLaunchedXnormal;
            _choiceTypes = new string[] { "Detailed", "Simple", "Dropdown", "Group Is Checkbox" };
            _bodyNames = new string[] { "Vanilla and Gen2", "BIBO+", "Gen3", "TBSE and HRBODY", "TAIL", "Otopop" };
            _bodyNamesSimplified = new string[] { "BIBO+", "Gen3", "TBSE and HRBODY", "Otopop" };
            _genders = new string[] { "Masculine", "Feminine" };
            _faceTypes = new string[] { "Face 1", "Face 2", "Face 3", "Face 4", "Face 5", "Face 6", "Face 7", "Face 8", "Face 9" };
            _faceParts = new string[] { "Face", "Eyebrows", "Eyes", "Ears", "Face Paint", "Hair", "Face B", "Etc B" };
            _faceScales = new string[] { "Vanilla Scales", "Scaleless Vanilla", "Scaleless Varied" };
            _textureProvider = textureProvider;
        }

        private void TextureProcessor_OnLaunchedXnormal(object? sender, EventArgs e) {
            _exportStatus = "Waiting For XNormal To Generate Assets For";
            plugin.Chat.Print("[Artemis Roleplaying Kit] " + _exportStatus);
        }

        private void TextureProcessor_OnStartedProcessing(object? sender, EventArgs e) {
            _exportStatus = "Compiling Assets For Mod";
            plugin.Chat.Print("[Artemis Roleplaying Kit] " + _exportStatus);
        }
        // to get the currently active display's bounding rectangle
        public System.Drawing.Rectangle GetCurrentDisplaySize() {
            return Screen.FromPoint(Cursor.Position).Bounds;
        }
        public System.Drawing.Point GetCurrentCursorPosition() {
            System.Drawing.Rectangle displayDimensions = GetCurrentDisplaySize();
            Point cursorPos = Cursor.Position;
            int cursorRelativeToDisplayX = cursorPos.X - displayDimensions.X;
            int cursorRelativeToDisplayY = cursorPos.Y - displayDimensions.Y;
            return new Point(cursorRelativeToDisplayX, cursorRelativeToDisplayY);
        }

        public override void Draw() {
            if (IsOpen) {
                if (!_lockDuplicateGeneration) {
                    Guid mainPlayerCollection = Guid.Empty;
                    Guid selectedPlayerCollection = Guid.Empty;
                    KeyValuePair<string, ICharacter> selectedPlayer = new KeyValuePair<string, ICharacter>("", null);
                    bool holdingModifier = ImGui.GetIO().KeyShift;
                    _dragDropManager.CreateImGuiSource("TextureDragDrop", m => m.Extensions.Any(e => ValidTextureExtensions.Contains(e.ToLowerInvariant())), m => {
                        try {
                            mainPlayerCollection = PenumbraAndGlamourerIpcWrapper.Instance.GetCollectionForObject.Invoke(plugin.ClientState.LocalPlayer.ObjectIndex).Item3.Id;
                            List<KeyValuePair<string, ICharacter>> _objects = new List<KeyValuePair<string, ICharacter>>();
                            _objects.Add(new KeyValuePair<string, ICharacter>(plugin.ClientState.LocalPlayer.Name.TextValue, Plugin.ClientState.LocalPlayer as ICharacter));
                            bool oneMinionOnly = false;
                            foreach (var item in Plugin.GetNearestObjects()) {
                                ICharacter character = item as ICharacter;
                                if (character != null) {
                                    string name = character.Name.TextValue;
                                    if (character.ObjectKind == ObjectKind.Companion) {
                                        if (!oneMinionOnly) {
                                            foreach (var customNPC in Plugin.Config.CustomNpcCharacters) {
                                                if (character.Name.TextValue.ToLower().Contains(customNPC.MinionToReplace.ToLower())) {
                                                    name = customNPC.NpcName;
                                                    _objects.Add(new KeyValuePair<string, ICharacter>(name, character));
                                                }
                                            }
                                            oneMinionOnly = true;
                                        }
                                    } else if (character.ObjectKind == ObjectKind.EventNpc) {
                                        if (!string.IsNullOrEmpty(character.Name.TextValue)) {
                                            _objects.Add(new KeyValuePair<string, ICharacter>(name, character));
                                        }
                                    }
                                }
                            }
                            float aboveNoseYPosFinal = 0;
                            float aboveNeckYPosFinal = 0;
                            float aboveEyesYPosFinal = 0;
                            var cursorPoint = GetCurrentCursorPosition();
                            Vector2 cursorPosition = new Vector2(cursorPoint.X, cursorPoint.Y);
                            foreach (var item in _objects) {
                                unsafe {
                                    float closestDistance = float.MaxValue;
                                    Bone closestBone = null;
                                    float aboveEyesYPos = 0;
                                    float aboveNoseYPos = 0;
                                    float aboveNeckYPos = 0;
                                    float xPos = 0;
                                    float minWidth = float.MaxValue;
                                    float maxWidth = 0;
                                    float maxDistance = 0;
                                    Actor* characterActor = (Actor*)item.Value.Address;
                                    var model = characterActor->Model;
                                    for (int i = 0; i < model->Skeleton->PartialSkeletonCount; i++) {
                                        var partialSkeleton = model->Skeleton->PartialSkeletons[i];
                                        var pos = partialSkeleton.GetHavokPose(0);
                                        if (pos != null) {
                                            var skeleton = pos->Skeleton;
                                            for (var i2 = 1; i2 < skeleton->Bones.Length; i2++) {
                                                var bone = model->Skeleton->GetBone(i, i2);
                                                var worldPos = bone.GetWorldPos(characterActor, model);
                                                Vector2 screenPosition = new Vector2();
                                                Plugin.GameGui.WorldToScreen(worldPos, out screenPosition);
                                                float distance = Vector2.Distance(screenPosition, cursorPosition);
                                                _cursorPosition = cursorPosition;
                                                if (distance < closestDistance) {
                                                    closestDistance = distance;
                                                    closestBone = bone;
                                                }
                                                if (bone.UniqueId.Contains("1_41")) {
                                                    aboveEyesYPos = screenPosition.Y;
                                                    xPos = screenPosition.X;
                                                }
                                                if (bone.UniqueId.Contains("0_46") || bone.UniqueId.Contains("1_40")) {
                                                    aboveNoseYPos = screenPosition.Y;
                                                    xPos = screenPosition.X;
                                                }
                                                if (bone.UniqueId.Contains("0_33")) {
                                                    aboveNeckYPos = screenPosition.Y;
                                                }
                                                if (screenPosition.X > maxWidth) {
                                                    maxWidth = screenPosition.X;
                                                }
                                                if (screenPosition.X < minWidth) {
                                                    minWidth = screenPosition.X;
                                                }
                                                //if (!_alreadyAddedBoneList.Contains(bone.UniqueId)) {
                                                //    _alreadyAddedBoneList.Add(bone.UniqueId);
                                                //    boneSorting.Add(new Tuple<string, float>(bone.UniqueId, screenPosition.Y));
                                                //}
                                            }
                                        }
                                    }
                                    maxDistance = Vector2.Distance(new Vector2(minWidth, 0), new Vector2(maxWidth, 0)) / 2f;
                                    if (Vector2.Distance(new(cursorPosition.X, 0), new(xPos, 0)) < maxDistance) {
                                        selectedPlayer = item;
                                        aboveEyesYPosFinal = aboveEyesYPos;
                                        aboveNoseYPosFinal = aboveNoseYPos;
                                        aboveNeckYPosFinal = aboveNeckYPos;
                                        _closestBone = closestBone;
                                    }
                                    //foreach (var sortedItem in boneSorting.OrderBy(o => o.Item2)) {
                                    //    Plugin.Chat.Print(sortedItem.Item1 + " " + sortedItem.Item2);
                                    //}
                                }
                            }
                            try {
                                if (cursorPosition.Y < aboveEyesYPosFinal) {
                                    bodyDragPart = BodyDragPart.EyebrowsAndLashes;
                                } else {
                                    if (cursorPosition.Y < aboveNeckYPosFinal) {

                                        if (cursorPosition.Y < aboveNoseYPosFinal) {
                                            bodyDragPart = BodyDragPart.Eyes;
                                        } else {
                                            bodyDragPart = BodyDragPart.Face;
                                        }

                                    } else {
                                        bodyDragPart = BodyDragPart.Body;
                                    }
                                }
                                if (selectedPlayer.Value != null) {
                                    selectedPlayerCollection = PenumbraAndGlamourerIpcWrapper.Instance.GetCollectionForObject.Invoke(selectedPlayer.Value.ObjectIndex).Item3.Id;
                                }
                                string debugInfo = (_closestBone != null ? "Closest Bone" + _closestBone.HkaBone.Name.String : "") + " " + (cursorPosition != null ? cursorPosition.X + " " + cursorPosition.Y : "");
                                if (selectedPlayer.Value != null) {
                                    if (selectedPlayerCollection != mainPlayerCollection ||
                                        selectedPlayer.Value == plugin.ClientState.LocalPlayer) {
                                        ImGui.TextUnformatted($"Dragging texture onto {selectedPlayer.Key.Split(' ')[0]}'s {bodyDragPart.ToString()}:\n\t{string.Join("\n\t", m.Files.Select(Path.GetFileName))} " + debugInfo);
                                    } else {
                                        ImGui.TextUnformatted(selectedPlayer.Key.Split(' ')[0] + " has the same collection as your main character.\r\nPlease give them a unique collection in Penumbra, or drag onto your main character. " + debugInfo);
                                    }
                                } else {
                                    ImGui.TextUnformatted($"Dragging onto no character." + debugInfo);
                                }
                            } catch {
                                ImGui.TextUnformatted($"Dragging texture on unknown:\n\t{string.Join("\n\t", m.Files.Select(Path.GetFileName))}");
                            }
                            AllowClickthrough = false;
                        } catch {
                            ImGui.TextUnformatted($"Penumbra is not installed.");
                        }
                        return true;
                    });

                    if (!AllowClickthrough) {
                        Flags = _dragAndDropFlags;
                        if (!_alreadyLoadingFrame) {
                            Task.Run(async () => {
                                _alreadyLoadingFrame = true;
                                _nextFrameToLoad = _blank.ToArray();
                                if (_lastLoadedFrame != _nextFrameToLoad) {
                                    _frameToLoad = await _textureProvider.CreateFromImageAsync(_nextFrameToLoad);
                                    _lastLoadedFrame = _nextFrameToLoad;
                                }
                                _alreadyLoadingFrame = false;
                            });
                        }
                        try {
                            textureWrap = _frameToLoad.CreateWrapSharingLowLevelResource();
                            ImGui.Image(textureWrap.ImGuiHandle, new Vector2(ImGui.GetMainViewport().Size.X, ImGui.GetMainViewport().Size.Y));
                        } catch {

                        }
                    } else {
                        Flags = _defaultFlags;
                    }

                    if (_dragDropManager.CreateImGuiTarget("TextureDragDrop", out var files, out _)) {
                        string modPath = PenumbraAndGlamourerIpcWrapper.Instance.GetModDirectory.Invoke();
                        _textureProcessor.BasePath = modPath + @"\LooseTextureCompilerDLC";
                        List<TextureSet> textureSets = new List<TextureSet>();
                        if (selectedPlayer.Value != null && selectedPlayerCollection != mainPlayerCollection ||
                            selectedPlayer.Value == plugin.ClientState.LocalPlayer) {
                            string modName = selectedPlayer.Key.Split(' ')[0] + " Texture Mod";
                            foreach (var file in files) {
                                if (ValidTextureExtensions.Contains(Path.GetExtension(file))) {
                                    string filePath = file;
                                    string fileName = Path.GetFileNameWithoutExtension(filePath).ToLower();
                                    _currentCustomization = PenumbraAndGlamourerHelperFunctions.GetCustomization(selectedPlayer.Value);
                                    if (fileName.Contains("mata") || fileName.Contains("amat")
                                        || fileName.Contains("materiala") || fileName.Contains("gen2")) {
                                        var item = AddBody(_currentCustomization.Customize.Gender.Value, 0,
                                        RaceInfo.SubRaceToMainRace(_currentCustomization.Customize.Clan.Value - 1),
                                        _currentCustomization.Customize.TailShape.Value - 1, false);
                                        SortUVTexture(item, file);
                                        textureSets.Add(item);
                                        modName = modName.Replace("Mod", "Body");
                                        item.OmniExportMode = File.Exists(_xNormalPath) && Path.Exists(_textureProcessor.BasePath) && holdingModifier;
                                    } else if (fileName.Contains("bibo") || fileName.Contains("b+")) {
                                        var item = AddBody(_currentCustomization.Customize.Gender.Value, 1,
                                        RaceInfo.SubRaceToMainRace(_currentCustomization.Customize.Clan.Value - 1),
                                        _currentCustomization.Customize.TailShape.Value - 1, false);
                                        SortUVTexture(item, file);
                                        textureSets.Add(item);
                                        modName = modName.Replace("Mod", "Body");
                                        item.OmniExportMode = File.Exists(_xNormalPath) && Path.Exists(_textureProcessor.BasePath) && holdingModifier;
                                    } else if (fileName.Contains("gen3") || fileName.Contains("eve")) {
                                        var item = AddBody(_currentCustomization.Customize.Gender.Value, 2,
                                        RaceInfo.SubRaceToMainRace(_currentCustomization.Customize.Clan.Value - 1),
                                        _currentCustomization.Customize.TailShape.Value - 1, false);
                                        SortUVTexture(item, file);
                                        textureSets.Add(item);
                                        modName = modName.Replace("Mod", "Body");
                                        item.OmniExportMode = File.Exists(_xNormalPath) && Path.Exists(_textureProcessor.BasePath) && holdingModifier;
                                    } else if (fileName.Contains("tbse")) {
                                        var item = AddBody(_currentCustomization.Customize.Gender.Value, 3,
                                        RaceInfo.SubRaceToMainRace(_currentCustomization.Customize.Clan.Value - 1),
                                        _currentCustomization.Customize.TailShape.Value - 1, false);
                                        SortUVTexture(item, file);
                                        textureSets.Add(item);
                                        modName = modName.Replace("Mod", "Body");
                                        item.OmniExportMode = File.Exists(_xNormalPath) && Path.Exists(_textureProcessor.BasePath) && holdingModifier;
                                    } else if (fileName.Contains("eyebrow") || fileName.Contains("lash")) {
                                        var item = AddFace(_currentCustomization.Customize.Face.Value - 1, 1, 0,
                                         _currentCustomization.Customize.Gender.Value,
                                         RaceInfo.SubRaceToMainRace(_currentCustomization.Customize.Clan.Value - 1),
                                         _currentCustomization.Customize.Clan.Value - 1, 0, false);
                                        item.Normal = file;
                                        textureSets.Add(item);
                                        modName = modName.Replace("Mod", "Eyebrows");
                                    } else if (fileName.Contains("eye")) {
                                        var item = AddFace(_currentCustomization.Customize.Face.Value - 1, 2, 0,
                                        _currentCustomization.Customize.Gender.Value,
                                        RaceInfo.SubRaceToMainRace(_currentCustomization.Customize.Clan.Value - 1),
                                        _currentCustomization.Customize.Clan.Value - 1, 0, false);
                                        item.Base = file;
                                        textureSets.Add(item);
                                        modName = modName.Replace("Mod", "Eyes");
                                    } else if (fileName.Contains("face") || fileName.Contains("makeup")) {
                                        var item = AddFace(_currentCustomization.Customize.Face.Value - 1, 0, 0,
                                        _currentCustomization.Customize.Gender.Value,
                                        RaceInfo.SubRaceToMainRace(_currentCustomization.Customize.Clan.Value - 1),
                                        _currentCustomization.Customize.Clan.Value - 1, 0, false);
                                        SortUVTexture(item, file);
                                        textureSets.Add(item);
                                        modName = modName.Replace("Mod", "Face");
                                    } else {
                                        TextureSet item = null;
                                        switch (bodyDragPart) {
                                            case BodyDragPart.Body:
                                                if (_currentCustomization.Customize.Race.Value - 1 == 3) {
                                                    item = AddBody(_currentCustomization.Customize.Gender.Value, 5,
                                                    RaceInfo.SubRaceToMainRace(_currentCustomization.Customize.Clan.Value - 1),
                                                    _currentCustomization.Customize.TailShape.Value - 1, false);
                                                    SortUVTexture(item, file);
                                                    textureSets.Add(item);
                                                    modName = modName.Replace("Mod", "Body");
                                                    item.OmniExportMode = File.Exists(_xNormalPath) && Path.Exists(_textureProcessor.BasePath) && holdingModifier;
                                                } else if (_currentCustomization.Customize.Gender.Value == 0) {
                                                    item = AddBody(_currentCustomization.Customize.Gender.Value, 3,
                                                    RaceInfo.SubRaceToMainRace(_currentCustomization.Customize.Clan.Value - 1),
                                                    _currentCustomization.Customize.TailShape.Value - 1, false);
                                                    SortUVTexture(item, file);
                                                    textureSets.Add(item);
                                                    modName = modName.Replace("Mod", "Body");
                                                    item.OmniExportMode = File.Exists(_xNormalPath) && Path.Exists(_textureProcessor.BasePath) && holdingModifier;
                                                } else {
                                                    switch (ImageManipulation.FemaleBodyUVClassifier(file)) {
                                                        case BodyUVType.Bibo:
                                                            item = AddBody(_currentCustomization.Customize.Gender.Value, 1,
                                                            RaceInfo.SubRaceToMainRace(_currentCustomization.Customize.Clan.Value - 1),
                                                            _currentCustomization.Customize.TailShape.Value - 1, false);
                                                            SortUVTexture(item, file);
                                                            textureSets.Add(item);
                                                            modName = modName.Replace("Mod", "Body");
                                                            item.OmniExportMode = File.Exists(_xNormalPath) && Path.Exists(_textureProcessor.BasePath) && holdingModifier;
                                                            break;
                                                        case BodyUVType.Gen3:
                                                            item = AddBody(_currentCustomization.Customize.Gender.Value, 2,
                                                            RaceInfo.SubRaceToMainRace(_currentCustomization.Customize.Clan.Value - 1),
                                                            _currentCustomization.Customize.TailShape.Value - 1, false);
                                                            SortUVTexture(item, file);
                                                            textureSets.Add(item);
                                                            modName = modName.Replace("Mod", "Body");
                                                            item.OmniExportMode = File.Exists(_xNormalPath) && Path.Exists(_textureProcessor.BasePath) && holdingModifier;
                                                            break;
                                                        case BodyUVType.Gen2:
                                                            item = AddBody(_currentCustomization.Customize.Gender.Value, 0,
                                                            RaceInfo.SubRaceToMainRace(_currentCustomization.Customize.Clan.Value - 1),
                                                            _currentCustomization.Customize.TailShape.Value - 1, false);
                                                            SortUVTexture(item, file);
                                                            textureSets.Add(item);
                                                            modName = modName.Replace("Mod", "Body");
                                                            item.OmniExportMode = File.Exists(_xNormalPath) && Path.Exists(_textureProcessor.BasePath) && holdingModifier;
                                                            break;
                                                    }
                                                }
                                                break;
                                            case BodyDragPart.Face:
                                                item = AddFace(_currentCustomization.Customize.Face.Value - 1, 0, 0,
                                                _currentCustomization.Customize.Gender.Value,
                                                RaceInfo.SubRaceToMainRace(_currentCustomization.Customize.Clan.Value - 1),
                                                _currentCustomization.Customize.Clan.Value - 1, 0, false);
                                                SortUVTexture(item, file);
                                                textureSets.Add(item);
                                                modName = modName.Replace("Mod", "Face");
                                                break;
                                            case BodyDragPart.Eyes:
                                                item = AddFace(_currentCustomization.Customize.Face.Value - 1, 2, 0,
                                                _currentCustomization.Customize.Gender.Value,
                                                RaceInfo.SubRaceToMainRace(_currentCustomization.Customize.Clan.Value - 1),
                                                _currentCustomization.Customize.Clan.Value - 1, 0, false);
                                                item.Normal = file;
                                                textureSets.Add(item);
                                                modName = modName.Replace("Mod", "Eyes");
                                                break;
                                            case BodyDragPart.EyebrowsAndLashes:
                                                item = AddFace(_currentCustomization.Customize.Face.Value - 1, 1, 0,
                                                _currentCustomization.Customize.Gender.Value,
                                                RaceInfo.SubRaceToMainRace(_currentCustomization.Customize.Clan.Value - 1),
                                                _currentCustomization.Customize.Clan.Value - 1, 0, false);
                                                item.Normal = file;
                                                textureSets.Add(item);
                                                modName = modName.Replace("Mod", "Eyebrows");
                                                break;
                                        }
                                    }
                                }
                            }
                            string fullModPath = Path.Combine(PenumbraAndGlamourerIpcWrapper.Instance.GetModDirectory.Invoke(), modName);
                            if (textureSets.Count > 0) {
                                Task.Run(() => Export(true, textureSets, fullModPath, modName, selectedPlayer));
                            } else {
                                plugin.Chat.PrintError("[Artemis Roleplaying Kit] Unable to identify texture type!");
                            }
                        }
                    }
                    AllowClickthrough = true;
                } else {
                    AllowClickthrough = true;
                    Flags = _defaultFlags;
                }
            }
        }
        private void ExportJson(string jsonFilePath) {
            string jsonText = @"{
  ""Name"": """",
  ""Priority"": 0,
  ""Files"": { },
  ""FileSwaps"": { },
  ""Manipulations"": []
}";
            if (jsonFilePath != null) {
                using (StreamWriter writer = new StreamWriter(jsonFilePath)) {
                    writer.WriteLine(jsonText);
                }
            }
        }
        private void SortUVTexture(TextureSet textureSet, string file) {
            switch (ImageManipulation.UVMapTypeClassifier(file)) {
                case UVMapType.Diffuse:
                    textureSet.Diffuse = file;
                    break;
                case UVMapType.Normal:
                    textureSet.Normal = file;
                    break;
                case UVMapType.Multi:
                    textureSet.Multi = file;
                    break;
            }
        }
        private void ExportMeta(string metaFilePath, string name, string author = "Loose Texture Compiler",
            string description = "Exported By Loose Texture Compiler", string modVersion = "0.0.0",
            string modWebsite = @"https://github.com/Sebane1/FFXIVLooseTextureCompiler") {
            string metaText = @"{
  ""FileVersion"": 3,
  ""Name"": """ + (!string.IsNullOrEmpty(name) ? name : "") + @""",
  ""Author"": """ + (!string.IsNullOrEmpty(author) ? author :
        "FFXIV Loose Texture Compiler") + @""",
  ""Description"": """ + (!string.IsNullOrEmpty(description) ? description :
        "Exported by FFXIV Loose Texture Compiler") + @""",
  ""Version"": """ + modVersion + @""",
  ""Website"": """ + modWebsite + @""",
  ""ModTags"": []
}";
            if (metaFilePath != null) {
                using (StreamWriter writer = new StreamWriter(metaFilePath)) {
                    writer.WriteLine(metaText);
                }
            }
        }
        private TextureSet AddBody(int gender, int baseBody, int race, int tail, bool uniqueAuRa = false) {
            TextureSet textureSet = new TextureSet();
            textureSet.TextureSetName = _bodyNames[baseBody] + (_bodyNames[baseBody].ToLower().Contains("tail") ? " " +
                (tail + 1) : "") + ", " + (race == 5 ? "Unisex" : _genders[gender])
                + ", " + RaceInfo.Races[race];
            AddBodyPaths(textureSet, gender, baseBody, race, tail, uniqueAuRa);
            return textureSet;
        }

        private TextureSet AddFace(int faceType, int facePart, int faceExtra, int gender, int race, int subRace, int auraScales, bool asym) {
            TextureSet textureSet = new TextureSet();
            textureSet.TextureSetName = _faceParts[facePart] + (facePart == 4 ? " "
                + (faceExtra + 1) : "") + ", " + (facePart != 4 ? _genders[gender] : "Unisex")
                + ", " + (facePart != 4 ? RaceInfo.SubRaces[subRace] : "Multi Race") + ", "
                + (facePart != 4 ? _faceTypes[faceType] : "Multi Face");
            switch (facePart) {
                default:
                    AddFacePaths(textureSet, subRace, facePart, faceType, gender, auraScales, asym);
                    break;
                case 2:
                    AddEyePaths(textureSet, subRace, faceType, gender, auraScales, asym);
                    break;
                case 4:
                    AddDecalPath(textureSet, faceExtra);
                    break;
                case 5:
                    AddHairPaths(textureSet, gender, facePart, faceExtra, race, subRace);
                    break;
            }
            textureSet.IgnoreMultiGeneration = true;
            if (facePart == 0) {
                BackupTexturePaths.AddFaceBackupPaths(gender, subRace, faceExtra, textureSet);
            }
            return textureSet;
        }
        private void AddBodyPaths(TextureSet textureSet, int gender, int baseBody, int race, int tail, bool uniqueAuRa = false) {
            if (race != 3 || baseBody != 6) {
                textureSet.InternalBasePath = RacePaths.GetBodyTexturePath(0, gender,
                  baseBody, race, tail, uniqueAuRa);
            }
            textureSet.InternalNormalPath = RacePaths.GetBodyTexturePath(1, gender,
                  baseBody, race, tail, uniqueAuRa);

            textureSet.InternalMultiPath = RacePaths.GetBodyTexturePath(2, gender,
                  baseBody, race, tail, uniqueAuRa);
            BackupTexturePaths.AddBodyBackupPaths(gender, race, textureSet);
        }

        private void AddDecalPath(TextureSet textureSet, int faceExtra) {
            textureSet.InternalBasePath = RacePaths.GetFaceTexturePath(faceExtra);
        }

        private void AddHairPaths(TextureSet textureSet, int gender, int facePart, int faceExtra, int race, int subrace) {
            textureSet.TextureSetName = _faceParts[facePart] + " " + (faceExtra + 1)
                + ", " + _genders[gender] + ", " + RaceInfo.Races[race];

            textureSet.InternalNormalPath = RacePaths.GetHairTexturePath(1, faceExtra,
                gender, race, subrace);

            textureSet.InternalMultiPath = RacePaths.GetHairTexturePath(2, faceExtra,
                gender, race, subrace);
        }

        private void AddEyePaths(TextureSet textureSet, int subrace, int faceType, int gender, int auraScales, bool asym) {
            RaceEyePaths.GetEyeTextureSet(subrace,faceType, gender == 1, textureSet);
        }

        private void AddFacePaths(TextureSet textureSet, int subrace, int facePart, int faceType, int gender, int auraScales, bool asym) {
            if (facePart != 1) {
                textureSet.InternalBasePath = RacePaths.GetFacePath(0, gender, subrace,
                    facePart, faceType, auraScales, asym);
            }

            textureSet.InternalNormalPath = RacePaths.GetFacePath(1, gender, subrace,
            facePart, faceType, auraScales, asym);

            textureSet.InternalMaskPath = RacePaths.GetFacePath(2, gender, subrace,
            facePart, faceType, auraScales, asym);

            if (facePart == 0) {
                if (subrace == 10 || subrace == 11) {
                    if (auraScales > 0) {
                        if (faceType < 4) {
                            if (asym) {
                                textureSet.NormalCorrection = Path.Combine(_textureProcessor.BasePath,
                                      @"res\textures\s" + (gender == 0 ? "m" : "f") + faceType + "a.png");
                            } else {
                                textureSet.NormalCorrection = Path.Combine(_textureProcessor.BasePath,
                                    @"res\textures\s" + (gender == 0 ? "m" : "f") + faceType + ".png");
                            }
                        }
                    }
                }
            }
        }
        public async Task<bool> Export(bool finalize, List<TextureSet> exportTextureSets, string path, string name, KeyValuePair<string, ICharacter> character) {
            if (!_lockDuplicateGeneration) {
                plugin.Chat.Print("[Artemis Roleplaying Kit] Processing textures, please wait.");
                string modPath = PenumbraAndGlamourerIpcWrapper.Instance.GetModDirectory.Invoke();
                _textureProcessor.BasePath = modPath + @"\LooseTextureCompilerDLC";
                _exportStatus = "Initializing";
                _lockDuplicateGeneration = true;
                List<TextureSet> textureSets = new List<TextureSet>();
                string jsonFilepath = Path.Combine(path, "default_mod.json");
                string metaFilePath = Path.Combine(path, "meta.json");
                foreach (TextureSet item in exportTextureSets) {
                    if (item.OmniExportMode) {
                        UniversalTextureSetCreator.ConfigureOmniConfiguration(item);
                    }
                    textureSets.Add(item);
                }
                Directory.CreateDirectory(path);
                _textureProcessor.CleanGeneratedAssets(path);
                await _textureProcessor.Export(textureSets, new Dictionary<string, int>(), path, 3, false, false, File.Exists(_xNormalPath) && finalize, _xNormalPath);
                ExportJson(jsonFilepath);
                ExportMeta(metaFilePath, name);
                Thread.Sleep(100);
                PenumbraAndGlamourerIpcWrapper.Instance.AddMod.Invoke(name);
                PenumbraAndGlamourerIpcWrapper.Instance.ReloadMod.Invoke(path, name);
                Guid collection = PenumbraAndGlamourerIpcWrapper.Instance.GetCollectionForObject.Invoke(character.Value.ObjectIndex).Item3.Id;
                PenumbraAndGlamourerIpcWrapper.Instance.TrySetMod.Invoke(collection, path, true, name);
                PenumbraAndGlamourerIpcWrapper.Instance.TrySetModPriority.Invoke(collection, path, 100, name);
                var settings = PenumbraAndGlamourerIpcWrapper.Instance.GetCurrentModSettings.Invoke(collection, path, name, true);
                foreach (var group in settings.Item2.Value.Item3) {
                    PenumbraAndGlamourerIpcWrapper.Instance.TrySetModSetting.Invoke(collection, path, group.Key, "Enable", name);
                }
                Thread.Sleep(300);
                PenumbraAndGlamourerIpcWrapper.Instance.RedrawObject.Invoke(character.Value.ObjectIndex, Penumbra.Api.Enums.RedrawType.Redraw);
                Thread.Sleep(300);
                PenumbraAndGlamourerIpcWrapper.Instance.RedrawObject.Invoke(character.Value.ObjectIndex, Penumbra.Api.Enums.RedrawType.Redraw);
                _lockDuplicateGeneration = false;
                plugin.Chat.Print("[Artemis Roleplaying Kit] Import complete!");
            }
            return true;
        }
        private static readonly string[] ValidTextureExtensions = new[]
        {
          ".png",
          ".dds",
          ".bmp",
          ".tex",
        };
        private readonly string _xNormalPath;
    }
}

namespace RoleplayingVoiceDalamud {
    public enum BodyDragPart {
        Unknown,
        Eyes,
        Face,
        Body,
        EyebrowsAndLashes
    }
}