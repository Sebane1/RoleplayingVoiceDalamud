// © Anamnesis.
// Licensed under the MIT license.

namespace Anamnesis.Actor;

using System;
using System.Numerics;

public static class VectorExtensions
{
	public static Vector3 ToMedia3DVector(this Vector3 self)
	{
		return new Vector3(self.X, self.Y, self.Z);
	}

	public static Vector3 ToVector3(this Vector3 self)
	{
		return new Vector3((float)self.X, (float)self.Y, (float)self.Z);
	}

	public static void FromMedia3DQuaternion(this Vector3 self, Vector3 other)
	{
		self.X = (float)other.X;
		self.Y = (float)other.Y;
		self.Z = (float)other.Z;
	}

	public static void FromCmQuaternion(this Vector3 self, Vector3 other)
	{
		self.X = other.X;
		self.Y = other.Y;
		self.Z = other.Z;
	}

	public static float Length(this Vector3 self)
	{
		return MathF.Sqrt((self.X * self.X) + (self.Y * self.Y) + (self.Z * self.Z));
	}
}
