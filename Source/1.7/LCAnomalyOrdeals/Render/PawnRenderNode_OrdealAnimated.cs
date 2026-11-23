using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace LCAnomalyOrdeals.Render
{
    /// <summary>考验多帧动态渲染节点配置。</summary>
    public class PawnRenderNodeProperties_OrdealAnimated : PawnRenderNodeProperties
    {
        public PawnRenderNodeProperties_OrdealAnimated()
        {
            nodeClass = typeof(PawnRenderNode_OrdealAnimated);
            workerClass = typeof(PawnRenderNodeWorker_OrdealAnimated);
            pawnType = RenderNodePawnType.Any;
        }
    }

    /// <summary>
    /// 考验实体动态渲染节点：按动作状态（Idle / Move / Attack / Special）在主线程安全缓存并切换多帧 Spine 渲染贴图。
    /// </summary>
    public class PawnRenderNode_OrdealAnimated : PawnRenderNode
    {
        private readonly Dictionary<string, Graphic> graphicCache = new Dictionary<string, Graphic>();

        public PawnRenderNode_OrdealAnimated(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
            : base(pawn, props, tree)
        {
        }

        public override Graphic GraphicFor(Pawn pawn)
        {
            PawnKindLifeStage stage = pawn?.ageTracker?.CurKindLifeStage;
            return stage?.bodyGraphicData?.Graphic;
        }

        public Graphic GetOrLoadFrameGraphic(Pawn pawn, string suffix)
        {
            PawnKindLifeStage stage = pawn?.ageTracker?.CurKindLifeStage;
            GraphicData data = stage?.bodyGraphicData;
            if (data == null || data.texPath.NullOrEmpty())
            {
                return null;
            }

            string fullPath = string.IsNullOrEmpty(suffix) ? data.texPath : data.texPath + suffix;
            if (graphicCache.TryGetValue(fullPath, out Graphic cached))
            {
                return cached;
            }

            if (!UnityData.IsInMainThread)
            {
                return data.Graphic;
            }

            // 检查 _south 或 _east 是否存在，避免 ContentFinder 报错刷屏
            if (!string.IsNullOrEmpty(suffix)
                && ContentFinder<Texture2D>.Get(fullPath + "_east", reportFailure: false) == null
                && ContentFinder<Texture2D>.Get(fullPath + "_south", reportFailure: false) == null)
            {
                graphicCache[fullPath] = null;
                return null;
            }

            Graphic created = GraphicDatabase.Get<Graphic_Multi>(
                fullPath,
                ShaderDatabase.Cutout,
                data.drawSize,
                data.color,
                data.colorTwo);
            graphicCache[fullPath] = created;
            return created;
        }
    }

    /// <summary>
    /// 考验实体动态渲染 Worker：执行 Spine 关键帧切换 + 按生物类别的程序化位移、旋转、伸缩动画。
    /// </summary>
    public class PawnRenderNodeWorker_OrdealAnimated : PawnRenderNodeWorker
    {
        private static float AnimTime(CompOrdealAnimator comp)
        {
            float ticks = Find.TickManager != null ? Find.TickManager.TicksGame : 0f;
            return ticks / 60f + (comp != null ? comp.PhaseSeed * 0.017f : 0f);
        }

        public override Material GetFinalizedMaterial(PawnRenderNode node, PawnDrawParms parms)
        {
            Pawn pawn = parms.pawn;
            if (pawn != null && !pawn.Dead && node is PawnRenderNode_OrdealAnimated animNode)
            {
                CompOrdealAnimator comp = pawn.TryGetComp<CompOrdealAnimator>();
                if (comp != null)
                {
                    string suffix = SelectFrameSuffix(comp);
                    Graphic frameGraphic = animNode.GetOrLoadFrameGraphic(pawn, suffix)
                        ?? animNode.GetOrLoadFrameGraphic(pawn, string.Empty);
                    if (frameGraphic != null)
                    {
                        Material mat = frameGraphic.MatAt(parms.facing);
                        if (mat != null)
                        {
                            return mat;
                        }
                    }
                }
            }

            return base.GetFinalizedMaterial(node, parms);
        }

        public override Vector3 OffsetFor(PawnRenderNode node, PawnDrawParms parms, out Vector3 pivot)
        {
            Vector3 offset = base.OffsetFor(node, parms, out pivot);
            Pawn pawn = parms.pawn;
            if (pawn == null || pawn.Dead) return offset;

            CompOrdealAnimator comp = pawn.TryGetComp<CompOrdealAnimator>();
            if (comp == null) return offset;

            float t = AnimTime(comp);
            OrdealAnimState state = comp.CurrentState;
            switch (comp.MotionStyle)
            {
                case OrdealMotionStyle.Float:
                {
                    float speed = state == OrdealAnimState.Special ? 4.2f : 2.2f;
                    float amp = state == OrdealAnimState.Special ? 0.14f : 0.09f;
                    offset.z += Mathf.Sin(t * speed) * amp;
                    break;
                }
                case OrdealMotionStyle.Biped:
                {
                    if (state == OrdealAnimState.Move)
                    {
                        offset.z += Mathf.Abs(Mathf.Sin(t * 7.5f)) * 0.09f;
                    }
                    else if (state == OrdealAnimState.Attack)
                    {
                        float dir = parms.facing == Rot4.West ? -1f : 1f;
                        offset.x += Mathf.Sin(t * 14f) * 0.12f * dir;
                        offset.z += Mathf.Abs(Mathf.Sin(t * 14f)) * 0.06f;
                    }
                    else if (state == OrdealAnimState.Special)
                    {
                        offset.x += Mathf.Sin(t * 28f) * 0.03f * (0.4f + comp.ChargeIntensity);
                    }
                    break;
                }
                case OrdealMotionStyle.Crawler:
                {
                    if (state == OrdealAnimState.Move)
                    {
                        offset.z += Mathf.Abs(Mathf.Sin(t * 5.2f)) * 0.05f;
                    }
                    else if (state == OrdealAnimState.Special)
                    {
                        offset.x += Mathf.Sin(t * 22f) * 0.04f;
                    }
                    break;
                }
                case OrdealMotionStyle.Roller:
                {
                    if (state == OrdealAnimState.Move || state == OrdealAnimState.Attack)
                    {
                        offset.z += Mathf.Abs(Mathf.Sin(t * 8.5f)) * 0.12f;
                    }
                    break;
                }
                case OrdealMotionStyle.Factory:
                case OrdealMotionStyle.HelixTower:
                {
                    if (state == OrdealAnimState.Special || state == OrdealAnimState.Attack)
                    {
                        offset.x += Mathf.Sin(t * 32f) * 0.04f;
                        offset.z += Mathf.Cos(t * 26f) * 0.025f;
                    }
                    break;
                }
            }

            return offset;
        }

        public override Quaternion RotationFor(PawnRenderNode node, PawnDrawParms parms)
        {
            Quaternion rot = base.RotationFor(node, parms);
            Pawn pawn = parms.pawn;
            if (pawn == null || pawn.Dead) return rot;

            CompOrdealAnimator comp = pawn.TryGetComp<CompOrdealAnimator>();
            if (comp == null) return rot;

            float t = AnimTime(comp);
            OrdealAnimState state = comp.CurrentState;
            float angle = 0f;

            switch (comp.MotionStyle)
            {
                case OrdealMotionStyle.Biped:
                    if (state == OrdealAnimState.Move) angle = Mathf.Sin(t * 7.5f) * 4.5f;
                    else if (state == OrdealAnimState.Attack) angle = Mathf.Sin(t * 14f) * 9.5f;
                    break;
                case OrdealMotionStyle.Crawler:
                    if (state == OrdealAnimState.Move) angle = Mathf.Sin(t * 5.2f) * 3.5f;
                    break;
                case OrdealMotionStyle.Roller:
                    if (state == OrdealAnimState.Move || state == OrdealAnimState.Attack) angle = Mathf.Sin(t * 9.0f) * 11f;
                    break;
                case OrdealMotionStyle.Float:
                    angle = Mathf.Sin(t * 1.8f) * 2.2f;
                    break;
            }

            return Mathf.Abs(angle) > 0.01f ? rot * Quaternion.AngleAxis(angle, Vector3.up) : rot;
        }

        public override Vector3 ScaleFor(PawnRenderNode node, PawnDrawParms parms)
        {
            Vector3 scale = base.ScaleFor(node, parms);
            Pawn pawn = parms.pawn;
            if (pawn == null || pawn.Dead) return scale;

            CompOrdealAnimator comp = pawn.TryGetComp<CompOrdealAnimator>();
            if (comp == null) return scale;

            float t = AnimTime(comp);
            OrdealAnimState state = comp.CurrentState;
            float sx = 1f;
            float sz = 1f;

            switch (comp.MotionStyle)
            {
                case OrdealMotionStyle.Crawler:
                {
                    float freq = state == OrdealAnimState.Move ? 5.6f : 2.6f;
                    float amp = state == OrdealAnimState.Move ? 0.08f : 0.045f;
                    if (comp.VariantKey == "VioletDawn")
                    {
                        freq += comp.ChargeIntensity * 5.0f;
                        amp += comp.ChargeIntensity * 0.12f;
                    }
                    float wave = Mathf.Sin(t * freq);
                    sx = 1f + wave * amp;
                    sz = 1f - wave * amp * 0.8f;
                    break;
                }
                case OrdealMotionStyle.Factory:
                {
                    float amp = state == OrdealAnimState.Special ? 0.06f : 0.025f;
                    float wave = Mathf.Sin(t * (state == OrdealAnimState.Special ? 8f : 3f));
                    sx = 1f + wave * amp;
                    sz = 1f - wave * amp * 0.7f;
                    break;
                }
                case OrdealMotionStyle.HelixTower:
                {
                    float pulse = Mathf.Sin(t * (state == OrdealAnimState.Attack ? 10f : 3.2f)) * 0.035f;
                    sx = 1f + pulse;
                    sz = 1f + pulse;
                    break;
                }
                case OrdealMotionStyle.Biped:
                case OrdealMotionStyle.Float:
                {
                    float pulse = Mathf.Sin(t * 2.8f) * 0.025f;
                    sx = 1f - pulse * 0.5f;
                    sz = 1f + pulse;
                    if (state == OrdealAnimState.Special)
                    {
                        float charge = 1f + Mathf.Sin(t * 9f) * 0.045f * (0.5f + comp.ChargeIntensity);
                        sx *= charge;
                        sz *= charge;
                    }
                    break;
                }
            }

            return new Vector3(scale.x * sx, scale.y, scale.z * sz);
        }

        private static string SelectFrameSuffix(CompOrdealAnimator comp)
        {
            OrdealAnimState state = comp.CurrentState;
            if (state == OrdealAnimState.Special)
            {
                return "_special";
            }
            if (state == OrdealAnimState.Attack)
            {
                return "_attack";
            }

            // Idle/Move 在默认帧与两个 Spine 关键帧之间平滑循环
            int tick = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            int stepTicks = state == OrdealAnimState.Move ? 11 : 22;
            int phase = ((tick + comp.PhaseSeed) / stepTicks) % 4;
            switch (phase)
            {
                case 1: return "_move1";
                case 2: return "_move2";
                case 3: return "_move1";
                default: return string.Empty;
            }
        }
    }
}
