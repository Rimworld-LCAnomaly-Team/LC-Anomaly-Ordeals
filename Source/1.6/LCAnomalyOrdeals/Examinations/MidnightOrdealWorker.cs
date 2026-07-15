using System;
using System.Collections.Generic;
using System.Linq;
using LCAnomalyOrdeals.DefOfs;
using LCAnomalyOrdeals.Defs;
using LCAnomalyOrdeals.Presentation;
using LCAnomalyStory;
using LCAnomalyStory.Defs;
using LCAnomalyStory.Examinations;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;
using LCDamageDefOf = LCAnomalyCore.Defs.DamageDefOf;

namespace LCAnomalyOrdeals.Examinations
{
    public sealed class MidnightOrdealWorker : CompanyExaminationWorker
    {
        private const string Prefix = "LCAnomalyOrdeals.midnight.";
        private const string VariantKey = Prefix + "variant";
        private const string MapIdKey = Prefix + "mapId";
        private const string TargetIdsKey = Prefix + "targetIds";
        private const string GreenAngleKey = Prefix + "green.angleMilli";
        private const string GreenActiveAtKey = Prefix + "green.activeAt";
        private const string GreenCooldownUntilKey = Prefix + "green.cooldownUntil";
        private const string GreenSlowUntilKey = Prefix + "green.slowUntil";
        private const string GreenLastUpdateTickKey = Prefix + "green.lastUpdate";
        private const string GreenNextBeamTickKey = Prefix + "green.nextBeam";
        private enum MidnightVariant { Amber, Green, Violet }
        private enum VioletColor { Red, White, Black, Pale }

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
            MidnightVariant variant = (MidnightVariant)Rand.RangeInclusive(0, 2);
            context.SetLong(MapIdKey, map.uniqueID); context.SetString(VariantKey, variant.ToString()); context.SetString(TargetIdsKey, string.Empty);
            List<Pawn> spawned = variant == MidnightVariant.Amber ? SpawnAmber(context, map) : variant == MidnightVariant.Green ? SpawnGreen(context, map) : SpawnViolet(context, map);
            if (spawned.Count == 0) { context.Fail("LCOrdeal_DawnSpawnFailed".Translate()); return; }
            SaveTargetIds(context, spawned.Select(item => item.thingIDNumber));
            GameComponent_MidnightPresentation.ShowStart(variant.ToString());
        }

        public override void OnLoaded(ExaminationContext context) { base.OnLoaded(context); if (FindMap(context) == null) context.Fail("LCOrdeal_DawnNoMap".Translate()); }
        public override void Tick(ExaminationContext context)
        {
            base.Tick(context); Map map = FindMap(context);
            if (map == null) { context.Fail("LCOrdeal_DawnNoMap".Translate()); return; }
            List<Pawn> targets = LivingTargets(context, map); SaveTargetIds(context, targets.Select(item => item.thingIDNumber));
            if (targets.Count == 0) { context.Pass(); return; }
            MidnightVariant variant = ReadVariant(context);
            if (variant == MidnightVariant.Amber) UpdateAmber(context, map, targets);
            else if (variant == MidnightVariant.Green) UpdateGreen(context, map, targets);
            else UpdateViolet(context, map, targets);
        }

        public override ExaminationOutcome Evaluate(ExaminationContext context) { Map map = FindMap(context); return map != null && LivingTargets(context, map).Count == 0 ? ExaminationOutcome.Passed : base.Evaluate(context); }
        public override void OnPassed(ExaminationContext context)
        {
            MidnightVariant variant = ReadVariant(context); Map map = FindMap(context); MidnightOrdealSettings settings = Settings;
            SpawnReward(map, settings.rewardBase + (map == null ? 0 : settings.rewardPerColonist * map.mapPawns.FreeColonistsSpawnedCount)); Cleanup(context); GameComponent_MidnightPresentation.ShowEnd(variant.ToString(), true); base.OnPassed(context);
        }
        public override void OnFailed(ExaminationContext context) { MidnightVariant variant = ReadVariant(context); Cleanup(context); GameComponent_MidnightPresentation.ShowEnd(variant.ToString(), false); base.OnFailed(context); }
        public override void OnCancelled(ExaminationContext context) { Cleanup(context); base.OnCancelled(context); }
        public override IEnumerable<string> ProgressLines(ExaminationContext context)
        {
            Map map = FindMap(context); yield return "LCOrdeal_MidnightVariant".Translate() + ": " + ("LCOrdeal_Midnight" + ReadVariant(context) + "Name").Translate(); yield return "LCOrdeal_DawnTargets".Translate() + ": " + (map == null ? 0 : LivingTargets(context, map).Count);
        }

