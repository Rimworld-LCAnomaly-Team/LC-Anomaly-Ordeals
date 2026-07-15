using System.Collections.Generic;
using HarmonyLib;
using LCAnomalyOrdeals.DefOfs;
using LCAnomalyOrdeals.Defs;
using LCAnomalyOrdeals.Examinations;
using LCAnomalyOrdeals.Presentation;
using LCAnomalyOrdeals.Utilities;
using RimWorld;
using UnityEngine;
using Verse;

namespace LCAnomalyOrdeals.Patches
{
    [HarmonyPatch(typeof(GenHostility), nameof(GenHostility.HostileTo), new[] { typeof(Thing), typeof(Thing) })]
    internal static class OrdealHostilityPatch
    {
        private static bool Prefix(Thing a, Thing b, ref bool __result)
        {
            Pawn first = a as Pawn;
            Pawn second = b as Pawn;
            if (first == null || second == null)
            {
                return true;
            }

            bool firstIsOrdeal = OrdealTargetUtility.IsOrdealPawn(first);
            bool secondIsOrdeal = OrdealTargetUtility.IsOrdealPawn(second);
            if (!firstIsOrdeal && !secondIsOrdeal)
            {
                return true;
            }

            __result = firstIsOrdeal != secondIsOrdeal;
            return false;
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PreApplyDamage))]
    internal static class VioletMidnightAbsorptionPatch
    {
        private static bool Prefix(Pawn __instance, ref DamageInfo dinfo, out bool absorbed)
        {
            absorbed = MidnightOrdealWorker.TryAbsorbVioletDamage(__instance, dinfo) || WhiteOrdealWorker.TryAbsorbDamage(__instance, dinfo);
            return !absorbed;
        }
    }

    [HarmonyPatch(typeof(UIRoot), nameof(UIRoot.UIRootOnGUI))]
    internal static class DawnPresentationInputPatch
    {
        private static void Prefix()
        {
            GameComponent_DawnPresentation.FilterCurrentInput();
            GameComponent_NoonPresentation.FilterCurrentInput();
            GameComponent_DuskPresentation.FilterCurrentInput();
            GameComponent_MidnightPresentation.FilterCurrentInput();
            GameComponent_WhitePresentation.FilterCurrentInput();
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PostApplyDamage))]
    internal static class VioletPawnDamagePatch
    {
        private static void Postfix(Pawn __instance, DamageInfo dinfo, float totalDamageDealt)
        {
            if (totalDamageDealt > 0f
                && __instance.kindDef == OrdealDefOf.LCOrdeal_VioletDawn
                && dinfo.Instigator is Pawn attacker)
            {
                DawnOrdealWorker.NotifyVioletDamaged(__instance, attacker);
            }

            if (totalDamageDealt > 0f && dinfo.Instigator is Pawn instigator)
            {
                NoonOrdealWorker.NotifyIndigoDamage(instigator, totalDamageDealt);
            }

            if (totalDamageDealt > 0f && __instance.kindDef == OrdealDefOf.LCOrdeal_AmberDusk)
            {
                DuskOrdealWorker.NotifyAmberDamaged(__instance, totalDamageDealt);
            }

            if (totalDamageDealt > 0f && __instance.kindDef == OrdealDefOf.LCOrdeal_GreenMidnight)
            {
                MidnightOrdealWorker.NotifyGreenTowerDamaged(__instance, totalDamageDealt);
            }

            if (totalDamageDealt > 0f)
            {
                MidnightOrdealWorker.NotifyVioletShrineDamaged(__instance, totalDamageDealt);
                WhiteOrdealWorker.NotifyDamaged(__instance, totalDamageDealt);
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    internal static class OrdealPawnKillPatch
    {
        internal sealed class KillState
        {
            public Map map;
            public IntVec3 position;
            public Pawn greenKiller;
            public Rot4 rotation;
        }

        private static void Prefix(Pawn __instance, DamageInfo? dinfo, out KillState __state)
        {
            __state = new KillState
            {
                map = __instance.MapHeld,
                position = __instance.PositionHeld,
                greenKiller = dinfo.HasValue ? dinfo.Value.Instigator as Pawn : null
                ,rotation = __instance.Rotation
            };

            if (__instance.kindDef == OrdealDefOf.LCOrdeal_CrimsonNoon && __instance.Spawned)
            {
                NoonOrdealWorker.NotifyCrimsonNoonKilled(__instance);
                DuskOrdealWorker.NotifyCrimsonKilled(__instance);
            }

            if (__instance.kindDef == OrdealDefOf.LCOrdeal_CrimsonDusk && __instance.Spawned)
            {
                DuskOrdealWorker.NotifyCrimsonKilled(__instance);
            }

            if (__instance.kindDef == OrdealDefOf.LCOrdeal_CrimsonDawn && __instance.Spawned)
            {
                DawnOrdealSettings settings = DawnOrdealWorker.ActiveSettings();
                GenExplosion.DoExplosion(
                    __instance.Position,
                    __instance.Map,
                    settings.crimsonExplosionRadius,
                    DamageDefOf.Bomb,
                    __instance,
                    settings.crimsonExplosionDamage,
                    settings.crimsonExplosionArmorPenetration,
                    ignoredThings: new List<Thing> { __instance },
                    damageFalloff: true);
            }
        }

        private static void Postfix(Pawn __instance, KillState __state)
        {
            if (__state != null) WhiteOrdealWorker.NotifyKilled(__instance, __state.map, __state.position, __state.rotation);
            if (__state == null
                || __state.map == null
                || __state.greenKiller == null
                || __state.greenKiller.kindDef != OrdealDefOf.LCOrdeal_GreenDawn
                || OrdealTargetUtility.IsOrdealPawn(__instance))
            {
                return;
            }

            DawnOrdealSettings settings = DawnOrdealWorker.ActiveSettings();
            foreach (Pawn pawn in OrdealTargetUtility.AllTargets(__state.map))
            {
                if (!pawn.Dead && pawn.Position.InHorDistOf(__state.position, settings.greenExecutionPsychicRadius))
                {
                    pawn.TakeDamage(new DamageInfo(
                        DamageDefOf.Psychic,
                        settings.greenExecutionPsychicDamage,
                        0f,
                        instigator: __state.greenKiller));
                }
            }
        }
    }
}
