using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using RoleplayingMediaCore;
using System;
using System.Numerics;

namespace RoleplayingVoiceDalamud {
    internal class MediaGameObject : IGameObject {
        private GameObject _gameObject;
        private string _name = "";
        private Vector3 _position = new Vector3();

        string IGameObject.Name {
            get {
                try {
                    return _gameObject != null ? (_gameObject.Name != null ? _gameObject.Name.TextValue : _name) : _name;
                } catch {
                    return _name;
                }
            }
        }

        Vector3 IGameObject.Position {
            get {
                try {
                    return (_gameObject != null ? _gameObject.Position : _position);
                } catch {
                    return _position;
                }
            }
        }

        float IGameObject.Rotation {
            get {
                try {
                    return _gameObject != null ? _gameObject.Rotation : 0;
                } catch {
                    return 0;
                }
            }
        }

        string IGameObject.FocusedPlayerObject {
            get {
                if (_gameObject != null) {
                    try {
                        return _gameObject.TargetObject != null ?
                            (_gameObject.TargetObject.ObjectKind == ObjectKind.Player ? _gameObject.TargetObject.Name.TextValue : "")
                            : "";
                    } catch {
                        return "";
                    }
                } else {
                    return "";
                }
            }
        }

        Vector3 IGameObject.Forward {
            get {
                float rotation = _gameObject != null ? _gameObject.Rotation : 0;
                return new Vector3((float)Math.Cos(rotation), 0, (float)Math.Sin(rotation));
            }
        }

        public Vector3 Top {
            get {
                return new Vector3(0, 1, 0);
            }
        }

        public MediaGameObject(GameObject gameObject) {
            _gameObject = gameObject;
        }
        public MediaGameObject(string name, Vector3 position) {
            _name = name;
            _position = position;
        }
        public MediaGameObject(GameObject gameObject, string name, Vector3 position) {
            _gameObject = gameObject;
            _name = name;
            _position = position;
        }
    }
}
