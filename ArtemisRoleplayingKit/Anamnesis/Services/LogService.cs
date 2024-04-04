// © Anamnesis.
// Licensed under the MIT license.

namespace Anamnesis.Services;

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Anamnesis.Files;

public class LogService : IService
{
	private const string LogfilePath = "/Logs/";

	private static LogService? instance;
	private static string? currentLogPath;

	public static LogService Instance
	{
		get
		{
			if (instance == null)
				throw new Exception("No logging service found");

			return instance;
		}
	}

	public static void ShowLogs()
	{
		string? dir = Path.GetDirectoryName(FileService.StoreDirectory + LogfilePath);

		if (dir == null)
			throw new Exception("Failed to get directory name for path");

		dir = FileService.ParseToFilePath(dir);
		Process.Start(Environment.GetEnvironmentVariable("WINDIR") + @"\explorer.exe", dir);
	}

	public static void ShowCurrentLog()
	{
		Process.Start(Environment.GetEnvironmentVariable("WINDIR") + @"\explorer.exe", $"/select, \"{currentLogPath}\"");
	}

	public static void CreateLog()
	{
		if (!string.IsNullOrEmpty(currentLogPath))
			return;

		string dir = Path.GetDirectoryName(FileService.StoreDirectory + LogfilePath) + "\\";
		dir = FileService.ParseToFilePath(dir);

		if (!Directory.Exists(dir))
			Directory.CreateDirectory(dir);

		string[] logs = Directory.GetFiles(dir);
		for (int i = logs.Length - 1; i >= 0; i--)
		{
			if (i <= logs.Length - 15)
			{
				File.Delete(logs[i]);
			}
		}

		currentLogPath = dir + DateTime.Now.ToString(@"yyyy-MM-dd-HH-mm-ss") + ".txt";
	}

	public Task Initialize()
	{
		instance = this;
		CreateLog();

		return Task.CompletedTask;
	}

	public Task Shutdown()
	{
		return Task.CompletedTask;
	}

	public Task Start()
	{
		return Task.CompletedTask;
	}

	private class ErrorDialogLogDestination
	{
	}
}
