using Penumbra.GameData.Enums;

public class Identifier {
    public int Id { get; set; }
    public bool IsItem { get; set; }
    public IdObject Item { get; set; }
    public Split Split { get; set; }
}

public class IdObject {
    public int Id { get; set; }
}
public class Level {
    public int Value { get; set; }
}

public class EquipObject {
    public string Name { get; set; }
    public Identifier Id { get; set; }
    public IdObject IconId { get; set; }
    public IdObject ModelId { get; set; }
    public WeaponType WeaponType { get; set; }
    public Variant Variant { get; set; }
    public FullEquipType Type { get; set; }
    public int Flags { get; set; }
    public Level Level { get; set; }
    public IdObject JobRestrictions { get; set; }
    public IdObject ItemId { get; set; }
    public bool Valid { get; set; }
    public string ModelString { get; set; }
}

public class Split {
    public IdObject Item1 { get; set; }
    public IdObject Item2 { get; set; }
    public IdObject Item3 { get; set; }
    public int Item4 { get; set; }
}

public class Variant {
    public int Id { get; set; }
}

public class WeaponType {
    public int Id { get; set; }
}