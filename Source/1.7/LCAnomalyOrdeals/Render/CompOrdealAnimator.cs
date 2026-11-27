using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace LCAnomalyOrdeals.Render
{
    /// <summary>考验实体的动画动作状态。</summary>
    public enum OrdealAnimState
    {
        Idle = 0,
        Move = 1,
        Attack = 2,
        Special = 3
    }

    /// <summary>考验实体的程序化运动风格分类。</summary>
    public enum OrdealMotionStyle
    {
        Biped = 0,
        Crawler = 1,
        Float = 2,
        Roller = 3,
        Factory = 4,
        HelixTower = 5
    }

    /// <summary>考验实体动画组件配置。</summary>
    public class CompProperties_OrdealAnimator : CompProperties
    {
        public CompProperties_OrdealAnimator()
        {
            compClass = typeof(CompOrdealAnimator);
        }
    }

    /// <summary>
    /// 驱动 22 种考验实体的多帧 Spine 姿态切换与程序化辅助运动（呼吸、爬行蠕动、悬浮、攻击冲刺、特殊蓄力）。
    /// </summary>
    public class CompOrdealAnimator : ThingComp
    {
        private int attackTicksRemaining;
        private int specialTicksRemaining;
        private int phaseSeed;
        private float chargeIntensity;
        private Color tintOverride = Color.white;
        private int tintTicksRemaining;
        private string cachedVariantKey;
        private OrdealMotionStyle? cachedMotionStyle;

        public Pawn Pawn => parent as Pawn;

        public int PhaseSeed
        {
            get
            {
                if (phaseSeed == 0 && parent != null)
                {
                    phaseSeed = (parent.thingIDNumber * 397) & 0x7FFF;
                }
                return phaseSeed;
            }
        }

        public float ChargeIntensity => chargeIntensity;

        public Color CurrentTint => tintTicksRemaining > 0 ? tintOverride : Color.white;

        /// <summary>从 PawnKindDef 提取标准变体名（如 AmberDawn / WhiteClaw）。</summary>
        public string VariantKey
        {
            get
            {
                if (cachedVariantKey == null && Pawn?.kindDef != null)
                {
                    string defName = Pawn.kindDef.defName ?? string.Empty;
                    cachedVariantKey = defName.StartsWith("LCOrdeal_", StringComparison.Ordinal)
                        ? defName.Substring("LCOrdeal_".Length)
                        : defName;
                }
                return cachedVariantKey ?? string.Empty;
            }
        }

        public OrdealMotionStyle MotionStyle
        {
            get
            {
                if (!cachedMotionStyle.HasValue)
                {
                    cachedMotionStyle = ResolveMotionStyle(VariantKey);
                }
                return cachedMotionStyle.Value;
            }
        }

        public OrdealAnimState CurrentState
        {
            get
            {
                if (specialTicksRemaining > 0) return OrdealAnimState.Special;
                if (attackTicksRemaining > 0) return OrdealAnimState.Attack;
                if (Pawn?.stances?.curStance is Stance_Busy) return OrdealAnimState.Attack;
                if (Pawn?.pather != null && Pawn.pather.Moving) return OrdealAnimState.Move;
                return OrdealAnimState.Idle;
            }
        }

        /// <summary>触发一次攻击/施法姿态动画（自动切到 _attack 关键帧并叠加冲刺动作）。</summary>
        public void TriggerAttack(int durationTicks = 36)
        {
            attackTicksRemaining = Math.Max(attackTicksRemaining, durationTicks);
        }

        /// <summary>触发一次特殊状态动画（如翠绿正午过热、白收尾人祈祷、爪牙血清蓄力、紫罗兰午夜施法）。</summary>
        public void TriggerSpecial(int durationTicks, Color? tint = null, float intensity = 1f)
        {
            specialTicksRemaining = Math.Max(specialTicksRemaining, durationTicks);
            chargeIntensity = Mathf.Clamp01(intensity);
            if (tint.HasValue)
            {
                tintOverride = tint.Value;
                tintTicksRemaining = Math.Max(tintTicksRemaining, durationTicks);
            }
        }

        public void SetChargeIntensity(float intensity)
        {
            chargeIntensity = Mathf.Clamp01(intensity);
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!respawningAfterLoad && parent != null && parent.Spawned && parent.Map != null && !parent.Position.Fogged(parent.Map))
            {
                LCAnomalyCore.Util.AbnormalityUtility.Discover(parent.def);
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo g in base.CompGetGizmosExtra())
            {
                yield return g;
            }

            if (parent != null)
            {
                yield return LCAnomalyCore.Util.Gizmos.Get_EntityCodex(parent);
            }
        }

        public override void CompTick()
        {
            base.CompTick();
            if (attackTicksRemaining > 0) attackTicksRemaining--;
            if (specialTicksRemaining > 0)
            {
                specialTicksRemaining--;
                if (specialTicksRemaining == 0) chargeIntensity = 0f;
            }
            if (tintTicksRemaining > 0) tintTicksRemaining--;
        }

        public static void TriggerAttackOn(Pawn pawn, int durationTicks = 36)
        {
            pawn?.TryGetComp<CompOrdealAnimator>()?.TriggerAttack(durationTicks);
        }

        public static void TriggerSpecialOn(Pawn pawn, int durationTicks, Color? tint = null, float intensity = 1f)
        {
            pawn?.TryGetComp<CompOrdealAnimator>()?.TriggerSpecial(durationTicks, tint, intensity);
        }

        private static OrdealMotionStyle ResolveMotionStyle(string key)
        {
            switch (key)
            {
                case "AmberDawn":
                case "VioletDawn":
                case "CrimsonNoon":
                case "AmberDusk":
                case "AmberMidnight":
                    return OrdealMotionStyle.Crawler;

                case "VioletNoon":
                case "VioletMidnightRed":
                case "VioletMidnightWhite":
                case "VioletMidnightBlack":
                case "VioletMidnightPale":
                case "WhiteFixerWhite":
                    return OrdealMotionStyle.Float;

                case "CrimsonDusk":
                    return OrdealMotionStyle.Roller;

                case "GreenDusk":
                    return OrdealMotionStyle.Factory;

                case "GreenMidnight":
                    return OrdealMotionStyle.HelixTower;

                default:
                    return OrdealMotionStyle.Biped;
            }
        }
    }
}
