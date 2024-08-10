using FFXIVClientStructs.Havok.Common.Base.Math.Quaternion;
using Ktisis.Structs.Actor;
using Ktisis.Structs.Bones;
using RoleplayingMediaCore;
using System;
using System.Numerics;

namespace RoleplayingVoiceDalamud.GameObjects {
    public unsafe class MediaBoneObject : IMediaGameObject {
        private Bone _bone;
        private unsafe Actor* _actor;
        private unsafe ActorModel* _actorModel;
        string _name = "";
        private bool _invalid;

        public unsafe MediaBoneObject(Bone bone, Actor* actor, ActorModel* actorModel) {
            _bone = bone;
            _actor = actor;
            _actorModel = actorModel;
            _name = _bone.HkaBone.Name.String + _actor->GetName();
        }

        string IMediaGameObject.Name => _name;

        Vector3 IMediaGameObject.Position {
            get {
                try {
                    return _bone.GetWorldPos(_actor, _actorModel);
                } catch {
                    _invalid = true;
                    return Vector3.Zero;
                }
            }
        }
        Vector3 IMediaGameObject.Rotation {
            get {
                try {
                    return Q2E(_bone.Transform.Rotation);
                } catch {
                    _invalid = true;
                    return Vector3.Zero;
                }
            }
        }

        Vector3 IMediaGameObject.Forward => new Vector3();

        Vector3 IMediaGameObject.Top => new Vector3();
        public static Vector3 Q2E(hkQuaternionf q) // Returns the XYZ in ZXY
    {
            Vector3 angles;

            angles.X = (float)Math.Atan2(2 * (q.W * q.X + q.Y * q.Z), 1 - 2 * (q.X * q.X + q.Y * q.Y));
            if (Math.Abs(2 * (q.W * q.Y - q.Z * q.X)) >= 1) angles.Y = (float)Math.CopySign(Math.PI / 2, 2 * (q.W * q.Y - q.Z * q.X));
            else angles.Y = (float)Math.Asin(2 * (q.W * q.Y - q.Z * q.X));
            angles.Z = (float)Math.Atan2(2 * (q.W * q.Z + q.X * q.Y), 1 - 2 * (q.Y * q.Y + q.Z * q.Z));

            return new Vector3() {
                X = (float)(180 / Math.PI) * angles.X,
                Y = (float)(180 / Math.PI) * angles.Y,
                Z = (float)(180 / Math.PI) * angles.Z
            };
        }
        string IMediaGameObject.FocusedPlayerObject => "";

        bool IMediaGameObject.Invalid => _invalid;
    }
}
