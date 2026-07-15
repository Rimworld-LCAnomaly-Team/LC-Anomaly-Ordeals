using System;
using System.Collections.Generic;
using System.Linq;
using LCAnomalyCore.Buildings;
using LCAnomalyCore.Comp;
using LCAnomalyOrdeals.DefOfs;
using LCAnomalyOrdeals.Defs;
using LCAnomalyOrdeals.Presentation;
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
    public sealed class DawnOrdealWorker : CompanyExaminationWorker
    {
        private const string VariantKey = "LCAnomalyOrdeals.dawn.variant";
        private const string MapIdKey = "LCAnomalyOrdeals.dawn.mapId";
        private const string TargetIdsKey = "LCAnomalyOrdeals.dawn.targetIds";
        private const string NextSpecialTickKey = "LCAnomalyOrdeals.dawn.nextSpecialTick";
        private const string VioletMatureTickKey = "LCAnomalyOrdeals.dawn.violetMatureTick";

        private enum DawnVariant
        {
            Amber,
            Crimson,
            Green,
            Violet
        }

        public override bool CanStart(ExaminationContext context, out string rejectionReason)
        {
            if (!base.CanStart(context, out rejectionReason))
            {
                return false;
            }

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

            DawnVariant variant = (DawnVariant)Rand.RangeInclusive(0, 3);
            DawnOrdealSettings settings = Settings;
            int colonists = Math.Max(1, map.mapPawns.FreeColonistsSpawnedCount);
            int count = TargetCount(variant, colonists, settings);
            PawnKindDef pawnKind = PawnKindFor(variant);
            List<Pawn> spawned = new List<Pawn>();

            context.SetLong(MapIdKey, map.uniqueID);
            context.SetString(VariantKey, variant.ToString());
            context.SetString(TargetIdsKey, string.Empty);
            context.SetLong(NextSpecialTickKey, context.CurrentTick + SpecialInterval(variant, settings));
            if (variant == DawnVariant.Violet)
            {
                context.SetLong(VioletMatureTickKey, context.CurrentTick + settings.violetMatureDelayTicks);
            }

            for (int i = 0; i < count; i++)
            {
                IntVec3 cell;
                if (!RCellFinder.TryFindRandomPawnEntryCell(out cell, map, CellFinder.EdgeRoadChance_Hostile, true))
                {
                    Cleanup(context);
                    context.Fail("LCOrdeal_DawnSpawnFailed".Translate());
                    return;
                }

                Pawn pawn = PawnGenerator.GeneratePawn(pawnKind, Faction.OfMechanoids);
                if (variant == DawnVariant.Violet)
                {
                    pawn.health.AddHediff(OrdealDefOf.LCOrdeal_VioletSlowness);
                }
                GenSpawn.Spawn(pawn, cell, map);
                spawned.Add(pawn);
                SaveTargetIds(context, spawned.Select(target => target.thingIDNumber));
            }

            if (spawned.Count > 0 && variant != DawnVariant.Violet)
            {
                LordMaker.MakeNewLord(
                    Faction.OfMechanoids,
                    new LordJob_AssaultColony(Faction.OfMechanoids, false, false, false, false, false),
                    map,
                    spawned);
            }

            GameComponent_DawnPresentation.ShowStart(variant.ToString());
        }

        public override void OnLoaded(ExaminationContext context)
        {
            base.OnLoaded(context);
            if (FindMap(context) == null)
            {
                context.Fail("LCOrdeal_DawnNoMap".Translate());
            }
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

            DawnVariant variant = ReadVariant(context);
            DawnOrdealSettings settings = Settings;
            List<Pawn> targets = LivingTargets(context, map);
            SaveTargetIds(context, targets.Select(target => target.thingIDNumber));

            if (targets.Count == 0)
            {
                context.Pass();
                return;
            }

            if (variant == DawnVariant.Violet
                && context.CurrentTick >= context.GetLong(VioletMatureTickKey, int.MaxValue))
            {
                int affectedPlatforms = 0;
                foreach (Pawn target in targets.ToList())
                {
                    affectedPlatforms += DetonateViolet(target, settings);
                }
                SaveTargetIds(context, Enumerable.Empty<int>());
                Messages.Message(
                    "LCOrdeal_VioletDetonated".Translate(affectedPlatforms),
                    MessageTypeDefOf.NegativeEvent);
                context.Pass();
                return;
            }

            if (variant == DawnVariant.Violet)
            {
                UpdateVioletBehavior(context, map, targets, settings);
                return;
            }

            if (context.CurrentTick < context.GetLong(NextSpecialTickKey, int.MaxValue))
            {
                return;
            }

            if (variant == DawnVariant.Amber)
            {
                BurrowAmberTargets(map, targets, settings);
            }
            else if (variant == DawnVariant.Crimson)
            {
                RetargetCrimsonSaboteurs(map, targets);
            }

            context.SetLong(NextSpecialTickKey, context.CurrentTick + SpecialInterval(variant, settings));
        }

        public override ExaminationOutcome Evaluate(ExaminationContext context)
        {
            Map map = FindMap(context);
            if (map != null && LivingTargets(context, map).Count == 0)
            {
                return ExaminationOutcome.Passed;
            }
            return base.Evaluate(context);
        }

        public override void OnPassed(ExaminationContext context)
        {
            Map map = FindMap(context);
            DawnOrdealSettings settings = Settings;
            int reward = settings.rewardBase
                + (map == null ? 0 : settings.rewardPerColonist * map.mapPawns.FreeColonistsSpawnedCount);
            Cleanup(context);
            SpawnReward(map, reward);
            GameComponent_DawnPresentation.ShowEnd(ReadVariant(context).ToString(), true);
            base.OnPassed(context);
        }

        public override void OnFailed(ExaminationContext context)
        {
            GameComponent_DawnPresentation.ShowEnd(ReadVariant(context).ToString(), false);
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
            DawnVariant variant = ReadVariant(context);
            Map map = FindMap(context);
            int remaining = map == null ? 0 : LivingTargets(context, map).Count;
            yield return "LCOrdeal_DawnVariant".Translate() + ": " + VariantName(variant);
            yield return "LCOrdeal_DawnTargets".Translate() + ": " + remaining;

            if (variant == DawnVariant.Violet)
            {
                int ticks = Math.Max(0, (int)(context.GetLong(VioletMatureTickKey) - context.CurrentTick));
                yield return "LCOrdeal_VioletTime".Translate() + ": " + ticks.ToStringTicksToPeriod();
            }
        }

        private static Map SelectMap()
        {
            if (Find.CurrentMap != null
                && Find.CurrentMap.IsPlayerHome
                && Find.CurrentMap.mapPawns.AnyFreeColonistSpawned)
            {
                return Find.CurrentMap;
            }
            return Find.Maps.FirstOrDefault(map => map.IsPlayerHome && map.mapPawns.AnyFreeColonistSpawned);
        }

        private static Map FindMap(ExaminationContext context)
        {
            int mapId = (int)context.GetLong(MapIdKey, -1L);
            return Find.Maps.FirstOrDefault(map => map.uniqueID == mapId);
        }

        private static DawnVariant ReadVariant(ExaminationContext context)
        {
            DawnVariant variant;
            return Enum.TryParse(context.GetString(VariantKey), out variant) ? variant : DawnVariant.Amber;
        }

        private static PawnKindDef PawnKindFor(DawnVariant variant)
        {
            switch (variant)
            {
                case DawnVariant.Crimson: return OrdealDefOf.LCOrdeal_CrimsonDawn;
                case DawnVariant.Green: return OrdealDefOf.LCOrdeal_GreenDawn;
                case DawnVariant.Violet: return OrdealDefOf.LCOrdeal_VioletDawn;
                default: return OrdealDefOf.LCOrdeal_AmberDawn;
            }
        }

        private DawnOrdealSettings Settings
        {
            get { return Def.GetModExtension<DawnOrdealSettings>() ?? new DawnOrdealSettings(); }
        }

        public static DawnOrdealSettings ActiveSettings()
        {
            CompanyExaminationDef definition = DefDatabase<CompanyExaminationDef>.GetNamedSilentFail("LCOrdeal_Dawn");
            return definition?.GetModExtension<DawnOrdealSettings>() ?? new DawnOrdealSettings();
        }

        private static int TargetCount(DawnVariant variant, int colonists, DawnOrdealSettings settings)
        {
            switch (variant)
            {
                case DawnVariant.Amber: return Mathf.Clamp(colonists + settings.amberCountOffset, settings.amberCountMin, settings.amberCountMax);
                case DawnVariant.Crimson: return Mathf.Clamp(Mathf.CeilToInt(colonists / settings.crimsonColonistsPerTarget), settings.crimsonCountMin, settings.crimsonCountMax);
                case DawnVariant.Green: return Mathf.Clamp(Mathf.CeilToInt(colonists / settings.greenColonistsPerTarget), settings.greenCountMin, settings.greenCountMax);
                case DawnVariant.Violet: return Mathf.Clamp(Mathf.CeilToInt(colonists / settings.violetColonistsPerTarget), settings.violetCountMin, settings.violetCountMax);
                default: return 1;
            }
        }

        private static int SpecialInterval(DawnVariant variant, DawnOrdealSettings settings)
        {
            return variant == DawnVariant.Amber
                ? settings.amberBurrowIntervalTicks
                : settings.crimsonRetargetIntervalTicks;
        }

        private static string VariantName(DawnVariant variant)
        {
            return ("LCOrdeal_" + variant + "Name").Translate();
        }

        private static string VariantQuote(DawnVariant variant)
        {
            return ("LCOrdeal_" + variant + "Quote").Translate();
        }

        private static List<int> TargetIds(ExaminationContext context)
        {
            string value = context.GetString(TargetIdsKey, string.Empty);
            List<int> result = new List<int>();
            foreach (string part in value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int id;
                if (int.TryParse(part, out id))
                {
                    result.Add(id);
                }
            }
            return result;
        }

        private static void SaveTargetIds(ExaminationContext context, IEnumerable<int> ids)
        {
            context.SetString(TargetIdsKey, string.Join(",", ids.Distinct()));
        }

        private static List<Pawn> LivingTargets(ExaminationContext context, Map map)
        {
            HashSet<int> ids = new HashSet<int>(TargetIds(context));
            return map.mapPawns.AllPawnsSpawned
                .Where(pawn => ids.Contains(pawn.thingIDNumber) && !pawn.Dead && !pawn.Destroyed)
                .ToList();
        }

        private static void BurrowAmberTargets(Map map, IEnumerable<Pawn> targets, DawnOrdealSettings settings)
        {
            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
            if (colonists.Count == 0)
            {
                return;
            }

            foreach (Pawn pawn in targets.ToList())
            {
                Pawn colonist = colonists.RandomElement();
                IntVec3 destination = CellFinder.RandomClosewalkCellNear(
                    colonist.Position,
                    map,
                    settings.amberBurrowRadius,
                    cell => cell.Standable(map) && !cell.Fogged(map));
                if (destination.IsValid && destination != pawn.Position)
                {
                    pawn.DeSpawn();
                    GenSpawn.Spawn(pawn, destination, map);
                }
            }
        }

        private static void RetargetCrimsonSaboteurs(Map map, IEnumerable<Pawn> targets)
        {
            List<Building> priorities = map.listerThings.AllThings
                .OfType<Building>()
                .Where(building => building.Faction == Faction.OfPlayer
                    && (building.def.defName == "LC_HoldingPlatform"
                        || building.TryGetComp<CompPowerTrader>() != null))
                .ToList();
            if (priorities.Count == 0)
            {
                return;
            }

            foreach (Pawn pawn in targets)
            {
                Building target = priorities.MinBy(building => building.Position.DistanceToSquared(pawn.Position));
                if (target != null && pawn.CanReach(target, PathEndMode.Touch, Danger.Deadly))
                {
                    pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.AttackMelee, target));
                }
            }
        }

        private static void PulsePsychic(Map map, Thing instigator, float amount, float radius)
        {
            foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
            {
                if (!pawn.Dead && (radius < 0f || pawn.Position.InHorDistOf(instigator.PositionHeld, radius)))
                {
                    pawn.TakeDamage(new DamageInfo(DamageDefOf.Psychic, amount, 0f, instigator: instigator));
                }
            }
        }

        private static int DetonateViolet(Pawn fruit, DawnOrdealSettings settings)
        {
            if (fruit == null || !fruit.Spawned)
            {
                return 0;
            }

            Map map = fruit.Map;
            IntVec3 center = fruit.Position;
            PulsePsychic(map, fruit, settings.violetPsychicDamage, settings.violetBlastRadius);

            int affectedPlatforms = 0;
            foreach (Building_AbnormalityHoldingPlatform platform in map.listerBuildings
                .AllBuildingsColonistOfClass<Building_AbnormalityHoldingPlatform>())
            {
                if (!platform.Position.InHorDistOf(center, settings.violetBlastRadius))
                {
                    continue;
                }

                CompAbnormality abnormality = platform.HeldPawn?.TryGetComp<CompAbnormality>();
                if (abnormality != null && abnormality.QliphothEnabled)
                {
                    abnormality.QliphothCountCurrent = 0;
                    affectedPlatforms++;
                }
            }

            fruit.Destroy(DestroyMode.Vanish);
            return affectedPlatforms;
        }

        public static void NotifyVioletDamaged(Pawn fruit, Pawn attacker)
        {
            if (fruit == null
                || attacker == null
                || fruit.kindDef != OrdealDefOf.LCOrdeal_VioletDawn
                || StoryComponents.Development == null)
            {
                return;
            }

            ExaminationContext context;
            if (!StoryComponents.Development.TryGetActiveExaminationContext(out context)
                || context.Definition.defName != "LCOrdeal_Dawn"
                || ReadVariant(context) != DawnVariant.Violet)
            {
                return;
            }

            context.SetLong(VioletAttackerKey(fruit.thingIDNumber), attacker.thingIDNumber);
            context.SetLong(
                VioletChaseUntilKey(fruit.thingIDNumber),
                context.CurrentTick + ActiveSettings().violetChaseDurationTicks.RandomInRange);
        }

        private static void UpdateVioletBehavior(
            ExaminationContext context,
            Map map,
            IEnumerable<Pawn> targets,
            DawnOrdealSettings settings)
        {
            foreach (Pawn fruit in targets)
            {
                int attackerId = (int)context.GetLong(VioletAttackerKey(fruit.thingIDNumber), -1L);
                int chaseUntil = (int)context.GetLong(VioletChaseUntilKey(fruit.thingIDNumber), -1L);
                Pawn attacker = map.mapPawns.AllPawnsSpawned
                    .FirstOrDefault(pawn => pawn.thingIDNumber == attackerId && !pawn.Dead && !pawn.Destroyed);

                if (attacker != null
                    && context.CurrentTick < chaseUntil
                    && fruit.CanReach(attacker, PathEndMode.Touch, Danger.Deadly))
                {
                    if (fruit.CurJobDef != JobDefOf.AttackMelee || fruit.CurJob.targetA.Thing != attacker)
                    {
                        fruit.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.AttackMelee, attacker));
                    }
                    continue;
                }

                if (fruit.CurJobDef == JobDefOf.Goto && fruit.CurJob.targetA.Cell.IsValid)
                {
                    continue;
                }

                IntVec3 destination = CellFinder.RandomClosewalkCellNear(
                    fruit.Position,
                    map,
                    settings.violetWanderRadius,
                    cell => cell.Standable(map) && !cell.Fogged(map));
                Job wander = JobMaker.MakeJob(JobDefOf.Goto, destination);
                wander.locomotionUrgency = LocomotionUrgency.Amble;
                wander.expiryInterval = settings.violetWanderJobExpiryTicks;
                fruit.jobs.TryTakeOrderedJob(wander);
            }
        }

        private static string VioletAttackerKey(int thingId)
        {
            return "LCAnomalyOrdeals.dawn.violet." + thingId + ".attackerId";
        }

        private static string VioletChaseUntilKey(int thingId)
        {
            return "LCAnomalyOrdeals.dawn.violet." + thingId + ".chaseUntil";
        }

        private static void SpawnReward(Map map, int count)
        {
            if (map == null || count <= 0)
            {
                return;
            }

            ThingDef rewardDef = DefDatabase<ThingDef>.GetNamedSilentFail("EnkephalinBox");
            Pawn recipient = map.mapPawns.FreeColonistsSpawned.FirstOrDefault();
            if (rewardDef == null || recipient == null)
            {
                return;
            }

            Thing reward = ThingMaker.MakeThing(rewardDef);
            reward.stackCount = Math.Min(count, rewardDef.stackLimit);
            GenPlace.TryPlaceThing(reward, recipient.Position, map, ThingPlaceMode.Near);
        }

        private static void Cleanup(ExaminationContext context)
        {
            HashSet<int> ids = new HashSet<int>(TargetIds(context));
            foreach (Map map in Find.Maps)
            {
                foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned.Where(pawn => ids.Contains(pawn.thingIDNumber)).ToList())
                {
                    if (!pawn.Destroyed)
                    {
                        pawn.Destroy(DestroyMode.Vanish);
                    }
                }
            }
        }
    }
}
