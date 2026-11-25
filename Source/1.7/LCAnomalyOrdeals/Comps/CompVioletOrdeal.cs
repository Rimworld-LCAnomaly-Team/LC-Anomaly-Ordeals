using LCAnomalyOrdeals.DefOfs;
using LCAnomalyOrdeals.Render;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LCAnomalyOrdeals.Comps
{
    /// <summary>紫罗兰高阶考验类型（正午石碑《请给我们爱！！！》 / 午夜四色祭坛《神圣的赋予》）。</summary>
    public enum VioletOrdealArchetype
    {
        NoonMonolith = 0,
        MidnightRed = 1,
        MidnightWhite = 2,
        MidnightBlack = 3,
        MidnightPale = 4
    }

    /// <summary>紫罗兰正午与紫罗兰午夜四色祭坛骨骼动画状态机配置。</summary>
    public class CompProperties_VioletOrdeal : CompProperties
    {
        public VioletOrdealArchetype archetype = VioletOrdealArchetype.NoonMonolith;

        public CompProperties_VioletOrdeal()
        {
            compClass = typeof(CompVioletOrdeal);
        }
    }

    /// <summary>
    /// 紫罗兰正午《请给我们爱！！！》(HE) 与紫罗兰午夜《神圣的赋予》(ALEPH 四色祭坛) 原生骨骼动画状态机：
    /// - 紫罗兰正午：从天而降砸落（Impact）+ 6 根漆黑触手从石碑背后爆裂狂舞（Cast）+ 铭文符文脉动；
    /// - 紫罗兰午夜四祭坛：红/白/黑/苍白神龛符文光环脉动，召唤对应颜色传送门与触手/苍白之眼时触发祭坛震颤爆发（Cast）。
    /// </summary>
    public class CompVioletOrdeal : ThingComp, IOrdealArticulatedComp
    {
        private int impactTicksLeft;
        private int castTicksLeft;

        public CompProperties_VioletOrdeal Props => (CompProperties_VioletOrdeal)props;
        public Pawn Pawn => parent as Pawn;
        public VioletOrdealArchetype Archetype => Props.archetype;

        public bool IsFacingLeft => false;
        public bool IsBurrowedHidden => false;
        public bool IsAlertVisual => castTicksLeft > 0 || impactTicksLeft > 0;
        public float ChargeProgress => castTicksLeft > 0 ? 0.85f : Mathf.PingPong(Find.TickManager.TicksGame * 0.006f, 0.35f);
        public float EmitterSpinAngle => 0f;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref impactTicksLeft, "impactTicksLeft", 0);
            Scribe_Values.Look(ref castTicksLeft, "castTicksLeft", 0);
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!respawningAfterLoad && Pawn != null && Pawn.Spawned)
            {
                if (Archetype == VioletOrdealArchetype.NoonMonolith)
                {
                    impactTicksLeft = 48;
                }
                else
                {
                    castTicksLeft = 50;
                    OrdealDefOf.LCOrdeal_Sound_VioletMidnightPortal?.PlayOneShot(new TargetInfo(Pawn.Position, Pawn.Map));
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

            if (impactTicksLeft > 0) impactTicksLeft--;
            if (castTicksLeft > 0) castTicksLeft--;

            UpdateAnimation(pawn);
        }

        /// <summary>触发紫罗兰正午六触手狂舞施法或紫罗兰午夜祭坛召唤脉冲动画。</summary>
        public void TriggerCast(int durationTicks = 56)
        {
            castTicksLeft = Mathf.Max(castTicksLeft, durationTicks);
        }

        private void UpdateAnimation(Pawn pawn)
        {
            AnimationDef desired = ResolveDesiredAnimation();
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

        private AnimationDef ResolveDesiredAnimation()
        {
            if (Archetype == VioletOrdealArchetype.NoonMonolith)
            {
                if (impactTicksLeft > 0) return OrdealDefOf.LCOrdeal_VioletNoon_Impact;
                if (castTicksLeft > 0) return OrdealDefOf.LCOrdeal_VioletNoon_Cast;
                return OrdealDefOf.LCOrdeal_VioletNoon_Idle;
            }

            return castTicksLeft > 0 ? OrdealDefOf.LCOrdeal_VioletShrine_Cast : OrdealDefOf.LCOrdeal_VioletShrine_Idle;
        }
    }
}
