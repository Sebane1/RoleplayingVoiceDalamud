using System;

namespace SoundFilter {
    internal class InterceptedSound : EventArgs {
        public string SoundPath { get; set; }
    }
}