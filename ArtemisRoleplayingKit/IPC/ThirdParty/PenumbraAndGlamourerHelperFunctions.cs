using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Logging;
using Dalamud.Plugin.Services;
using Glamourer.Api.Enums;
using Glamourer.Utility;
using Newtonsoft.Json;
using Penumbra.Api.Enums;
using RoleplayingVoice;
using RoleplayingVoiceDalamud.Glamourer;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RoleplayingVoiceDalamud.Catalogue {
    public static class PenumbraAndGlamourerHelperFunctions {
        public static void WearOutfit(EquipObject item, Guid collection, int objectIndex, ICollection<string> modelMods) {
            //CleanSlate();
            Plugin.BlockDataRefreshes = true;
            if (collection == Guid.Empty) {
                collection = PenumbraAndGlamourerIpcWrapper.Instance.GetCollectionForObject.Invoke(objectIndex).Item3.Id;
            }
            SetClothingMod(item.Name, modelMods, collection, false);
            SetDependancies(item.Name, modelMods, collection, false);
            PenumbraAndGlamourerIpcWrapper.Instance.RedrawObject.Invoke(objectIndex, RedrawType.Redraw);
            SetEquipment(item, objectIndex);
            Plugin.BlockDataRefreshes = false;
        }

        public static bool SetEquipment(EquipObject equipItem, int objectIndex) {
            bool changed = false;
            var result = PenumbraAndGlamourerIpcWrapper.Instance.SetItem.Invoke(objectIndex, FullEquipTypeToApiEquipSlot(equipItem.Type), equipItem.ItemId.Id, new List<byte>());
            changed = true;
            Plugin.PluginLog.Verbose("Completed sending IPC to glamourer");
            return changed;
        }

        public static ApiEquipSlot FullEquipTypeToApiEquipSlot(FullEquipType fullEquipType) {
            switch (fullEquipType) {
                case FullEquipType.Unknown:
                    return ApiEquipSlot.Unknown;
                case FullEquipType.Head:
                    return ApiEquipSlot.Head;
                case FullEquipType.Body:
                    return ApiEquipSlot.Body;
                case FullEquipType.Hands:
                    return ApiEquipSlot.Hands;
                case FullEquipType.Legs:
                    return ApiEquipSlot.Legs;
                case FullEquipType.Feet:
                    return ApiEquipSlot.Feet;
                case FullEquipType.Ears:
                    return ApiEquipSlot.Ears;
                case FullEquipType.Wrists:
                    return ApiEquipSlot.Wrists;
                case FullEquipType.Finger:
                    return ApiEquipSlot.RFinger;
                default:
                    return ApiEquipSlot.Unknown;
            }
        }

        public static int GetRace(ICharacter playerCharacter) {
            try {
                CharacterCustomization characterCustomization = null;
                string customizationValue = (PenumbraAndGlamourerIpcWrapper.Instance.GetStateBase64.Invoke(playerCharacter.ObjectIndex)).Item2;
                var bytes = System.Convert.FromBase64String(customizationValue);
                var version = bytes[0];
                version = bytes.DecompressToString(out var decompressed);
                characterCustomization = JsonConvert.DeserializeObject<CharacterCustomization>(decompressed);
                return characterCustomization.Customize.Race.Value;
            } catch {
                return playerCharacter.Customize[(int)CustomizeIndex.Race];
            }
        }

        public static int GetGender(ICharacter playerCharacter) {
            try {
                CharacterCustomization characterCustomization = null;
                string customizationValue = (PenumbraAndGlamourerIpcWrapper.Instance.GetStateBase64.Invoke(playerCharacter.ObjectIndex)).Item2;
                var bytes = System.Convert.FromBase64String(customizationValue);
                var version = bytes[0];
                version = bytes.DecompressToString(out var decompressed);
                characterCustomization = JsonConvert.DeserializeObject<CharacterCustomization>(decompressed);
                return characterCustomization.Customize.Gender.Value;
            } catch {
                if (playerCharacter != null) {
                    return playerCharacter.Customize[(int)CustomizeIndex.Gender];
                } else {
                    return 0;
                }
            }
        }

        public static CharacterCustomization GetCustomization(ICharacter playerCharacter) {
            try {
                CharacterCustomization characterCustomization = null;
                string customizationValue = (PenumbraAndGlamourerIpcWrapper.Instance.GetStateBase64.Invoke(playerCharacter.ObjectIndex)).Item2;
                var bytes = System.Convert.FromBase64String(customizationValue);
                var version = bytes[0];
                version = bytes.DecompressToString(out var decompressed);
                characterCustomization = JsonConvert.DeserializeObject<CharacterCustomization>(decompressed);
                return characterCustomization;
            } catch {
                return new CharacterCustomization() {
                    Customize = new Customize() {
                        EyeColorLeft = new FacialValue() { Value = playerCharacter.Customize[(int)CustomizeIndex.EyeColor] },
                        EyeColorRight = new FacialValue() { Value = playerCharacter.Customize[(int)CustomizeIndex.EyeColor2] },
                        BustSize = new BustSize() { Value = playerCharacter.Customize[(int)CustomizeIndex.BustSize] },
                        LipColor = new LipColor() { Value = playerCharacter.Customize[(int)CustomizeIndex.LipColor] },
                        Gender = new Gender() { Value = playerCharacter.Customize[(int)CustomizeIndex.Gender] },
                        Height = new Height() { Value = playerCharacter.Customize[(int)CustomizeIndex.Height] },
                        Clan = new Clan() { Value = playerCharacter.Customize[(int)CustomizeIndex.Tribe] },
                        Race = new Race() { Value = playerCharacter.Customize[(int)CustomizeIndex.Race] },
                        BodyType = new BodyType() { Value = playerCharacter.Customize[(int)CustomizeIndex.ModelType] }
                    }
                };
            }
        }
        public static Dictionary<Guid, string> GetGlamourerDesigns() {
            try {
                var glamourerDesignList = PenumbraAndGlamourerIpcWrapper.Instance.GetDesignList.Invoke();
                return glamourerDesignList;
            } catch (Exception e) {
                return new Dictionary<Guid, string>();
            }
        }
        public static bool IsHumanoid(ICharacter playerCharacter) {
            try {
                CharacterCustomization characterCustomization = null;
                string customizationValue = (PenumbraAndGlamourerIpcWrapper.Instance.GetStateBase64.Invoke(playerCharacter.ObjectIndex)).Item2;
                var bytes = System.Convert.FromBase64String(customizationValue);
                var version = bytes[0];
                version = bytes.DecompressToString(out var decompressed);
                characterCustomization = JsonConvert.DeserializeObject<CharacterCustomization>(decompressed);
                return characterCustomization.Customize.ModelId < 5;
            } catch {
                var modelType = playerCharacter.Customize[(int)CustomizeIndex.ModelType];
                return modelType is not 0 && modelType < 5;
            }
        }
        public static void SetClothingMod(string modelMod, ICollection<string> modelMods, Guid collection, bool disableOtherMods = true) {
            Plugin.PluginLog.Debug("Attempting to find mods that contain \"" + modelMod + "\" (Set Clothing Mod).");
            int highestPriority = 10;
            foreach (string modName in modelMods) {
                if (modName.ToLower().Contains(modelMod.ToLower())) {
                    PenumbraAndGlamourerIpcWrapper.Instance.TrySetMod.Invoke(collection, modName, true);
                    PenumbraAndGlamourerIpcWrapper.Instance.TrySetModPriority.Invoke(collection, modName, 11);
                } else {
                    if (CheckIfValidToChange(modName, modelMods)) {
                        if (disableOtherMods) {
                            var ipcResult = PenumbraAndGlamourerIpcWrapper.Instance.TrySetMod.Invoke(collection, modName, false);
                        } else {
                            PenumbraAndGlamourerIpcWrapper.Instance.TrySetModPriority.Invoke(collection, modName, 5);
                        }
                    }
                }
            }
        }

        public static Dictionary<string, object> GetChangedItemsForMod(string modelMod, ICollection<string> modelMods) {
            Plugin.PluginLog.Debug("Attempting to find mods that contain \"" + modelMod + "\" (Getting Changed Items).");
            int lowestPriority = 10;
            foreach (string modName in modelMods) {
                if (modName.ToLower().Contains(modelMod.ToLower())) {
                    return PenumbraAndGlamourerIpcWrapper.Instance.GetChangedItemsForMod.Invoke("", modName);
                }
            }
            return new Dictionary<string, object>();
        }
        public static void SetBodyDependancies(Guid collection, ICollection<string> modelDependancies) {
            //int lowestPriority = 10;
            //foreach (string modName in modelDependancies) {
            //    var result = PenumbraAndGlamourerIpcWrapper.Instance.TrySetMod.Invoke(collection, modName, true);
            //    PenumbraAndGlamourerIpcWrapper.Instance.TrySetModPriority.Invoke(collection, modName, 5);
            //}
        }
        public static void SetDependancies(string modelMod, ICollection<string> modelMods, Guid collection, bool disableOtherMods = true) {
            Dictionary<string, bool> alreadyDisabled = new Dictionary<string, bool>();
            Plugin.PluginLog.Debug("Attempting to find mod dependancies that contain \"" + modelMod + "\" (Set Dependancies).");
            int lowestPriority = 10;
            foreach (string modName in modelMods) {
                if (modName.ToLower().Contains(modelMod.ToLower())) {
                    var result = PenumbraAndGlamourerIpcWrapper.Instance.TrySetMod.Invoke(collection, modName, true);
                    PenumbraAndGlamourerIpcWrapper.Instance.TrySetModPriority.Invoke(collection, modName, 11);
                } else {
                    if (FindStringMatch(modelMod, modName)) {
                        var ipcResult = PenumbraAndGlamourerIpcWrapper.Instance.TrySetMod.Invoke(collection, modName, true);
                        PenumbraAndGlamourerIpcWrapper.Instance.TrySetModPriority.Invoke(collection, modName, 10);
                    } else if (disableOtherMods && CheckIfValidToChange(modName, modelMods)) {
                        var ipcResult = PenumbraAndGlamourerIpcWrapper.Instance.TrySetMod.Invoke(collection, modName, false);
                    } else if (CheckIfValidToChange(modName, modelMods)) {
                        PenumbraAndGlamourerIpcWrapper.Instance.TrySetModPriority.Invoke(collection, modName, 5);
                    }
                }
            }
        }

        public static bool CheckIfValidToChange(string mod, ICollection<string> modelMods) {
            var items = GetChangedItemsForMod(mod, modelMods).Values;
            foreach (var changedItem in items) {
                try {
                    string equipItemJson = JsonConvert.SerializeObject(changedItem,
                new JsonSerializerSettings() { ReferenceLoopHandling = ReferenceLoopHandling.Ignore, PreserveReferencesHandling = PreserveReferencesHandling.Objects });
                    if (equipItemJson.Length > 200) {
                        var equipObject = JsonConvert.DeserializeObject<EquipObject>(equipItemJson);
                        switch (equipObject.ItemId.Id) {
                            case 9292:
                            case 9293:
                            case 9294:
                            case 9295:
                            case 10032:
                            case 10033:
                            case 10034:
                            case 10035:
                            case 10036:
                            case 13775:
                            case 0:
                                return false;
                        }
                    }
                } catch (Exception e) {
                    Plugin.PluginLog.Debug(e, e.Message);
                }
            }
            return true;
        }

        public static bool FindStringMatch(string sourceMod, string comparisonMod) {
            string[] strings = sourceMod.Split(' ');
            foreach (string value in strings) {
                string loweredValue = value.ToLower();
                if (comparisonMod.ToLower().Contains(loweredValue)
                  && loweredValue.Length > 4 && !loweredValue.Contains("[") && !loweredValue.Contains("]")
                  && !loweredValue.Contains("by") && !loweredValue.Contains("update")
                  && !loweredValue.Contains("megapack") && !comparisonMod.Contains("megapack")) {
                    return true;
                }
            }
            return false;
        }
        public static void CleanSlate(Guid collection, ICollection<string> modelMods, ICollection<string> modelDepandacies) {
            string foundModName = "";
            if (collection == Guid.Empty) {
                collection = PenumbraAndGlamourerIpcWrapper.Instance.GetCollectionForObject.Invoke(0).EffectiveCollection.Id;
            }
            Dictionary<string, bool> alreadyDisabled = new Dictionary<string, bool>();
            foreach (string modName in modelMods) {
                if (CheckIfValidToChange(modName, modelMods)) {
                    var ipcResult = PenumbraAndGlamourerIpcWrapper.Instance.TrySetMod.Invoke(collection, "", false, modName);
                }
            }
            SetBodyDependancies(collection, modelDepandacies);
        }
    }
}
