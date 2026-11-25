using System;
using System.Collections.Generic;
using LCAnomalyOrdeals.DefOfs;
using LCAnomalyOrdeals.Render;
using LCAnomalyOrdeals.Utilities;
using LCAnomalyOrdeals.Vfx;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;
using LCDamageDefOf = LCAnomalyCore.Defs.DamageDefOf;

namespace LCAnomalyOrdeals.Comps
{
    /// <summary>深红考验高阶实体层级（正午《汁水大合唱》 / 黄昏《绝顶之战》）。</summary>
    public enum CrimsonOrdealTier
    {
        Noon = 0,
        Dusk = 1
    }

    /// <summary>深红正午与深红黄昏核心行为与多部件骨骼动画状态机配置。</summary>
    public class CompProperties_CrimsonOrdeal : CompProperties
    {
        public CrimsonOrdealTier tier = CrimsonOrdealTier.Noon;
        public float noonSpecialDamageMin = 15f;
        public float noonSpecialDamageMax = 20f;
        public float duskRollDamageMin = 15f;
        public float duskRollDamageMax = 20f;
        public float duskRollHitRadius = 2.75f;
        public int duskRollDurationTicks = 56;
        public int duskAutonomousRollCooldownTicks = 660;

        public CompProperties_CrimsonOrdeal()
        {
            compClass = typeof(CompCrimsonOrdeal);
        }
    }

    /// <summary>
    /// 深红正午《汁水大合唱》(HE) 与深红黄昏《绝顶之战》(WAW) 原生骨骼动画与战斗机制组件：
    /// - 深红正午：三段式蝎尾砸击 / 双钳撕咬 / 飞扑重压（Attack1/Attack2/Attack3），每次攻击附带原版音效与四色红伤（HE）；
    /// - 深红黄昏：真实可观察的 360° 翻滚肉球冲锋（Roll，伴随滚压音效、喷血与沿途红伤碾压）+ 斩骨刀/铁签穿刺（Attack1）+ 巨型拍肉锤震地（Attack2）。
    /// </summary>
    public class CompCrimsonOrdeal : ThingComp, IOrdealArticulatedComp
    {
        private bool isFacingLeft;
        private int attackAnimTicksLeft;
        private int attackComboIndex;
        private bool suppressMeleeHook;

        // Crimson Dusk physical rolling boulder charge state
        private int rollTicksLeft;
        private int rollTotalTicks;
        private IntVec3 rollStartCell = IntVec3.Invalid;
        private IntVec3 rollDestCell = IntVec3.Invalid;
        private int nextAutonomousRollTick = -1;
        private readonly Dictionary<int, int> rollHitCooldownByPawnId = new Dictionary<int, int>();

        public CompProperties_CrimsonOrdeal Props => (CompProperties_CrimsonOrdeal)props;
        public Pawn Pawn => parent as Pawn;
        public CrimsonOrdealTier Tier => Props.tier;

