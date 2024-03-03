using FFXIVVoicePackCreator.VoiceSorting;
using RoleplayingVoiceDalamud;
using System;
using System.Collections.Generic;
namespace FFXIVVoicePackCreator {
    public class RaceVoice {
        private static Dictionary<string, TimeCodeData> timeCodeData = new Dictionary<string, TimeCodeData>();
        public static Dictionary<string, TimeCodeData> TimeCodeData { get => timeCodeData; set => timeCodeData = value; }

        public static void LoadRacialVoiceInfo() {
            LoadTimeCodes();
        }

        private static void LoadTimeCodes() {
            timeCodeData.Clear();
            var timeCodes = RacialEmoteTime.TimeCodes();
            for (int raceIndex = 0; raceIndex < 18; raceIndex += 2) {
                timeCodeData.Add(timeCodes[raceIndex].Descriptor, timeCodes[raceIndex]);
                timeCodeData.Add(timeCodes[raceIndex + 1].Descriptor, timeCodes[raceIndex + 1]);
            }
        }
    }
}
