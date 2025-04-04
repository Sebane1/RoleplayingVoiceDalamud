using Dalamud.Plugin;
using Newtonsoft.Json;
using RoleplayingVoiceDalamud.Catalogue;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;
using RoleplayingVoiceDalamud.IPC.ThirdParty.Glamourer;

namespace RoleplayingVoice
{
    public partial class Plugin : IDalamudPlugin {
        public void StartCatalogingItems() {
            _originalCollection = PenumbraAndGlamourerIpcWrapper.Instance.GetCollectionForObject.Invoke(_threadSafeObjectTable.LocalPlayer.ObjectIndex);
            _catalogueCollectionName = _originalCollection.Item3.Id;
            Directory.CreateDirectory(_catalogueWindow.CataloguePath);
            _currentScreenshotList = Directory.GetFiles(_catalogueWindow.CataloguePath);
            _chat?.Print("Creating Thumbnails For New Clothing Mods");
            _catalogueMods = true;
            _modelModList = new List<string>();
            _modelModList.AddRange(_modelMods.Keys);
            _catalogueWindow.ScanCatalogue();
            ScanClothingMods();
        }
        private void ScanClothingMods() {
            Task.Run(() => {
                while (_catalogueMods && !disposed) {
                    if (_catalogueIndex < _modelModList.Count) {
                        ignoreModSettingChanged = true;
                        _catalogueScreenShotTaken = false;
                        _catalogueOffsetTimer.Restart();
                        while (_glamourerScreenshotQueue.Count is not 0) {
                            Thread.Sleep(500);
                        }
                        while (_catalogueIndex < _modelModList.Count) {
                            _currentModelMod = _modelModList[_catalogueIndex];
                            if (!AlreadyHasScreenShots(_currentModelMod) && !_currentModelMod.ToLower().Contains("megapack")
                            && !_currentModelMod.ToLower().Contains("mega pack") && !_currentModelMod.ToLower().Contains("hrothgar & viera")) {
                                _catalogueModsToEnable.Enqueue(_currentModelMod);
                                break;
                            } else {
                                _catalogueIndex++;
                            }
                        }
                        if (_catalogueModsToEnable.Count > 0) {
                            var catalogueMod = _catalogueModsToEnable.Dequeue();
                            if (catalogueMod != null) {
                                //PenumbraAndGlamourerHelperFunctions.CleanSlate(Guid.Empty, _modelMods.Keys, _modelDependancyMods.Keys);
                                //Thread.Sleep(300);
                                _currentClothingChangedItems = new List<EquipObject>();
                                var clothingChangedItems = new List<object>();
                                var items = PenumbraAndGlamourerHelperFunctions.GetChangedItemsForMod(catalogueMod, _modelMods.Keys).Values;
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
                                                    break;
                                                default:
                                                    _currentClothingChangedItems.Add(equipObject);
                                                    break;
                                            }
                                        }
                                    } catch (Exception e) {
                                        Plugin.PluginLog.Debug(e, e.Message);
                                    }
                                }
                                if (_currentClothingChangedItems.Count > 0) {
                                    PenumbraAndGlamourerHelperFunctions.SetClothingMod(catalogueMod, _modelMods.Keys, _catalogueCollectionName);
                                    Thread.Sleep(100);
                                    PenumbraAndGlamourerHelperFunctions.SetDependancies(catalogueMod, _modelMods.Keys, _catalogueCollectionName);
                                    Thread.Sleep(100);
                                    PenumbraAndGlamourerIpcWrapper.Instance.RedrawObject.Invoke(_threadSafeObjectTable.LocalPlayer.ObjectIndex);
                                }
                            }
                        }
                        Thread.Sleep(4000);
                        _equipmentFound = false;
                        while (!disposed && _currentChangedItemIndex <
                        _currentClothingChangedItems.Count && !AlreadyHasScreenShots(_currentModelMod)) {
                            try {
                                _currentClothingItem = _currentClothingChangedItems[_currentChangedItemIndex];
                                CleanEquipment(_threadSafeObjectTable.LocalPlayer.ObjectIndex);
                                _glamourerScreenshotQueue.Enqueue(_currentClothingItem);
                                _catalogueScreenShotTaken = false;
                                while (!_catalogueScreenShotTaken) {
                                    Thread.Sleep(100);
                                }

                            } catch (Exception e) {
                                Plugin.PluginLog.Debug(e, e.Message);
                            }
                            _currentChangedItemIndex++;
                            if (_currentChangedItemIndex >= _currentClothingChangedItems.Count) {
                                _catalogueIndex++;
                                _catalogueStage = 0;
                                _currentChangedItemIndex = 0;
                                _currentClothingItem = null;
                                break;
                            }
                        }
                        _catalogueTimer.Restart();
                        _catalogueIndex++;
                    } else {
                        _catalogueIndex = 0;
                        _catalogueMods = false;
                        ignoreModSettingChanged = false;
                        _chat?.Print("Done Catalog");
                        _catalogueTimer.Reset();
                        RefreshData();
                        //PenumbraAndGlamourerHelperFunctions.CleanSlate(Guid.Empty, _modelMods.Keys, _modelDependancyMods.Keys);
                        _catalogueWindow.ScanCatalogue();
                        PenumbraAndGlamourerIpcWrapper.Instance.SetCollectionForObject.Invoke(0, _originalCollection.Item3.Id, true, true);
                    }
                }
            });
        }
        private void CheckCataloging() {
            if (_glamourerScreenshotQueue.Count > 0) {
                var item = _glamourerScreenshotQueue.Dequeue();
                if (item != null && item != null) {
                    _equipmentFound = PenumbraAndGlamourerHelperFunctions.SetEquipment(item, _threadSafeObjectTable.LocalPlayer.ObjectIndex);
                    if (_equipmentFound) {
                        _chat.Print("Screenshotting item " + item.Name + "! " + (((float)_catalogueIndex / (float)_modelModList.Count) * 100f) + "% complete!");
                        Task.Run(() => {
                            string path = Path.Combine(config.CacheFolder, "ClothingCatalogue\\" + _currentModelMod
                                + "@" + item.Type + "@" + item.ItemId.Id + ".jpg");
                            if (!File.Exists(path)) {
                                Thread.Sleep(500);
                                try {
                                    //NativeGameWindow.BringMainWindowToFront(Process.GetCurrentProcess().ProcessName);
                                } catch { }
                                TakeScreenshot(item, path);
                            }
                        });
                    } else {
                        _catalogueScreenShotTaken = true;
                    }
                }
            }
        }

        private bool AlreadyHasScreenShots(string name) {
            //_chat?.Print(name);
            foreach (var item in _currentScreenshotList) {
                if (Path.GetFileNameWithoutExtension(item.ToLower()).Contains(name.ToLower())) {
                    return true;
                }
            }
            return false;
        }

        private void TakeScreenshot(EquipObject clothingItem, string pathName) {
            if (clothingItem != null) {
                Rectangle bounds = Screen.GetBounds(Point.Empty);
                using (Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height)) {
                    using (Graphics g = Graphics.FromImage(bitmap)) {
                        g.CopyFromScreen(Point.Empty, Point.Empty, bounds.Size);
                    }
                    Directory.CreateDirectory(_catalogueWindow.CataloguePath);
                    new Bitmap(CropImage(new Bitmap(bitmap, 1920, 1080), new Rectangle(560, 200, 800, 800)), 250, 250).Save(pathName, ImageFormat.Jpeg);
                }
            }
            _catalogueScreenShotTaken = true;
        }
        private static Image CropImage(Image img, Rectangle cropArea) {
            Bitmap bmpImage = new Bitmap(img);
            return bmpImage.Clone(cropArea, bmpImage.PixelFormat);
        }
        private void PrintCustomization(CharacterCustomization customization) {
            _chat?.Print("Head: " + customization.Equipment.Head.ItemId +
                        ", Body: " + customization.Equipment.Body.ItemId +
                        ", Hands: " + customization.Equipment.Hands.ItemId +
                        ", Legs: " + customization.Equipment.Legs.ItemId +
                        ", Feet: " + customization.Equipment.Feet.ItemId +
                        ", Ears: " + customization.Equipment.Ears.ItemId +
                        ", Neck: " + customization.Equipment.Neck.ItemId +
                        ", Wrists: " + customization.Equipment.Wrists.ItemId +
                        ", RFinger: " + customization.Equipment.RFinger.ItemId +
                        ", LFinger: " + customization.Equipment.LFinger.ItemId);
        }
    }
}