        public bool IsFacingLeft => isFacingLeft;
        public bool IsBurrowedHidden => false;
        public bool IsAlertVisual => IsRolling || attackAnimTicksLeft > 0;
        public float ChargeProgress => 0f;
        public float EmitterSpinAngle => 0f;
        public bool IsRolling => Tier == CrimsonOrdealTier.Dusk && rollTicksLeft > 0;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref isFacingLeft, "isFacingLeft", false);
            Scribe_Values.Look(ref attackAnimTicksLeft, "attackAnimTicksLeft", 0);
            Scribe_Values.Look(ref attackComboIndex, "attackComboIndex", 0);
            Scribe_Values.Look(ref rollTicksLeft, "rollTicksLeft", 0);
            Scribe_Values.Look(ref rollTotalTicks, "rollTotalTicks", 0);
            Scribe_Values.Look(ref rollStartCell, "rollStartCell", IntVec3.Invalid);
            Scribe_Values.Look(ref rollDestCell, "rollDestCell", IntVec3.Invalid);
            Scribe_Values.Look(ref nextAutonomousRollTick, "nextAutonomousRollTick", -1);
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!respawningAfterLoad && Pawn != null && Pawn.Spawned)
            {
                Vector3 spawnPos = Pawn.Position.ToVector3Shifted();
                float tentSize = Tier == CrimsonOrdealTier.Dusk ? 3.8f : 3.0f;
                OrdealDefOf.LCOrdeal_Sound_CircusTentCreate?.PlayOneShot(new TargetInfo(Pawn.Position, Pawn.Map));
                OrdealVfxUtility.SpawnCircusTent(Pawn.Map, spawnPos, tentSize, 56);
                if (Tier == CrimsonOrdealTier.Dusk)
                {
                    nextAutonomousRollTick = Find.TickManager.TicksGame + Props.duskAutonomousRollCooldownTicks;
                }
            }
        }

        public override void CompTick()
        {
            base.CompTick();
            Pawn pawn = Pawn;
            if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.Map == null)
            {
                return;
            }

            if (attackAnimTicksLeft > 0)
            {
                attackAnimTicksLeft--;
            }

            UpdateFacing(pawn);

            if (Tier == CrimsonOrdealTier.Dusk)
            {
                TickDuskRoll(pawn);
            }

            UpdateAnimation(pawn);
        }

        /// <summary>由近战命中钩子触发：驱动深红正午三段连招或深红黄昏斩骨刀/拍肉锤骨骼动画与四色红伤技能。</summary>
        public void NotifyMeleeAttack(Thing target)
        {
            Pawn pawn = Pawn;
            if (suppressMeleeHook || pawn == null || !pawn.Spawned || pawn.Map == null || IsRolling)
            {
                return;
            }

            if (target != null)
            {
                UpdateFacingToward(pawn, target.Position);
            }

            Map map = pawn.Map;
            Vector3 fxPos = target != null ? target.DrawPos : pawn.DrawPos;
            Color crimsonFx = new Color(0.95f, 0.14f, 0.20f, 0.94f);
            DamageDef redDmg = LCDamageDefOf.LC_RedDamage ?? DamageDefOf.Cut;

            if (Tier == CrimsonOrdealTier.Noon)
            {
                int move = attackComboIndex % 3;
                attackComboIndex++;
                if (move == 0)
                {
                    // Attack1: 蝎尾凌空重砸（范围红伤）
                    attackAnimTicksLeft = 44;
                    OrdealDefOf.LCOrdeal_Sound_CircusNoonAtk1?.PlayOneShot(new TargetInfo(pawn.Position, map));
                    OrdealVfxUtility.SpawnShockwave(map, fxPos, 0.4f, 2.8f, crimsonFx, 26);
                    OrdealVfxUtility.SpawnSlash(map, fxPos, 2.6f, crimsonFx, 22);
                    ApplyCrimsonAoe(map, pawn, fxPos, 2.3f, Props.noonSpecialDamageMin, Props.noonSpecialDamageMax, redDmg, exclude: target as Pawn);
                }
                else if (move == 1)
                {
                    // Attack2: 双钳小丑头突刺撕咬
                    attackAnimTicksLeft = 40;
                    OrdealDefOf.LCOrdeal_Sound_CircusNoonAtk2?.PlayOneShot(new TargetInfo(pawn.Position, map));
                    OrdealVfxUtility.SpawnSlash(map, fxPos, 2.4f, crimsonFx, 20);
                    OrdealVfxUtility.SpawnSlash(map, fxPos + new Vector3(0.25f, 0f, 0.15f), 2.1f, new Color(1f, 0.26f, 0.28f, 0.90f), 18);
                }
                else
                {
                    // Attack3: 全身飞扑重压 + 血浆喷溅
                    attackAnimTicksLeft = 48;
                    OrdealDefOf.LCOrdeal_Sound_CircusNoonAtk3?.PlayOneShot(new TargetInfo(pawn.Position, map));
                    OrdealVfxUtility.SpawnCrater(map, fxPos, 2.8f, new Color(0.75f, 0.12f, 0.16f, 0.85f), 90);
                    OrdealVfxUtility.SpawnShockwave(map, fxPos, 0.5f, 3.2f, crimsonFx, 30);
                    FilthMaker.TryMakeFilth(fxPos.ToIntVec3().ClampInsideMap(map), map, ThingDefOf.Filth_Blood, 2);
                    ApplyCrimsonAoe(map, pawn, fxPos, 2.6f, Props.noonSpecialDamageMin, Props.noonSpecialDamageMax, redDmg, exclude: target as Pawn);
                }
            }
            else
            {
                int move = attackComboIndex % 2;
                attackComboIndex++;
                if (move == 0)
                {
                    // Attack1: 巨型斩骨刀劈砍 + 铁签突刺 + 嘴中小丑头窜出
                    attackAnimTicksLeft = 44;
                    OrdealDefOf.LCOrdeal_Sound_CircusDuskAtk1?.PlayOneShot(new TargetInfo(pawn.Position, map));
                    OrdealVfxUtility.SpawnSlash(map, fxPos, 3.2f, crimsonFx, 24);
                    OrdealVfxUtility.SpawnSlash(map, fxPos + new Vector3(0.2f, 0f, -0.2f), 2.6f, new Color(1f, 0.25f, 0.25f, 0.90f), 20);
                }
                else
                {
                    // Attack2: 巨型拍肉锤高举砸地（高额 WAW 范围红伤 + 震地裂坑）
                    attackAnimTicksLeft = 50;
                    OrdealDefOf.LCOrdeal_Sound_CircusDuskAtk2?.PlayOneShot(new TargetInfo(pawn.Position, map));
                    OrdealVfxUtility.SpawnCrater(map, fxPos, 3.8f, new Color(0.78f, 0.12f, 0.16f, 0.88f), 120);
                    OrdealVfxUtility.SpawnShockwave(map, fxPos, 0.6f, 3.8f, crimsonFx, 34);
                    FilthMaker.TryMakeFilth(fxPos.ToIntVec3().ClampInsideMap(map), map, ThingDefOf.Filth_Blood, 2);
                    ApplyCrimsonAoe(map, pawn, fxPos, 3.2f, Props.duskRollDamageMin, Props.duskRollDamageMax, redDmg, exclude: target as Pawn);
                }
            }
        }

        /// <summary>深红黄昏启动 360° 翻滚肉球冲锋（物理推进至目标区域并沿途喷血碾压）。</summary>
        public bool BeginRollCharge(IntVec3 destination)
        {
            Pawn pawn = Pawn;
            if (Tier != CrimsonOrdealTier.Dusk || pawn == null || !pawn.Spawned || pawn.Map == null || IsRolling || !destination.IsValid)
            {
                return false;
            }

            Map map = pawn.Map;
            IntVec3 clampedDest = destination.ClampInsideMap(map);
            if (!clampedDest.Standable(map))
            {
                clampedDest = CellFinder.RandomClosewalkCellNear(clampedDest, map, 3, c => c.Standable(map) && !c.Fogged(map));
            }
            if (!clampedDest.IsValid || clampedDest == pawn.Position)
            {
                return false;
            }

            float dist = pawn.Position.DistanceTo(clampedDest);
            rollTotalTicks = Mathf.Clamp(Mathf.RoundToInt(dist * 2.4f), 28, Props.duskRollDurationTicks);
            rollTicksLeft = rollTotalTicks;
            rollStartCell = pawn.Position;
            rollDestCell = clampedDest;
            rollHitCooldownByPawnId.Clear();
            nextAutonomousRollTick = Find.TickManager.TicksGame + Props.duskAutonomousRollCooldownTicks;

            UpdateFacingToward(pawn, clampedDest);
            pawn.pather?.StopDead();
            if (pawn.CurJob != null) pawn.jobs?.StopAll();

            OrdealDefOf.LCOrdeal_Sound_CircusDuskMove?.PlayOneShot(new TargetInfo(pawn.Position, map));
            OrdealVfxUtility.SpawnShockwave(map, pawn.DrawPos, 0.5f, 3.2f, new Color(0.95f, 0.16f, 0.22f, 0.90f), 28);
            return true;
        }

        private void TickDuskRoll(Pawn pawn)
        {
            Map map = pawn.Map;
            if (rollTicksLeft <= 0)
            {
                // 即使在无考验 Worker 的测试/自由生成场景下，也支持自主周期性发动翻滚冲锋
                if (nextAutonomousRollTick > 0 && Find.TickManager.TicksGame >= nextAutonomousRollTick && pawn.IsHashIntervalTick(30))
                {
                    Pawn victim = OrdealTargetUtility.AllTargets(map)
                        .Find(v => v.Spawned && !v.Dead && v.Position.InHorDistOf(pawn.Position, 22f) && !v.Position.InHorDistOf(pawn.Position, 4.5f));
                    if (victim != null)
                    {
                        BeginRollCharge(victim.Position);
                    }
                    else
                    {
                        nextAutonomousRollTick = Find.TickManager.TicksGame + 240;
                    }
                }
                return;
            }

            pawn.pather?.StopDead();
            rollTicksLeft--;
            float progress = rollTotalTicks > 0 ? 1f - (rollTicksLeft / (float)rollTotalTicks) : 1f;
            Vector3 worldPos = Vector3.Lerp(rollStartCell.ToVector3Shifted(), rollDestCell.ToVector3Shifted(), progress);
            IntVec3 stepCell = worldPos.ToIntVec3().ClampInsideMap(map);

            if (stepCell != pawn.Position && stepCell.Standable(map))
            {
                pawn.Position = stepCell;
                pawn.Notify_Teleported(endCurrentJob: false, resetTweenedPos: false);
            }

            int now = Find.TickManager.TicksGame;
            Color crimsonFx = new Color(0.95f, 0.15f, 0.22f, 0.92f);

            // 沿途喷血与肉球滚压音效
            if (rollTicksLeft % 9 == 0)
            {
                FilthMaker.TryMakeFilth(pawn.Position, map, ThingDefOf.Filth_Blood, 1);
                OrdealVfxUtility.SpawnShockwave(map, pawn.DrawPos, 0.4f, 2.2f, crimsonFx, 18);
                SoundDef bleedSfx = Rand.Bool ? OrdealDefOf.LCOrdeal_Sound_CircusDuskBleed1 : OrdealDefOf.LCOrdeal_Sound_CircusDuskBleed2;
                bleedSfx?.PlayOneShot(new TargetInfo(pawn.Position, map));
            }

            // 沿途碾压碰撞红伤（WAW 等级）
            if (rollTicksLeft % 4 == 0)
            {
                DamageDef redDmg = LCDamageDefOf.LC_RedDamage ?? DamageDefOf.Blunt;
                foreach (Pawn victim in OrdealTargetUtility.AllTargets(map))
                {
                    if (victim.Dead || !victim.Spawned || !victim.Position.InHorDistOf(pawn.Position, Props.duskRollHitRadius))
                    {
                        continue;
                    }

                    if (rollHitCooldownByPawnId.TryGetValue(victim.thingIDNumber, out int nextHitTick) && now < nextHitTick)
                    {
                        continue;
                    }

                    rollHitCooldownByPawnId[victim.thingIDNumber] = now + 26;
                    victim.TakeDamage(new DamageInfo(redDmg, Rand.Range(Props.duskRollDamageMin, Props.duskRollDamageMax), 0.25f, instigator: pawn, weapon: pawn.def));
                    OrdealVfxUtility.SpawnSlash(map, victim.DrawPos, 2.5f, crimsonFx, 20);
                }
            }

            // 翻滚着陆重击（moveEnd）
            if (rollTicksLeft == 0)
            {
                if (rollDestCell.IsValid && rollDestCell.Standable(map) && pawn.Position != rollDestCell)
                {
                    pawn.Position = rollDestCell;
                    pawn.Notify_Teleported(endCurrentJob: true, resetTweenedPos: true);
                }
                else
                {
                    pawn.Notify_Teleported(endCurrentJob: true, resetTweenedPos: true);
                }

                OrdealDefOf.LCOrdeal_Sound_CircusDuskAtk2?.PlayOneShot(new TargetInfo(pawn.Position, map));
                OrdealVfxUtility.SpawnCrater(map, pawn.DrawPos, 4.0f, new Color(0.80f, 0.14f, 0.18f, 0.88f), 130);
                OrdealVfxUtility.SpawnShockwave(map, pawn.DrawPos, 0.7f, 4.2f, crimsonFx, 34);
                DamageDef redDmg = LCDamageDefOf.LC_RedDamage ?? DamageDefOf.Blunt;
                ApplyCrimsonAoe(map, pawn, pawn.DrawPos, 3.4f, Props.duskRollDamageMin, Props.duskRollDamageMax, redDmg);
                rollStartCell = IntVec3.Invalid;
                rollDestCell = IntVec3.Invalid;
            }
        }

        private void ApplyCrimsonAoe(Map map, Pawn attacker, Vector3 center, float radius, float minDmg, float maxDmg, DamageDef dmgDef, Pawn exclude = null)
        {
            IntVec3 centerCell = center.ToIntVec3().ClampInsideMap(map);
            suppressMeleeHook = true;
            try
            {
                foreach (Pawn victim in OrdealTargetUtility.AllTargets(map))
                {
                    if (victim == exclude || victim.Dead || !victim.Spawned)
                    {
                        continue;
                    }

                    if (victim.Position.InHorDistOf(centerCell, radius))
                    {
                        victim.TakeDamage(new DamageInfo(dmgDef, Rand.Range(minDmg, maxDmg), 0.2f, instigator: attacker, weapon: attacker.def));
                    }
                }
            }
            finally
            {
                suppressMeleeHook = false;
            }
        }

        private void UpdateFacing(Pawn pawn)
        {
            if (pawn.pather != null && pawn.pather.Moving)
            {
                int dx = pawn.pather.nextCell.x - pawn.Position.x;
                if (dx < 0) isFacingLeft = true;
                else if (dx > 0) isFacingLeft = false;
                return;
            }

            if (pawn.Rotation == Rot4.West) isFacingLeft = true;
            else if (pawn.Rotation == Rot4.East) isFacingLeft = false;
        }

        private void UpdateFacingToward(Pawn pawn, IntVec3 targetCell)
        {
            int dx = targetCell.x - pawn.Position.x;
            if (dx < 0) isFacingLeft = true;
            else if (dx > 0) isFacingLeft = false;
        }

        private void UpdateAnimation(Pawn pawn)
        {
            AnimationDef desired = ResolveDesiredAnimation(pawn);
            if (desired == null)
            {
                return;
            }

            PawnRenderTree tree = pawn.Drawer?.renderer?.renderTree;
            if (tree == null)
            {
                return;
            }

            if (tree.currentAnimation != desired)
            {
                pawn.Drawer.renderer.SetAnimation(desired);
            }
        }

        private AnimationDef ResolveDesiredAnimation(Pawn pawn)
        {
            bool moving = pawn.pather != null && pawn.pather.Moving;
            if (Tier == CrimsonOrdealTier.Noon)
            {
                if (attackAnimTicksLeft > 0)
                {
                    int m = Math.Max(0, attackComboIndex - 1) % 3;
                    if (m == 0) return OrdealDefOf.LCOrdeal_CrimsonNoon_Attack1;
                    if (m == 1) return OrdealDefOf.LCOrdeal_CrimsonNoon_Attack2;
                    return OrdealDefOf.LCOrdeal_CrimsonNoon_Attack3;
                }

                return moving ? OrdealDefOf.LCOrdeal_CrimsonNoon_Walk : OrdealDefOf.LCOrdeal_CrimsonNoon_Idle;
            }

            if (rollTicksLeft > 0)
            {
                return OrdealDefOf.LCOrdeal_CrimsonDusk_Roll;
            }

            if (attackAnimTicksLeft > 0)
            {
                int m = Math.Max(0, attackComboIndex - 1) % 2;
                return m == 0 ? OrdealDefOf.LCOrdeal_CrimsonDusk_Attack1 : OrdealDefOf.LCOrdeal_CrimsonDusk_Attack2;
            }

            return moving ? OrdealDefOf.LCOrdeal_CrimsonDusk_Walk : OrdealDefOf.LCOrdeal_CrimsonDusk_Idle;
        }
    }
}
