using System;
using System.Collections.Generic;
using System.Linq;
using LCAnomalyCore.Buildings;
using LCAnomalyOrdeals.DefOfs;
using LCAnomalyOrdeals.Defs;
using LCAnomalyOrdeals.Presentation;
using LCAnomalyOrdeals.Utilities;
using LCAnomalyStory;
using LCAnomalyStory.Defs;
using LCAnomalyStory.Examinations;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace LCAnomalyOrdeals.Examinations
{
    public sealed class DuskOrdealWorker : CompanyExaminationWorker
    {
        private const string VariantKey = "LCAnomalyOrdeals.dusk.variant";
        private const string MapIdKey = "LCAnomalyOrdeals.dusk.mapId";
        private const string TargetIdsKey = "LCAnomalyOrdeals.dusk.targetIds";
        private const string NextSpecialTickKey = "LCAnomalyOrdeals.dusk.nextSpecialTick";
        private const string NextSecondaryTickKey = "LCAnomalyOrdeals.dusk.nextSecondaryTick";

        private enum DuskVariant { Amber, Crimson, Green }

        private static DuskVariant? debugForcedVariant;

        internal static void DebugForceNextVariant(string variant)
        {
            DuskVariant parsed;
            debugForcedVariant = Enum.TryParse(variant, out parsed) ? parsed : (DuskVariant?)null;
        }

        public override bool CanStart(ExaminationContext context, out string rejectionReason)
        {
            if (!base.CanStart(context, out rejectionReason)) return false;
            if (SelectMap() == null) { rejectionReason = "LCOrdeal_DawnNoMap".Translate(); return false; }
            return true;
        }

        public override void OnStarted(ExaminationContext context)
        {
            base.OnStarted(context);
            Map map = SelectMap();
            if (map == null) { context.Fail("LCOrdeal_DawnNoMap".Translate()); return; }
            DuskVariant variant = debugForcedVariant ?? (DuskVariant)Rand.RangeInclusive(0, 2);
            debugForcedVariant = null;
            DuskOrdealSettings settings = Settings;
            context.SetLong(MapIdKey, map.uniqueID);
            context.SetString(VariantKey, variant.ToString());
            context.SetString(TargetIdsKey, string.Empty);
            context.SetLong(NextSpecialTickKey, context.CurrentTick + InitialSpecialInterval(variant, settings));
            context.SetLong(NextSecondaryTickKey, context.CurrentTick + InitialSecondaryInterval(variant, settings));

            int count = variant == DuskVariant.Amber
                ? Mathf.Clamp(Mathf.CeilToInt(Math.Max(1, OrdealTargetUtility.AllTargets(map).Count) / settings.amberColonistsPerTarget), settings.amberCountMin, settings.amberCountMax)
                : variant == DuskVariant.Crimson ? settings.crimsonCount : settings.greenFactoryCount;
            List<Pawn> spawned = SpawnInitial(map, variant, count, settings);
            if (spawned.Count == 0) { context.Fail("LCOrdeal_DawnSpawnFailed".Translate()); return; }
            SaveTargetIds(context, spawned.Select(pawn => pawn.thingIDNumber));
            if (variant != DuskVariant.Green) AssignAssaultLord(map, spawned);
            GameComponent_DuskPresentation.ShowStart(variant.ToString());
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
            if (map == null) { context.Fail("LCOrdeal_DawnNoMap".Translate()); return; }
            List<Pawn> targets = LivingTargets(context, map);
            SaveTargetIds(context, targets.Select(pawn => pawn.thingIDNumber));
            if (targets.Count == 0) { context.Pass(); return; }
            DuskOrdealSettings settings = Settings;
            DuskVariant variant = ReadVariant(context);
            if (variant == DuskVariant.Amber) UpdateAmber(context, map, targets, settings);
            else if (variant == DuskVariant.Crimson) UpdateCrimson(context, map, targets, settings);
            else UpdateGreen(context, map, targets, settings);
        }

        public override ExaminationOutcome Evaluate(ExaminationContext context)
        {
            Map map = FindMap(context);
            if (map != null && LivingTargets(context, map).Count == 0) return ExaminationOutcome.Passed;
            return base.Evaluate(context);
        }

        public override void OnPassed(ExaminationContext context)
        {
            DuskVariant variant = ReadVariant(context); Map map = FindMap(context); DuskOrdealSettings settings = Settings;
            int reward = settings.rewardBase + (map == null ? 0 : settings.rewardPerColonist * map.mapPawns.FreeColonistsSpawnedCount);
            Cleanup(context); SpawnReward(map, reward); GameComponent_DuskPresentation.ShowEnd(variant.ToString(), true); base.OnPassed(context);
        }

        public override void OnFailed(ExaminationContext context)
        {
            DuskVariant variant = ReadVariant(context); GameComponent_DuskPresentation.ShowEnd(variant.ToString(), false); Cleanup(context); base.OnFailed(context);
        }

        public override void OnCancelled(ExaminationContext context) { Cleanup(context); base.OnCancelled(context); }

        public override IEnumerable<string> ProgressLines(ExaminationContext context)
        {
            DuskVariant variant = ReadVariant(context); Map map = FindMap(context);
            yield return "LCOrdeal_DuskVariant".Translate() + ": " + ("LCOrdeal_Dusk" + variant + "Name").Translate();
            yield return "LCOrdeal_DawnTargets".Translate() + ": " + (map == null ? 0 : LivingTargets(context, map).Count);
        }

        public static DuskOrdealSettings ActiveSettings()
        {
            CompanyExaminationDef definition = DefDatabase<CompanyExaminationDef>.GetNamedSilentFail("LCOrdeal_Dusk");
            return definition?.GetModExtension<DuskOrdealSettings>() ?? new DuskOrdealSettings();
        }

        public static void NotifyAmberDamaged(Pawn worm, float damageDealt)
        {
            if (worm?.kindDef != OrdealDefOf.LCOrdeal_AmberDusk || worm.Dead || damageDealt < ActiveSettings().amberSlowDamageThreshold || StoryComponents.Development == null) return;
            ExaminationContext context;
            if (!TryActiveContext(out context, DuskVariant.Amber)) return;
            if (worm.health.hediffSet.GetFirstHediffOfDef(OrdealDefOf.LCOrdeal_AmberDuskSlowness) == null) worm.health.AddHediff(OrdealDefOf.LCOrdeal_AmberDuskSlowness);
            context.SetLong(AmberSlowUntilKey(worm.thingIDNumber), context.CurrentTick + ActiveSettings().amberSlowDurationTicks.RandomInRange);
        }

        public static void NotifyCrimsonKilled(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || StoryComponents.Development == null) return;
            ExaminationContext context;
            if (!TryActiveContext(out context, DuskVariant.Crimson)) return;
            DuskOrdealSettings settings = ActiveSettings(); Map map = pawn.Map; List<Pawn> spawned = new List<Pawn>();
            if (pawn.kindDef == OrdealDefOf.LCOrdeal_CrimsonDusk)
            {
                for (int i = 0; i < settings.crimsonNoonSplitCount; i++) spawned.Add(SpawnNear(map, pawn.Position, OrdealDefOf.LCOrdeal_CrimsonNoon, 5));
            }
            else if (pawn.kindDef == OrdealDefOf.LCOrdeal_CrimsonNoon)
            {
                for (int i = 0; i < settings.crimsonDawnSplitCount; i++)
                {
                    IntVec3 cell; if (!RCellFinder.TryFindRandomPawnEntryCell(out cell, map, CellFinder.EdgeRoadChance_Hostile, true)) continue;
                    spawned.Add(SpawnAt(map, cell, OrdealDefOf.LCOrdeal_CrimsonDawn));
                }
            }
            spawned.RemoveAll(item => item == null); AppendTargetIds(context, spawned.Select(item => item.thingIDNumber)); AssignAssaultLord(map, spawned);
        }

        private DuskOrdealSettings Settings => Def.GetModExtension<DuskOrdealSettings>() ?? new DuskOrdealSettings();

        private static bool TryActiveContext(out ExaminationContext context, DuskVariant variant)
        {
            context = null;
            return StoryComponents.Development.TryGetActiveExaminationContext(out context) && context.Definition.defName == "LCOrdeal_Dusk" && ReadVariant(context) == variant;
        }

        private static List<Pawn> SpawnInitial(Map map, DuskVariant variant, int count, DuskOrdealSettings settings)
        {
            List<Pawn> result = new List<Pawn>(); PawnKindDef kind = variant == DuskVariant.Amber ? OrdealDefOf.LCOrdeal_AmberDusk : variant == DuskVariant.Crimson ? OrdealDefOf.LCOrdeal_CrimsonDusk : OrdealDefOf.LCOrdeal_GreenDusk;
            for (int i = 0; i < count; i++)
            {
                IntVec3 cell; if (!RCellFinder.TryFindRandomPawnEntryCell(out cell, map, CellFinder.EdgeRoadChance_Hostile, true)) continue;
                Pawn pawn = SpawnAt(map, cell, kind); if (pawn == null) continue;
                if (variant == DuskVariant.Green) pawn.health.AddHediff(OrdealDefOf.LCOrdeal_VioletNoonImmobile);
                if (variant == DuskVariant.Amber) DamageNearby(map, pawn, settings.amberEmergenceRadius, settings.amberEmergenceDamage, DamageDefOf.Blunt);
                result.Add(pawn);
            }
            return result;
        }

        private static void UpdateAmber(ExaminationContext context, Map map, List<Pawn> targets, DuskOrdealSettings settings)
        {
            foreach (Pawn worm in targets.Where(item => item.kindDef == OrdealDefOf.LCOrdeal_AmberDusk))
            {
                Hediff slow = worm.health.hediffSet.GetFirstHediffOfDef(OrdealDefOf.LCOrdeal_AmberDuskSlowness);
                if (slow != null && context.CurrentTick >= context.GetLong(AmberSlowUntilKey(worm.thingIDNumber), int.MaxValue)) worm.health.RemoveHediff(slow);
            }
            if (context.CurrentTick < context.GetLong(NextSpecialTickKey, int.MaxValue)) return;
            List<Pawn> victims = OrdealTargetUtility.AllTargets(map);
            foreach (Pawn parent in targets.Where(item => item.kindDef == OrdealDefOf.LCOrdeal_AmberDusk).ToList())
            {
                List<Pawn> children = targets.Where(item => item.kindDef == OrdealDefOf.LCOrdeal_AmberDawn && context.GetLong(AmberOwnerKey(item.thingIDNumber), -1L) == parent.thingIDNumber).ToList();
                int spawnCount = Math.Min(settings.amberDawnSpawnPerCycle, Math.Max(0, settings.amberDawnCapPerParent - children.Count));
                for (int i = 0; i < spawnCount; i++)
                {
                    Pawn child = SpawnNear(map, parent.Position, OrdealDefOf.LCOrdeal_AmberDawn, 4); if (child == null) continue;
                    context.SetLong(AmberOwnerKey(child.thingIDNumber), parent.thingIDNumber); children.Add(child); AppendTargetIds(context, new[] { child.thingIDNumber });
                }
                if (victims.Count > 0)
                {
                    IntVec3 destination = CellFinder.RandomClosewalkCellNear(victims.RandomElement().Position, map, settings.amberBurrowRadius, cell => cell.Standable(map) && !cell.Fogged(map));
                    MoveByBurrow(parent, destination, map);
                    foreach (Pawn child in children.Where(item => item.Spawned).ToList()) MoveByBurrow(child, CellFinder.RandomClosewalkCellNear(destination, map, 4), map);
                    DamageNearby(map, parent, settings.amberEmergenceRadius, settings.amberEmergenceDamage, DamageDefOf.Blunt);
                }
                AssignAssaultLord(map, children.Where(item => item.GetLord() == null));
            }
            context.SetLong(NextSpecialTickKey, context.CurrentTick + settings.amberBurrowIntervalTicks);
        }

        private static void UpdateCrimson(ExaminationContext context, Map map, List<Pawn> targets, DuskOrdealSettings settings)
        {
            if (context.CurrentTick >= context.GetLong(NextSpecialTickKey, int.MaxValue))
            {
                List<Pawn> victims = OrdealTargetUtility.AllTargets(map);
                foreach (Pawn dusk in targets.Where(item => item.kindDef == OrdealDefOf.LCOrdeal_CrimsonDusk).ToList())
                {
                    if (victims.Count == 0) break;
                    IntVec3 destination = CellFinder.RandomClosewalkCellNear(victims.RandomElement().Position, map, settings.crimsonRollArrivalRadius, cell => cell.Standable(map) && !cell.Fogged(map));
                    MoveByBurrow(dusk, destination, map); DamageNearby(map, dusk, settings.crimsonRollDamageRadius, settings.crimsonRollDamage, DamageDefOf.Blunt);
                }
                context.SetLong(NextSpecialTickKey, context.CurrentTick + settings.crimsonRollIntervalTicks);
            }
            if (context.CurrentTick >= context.GetLong(NextSecondaryTickKey, int.MaxValue))
            {
                RetargetCrimsonSaboteurs(map, targets.Where(item => item.kindDef == OrdealDefOf.LCOrdeal_CrimsonDawn));
                context.SetLong(NextSecondaryTickKey, context.CurrentTick + settings.crimsonDawnRetargetIntervalTicks);
            }
        }

        private static void UpdateGreen(ExaminationContext context, Map map, List<Pawn> targets, DuskOrdealSettings settings)
        {
            List<Pawn> factories = targets.Where(item => item.kindDef == OrdealDefOf.LCOrdeal_GreenDusk).ToList();
            foreach (Pawn factory in factories) { factory.pather.StopDead(); if (factory.CurJob != null) factory.jobs.StopAll(); }
            List<Pawn> products = targets.Where(item => item.kindDef == OrdealDefOf.LCOrdeal_GreenDawn || item.kindDef == OrdealDefOf.LCOrdeal_GreenNoon).ToList();
            foreach (Pawn factory in factories)
            {
                string nextKey = GreenFactoryNextKey(factory.thingIDNumber); long next = context.GetLong(nextKey, -1L);
                if (next < 0) { context.SetLong(nextKey, context.CurrentTick + settings.greenFactoryInitialDelayTicks); continue; }
                if (context.CurrentTick < next || products.Count >= settings.greenSpawnedUnitCap) continue;
                int count = Math.Min(settings.greenSpawnPerCycle, settings.greenSpawnedUnitCap - products.Count); List<Pawn> spawned = new List<Pawn>();
                for (int i = 0; i < count; i++)
                {
                    PawnKindDef kind = Rand.Chance(settings.greenNoonChance) ? OrdealDefOf.LCOrdeal_GreenNoon : OrdealDefOf.LCOrdeal_GreenDawn;
                    Pawn product = SpawnNear(map, factory.Position, kind, settings.greenSpawnRadius); if (product != null) { spawned.Add(product); products.Add(product); }
                }
                AppendTargetIds(context, spawned.Select(item => item.thingIDNumber)); AssignAssaultLord(map, spawned);
                context.SetLong(nextKey, context.CurrentTick + settings.greenFactoryProductionIntervalTicks.RandomInRange);
            }
            UpdateGreenNoons(context, map, products.Where(item => item.kindDef == OrdealDefOf.LCOrdeal_GreenNoon), settings);
        }

        private static void UpdateGreenNoons(ExaminationContext context, Map map, IEnumerable<Pawn> noons, DuskOrdealSettings settings)
        {
            List<Pawn> list = noons.ToList();
            foreach (Pawn pawn in list)
            {
                string nextKey = GreenNoonNextShutdownKey(pawn.thingIDNumber); string untilKey = GreenNoonShutdownUntilKey(pawn.thingIDNumber); long next = context.GetLong(nextKey, -1L);
                if (next < 0) { context.SetLong(nextKey, context.CurrentTick + settings.greenNoonShutdownIntervalTicks.RandomInRange); continue; }
                if (context.CurrentTick >= next)
                {
                    int duration = settings.greenNoonShutdownDurationTicks.RandomInRange; context.SetLong(untilKey, context.CurrentTick + duration); context.SetLong(nextKey, context.CurrentTick + duration + settings.greenNoonShutdownIntervalTicks.RandomInRange); pawn.stances.stunner.StunFor(duration, pawn, false);
                }
            }
            if (context.CurrentTick < context.GetLong(NextSecondaryTickKey, int.MaxValue)) return;
            foreach (Pawn machine in list.Where(item => !item.stances.stunner.Stunned))
                foreach (Pawn victim in OrdealTargetUtility.AllTargets(map).Where(item => item.Position.InHorDistOf(machine.Position, settings.greenNoonSawRadius)))
                    victim.TakeDamage(new DamageInfo(DamageDefOf.Cut, settings.greenNoonSawDamage, 0f, instigator: machine));
            context.SetLong(NextSecondaryTickKey, context.CurrentTick + settings.greenNoonSawIntervalTicks);
        }

        private static void RetargetCrimsonSaboteurs(Map map, IEnumerable<Pawn> targets)
        {
            List<Building> priorities = map.listerThings.AllThings.OfType<Building>().Where(building => building.Faction == Faction.OfPlayer && (building.def.defName == "LC_HoldingPlatform" || building.TryGetComp<CompPowerTrader>() != null)).ToList();
            if (priorities.Count == 0) return;
            foreach (Pawn pawn in targets)
            {
                Building target = priorities.MinBy(item => item.Position.DistanceToSquared(pawn.Position));
                if (target != null && pawn.CanReach(target, PathEndMode.Touch, Danger.Deadly)) pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.AttackMelee, target));
            }
        }

        private static void DamageNearby(Map map, Thing instigator, float radius, float damage, DamageDef damageDef)
        {
            foreach (Pawn pawn in OrdealTargetUtility.AllTargets(map).Where(item => item.Position.InHorDistOf(instigator.PositionHeld, radius))) pawn.TakeDamage(new DamageInfo(damageDef, damage, 0f, instigator: instigator));
        }

        private static void MoveByBurrow(Pawn pawn, IntVec3 destination, Map map)
        {
            if (pawn == null || !pawn.Spawned || !destination.IsValid || destination == pawn.Position) return; pawn.DeSpawn(); GenSpawn.Spawn(pawn, destination, map);
        }

        private static Pawn SpawnNear(Map map, IntVec3 center, PawnKindDef kind, int radius) => SpawnAt(map, CellFinder.RandomClosewalkCellNear(center, map, radius, cell => cell.Standable(map) && !cell.Fogged(map)), kind);
        private static Pawn SpawnAt(Map map, IntVec3 cell, PawnKindDef kind) { if (!cell.IsValid) return null; Pawn pawn = PawnGenerator.GeneratePawn(kind, Faction.OfMechanoids); GenSpawn.Spawn(pawn, cell, map); return pawn; }
        private static void AssignAssaultLord(Map map, IEnumerable<Pawn> pawns) { List<Pawn> list = pawns.Where(item => item != null && item.Spawned).ToList(); if (list.Count > 0) LordMaker.MakeNewLord(Faction.OfMechanoids, new LordJob_AssaultColony(Faction.OfMechanoids, false, false, false, false, false), map, list); }

        private static int InitialSpecialInterval(DuskVariant variant, DuskOrdealSettings settings) => variant == DuskVariant.Amber ? settings.amberBurrowIntervalTicks : variant == DuskVariant.Crimson ? settings.crimsonRollIntervalTicks : settings.greenFactoryInitialDelayTicks;
        private static int InitialSecondaryInterval(DuskVariant variant, DuskOrdealSettings settings) => variant == DuskVariant.Crimson ? settings.crimsonDawnRetargetIntervalTicks : variant == DuskVariant.Green ? settings.greenNoonSawIntervalTicks : int.MaxValue / 2;
        private static Map SelectMap() => Find.CurrentMap != null && Find.CurrentMap.IsPlayerHome && Find.CurrentMap.mapPawns.AnyFreeColonistSpawned ? Find.CurrentMap : Find.Maps.FirstOrDefault(map => map.IsPlayerHome && map.mapPawns.AnyFreeColonistSpawned);
        private static Map FindMap(ExaminationContext context) { int id = (int)context.GetLong(MapIdKey, -1L); return Find.Maps.FirstOrDefault(map => map.uniqueID == id); }
        private static DuskVariant ReadVariant(ExaminationContext context) { DuskVariant variant; return Enum.TryParse(context.GetString(VariantKey), out variant) ? variant : DuskVariant.Amber; }
        private static string AmberOwnerKey(int id) => "LCAnomalyOrdeals.dusk.amber." + id + ".owner";
        private static string AmberSlowUntilKey(int id) => "LCAnomalyOrdeals.dusk.amber." + id + ".slowUntil";
        private static string GreenFactoryNextKey(int id) => "LCAnomalyOrdeals.dusk.green." + id + ".nextProduction";
        private static string GreenNoonNextShutdownKey(int id) => "LCAnomalyOrdeals.dusk.green." + id + ".nextShutdown";
        private static string GreenNoonShutdownUntilKey(int id) => "LCAnomalyOrdeals.dusk.green." + id + ".shutdownUntil";
        private static List<int> TargetIds(ExaminationContext context) { List<int> result = new List<int>(); foreach (string part in context.GetString(TargetIdsKey, string.Empty).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)) { int id; if (int.TryParse(part, out id)) result.Add(id); } return result; }
        private static void SaveTargetIds(ExaminationContext context, IEnumerable<int> ids) => context.SetString(TargetIdsKey, string.Join(",", ids.Distinct()));
        private static void AppendTargetIds(ExaminationContext context, IEnumerable<int> ids) => SaveTargetIds(context, TargetIds(context).Concat(ids));
        private static List<Pawn> LivingTargets(ExaminationContext context, Map map) { HashSet<int> ids = new HashSet<int>(TargetIds(context)); return map.mapPawns.AllPawnsSpawned.Where(pawn => ids.Contains(pawn.thingIDNumber) && !pawn.Dead && !pawn.Destroyed).ToList(); }

        private static void SpawnReward(Map map, int count)
        {
            ThingDef rewardDef = DefDatabase<ThingDef>.GetNamedSilentFail("EnkephalinBox"); Pawn recipient = map?.mapPawns.FreeColonistsSpawned.FirstOrDefault(); if (rewardDef == null || recipient == null || count <= 0) return;
            Thing reward = ThingMaker.MakeThing(rewardDef); reward.stackCount = Math.Min(count, rewardDef.stackLimit); GenPlace.TryPlaceThing(reward, recipient.Position, map, ThingPlaceMode.Near);
        }

        private static void Cleanup(ExaminationContext context)
        {
            HashSet<int> ids = new HashSet<int>(TargetIds(context)); foreach (Map map in Find.Maps) foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned.Where(item => ids.Contains(item.thingIDNumber)).ToList()) if (!pawn.Destroyed) pawn.Destroy(DestroyMode.Vanish);
        }
    }
}
