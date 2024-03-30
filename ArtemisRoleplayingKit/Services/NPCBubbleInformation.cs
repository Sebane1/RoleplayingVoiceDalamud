using Dalamud.Game.Text.SeStringHandling;
using System.Diagnostics;

namespace RoleplayingVoiceDalamud;

internal class NPCBubbleInformation
{
	Stopwatch stopwatch;
	public NPCBubbleInformation( SeString messageText, long timeLastSeen_mSec, SeString speakerName )
	{
		TimeLastSeen_mSec = timeLastSeen_mSec;
		HasBeenPrinted = false;
		MessageText = messageText;
		SpeakerName = speakerName;
		stopwatch = Stopwatch.StartNew();
	}

	protected NPCBubbleInformation(){}

	public bool IsSameMessageAs( NPCBubbleInformation rhs )
	{
		//***** TODO: Is there a better comparison that we can easily do on the whole thing, and not just the text value?  Can we encode and compare and get what we want?
		return stopwatch.ElapsedMilliseconds < 5000 && SpeakerName.TextValue.Equals( rhs.SpeakerName.TextValue ) && MessageText.TextValue.Equals( rhs.MessageText.TextValue );
	}

	public long TimeLastSeen_mSec { get; set; }
	public bool HasBeenPrinted { get; set; }
	public SeString SpeakerName { get; set; }
	public SeString MessageText { get; set; }
}