        public static MidnightOrdealSettings ActiveSettings()
        {
            CompanyExaminationDef def = DefDatabase<CompanyExaminationDef>.GetNamedSilentFail("LCOrdeal_Midnight"); return def?.GetModExtension<MidnightOrdealSettings>() ?? new MidnightOrdealSettings();
        }

        public static void NotifyGreenTowerDamaged(Pawn tower, float damage)
        {
            if (tower?.kindDef != OrdealDefOf.LCOrdeal_GreenMidnight || damage <= 0f || StoryComponents.Development == null) return;
            ExaminationContext context; if (!TryActiveContext(out context, MidnightVariant.Green)) return;
            string accumulatedKey = Prefix + "green.damage"; float accumulated = context.GetLong(accumulatedKey, 0L) / 1000f + damage; MidnightOrdealSettings settings = ActiveSettings();
            if (accumulated >= settings.greenDamageSlowThreshold)
            {
                accumulated %= settings.greenDamageSlowThreshold; long current = Math.Max(context.CurrentTick, context.GetLong(GreenSlowUntilKey, context.CurrentTick)); long cap = context.CurrentTick + settings.greenMaximumRotationSlowTicks; context.SetLong(GreenSlowUntilKey, Math.Min(cap, current + settings.greenRotationSlowDurationTicks));
            }
            context.SetLong(accumulatedKey, Mathf.RoundToInt(accumulated * 1000f));
        }

        public static bool TryAbsorbVioletDamage(Pawn shrine, DamageInfo info)
        {
            VioletColor color; if (shrine == null || info.Amount <= 0f || !TryVioletColor(shrine.kindDef, out color) || !Matches(color, info.Def) || StoryComponents.Development == null) return false;
            ExaminationContext context; if (!TryActiveContext(out context, MidnightVariant.Violet)) return false;
            float remaining = info.Amount;
            foreach (Hediff_Injury injury in shrine.health.hediffSet.hediffs.OfType<Hediff_Injury>().OrderByDescending(item => item.Severity).ToList())
            {
                float healed = Math.Min(remaining, injury.Severity); injury.Severity -= healed; remaining -= healed; if (remaining <= 0f) break;
            }
            return true;
        }

        public static void NotifyVioletShrineDamaged(Pawn shrine, float damage)
        {
            VioletColor color; if (shrine == null || damage <= 0f || !TryVioletColor(shrine.kindDef, out color) || StoryComponents.Development == null) return;
            ExaminationContext context; if (TryActiveContext(out context, MidnightVariant.Violet)) CheckVioletDefense(context, shrine, color);
        }

        private MidnightOrdealSettings Settings => Def.GetModExtension<MidnightOrdealSettings>() ?? new MidnightOrdealSettings();
        private static bool TryActiveContext(out ExaminationContext context, MidnightVariant variant) { context = null; return StoryComponents.Development.TryGetActiveExaminationContext(out context) && context.Definition.defName == "LCOrdeal_Midnight" && ReadVariant(context) == variant; }

        private List<Pawn> SpawnAmber(ExaminationContext context, Map map)
        {
            List<Pawn> result = new List<Pawn>();
            for (int i = 0; i < Settings.amberMidnightCount; i++) { Pawn pawn = SpawnAtEntry(map, OrdealDefOf.LCOrdeal_AmberMidnight); if (pawn == null) continue; result.Add(pawn); context.SetLong(AmberNextKey(pawn.thingIDNumber), context.CurrentTick + Settings.amberInitialSpawnDelayTicks); DamageNearby(map, pawn, Settings.amberEmergenceRadius, Settings.amberEmergenceDamage, LCDamageDefOf.LC_RedDamage); }
            return result;
        }

