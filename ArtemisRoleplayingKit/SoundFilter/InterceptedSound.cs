using System;
using System.Numerics;

namespace SoundFilter {
    internal class InterceptedSound : EventArgs {
        public string SoundPath { get; set; }

        public bool isBlocking { get; set; }

        /// <summary>
        /// The 3D world position of the sound source, extracted from the SoundManager's
        /// SoundData pool. Null if the sound is not positional or couldn't be resolved.
        /// Use this to correlate with game object positions in the object table.
        /// </summary>
        public Vector3? SourcePosition { get; set; }
    }
}