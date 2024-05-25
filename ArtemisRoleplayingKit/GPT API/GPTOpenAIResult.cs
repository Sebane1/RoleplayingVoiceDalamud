using System.Collections.Generic;
namespace RoleplayingSpeechDalamud.GPT_API
{
    public class GPTOpenAIResult
    {
        public List<Choice> choices { get; set; }
        public int created { get; set; }
        public string id { get; set; }
        public string model { get; set; }
        public string @object { get; set; }
    }
    public class Choice
    {
        public string finish_reason { get; set; }
        public int index { get; set; }
        public Logprobs logprobs { get; set; }
        public string text { get; set; }
        public int token_index { get; set; }
    }

    public class Logprobs
    {
        public List<int> text_offset { get; set; }
        public List<string> tokens { get; set; }
    }
}
