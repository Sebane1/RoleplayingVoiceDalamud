using Dalamud.Game.ClientState.Objects.Types;
using Ktisis.Structs.Actor;
using Ktisis.Structs;
using RoleplayingMediaCore;

namespace RoleplayingVoiceDalamud.GameObjects {
    public class MediaBoneManager {
        public static void CheckForValidBoneSounds(ICharacter character, CharacterVoicePack characterVoicePack, RoleplayingMediaManager roleplayingMediaManager, MediaManager mediaManager) {
            unsafe {
                Actor* characterActor = (Actor*)character.Address;
                var model = characterActor->Model;
                for (int i = 0; i < model->Skeleton->PartialSkeletonCount; i++) {
                    var partialSkeleton = model->Skeleton->PartialSkeletons[i];
                    var pos = partialSkeleton.GetHavokPose(0);
                    if (pos != null) {
                        var skeleton = pos->Skeleton;
                        for (var i2 = 1; i2 < skeleton->Bones.Length; i2++) {
                            var bone = model->Skeleton->GetBone(i, i2);
                            string value = characterVoicePack.GetMisc(bone.HkaBone.Name.String, false, true);
                            if (!string.IsNullOrEmpty(value)) {
                                mediaManager.PlayAudio(new MediaBoneObject(bone, characterActor, model), value, SoundType.LoopWhileMoving, false, 0);
                            }
                        }
                    }
                }
            }
        }
    }
}
