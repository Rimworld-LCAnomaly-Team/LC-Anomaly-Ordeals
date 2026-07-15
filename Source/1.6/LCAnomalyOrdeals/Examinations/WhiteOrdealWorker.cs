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
using Verse.AI.Group;
using LCDamageDefOf = LCAnomalyCore.Defs.DamageDefOf;

namespace LCAnomalyOrdeals.Examinations
{
    public sealed class WhiteOrdealWorker : CompanyExaminationWorker
    {
        private const string Prefix = "LCAnomalyOrdeals.white.";
        private const string MapIdKey = Prefix + "mapId";
        private const string TargetIdsKey = Prefix + "targets";
        private const string DawnColorKey = Prefix + "dawnColor";
        private enum WhiteStage { Dawn, Noon, Dusk, Midnight }
        private enum FixerColor { Red, White, Black, Pale }

        public override bool CanStart(ExaminationContext context, out string rejectionReason)
        {
            if (!base.CanStart(context, out rejectionReason)) return false;
            WhiteStage stage = Stage;
            if (stage != WhiteStage.Dawn)
            {
                CompanyExaminationDef previous = DefDatabase<CompanyExaminationDef>.GetNamedSilentFail(PreviousDefName(stage));
                if (previous == null || !context.Component.IsExaminationPassed(previous)) { rejectionReason = "LCOrdeal_WhitePreviousRequired".Translate(); return false; }
            }
            if (SelectMap() == null) { rejectionReason = "LCOrdeal_DawnNoMap".Translate(); return false; }
            return true;
        }

        public override void OnStarted(ExaminationContext context)
        {
            base.OnStarted(context); Map map = SelectMap(); if (map == null) { context.Fail("LCOrdeal_DawnNoMap".Translate()); return; }
            context.SetLong(MapIdKey, map.uniqueID); context.SetString(TargetIdsKey, string.Empty); List<Pawn> spawned = new List<Pawn>();
            if (Stage == WhiteStage.Dawn)
            {
                FixerColor color = (FixerColor)Rand.RangeInclusive(0, 2); context.SetString(DawnColorKey, color.ToString()); spawned.Add(SpawnAtEntry(map, KindFor(color)));
            }
            else if (Stage == WhiteStage.Noon)
            {
                FixerColor dawn = ReadDawnColor(context); foreach (FixerColor color in new[] { FixerColor.Red, FixerColor.White, FixerColor.Black }.Where(item => item != dawn)) spawned.Add(SpawnAtEntry(map, KindFor(color)));
            }
            else if (Stage == WhiteStage.Dusk) foreach (FixerColor color in Enum.GetValues(typeof(FixerColor))) spawned.Add(SpawnAtEntry(map, KindFor(color)));
            else spawned.Add(SpawnAtEntry(map, OrdealDefOf.LCOrdeal_WhiteClaw));
            spawned.RemoveAll(item => item == null); if (spawned.Count == 0) { context.Fail("LCOrdeal_DawnSpawnFailed".Translate()); return; }
            SaveTargetIds(context, spawned.Select(item => item.thingIDNumber)); AssignAssaultLord(map, spawned);
            foreach (Pawn pawn in spawned) { FixerColor color; context.SetLong(NextKey(pawn.thingIDNumber), context.CurrentTick + (Stage == WhiteStage.Midnight ? Settings.clawInitialSpecialDelayTicks : TryFixerColor(pawn.kindDef, out color) ? InitialDelay(Settings, color) : 1)); context.SetLong(BasicNextKey(pawn.thingIDNumber), context.CurrentTick + Settings.clawBasicAttackIntervalTicks); if (pawn.kindDef == OrdealDefOf.LCOrdeal_WhiteFixerPale) context.SetLong(TeleportKey(pawn.thingIDNumber), context.CurrentTick + Settings.paleTeleportIntervalTicks.RandomInRange); }
            GameComponent_WhitePresentation.ShowStart(Stage.ToString(), Def.defName);
        }

