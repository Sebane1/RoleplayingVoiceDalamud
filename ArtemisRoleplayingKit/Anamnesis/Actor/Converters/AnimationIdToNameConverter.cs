// © Anamnesis.
// Licensed under the MIT license.

namespace Anamnesis.Actor.Converters;

using System;
using System.Globalization;
using Anamnesis.GameData.Excel;
using Anamnesis.Services;

public class AnimationIdToNameConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		string animationName;

		try
		{
			ushort animId = (ushort)value;
			ActionTimeline timeline = GameDataService.ActionTimelines.Get(animId);
			animationName = timeline.Key ?? LocalizationService.GetString("Character_Action_NoAnimation");
		}
		catch
		{
			animationName = LocalizationService.GetString("Character_Action_UnknownAnimation");
		}

		return animationName;
	}
}
