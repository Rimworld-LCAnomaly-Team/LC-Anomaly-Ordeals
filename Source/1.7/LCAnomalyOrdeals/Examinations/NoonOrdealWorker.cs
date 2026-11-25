using System;
using System.Collections.Generic;
using System.Linq;
using LCAnomalyCore.Buildings;
using LCAnomalyCore.Comp;
using LCAnomalyCore.Interface;
using LCAnomalyOrdeals.DefOfs;
using LCAnomalyOrdeals.Defs;
using LCAnomalyOrdeals.Presentation;
using LCAnomalyOrdeals.Render;
using LCAnomalyOrdeals.Utilities;
using LCAnomalyOrdeals.Vfx;
using LCAnomalyStory;
using LCAnomalyStory.Defs;
using LCAnomalyStory.Examinations;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;

namespace LCAnomalyOrdeals.Examinations
{
    public sealed class NoonOrdealWorker : CompanyExaminationWorker
    {
        private const string VariantKey = "LCAnomalyOrdeals.noon.variant";
        private const string MapIdKey = "LCAnomalyOrdeals.noon.mapId";
        private const string TargetIdsKey = "LCAnomalyOrdeals.noon.targetIds";
        private const string NextSpecialTickKey = "LCAnomalyOrdeals.noon.nextSpecialTick";
        private const string NextSecondaryTickKey = "LCAnomalyOrdeals.noon.nextSecondaryTick";

        private enum NoonVariant
        {
            Crimson,
            Green,
            Violet,
            Indigo
        }

        private static NoonVariant? debugForcedVariant;

        internal static void DebugForceNextVariant(string variant)
        {
            NoonVariant parsed;
            debugForcedVariant = Enum.TryParse(variant, out parsed) ? parsed : (NoonVariant?)null;
        }

        public override bool CanStart(ExaminationContext context, out string rejectionReason)
        {
            if (!base.CanStart(context, out rejectionReason)) return false;
            if (SelectMap() == null)
            {
                rejectionReason = "LCOrdeal_DawnNoMap".Translate();
                return false;
            }
            return true;
        }

        public override void OnStarted(ExaminationContext context)
        {
            base.OnStarted(context);
            Map map = SelectMap();
            if (map == null)
            {
                context.Fail("LCOrdeal_DawnNoMap".Translate());
                return;
            }

            NoonVariant variant = debugForcedVariant ?? (NoonVariant)Rand.RangeInclusive(0, 3);
            debugForcedVariant = null;
            NoonOrdealSettings settings = Settings;
            context.SetLong(MapIdKey, map.uniqueID);
            context.SetString(VariantKey, variant.ToString());
            context.SetString(TargetIdsKey, string.Empty);
            context.SetLong(NextSpecialTickKey, context.CurrentTick + FirstSpecialInterval(variant, settings));
            context.SetLong(NextSecondaryTickKey, context.CurrentTick + SecondaryInterval(variant, settings));

            List<Pawn> spawned = variant == NoonVariant.Indigo
                ? SpawnIndigoGroups(map, settings)
                : SpawnStandardTargets(map, variant, TargetCount(variant, OrdealTargetUtility.AllTargets(map).Count, settings));
            if (spawned.Count == 0)
            {
                context.Fail("LCOrdeal_DawnSpawnFailed".Translate());
                return;
            }

            SaveTargetIds(context, spawned.Select(pawn => pawn.thingIDNumber));
            if (variant != NoonVariant.Violet)
            {
                LordMaker.MakeNewLord(Faction.OfMechanoids, new LordJob_AssaultColony(Faction.OfMechanoids, false, false, false, false, false), map, spawned);
            }
            GameComponent_NoonPresentation.ShowStart(variant.ToString());
        }

        public override void OnLoaded(ExaminationContext context)
        {
            base.OnLoaded(context);
            if (FindMap(context) == null) context.Fail("LCOrdeal_DawnNoMap".Translate());
        }