        public override void OnLoaded(ExaminationContext context) { base.OnLoaded(context); if (FindMap(context) == null) context.Fail("LCOrdeal_DawnNoMap".Translate()); }
        public override void Tick(ExaminationContext context)
        {
            base.Tick(context); Map map = FindMap(context); if (map == null) { context.Fail("LCOrdeal_DawnNoMap".Translate()); return; } List<Pawn> targets = LivingTargets(context, map); SaveTargetIds(context, targets.Select(item => item.thingIDNumber)); if (targets.Count == 0) { context.Pass(); return; }
            if (Stage == WhiteStage.Midnight) UpdateClaw(context, map, targets[0]); else foreach (Pawn fixer in targets) UpdateFixer(context, map, fixer);
        }
        public override ExaminationOutcome Evaluate(ExaminationContext context) { Map map = FindMap(context); return map != null && LivingTargets(context, map).Count == 0 ? ExaminationOutcome.Passed : base.Evaluate(context); }
        public override void OnPassed(ExaminationContext context) { WhiteStage stage = Stage; Map map = FindMap(context); SpawnReward(map, Settings.rewardBase + (map == null ? 0 : Settings.rewardPerColonist * map.mapPawns.FreeColonistsSpawnedCount)); Cleanup(context); GameComponent_WhitePresentation.ShowEnd(stage.ToString(), Def.defName, true); base.OnPassed(context); }
        public override void OnFailed(ExaminationContext context) { WhiteStage stage = Stage; Cleanup(context); GameComponent_WhitePresentation.ShowEnd(stage.ToString(), Def.defName, false); base.OnFailed(context); }
        public override void OnCancelled(ExaminationContext context) { Cleanup(context); base.OnCancelled(context); }
        public override IEnumerable<string> ProgressLines(ExaminationContext context) { Map map = FindMap(context); yield return "LCOrdeal_WhiteStage".Translate() + ": " + ("LCOrdeal_White" + Stage + "Name").Translate(); yield return "LCOrdeal_DawnTargets".Translate() + ": " + (map == null ? 0 : LivingTargets(context, map).Count); }

        public static WhiteOrdealSettings ActiveSettings(string defName = null) { CompanyExaminationDef def = defName.NullOrEmpty() ? ActiveWhiteDef() : DefDatabase<CompanyExaminationDef>.GetNamedSilentFail(defName); return def?.GetModExtension<WhiteOrdealSettings>() ?? new WhiteOrdealSettings(); }

        public static bool TryAbsorbDamage(Pawn pawn, DamageInfo info)
        {
            FixerColor color; ExaminationContext context; if (pawn == null || !TryActiveContext(out context) || !TryFixerColor(pawn.kindDef, out color)) return false;
            if (color == FixerColor.White && context.CurrentTick < context.GetLong(PrayerUntilKey(pawn.thingIDNumber), 0L))
            {
                Pawn attacker = info.Instigator as Pawn; if (attacker != null && attacker != pawn && !attacker.Dead) attacker.TakeDamage(new DamageInfo(info.Def, info.Amount, 0f, instigator: pawn)); return true;
            }
            return info.Def == DamageFor(color);
        }

