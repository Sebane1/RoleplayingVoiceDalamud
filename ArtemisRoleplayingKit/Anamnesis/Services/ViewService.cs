// © Anamnesis.
// Licensed under the MIT license.

namespace Anamnesis.Services;

using System;
using System.Reflection;
using System.Threading.Tasks;
using Anamnesis;

public delegate void DrawerEvent();
public delegate void DialogEvent();

public enum DrawerDirection
{
	Left,
	Top,
	Right,
	Bottom,
}

public interface IDrawer
{
	event DrawerEvent OnClosing;
	void Close();
	void OnClosed();
}

public interface IDialog<TResult> : IDialog
{
	TResult Result { get; set; }
}

public interface IDialog
{
	event DialogEvent Close;

	void Cancel();
}