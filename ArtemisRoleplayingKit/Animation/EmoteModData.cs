using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RoleplayingVoiceDalamud.Animation {
    public class EmoteModData {
        string _emote = "";
        uint _emoteId = 0;
        uint _animationId = 0;
        string _foundModName;

        public EmoteModData(string emote, uint emoteId, uint animationId, string foundModName) {
            _emote = emote;
            _emoteId = emoteId;
            _animationId = animationId;
            _foundModName = foundModName;
        }

        public string Emote { get => _emote; set => _emote = value; }
        public uint EmoteId { get => _emoteId; set => _emoteId = value; }
        public uint AnimationId { get => _animationId; set => _animationId = value; }
        public string FoundModName { get => _foundModName; set => _foundModName = value; }
    }
}
