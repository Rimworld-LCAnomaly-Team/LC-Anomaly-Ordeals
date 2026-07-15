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
        public static HediffDef LCOrdeal_VioletSlowness;

        static OrdealDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(OrdealDefOf));
        }
    }
}
