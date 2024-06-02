using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using Lumina.Excel.GeneratedSheets;
using RoleplayingSpeechDalamud;
using RoleplayingVoice;
using RoleplayingVoiceDalamud.Catalogue;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RoleplayingVoiceDalamud.NPC {
    public class NPCConversationManager {
        private GPTWrapper _gptWrapper;
        private Plugin _plugin;
        private Character _aiCharacter;
        private string emotion;

        public NPCConversationManager(string name, string baseDirectory, Plugin plugin, Character receivingCharacter) {
            string aiName = name.Split(" ")[0];
            _gptWrapper = new GPTWrapper(aiName, Path.Combine(baseDirectory, name + "-memories.json"));
            _plugin = plugin;
            _aiCharacter = receivingCharacter;
        }
        public async Task<string> SendMessage(Character sendingCharacter, Character receivingCharacter, string aiName,
            string aiGreeting, string message, string setting, string aiDescription) {
            string senderName = sendingCharacter.Name.TextValue.Split(" ")[0];
            string aiMessage = await _gptWrapper.SendMessage(senderName, message, $@" smiles ""{aiGreeting}""",
            GetPlayerDescription(sendingCharacter), aiDescription.Trim('.').Trim() + ". " + GetPlayerDescription(receivingCharacter, true, aiName), setting, 2);
            string correctedMessage = PenumbraAndGlamourerHelperFunctions.GetCustomization(sendingCharacter).Customize.Gender.Value == 1 ? GenderFix(aiMessage) : aiMessage;
            _gptWrapper.AddToHistory(senderName, message, correctedMessage);
            Task.Run(() => {
                EmoteReaction(correctedMessage);
            });
            return correctedMessage;
        }
        public string GetPlayerDescription(Character player, bool skipSummary = false, string alias = "") {
            var customization = PenumbraAndGlamourerHelperFunctions.GetCustomization(player);
            string gender = customization.Customize.Gender.Value == 1 ? "female" : "male";
            string pronouns = customization.Customize.Gender.Value == 1 ? "she/her" : "he/him";
            string pronounSingular = customization.Customize.Gender.Value == 1 ? "her" : "his";
            string pronounSingularAlternate = customization.Customize.Gender.Value == 1 ? "She" : "He";
            string breastSize = customization.Customize.Gender.Value == 0 ? "no breasted" : customization.Customize.BustSize.Value > 50 ? "big breasted" : "small breasted";
            string race = GetRaceDescription(customization.Customize.Race.Value, pronounSingularAlternate);
            string skin = GetSkinTone(customization.Customize.SkinColor.Value);
            var summaries = !skipSummary ? _gptWrapper.GetConversationalMemory(player.Name.TextValue) : new List<string>();
            string chatSummaries = "\n\nIn the past " + _gptWrapper.Personality
            + " and " + player.Name.TextValue + " had the following situations:";
            if (summaries.Count == 0) {
                chatSummaries = "";
            } else {
                for (int i = summaries.Count - 1; i >= Math.Clamp(summaries.Count - 5, 0, summaries.Count); i--) {
                    if (i > -1) {
                        var summary = summaries[i];
                        chatSummaries += "\nEncounter " + i + summary;
                    } else {
                        break;
                    }
                }
            }
            string name = !string.IsNullOrEmpty(alias) ? alias : player.Name.TextValue.Split(" ")[0];
            return $"{name} is a {gender}. {pronounSingularAlternate} is a {skin} race of {race}, and " +
                $"{pronounSingular} breast size is {breastSize}. {pronounSingularAlternate} has " +
                $"{GetHairColour(customization.Customize.HairColor.Value)} hair colour and " +
                $"{GetLipColour(customization.Customize.LipColor.Value)} lip colour. " +
                $"{GetPlayerExperience(player.Level, player.ClassJob.GameData.NameEnglish, pronounSingularAlternate)}." +
                chatSummaries;
        }
        private string GetSkinTone(int value) {
            int index = 0;
            int xCoordinate = 0;
            for (int y = 0; y < 24; y++) {
                for (int x = 0; x < 8; x++) {
                    if (index == value) {
                        xCoordinate = x;
                        break;
                    }
                    index++;
                }
                if (index == value) {
                    break;
                }
            }
            switch (xCoordinate) {
                case 0:
                case 1:
                case 2:
                case 3:
                    return "light skinned";
                case 4:
                case 5:
                case 6:
                case 7:
                    return "dark skinned";
            }
            return "unknown skinned";
        }
        private string GetRaceDescription(int race, string pronoun) {
            switch (race) {
                case 0:
                    return $"Hyur. {pronoun} looks like an average person.";
                case 1:
                    return $"Highlander. {pronoun} looks muscular, tough";
                case 2:
                    return $"Elezen. {pronoun} looks like a tall elf with pointy ears";
                case 4:
                    return $"Miqo'te. {pronoun} has cat ears, a tail, and likes to meow.";
                case 3:
                    return $"Roegadyn. {pronoun} a tall and muscular sea faring race.";
                case 5:
                    return $"Lalafel. {pronoun} looks like a short stubby person.";
                case 6:
                    return $"Au'Ra. {pronoun} has dragonlike scales, horns, a scaley tail";
                case 7:
                    return $"Hrothgar. {pronoun} looks like a furry humanoid cat.";
                case 8:
                    return $"Viera. {pronoun} is tall, and has cute bunny ears";
            }
            return "Unidentified";
        }
        private string GetPlayerExperience(int level, string className, string pronoun) {
            if (level < 10) {
                return pronoun + " is a very inexperienced " + className;
            } else if (level < 20) {
                return pronoun + " is a learning " + className;
            } else if (level < 30) {
                return pronoun + " is an umimpressive " + className;
            } else if (level < 40) {
                return pronoun + " is an average " + className;
            } else if (level < 50) {
                return pronoun + " is an above average " + className;
            } else if (level < 60) {
                return pronoun + " is a decently skilled " + className;
            } else if (level < 70) {
                return pronoun + " is a an experienced " + className;
            } else if (level < 80) {
                return pronoun + " is a highly experienced " + className;
            } else if (level < 90) {
                return pronoun + " is a very outstanding " + className;
            } else if (level < 100) {
                return pronoun + " is the best of the best " + className;
            }
            return pronoun + " has no skills";
        }
        private string GetLipColour(int value) {
            string[] names = new string[] { "Grey","Tinted Yellow","Yellow","Tinted Orange","Orange",
                "Red","Pink","Purple","Blue","Greenish Blue","Green","Tinted Green" };
            int index = 0;
            int xCoordinate = 0;
            int yCoordinate = 0;
            for (int i = 0; i < 2; i++) {
                for (int y = 0; y < 12; y++) {
                    for (int x = 0; x < 8; x++) {
                        if (index == value) {
                            xCoordinate = x;
                            yCoordinate = y;
                            break;
                        }
                        index++;
                    }
                    if (index == value) {
                        break;
                    }
                }
                if (index == value) {
                    break;
                }
            }
            switch (xCoordinate) {
                case 0:
                case 1:
                case 2:
                case 3:
                    return "light " + names[yCoordinate].ToLower();
                case 4:
                case 5:
                case 6:
                case 7:
                    return "dark " + names[yCoordinate].ToLower();
            }
            return "unknown skinned";
        }
        private string GetHairColour(int value) {
            string[] names = new string[] { "Grey","Cream","Greyish Cream","Tinted Yellow","Greyish Tinted Yellow",
                "Yellow","Greyish Tinted Yellow","Tinted Orange","Greyish Tinted Orange","Orange","Greyish Orange",
                "Red","Greyish Red","Pink","Magenta","Greyish Magenta","Greyish Pink","Purple","Greyish Purple","Blue",
                "Greyish Greenish Blue","Green","Greyish Green","Tinted Green" };
            int index = 0;
            int xCoordinate = 0;
            int yCoordinate = 0;
            for (int y = 0; y < 24; y++) {
                for (int x = 0; x < 8; x++) {
                    if (index == value) {
                        xCoordinate = x;
                        yCoordinate = y;
                        break;
                    }
                    index++;
                }
                if (index == value) {
                    break;
                }
            }
            switch (xCoordinate) {
                case 0:
                case 1:
                case 2:
                case 3:
                    return "light " + names[yCoordinate].ToLower();
                case 4:
                case 5:
                case 6:
                case 7:
                    return "dark " + names[yCoordinate].ToLower();
            }
            return "unknown skinned";
        }
        string GenderFix(string value) {
            return value.Replace(" himself", " herself").Replace("He ", "She ")
                                 .Replace(" he ", " she ").Replace(" he?", " she?")
                                 .Replace(" hes ", " she's ").Replace(" he's ", " she's ").Replace("He's ", "She's ")
                                 .Replace(" him ", " her ").Replace(" him,", " her,").Replace(" him.", " her.").Replace(" his ", " her ").Replace(" his.", " her.")
                                 .Replace("His ", "Her ").Replace(" men ", " women ").Replace(" men.", " women.").Replace(" sir ", " ma'am ")
                                 .Replace(" man ", " woman ").Replace(" boy", " girl").Replace(" man.", " woman.");
        }
        private async void EmoteReaction(string messageValue) {
            var emotes = _plugin.DataManager.GetExcelSheet<Emote>();
            string[] messageEmotes = messageValue.Replace("*", " ").Split("\"");
            string emoteString = " ";
            for (int i = 1; i < messageEmotes.Length + 1; i++) {
                if ((i + 1) % 2 == 0) {
                    emoteString += messageEmotes[i - 1] + " ";
                }
            }
            foreach (var item in emotes) {
                if (!string.IsNullOrWhiteSpace(item.Name.RawString)) {
                    if ((emoteString.ToLower().Contains(" " + item.Name.RawString.ToLower() + " ") ||
                        emoteString.ToLower().Contains(" " + item.Name.RawString.ToLower() + "s ") ||
                        emoteString.ToLower().Contains(" " + item.Name.RawString.ToLower() + "ed ") ||
                        emoteString.ToLower().Contains(" " + item.Name.RawString.ToLower() + "ing ") ||
                        emoteString.ToLower().EndsWith(" " + item.Name.RawString.ToLower()) ||
                        emoteString.ToLower().Contains(" " + item.Name.RawString.ToLower() + "s") ||
                        emoteString.ToLower().Contains(" " + item.Name.RawString.ToLower() + "ed") ||
                        emoteString.ToLower().Contains(" " + item.Name.RawString.ToLower() + "ing"))
                        || (emoteString.ToLower().Contains(" " + item.Name.RawString.ToLower()) && item.Name.RawString.Length > 3)) {
                        _plugin.DoEmote("/" + item.Name.RawString.ToLower(), _aiCharacter, false);
                        break;
                    }
                }
            }
        }
    }
}
