using RimWorld;
using Verse;

namespace LCAnomalyOrdeals.DefOfs
{
    [DefOf]
    public static class OrdealDefOf
    {
        public static PawnKindDef LCOrdeal_AmberDawn;
        public static PawnKindDef LCOrdeal_CrimsonDawn;
        public static PawnKindDef LCOrdeal_GreenDawn;
        public static PawnKindDef LCOrdeal_VioletDawn;
        public static PawnKindDef LCOrdeal_CrimsonNoon;
        public static PawnKindDef LCOrdeal_GreenNoon;
        public static PawnKindDef LCOrdeal_VioletNoon;
        public static PawnKindDef LCOrdeal_IndigoNoon;
        public static PawnKindDef LCOrdeal_AmberDusk;
        public static PawnKindDef LCOrdeal_CrimsonDusk;
        public static PawnKindDef LCOrdeal_GreenDusk;
        public static PawnKindDef LCOrdeal_AmberMidnight;
        public static PawnKindDef LCOrdeal_GreenMidnight;
        public static PawnKindDef LCOrdeal_VioletMidnightRed;
        public static PawnKindDef LCOrdeal_VioletMidnightWhite;
        public static PawnKindDef LCOrdeal_VioletMidnightBlack;
        public static PawnKindDef LCOrdeal_VioletMidnightPale;
        public static HediffDef LCOrdeal_VioletSlowness;
        public static HediffDef LCOrdeal_VioletNoonImmobile;
        public static HediffDef LCOrdeal_AmberDuskSlowness;
        public static HediffDef LCOrdeal_GreenMidnightBeamSlowness;

        static OrdealDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(OrdealDefOf));
        }
    }
}