        private List<Pawn> SpawnGreen(ExaminationContext context, Map map)
        {
            List<Pawn> result = new List<Pawn>();
            for (int i = 0; i < Settings.greenTowerCount; i++) { Pawn pawn = SpawnAtEntry(map, OrdealDefOf.LCOrdeal_GreenMidnight); if (pawn != null) { MakeImmobile(pawn); result.Add(pawn); } }
            context.SetLong(GreenActiveAtKey, context.CurrentTick + Settings.greenActivationDelayTicks); context.SetLong(GreenAngleKey, 0L); context.SetLong(GreenLastUpdateTickKey, context.CurrentTick); context.SetLong(GreenNextBeamTickKey, context.CurrentTick + Settings.greenActivationDelayTicks); return result;
        }

        private List<Pawn> SpawnViolet(ExaminationContext context, Map map)
        {
            List<Pawn> result = new List<Pawn>();
            foreach (VioletColor color in Enum.GetValues(typeof(VioletColor))) for (int i = 0; i < Settings.violetShrinesPerColor; i++)
            {
                Pawn pawn = SpawnAtEntry(map, VioletKind(color)); if (pawn == null) continue; MakeImmobile(pawn); result.Add(pawn); context.SetLong(VioletNextKey(pawn.thingIDNumber), context.CurrentTick + Settings.violetInitialAttackDelayTicks.RandomInRange);
            }
            return result;
        }

        private static void UpdateAmber(ExaminationContext context, Map map, List<Pawn> targets)
        {
            MidnightOrdealSettings settings = ActiveSettings(); List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
            foreach (Pawn parent in targets.Where(item => item.kindDef == OrdealDefOf.LCOrdeal_AmberMidnight).ToList())
            {
                if (context.CurrentTick < context.GetLong(AmberNextKey(parent.thingIDNumber), int.MaxValue)) continue;
                int spawnedTotal = (int)context.GetLong(AmberSpawnedKey(parent.thingIDNumber), 0L); int count = Math.Min(settings.amberDuskSpawnPerCycle, settings.amberDuskLifetimeCapPerParent - spawnedTotal); List<Pawn> spawned = new List<Pawn>();
                for (int i = 0; i < count; i++) { Pawn child = SpawnNear(map, parent.Position, OrdealDefOf.LCOrdeal_AmberDusk, settings.amberSpawnRadius); if (child == null) continue; spawned.Add(child); context.SetLong(AmberOwnerKey(child.thingIDNumber), parent.thingIDNumber); context.SetLong(AmberDuskNextKey(child.thingIDNumber), context.CurrentTick + settings.amberDuskActionIntervalTicks); }
                spawnedTotal += spawned.Count; context.SetLong(AmberSpawnedKey(parent.thingIDNumber), spawnedTotal); AppendTargetIds(context, spawned.Select(item => item.thingIDNumber)); AssignAssaultLord(map, spawned);
                if (colonists.Count > 0) { IntVec3 destination = CellFinder.RandomClosewalkCellNear(colonists.RandomElement().Position, map, settings.amberBurrowArrivalRadius, cell => cell.Standable(map) && !cell.Fogged(map)); MoveByBurrow(parent, destination, map); DamageNearby(map, parent, settings.amberEmergenceRadius, settings.amberEmergenceDamage, LCDamageDefOf.LC_RedDamage); }
                context.SetLong(AmberNextKey(parent.thingIDNumber), context.CurrentTick + settings.amberSpawnIntervalTicks.RandomInRange);
            }
            foreach (Pawn dusk in targets.Where(item => item.kindDef == OrdealDefOf.LCOrdeal_AmberDusk).ToList())
            {
                if (context.CurrentTick < context.GetLong(AmberDuskNextKey(dusk.thingIDNumber), int.MaxValue)) continue;
                List<Pawn> livingChildren = targets.Where(item => item.kindDef == OrdealDefOf.LCOrdeal_AmberDawn && context.GetLong(AmberOwnerKey(item.thingIDNumber), -1L) == dusk.thingIDNumber).ToList(); int count = Math.Min(settings.amberDawnSpawnPerCycle, settings.amberDawnCapPerParent - livingChildren.Count); List<Pawn> spawned = new List<Pawn>();
                for (int i = 0; i < count; i++) { Pawn child = SpawnNear(map, dusk.Position, OrdealDefOf.LCOrdeal_AmberDawn, settings.amberSpawnRadius); if (child == null) continue; spawned.Add(child); context.SetLong(AmberOwnerKey(child.thingIDNumber), dusk.thingIDNumber); }
                AppendTargetIds(context, spawned.Select(item => item.thingIDNumber)); AssignAssaultLord(map, spawned); context.SetLong(AmberDuskNextKey(dusk.thingIDNumber), context.CurrentTick + settings.amberDuskActionIntervalTicks);
            }
        }

