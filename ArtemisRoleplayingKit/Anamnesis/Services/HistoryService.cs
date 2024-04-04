// © Anamnesis.
// Licensed under the MIT license.

namespace Anamnesis.Services;

using System.Threading.Tasks;
using Anamnesis.Memory;

public class HistoryService : ServiceBase<HistoryService>
{
	public delegate void HistoryAppliedEvent();

	public static event HistoryAppliedEvent? OnHistoryApplied;

	public static bool IsRestoring { get; private set; } = false;

	public override Task Initialize()
	{
		return base.Initialize();
	}

	private void StepBack()
	{
		this.Step(false);
	}

	private void StepForward()
	{
		this.Step(true);
	}

	private void Step(bool forward)
	{
		ActorMemory? actor = TargetService.Instance.SelectedActor;

		if (actor == null)
			return;

		IsRestoring = true;

		if (forward)
		{
			actor.History.StepForward();
		}
		else
		{
			actor.History.StepBack();
		}

		OnHistoryApplied?.Invoke();
		IsRestoring = false;
	}
}
