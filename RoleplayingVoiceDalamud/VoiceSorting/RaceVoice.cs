using FFXIVVoicePackCreator.VoiceSorting;
using RoleplayingVoiceDalamud;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FFXIVVoicePackCreator {
    public class RaceVoice {
        private static Dictionary<string, TimeCodeData> timeCodeData = new Dictionary<string, TimeCodeData>();
        public static Dictionary<string, TimeCodeData> TimeCodeData { get => timeCodeData; set => timeCodeData = value; }

        public static void LoadRacialVoiceInfo() {
            LoadTimeCodes();
        }

        private static void LoadTimeCodes() {
            timeCodeData.Clear();
            string racialListPath = Path.Combine(Application.StartupPath, @"res\racialEmoteTime.txt");
            using (StreamReader streamReader = new StreamReader(new MemoryStream(Encoding.ASCII.GetBytes(RacialEmoteTime.Times)))) {
                int races = int.Parse(streamReader.ReadLine());
                for (int raceIndex = 0; raceIndex < races; raceIndex++) {
                    string raceName = streamReader.ReadLine();
                    string gender = streamReader.ReadLine();
                    TimeCodeData timeCodeDataMasculine = new TimeCodeData();
                    timeCodeDataMasculine.Descriptor = raceIndex + "_" + gender;
                    for (int i = 0; i < 16; i++) {
                        string value = streamReader.ReadLine();
                        if (!string.IsNullOrWhiteSpace(value)) {
                            try {
                                timeCodeDataMasculine.TimeCodes.Add(decimal.Parse(value, new CultureInfo("en-US")));
                            } catch {
                                timeCodeDataMasculine.TimeCodes.Add(decimal.Parse(value.Replace(".", ",")));
                            }
                        }
                    }

                    gender = streamReader.ReadLine();
                    TimeCodeData timeCodeDataFeminine = new TimeCodeData();
                    timeCodeDataFeminine.Descriptor = raceIndex + "_" + gender;
                    for (int i = 0; i < 16; i++) {
                        string value = streamReader.ReadLine();
                        if (!string.IsNullOrWhiteSpace(value)) {
                            try {
                                timeCodeDataFeminine.TimeCodes.Add(decimal.Parse(value));
                            } catch {
                                timeCodeDataFeminine.TimeCodes.Add(decimal.Parse(value.Replace(".", ",")));
                            }
                        }
                    }
                    timeCodeData.Add(timeCodeDataMasculine.Descriptor, timeCodeDataMasculine);
                    timeCodeData.Add(timeCodeDataFeminine.Descriptor, timeCodeDataFeminine);
                }
            }
        }
    }
}
