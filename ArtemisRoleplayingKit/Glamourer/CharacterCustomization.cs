using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RoleplayingVoiceDalamud.Glamourer {
    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
    public class Body {
        public long ItemId { get; set; }
        public int Stain { get; set; }
        public bool Crest { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
        public bool ApplyStain { get; set; }
        public bool ApplyCrest { get; set; }
    }

    public class BodyType {
        public int Value { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
    }

    public class BustSize {
        public int Value { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
    }

    public class Clan {
        public int Value { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
    }

    public class Customize {
        public Customize() {
            Race = new Race();
            Gender = new Gender();
            BodyType = new BodyType();
            Height = new Height();
            Clan = new Clan();
            Face = new FacialValue();
            Hairstyle = new Hairstyle();
            Highlights = new Highlights();
            SkinColor = new SkinColor();
            EyeColorRight = new FacialValue();
            HairColor = new HairColor();
            HighlightsColor = new HighlightsColor();
            FacialFeature1 = new FacialValue();
            FacialFeature2 = new FacialValue();
            FacialFeature3 = new FacialValue();
            FacialFeature4 = new FacialValue();
            FacialFeature5 = new FacialValue();
            FacialFeature6 = new FacialValue();
            FacialFeature7 = new FacialValue();
            LegacyTattoo = new LegacyTattoo();
            TattooColor = new TattooColor();
            Eyebrows = new FacialValue();
            Nose = new Nose();
            Jaw = new Jaw();
            Mouth = new Mouth();
            Lipstick = new Lipstick();
            LipColor = new LipColor();
            MuscleMass = new MuscleMass();
            TailShape = new TailShape();
            BustSize = new BustSize();
            FacePaint = new FacialValue();
            FacePaintReversed = new FacialValue();
            FacePaintColor = new FacialValue();
            Wetness = new Wetness();
        }

        public int ModelId { get; set; }
        public Race Race { get; set; }
        public Gender Gender { get; set; }
        public BodyType BodyType { get; set; }
        public Height Height { get; set; }
        public Clan Clan { get; set; }
        public FacialValue Face { get; set; }
        public Hairstyle Hairstyle { get; set; }
        public Highlights Highlights { get; set; }
        public SkinColor SkinColor { get; set; }
        public FacialValue EyeColorRight { get; set; }
        public HairColor HairColor { get; set; }
        public HighlightsColor HighlightsColor { get; set; }
        public FacialValue FacialFeature1 { get; set; }
        public FacialValue FacialFeature2 { get; set; }
        public FacialValue FacialFeature3 { get; set; }
        public FacialValue FacialFeature4 { get; set; }
        public FacialValue FacialFeature5 { get; set; }
        public FacialValue FacialFeature6 { get; set; }
        public FacialValue FacialFeature7 { get; set; }
        public LegacyTattoo LegacyTattoo { get; set; }
        public TattooColor TattooColor { get; set; }
        public FacialValue Eyebrows { get; set; }
        public FacialValue EyeColorLeft { get; set; }
        public FacialValue EyeShape { get; set; }
        public SmallIris SmallIris { get; set; }
        public Nose Nose { get; set; }
        public Jaw Jaw { get; set; }
        public Mouth Mouth { get; set; }
        public Lipstick Lipstick { get; set; }
        public LipColor LipColor { get; set; }
        public MuscleMass MuscleMass { get; set; }
        public TailShape TailShape { get; set; }
        public BustSize BustSize { get; set; }
        public FacialValue FacePaint { get; set; }
        public FacialValue FacePaintReversed { get; set; }
        public FacialValue FacePaintColor { get; set; }
        public Wetness Wetness { get; set; }
    }

    public class Ears {
        public long ItemId { get; set; }
        public int Stain { get; set; }
        public bool Crest { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
        public bool ApplyStain { get; set; }
        public bool ApplyCrest { get; set; }
    }

    public class Equipment {
        public Equipment() {
            MainHand = new MainHand();
            OffHand = new OffHand();
            Head = new Head();
            Body = new Body();
            Hands = new Hands();
            Legs = new Legs();
            Feet = new Feet();
            Ears = new Ears();
            Neck = new Neck();
            Wrists = new Wrists();
            RFinger = new RFinger();
            LFinger = new LFinger();
            Hat = new Hat();
            Visor = new Visor();
            Weapon = new Weapon();
        }

        public MainHand MainHand { get; set; }
        public OffHand OffHand { get; set; }
        public Head Head { get; set; }
        public Body Body { get; set; }
        public Hands Hands { get; set; }
        public Legs Legs { get; set; }
        public Feet Feet { get; set; }
        public Ears Ears { get; set; }
        public Neck Neck { get; set; }
        public Wrists Wrists { get; set; }
        public RFinger RFinger { get; set; }
        public LFinger LFinger { get; set; }
        public Hat Hat { get; set; }
        public Visor Visor { get; set; }
        public Weapon Weapon { get; set; }
    }

    public class FacialValue {
        public int Value { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
    }
    public class Feet {
        public long ItemId { get; set; }
        public int Stain { get; set; }
        public bool Crest { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
        public bool ApplyStain { get; set; }
        public bool ApplyCrest { get; set; }
    }

    public class Gender {
        public int Value { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
    }

    public class HairColor {
        public int Value { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
    }

    public class Hairstyle {
        public int Value { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
    }

    public class Hands {
        public long ItemId { get; set; }
        public int Stain { get; set; }
        public bool Crest { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
        public bool ApplyStain { get; set; }
        public bool ApplyCrest { get; set; }
    }

    public class Hat {
        public bool Show { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
    }

    public class Head {
        public long ItemId { get; set; }
        public int Stain { get; set; }
        public bool Crest { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
        public bool ApplyStain { get; set; }
        public bool ApplyCrest { get; set; }
    }

    public class Height {
        public int Value { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
    }

    public class Highlights {
        public int Value { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
    }

    public class HighlightsColor {
        public int Value { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
    }

    public class Jaw {
        public int Value { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
    }

    public class LegacyTattoo {
        public int Value { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
    }

    public class Legs {
        public long ItemId { get; set; }
        public int Stain { get; set; }
        public bool Crest { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
        public bool ApplyStain { get; set; }
        public bool ApplyCrest { get; set; }
    }

    public class LFinger {
        public long ItemId { get; set; }
        public int Stain { get; set; }
        public bool Crest { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
        public bool ApplyStain { get; set; }
        public bool ApplyCrest { get; set; }
    }

    public class LipColor {
        public int Value { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
    }

    public class Lipstick {
        public int Value { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
    }

    public class MainHand {
        public ulong ItemId { get; set; }
        public int Stain { get; set; }
        public bool Crest { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
        public bool ApplyStain { get; set; }
        public bool ApplyCrest { get; set; }
    }

    public class Mouth {
        public int Value { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
    }

    public class MuscleMass {
        public int Value { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
    }

    public class Neck {
        public long ItemId { get; set; }
        public int Stain { get; set; }
        public bool Crest { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
        public bool ApplyStain { get; set; }
        public bool ApplyCrest { get; set; }
    }

    public class Nose {
        public int Value { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
    }

    public class OffHand {
        public long ItemId { get; set; }
        public int Stain { get; set; }
        public bool Crest { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
        public bool ApplyStain { get; set; }
        public bool ApplyCrest { get; set; }
    }

    public class Race {
        public int Value { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
    }

    public class RFinger {
        public long ItemId { get; set; }
        public int Stain { get; set; }
        public bool Crest { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
        public bool ApplyStain { get; set; }
        public bool ApplyCrest { get; set; }
    }

    public class CharacterCustomization {
        public CharacterCustomization() {
            Equipment = new Equipment();
            Customize = new Customize();
        }

        public int FileVersion { get; set; }
        public Equipment Equipment { get; set; }
        public Customize Customize { get; set; }
    }

    public class SkinColor {
        public int Value { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
    }

    public class SmallIris {
        public int Value { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
    }

    public class TailShape {
        public int Value { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
    }

    public class TattooColor {
        public int Value { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
    }

    public class Visor {
        public bool IsToggled { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
    }

    public class Weapon {
        public bool Show { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
    }

    public class Wetness {
        public bool Value { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
    }

    public class Wrists {
        public long ItemId { get; set; }
        public int Stain { get; set; }
        public bool Crest { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
        public bool ApplyStain { get; set; }
        public bool ApplyCrest { get; set; }
    }


}
