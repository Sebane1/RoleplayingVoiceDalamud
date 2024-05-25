using System.Collections.Generic;

namespace RoleplayingSpeechDalamud.GPT_API
{
    public class GPTHistory
    {
        public GPTHistory(string name, string firstMessage, string botResponse, string summaryExample)
        {
            Visible = new List<List<string>> { new List<string> { $"{name} smiles happily \"Hello there!, how are you!\"", botResponse, "[Chat Summary: " + summaryExample + "]" } };
        }

        public List<List<string>> Visible { get; set; }

        public string GetLastVisibleItem()
        {
            if (Visible.Count > 0)
            {
                if (Visible[Visible.Count - 1].Count > 0)
                {
                    return Visible[Visible.Count - 1][Visible[Visible.Count - 1].Count - 1];
                }
            }
            return "";
        }

        public void ReplaceLastVisibleItem(string value)
        {
            if (Visible.Count > 0)
            {
                if (Visible[Visible.Count - 1].Count > 0)
                {
                    Visible[Visible.Count - 1][Visible[Visible.Count - 1].Count - 1] = value;
                }
            }
        }
    }
}
