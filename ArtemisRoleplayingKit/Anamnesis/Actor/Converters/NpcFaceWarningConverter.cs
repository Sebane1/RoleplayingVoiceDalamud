// © Anamnesis.
// Licensed under the MIT license.

namespace Anamnesis.Converters;

using System;
using System.Globalization;

public class NpcFaceWarningConverter
{
	public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
	{
		throw new NotSupportedException("NpcFaceWarningConverter is a OneWay converter.");
	}
}
