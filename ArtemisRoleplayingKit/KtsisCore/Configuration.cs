using System;
using System.Numerics;
using System.Collections.Generic;

namespace Ktisis {
    [Serializable]
    public static class Configuration {
        public static Dictionary<string, Dictionary<string, Vector3>> CustomBoneOffset = new Dictionary<string, Dictionary<string, Vector3>>();
    }
    [Serializable]
    public class ReferenceInfo {
        public bool Showing { get; set; }
        public string? Path { get; set; }
    }
    [Serializable]
    public enum OpenKtisisMethod {
        Manually,
        OnPluginLoad,
        OnEnterGpose,
    }
}
