using System;

namespace SoundFilter {
    internal class InterceptedSound : EventArgs {
        public string SoundPath { get; set; }

        public bool isBlocking { get; set; }
    }
}