        public override void Tick(ExaminationContext context)
        {
            base.Tick(context);
            Map map = FindMap(context);
            if (map == null)
            {
                context.Fail("LCOrdeal_DawnNoMap".Translate());
                return;
            }

            NoonVariant variant = ReadVariant(context);
            NoonOrdealSettings settings = Settings;
            List<Pawn> targets = LivingTargets(context, map);
            SaveTargetIds(context, targets.Select(pawn => pawn.thingIDNumber));
            if (targets.Count == 0)
            {
                context.Pass();
                return;
            }

            if (variant == NoonVariant.Green) UpdateGreen(context, map, targets, settings);
            else if (variant == NoonVariant.Violet) UpdateViolet(context, map, targets, settings);
            else if (variant == NoonVariant.Indigo) UpdateIndigo(context, map, targets, settings);
            else if (context.CurrentTick >= context.GetLong(NextSpecialTickKey, int.MaxValue))
            {
                RetargetCrimsonSaboteurs(map, targets.Where(pawn => pawn.kindDef == OrdealDefOf.LCOrdeal_CrimsonDawn));
                context.SetLong(NextSpecialTickKey, context.CurrentTick + settings.crimsonDawnRetargetIntervalTicks);
            }
        }

        public override ExaminationOutcome Evaluate(ExaminationContext context)
        {
            Map map = FindMap(context);
            if (map != null && LivingTargets(context, map).Count == 0) return ExaminationOutcome.Passed;
            return base.Evaluate(context);
        }

        public override void OnPassed(ExaminationContext context)
        {
            NoonVariant variant = ReadVariant(context);
            Map map = FindMap(context);
            NoonOrdealSettings settings = Settings;
            int reward = settings.rewardBase + (map == null ? 0 : settings.rewardPerColonist * map.mapPawns.FreeColonistsSpawnedCount);
            Cleanup(context);
            SpawnReward(map, reward);
            OrdealTargetUtility.GrantOrdealSuppressionLoopRewards(map, 2);
            GameComponent_NoonPresentation.ShowEnd(variant.ToString(), true);
            base.OnPassed(context);
        }

        public override void OnFailed(ExaminationContext context)
        {
            NoonVariant variant = ReadVariant(context);
            GameComponent_NoonPresentation.ShowEnd(variant.ToString(), false);
            Cleanup(context);
            base.OnFailed(context);
        }

        public override void OnCancelled(ExaminationContext context)
        {
            Cleanup(context);
            base.OnCancelled(context);
        }

        public override IEnumerable<string> ProgressLines(ExaminationContext context)
        {
            NoonVariant variant = ReadVariant(context);
            Map map = FindMap(context);
            yield return "LCOrdeal_NoonVariant".Translate() + ": " + ("LCOrdeal_Noon" + variant + "Name").Translate();
            yield return "LCOrdeal_DawnTargets".Translate() + ": " + (map == null ? 0 : LivingTargets(context, map).Count);
        }

        public static NoonOrdealSettings ActiveSettings()
        {
            CompanyExaminationDef definition = DefDatabase<CompanyExaminationDef>.GetNamedSilentFail("LCOrdeal_Noon");
            return definition?.GetModExtension<NoonOrdealSettings>() ?? new NoonOrdealSettings();
        }

        public static void NotifyCrimsonNoonKilled(Pawn parent)
        {
            if (parent == null || !parent.Spawned || parent.kindDef != OrdealDefOf.LCOrdeal_CrimsonNoon || StoryComponents.Development == null) return;
            ExaminationContext context;
            if (!StoryComponents.Development.TryGetActiveExaminationContext(out context)
                || context.Definition.defName != "LCOrdeal_Noon"
                || ReadVariant(context) != NoonVariant.Crimson) return;

            Map map = parent.Map;
            Color crimsonFx = new Color(0.92f, 0.16f, 0.22f, 0.90f);
            OrdealVfxUtility.SpawnShockwave(map, parent.DrawPos, 0.6f, 4.2f, crimsonFx, 36);
            OrdealVfxUtility.SpawnCircusTent(map, parent.DrawPos, 3.2f, 54);
            List<Pawn> children = new List<Pawn>();
            for (int i = 0; i < ActiveSettings().crimsonDawnSplitCount; i++)
            {
                IntVec3 cell;
                if (!RCellFinder.TryFindRandomPawnEntryCell(out cell, map, CellFinder.EdgeRoadChance_Hostile, true)) continue;
                Pawn child = PawnGenerator.GeneratePawn(OrdealDefOf.LCOrdeal_CrimsonDawn, Faction.OfMechanoids);
                GenSpawn.Spawn(child, cell, map);
                children.Add(child);
            }
            AppendTargetIds(context, children.Select(child => child.thingIDNumber));
            if (children.Count > 0)
            {
                LordMaker.MakeNewLord(Faction.OfMechanoids, new LordJob_AssaultColony(Faction.OfMechanoids, false, false, false, false, false), map, children);
            }
        }

