using Ktisis.Structs.Actor;
using Ktisis.Structs.Bones;
using RoleplayingMediaCore;
using System.Numerics;

namespace RoleplayingVoiceDalamud.GameObjects {
    public unsafe class MediaBoneObject : IMediaGameObject {
        private Bone _bone;
        private unsafe Actor* _actor;
        private unsafe ActorModel* _actorModel;
        string _name = "";
        public unsafe MediaBoneObject(Bone bone, Actor* actor, ActorModel* actorModel) {
            _bone = bone;
            _actor = actor;
            _actorModel = actorModel;
            _name = _bone.HkaBone.Name.String + _actor->GetName();
        }

        string IMediaGameObject.Name => _name;

        Vector3 IMediaGameObject.Position => _bone != null && _actor != null && _actorModel != null ? _bone.GetWorldPos(_actor, _actorModel) : new Vector3();

        float IMediaGameObject.Rotation => 0;

        Vector3 IMediaGameObject.Forward => new Vector3();

        Vector3 IMediaGameObject.Top => new Vector3();

        string IMediaGameObject.FocusedPlayerObject => "";
    }
}
