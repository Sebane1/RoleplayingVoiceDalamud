﻿// © Anamnesis.
// Licensed under the MIT license.

namespace Anamnesis.Utils;

using System;
using System.Threading.Tasks;
public static class ClipboardUtility
{


	public static async Task CopyToClipboardAsync(string text)
	{
		int attempt = 3;
		bool success = false;
		do
		{
			try
			{
				success = true;
			}
			catch (Exception)
			{
				if (attempt <= 0)
					throw;

				success = false;
				await Task.Delay(1);
			}
		}
		while (attempt > 0 && !success);
	}
}
