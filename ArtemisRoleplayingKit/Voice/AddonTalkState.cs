namespace RoleplayingVoiceDalamud.Voice {
    internal class AddonTalkState {
        private string speaker;
        private string text;

        public AddonTalkState(string speaker, string text) {
            this.Speaker = speaker;
            this.text = text;
        }

        public string Text { get => text; set => text = value; }
        public string Speaker { get => speaker; set => speaker = value; }
    }
}