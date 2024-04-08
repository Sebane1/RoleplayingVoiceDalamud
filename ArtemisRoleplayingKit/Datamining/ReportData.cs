using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace RoleplayingVoiceDalamud.Datamining {
    public class ReportData {
        public string speaker { get; set; }
        public string sentence { get; set; }
        public uint npcid { get; set; }
        public byte body { get; set; }
        public bool gender { get; set; }
        public byte race { get; set; }
        public byte tribe { get; set; }
        public byte eyes { get; set; }
        public string user { get; set; }
        public ReportData(string name, string message, GameObject gameObject) {
            Character character = gameObject as Character;
            if (character != null) {
                speaker = name;
                sentence = message;
                npcid = character.ObjectId;
                body = character.Customize[(int)CustomizeIndex.ModelType];
                gender = character.Customize[(int)CustomizeIndex.Gender] == 0;
                race = character.Customize[(int)CustomizeIndex.Race];
                tribe = character.Customize[(int)CustomizeIndex.Tribe];
                eyes = character.Customize[(int)CustomizeIndex.EyeShape];
                user = "ArtemisRoleplayingKit";
            } else {
                speaker = name;
                sentence = message;
                user = "ArtemisRoleplayingKit";
            }
        }
        public ReportData(string name, string message, uint objectId, byte body, bool gender, byte race, byte tribe, byte eyes) {
            speaker = name;
            sentence = message;
            npcid = objectId;
            this.body = body;
            this.gender = gender;
            this.race = race;
            this.tribe = tribe;
            this.eyes = eyes;
            user = "ArtemisRoleplayingKit";
        }
        public async void ReportToXivVoice() {
            using (HttpClient httpClient = new HttpClient()) {
                httpClient.BaseAddress = new Uri("https://arcsidian.com/report_to_seb.php");
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                var post = await httpClient.PostAsync(httpClient.BaseAddress, new StringContent(JsonConvert.SerializeObject(this)));
                if (post.StatusCode != HttpStatusCode.OK) {
                }
            }
        }
    }
}
