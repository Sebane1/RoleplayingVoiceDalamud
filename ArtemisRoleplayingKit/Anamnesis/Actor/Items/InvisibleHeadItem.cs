﻿// © Anamnesis.
// Licensed under the MIT license.

namespace Anamnesis.Actor.Items;
using Anamnesis.GameData;
using Anamnesis.GameData.Sheets;
using Anamnesis.Services;

public class InvisibleHeadItem : IItem
{
	public string Name => LocalizationService.GetString("Item_InvisibleHead");
	public string Description => LocalizationService.GetString("Item_InvisibleHeadDesc");
	public ImageReference? Icon => GameDataService.Items.Get(10032)?.Icon;
	public ushort ModelSet => 0;
	public ushort ModelBase => 6121;
	public ushort ModelVariant => 254;
	public bool HasSubModel => false;
	public ushort SubModelSet => 0;
	public ushort SubModelBase => 0;
	public ushort SubModelVariant => 0;
	public Classes EquipableClasses => Classes.All;
	public bool IsWeapon => false;
	public uint RowId => 0;
	public byte EquipLevel => 0;

	public bool IsFavorite
	{
		get => FavoritesService.IsFavorite(this);
		set => FavoritesService.SetFavorite(this, value);
	}

	public bool CanOwn => false;
	public bool IsOwned { get; set; }

	public ItemCategories Category => ItemCategories.Standard;

	public bool FitsInSlot(ItemSlots slot)
	{
		return slot == ItemSlots.Head;
	}
}