        public static void NotifyDamaged(Pawn pawn, float damage)
        {
            if (pawn == null || damage <= 0f) return; ExaminationContext context; if (!TryActiveContext(out context)) return; WhiteOrdealSettings settings = ActiveSettings(context.Definition.defName);
            if (pawn.kindDef == OrdealDefOf.LCOrdeal_WhiteFixerWhite)
            {
                int reached = Mathf.FloorToInt((1f - pawn.health.summaryHealth.SummaryHealthPercent) / settings.whitePrayerHealthStep); int prior = (int)context.GetLong(PrayerStageKey(pawn.thingIDNumber), 0L); if (reached > prior) { context.SetLong(PrayerStageKey(pawn.thingIDNumber), reached); context.SetLong(PrayerUntilKey(pawn.thingIDNumber), context.CurrentTick + settings.whitePrayerDurationTicks); }
            }
            if (pawn.kindDef == OrdealDefOf.LCOrdeal_WhiteClaw)
            {
                string special = context.GetString(SpecialKey(pawn.thingIDNumber), string.Empty); if (special == "Blue" || special == "Green")
                {
                    float prep = ReadFloat(context, PrepDamageKey(pawn.thingIDNumber)) + damage; WriteFloat(context, PrepDamageKey(pawn.thingIDNumber), prep); if (prep >= settings.clawInterruptDamage) { context.SetString(SpecialKey(pawn.thingIDNumber), string.Empty); context.SetLong(ResolveKey(pawn.thingIDNumber), -1L); context.SetLong(NextKey(pawn.thingIDNumber), context.CurrentTick + settings.clawSpecialIntervalTicks.RandomInRange); pawn.stances.stunner.StunFor(settings.clawInterruptStunTicks.RandomInRange, pawn, false); }
                }
                float green = ReadFloat(context, GreenDamageKey(pawn.thingIDNumber)) + damage; if (green >= settings.clawGreenDamageTrigger) { context.SetLong(GreenRequestedKey(pawn.thingIDNumber), 1L); if (special.NullOrEmpty()) context.SetLong(NextKey(pawn.thingIDNumber), context.CurrentTick); green %= settings.clawGreenDamageTrigger; } WriteFloat(context, GreenDamageKey(pawn.thingIDNumber), green);
            }
        }

        public static void NotifyKilled(Pawn pawn, Map map, IntVec3 position, Rot4 rotation)
        {
            ExaminationContext context; if (pawn == null || map == null || !TryActiveContext(out context)) return; WhiteOrdealSettings settings = ActiveSettings(context.Definition.defName); FixerColor color; if (!TryFixerColor(pawn.kindDef, out color)) return;
            if (color == FixerColor.Red) DamageFan(map, position, rotation, settings.redDeathSweepRadius, settings.redDeathSweepAngle, settings.redDeathSweepDamage, LCDamageDefOf.LC_RedDamage, pawn);
            else if (color == FixerColor.White) DamageRadius(map, position, settings.whiteDeathRadius, settings.whiteDeathDamage, LCDamageDefOf.LC_WhiteDamage, pawn);
            else if (color == FixerColor.Black) ResetPlatforms(map, position, settings.blackPlatformResetRadius);
            else DamageRadius(map, position, settings.paleDeathRadius, settings.paleDeathDamage, LCDamageDefOf.LC_PaleDamage, pawn);
        }

        private WhiteStage Stage => Def.defName.EndsWith("Dawn") ? WhiteStage.Dawn : Def.defName.EndsWith("Noon") ? WhiteStage.Noon : Def.defName.EndsWith("Dusk") ? WhiteStage.Dusk : WhiteStage.Midnight;
        private WhiteOrdealSettings Settings => Def.GetModExtension<WhiteOrdealSettings>() ?? new WhiteOrdealSettings();
        private static void UpdateFixer(ExaminationContext context, Map map, Pawn fixer)
        {
            WhiteOrdealSettings settings = ActiveSettings(context.Definition.defName); FixerColor color; if (!TryFixerColor(fixer.kindDef, out color)) return;
            if (color == FixerColor.White && context.CurrentTick < context.GetLong(PrayerUntilKey(fixer.thingIDNumber), 0L)) { fixer.pather.StopDead(); if (fixer.CurJob != null) fixer.jobs.StopAll(); }
            if (color == FixerColor.White) TickWhiteFog(context, map, fixer, settings);
            if (color == FixerColor.Black) TickBlackPulses(context, map, fixer, settings);
            if (color == FixerColor.Pale && context.CurrentTick >= context.GetLong(TeleportKey(fixer.thingIDNumber), int.MaxValue)) { Pawn target = RandomColonist(map); if (target != null) MovePawn(fixer, CellFinder.RandomClosewalkCellNear(target.Position, map, 2), map); context.SetLong(TeleportKey(fixer.thingIDNumber), context.CurrentTick + settings.paleTeleportIntervalTicks.RandomInRange); }
            long resolve = context.GetLong(ResolveKey(fixer.thingIDNumber), -1L); if (resolve >= 0 && context.CurrentTick >= resolve) { ResolveFixerSpecial(context, map, fixer, color, settings); context.SetLong(ResolveKey(fixer.thingIDNumber), -1L); context.SetLong(NextKey(fixer.thingIDNumber), context.CurrentTick + SpecialInterval(settings, color)); return; }
            if (resolve < 0 && context.CurrentTick >= context.GetLong(NextKey(fixer.thingIDNumber), int.MaxValue)) BeginTargeted(context, map, fixer, settings.fixerTelegraphTicks);
        }