        private static void UpdateGreen(ExaminationContext context, Map map, List<Pawn> targets)
        {
            MidnightOrdealSettings settings = ActiveSettings(); List<Pawn> towers = targets.Where(item => item.kindDef == OrdealDefOf.LCOrdeal_GreenMidnight).ToList(); if (towers.Count == 0) return; foreach (Pawn tower in towers) MakeImmobile(tower);
            if (context.CurrentTick < context.GetLong(GreenActiveAtKey, int.MaxValue) || context.CurrentTick < context.GetLong(GreenCooldownUntilKey, 0L)) { context.SetLong(GreenLastUpdateTickKey, context.CurrentTick); return; }
            long lastTick = context.GetLong(GreenLastUpdateTickKey, context.CurrentTick); int elapsedTicks = Math.Max(1, context.CurrentTick - (int)lastTick); context.SetLong(GreenLastUpdateTickKey, context.CurrentTick);
            float angle = context.GetLong(GreenAngleKey, 0L) / 1000f; float factor = context.CurrentTick < context.GetLong(GreenSlowUntilKey, 0L) ? settings.greenRotationSlowFactor : 1f; angle += 360f * elapsedTicks * factor / settings.greenRotationDurationTicks;
            if (angle >= 360f) { angle %= 360f; context.SetLong(GreenCooldownUntilKey, context.CurrentTick + settings.greenCooldownTicks); }
            context.SetLong(GreenAngleKey, Mathf.RoundToInt(angle * 1000f));
            if (context.CurrentTick < context.GetLong(GreenNextBeamTickKey, 0L)) return; context.SetLong(GreenNextBeamTickKey, context.CurrentTick + settings.greenBeamTickIntervalTicks);
            foreach (Pawn tower in towers) { ApplyBeam(map, tower, settings.greenFixedBeamAngleDegrees, settings); ApplyBeam(map, tower, angle, settings); }
        }

