using Dalamud.Game.ClientState.Objects.Types;
using Ktisis.Structs.Actor;
using Ktisis.Structs;
using RoleplayingMediaCore;
using System.Collections.Generic;
using System.Numerics;
using System;
using FFXIVClientStructs.FFXIV.Common.Lua;
using RoleplayingVoice;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using RoleplayingVoiceDalamudWrapper;

namespace RoleplayingVoiceDalamud.GameObjects {
    public class MediaBoneManager {
        public static Dictionary<string, Dictionary<string, MovingObject>> _lastBonePositions = new Dictionary<string, Dictionary<string, MovingObject>>();
        public static void CheckForValidBoneSounds(ICharacter character, CharacterVoicePack characterVoicePack,
            RoleplayingMediaManager roleplayingMediaManager, MediaManager mediaManager) {
            unsafe {
                if (!_lastBonePositions.ContainsKey(character.Name.TextValue)) {
                    _lastBonePositions[character.Name.TextValue] = new Dictionary<string, MovingObject>();
                }
                try {
                    if (character != null) {
                        Actor* characterActor = (Actor*)character.Address;
                        var model = characterActor->Model;
                        if (model != null && model->Skeleton != null) {
                            for (int i = 0; i < model->Skeleton->PartialSkeletonCount; i++) {
                                var partialSkeleton = model->Skeleton->PartialSkeletons[i];
                                var pos = partialSkeleton.GetHavokPose(0);
                                if (pos != null) {
                                    var skeleton = pos->Skeleton;
                                    for (var i2 = 1; i2 < skeleton->Bones.Length; i2++) {
                                        if (model->Skeleton != null) {
                                            var bone = model->Skeleton->GetBone(i, i2);
                                            if (bone.HkaBone.Name.String != null) {
                                                if (!_lastBonePositions[character.Name.TextValue].ContainsKey(bone.HkaBone.Name.String)) {
                                                    _lastBonePositions[character.Name.TextValue][bone.HkaBone.Name.String] = new MovingObject(new Vector3(), new Vector3(), false);
                                                }
                                                var movingObject = _lastBonePositions[character.Name.TextValue][bone.HkaBone.Name.String];

                                                var worldPos = bone.GetWorldPos(characterActor, model);
                                                var rotation = MediaBoneObject.Q2E(bone.Transform.Rotation);
                                                float distance = Vector3.Distance(movingObject.LastPosition, worldPos);
                                                float rotationDistance = Vector3.Distance(movingObject.LastRotation, rotation);
                                                if (distance > 2f || rotationDistance > 2f) {
                                                    if (!movingObject.IsMoving) {
                                                        string value = characterVoicePack.GetMisc(bone.HkaBone.Name.String, false, true);
                                                        if (!string.IsNullOrEmpty(value)) {
                                                            var boneObject = new MediaBoneObject(bone, characterActor, model);
                                                            string boneName = bone.HkaBone.Name.String;
                                                            Plugin.PluginLog.Verbose(boneName + " playing sound.");
                                                            mediaManager.PlayMedia(boneObject, value, SoundType.LoopWhileMoving, false, 0, default, (object o, string args) => {
                                                                movingObject.IsMoving = false;
                                                                Plugin.PluginLog.Verbose(boneName + " stopping sound.");
                                                            });
                                                            movingObject.IsMoving = true;
                                                        }
                                                    }
                                                }
                                                movingObject.LastPosition = worldPos;
                                                movingObject.LastRotation = rotation;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                } catch {

                }
            }
        }
    }
}