        private static void ResolveFixerSpecial(ExaminationContext context, Map map, Pawn fixer, FixerColor color, WhiteOrdealSettings settings)
        {
            IntVec3 target = DecodeCell(context.GetLong(TargetKey(fixer.thingIDNumber), 0L));
            if (color == FixerColor.Red)
            {
                bool laser = context.GetLong(AlternateKey(fixer.thingIDNumber), 0L) != 0; if (laser) DamageLine(map, fixer.Position, target, settings.redLaserWidth, settings.redLaserDamage, LCDamageDefOf.LC_RedDamage, fixer); else DamageRadius(map, fixer.Position, settings.redSpinRadius, settings.redSpinDamage, LCDamageDefOf.LC_RedDamage, fixer); context.SetLong(AlternateKey(fixer.thingIDNumber), laser ? 0L : 1L);
            }
            else if (color == FixerColor.White)
            {
                DamageLine(map, fixer.Position, target, settings.whiteBeamWidth, settings.whiteBeamDamage, LCDamageDefOf.LC_WhiteDamage, fixer); context.SetLong(FogOriginKey(fixer.thingIDNumber), EncodeCell(fixer.Position)); context.SetLong(FogTargetKey(fixer.thingIDNumber), EncodeCell(target)); context.SetLong(FogUntilKey(fixer.thingIDNumber), context.CurrentTick + settings.whiteFogDurationTicks); context.SetLong(FogNextKey(fixer.thingIDNumber), context.CurrentTick);
            }
            else if (color == FixerColor.Black) { context.SetLong(PulsesKey(fixer.thingIDNumber), settings.blackResonancePulseCount); context.SetLong(PulseNextKey(fixer.thingIDNumber), context.CurrentTick); }
            else { MovePawn(fixer, CellFinder.RandomClosewalkCellNear(target, map, 2), map); DamageRadius(map, fixer.Position, settings.paleBurstRadius, settings.paleBurstDamage, LCDamageDefOf.LC_PaleDamage, fixer); }
        }

        private static void TickWhiteFog(ExaminationContext context, Map map, Pawn fixer, WhiteOrdealSettings settings)
        {
            if (context.CurrentTick >= context.GetLong(FogUntilKey(fixer.thingIDNumber), 0L) || context.CurrentTick < context.GetLong(FogNextKey(fixer.thingIDNumber), int.MaxValue)) return; IntVec3 origin = DecodeCell(context.GetLong(FogOriginKey(fixer.thingIDNumber), 0L)); IntVec3 target = DecodeCell(context.GetLong(FogTargetKey(fixer.thingIDNumber), 0L));
            foreach (Pawn pawn in PawnsAlongLine(map, origin, target, settings.whiteFogWidth)) { pawn.TakeDamage(new DamageInfo(LCDamageDefOf.LC_WhiteDamage, settings.whiteFogDamage, 0f, instigator: fixer)); AddSlow(pawn, settings.whiteFogDurationTicks); } context.SetLong(FogNextKey(fixer.thingIDNumber), context.CurrentTick + settings.whiteFogTickIntervalTicks);
        }

        private static void TickBlackPulses(ExaminationContext context, Map map, Pawn fixer, WhiteOrdealSettings settings)
        {
            int pulses = (int)context.GetLong(PulsesKey(fixer.thingIDNumber), 0L); if (pulses <= 0 || context.CurrentTick < context.GetLong(PulseNextKey(fixer.thingIDNumber), int.MaxValue)) return; DamageRadius(map, fixer.Position, settings.blackResonanceRadius, settings.blackResonanceDamage, LCDamageDefOf.LC_BlackDamage, fixer); pulses--; context.SetLong(PulsesKey(fixer.thingIDNumber), pulses); context.SetLong(PulseNextKey(fixer.thingIDNumber), context.CurrentTick + settings.blackResonancePulseIntervalTicks); if (pulses == 0) ResetPlatforms(map, fixer.Position, settings.blackPlatformResetRadius);
        }