        public static void NotifyIndigoDamage(Pawn sweeper, float damageDealt)
        {
            if (sweeper?.kindDef != OrdealDefOf.LCOrdeal_IndigoNoon || damageDealt <= 0f) return;
            Heal(sweeper, damageDealt * ActiveSettings().indigoLifestealFactor);
        }

        private NoonOrdealSettings Settings => Def.GetModExtension<NoonOrdealSettings>() ?? new NoonOrdealSettings();

        private static List<Pawn> SpawnStandardTargets(Map map, NoonVariant variant, int count)
        {
            List<Pawn> result = new List<Pawn>();
            for (int i = 0; i < count; i++)
            {
                IntVec3 cell;
                if (!RCellFinder.TryFindRandomPawnEntryCell(out cell, map, CellFinder.EdgeRoadChance_Hostile, true)) continue;
                Pawn pawn = PawnGenerator.GeneratePawn(PawnKindFor(variant), Faction.OfMechanoids);
                if (variant == NoonVariant.Violet) pawn.health.AddHediff(OrdealDefOf.LCOrdeal_VioletNoonImmobile);
                GenSpawn.Spawn(pawn, cell, map);
                result.Add(pawn);
                if (variant == NoonVariant.Violet) ApplyVioletImpact(map, pawn, ActiveSettings());
            }
            return result;
        }

        private static List<Pawn> SpawnIndigoGroups(Map map, NoonOrdealSettings settings)
        {
            List<Pawn> result = new List<Pawn>();
            for (int group = 0; group < settings.indigoGroupCount; group++)
            {
                IntVec3 anchor;
                if (!RCellFinder.TryFindRandomPawnEntryCell(out anchor, map, CellFinder.EdgeRoadChance_Hostile, true)) continue;
                for (int member = 0; member < settings.indigoGroupSize; member++)
                {
                    IntVec3 cell = CellFinder.RandomClosewalkCellNear(anchor, map, settings.indigoGroupScatterRadius);
                    Pawn pawn = PawnGenerator.GeneratePawn(OrdealDefOf.LCOrdeal_IndigoNoon, Faction.OfMechanoids);
                    GenSpawn.Spawn(pawn, cell, map);
                    result.Add(pawn);
                }
            }
            return result;
        }

        private static void ApplyVioletImpact(Map map, Pawn monolith, NoonOrdealSettings settings)
        {
            OrdealDefOf.LCOrdeal_Sound_OutterGodNoonDown?.PlayOneShot(new TargetInfo(monolith.Position, map));
            CompOrdealAnimator.TriggerAttackOn(monolith, 55);
            Vector3 impactPos = monolith.Position.ToVector3Shifted();
            Color violetFx = new Color(0.64f, 0.20f, 0.95f, 0.94f);
            OrdealVfxUtility.SpawnPortal(map, impactPos, 4.8f, violetFx, 75, rotateSpeed: 180f);
            OrdealVfxUtility.SpawnCrater(map, impactPos, 5.2f, new Color(0.45f, 0.16f, 0.72f, 0.85f), 200);
            OrdealVfxUtility.SpawnShockwave(map, impactPos, 0.8f, settings.violetImpactRadius, violetFx, 46);
            FleckMaker.Static(monolith.Position, map, FleckDefOf.PsycastAreaEffect, 5f);
            DamageDef blackDamage = LCAnomalyCore.Defs.DamageDefOf.LC_BlackDamage ?? DamageDefOf.Blunt;
            foreach (Pawn pawn in OrdealTargetUtility.AllTargets(map))
            {
                if (!pawn.Dead && pawn.Position.InHorDistOf(monolith.Position, settings.violetImpactRadius))
                {
                    pawn.TakeDamage(new DamageInfo(blackDamage, settings.violetImpactDamage, 0.2f, instigator: monolith, weapon: monolith.def));
                }
            }
        }

