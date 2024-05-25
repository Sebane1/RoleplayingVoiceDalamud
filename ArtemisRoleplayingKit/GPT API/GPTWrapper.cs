using RoleplayingSpeechDalamud.GPT_API;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using static Anamnesis.Files.CmToolAppearanceFile;

namespace RoleplayingSpeechDalamud {
    public class GPTWrapper : IDisposable {
        private string _personality;
        ConcurrentDictionary<string, GPTContextBuilder> _histories = new ConcurrentDictionary<string, GPTContextBuilder>();
        ConcurrentDictionary<string, int> _persistenceCounter = new ConcurrentDictionary<string, int>();
        MemoryContextManager memoryContextManager;

        public string Personality { get => _personality; }

        public GPTWrapper(string personality, string memoryPath) {
            _personality = personality;
            memoryContextManager = new MemoryContextManager(memoryPath);
        }

        public async Task<string> SendMessage(string name, string message, string aiGreeting, string userDetails, string aiDetails, string setting, int maxContext) {
            var newHistory = new GPTHistory(name, userDetails,
                _personality + aiGreeting, $"{name} said hello, and {_personality} responded back with their own greeting.");
            if (!_histories.ContainsKey(name)) {
                _histories[name] = new GPTContextBuilder("Square Enix", "Final Fantasy XIV", "fantasy",
                _personality, name, aiDetails, userDetails, newHistory);
                _histories[name].UpdateMemories(memoryContextManager.GetMemoriesInValue(message));
            } else {
                _histories[name].UpdateAITraits(_personality, aiDetails);
                _histories[name].UpdateUserTraits(name, userDetails);
                _histories[name].UpdateMemories(memoryContextManager.GetMemoriesInValue((!string.IsNullOrEmpty(userDetails) ? "" : name + " ") + message));
            }
            _histories[name].UpdateSetting(setting);
            string lastValue = _histories.ContainsKey(name) ? _histories[name].History.GetLastVisibleItem() : Guid.NewGuid().ToString();
            string response = await new GPTRequestSender().GetGPTResponse(name, _histories[name].ToString()
                + name.Split(" ")[0] + DetectFormatting(message.Trim()) + "\n" + _personality, _personality, false);
            AddToHistory(name, message, response);
            if (_persistenceCounter.ContainsKey(name)) {
                _persistenceCounter[name]++;
            } else {
                _persistenceCounter[name] = 1;
            }
            if (_persistenceCounter[name] >= maxContext) {
                memoryContextManager.AddConversationalMemory(name, await GetSummary(name));
                _persistenceCounter[name] = 0;
            }
            if (_histories[name].History.Visible.Count > maxContext) {
                _histories[name].History.Visible.RemoveAt(1);
            }
            string value = _histories[name].History.GetLastVisibleItem();
            Thread.Sleep(1000);
            return WordFilter(value);
        }

        public void ChangeSetting(string name, string setting) {
            _histories[name].UpdateSetting(setting);
        }
        public async Task<string> GetSummary(string name) {
            string lastValue = _histories.ContainsKey(name) ? _histories[name].History.GetLastVisibleItem() : Guid.NewGuid().ToString();
            string response = await new GPTRequestSender().GetGPTResponse(name, _histories[name].ToString()
                + "[Chat Summary:", _personality, false);
            return response.Replace("[Chat Summary:", null);
        }

        public void Dispose() {
            _histories.Clear();
        }
        public async void AddConversationalMemory(string key) {
            memoryContextManager.AddConversationalMemory(key, await GetSummary(key));
        }
        internal void ClearHistory(string sender) {
            _histories.TryRemove(sender, out var value);
        }
        public List<string> GetConversationalMemory(string name) {
            return memoryContextManager.GetConversationalMemory(name);
        }

        public void AddToHistory(string sender, string userText, string botResponse) {
            if (_histories.ContainsKey(sender)) {
                string trimmedUserText = userText.Trim();
                string trimmedBotResponse = botResponse.Trim();
                _histories[sender].History.Visible.Add(new List<string>() {
                sender.Split(" ")[0] + DetectFormatting(trimmedUserText), _personality + DetectFormatting(trimmedBotResponse) });
            }
        }

        public string WordFilter(string value) {
            if (value.Length > 0 && value.StartsWith(_personality)) {
                int i = value.IndexOf(" ") + 1;
                value = value.Substring(i) + (value.Split('"').Length == 1 ? @"""" : "");
            }
            return value;
        }
        public void AddMemory(string title, string description) {
            memoryContextManager.AddMemory(title, description);
        }
        public string DetectFormatting(string value) {
            if (value.StartsWith('"')) {
                if (value.Contains("?")) {
                    return " asks, " + value;
                } else if (value.Contains("!")) {
                    return " exclaims, " + value;
                } else {
                    return " says, " + value;
                }
            } else {
                return " " + value;
            }
        }
    }
}