        private static void UpdateClaw(ExaminationContext context, Map map, Pawn claw)
        {
            WhiteOrdealSettings settings = ActiveSettings(context.Definition.defName); long resolve = context.GetLong(ResolveKey(claw.thingIDNumber), -1L); string special = context.GetString(SpecialKey(claw.thingIDNumber), string.Empty);
            if (resolve >= 0 && context.CurrentTick >= resolve) { ResolveClaw(context, map, claw, special, settings); context.SetString(SpecialKey(claw.thingIDNumber), string.Empty); context.SetLong(ResolveKey(claw.thingIDNumber), -1L); context.SetLong(NextKey(claw.thingIDNumber), context.CurrentTick + settings.clawSpecialIntervalTicks.RandomInRange); return; }
            if (resolve < 0 && context.CurrentTick >= context.GetLong(BasicNextKey(claw.thingIDNumber), int.MaxValue))
            {
                Pawn victim = map.mapPawns.FreeColonistsSpawned.Where(item => !item.Dead && item.Position.InHorDistOf(claw.Position, settings.clawBasicAttackRadius)).OrderBy(item => item.Position.DistanceToSquared(claw.Position)).FirstOrDefault(); if (victim != null) { victim.TakeDamage(new DamageInfo(LCDamageDefOf.LC_RedDamage, settings.clawBasicAttackDamage, 0f, instigator: claw)); if (Rand.Chance(settings.clawBasicDoubleAttackChance) && !victim.Dead) victim.TakeDamage(new DamageInfo(LCDamageDefOf.LC_RedDamage, settings.clawBasicAttackDamage, 0f, instigator: claw)); } context.SetLong(BasicNextKey(claw.thingIDNumber), context.CurrentTick + settings.clawBasicAttackIntervalTicks);
            }
            if (resolve < 0 && context.CurrentTick >= context.GetLong(NextKey(claw.thingIDNumber), int.MaxValue))
            {
                string next = context.GetLong(GreenRequestedKey(claw.thingIDNumber), 0L) != 0 ? "Green" : Rand.Chance(settings.clawOrangeChance) ? "Orange" : "Blue"; context.SetLong(GreenRequestedKey(claw.thingIDNumber), 0L); context.SetString(SpecialKey(claw.thingIDNumber), next); WriteFloat(context, PrepDamageKey(claw.thingIDNumber), 0f); Pawn target = RandomColonist(map); if (target != null) context.SetLong(TargetKey(claw.thingIDNumber), EncodeCell(target.Position));
                if (next == "Blue") context.SetString(MarksKey(claw.thingIDNumber), string.Join(",", map.mapPawns.FreeColonistsSpawned.Where(item => !item.Dead).InRandomOrder().Take(settings.clawBlueMarkCount).Select(item => item.thingIDNumber)));
                context.SetLong(ResolveKey(claw.thingIDNumber), context.CurrentTick + (next == "Orange" ? settings.clawOrangeTelegraphTicks : next == "Blue" ? settings.clawBlueTelegraphTicks : settings.clawGreenTelegraphTicks));
            }
        }

        private static void ResolveClaw(ExaminationContext context, Map map, Pawn claw, string special, WhiteOrdealSettings settings)
        {
            if (special == "Orange") { IntVec3 target = DecodeCell(context.GetLong(TargetKey(claw.thingIDNumber), 0L)); DamageLine(map, claw.Position, target, settings.clawOrangeDashWidth, settings.clawOrangeDashDamage, LCDamageDefOf.LC_RedDamage, claw); MovePawn(claw, CellFinder.RandomClosewalkCellNear(target, map, 2), map); }
            else if (special == "Blue") foreach (int id in ParseIds(context.GetString(MarksKey(claw.thingIDNumber), string.Empty))) { Pawn target = map.mapPawns.AllPawnsSpawned.FirstOrDefault(item => item.thingIDNumber == id && !item.Dead); if (target == null) continue; MovePawn(claw, CellFinder.RandomClosewalkCellNear(target.Position, map, 1), map); target.TakeDamage(new DamageInfo(LCDamageDefOf.LC_BlackDamage, settings.clawBlueDamage, 0f, instigator: claw)); }
            else if (special == "Green") Heal(claw, settings.clawGreenHealAmount);
        }

