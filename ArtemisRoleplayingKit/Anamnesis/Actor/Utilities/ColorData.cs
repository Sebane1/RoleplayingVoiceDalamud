// © Anamnesis.
// Licensed under the MIT license.

namespace Anamnesis.Actor.Utilities;

using System;
using System.Collections.Generic;
using Anamnesis.Memory;
using Anamnesis.Services;

public static class ColorData
{
	private static readonly Entry[] Colors;
	public static Entry[] GetSkin(ActorCustomizeMemory.Tribes tribe, ActorCustomizeMemory.Genders gender)
	{
		int from = GetTribeSkinStartIndex(tribe, gender);
		return Span(from, 192);
	}

	public static Entry[] GetHair(ActorCustomizeMemory.Tribes tribe, ActorCustomizeMemory.Genders gender)
	{
		int from = GetTribeHairStartIndex(tribe, gender);
		return Span(from, 192);
	}

	public static Entry[] GetHairHighlights()
	{
		return Span(256, 192);
	}

	public static Entry[] GetEyeColors()
	{
		return Span(0, 192);
	}

	public static Entry[] GetLimbalColors()
	{
		return Span(0, 192);
	}

	public static Entry[] GetFacePaintColor()
	{
		return Span(512, 224);
	}

	public static Entry[] GetLipColors()
	{
		List<Entry> entries = new List<Entry>();
		entries.AddRange(Span(512, 96));

		for (int i = 0; i < 32; i++)
		{
			Entry entry = default;
			entry.Skip = true;
			entries.Add(entry);
		}

		entries.AddRange(Span(1792, 96));

		return entries.ToArray();
	}

	private static Entry[] Span(int from, int count)
	{
		Entry[] entries = new Entry[count];

		if (Colors.Length <= 0)
			return entries;

		Array.Copy(Colors, from, entries, 0, count);
		return entries;
	}

	private static int GetTribeSkinStartIndex(ActorCustomizeMemory.Tribes tribe, ActorCustomizeMemory.Genders gender)
	{
		bool isMasculine = gender == ActorCustomizeMemory.Genders.Masculine;

		int genderValue = isMasculine ? 0 : 1;
		int listIndex = ((((int)tribe * 2) + genderValue) * 5) + 3;
		return listIndex * 256;
	}

	private static int GetTribeHairStartIndex(ActorCustomizeMemory.Tribes tribe, ActorCustomizeMemory.Genders gender)
	{
		bool isMasculine = gender == ActorCustomizeMemory.Genders.Masculine;

		int genderValue = isMasculine ? 0 : 1;
		int listIndex = ((((int)tribe * 2) + genderValue) * 5) + 4;
		return listIndex * 256;
	}

	public struct Entry
	{
		public bool Skip { get; set; }
	}
}
