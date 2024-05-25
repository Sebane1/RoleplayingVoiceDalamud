namespace RoleplayingSpeechDalamud.GPT_API
{
    internal class GPTStreamResponse
    {
        public string @event { get; set; }
        public int message_num { get; set; }
        public string text { get; set; }
    }
}