        private static void UpdateGreen(ExaminationContext context, Map map, IEnumerable<Pawn> targets, NoonOrdealSettings settings)
        {
            foreach (Pawn pawn in targets.Where(target => target.kindDef == OrdealDefOf.LCOrdeal_GreenNoon))
            {
                string nextKey = GreenNextShutdownKey(pawn.thingIDNumber);
                string untilKey = GreenShutdownUntilKey(pawn.thingIDNumber);
                long next = context.GetLong(nextKey, -1L);
                if (next < 0)
                {
                    context.SetLong(nextKey, context.CurrentTick + settings.greenShutdownIntervalTicks.RandomInRange);
                    continue;
                }
                if (context.CurrentTick >= next)
                {
                    int duration = settings.greenShutdownDurationTicks.RandomInRange;
                    context.SetLong(untilKey, context.CurrentTick + duration);
                    context.SetLong(nextKey, context.CurrentTick + duration + settings.greenShutdownIntervalTicks.RandomInRange);
                    pawn.stances.stunner.StunFor(duration, pawn, false);
                    CompOrdealAnimator.TriggerSpecialOn(pawn, duration, new Color(0.76f, 0.86f, 0.78f), 0.7f);
                    OrdealVfxUtility.SpawnPawnAura(map, pawn, OrdealVfxUtility.TexSoftGlow, 2.0f, new Color(0.70f, 0.76f, 0.72f, 0.55f), duration, rotateSpeed: 40f);
                    OrdealDefOf.LCOrdeal_Sound_MachineNoonCooldown?.PlayOneShot(new TargetInfo(pawn.Position, map));
                }
                else if (context.CurrentTick < context.GetLong(untilKey, -1L) && !pawn.stances.stunner.Stunned)
                {
                    pawn.stances.stunner.StunFor(Math.Min(60, (int)(context.GetLong(untilKey) - context.CurrentTick)), pawn, false);
                }
            }

            if (context.CurrentTick >= context.GetLong(NextSecondaryTickKey, int.MaxValue))
            {
                DamageDef redDamage = LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage ?? DamageDefOf.Cut;
                foreach (Pawn machine in targets.Where(target => target.kindDef == OrdealDefOf.LCOrdeal_GreenNoon && !target.stances.stunner.Stunned))
                {
                    bool hitAny = false;
                    foreach (Pawn colonist in OrdealTargetUtility.AllTargets(map).Where(pawn => pawn.Position.InHorDistOf(machine.Position, settings.greenSawRadius)))
                    {
                        colonist.TakeDamage(new DamageInfo(redDamage, settings.greenSawDamage, 0.2f, instigator: machine, weapon: machine.def));
                        hitAny = true;
                    }
                    if (hitAny)
                    {
                        Comps.CompGreenOrdeal greenComp = machine.TryGetComp<Comps.CompGreenOrdeal>();
                        if (greenComp != null) greenComp.NotifyMeleeAttack(null);
                        else CompOrdealAnimator.TriggerAttackOn(machine, 32);
                        OrdealVfxUtility.SpawnSlash(map, machine.DrawPos, settings.greenSawRadius * 1.45f, new Color(0.32f, 0.95f, 0.45f, 0.92f), 22);
                        OrdealVfxUtility.SpawnShockwave(map, machine.DrawPos, 0.5f, settings.greenSawRadius, new Color(0.95f, 0.30f, 0.25f, 0.80f), 24);
                        OrdealDefOf.LCOrdeal_Sound_MachineNoonAtk?.PlayOneShot(new TargetInfo(machine.Position, map));
                    }
                }
                context.SetLong(NextSecondaryTickKey, context.CurrentTick + settings.greenSawIntervalTicks);
            }
        }

