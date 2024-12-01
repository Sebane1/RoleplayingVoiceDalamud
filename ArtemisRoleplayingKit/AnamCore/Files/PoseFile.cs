// © Anamnesis.
// Licensed under the MIT license.

namespace Anamnesis.Files;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Anamnesis.Memory;
using Anamnesis.Actor;
using Anamnesis.Posing;
public class PoseFile : JsonFileBase
{
	[Flags]
	public enum Mode
	{
		Rotation = 1,
		Scale = 2,
		Position = 4,
		WorldRotation = 8,
		WorldScale = 16,

		All = Rotation | Scale | Position | WorldRotation | WorldScale,
	}

	public enum BoneProcessingModes
	{
		Ignore,
		KeepRelative,
		FullLoad,
	}

	public override string FileExtension => ".pose";
	public override string TypeName => "Anamnesis Pose";

	public Vector? Position { get; set; }
	public Quaternion? Rotation { get; set; }
	public Vector? Scale { get; set; }

	public Dictionary<string, Bone?>? Bones { get; set; }

	[Serializable]
	public class Bone
	{
		public Bone()
		{
		}

		public Vector? Position { get; set; }
		public Quaternion? Rotation { get; set; }
		public Vector? Scale { get; set; }
	}
}
