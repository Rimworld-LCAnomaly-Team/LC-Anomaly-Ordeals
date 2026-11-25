using LCAnomalyOrdeals.DefOfs;
using LCAnomalyOrdeals.Render;
using LCAnomalyOrdeals.Vfx;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LCAnomalyOrdeals.Comps
{
    /// <summary>靛蓝正午（清道夫）与白昼考验（四色收尾人 + 爪牙）实体原型枚举。</summary>
    public enum WhiteOrIndigoArchetype
    {
        IndigoSweeper = 0,
        FixerRed = 1,
        FixerWhite = 2,
        FixerBlack = 3,
        FixerPale = 4,
        TheClaw = 5
    }

    /// <summary>清道夫、四色收尾人与爪牙多部件骨骼动画状态机配置。</summary>
    public class CompProperties_WhiteAndIndigoOrdeal : CompProperties
    {
        public WhiteOrIndigoArchetype archetype = WhiteOrIndigoArchetype.IndigoSweeper;

        public CompProperties_WhiteAndIndigoOrdeal()
        {
            compClass = typeof(CompWhiteAndIndigoOrdeal);
        }
    }

    /// <summary>
    /// 靛蓝正午《清道夫》(HE)、白昼四色收尾人 (WAW) 与白昼午夜《爪牙》(ALEPH) 原生骨骼动画状态机：
    /// - 清道夫：双钩撕裂（Attack）+ 蹲伏吞噬尸体回满血（Eat）；
    /// - 四色收尾人：红/白/黑/苍白专属武器骨骼挥击与光束/震荡/传送蓄力（Attack / Special）；
    /// - 爪牙：机械巨爪前冲斩击（Attack）+ 红/蓝/绿血清注射准备与冲锋（Serum）+ 打断后跪地硬直（Groggy）。
    /// </summary>
    public class CompWhiteAndIndigoOrdeal : ThingComp, IOrdealArticulatedComp
    {
        private bool isFacingLeft;
        private int attackAnimTicksLeft;
        private int specialAnimTicksLeft;
        private int eatAnimTicksLeft;
        private int groggyAnimTicksLeft;

        public CompProperties_WhiteAndIndigoOrdeal Props => (CompProperties_WhiteAndIndigoOrdeal)props;
        public Pawn Pawn => parent as Pawn;
        public WhiteOrIndigoArchetype Archetype => Props.archetype;

        public bool IsFacingLeft => isFacingLeft;
        public bool IsBurrowedHidden => false;
        public bool IsAlertVisual => specialAnimTicksLeft > 0 || attackAnimTicksLeft > 0;
        public float ChargeProgress => specialAnimTicksLeft > 0 ? 0.6f : 0f;
        public float EmitterSpinAngle => 0f;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref isFacingLeft, "isFacingLeft", false);
            Scribe_Values.Look(ref attackAnimTicksLeft, "attackAnimTicksLeft", 0);
            Scribe_Values.Look(ref specialAnimTicksLeft, "specialAnimTicksLeft", 0);
            Scribe_Values.Look(ref eatAnimTicksLeft, "eatAnimTicksLeft", 0);
            Scribe_Values.Look(ref groggyAnimTicksLeft, "groggyAnimTicksLeft", 0);
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
            if (specialAnimTicksLeft > 0) specialAnimTicksLeft--;
            if (eatAnimTicksLeft > 0) eatAnimTicksLeft--;
            if (groggyAnimTicksLeft > 0) groggyAnimTicksLeft--;

            UpdateFacing(pawn);
            UpdateAnimation(pawn);
        }

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

            attackAnimTicksLeft = Archetype == WhiteOrIndigoArchetype.TheClaw ? 44 : 40;
            Vector3 fxPos = target != null ? target.DrawPos : pawn.DrawPos;
            switch (Archetype)
            {
                case WhiteOrIndigoArchetype.IndigoSweeper:
                    OrdealDefOf.LCOrdeal_Sound_ScavengerAtk?.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                    OrdealVfxUtility.SpawnSlash(pawn.Map, fxPos, 2.2f, new Color(0.85f, 0.16f, 0.30f, 0.92f), 20);
                    break;
                case WhiteOrIndigoArchetype.FixerRed:
                    OrdealDefOf.LCOrdeal_Sound_FixerRedAtk?.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                    OrdealVfxUtility.SpawnSlash(pawn.Map, fxPos, 2.6f, new Color(0.98f, 0.18f, 0.22f, 0.94f), 22);
                    break;
                case WhiteOrIndigoArchetype.FixerWhite:
                    OrdealDefOf.LCOrdeal_Sound_FixerWhiteAtk?.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                    OrdealVfxUtility.SpawnShockwave(pawn.Map, fxPos, 0.4f, 2.4f, new Color(1f, 0.98f, 0.90f, 0.90f), 22);
                    break;
                case WhiteOrIndigoArchetype.FixerBlack:
                    OrdealDefOf.LCOrdeal_Sound_FixerBlackAtk?.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                    OrdealVfxUtility.SpawnShockwave(pawn.Map, fxPos, 0.5f, 2.8f, new Color(0.60f, 0.22f, 0.96f, 0.92f), 24);
                    break;
                case WhiteOrIndigoArchetype.FixerPale:
                    OrdealDefOf.LCOrdeal_Sound_FixerPaleAtk?.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                    OrdealVfxUtility.SpawnSlash(pawn.Map, fxPos, 2.8f, new Color(0.42f, 0.96f, 0.98f, 0.94f), 22);
                    break;
                case WhiteOrIndigoArchetype.TheClaw:
                    OrdealDefOf.LCOrdeal_Sound_ClawStrong?.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                    OrdealVfxUtility.SpawnSlash(pawn.Map, fxPos, 3.2f, new Color(0.96f, 0.22f, 0.24f, 0.95f), 24);
                    break;
            }
        }

        public void TriggerEat(int durationTicks = 48)
        {
            eatAnimTicksLeft = Mathf.Max(eatAnimTicksLeft, durationTicks);
        }

        public void TriggerSpecial(int durationTicks = 50, IntVec3? faceCell = null)
        {
            if (faceCell.HasValue && faceCell.Value.IsValid && Pawn != null)
            {
                UpdateFacingToward(Pawn, faceCell.Value);
            }

            specialAnimTicksLeft = Mathf.Max(specialAnimTicksLeft, durationTicks);
            if (Archetype == WhiteOrIndigoArchetype.TheClaw && Pawn != null && Pawn.Spawned)
            {
                OrdealDefOf.LCOrdeal_Sound_ClawInjection?.PlayOneShot(new TargetInfo(Pawn.Position, Pawn.Map));
            }
        }

        public void TriggerAttack(int durationTicks = 44, IntVec3? faceCell = null)
        {
            if (faceCell.HasValue && faceCell.Value.IsValid && Pawn != null)
            {
                UpdateFacingToward(Pawn, faceCell.Value);
            }

            attackAnimTicksLeft = Mathf.Max(attackAnimTicksLeft, durationTicks);
        }

        public void TriggerGroggy(int durationTicks = 60)
        {
            groggyAnimTicksLeft = Mathf.Max(groggyAnimTicksLeft, durationTicks);
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
            switch (Archetype)
            {
                case WhiteOrIndigoArchetype.IndigoSweeper:
                    if (eatAnimTicksLeft > 0) return OrdealDefOf.LCOrdeal_IndigoNoon_Eat;
                    if (attackAnimTicksLeft > 0) return OrdealDefOf.LCOrdeal_IndigoNoon_Attack;
                    return moving ? OrdealDefOf.LCOrdeal_IndigoNoon_Walk : OrdealDefOf.LCOrdeal_IndigoNoon_Idle;
                case WhiteOrIndigoArchetype.FixerRed:
                    if (specialAnimTicksLeft > 0) return OrdealDefOf.LCOrdeal_FixerRed_Special;
                    if (attackAnimTicksLeft > 0) return OrdealDefOf.LCOrdeal_FixerRed_Attack;
                    return moving ? OrdealDefOf.LCOrdeal_FixerRed_Walk : OrdealDefOf.LCOrdeal_FixerRed_Idle;
                case WhiteOrIndigoArchetype.FixerWhite:
                    if (specialAnimTicksLeft > 0) return OrdealDefOf.LCOrdeal_FixerWhite_Special;
                    if (attackAnimTicksLeft > 0) return OrdealDefOf.LCOrdeal_FixerWhite_Attack;
                    return moving ? OrdealDefOf.LCOrdeal_FixerWhite_Walk : OrdealDefOf.LCOrdeal_FixerWhite_Idle;
                case WhiteOrIndigoArchetype.FixerBlack:
                    if (attackAnimTicksLeft > 0 || specialAnimTicksLeft > 0) return OrdealDefOf.LCOrdeal_FixerBlack_Attack;
                    return moving ? OrdealDefOf.LCOrdeal_FixerBlack_Walk : OrdealDefOf.LCOrdeal_FixerBlack_Idle;
                case WhiteOrIndigoArchetype.FixerPale:
                    if (attackAnimTicksLeft > 0 || specialAnimTicksLeft > 0) return OrdealDefOf.LCOrdeal_FixerPale_Attack;
                    return moving ? OrdealDefOf.LCOrdeal_FixerPale_Walk : OrdealDefOf.LCOrdeal_FixerPale_Idle;
                default:
                    if (groggyAnimTicksLeft > 0 || pawn.stances?.stunner?.Stunned == true) return OrdealDefOf.LCOrdeal_WhiteClaw_Groggy;
                    if (attackAnimTicksLeft > 0) return OrdealDefOf.LCOrdeal_WhiteClaw_Attack;
                    if (specialAnimTicksLeft > 0) return OrdealDefOf.LCOrdeal_WhiteClaw_Serum;
                    return moving ? OrdealDefOf.LCOrdeal_WhiteClaw_Walk : OrdealDefOf.LCOrdeal_WhiteClaw_Idle;
            }
        }
    }
}