        private static void UpdateViolet(ExaminationContext context, Map map, IEnumerable<Pawn> targets, NoonOrdealSettings settings)
        {
            foreach (Pawn pawn in targets)
            {
                pawn.pather.StopDead();
                if (pawn.CurJob != null) pawn.jobs.StopAll();
            }

            if (context.CurrentTick >= context.GetLong(NextSpecialTickKey, int.MaxValue))
            {
                foreach (Pawn monolith in targets)
                {
                    Color pulseViolet = new Color(0.68f, 0.24f, 0.96f, 0.88f);
                    Comps.CompVioletOrdeal violetComp = monolith.TryGetComp<Comps.CompVioletOrdeal>();
                    if (violetComp != null) violetComp.TriggerCast(56);
                    else CompOrdealAnimator.TriggerSpecialOn(monolith, 40, pulseViolet, 0.9f);
                    OrdealVfxUtility.SpawnShockwave(map, monolith.DrawPos, 0.7f, settings.violetPulseRadius, pulseViolet, 38);
                    OrdealVfxUtility.SpawnStrikeSprite(map, monolith.DrawPos, OrdealVfxUtility.TexTentacleBlack, 1.4f, 3.2f, Color.white, 34, riseHeight: 0.6f, angleDegrees: Rand.Range(0f, 360f));
                    if (PulseBlackDamage(map, monolith, settings.violetPulsePsychicDamage, settings.violetPulseRadius))
                    {
                        OrdealDefOf.LCOrdeal_Sound_OutterGodNoonCasting?.PlayOneShot(new TargetInfo(monolith.Position, map));
                    }
                }
                context.SetLong(NextSpecialTickKey, context.CurrentTick + settings.violetPulseIntervalTicks);
            }
            if (context.CurrentTick >= context.GetLong(NextSecondaryTickKey, int.MaxValue))
            {
                List<Building> units = map.listerBuildings.allBuildingsColonist.Where(b => b is IAbnormalityContainmentUnit).ToList();
                foreach (Pawn monolith in targets)
                {
                    Building unitBuilding = units.Where(item => (item as IAbnormalityContainmentUnit)?.HeldPawn?.TryGetComp<CompAbnormality>()?.QliphothEnabled == true).RandomElementWithFallback();
                    CompAbnormality abnormality = (unitBuilding as IAbnormalityContainmentUnit)?.HeldPawn?.TryGetComp<CompAbnormality>();
                    if (abnormality != null)
                    {
                        abnormality.QliphothCountCurrent = Math.Max(0, abnormality.QliphothCountCurrent - settings.violetCounterReduction);
                        OrdealVfxUtility.SpawnPortal(map, unitBuilding.DrawPos, 2.2f, new Color(0.66f, 0.22f, 0.96f, 0.86f), 45);
                    }
                }
                context.SetLong(NextSecondaryTickKey, context.CurrentTick + settings.violetCounterIntervalTicks);
            }
        }

        private static void UpdateIndigo(ExaminationContext context, Map map, IEnumerable<Pawn> targets, NoonOrdealSettings settings)
        {
            if (context.CurrentTick >= context.GetLong(NextSpecialTickKey, int.MaxValue))
            {
                List<Corpse> corpses = map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse)
                    .OfType<Corpse>()
                    .Where(corpse => corpse.Spawned && corpse.InnerPawn?.RaceProps?.Humanlike == true)
                    .ToList();
                foreach (Pawn sweeper in targets)
                {
                    Corpse corpse = corpses
                        .Where(item => item.Position.InHorDistOf(sweeper.Position, settings.indigoCorpseSearchRadius))
                        .OrderBy(item => item.Position.DistanceToSquared(sweeper.Position))
                        .FirstOrDefault();
                    if (corpse == null) continue;
                    Heal(sweeper, 99999f);
                    Comps.CompWhiteAndIndigoOrdeal swComp = sweeper.TryGetComp<Comps.CompWhiteAndIndigoOrdeal>();
                    if (swComp != null) swComp.TriggerEat(48);
                    else CompOrdealAnimator.TriggerSpecialOn(sweeper, 45, new Color(0.95f, 0.26f, 0.34f), 0.95f);
                    OrdealVfxUtility.SpawnPawnAura(map, sweeper, OrdealVfxUtility.TexHollowRing, 1.8f, new Color(0.92f, 0.18f, 0.26f, 0.88f), 42, rotateSpeed: 150f);
                    OrdealVfxUtility.SpawnShockwave(map, corpse.DrawPos, 0.4f, 2.6f, new Color(0.88f, 0.14f, 0.24f, 0.86f), 30);
                    OrdealDefOf.LCOrdeal_Sound_ScavengerNoonSkill?.PlayOneShot(new TargetInfo(sweeper.Position, map));
                    corpses.Remove(corpse);
                    corpse.Destroy(DestroyMode.Vanish);
                }
                context.SetLong(NextSpecialTickKey, context.CurrentTick + settings.indigoCorpseCheckIntervalTicks);
            }
            if (context.CurrentTick >= context.GetLong(NextSecondaryTickKey, int.MaxValue))
            {
                foreach (Pawn sweeper in targets)
                {
                    if (PulseBlackDamage(map, sweeper, settings.indigoChargePsychicDamage, settings.indigoChargeRadius))
                    {
                        CompOrdealAnimator.TriggerAttackOn(sweeper, 36);
                        OrdealVfxUtility.SpawnSlash(map, sweeper.DrawPos, settings.indigoChargeRadius * 1.25f, new Color(0.32f, 0.44f, 0.98f, 0.92f), 26);
                        OrdealVfxUtility.SpawnShockwave(map, sweeper.DrawPos, 0.5f, settings.indigoChargeRadius, new Color(0.28f, 0.34f, 0.88f, 0.85f), 30);
                        OrdealDefOf.LCOrdeal_Sound_ScavengerNoonSkill?.PlayOneShot(new TargetInfo(sweeper.Position, map));
                    }
                }
                context.SetLong(NextSecondaryTickKey, context.CurrentTick + settings.indigoChargeIntervalTicks);
            }
        }

