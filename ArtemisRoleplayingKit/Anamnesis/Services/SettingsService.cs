// © Anamnesis.
// Licensed under the MIT license.

namespace Anamnesis.Services;

using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Anamnesis;
using Anamnesis.Files;
using Anamnesis.Serialization;

public class SettingsService : ServiceBase<SettingsService>
{
	private static readonly string SettingsPath = FileService.ParseToFilePath(FileService.StoreDirectory + "/Settings.json");

	public static event PropertyChangedEventHandler? SettingsChanged;

	public static Settings Current => Instance.Settings!;

	public Settings? Settings { get; private set; }
	public bool FirstTimeUser { get; private set; }

	public static void ShowDirectory()
	{
		FileService.OpenDirectory(FileService.StoreDirectory);
	}

	public static void Save()
	{
		string json = SerializerService.Serialize(Instance.Settings!);
		File.WriteAllText(SettingsPath, json);
	}

	public static void ApplyTheme()
	{

	}

	public override async Task Initialize()
	{
		await base.Initialize();

		if (!File.Exists(SettingsPath))
		{
			this.FirstTimeUser = true;
			this.Settings = new Settings();
			Save();
		}
		else
		{
			this.FirstTimeUser = false;
		}

		//this.Settings.PropertyChanged += this.OnSettingsChanged;
		//this.OnSettingsChanged(null, new PropertyChangedEventArgs(null));
	}

	private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (this.Settings == null)
			return;

		if (sender is Settings settings)
		{
			Save();
		}

		ApplyTheme();
		SettingsChanged?.Invoke(sender, e);
	}
}
