﻿using Dalamud.Game.ClientState.Objects.Types;
using Ktisis.Structs.Actor;
using Ktisis.Structs;
using RoleplayingMediaCore;
using System.Collections.Generic;
using System.Numerics;
using System;
using FFXIVClientStructs.FFXIV.Common.Lua;

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
                    Actor* characterActor = (Actor*)character.Address;
                    var model = characterActor->Model;
                    for (int i = 0; i < model->Skeleton->PartialSkeletonCount; i++) {
                        var partialSkeleton = model->Skeleton->PartialSkeletons[i];
                        var pos = partialSkeleton.GetHavokPose(0);
                        if (pos != null) {
                            var skeleton = pos->Skeleton;
                            for (var i2 = 1; i2 < skeleton->Bones.Length; i2++) {
                                var bone = model->Skeleton->GetBone(i, i2);
                                if (!_lastBonePositions[character.Name.TextValue].ContainsKey(bone.HkaBone.Name.String)) {
                                    _lastBonePositions[character.Name.TextValue][bone.HkaBone.Name.String] = new MovingObject(new Vector3(), new Vector3(), false);
                                }
                                var movingObject = _lastBonePositions[character.Name.TextValue][bone.HkaBone.Name.String];

                                var worldPos = bone.GetWorldPos(characterActor, model);
                                var rotation = MediaBoneObject.Q2E(bone.Transform.Rotation);
                                float distance = Vector3.Distance(movingObject.LastPosition, worldPos);
                                float rotationDistance = Vector3.Distance(new Vector3(0, movingObject.LastRotation.Y, 0), new Vector3(0, rotation.Y, 0));
                                if (distance > 0.1f || rotationDistance > 30f) {
                                    if (!movingObject.IsMoving) {
                                        string value = characterVoicePack.GetMisc(bone.HkaBone.Name.String, false, true);
                                        if (!string.IsNullOrEmpty(value)) {
                                            var boneObject = new MediaBoneObject(bone, characterActor, model);
                                            mediaManager.PlayAudio(boneObject, value, SoundType.LoopWhileMoving, false, 0);
                                        }
                                        movingObject.IsMoving = true;
                                    }
                                } else {
                                    if (movingObject.IsMoving) {
                                        movingObject.IsMoving = false;
                                        //var boneObject = new MediaBoneObject(bone, characterActor, model);
                                        //mediaManager.StopAudio(boneObject);
                                    }
                                }
                                movingObject.LastPosition = worldPos;
                                movingObject.LastRotation = rotation;
                            }
                        }
                    }
                } catch {

                }
            }
        }
    }
}
