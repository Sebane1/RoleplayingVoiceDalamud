using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RoleplayingVoiceDalamud.Glamourer {
    public enum FullEquipType : byte {
        Unknown = 0,

        Head = 1,
        Body = 2,
        Hands = 3,
        Legs = 4,
        Feet = 5,

        Ears = 6,
        Neck = 7,
        Wrists = 8,
        Finger = 9,

        Fists, // PGL, MNK
        FistsOff,
        Sword, // GLA, PLD Main
        Axe,   // MRD, WAR
        Bow,   // ARC, BRD
        BowOff,
        Lance,   // LNC, DRG,
        Staff,   // THM, BLM, CNJ, WHM
        Wand,    // THM, BLM, CNJ, WHM Main
        Book,    // ACN, SMN, SCH
        Daggers, // ROG, NIN
        DaggersOff,
        Broadsword, // DRK,
        Gun,        // MCH,
        GunOff,
        Orrery, // AST,
        OrreryOff,
        Katana, // SAM
        KatanaOff,
        Rapier, // RDM
        RapierOff,
        Cane,     // BLU
        Gunblade, // GNB,
        Glaives,  // DNC,
        GlaivesOff,
        Scythe,   // RPR,
        Nouliths, // SGE
        Shield,   // GLA, PLD, THM, BLM, CNJ, WHM Off

        Saw,             // CRP
        CrossPeinHammer, // BSM
        RaisingHammer,   // ARM
        LapidaryHammer,  // GSM
        Knife,           // LTW
        Needle,          // WVR
        Alembic,         // ALC
        Frypan,          // CUL
        Pickaxe,         // MIN
        Hatchet,         // BTN
        FishingRod,      // FSH

        ClawHammer,    // CRP Off
        File,          // BSM Off
        Pliers,        // ARM Off
        GrindingWheel, // GSM Off
        Awl,           // LTW Off
        SpinningWheel, // WVR Off
        Mortar,        // ALC Off
        CulinaryKnife, // CUL Off
        Sledgehammer,  // MIN Off
        GardenScythe,  // BTN Off
        Gig,           // FSH Off

        Brush,        // PCT
        Palette,      // PCT Off
        Twinfangs,    // VPR
        TwinfangsOff, // VPR Off
        Whip,         // BMR TODO
    }
}
