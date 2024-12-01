// © Anamnesis.
// Licensed under the MIT license.

namespace Anamnesis.GUI.Views;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Anamnesis.Files;
using Anamnesis.Services;

public abstract class FileBrowserDrawer
{
}

/// <summary>
/// Interaction logic for FileBrowserView.xaml.
/// </summary>
public partial class FileBrowserView : FileBrowserDrawer
{
	private static bool isFlattened;
	private static Sort sortMode;

	private readonly Modes mode;
	private readonly IEnumerable<FileFilter> filters;
	private bool updatingEntries = false;

	public FileBrowserView(Shortcut[] shortcuts, IEnumerable<FileFilter> filters, DirectoryInfo? defaultDir, string? defaultName, Modes mode)
	{
		if (shortcuts.Length == 0)
			throw new Exception("At least one shortcut must be provided to the file browser constructor");

		//this.InitializeComponent();

		if (defaultDir != null && !defaultDir.Exists)
			defaultDir = null;

		List<Shortcut> finalShortcuts = new List<Shortcut>();
		finalShortcuts.AddRange(shortcuts);
		finalShortcuts.Add(FileService.Desktop);

		Shortcut? defaultShortcut = null;
		foreach (Shortcut shortcut in finalShortcuts)
		{
			if (defaultDir == null && mode == Modes.Load && shortcut.Directory.Exists && defaultShortcut == null)
				defaultShortcut = shortcut;

			if (defaultDir != null)
			{
				string defaultDirName = defaultDir.FullName;

				if (!defaultDirName.EndsWith("\\"))
					defaultDirName += "\\";

				if (defaultDirName.Contains(shortcut.Directory.FullName))
				{
					defaultShortcut = shortcut;
				}
			}

			this.Shortcuts.Add(shortcut);
		}

		if (defaultShortcut == null)
		{
			defaultShortcut = this.Shortcuts[0];
			defaultDir = null;
		}

		if (defaultDir == null)
			defaultDir = defaultShortcut.Directory;

		this.BaseDir = defaultShortcut;
		this.CurrentDir = defaultDir;

		this.mode = mode;
		this.filters = filters;



		this.IsOpen = true;

		if (this.mode == Modes.Save)
		{
			this.FileName = "New " + defaultName;

			Task.Run(async () =>
			{
				await Task.Delay(100);
			});
		}

		////this.PropertyChanged?.Invoke(this, new(nameof(FileBrowserView.SortMode)));
	}

	public enum Modes
	{
		Load,
		Save,
	}

	public enum Sort
	{
		None = -1,

		AlphaNumeric,
		Date,
	}

	public bool IsOpen
	{
		get;
		private set;
	}

	public int SortModeInt
	{
		get => (int)this.SortMode;
		set => this.SortMode = (Sort)value;
	}

	public Sort SortMode
	{
		get => sortMode;
		set
		{
			sortMode = value;
			this.UpdateEntriesThreaded();
		}
	}

	public SettingsService SettingsService => SettingsService.Instance;
	public ObservableCollection<Shortcut> Shortcuts { get; } = new ObservableCollection<Shortcut>();

	public bool UseFileBrowser { get; set; }

	public FileSystemInfo? FinalSelection { get; private set; }

	public bool IsFlattened
	{
		get
		{
			return isFlattened;
		}

		set
		{
			isFlattened = value;
			this.UpdateEntriesThreaded();
		}
	}

	public bool ShowExtensions { get; set; } = false;
	public string? FileName { get; set; }

	public bool ShowFileName => this.mode == Modes.Save;
	public bool CanGoUp => this.CurrentDir.FullName.TrimEnd('\\') != this.BaseDir.Directory.FullName.TrimEnd('\\');
	public string? CurrentPath => this.CurrentDir?.FullName.Replace(this.BaseDir.Directory.FullName.Trim('\\'), string.Empty);
	public bool IsModeOpen => this.mode == Modes.Load;

	public Shortcut BaseDir { get; set; }

	public DirectoryInfo CurrentDir { get; private set; }

	public bool CanSelect
	{
		get
		{
			if (this.mode == Modes.Load)
			{
				return  false;
			}
			else
			{
				return !string.IsNullOrWhiteSpace(this.FileName);
			}
		}
	}

	private void OnBaseDirChanged()
	{
		this.CurrentDir = this.BaseDir.Directory;
	}

	private void OnCurrentDirChanged()
	{
		this.UpdateEntriesThreaded();
	}

	private void UpdateEntriesThreaded()
	{
		Task.Run(this.UpdateEntries);
	}

	private async Task UpdateEntries()
	{

	}
}
