using System.Collections.Generic;
using HarmonyLib;
using LCAnomalyOrdeals.Comps;
using LCAnomalyOrdeals.DefOfs;
using LCAnomalyOrdeals.Defs;
using LCAnomalyOrdeals.Examinations;
using LCAnomalyOrdeals.Presentation;
using LCAnomalyOrdeals.Render;
using LCAnomalyOrdeals.Utilities;
using LCAnomalyOrdeals.Vfx;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

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
                CompDawnOrdeal dawnComp = instigator.TryGetComp<CompDawnOrdeal>();
                if (dawnComp != null)
                {
                    dawnComp.NotifyMeleeAttack(__instance);
                }
                else
                {
                    CompCrimsonOrdeal crimsonComp = instigator.TryGetComp<CompCrimsonOrdeal>();
                    if (crimsonComp != null)
                    {
                        crimsonComp.NotifyMeleeAttack(__instance);
                    }
                    else
                    {
                        CompAmberOrdeal amberComp = instigator.TryGetComp<CompAmberOrdeal>();
                        if (amberComp != null)
                        {
                            amberComp.NotifyMeleeAttack(__instance);
                        }
                        else
                        {
                            CompGreenOrdeal greenComp = instigator.TryGetComp<CompGreenOrdeal>();
                            if (greenComp != null)
                            {
                                greenComp.NotifyMeleeAttack(__instance);
                            }
                            else
                            {
                                CompWhiteAndIndigoOrdeal wiComp = instigator.TryGetComp<CompWhiteAndIndigoOrdeal>();
                                if (wiComp != null)
                                {
                                    wiComp.NotifyMeleeAttack(__instance);
                                }
                                else if (OrdealTargetUtility.IsOrdealPawn(instigator))
                                {
                                    CompOrdealAnimator.TriggerAttackOn(instigator, 26);
                                }
                            }
                        }
                    }
                }
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

            if (__instance.Spawned)
            {
                PlayOrdealDeathSound(__instance);
            }

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
                Vector3 blastPos = __instance.DrawPos;
                OrdealVfxUtility.SpawnShockwave(__instance.Map, blastPos, 0.5f, settings.crimsonExplosionRadius * 1.35f, new Color(0.96f, 0.16f, 0.22f, 0.95f), 34);
                OrdealVfxUtility.SpawnSlash(__instance.Map, blastPos, settings.crimsonExplosionRadius * 1.25f, new Color(1f, 0.25f, 0.25f, 0.90f), 24);
                OrdealVfxUtility.SpawnStrikeSprite(__instance.Map, blastPos, OrdealVfxUtility.TexLaserHit, 1.0f, 3.6f, new Color(0.98f, 0.18f, 0.22f, 0.92f), 28, riseHeight: 0.2f, rotateSpeed: 120f, additive: true);
                DamageDef redBlast = LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage ?? DamageDefOf.Bomb;
                GenExplosion.DoExplosion(
                    __instance.Position,
                    __instance.Map,
                    settings.crimsonExplosionRadius,
                    redBlast,
                    __instance,
                    settings.crimsonExplosionDamage,
                    settings.crimsonExplosionArmorPenetration,
                    explosionSound: OrdealDefOf.LCOrdeal_Sound_CircusDawnDead ?? DamageDefOf.Bomb.soundExplosion,
                    weapon: __instance.def,
                    ignoredThings: new List<Thing> { __instance },
                    damageFalloff: true);
            }
        }

        private static void PlayOrdealDeathSound(Pawn pawn)
        {
            SoundDef sound = null;
            if (pawn.kindDef == OrdealDefOf.LCOrdeal_AmberDawn) sound = OrdealDefOf.LCOrdeal_Sound_Bug1Dead;
            else if (pawn.kindDef == OrdealDefOf.LCOrdeal_GreenDawn) sound = OrdealDefOf.LCOrdeal_Sound_MachineBroke;
            else if (pawn.kindDef == OrdealDefOf.LCOrdeal_VioletDawn) sound = OrdealDefOf.LCOrdeal_Sound_OutterGodDawnDead;
            else if (pawn.kindDef == OrdealDefOf.LCOrdeal_CrimsonNoon) sound = OrdealDefOf.LCOrdeal_Sound_CircusNoonDead;
            else if (pawn.kindDef == OrdealDefOf.LCOrdeal_GreenNoon) sound = OrdealDefOf.LCOrdeal_Sound_MachineNoonDead;
            else if (pawn.kindDef == OrdealDefOf.LCOrdeal_VioletNoon) sound = OrdealDefOf.LCOrdeal_Sound_OutterGodNoonDead;
            else if (pawn.kindDef == OrdealDefOf.LCOrdeal_IndigoNoon) sound = OrdealDefOf.LCOrdeal_Sound_ScavengerNoonDead;
            else if (pawn.kindDef == OrdealDefOf.LCOrdeal_AmberDusk) sound = OrdealDefOf.LCOrdeal_Sound_BugDuskDead;
            else if (pawn.kindDef == OrdealDefOf.LCOrdeal_CrimsonDusk) sound = OrdealDefOf.LCOrdeal_Sound_CircusDuskDead;
            else if (pawn.kindDef == OrdealDefOf.LCOrdeal_GreenDusk) sound = OrdealDefOf.LCOrdeal_Sound_MachineDuskDead;
            else if (pawn.kindDef == OrdealDefOf.LCOrdeal_AmberMidnight) sound = OrdealDefOf.LCOrdeal_Sound_BugMidnightDead;
            else if (pawn.kindDef == OrdealDefOf.LCOrdeal_GreenMidnight) sound = OrdealDefOf.LCOrdeal_Sound_MachineMidnightDead;
            else if (pawn.kindDef == OrdealDefOf.LCOrdeal_VioletMidnightRed
                || pawn.kindDef == OrdealDefOf.LCOrdeal_VioletMidnightWhite
                || pawn.kindDef == OrdealDefOf.LCOrdeal_VioletMidnightBlack
                || pawn.kindDef == OrdealDefOf.LCOrdeal_VioletMidnightPale)
                sound = OrdealDefOf.LCOrdeal_Sound_VioletMidnightDead;
            else if (pawn.kindDef == OrdealDefOf.LCOrdeal_WhiteFixerRed) sound = OrdealDefOf.LCOrdeal_Sound_FixerRedDead;
            else if (pawn.kindDef == OrdealDefOf.LCOrdeal_WhiteFixerWhite) sound = OrdealDefOf.LCOrdeal_Sound_FixerWhiteDead;
            else if (pawn.kindDef == OrdealDefOf.LCOrdeal_WhiteFixerBlack) sound = OrdealDefOf.LCOrdeal_Sound_FixerBlackDead;
            else if (pawn.kindDef == OrdealDefOf.LCOrdeal_WhiteFixerPale) sound = OrdealDefOf.LCOrdeal_Sound_FixerPaleDead;
            else if (pawn.kindDef == OrdealDefOf.LCOrdeal_WhiteClaw) sound = OrdealDefOf.LCOrdeal_Sound_ClawDead;
            sound?.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
        }

        private static void Postfix(Pawn __instance, KillState __state)
        {
            if (OrdealTargetUtility.IsOrdealPawn(__instance))
            {
                LCAnomalyCore.GameComponent.GameComponent_LC.Instance?.RecordOrdealKill(__instance.def);
            }

            if (__state != null) WhiteOrdealWorker.NotifyKilled(__instance, __state.map, __state.position, __state.rotation);
            if (__state == null
                || __state.map == null
                || __state.greenKiller == null
                || __state.greenKiller.kindDef != OrdealDefOf.LCOrdeal_GreenDawn
                || OrdealTargetUtility.IsOrdealPawn(__instance))
            {
                return;
            }

            CompDawnOrdeal greenComp = __state.greenKiller.TryGetComp<CompDawnOrdeal>();
            if (greenComp != null && __state.greenKiller.Spawned && !__state.greenKiller.Dead)
            {
                greenComp.BeginGreenExecution(__state.position);
                return;
            }

            OrdealDefOf.LCOrdeal_Sound_MachineBlood?.PlayOneShot(new TargetInfo(__state.position, __state.map));
            CompOrdealAnimator.TriggerAttackOn(__state.greenKiller, 42);
            Vector3 execPos = __state.position.ToVector3Shifted();
            DawnOrdealSettings settings = DawnOrdealWorker.ActiveSettings();
            OrdealVfxUtility.SpawnShockwave(__state.map, execPos, 0.6f, settings.greenExecutionPsychicRadius, new Color(0.28f, 0.96f, 0.56f, 0.85f), 36);
            OrdealVfxUtility.SpawnStrikeSprite(__state.map, execPos, OrdealVfxUtility.TexSparkMidnight, 1.0f, 2.8f, new Color(0.85f, 1f, 0.92f, 0.9f), 26, riseHeight: 0.2f, rotateSpeed: 120f, additive: true);
            DamageDef whiteDamage = LCAnomalyCore.Defs.DamageDefOf.LC_WhiteDamage ?? DamageDefOf.Psychic;
            foreach (Pawn pawn in OrdealTargetUtility.AllTargets(__state.map))
            {
                if (!pawn.Dead && pawn.Position.InHorDistOf(__state.position, settings.greenExecutionPsychicRadius))
                {
                    pawn.TakeDamage(new DamageInfo(
                        whiteDamage,
                        settings.greenExecutionPsychicDamage,
                        0f,
                        instigator: __state.greenKiller));
                }
            }
        }
    }
}
