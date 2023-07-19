using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using RoleplayingVoiceCore;
using System.Numerics;

namespace RoleplayingVoiceDalamud {
    internal class PlayerObject : IPlayerObject {
        private PlayerCharacter _playerCharacter;

        string IPlayerObject.Name => _playerCharacter != null ? _playerCharacter.Name.TextValue : "null";

        Vector3 IPlayerObject.Position => _playerCharacter != null ? _playerCharacter.Position : Vector3.Zero;

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
    }
}
