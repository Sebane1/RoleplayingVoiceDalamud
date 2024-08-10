using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Memory;
using RoleplayingMediaCore;
using System;
using System.Numerics;
using IMediaGameObject = RoleplayingMediaCore.IMediaGameObject;

namespace RoleplayingVoiceDalamud {
    public unsafe class MediaGameObject : IMediaGameObject {
        private Dalamud.Game.ClientState.Objects.Types.IGameObject _gameObject;
        private FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject* _gameObjectPointer;
        private string _name = "";
        private Vector3 _position = new Vector3();

        string IMediaGameObject.Name {
            get {
                try {
                    return _gameObjectPointer != null ? (_gameObjectPointer != null ? _gameObjectPointer->NameString : _name) : _name;
                } catch {
                    return _name;
                }
            }
        }

        Vector3 IMediaGameObject.Position {
            get {
                try {
                    return (_gameObjectPointer != null ? _gameObjectPointer->Position : _position);
                } catch {
                    return _position;
                }
            }
        }

        Vector3 IMediaGameObject.Rotation {
            get {
                try {
                    return new Vector3(0, _gameObjectPointer != null ? _gameObjectPointer->Rotation : 0, 0);
                } catch {
                    return new Vector3(0, 0, 0);
                }
            }
        }

        string IMediaGameObject.FocusedPlayerObject {
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

        Vector3 IMediaGameObject.Forward {
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

        public Dalamud.Game.ClientState.Objects.Types.IGameObject GameObject { get => _gameObject; set => _gameObject = value; }

        bool IMediaGameObject.Invalid => false;

        public MediaGameObject(Dalamud.Game.ClientState.Objects.Types.IGameObject gameObject) {
            if (gameObject != null) {
                _gameObject = gameObject;
                _gameObjectPointer = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)(gameObject as ICharacter).Address;
            }
        }
        unsafe public MediaGameObject(FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject* gameObject) {
            _gameObjectPointer = gameObject;
        }
        public MediaGameObject(string name, Vector3 position) {
            _name = name;
            _position = position;
        }
        public MediaGameObject(Dalamud.Game.ClientState.Objects.Types.IGameObject gameObject, string name, Vector3 position) {
            _gameObject = gameObject;
            _gameObjectPointer = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)(gameObject as ICharacter).Address;
            _name = name;
            _position = position;
        }
    }
}
