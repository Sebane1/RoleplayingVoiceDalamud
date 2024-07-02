using Glamourer.Api.Enums;
using Penumbra.GameData.Enums;
using RoleplayingVoice;
using System;
using System.Collections.Generic;
using System.IO;

namespace RoleplayingVoiceDalamud.Catalogue {
    public class CatalogueItem {
        public EquipObject EquipObject { get; set; }
        public List<CatalogueItem> Variants { get => variants; set => variants = value; }
        public string ImagePath { get; set; }

        private List<CatalogueItem> variants = new List<CatalogueItem>();

        public CatalogueItem(string path) {
            ImagePath = path;
            string data = Path.GetFileNameWithoutExtension(path);
            string[] strings = data.Split("@");
            EquipObject = new EquipObject();
            EquipObject.Name = strings[0];
            int offset = 0;
            try {
                EquipObject.Type = ParseEnum<ApiEquipSlot>(strings[1]);
                EquipObject.ItemId = new IdObject() { Id = int.Parse(strings[2]) };
            } catch (Exception e) {
               Plugin.PluginLog.Warning(e, e.Message);
            }
        }
        public static T ParseEnum<T>(string value) {
            return (T)Enum.Parse(typeof(T), value, true);
        }
    }
}
