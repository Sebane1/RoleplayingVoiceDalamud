using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Logging;
using Dalamud.Plugin.Services;
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
            CharacterCustomization characterCustomization = null;
            string customizationValue = (PenumbraAndGlamourerIpcWrapper.Instance.GetStateBase64.Invoke(objectIndex)).Item2;
            var bytes = System.Convert.FromBase64String(customizationValue);
            var version = bytes[0];
            version = bytes.DecompressToString(out var decompressed);
            characterCustomization = JsonConvert.DeserializeObject<CharacterCustomization>(decompressed);
            SetEquipment(item, objectIndex);
            Plugin.BlockDataRefreshes = false;
        }

        public static bool SetEquipment(EquipObject equipItem, int objectIndex) {
            bool changed = false;
            PenumbraAndGlamourerIpcWrapper.Instance.SetItem.Invoke(objectIndex, equipItem.Type, (ulong)equipItem.ItemId.Id, 0);
            changed = true;
            Plugin.PluginLog.Verbose("Completed sending IPC to glamourer");
            return changed;
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
            Plugin.PluginLog.Debug("Attempting to find mods that contain \"" + modelMod + "\".");
            int lowestPriority = 10;
            foreach (string modName in modelMods) {
                if (modName.ToLower().Contains(modelMod.ToLower())) {
                    PenumbraAndGlamourerIpcWrapper.Instance.TrySetMod.Invoke(collection, modName, true);
                    PenumbraAndGlamourerIpcWrapper.Instance.TrySetModPriority.Invoke(collection, modName, 11);
                } else {
                    if (disableOtherMods) {
                        var ipcResult = PenumbraAndGlamourerIpcWrapper.Instance.TrySetMod.Invoke(collection, modName, false);
                    } else {
                        PenumbraAndGlamourerIpcWrapper.Instance.TrySetModPriority.Invoke(collection, modName, 5);
                    }
                }
            }
        }
        public static void SetBodyDependancies(Guid collection, ICollection<string> modelDependancies) {
            int lowestPriority = 10;
            foreach (string modName in modelDependancies) {
                var result = PenumbraAndGlamourerIpcWrapper.Instance.TrySetMod.Invoke(collection, modName, true);
                PenumbraAndGlamourerIpcWrapper.Instance.TrySetModPriority.Invoke(collection, modName, 5);
            }
        }
        public static void SetDependancies(string modelMod, ICollection<string> modelMods, Guid collection, bool disableOtherMods = true) {
            Dictionary<string, bool> alreadyDisabled = new Dictionary<string, bool>();
            Plugin.PluginLog.Debug("Attempting to find mod dependancies that contain \"" + modelMod + "\".");
            int lowestPriority = 10;
            foreach (string modName in modelMods) {
                if (modName.ToLower().Contains(modelMod.ToLower())) {
                    var result = PenumbraAndGlamourerIpcWrapper.Instance.TrySetMod.Invoke(collection, modName, true);
                    PenumbraAndGlamourerIpcWrapper.Instance.TrySetModPriority.Invoke(collection, modName, 11);
                } else {
                    if (FindStringMatch(modelMod, modName)) {
                        var ipcResult = PenumbraAndGlamourerIpcWrapper.Instance.TrySetMod.Invoke(collection, modName, true);
                        PenumbraAndGlamourerIpcWrapper.Instance.TrySetModPriority.Invoke(collection, modName, 10);
                    } else if (disableOtherMods) {
                        var ipcResult = PenumbraAndGlamourerIpcWrapper.Instance.TrySetMod.Invoke(collection, modName, false);
                    } else {
                        PenumbraAndGlamourerIpcWrapper.Instance.TrySetModPriority.Invoke(collection, modName, 5);
                    }
                }
            }
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
                var ipcResult = PenumbraAndGlamourerIpcWrapper.Instance.TrySetMod.Invoke(collection, "", false, modName);
            }
            SetBodyDependancies(collection, modelDepandacies);
        }
    }
}
