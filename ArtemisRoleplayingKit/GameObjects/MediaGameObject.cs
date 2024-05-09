using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Memory;
using RoleplayingMediaCore;
using System;
using System.Numerics;

namespace RoleplayingVoiceDalamud {
    public unsafe class MediaGameObject : IGameObject {
        private GameObject _gameObject;
        private FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject* _gameObjectPointer;
        private string _name = "";
        private Vector3 _position = new Vector3();

        string IGameObject.Name {
            get {
                try {
                    return _gameObjectPointer != null ? (_gameObjectPointer != null ? MemoryHelper.ReadSeString((nint)_gameObjectPointer->Name, 64).TextValue : _name) : _name;
                } catch {
                    return _name;
                }
            }
        }

        Vector3 IGameObject.Position {
            get {
                try {
                    return (_gameObjectPointer != null ? _gameObjectPointer->Position : _position);
                } catch {
                    return _position;
                }
            }
        }

        float IGameObject.Rotation {
            get {
                try {
                    return _gameObjectPointer != null ? _gameObjectPointer->Rotation : 0;
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
                float rotation = _gameObjectPointer != null ? _gameObjectPointer->Rotation : 0;
                return new Vector3((float)Math.Cos(rotation), 0, (float)Math.Sin(rotation));
            }
        }

        public Vector3 Top {
            get {
                return new Vector3(0, 1, 0);
            }
        }

        public GameObject GameObject { get => _gameObject; set => _gameObject = value; }

        public MediaGameObject(GameObject gameObject) {
            if (gameObject != null) {
                _gameObject = gameObject;
                _gameObjectPointer = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)gameObject.Address;
            }
        }
        unsafe public MediaGameObject(FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject* gameObject) {
            _gameObjectPointer = gameObject;
        }
        public MediaGameObject(string name, Vector3 position) {
            _name = name;
            _position = position;
        }
        public MediaGameObject(GameObject gameObject, string name, Vector3 position) {
            _gameObject = gameObject;
            _gameObjectPointer = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)gameObject.Address;
            _name = name;
            _position = position;
        }
    }
}
