using LCAnomalyOrdeals.DefOfs;
using LCAnomalyOrdeals.Render;
using LCAnomalyOrdeals.Vfx;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LCAnomalyOrdeals.Comps
{
    /// <summary>翠绿色高阶考验层级（正午《理解的过程》 / 黄昏《前往何方》 / 午夜《终末螺旋》）。</summary>
    public enum GreenOrdealTier
    {
        Noon = 0,
        Dusk = 1,
        Midnight = 2
    }

    /// <summary>翠绿正午、黄昏工厂与午夜终末螺旋骨骼动画状态机配置。</summary>
    public class CompProperties_GreenOrdeal : CompProperties
    {
        public GreenOrdealTier tier = GreenOrdealTier.Noon;

        public CompProperties_GreenOrdeal()
        {
            compClass = typeof(CompGreenOrdeal);
        }
    }

    /// <summary>
    /// 翠绿高阶考验原生骨骼动画状态机：
    /// - 翠绿正午《理解的过程》(HE)：圆锯高速旋转 + 臂炮齐射（Attack）+ 过热下蹲重启（Cooldown）；
    /// - 翠绿黄昏《前往何方》(WAW)：工业炉齿轮轰鸣 + 绿色能量槽充能 + 舱门 90° 横向抬起出兵（Create）；
    /// - 翠绿午夜《终末螺旋》(ALEPH)：装甲穹顶向两侧裂开 + 翠绿发光激光发射器升起并随真实激光角度 360° 同步旋转（Fire）。
    /// </summary>
    public class CompGreenOrdeal : ThingComp, IOrdealArticulatedComp
    {
        private bool isFacingLeft;
        private int attackAnimTicksLeft;
        private int createAnimTicksLeft;
        private int firingTicksLeft;
        private int chargingTicksLeft;
        private float emitterSpinAngle;
        private float chargeProgress;

        public CompProperties_GreenOrdeal Props => (CompProperties_GreenOrdeal)props;
        public Pawn Pawn => parent as Pawn;
        public GreenOrdealTier Tier => Props.tier;

        public bool IsFacingLeft => Tier == GreenOrdealTier.Noon && isFacingLeft;
        public bool IsBurrowedHidden => false;
        public bool IsAlertVisual => Tier == GreenOrdealTier.Midnight ? (firingTicksLeft > 0 || chargingTicksLeft > 0) : (attackAnimTicksLeft > 0 || createAnimTicksLeft > 0);
        public float ChargeProgress => chargeProgress;
        public float EmitterSpinAngle => Tier == GreenOrdealTier.Midnight ? emitterSpinAngle : 0f;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref isFacingLeft, "isFacingLeft", false);
            Scribe_Values.Look(ref attackAnimTicksLeft, "attackAnimTicksLeft", 0);
            Scribe_Values.Look(ref createAnimTicksLeft, "createAnimTicksLeft", 0);
            Scribe_Values.Look(ref firingTicksLeft, "firingTicksLeft", 0);
            Scribe_Values.Look(ref chargingTicksLeft, "chargingTicksLeft", 0);
            Scribe_Values.Look(ref emitterSpinAngle, "emitterSpinAngle", 0f);
            Scribe_Values.Look(ref chargeProgress, "chargeProgress", 0f);
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!respawningAfterLoad && Pawn != null && Pawn.Spawned)
            {
                if (Tier == GreenOrdealTier.Noon)
                {
                    OrdealDefOf.LCOrdeal_Sound_MachineNoonDefault?.PlayOneShot(new TargetInfo(Pawn.Position, Pawn.Map));
                }
                else if (Tier == GreenOrdealTier.Dusk)
                {
                    OrdealDefOf.LCOrdeal_Sound_MachineDusk1?.PlayOneShot(new TargetInfo(Pawn.Position, Pawn.Map));
                }
                else if (Tier == GreenOrdealTier.Midnight)
                {
                    chargingTicksLeft = 90;
                    OrdealDefOf.LCOrdeal_Sound_MachineMidnightStart?.PlayOneShot(new TargetInfo(Pawn.Position, Pawn.Map));
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
            if (createAnimTicksLeft > 0) createAnimTicksLeft--;
            if (firingTicksLeft > 0) firingTicksLeft--;
            if (chargingTicksLeft > 0) chargingTicksLeft--;

            if (Tier == GreenOrdealTier.Noon)
            {
                UpdateFacing(pawn);
            }
            else if (Tier == GreenOrdealTier.Dusk)
            {
                chargeProgress = createAnimTicksLeft > 0 ? 0.9f : Mathf.PingPong(Find.TickManager.TicksGame * 0.004f, 0.65f);
            }
            else if (Tier == GreenOrdealTier.Midnight && firingTicksLeft <= 0 && chargingTicksLeft <= 0)
            {
                emitterSpinAngle = Mathf.LerpAngle(emitterSpinAngle, 0f, 0.06f);
            }

            UpdateAnimation(pawn);
        }

        public void NotifyMeleeAttack(Thing target)
        {
            Pawn pawn = Pawn;
            if (pawn == null || !pawn.Spawned || pawn.Map == null)
            {
                return;
            }

            if (Tier == GreenOrdealTier.Noon)
            {
                if (target != null)
                {
                    UpdateFacingToward(pawn, target.Position);
                }

                attackAnimTicksLeft = 40;
                Vector3 fxPos = target != null ? target.DrawPos : pawn.DrawPos;
                OrdealDefOf.LCOrdeal_Sound_MachineNoonAtk?.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                OrdealVfxUtility.SpawnSlash(pawn.Map, fxPos, 2.5f, new Color(0.28f, 0.96f, 0.44f, 0.92f), 20);
            }
        }

        /// <summary>翠绿黄昏工厂开启舱门部署机械单位（抬门 90° + 排气管脉冲）。</summary>
        public void TriggerFactoryCreate()
        {
            if (Tier != GreenOrdealTier.Dusk)
            {
                return;
            }

            createAnimTicksLeft = 66;
            chargeProgress = 1f;
        }

        /// <summary>翠绿午夜更新穹顶充能状态。</summary>
        public void NotifyMidnightCharge(int durationTicks = 50)
        {
            if (Tier != GreenOrdealTier.Midnight)
            {
                return;
            }

            chargingTicksLeft = Mathf.Max(chargingTicksLeft, durationTicks);
        }

        /// <summary>翠绿午夜同步 360° 终末螺旋发射器角度与翠绿激光发光状态。</summary>
        public void NotifyMidnightFire(float beamAngleDegrees, int durationTicks = 24)
        {
            if (Tier != GreenOrdealTier.Midnight)
            {
                return;
            }

            firingTicksLeft = Mathf.Max(firingTicksLeft, durationTicks);
            // RimWorld Top-down 激光角 0° 为正东，发射器立绘默认朝正北 (90°)，转换为顺时针屏幕旋转角
            emitterSpinAngle = 90f - beamAngleDegrees;
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
            if (Tier == GreenOrdealTier.Noon)
            {
                if (pawn.stances?.stunner?.Stunned == true)
                {
                    return OrdealDefOf.LCOrdeal_GreenNoon_Cooldown;
                }

                if (attackAnimTicksLeft > 0)
                {
                    return OrdealDefOf.LCOrdeal_GreenNoon_Attack;
                }

                bool moving = pawn.pather != null && pawn.pather.Moving;
                return moving ? OrdealDefOf.LCOrdeal_GreenNoon_Walk : OrdealDefOf.LCOrdeal_GreenNoon_Idle;
            }

            if (Tier == GreenOrdealTier.Dusk)
            {
                return createAnimTicksLeft > 0 ? OrdealDefOf.LCOrdeal_GreenDusk_Create : OrdealDefOf.LCOrdeal_GreenDusk_Idle;
            }

            if (firingTicksLeft > 0)
            {
                return OrdealDefOf.LCOrdeal_GreenMidnight_Fire;
            }

            if (chargingTicksLeft > 0)
            {
                return OrdealDefOf.LCOrdeal_GreenMidnight_Charge;
            }

            return OrdealDefOf.LCOrdeal_GreenMidnight_Idle;
        }
    }
}
