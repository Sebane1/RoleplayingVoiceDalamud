using System.Numerics;

using Ktisis.Structs.Actor;

namespace Ktisis.Structs.Bones {
	public class CustomOffset {
		public unsafe static Vector3 GetBoneOffset(Bone bone, Actor.Actor* target) {
			if (!Ktisis.Configuration.CustomBoneOffset.TryGetValue(GetRaceGenderFromActor(target), out var bonesOffsets))
				return new();
			if (!bonesOffsets.TryGetValue(bone.HkaBone.Name.String!, out Vector3 offset))
				return new();
			return offset;
		}

		// changed from BodyType to string Race_Gender
		public unsafe static string GetRaceGenderFromActor(Actor.Actor* actor) =>
			$"{actor->DrawData.Customize.Race}_{actor->DrawData.Customize.Gender}";
		public unsafe static BodyType GetBodyTypeFromActor(Actor.Actor* actor) {
			var gender = actor->DrawData.Customize.Gender;
			var race = actor->DrawData.Customize.Race;

			if (gender == Gender.Masculine)
				return race switch {
					Race.Lalafell => BodyType.LalafellMale,
					Race.Roegadyn or Race.Hrothgar => BodyType.HrothgarRoegadyn,
					_ => BodyType.TallMale,
				};
			else
				return race switch {
					Race.Lalafell => BodyType.LalafellFemale,
					_ => BodyType.TallFemale,
				};
		}

		// This does the math to be inserted in Bone.GetWorldPos() equation
		public static unsafe Vector3 CalculateWorldOffset(Actor.Actor* actor, ActorModel* model, Bone bone) =>
			Vector3.Transform(Vector3.Transform(GetBoneOffset(bone, actor), bone.Transform.Rotation.ToQuat()), model->Rotation);


		public enum BodyType {
			TallMale,
			TallFemale,
			HrothgarRoegadyn,
			LalafellMale,
			LalafellFemale,
		}
	}
}
