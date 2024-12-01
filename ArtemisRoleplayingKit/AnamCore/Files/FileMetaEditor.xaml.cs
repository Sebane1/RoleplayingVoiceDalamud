// © Anamnesis.
// Licensed under the MIT license.

namespace Anamnesis.Files;

using System;
using System.IO;

using Anamnesis.Services;

/// <summary>
/// Interaction logic for FileMetaEditor.xaml.
/// </summary>
public partial class FileMetaEditor
{

	public FileMetaEditor( FileSystemInfo info, FileBase file)
	{
		this.Info = info;
		this.File = file;

		if (file.Author == null)
		{
			file.Author = SettingsService.Current.DefaultAuthor;
		}
	}

	public FileSystemInfo Info { get; private set; }
	public FileBase File { get; private set; }

	public static void Show(FileSystemInfo info, FileBase file)
	{
	}
}