        private static void ApplyBeam(Map map, Pawn tower, float angleDegrees, MidnightOrdealSettings settings)
        {
            Vector2 direction = new Vector2(Mathf.Cos(angleDegrees * Mathf.Deg2Rad), Mathf.Sin(angleDegrees * Mathf.Deg2Rad)); Vector2 origin = new Vector2(tower.Position.x, tower.Position.z);
            foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned.Where(item => !item.Dead).ToList())
            {
                Vector2 offset = new Vector2(pawn.Position.x, pawn.Position.z) - origin; float along = Vector2.Dot(offset, direction); float perpendicular = Mathf.Abs(offset.x * direction.y - offset.y * direction.x);
                if (along < 0f || perpendicular > settings.greenBeamWidthCells) continue; pawn.TakeDamage(new DamageInfo(LCDamageDefOf.LC_BlackDamage, settings.greenBeamDamage, 0f, instigator: tower)); AddTimedSlow(pawn, settings.greenBeamSlowDurationTicks);
            }
            for (int distance = settings.greenBeamVisualSpacingCells; distance < Math.Max(map.Size.x, map.Size.z); distance += settings.greenBeamVisualSpacingCells) { IntVec3 cell = new IntVec3(Mathf.RoundToInt(origin.x + direction.x * distance), 0, Mathf.RoundToInt(origin.y + direction.y * distance)); if (!cell.InBounds(map)) break; FleckMaker.ThrowLightningGlow(cell.ToVector3Shifted(), map, 1.2f); }
        }

        private static void UpdateViolet(ExaminationContext context, Map map, List<Pawn> targets)
        {
            foreach (Pawn shrine in targets.Where(item => IsVioletKind(item.kindDef)).ToList())
            {
                MakeImmobile(shrine); VioletColor color; if (!TryVioletColor(shrine.kindDef, out color)) continue; long resolve = context.GetLong(VioletResolveKey(shrine.thingIDNumber), -1L);
                if (resolve >= 0)
                {
                    IntVec3 target = DecodeCell(context.GetLong(VioletTargetKey(shrine.thingIDNumber), 0L)); if (context.CurrentTick >= resolve) { ResolveVioletAttack(map, shrine, color, target); context.SetLong(VioletResolveKey(shrine.thingIDNumber), -1L); int queued = (int)context.GetLong(VioletQueueKey(shrine.thingIDNumber), 0L); if (queued > 0) { context.SetLong(VioletQueueKey(shrine.thingIDNumber), queued - 1); BeginVioletAttack(context, map, shrine, color); context.SetLong(VioletResolveKey(shrine.thingIDNumber), context.CurrentTick + Math.Max(1, ActiveSettings().violetTelegraphTicks / 2)); } else context.SetLong(VioletNextKey(shrine.thingIDNumber), context.CurrentTick + ActiveSettings().violetAttackIntervalTicks.RandomInRange); }
                    else if (context.CurrentTick % 60 == 0 && target.InBounds(map)) FleckMaker.ThrowLightningGlow(target.ToVector3Shifted(), map, 2f);
                    continue;
                }
                if (context.CurrentTick >= context.GetLong(VioletNextKey(shrine.thingIDNumber), int.MaxValue)) BeginVioletAttack(context, map, shrine, color);
            }
        }

        private static void BeginVioletAttack(ExaminationContext context, Map map, Pawn shrine, VioletColor color)
        {
            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned; if (colonists.Count == 0) return; IntVec3 target = colonists.RandomElement().Position; context.SetLong(VioletTargetKey(shrine.thingIDNumber), EncodeCell(target)); context.SetLong(VioletResolveKey(shrine.thingIDNumber), context.CurrentTick + ActiveSettings().violetTelegraphTicks);
        }

        private static void ResolveVioletAttack(Map map, Pawn shrine, VioletColor color, IntVec3 target)
        {
            MidnightOrdealSettings settings = ActiveSettings(); DamageDef damageDef = DamageFor(color); float damage = color == VioletColor.Red ? settings.violetRedDamage : color == VioletColor.White ? settings.violetWhiteDamage : color == VioletColor.Black ? settings.violetBlackDamage : settings.violetPaleDamage;
            if (color == VioletColor.Black)
            {
                Vector2 origin = new Vector2(shrine.Position.x, shrine.Position.z); Vector2 direction = (new Vector2(target.x, target.z) - origin).normalized; foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned.Where(item => !item.Dead).ToList()) { Vector2 offset = new Vector2(pawn.Position.x, pawn.Position.z) - origin; if (Vector2.Dot(offset, direction) >= 0f && Mathf.Abs(offset.x * direction.y - offset.y * direction.x) <= settings.violetBlackLineWidth) pawn.TakeDamage(new DamageInfo(damageDef, damage, 0f, instigator: shrine)); }
            }
            else
            {
                float radius = color == VioletColor.Red ? settings.violetRedRadius : color == VioletColor.White ? settings.violetWhiteRadius : settings.violetPaleRadius; foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned.Where(item => !item.Dead && item.Position.InHorDistOf(target, radius)).ToList()) pawn.TakeDamage(new DamageInfo(damageDef, damage, 0f, instigator: shrine));
            }
            if (target.InBounds(map)) FleckMaker.ThrowLightningGlow(target.ToVector3Shifted(), map, color == VioletColor.White ? settings.violetWhiteRadius : 3f);
        }

        private static void CheckVioletDefense(ExaminationContext context, Pawn shrine, VioletColor color)
        {
            if (!shrine.Spawned || shrine.Map == null) return;
            float fraction = shrine.health.summaryHealth.SummaryHealthPercent; MidnightOrdealSettings settings = ActiveSettings(); int stage = (int)context.GetLong(VioletStageKey(shrine.thingIDNumber), 0L); int reached = fraction <= settings.violetDefenseThresholdThree ? 3 : fraction <= settings.violetDefenseThresholdTwo ? 2 : fraction <= settings.violetDefenseThresholdOne ? 1 : 0;
            if (reached <= stage) return; context.SetLong(VioletStageKey(shrine.thingIDNumber), reached); int extra = settings.violetDefenseExtraAttacks * (reached - stage); if (extra <= 0) return; long pending = context.GetLong(VioletResolveKey(shrine.thingIDNumber), -1L); if (pending >= 0) context.SetLong(VioletQueueKey(shrine.thingIDNumber), context.GetLong(VioletQueueKey(shrine.thingIDNumber), 0L) + extra); else { BeginVioletAttack(context, shrine.Map, shrine, color); context.SetLong(VioletResolveKey(shrine.thingIDNumber), context.CurrentTick + Math.Max(1, settings.violetTelegraphTicks / 2)); context.SetLong(VioletQueueKey(shrine.thingIDNumber), extra - 1); }
        }

        private static void AddTimedSlow(Pawn pawn, int duration)
        {
            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(OrdealDefOf.LCOrdeal_GreenMidnightBeamSlowness); if (hediff == null) { hediff = HediffMaker.MakeHediff(OrdealDefOf.LCOrdeal_GreenMidnightBeamSlowness, pawn); pawn.health.AddHediff(hediff); }
            HediffComp_Disappears disappears = hediff.TryGetComp<HediffComp_Disappears>(); if (disappears != null) disappears.ticksToDisappear = Math.Max(disappears.ticksToDisappear, duration);
        }

        private static bool Matches(VioletColor color, DamageDef damage) => damage == DamageFor(color);
        private static DamageDef DamageFor(VioletColor color) => color == VioletColor.Red ? LCDamageDefOf.LC_RedDamage : color == VioletColor.White ? LCDamageDefOf.LC_WhiteDamage : color == VioletColor.Black ? LCDamageDefOf.LC_BlackDamage : LCDamageDefOf.LC_PaleDamage;
        private static PawnKindDef VioletKind(VioletColor color) => color == VioletColor.Red ? OrdealDefOf.LCOrdeal_VioletMidnightRed : color == VioletColor.White ? OrdealDefOf.LCOrdeal_VioletMidnightWhite : color == VioletColor.Black ? OrdealDefOf.LCOrdeal_VioletMidnightBlack : OrdealDefOf.LCOrdeal_VioletMidnightPale;
        private static bool TryVioletColor(PawnKindDef kind, out VioletColor color) { foreach (VioletColor candidate in Enum.GetValues(typeof(VioletColor))) if (kind == VioletKind(candidate)) { color = candidate; return true; } color = VioletColor.Red; return false; }
        private static bool IsVioletKind(PawnKindDef kind) { VioletColor ignored; return TryVioletColor(kind, out ignored); }
        private static void MakeImmobile(Pawn pawn) { if (pawn.health.hediffSet.GetFirstHediffOfDef(OrdealDefOf.LCOrdeal_VioletNoonImmobile) == null) pawn.health.AddHediff(OrdealDefOf.LCOrdeal_VioletNoonImmobile); pawn.pather.StopDead(); if (pawn.CurJob != null) pawn.jobs.StopAll(); }
        private static void DamageNearby(Map map, Thing instigator, float radius, float damage, DamageDef def) { foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned.Where(item => !item.Dead && item.Position.InHorDistOf(instigator.PositionHeld, radius)).ToList()) pawn.TakeDamage(new DamageInfo(def, damage, 0f, instigator: instigator)); }
        private static Pawn SpawnAtEntry(Map map, PawnKindDef kind) { IntVec3 cell; return RCellFinder.TryFindRandomPawnEntryCell(out cell, map, CellFinder.EdgeRoadChance_Hostile, true) ? SpawnAt(map, cell, kind) : null; }
        private static Pawn SpawnNear(Map map, IntVec3 center, PawnKindDef kind, int radius) => SpawnAt(map, CellFinder.RandomClosewalkCellNear(center, map, radius, cell => cell.Standable(map) && !cell.Fogged(map)), kind);
        private static Pawn SpawnAt(Map map, IntVec3 cell, PawnKindDef kind) { if (!cell.IsValid) return null; Pawn pawn = PawnGenerator.GeneratePawn(kind, Faction.OfMechanoids); GenSpawn.Spawn(pawn, cell, map); return pawn; }
        private static void MoveByBurrow(Pawn pawn, IntVec3 destination, Map map) { if (pawn == null || !pawn.Spawned || !destination.IsValid || destination == pawn.Position) return; pawn.DeSpawn(); GenSpawn.Spawn(pawn, destination, map); }
        private static void AssignAssaultLord(Map map, IEnumerable<Pawn> pawns) { List<Pawn> list = pawns.Where(item => item != null && item.Spawned).ToList(); if (list.Count > 0) LordMaker.MakeNewLord(Faction.OfMechanoids, new LordJob_AssaultColony(Faction.OfMechanoids, false, false, false, false, false), map, list); }
        private static Map SelectMap() => Find.CurrentMap != null && Find.CurrentMap.IsPlayerHome && Find.CurrentMap.mapPawns.AnyFreeColonistSpawned ? Find.CurrentMap : Find.Maps.FirstOrDefault(map => map.IsPlayerHome && map.mapPawns.AnyFreeColonistSpawned);
        private static Map FindMap(ExaminationContext context) { int id = (int)context.GetLong(MapIdKey, -1L); return Find.Maps.FirstOrDefault(map => map.uniqueID == id); }
        private static MidnightVariant ReadVariant(ExaminationContext context) { MidnightVariant variant; return Enum.TryParse(context.GetString(VariantKey), out variant) ? variant : MidnightVariant.Amber; }
        private static string AmberNextKey(int id) => Prefix + "amber." + id + ".next"; private static string AmberSpawnedKey(int id) => Prefix + "amber." + id + ".spawned"; private static string AmberDuskNextKey(int id) => Prefix + "amber." + id + ".duskNext"; private static string AmberOwnerKey(int id) => Prefix + "amber." + id + ".owner";
        private static string VioletNextKey(int id) => Prefix + "violet." + id + ".next"; private static string VioletResolveKey(int id) => Prefix + "violet." + id + ".resolve"; private static string VioletTargetKey(int id) => Prefix + "violet." + id + ".target"; private static string VioletStageKey(int id) => Prefix + "violet." + id + ".stage"; private static string VioletQueueKey(int id) => Prefix + "violet." + id + ".queue";
        private static long EncodeCell(IntVec3 cell) => ((long)(uint)cell.x << 32) | (uint)cell.z; private static IntVec3 DecodeCell(long value) => new IntVec3((int)(value >> 32), 0, (int)value);
        private static List<int> TargetIds(ExaminationContext context) { List<int> result = new List<int>(); foreach (string part in context.GetString(TargetIdsKey, string.Empty).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)) { int id; if (int.TryParse(part, out id)) result.Add(id); } return result; }
        private static void SaveTargetIds(ExaminationContext context, IEnumerable<int> ids) => context.SetString(TargetIdsKey, string.Join(",", ids.Distinct())); private static void AppendTargetIds(ExaminationContext context, IEnumerable<int> ids) => SaveTargetIds(context, TargetIds(context).Concat(ids));
        private static List<Pawn> LivingTargets(ExaminationContext context, Map map) { HashSet<int> ids = new HashSet<int>(TargetIds(context)); return map.mapPawns.AllPawnsSpawned.Where(pawn => ids.Contains(pawn.thingIDNumber) && !pawn.Dead && !pawn.Destroyed).ToList(); }
        private static void SpawnReward(Map map, int count) { ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail("EnkephalinBox"); Pawn pawn = map?.mapPawns.FreeColonistsSpawned.FirstOrDefault(); if (def == null || pawn == null || count <= 0) return; Thing reward = ThingMaker.MakeThing(def); reward.stackCount = Math.Min(count, def.stackLimit); GenPlace.TryPlaceThing(reward, pawn.Position, map, ThingPlaceMode.Near); }
        private static void Cleanup(ExaminationContext context) { HashSet<int> ids = new HashSet<int>(TargetIds(context)); foreach (Map map in Find.Maps) foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned.Where(item => ids.Contains(item.thingIDNumber)).ToList()) if (!pawn.Destroyed) pawn.Destroy(DestroyMode.Vanish); }
    }
}
