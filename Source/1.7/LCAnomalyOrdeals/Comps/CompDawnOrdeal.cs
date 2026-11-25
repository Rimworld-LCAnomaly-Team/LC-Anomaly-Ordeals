using System;
using System.Collections.Generic;
using System.Linq;
using LCAnomalyCore.Buildings;
using LCAnomalyCore.Comp;
using LCAnomalyOrdeals.DefOfs;
using LCAnomalyOrdeals.Defs;
using LCAnomalyOrdeals.Examinations;
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
    /// <summary>黎明考验 4 种实体原型分类。</summary>
    public enum DawnOrdealArchetype
    {
        Amber = 0,
        Crimson = 1,
        Green = 2,
        Violet = 3
    }

    /// <summary>黎明考验单位核心组件配置（绑定 4 套原生骨骼动画与原版机制参数）。</summary>
    public class CompProperties_DawnOrdeal : CompProperties
    {
        public DawnOrdealArchetype archetype = DawnOrdealArchetype.Amber;
        public int crimsonTamperDurationTicks = 540;
        public int greenExecutionDurationTicks = 210;
        public int violetSporeIntervalTicks = 180;
        public float violetSporeRadius = 4.6f;
        public float violetSporeDamage = 4.5f;

        public CompProperties_DawnOrdeal()
        {
            compClass = typeof(CompDawnOrdeal);
        }
    }

    /// <summary>
    /// 黎明考验单位核心行为与骨骼动画状态机（对齐异想体设计品质）：
    /// - 琥珀黎明《完美食物》：三段式可视钻地突袭（DigIn -> 地下潜行 -> DigOut 破土红伤撕咬）+ 咬合骨骼动画；
    /// - 深红黎明《为开端欢呼》：真实收容室破坏 AI（奔袭收容平台 -> 9 秒吟唱破坏逆卡巴拉计数器 -> 马戏帐篷传送门撤离）+ 死亡高额红伤自爆；
    /// - 翠绿黎明《疑惑》：巡逻绿灯/战斗红灯自动切换 + 双式长矛连刺（Attack1/Attack2）+ 击杀后 3.5 秒虐尸狂暴（DeadScene 持续白伤冲击波）；
    /// - 紫罗兰黎明《理解之果》：周期性黑伤孢子脉冲 + 蓄力核心膨胀预警（Charge）+ 倒计时自爆（黑伤 + 白伤 + 周边收容逆卡巴拉归零）。
    /// </summary>
    public class CompDawnOrdeal : ThingComp, IOrdealArticulatedComp
    {
        // Shared animation & facing state
        private bool isFacingLeft;
        private int attackAnimTicksLeft;
        private bool attackAlternate;

        // Amber Dawn 3-phase burrow state
        private int digInTicksLeft;
        private int burrowedTicksLeft;
        private int digOutTicksLeft;
        private IntVec3 pendingBurrowDest = IntVec3.Invalid;

        // Crimson Dawn sabotage & circus portal state
        private int tamperTicksDone;
        private int tamperTargetId = -1;
        private int teleportAnimTicksLeft;

        // Green Dawn DeadScene execution frenzy state
        private int executionTicksLeft;
        private IntVec3 executionPos = IntVec3.Invalid;

        // Violet Dawn charge & spore pulse state
        private float chargeProgress;
        private int nextSporeTick = -1;

        public CompProperties_DawnOrdeal Props => (CompProperties_DawnOrdeal)props;
        public Pawn Pawn => parent as Pawn;
        public DawnOrdealArchetype Archetype => Props.archetype;

        public bool IsFacingLeft => isFacingLeft;
        public bool IsBurrowedHidden => Archetype == DawnOrdealArchetype.Amber && burrowedTicksLeft > 0;
        public bool IsBurrowing => Archetype == DawnOrdealArchetype.Amber && (digInTicksLeft > 0 || burrowedTicksLeft > 0 || digOutTicksLeft > 0);
        public bool IsTampering => Archetype == DawnOrdealArchetype.Crimson && tamperTicksDone > 0;
        public bool IsExecutingCorpse => Archetype == DawnOrdealArchetype.Green && executionTicksLeft > 0;
        public float ChargeProgress => chargeProgress;
        public float EmitterSpinAngle => 0f;

        /// <summary>翠绿黎明是否亮起战斗/杀戮红灯（Head_Red）。</summary>
        public bool IsAlertVisual
        {
            get
            {
                if (Archetype != DawnOrdealArchetype.Green)
                {
                    return false;
                }

                if (executionTicksLeft > 0 || attackAnimTicksLeft > 0)
                {
                    return true;
                }

                Pawn p = Pawn;
                if (p == null)
                {
                    return false;
                }

                return p.CurJobDef == JobDefOf.AttackMelee
                    || p.stances?.curStance is Stance_Busy
                    || p.mindState?.enemyTarget != null;
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref isFacingLeft, "isFacingLeft", false);
            Scribe_Values.Look(ref attackAnimTicksLeft, "attackAnimTicksLeft", 0);
            Scribe_Values.Look(ref attackAlternate, "attackAlternate", false);
            Scribe_Values.Look(ref digInTicksLeft, "digInTicksLeft", 0);
            Scribe_Values.Look(ref burrowedTicksLeft, "burrowedTicksLeft", 0);
            Scribe_Values.Look(ref digOutTicksLeft, "digOutTicksLeft", 0);
            Scribe_Values.Look(ref pendingBurrowDest, "pendingBurrowDest", IntVec3.Invalid);
            Scribe_Values.Look(ref tamperTicksDone, "tamperTicksDone", 0);
            Scribe_Values.Look(ref tamperTargetId, "tamperTargetId", -1);
            Scribe_Values.Look(ref teleportAnimTicksLeft, "teleportAnimTicksLeft", 0);
            Scribe_Values.Look(ref executionTicksLeft, "executionTicksLeft", 0);
            Scribe_Values.Look(ref executionPos, "executionPos", IntVec3.Invalid);
            Scribe_Values.Look(ref chargeProgress, "chargeProgress", 0f);
            Scribe_Values.Look(ref nextSporeTick, "nextSporeTick", -1);
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!respawningAfterLoad && Pawn != null && Pawn.Spawned)
            {
                Vector3 spawnPos = Pawn.Position.ToVector3Shifted();
                if (Archetype == DawnOrdealArchetype.Amber)
                {
                    digOutTicksLeft = 42;
                    OrdealDefOf.LCOrdeal_Sound_Bug1Birth?.PlayOneShot(new TargetInfo(Pawn.Position, Pawn.Map));
                    OrdealVfxUtility.SpawnCrater(Pawn.Map, spawnPos, 2.4f, new Color(0.88f, 0.54f, 0.16f, 0.85f), 110);
                }
                else if (Archetype == DawnOrdealArchetype.Crimson)
                {
                    teleportAnimTicksLeft = 30;
                    OrdealDefOf.LCOrdeal_Sound_CircusTentCreate?.PlayOneShot(new TargetInfo(Pawn.Position, Pawn.Map));
                    OrdealDefOf.LCOrdeal_Sound_CircusCreate?.PlayOneShot(new TargetInfo(Pawn.Position, Pawn.Map));
                    OrdealVfxUtility.SpawnCircusTent(Pawn.Map, spawnPos, 2.5f, 48);
                }
                else if (Archetype == DawnOrdealArchetype.Green)
                {
                    OrdealDefOf.LCOrdeal_Sound_MachineDefault?.PlayOneShot(new TargetInfo(Pawn.Position, Pawn.Map));
                }
                else if (Archetype == DawnOrdealArchetype.Violet)
                {
                    nextSporeTick = Find.TickManager.TicksGame + Props.violetSporeIntervalTicks;
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

            if (attackAnimTicksLeft > 0) attackAnimTicksLeft--;
            if (teleportAnimTicksLeft > 0) teleportAnimTicksLeft--;

            UpdateFacing(pawn);

            switch (Archetype)
            {
                case DawnOrdealArchetype.Amber:
                    TickAmberBurrow(pawn);
                    break;
                case DawnOrdealArchetype.Crimson:
                    TickCrimsonSabotage(pawn);
                    break;
                case DawnOrdealArchetype.Green:
                    TickGreenExecution(pawn);
                    break;
                case DawnOrdealArchetype.Violet:
                    TickVioletSporeAura(pawn);
                    break;
            }

            UpdateAnimation(pawn);
        }

        /// <summary>由近战命中钩子调用：播放对应考验的攻击骨骼动画、原版攻击音效与斩击特效。</summary>
        public void NotifyMeleeAttack(Thing target)
        {
            Pawn pawn = Pawn;
            if (pawn == null || !pawn.Spawned || pawn.Map == null)
            {
                return;
            }

            if (target != null)
            {
                UpdateFacingToward(pawn, target.Position);
            }

            attackAlternate = !attackAlternate;
            attackAnimTicksLeft = Archetype == DawnOrdealArchetype.Green ? (attackAlternate ? 42 : 48) : 40;

            Vector3 fxPos = target != null ? target.DrawPos : pawn.DrawPos;
            switch (Archetype)
            {
                case DawnOrdealArchetype.Amber:
                    OrdealDefOf.LCOrdeal_Sound_Bug1Attack?.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                    OrdealVfxUtility.SpawnSlash(pawn.Map, fxPos, 1.8f, new Color(0.95f, 0.48f, 0.16f, 0.90f), 18);
                    break;
                case DawnOrdealArchetype.Crimson:
                    OrdealVfxUtility.SpawnSlash(pawn.Map, fxPos, 2.0f, new Color(0.96f, 0.16f, 0.24f, 0.90f), 20);
                    break;
                case DawnOrdealArchetype.Green:
                    OrdealDefOf.LCOrdeal_Sound_MachineBlood?.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                    OrdealVfxUtility.SpawnSlash(pawn.Map, fxPos, 2.6f, new Color(0.95f, 0.22f, 0.26f, 0.94f), 22);
                    break;
                case DawnOrdealArchetype.Violet:
                    OrdealDefOf.LCOrdeal_Sound_OutterGodAtk?.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                    OrdealVfxUtility.SpawnShockwave(pawn.Map, fxPos, 0.3f, 2.0f, new Color(0.66f, 0.22f, 0.96f, 0.85f), 22);
                    break;
            }
        }

        /// <summary>琥珀黎明启动三段式可视钻地潜行（DigIn -> 地下潜行 -> DigOut 破土红伤撕咬）。</summary>
        public bool BeginAmberBurrow(IntVec3 destination)
        {
            Pawn pawn = Pawn;
            if (Archetype != DawnOrdealArchetype.Amber || pawn == null || !pawn.Spawned || IsBurrowing || !destination.IsValid)
            {
                return false;
            }

            pendingBurrowDest = destination;
            digInTicksLeft = 36;
            pawn.pather?.StopDead();
            if (pawn.CurJob != null) pawn.jobs?.StopAll();
            OrdealDefOf.LCOrdeal_Sound_Bug1DigIn?.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
            Color amberFx = new Color(0.90f, 0.56f, 0.16f, 0.88f);
            OrdealVfxUtility.SpawnCrater(pawn.Map, pawn.DrawPos, 2.4f, amberFx, 120);
            FleckMaker.ThrowDustPuffThick(pawn.DrawPos, pawn.Map, 1.5f, new Color(0.75f, 0.52f, 0.22f));
            return true;
        }

        /// <summary>翠绿黎明击杀人形目标后触发 3.5 秒虐尸狂暴（DeadScene 骨骼动画 + 周期性白伤精神冲击波）。</summary>
        public void BeginGreenExecution(IntVec3 corpseCell)
        {
            Pawn pawn = Pawn;
            if (Archetype != DawnOrdealArchetype.Green || pawn == null || !pawn.Spawned)
            {
                return;
            }

            executionTicksLeft = Props.greenExecutionDurationTicks;
            executionPos = corpseCell.IsValid ? corpseCell : pawn.Position;
            UpdateFacingToward(pawn, executionPos);
            pawn.pather?.StopDead();
            if (pawn.CurJob != null) pawn.jobs?.StopAll();
            PulseGreenExecutionHorror(pawn, executionPos);
        }

        /// <summary>更新紫罗兰黎明自爆倒计时蓄力进度（0..1）。</summary>
        public void SetVioletChargeProgress(float progress)
        {
            chargeProgress = Mathf.Clamp01(progress);
        }

        private void TickAmberBurrow(Pawn pawn)
        {
            if (digInTicksLeft > 0)
            {
                pawn.pather?.StopDead();
                digInTicksLeft--;
                if (digInTicksLeft == 0)
                {
                    burrowedTicksLeft = 42;
                }
                return;
            }

            if (burrowedTicksLeft > 0)
            {
                pawn.pather?.StopDead();
                burrowedTicksLeft--;
                if (burrowedTicksLeft % 10 == 0 && pendingBurrowDest.IsValid)
                {
                    float frac = 1f - burrowedTicksLeft / 42f;
                    Vector3 trailPos = Vector3.Lerp(pawn.DrawPos, pendingBurrowDest.ToVector3Shifted(), frac);
                    FleckMaker.ThrowDustPuffThick(trailPos, pawn.Map, 1.2f, new Color(0.76f, 0.52f, 0.20f));
                }

                if (burrowedTicksLeft == 0)
                {
                    Map map = pawn.Map;
                    IntVec3 dest = pendingBurrowDest.IsValid && pendingBurrowDest.Standable(map)
                        ? pendingBurrowDest
                        : pawn.Position;
                    pendingBurrowDest = IntVec3.Invalid;
                    if (dest != pawn.Position)
                    {
                        pawn.Position = dest;
                        pawn.Notify_Teleported(endCurrentJob: true, resetTweenedPos: true);
                    }

                    digOutTicksLeft = 42;
                    OrdealDefOf.LCOrdeal_Sound_Bug1DigOut?.PlayOneShot(new TargetInfo(pawn.Position, map));
                    Color amberFx = new Color(0.92f, 0.58f, 0.18f, 0.90f);
                    OrdealVfxUtility.SpawnCrater(map, pawn.DrawPos, 2.8f, amberFx, 140);
                    OrdealVfxUtility.SpawnShockwave(map, pawn.DrawPos, 0.4f, 3.0f, amberFx, 30);
                    FleckMaker.ThrowDustPuffThick(pawn.DrawPos, map, 1.8f, new Color(0.75f, 0.52f, 0.22f));

                    // 破土瞬间对贴身目标造成一次红伤突袭撕咬（TETH 等级）
                    DamageDef redDmg = LCDamageDefOf.LC_RedDamage ?? DamageDefOf.Bite;
                    foreach (Pawn victim in OrdealTargetUtility.AllTargets(map))
                    {
                        if (!victim.Dead && victim.Position.InHorDistOf(pawn.Position, 2.2f))
                        {
                            victim.TakeDamage(new DamageInfo(redDmg, Rand.Range(10f, 14f), 0.1f, instigator: pawn, weapon: pawn.def));
                            OrdealVfxUtility.SpawnSlash(map, victim.DrawPos, 2.0f, amberFx, 20);
                        }
                    }
                }
                return;
            }

            if (digOutTicksLeft > 0)
            {
                pawn.pather?.StopDead();
                digOutTicksLeft--;
            }
        }

        private void TickCrimsonSabotage(Pawn pawn)
        {
            Map map = pawn.Map;
            if (teleportAnimTicksLeft > 0)
            {
                pawn.pather?.StopDead();
                return;
            }

            // 寻找有效收容平台目标（优先仍收容着且逆卡巴拉计数器 > 0 的平台）
            Building targetBuilding = FindCrimsonTargetBuilding(pawn, map);
            if (targetBuilding == null)
            {
                tamperTicksDone = 0;
                tamperTargetId = -1;
                return;
            }

            if (tamperTargetId != targetBuilding.thingIDNumber)
            {
                tamperTargetId = targetBuilding.thingIDNumber;
                tamperTicksDone = 0;
            }

            if (pawn.Position.InHorDistOf(targetBuilding.Position, 2.4f))
            {
                pawn.pather?.StopDead();
                if (pawn.CurJob != null) pawn.jobs?.StopAll();
                UpdateFacingToward(pawn, targetBuilding.Position);
                tamperTicksDone++;

                if (tamperTicksDone == 1 || tamperTicksDone % 75 == 0)
                {
                    OrdealDefOf.LCOrdeal_Sound_CircusClick?.PlayOneShot(new TargetInfo(pawn.Position, map));
                    OrdealVfxUtility.SpawnShockwave(map, targetBuilding.DrawPos, 0.5f, 2.6f, new Color(0.96f, 0.18f, 0.24f, 0.85f), 28);
                    OrdealVfxUtility.SpawnPawnAura(map, pawn, OrdealVfxUtility.TexHollowRing, 1.8f, new Color(0.96f, 0.18f, 0.24f, 0.78f), 70, rotateSpeed: 150f);
                }

                if (tamperTicksDone >= Props.crimsonTamperDurationTicks)
                {
                    CompleteCrimsonSabotage(pawn, targetBuilding, map);
                }
            }
            else
            {
                tamperTicksDone = 0;
                if (pawn.IsHashIntervalTick(60)
                    && (pawn.CurJobDef != JobDefOf.Goto || !pawn.CurJob.targetA.Cell.InHorDistOf(targetBuilding.Position, 2.5f)))
                {
                    IntVec3 standCell = CellFinder.RandomClosewalkCellNear(targetBuilding.Position, map, 2, c => c.Standable(map) && !c.Fogged(map));
                    if (standCell.IsValid && pawn.CanReach(standCell, PathEndMode.OnCell, Danger.Deadly))
                    {
                        Job go = JobMaker.MakeJob(JobDefOf.Goto, standCell);
                        go.locomotionUrgency = LocomotionUrgency.Sprint;
                        pawn.jobs.TryTakeOrderedJob(go);
                    }
                }
            }
        }

        private void CompleteCrimsonSabotage(Pawn pawn, Building targetBuilding, Map map)
        {
            tamperTicksDone = 0;
            tamperTargetId = -1;

            if (targetBuilding is Building_AbnormalityHoldingPlatform platform)
            {
                CompAbnormality abn = platform.HeldPawn?.TryGetComp<CompAbnormality>();
                if (abn != null && abn.QliphothEnabled && abn.QliphothCountCurrent > 0)
                {
                    abn.QliphothCountCurrent = 0;
                    Messages.Message("LCOrdeal_CrimsonSabotagedPlatform".Translate(platform.HeldPawn.LabelCap), platform, MessageTypeDefOf.NegativeEvent);
                }
            }
            else
            {
                targetBuilding.TakeDamage(new DamageInfo(LCDamageDefOf.LC_RedDamage ?? DamageDefOf.Bomb, 35f, 0.2f, instigator: pawn, weapon: pawn.def));
            }

            // 马戏团帐篷传送门转移到地图另一处收容区/边缘走廊
            OrdealDefOf.LCOrdeal_Sound_CircusTentOn?.PlayOneShot(new TargetInfo(pawn.Position, map));
            OrdealVfxUtility.SpawnCircusTent(map, pawn.DrawPos, 2.7f, 52);

            IntVec3 nextDest = IntVec3.Invalid;
            Building otherPlatform = map.listerBuildings.AllBuildingsColonistOfClass<Building_AbnormalityHoldingPlatform>()
                .Where(p => p != targetBuilding && p.HeldPawn?.TryGetComp<CompAbnormality>()?.QliphothCountCurrent > 0)
                .RandomElementWithFallback();
            if (otherPlatform != null)
            {
                nextDest = CellFinder.RandomClosewalkCellNear(otherPlatform.Position, map, 10, c => c.Standable(map) && !c.Fogged(map));
            }
            else if (RCellFinder.TryFindRandomPawnEntryCell(out IntVec3 edge, map, CellFinder.EdgeRoadChance_Hostile, true))
            {
                nextDest = edge;
            }

            if (nextDest.IsValid && nextDest != pawn.Position)
            {
                pawn.Position = nextDest;
                pawn.Notify_Teleported(endCurrentJob: true, resetTweenedPos: true);
                teleportAnimTicksLeft = 30;
                OrdealDefOf.LCOrdeal_Sound_CircusTentOff?.PlayOneShot(new TargetInfo(pawn.Position, map));
                OrdealVfxUtility.SpawnCircusTent(map, pawn.DrawPos, 2.7f, 52);
            }
        }

        private static Building FindCrimsonTargetBuilding(Pawn pawn, Map map)
        {
            List<Building_AbnormalityHoldingPlatform> activePlatforms = map.listerBuildings
                .AllBuildingsColonistOfClass<Building_AbnormalityHoldingPlatform>()
                .Where(p => p.Spawned && p.HeldPawn?.TryGetComp<CompAbnormality>() is CompAbnormality abn && abn.QliphothEnabled && abn.QliphothCountCurrent > 0)
                .ToList();
            if (activePlatforms.Count > 0)
            {
                return activePlatforms.MinBy(p => p.Position.DistanceToSquared(pawn.Position));
            }

            List<Building> powerBuildings = map.listerThings.AllThings
                .OfType<Building>()
                .Where(b => b.Spawned && b.Faction == Faction.OfPlayer && b.TryGetComp<CompPowerTrader>() != null)
                .ToList();
            return powerBuildings.Count > 0 ? powerBuildings.MinBy(b => b.Position.DistanceToSquared(pawn.Position)) : null;
        }

        private void TickGreenExecution(Pawn pawn)
        {
            if (executionTicksLeft <= 0)
            {
                return;
            }

            pawn.pather?.StopDead();
            if (pawn.CurJob != null) pawn.jobs?.StopAll();
            executionTicksLeft--;

            if (executionTicksLeft > 0 && executionTicksLeft % 45 == 0)
            {
                PulseGreenExecutionHorror(pawn, executionPos.IsValid ? executionPos : pawn.Position);
            }
            else if (executionTicksLeft == 0)
            {
                OrdealDefOf.LCOrdeal_Sound_MachineFinish?.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
            }
        }

        private static void PulseGreenExecutionHorror(Pawn greenKiller, IntVec3 pos)
        {
            Map map = greenKiller.Map;
            if (map == null) return;

            DawnOrdealSettings settings = DawnOrdealWorker.ActiveSettings();
            Vector3 fxPos = pos.ToVector3Shifted();
            OrdealDefOf.LCOrdeal_Sound_MachineBlood?.PlayOneShot(new TargetInfo(pos, map));
            OrdealVfxUtility.SpawnSlash(map, fxPos, 2.8f, new Color(0.96f, 0.16f, 0.22f, 0.94f), 20);
            OrdealVfxUtility.SpawnShockwave(map, fxPos, 0.5f, settings.greenExecutionPsychicRadius, new Color(0.92f, 0.98f, 0.92f, 0.80f), 32);

            DamageDef whiteDamage = LCDamageDefOf.LC_WhiteDamage ?? DamageDefOf.Psychic;
            foreach (Pawn victim in OrdealTargetUtility.AllTargets(map))
            {
                if (!victim.Dead && victim.Position.InHorDistOf(pos, settings.greenExecutionPsychicRadius))
                {
                    victim.TakeDamage(new DamageInfo(whiteDamage, settings.greenExecutionPsychicDamage, 0f, instigator: greenKiller));
                }
            }
        }

        private void TickVioletSporeAura(Pawn pawn)
        {
            int now = Find.TickManager.TicksGame;
            if (nextSporeTick < 0)
            {
                nextSporeTick = now + Props.violetSporeIntervalTicks;
                return;
            }

            if (now >= nextSporeTick)
            {
                nextSporeTick = now + Props.violetSporeIntervalTicks;
                Map map = pawn.Map;
                Color violetFx = new Color(0.68f, 0.24f, 0.96f, 0.85f);
                bool hitAny = false;
                DamageDef blackDmg = LCDamageDefOf.LC_BlackDamage ?? DamageDefOf.Psychic;
                foreach (Pawn victim in OrdealTargetUtility.AllTargets(map))
                {
                    if (!victim.Dead && victim.Position.InHorDistOf(pawn.Position, Props.violetSporeRadius))
                    {
                        victim.TakeDamage(new DamageInfo(blackDmg, Props.violetSporeDamage, 0f, instigator: pawn, weapon: pawn.def));
                        hitAny = true;
                    }
                }

                if (hitAny || chargeProgress > 0.5f)
                {
                    OrdealVfxUtility.SpawnShockwave(map, pawn.DrawPos, 0.5f, Props.violetSporeRadius, violetFx, 34);
                    if (hitAny)
                    {
                        OrdealDefOf.LCOrdeal_Sound_OutterGodAtk?.PlayOneShot(new TargetInfo(pawn.Position, map));
                    }
                }
            }
        }

        private void UpdateFacing(Pawn pawn)
        {
            if (pawn.pather != null && pawn.pather.Moving && pawn.pather.nextCell.IsValid)
            {
                int dx = pawn.pather.nextCell.x - pawn.Position.x;
                if (dx < 0) isFacingLeft = true;
                else if (dx > 0) isFacingLeft = false;
                return;
            }

            if (pawn.CurJob?.targetA.HasThing == true && pawn.CurJob.targetA.Thing.Spawned)
            {
                UpdateFacingToward(pawn, pawn.CurJob.targetA.Thing.Position);
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
            if (desired != null && pawn.Drawer?.renderer != null && pawn.Drawer.renderer.CurAnimation != desired)
            {
                pawn.Drawer.renderer.SetAnimation(desired);
            }
        }

        private AnimationDef ResolveDesiredAnimation(Pawn pawn)
        {
            bool moving = pawn.pather != null && pawn.pather.Moving;
            bool busyAttack = attackAnimTicksLeft > 0 || pawn.stances?.curStance is Stance_Busy;

            switch (Archetype)
            {
                case DawnOrdealArchetype.Amber:
                    if (digInTicksLeft > 0) return OrdealDefOf.LCOrdeal_AmberDawn_DigIn;
                    if (digOutTicksLeft > 0) return OrdealDefOf.LCOrdeal_AmberDawn_DigOut;
                    if (busyAttack) return OrdealDefOf.LCOrdeal_AmberDawn_Attack;
                    return moving ? OrdealDefOf.LCOrdeal_AmberDawn_Walk : OrdealDefOf.LCOrdeal_AmberDawn_Idle;

                case DawnOrdealArchetype.Crimson:
                    if (teleportAnimTicksLeft > 0) return OrdealDefOf.LCOrdeal_CrimsonDawn_Teleport;
                    if (tamperTicksDone > 0) return OrdealDefOf.LCOrdeal_CrimsonDawn_Tamper;
                    if (busyAttack) return OrdealDefOf.LCOrdeal_CrimsonDawn_Attack;
                    return moving ? OrdealDefOf.LCOrdeal_CrimsonDawn_Walk : OrdealDefOf.LCOrdeal_CrimsonDawn_Idle;

                case DawnOrdealArchetype.Green:
                    if (executionTicksLeft > 0) return OrdealDefOf.LCOrdeal_GreenDawn_Execution;
                    if (busyAttack) return attackAlternate ? OrdealDefOf.LCOrdeal_GreenDawn_Attack1 : OrdealDefOf.LCOrdeal_GreenDawn_Attack2;
                    return moving ? OrdealDefOf.LCOrdeal_GreenDawn_Walk : OrdealDefOf.LCOrdeal_GreenDawn_Idle;

                case DawnOrdealArchetype.Violet:
                    if (chargeProgress >= 0.72f) return OrdealDefOf.LCOrdeal_VioletDawn_Charge;
                    if (busyAttack) return OrdealDefOf.LCOrdeal_VioletDawn_Attack;
                    return moving ? OrdealDefOf.LCOrdeal_VioletDawn_Walk : OrdealDefOf.LCOrdeal_VioletDawn_Idle;

                default:
                    return null;
            }
        }
    }
}
