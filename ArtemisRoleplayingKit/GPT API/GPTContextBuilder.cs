using Anamnesis.Services;
using System.Collections.Generic;

namespace RoleplayingSpeechDalamud.GPT_API
{
    public class GPTContextBuilder
    {
        string _author = "";
        string _title = "";
        string _genre = "";
        string _aiName = "";
        string _userName = "";
        string _aiTraits = "";
        string _userTraits = "";
        private List<KeyValuePair<string, string>> _memories = new List<KeyValuePair<string, string>>();
        GPTHistory _history;
        private string _setting;

        public GPTHistory History { get => _history; set => _history = value; }
        public List<KeyValuePair<string, string>> Memories { get => _memories; set => _memories = value; }

        public GPTContextBuilder(string author, string title, string genre,
        string aiName, string userName, string aiTraits, string userTraits, GPTHistory history)
        {
            _author = author;
            _title = title;
            _genre = genre;
            _aiName = aiName;
            _userName = userName;
            _aiTraits = aiTraits;
            _userTraits = userTraits;
            _history = history;
        }
        public void UpdateSetting(string setting) {
            _setting = setting;
        }
        public void UpdateTitle(string author, string title, string genre)
        {
            _author = author;
            _title = title;
            _genre = genre;
        }
        public void UpdateAITraits(string name, string traits)
        {
            _aiName = name;
            _aiTraits = traits;
        }
        public void UpdateUserTraits(string name, string traits)
        {
            _userName = name.Split(" ")[0];
            _userTraits = traits;
        }
        public void UpdateMemories(List<KeyValuePair<string, string>> memories)
        {
            if (memories != null && memories.Count > 0)
            {
                _memories = memories;
            }
        }
        public override string ToString()
        {
            string context = $"[ Author: {_author}; Title: {_title}; Genre: {_genre}]";
            if (!string.IsNullOrEmpty(_setting)) {
                context += $"\n----";
                context += $"\n[ Setting ]\n" + _setting;
            }
            if (!string.IsNullOrEmpty(_aiTraits))
            {
                context += $"\n----";
                context += $"\n[ Knowledge: {_aiName} ]\n" + _aiTraits;
            }
            if (!string.IsNullOrEmpty(_userTraits))
            {
                context += $"\n----";
                context += $"\n[ Knowledge: {_userName} ]\n" + _userTraits;
            }
            context += $"\n----";
            context += $"\n[ Knowledge: Chat Summary ]\r\nThe chat summary will always summarize past events between {_userName} and {_aiName} in short digestable form. The summary only references the past.";
            if (_memories != null && _memories.Count > 0)
            {
                foreach (var memory in Memories)
                {
                    context += $"\n----";
                    context += $"\n[ Knowledge: {memory.Key} ]\n" + memory.Value;
                }
            }
            context += $"\n***\n";
            context += $"\n[ Style: roleplaying ]\n";
            foreach (var value in _history.Visible)
            {
                foreach (var message in value)
                {
                    context += message + "\n";
                }
            }
            return context;
        }
    }
}
