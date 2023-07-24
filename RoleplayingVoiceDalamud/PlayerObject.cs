using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using RoleplayingVoiceCore;
using System.Numerics;

namespace RoleplayingVoiceDalamud {
    internal class PlayerObject : IPlayerObject {
        private PlayerCharacter _playerCharacter;
        private string _name = "";
        private Vector3 _position = new Vector3();

        string IPlayerObject.Name => _playerCharacter != null ? (_playerCharacter.Name != null ? _playerCharacter.Name.TextValue : _name) : _name;

        Vector3 IPlayerObject.Position {
            get {
                try {
                    return (_playerCharacter != null ? _playerCharacter.Position : _position);
                } catch {
                    return _position;
                }
            }
        }

        float IPlayerObject.Rotation => _playerCharacter != null ? _playerCharacter.Rotation : 0;

        string IPlayerObject.FocusedPlayerObject {
            get {
                if (_playerCharacter != null) {
                    try {
                        return _playerCharacter.TargetObject != null ?
                            (_playerCharacter.TargetObject.ObjectKind == ObjectKind.Player ? _playerCharacter.TargetObject.Name.TextValue : "")
                            : "";
                    } catch {
                        return "";
                    }
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
