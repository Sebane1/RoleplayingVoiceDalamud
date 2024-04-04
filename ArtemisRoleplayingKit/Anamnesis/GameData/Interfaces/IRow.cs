// © Anamnesis.
// Licensed under the MIT license.

namespace Anamnesis.GameData;

using Anamnesis.GameData.Excel;

public interface IRow : ISelectable
{
	uint RowId { get; }
}
