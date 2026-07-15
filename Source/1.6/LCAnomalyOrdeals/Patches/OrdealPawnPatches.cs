using System.Collections.Generic;
using HarmonyLib;
using LCAnomalyOrdeals.DefOfs;
using LCAnomalyOrdeals.Defs;
using LCAnomalyOrdeals.Examinations;
using LCAnomalyOrdeals.Presentation;
using RimWorld;
using UnityEngine;
using Verse;

namespace LCAnomalyOrdeals.Patches
{
    [HarmonyPatch(typeof(UIRoot), nameof(UIRoot.UIRootOnGUI))]
    internal static class DawnPresentationInputPatch
    {
        private static void Prefix()
        {
            GameComponent_DawnPresentation.FilterCurrentInput();
            GameComponent_NoonPresentation.FilterCurrentInput();
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
        }

        private static void Prefix(Pawn __instance, DamageInfo? dinfo, out KillState __state)
        {
            __state = new KillState
            {
                map = __instance.MapHeld,
                position = __instance.PositionHeld,
                greenKiller = dinfo.HasValue ? dinfo.Value.Instigator as Pawn : null
            };

            if (__instance.kindDef == OrdealDefOf.LCOrdeal_CrimsonNoon && __instance.Spawned)
            {
                NoonOrdealWorker.NotifyCrimsonNoonKilled(__instance);
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
            if (__state == null
                || __state.map == null
                || __state.greenKiller == null
                || __state.greenKiller.kindDef != OrdealDefOf.LCOrdeal_GreenDawn
                || __instance.Faction != Faction.OfPlayer)
            {
                return;
            }

            DawnOrdealSettings settings = DawnOrdealWorker.ActiveSettings();
            foreach (Pawn pawn in __state.map.mapPawns.FreeColonistsSpawned)
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
