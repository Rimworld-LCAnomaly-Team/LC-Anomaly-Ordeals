using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace LCAnomalyOrdeals.Utilities
{
    internal static class OrdealTargetUtility
    {
        internal static bool IsOrdealPawn(Pawn pawn)
        {
            return pawn?.kindDef?.defName?.StartsWith("LCOrdeal_", StringComparison.Ordinal) == true;
        }

        internal static bool IsValidTarget(Pawn pawn)
        {
            return pawn != null
                && pawn.Spawned
                && !pawn.Dead
                && !pawn.Destroyed
                && !IsOrdealPawn(pawn);
        }

        internal static List<Pawn> AllTargets(Map map)
        {
            return map == null
                ? new List<Pawn>()
                : map.mapPawns.AllPawnsSpawned.Where(IsValidTarget).ToList();
        }
    }
}