        private static bool PulseBlackDamage(Map map, Thing instigator, float damage, float radius)
        {
            DamageDef blackDamage = LCAnomalyCore.Defs.DamageDefOf.LC_BlackDamage ?? DamageDefOf.Psychic;
            bool hitAny = false;
            foreach (Pawn pawn in OrdealTargetUtility.AllTargets(map))
            {
                if (!pawn.Dead && pawn.Position.InHorDistOf(instigator.PositionHeld, radius))
                {
                    pawn.TakeDamage(new DamageInfo(blackDamage, damage, 0f, instigator: instigator));
                    hitAny = true;
                }
            }
            return hitAny;
        }

        private static void Heal(Pawn pawn, float amount)
        {
            if (pawn == null || pawn.Dead || amount <= 0f) return;
            foreach (Hediff_Injury injury in pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().ToList())
            {
                float healed = Math.Min(amount, injury.Severity);
                injury.Heal(healed);
                amount -= healed;
                if (amount <= 0f) break;
            }
        }

        private static void RetargetCrimsonSaboteurs(Map map, IEnumerable<Pawn> targets)
        {
            List<Building> priorities = map.listerThings.AllThings.OfType<Building>().Where(building => building.Faction == Faction.OfPlayer && (building.def.defName == "LC_ContainmentTerminal" || building.TryGetComp<CompPowerTrader>() != null)).ToList();
            if (priorities.Count == 0) return;
            foreach (Pawn pawn in targets)
            {
                Building target = priorities.MinBy(building => building.Position.DistanceToSquared(pawn.Position));
                if (target != null && pawn.CanReach(target, PathEndMode.Touch, Danger.Deadly)) pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.AttackMelee, target));
            }
        }

        private static int TargetCount(NoonVariant variant, int colonists, NoonOrdealSettings settings)
        {
            int population = Math.Max(1, colonists);
            if (variant == NoonVariant.Crimson) return Mathf.Clamp(Mathf.CeilToInt(population / settings.crimsonColonistsPerTarget), settings.crimsonCountMin, settings.crimsonCountMax);
            if (variant == NoonVariant.Green) return Mathf.Clamp(Mathf.CeilToInt(population / settings.greenColonistsPerTarget), settings.greenCountMin, settings.greenCountMax);
            if (variant == NoonVariant.Violet) return Mathf.Clamp(Mathf.CeilToInt(population / settings.violetColonistsPerTarget), settings.violetCountMin, settings.violetCountMax);
            return settings.indigoGroupCount * settings.indigoGroupSize;
        }

        private static PawnKindDef PawnKindFor(NoonVariant variant)
        {
            if (variant == NoonVariant.Crimson) return OrdealDefOf.LCOrdeal_CrimsonNoon;
            if (variant == NoonVariant.Green) return OrdealDefOf.LCOrdeal_GreenNoon;
            if (variant == NoonVariant.Violet) return OrdealDefOf.LCOrdeal_VioletNoon;
            return OrdealDefOf.LCOrdeal_IndigoNoon;
        }

        private static int FirstSpecialInterval(NoonVariant variant, NoonOrdealSettings settings)
        {
            if (variant == NoonVariant.Violet) return settings.violetPulseIntervalTicks;
            if (variant == NoonVariant.Indigo) return settings.indigoCorpseCheckIntervalTicks;
            if (variant == NoonVariant.Green) return settings.greenShutdownIntervalTicks.RandomInRange;
            return settings.crimsonDawnRetargetIntervalTicks;
        }

        private static int SecondaryInterval(NoonVariant variant, NoonOrdealSettings settings)
        {
            if (variant == NoonVariant.Violet) return settings.violetCounterIntervalTicks;
            if (variant == NoonVariant.Indigo) return settings.indigoChargeIntervalTicks;
            if (variant == NoonVariant.Green) return settings.greenSawIntervalTicks;
            return int.MaxValue / 2;
        }

        private static Map SelectMap() => Find.CurrentMap != null && Find.CurrentMap.IsPlayerHome && Find.CurrentMap.mapPawns.AnyFreeColonistSpawned ? Find.CurrentMap : Find.Maps.FirstOrDefault(map => map.IsPlayerHome && map.mapPawns.AnyFreeColonistSpawned);
        private static Map FindMap(ExaminationContext context) { int id = (int)context.GetLong(MapIdKey, -1L); return Find.Maps.FirstOrDefault(map => map.uniqueID == id); }
        private static NoonVariant ReadVariant(ExaminationContext context) { NoonVariant variant; return Enum.TryParse(context.GetString(VariantKey), out variant) ? variant : NoonVariant.Crimson; }
        private static string GreenNextShutdownKey(int id) => "LCAnomalyOrdeals.noon.green." + id + ".nextShutdown";
        private static string GreenShutdownUntilKey(int id) => "LCAnomalyOrdeals.noon.green." + id + ".shutdownUntil";

        private static List<int> TargetIds(ExaminationContext context)
        {
            List<int> result = new List<int>();
            foreach (string part in context.GetString(TargetIdsKey, string.Empty).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)) { int id; if (int.TryParse(part, out id)) result.Add(id); }
            return result;
        }
        private static void SaveTargetIds(ExaminationContext context, IEnumerable<int> ids) => context.SetString(TargetIdsKey, string.Join(",", ids.Distinct()));
        private static void AppendTargetIds(ExaminationContext context, IEnumerable<int> ids) => SaveTargetIds(context, TargetIds(context).Concat(ids));
        private static List<Pawn> LivingTargets(ExaminationContext context, Map map) { HashSet<int> ids = new HashSet<int>(TargetIds(context)); return map.mapPawns.AllPawnsSpawned.Where(pawn => ids.Contains(pawn.thingIDNumber) && !pawn.Dead && !pawn.Destroyed).ToList(); }

        private static void SpawnReward(Map map, int count)
        {
            ThingDef rewardDef = DefDatabase<ThingDef>.GetNamedSilentFail("EnkephalinBox");
            Pawn recipient = map?.mapPawns.FreeColonistsSpawned.FirstOrDefault();
            if (rewardDef == null || recipient == null || count <= 0) return;
            Thing reward = ThingMaker.MakeThing(rewardDef); reward.stackCount = Math.Min(count, rewardDef.stackLimit); GenPlace.TryPlaceThing(reward, recipient.Position, map, ThingPlaceMode.Near);
        }

        private static void Cleanup(ExaminationContext context)
        {
            HashSet<int> ids = new HashSet<int>(TargetIds(context));
            foreach (Map map in Find.Maps)
                foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned.Where(pawn => ids.Contains(pawn.thingIDNumber)).ToList())
                    if (!pawn.Destroyed) pawn.Destroy(DestroyMode.Vanish);
        }
    }
}
