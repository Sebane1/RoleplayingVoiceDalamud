using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace RoleplayingSpeechDalamud.GPT_API
{
    internal class MemoryContextManager
    {
        string memoryFile = "";
        Dictionary<string, string> memories = new Dictionary<string, string>();
        ConcurrentDictionary<string, List<string>> pastConversations = new ConcurrentDictionary<string, List<string>>();

        public MemoryContextManager(string memoryFile)
        {
            this.memoryFile = memoryFile;
            LoadMemories();
        }

        public void LoadMemories()
        {
            if (File.Exists(memoryFile))
            {
                var reader = File.ReadAllText(memoryFile);
                memories = JsonConvert.DeserializeObject<Dictionary<string, string>>(reader);
            }
            if (File.Exists(memoryFile.Replace(".json", "-conversation.json")))
            {
                var reader = File.ReadAllText(memoryFile.Replace(".json", "-conversation.json"));
                pastConversations = JsonConvert.DeserializeObject<ConcurrentDictionary<string, List<string>>>(reader);
            }
        }
        public void SaveMemories()
        {
            try
            {
                string json = JsonConvert.SerializeObject(memories);
                using (var writer = File.CreateText(memoryFile))
                {
                    writer.WriteLine(json);
                    writer.Flush();
                }
                json = JsonConvert.SerializeObject(pastConversations);
                using (var writer = File.CreateText(memoryFile.Replace(".json", "-conversation.json")))
                {
                    writer.WriteLine(json);
                    writer.Flush();
                }
            }
            catch
            {

            }
        }
        public void AddMemory(string key, string value)
        {
            memories[key.ToLower()] = value;
            SaveMemories();
        }
        public void AddConversationalMemory(string key, string value)
        {
            if (pastConversations.ContainsKey(key.ToLower()))
            {
                pastConversations[key.ToLower()].Add(value);
            }
            else
            {
                pastConversations[key.ToLower()] = new List<string> { value };
            }
            SaveMemories();
        }
        public List<string> GetConversationalMemory(string name)
        {
            if (pastConversations.ContainsKey(name.ToLower()))
            {
                return pastConversations[name.ToLower()];
            }
            else
            {
                return new List<string>();
            }
        }
        public List<KeyValuePair<string, string>> GetMemoriesInValue(string value)
        {
            List<KeyValuePair<string, string>> values = new List<KeyValuePair<string, string>>();
            string lowercaseValue = value.ToLower();
            foreach (string key in memories.Keys)
            {
                if (lowercaseValue.Contains(key))
                {
                    values.Add(new KeyValuePair<string, string>(key, memories[key]));
                }
            }
            return values;
        }
    }
}
