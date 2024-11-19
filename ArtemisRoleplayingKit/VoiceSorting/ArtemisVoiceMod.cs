using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RoleplayingVoiceDalamud.VoiceSorting {
    public class ArtemisVoiceMod {
        private string name = "";
        private List<string> _files = new List<string>();
        private List<ArtemisVoiceMod> _artemisSubMods = new List<ArtemisVoiceMod>();
        private int _priority = 0;

        public List<string> Files { get => _files; set => _files = value; }
        public int Priority { get => _priority; set => _priority = value; }
        public List<ArtemisVoiceMod> ArtemisSubMods { get => _artemisSubMods; set => _artemisSubMods = value; }
        public string Name { get => name; set => name = value; }
    }
}