        private static int InitialDelay(WhiteOrdealSettings settings, FixerColor color) => (color == FixerColor.Red ? settings.redInitialSpecialDelayTicks : color == FixerColor.White ? settings.whiteInitialSpecialDelayTicks : color == FixerColor.Black ? settings.blackInitialSpecialDelayTicks : settings.paleInitialSpecialDelayTicks).RandomInRange;
        private static int SpecialInterval(WhiteOrdealSettings settings, FixerColor color) => (color == FixerColor.Red ? settings.redSpecialIntervalTicks : color == FixerColor.White ? settings.whiteSpecialIntervalTicks : color == FixerColor.Black ? settings.blackSpecialIntervalTicks : settings.paleSpecialIntervalTicks).RandomInRange;

        private FixerColor ReadDawnColor(ExaminationContext context) { CompanyExaminationDef dawn = DefDatabase<CompanyExaminationDef>.GetNamedSilentFail("LCOrdeal_WhiteDawn"); ExaminationContext dawnContext = dawn == null ? null : context.Component.GetExaminationContext(dawn); FixerColor result; return dawnContext != null && Enum.TryParse(dawnContext.GetString(DawnColorKey), out result) ? result : FixerColor.Red; }
        private static string PreviousDefName(WhiteStage stage) => stage == WhiteStage.Noon ? "LCOrdeal_WhiteDawn" : stage == WhiteStage.Dusk ? "LCOrdeal_WhiteNoon" : "LCOrdeal_WhiteDusk";
        private static CompanyExaminationDef ActiveWhiteDef() { ExaminationContext context; return TryActiveContext(out context) ? context.Definition : DefDatabase<CompanyExaminationDef>.GetNamedSilentFail("LCOrdeal_WhiteDawn"); }
        private static bool TryActiveContext(out ExaminationContext context) { context = null; return StoryComponents.Development != null && StoryComponents.Development.TryGetActiveExaminationContext(out context) && context.Definition.defName.StartsWith("LCOrdeal_White"); }
        private static PawnKindDef KindFor(FixerColor color) => color == FixerColor.Red ? OrdealDefOf.LCOrdeal_WhiteFixerRed : color == FixerColor.White ? OrdealDefOf.LCOrdeal_WhiteFixerWhite : color == FixerColor.Black ? OrdealDefOf.LCOrdeal_WhiteFixerBlack : OrdealDefOf.LCOrdeal_WhiteFixerPale;
        private static DamageDef DamageFor(FixerColor color) => color == FixerColor.Red ? LCDamageDefOf.LC_RedDamage : color == FixerColor.White ? LCDamageDefOf.LC_WhiteDamage : color == FixerColor.Black ? LCDamageDefOf.LC_BlackDamage : LCDamageDefOf.LC_PaleDamage;
        private static bool TryFixerColor(PawnKindDef kind, out FixerColor color) { foreach (FixerColor candidate in Enum.GetValues(typeof(FixerColor))) if (kind == KindFor(candidate)) { color = candidate; return true; } color = FixerColor.Red; return false; }
        private static void BeginTargeted(ExaminationContext context, Map map, Pawn pawn, int delay) { Pawn target = RandomColonist(map); if (target == null) return; context.SetLong(TargetKey(pawn.thingIDNumber), EncodeCell(target.Position)); context.SetLong(ResolveKey(pawn.thingIDNumber), context.CurrentTick + delay); FleckMaker.ThrowLightningGlow(target.Position.ToVector3Shifted(), map, 2f); }
        private static Pawn RandomColonist(Map map) => map.mapPawns.FreeColonistsSpawned.Where(item => !item.Dead).RandomElementWithFallback();
        private static IEnumerable<Pawn> PawnsAlongLine(Map map, IntVec3 origin, IntVec3 target, float width) { Vector2 a = new Vector2(origin.x, origin.z); Vector2 direction = (new Vector2(target.x, target.z) - a).normalized; return map.mapPawns.FreeColonistsSpawned.Where(pawn => { Vector2 offset = new Vector2(pawn.Position.x, pawn.Position.z) - a; return Vector2.Dot(offset, direction) >= 0f && Mathf.Abs(offset.x * direction.y - offset.y * direction.x) <= width; }).ToList(); }
        private static void DamageLine(Map map, IntVec3 origin, IntVec3 target, float width, float damage, DamageDef def, Thing instigator) { foreach (Pawn pawn in PawnsAlongLine(map, origin, target, width)) pawn.TakeDamage(new DamageInfo(def, damage, 0f, instigator: instigator)); }
        private static void DamageRadius(Map map, IntVec3 center, float radius, float damage, DamageDef def, Thing instigator) { foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned.Where(item => !item.Dead && item.Position.InHorDistOf(center, radius)).ToList()) pawn.TakeDamage(new DamageInfo(def, damage, 0f, instigator: instigator)); }
        private static void DamageFan(Map map, IntVec3 center, Rot4 rotation, float radius, float angle, float damage, DamageDef def, Thing instigator) { Vector2 forward = new Vector2(rotation.FacingCell.x, rotation.FacingCell.z); foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned.Where(item => !item.Dead && item.Position.InHorDistOf(center, radius)).ToList()) { Vector2 offset = new Vector2(pawn.Position.x - center.x, pawn.Position.z - center.z).normalized; if (Vector2.Angle(forward, offset) <= angle / 2f) pawn.TakeDamage(new DamageInfo(def, damage, 0f, instigator: instigator)); } }
        private static int ResetPlatforms(Map map, IntVec3 center, float radius) { int count = 0; foreach (Building_AbnormalityHoldingPlatform platform in map.listerBuildings.AllBuildingsColonistOfClass<Building_AbnormalityHoldingPlatform>()) { if (!platform.Position.InHorDistOf(center, radius)) continue; CompAbnormality comp = platform.HeldPawn?.TryGetComp<CompAbnormality>(); if (comp != null && comp.QliphothEnabled) { comp.QliphothCountCurrent = 0; count++; } } return count; }
        private static void Heal(Pawn pawn, float amount) { float left = amount; foreach (Hediff_Injury injury in pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().OrderByDescending(item => item.Severity).ToList()) { float value = Math.Min(left, injury.Severity); injury.Severity -= value; left -= value; if (left <= 0f) break; } }
        private static void AddSlow(Pawn pawn, int duration) { Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(OrdealDefOf.LCOrdeal_WhiteFogSlowness); if (hediff == null) { hediff = HediffMaker.MakeHediff(OrdealDefOf.LCOrdeal_WhiteFogSlowness, pawn); pawn.health.AddHediff(hediff); } HediffComp_Disappears comp = hediff.TryGetComp<HediffComp_Disappears>(); if (comp != null) comp.ticksToDisappear = Math.Max(comp.ticksToDisappear, duration); }
        private static void MovePawn(Pawn pawn, IntVec3 cell, Map map) { if (pawn == null || !pawn.Spawned || !cell.IsValid || cell == pawn.Position) return; pawn.DeSpawn(); GenSpawn.Spawn(pawn, cell, map); }
        private static Pawn SpawnAtEntry(Map map, PawnKindDef kind) { IntVec3 cell; if (!RCellFinder.TryFindRandomPawnEntryCell(out cell, map, CellFinder.EdgeRoadChance_Hostile, true)) return null; Pawn pawn = PawnGenerator.GeneratePawn(kind, Faction.OfMechanoids); GenSpawn.Spawn(pawn, cell, map); return pawn; }
        private static void AssignAssaultLord(Map map, IEnumerable<Pawn> pawns) { List<Pawn> list = pawns.Where(item => item != null && item.Spawned).ToList(); if (list.Count > 0) LordMaker.MakeNewLord(Faction.OfMechanoids, new LordJob_AssaultColony(Faction.OfMechanoids, false, false, false, false, false), map, list); }
        private static Map SelectMap() => Find.CurrentMap != null && Find.CurrentMap.IsPlayerHome && Find.CurrentMap.mapPawns.AnyFreeColonistSpawned ? Find.CurrentMap : Find.Maps.FirstOrDefault(map => map.IsPlayerHome && map.mapPawns.AnyFreeColonistSpawned);
        private static Map FindMap(ExaminationContext context) { int id = (int)context.GetLong(MapIdKey, -1L); return Find.Maps.FirstOrDefault(map => map.uniqueID == id); }
        private static List<int> TargetIds(ExaminationContext context) => ParseIds(context.GetString(TargetIdsKey, string.Empty)); private static List<int> ParseIds(string text) { List<int> result = new List<int>(); foreach (string part in text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)) { int id; if (int.TryParse(part, out id)) result.Add(id); } return result; }
        private static void SaveTargetIds(ExaminationContext context, IEnumerable<int> ids) => context.SetString(TargetIdsKey, string.Join(",", ids.Distinct())); private static List<Pawn> LivingTargets(ExaminationContext context, Map map) { HashSet<int> ids = new HashSet<int>(TargetIds(context)); return map.mapPawns.AllPawnsSpawned.Where(item => ids.Contains(item.thingIDNumber) && !item.Dead && !item.Destroyed).ToList(); }
        private static long EncodeCell(IntVec3 cell) => ((long)(uint)cell.x << 32) | (uint)cell.z; private static IntVec3 DecodeCell(long value) => new IntVec3((int)(value >> 32), 0, (int)value);
        private static float ReadFloat(ExaminationContext context, string key) => context.GetLong(key, 0L) / 1000f; private static void WriteFloat(ExaminationContext context, string key, float value) => context.SetLong(key, Mathf.RoundToInt(value * 1000f));
        private static string BasicNextKey(int id) => Prefix + id + ".basicNext";
        private static string NextKey(int id) => Prefix + id + ".next"; private static string ResolveKey(int id) => Prefix + id + ".resolve"; private static string TargetKey(int id) => Prefix + id + ".target"; private static string AlternateKey(int id) => Prefix + id + ".alternate"; private static string TeleportKey(int id) => Prefix + id + ".teleport"; private static string PrayerStageKey(int id) => Prefix + id + ".prayerStage"; private static string PrayerUntilKey(int id) => Prefix + id + ".prayerUntil"; private static string FogOriginKey(int id) => Prefix + id + ".fogOrigin"; private static string FogTargetKey(int id) => Prefix + id + ".fogTarget"; private static string FogUntilKey(int id) => Prefix + id + ".fogUntil"; private static string FogNextKey(int id) => Prefix + id + ".fogNext"; private static string PulsesKey(int id) => Prefix + id + ".pulses"; private static string PulseNextKey(int id) => Prefix + id + ".pulseNext"; private static string SpecialKey(int id) => Prefix + id + ".special"; private static string PrepDamageKey(int id) => Prefix + id + ".prepDamage"; private static string GreenDamageKey(int id) => Prefix + id + ".greenDamage"; private static string GreenRequestedKey(int id) => Prefix + id + ".greenRequested"; private static string MarksKey(int id) => Prefix + id + ".marks";
        private static void SpawnReward(Map map, int count) { ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail("EnkephalinBox"); Pawn recipient = map?.mapPawns.FreeColonistsSpawned.FirstOrDefault(); if (def == null || recipient == null || count <= 0) return; Thing reward = ThingMaker.MakeThing(def); reward.stackCount = Math.Min(count, def.stackLimit); GenPlace.TryPlaceThing(reward, recipient.Position, map, ThingPlaceMode.Near); }
        private static void Cleanup(ExaminationContext context) { HashSet<int> ids = new HashSet<int>(TargetIds(context)); foreach (Map map in Find.Maps) foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned.Where(item => ids.Contains(item.thingIDNumber)).ToList()) if (!pawn.Destroyed) pawn.Destroy(DestroyMode.Vanish); }
    }
}
