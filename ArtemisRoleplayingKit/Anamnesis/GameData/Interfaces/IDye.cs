// © Anamnesis.
// Licensed under the MIT license.

namespace Anamnesis.GameData;


using Anamnesis.GameData.Sheets;

public interface IDye : IRow
{
	byte Id { get; }
	ImageReference? Icon { get; }

	bool IsFavorite { get; set; }
}
