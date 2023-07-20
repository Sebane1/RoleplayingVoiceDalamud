using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using RoleplayingVoiceCore;
using System.Numerics;

namespace RoleplayingVoiceDalamud {
    internal class PlayerObject : IPlayerObject {
        private PlayerCharacter _playerCharacter;
        private string _name = "";
        private Vector3 _position;

        string IPlayerObject.Name => _playerCharacter != null ? _playerCharacter.Name.TextValue : _name;

        Vector3 IPlayerObject.Position => _playerCharacter != null ? _playerCharacter.Position : _position;

        float IPlayerObject.Rotation => _playerCharacter != null ? _playerCharacter.Rotation : 0;

        string IPlayerObject.FocusedPlayerObject {
            get {
                if (_playerCharacter != null) {
                    return _playerCharacter.TargetObject != null ?
                        (_playerCharacter.TargetObject.ObjectKind == ObjectKind.Player ? _playerCharacter.TargetObject.Name.TextValue : "")
                        : null;
                } else {
                    return "";
                }
            }
        }
        public PlayerObject(PlayerCharacter playerCharacter) {
            _playerCharacter = playerCharacter;
        }
        public PlayerObject(string name, Vector3 position) {
            _name = name;
            _position = position;
        }
        public PlayerObject(PlayerCharacter playerCharacter, string name, Vector3 position) {
            _playerCharacter = playerCharacter;
            _name = name;
            _position = position;
        }
    }
}
