// © Anamnesis.
// Licensed under the MIT license.

namespace Anamnesis.Services;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;


[Serializable]
public class Settings : INotifyPropertyChanged
{
	public event PropertyChangedEventHandler? PropertyChanged;

	public enum Fonts
	{
		Default,
		Hyperlegible,
	}

	public string Language { get; set; } = "EN";
	public bool AlwaysOnTop { get; set; } = true;
	public bool OverlayWindow { get; set; } = false;
	public double Opacity { get; set; } = 1.0;
	public double Scale { get; set; } = 1.0;
	public bool ShowFileExtensions { get; set; } = false;
	public bool UseWindowsExplorer { get; set; } = false;
	public string DefaultPoseDirectory { get; set; } = "%MyDocuments%/Anamnesis/Poses/";
	public string DefaultCharacterDirectory { get; set; } = "%MyDocuments%/Anamnesis/Characters/";
	public string DefaultCameraShotDirectory { get; set; } = "%MyDocuments%/Anamnesis/CameraShots/";
	public string DefaultSceneDirectory { get; set; } = "%MyDocuments%/Anamnesis/Scenes/";
	public bool ShowAdvancedOptions { get; set; } = true;
	public bool FlipPoseGuiSides { get; set; } = false;
	public Fonts Font { get; set; } = Fonts.Default;
	public bool ShowGallery { get; set; } = true;
	public string? GalleryDirectory { get; set; }
	public bool EnableTranslucency { get; set; } = true;
	public bool ExtendIntoWindowChrome { get; set; } = true;
	public bool UseExternalRefresh { get; set; } = false;
	public bool UseExternalRefreshBrio { get; set; } = false;
	public bool EnableNpcHack { get; set; } = false;
	public bool EnableGameHotkeyHooks { get; set; } = false;
	public bool EnableHotkeys { get; set; } = true;
	public bool ForwardKeys { get; set; } = true;
	public bool EnableDeveloperTab { get; set; } = false;
	public bool ReapplyAppearance { get; set; } = false;
	public bool OverrideSystemTheme { get; set; } = false;
	public bool ThemeLight { get; set; } = false;
	public bool WrapRotationSliders { get; set; } = true;
	public string? DefaultAuthor { get; set; }
	public DateTimeOffset LastUpdateCheck { get; set; } = DateTimeOffset.MinValue;
	public string? GamePath { get; set; }
	public Binds KeyboardBindings { get; set; } = new();
	public Dictionary<string, int> ActorTabOrder { get; set; } = new();
	public Dictionary<string, bool> PosingBoneLinks { get; set; } = new();

	public double WindowOpcaticy
	{
		get
		{
			if (!this.EnableTranslucency)
				return 1.0;

			return this.Opacity;
		}
	}

	[Serializable]
	public class Binds
	{

	}
}
