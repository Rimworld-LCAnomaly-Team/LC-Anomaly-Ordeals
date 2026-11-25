using System.Collections.Generic;
using LCAnomalyOrdeals.Comps;
using RimWorld;
using UnityEngine;
using Verse;

namespace LCAnomalyOrdeals.Render
{
    /// <summary>
    /// 考验多部件 Spine 骨骼渲染节点属性定义（对齐异想体 PawnRenderTreeDef 标准）。
    /// 支持左右水平镜像贴图、警戒/狂暴切换贴图、透明发光核心、钻地隐藏、条件显示（如翠绿午夜激光核心）与独立旋转角度（360°激光塔）。
    /// </summary>
    public class PawnRenderNodeProperties_DawnPart : PawnRenderNodeProperties
    {
        public bool isGroupRoot = false;
        public float drawScale = 1.65f;
        public string texPathLeft;
        public string altTexPath;
        public string altTexPathLeft;
        public bool useAltWhenAlert = false;
        public bool useTransparentShader = false;
        public bool isChargeCore = false;
        public bool showOnlyWhenAlert = false;
        public bool useEmitterSpin = false;

        public PawnRenderNodeProperties_DawnPart()
        {
            nodeClass = typeof(PawnRenderNode_DawnPart);
            workerClass = typeof(PawnRenderNodeWorker_DawnPart);
            pawnType = RenderNodePawnType.Any;
        }

        public override void ResolveReferences()
        {
            base.ResolveReferences();
            if (isGroupRoot)
            {
                useGraphic = false;
            }
        }
    }

    /// <summary>考验多部件 Spine 骨骼渲染节点。</summary>
    public class PawnRenderNode_DawnPart : PawnRenderNode
    {
        private readonly Dictionary<string, Graphic> cachedGraphics = new Dictionary<string, Graphic>();

        public PawnRenderNodeProperties_DawnPart PartProps => Props as PawnRenderNodeProperties_DawnPart;

        public PawnRenderNode_DawnPart(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
            : base(pawn, props, tree)
        {
        }

        public override bool FlipGraphic(PawnDrawParms parms) => false;

        public static IOrdealArticulatedComp FindArticulatedComp(Pawn pawn)
        {
            if (pawn?.AllComps == null)
            {
                return null;
            }

            List<ThingComp> comps = pawn.AllComps;
            for (int i = 0; i < comps.Count; i++)
            {
                if (comps[i] is IOrdealArticulatedComp articulated)
                {
                    return articulated;
                }
            }

            return null;
        }

        public bool ShouldUseLeftFacing(PawnDrawParms parms)
        {
            if (parms.Portrait)
            {
                return false;
            }

            IOrdealArticulatedComp comp = FindArticulatedComp(parms.pawn);
            if (comp != null)
            {
                return comp.IsFacingLeft;
            }

            return parms.facing == Rot4.West;
        }

        public override Mesh GetMesh(PawnDrawParms parms)
        {
            return meshSet?.MeshAt(Rot4.South);
        }

        public override GraphicMeshSet MeshSetFor(Pawn pawn)
        {
            if (PartProps != null && (PartProps.isGroupRoot || !PartProps.useGraphic))
            {
                return null;
            }

            return MeshPool.GetMeshSetForSize(1f, 1f);
        }

        public Graphic GetCachedGraphic(string path)
        {
            if (path.NullOrEmpty())
            {
                return null;
            }

            if (cachedGraphics.TryGetValue(path, out Graphic existing) && existing != null)
            {
                return existing;
            }

            if (!UnityData.IsInMainThread)
            {
                return null;
            }

            Shader shader = (PartProps != null && PartProps.useTransparentShader)
                ? ShaderDatabase.Transparent
                : ShaderDatabase.Cutout;

            Graphic created = GraphicDatabase.Get<Graphic_Single>(path, shader, Vector2.one, Color.white);
            if (created != null)
            {
                cachedGraphics[path] = created;
            }

            return created;
        }

        public Graphic ResolveGraphic(PawnDrawParms parms)
        {
            PawnRenderNodeProperties_DawnPart p = PartProps;
            if (p == null || p.isGroupRoot || !p.useGraphic)
            {
                return null;
            }

            bool left = ShouldUseLeftFacing(parms);
            bool useAlt = false;
            if (!parms.Portrait && p.useAltWhenAlert)
            {
                IOrdealArticulatedComp comp = FindArticulatedComp(parms.pawn);
                if (comp != null && comp.IsAlertVisual)
                {
                    useAlt = true;
                }
            }

            string path;
            if (useAlt && (!p.altTexPath.NullOrEmpty() || !p.altTexPathLeft.NullOrEmpty()))
            {
                path = left && !p.altTexPathLeft.NullOrEmpty() ? p.altTexPathLeft : p.altTexPath;
            }
            else
            {
                path = left && !p.texPathLeft.NullOrEmpty() ? p.texPathLeft : p.texPath;
            }

            return GetCachedGraphic(path);
        }

        public override Graphic GraphicFor(Pawn pawn)
        {
            PawnRenderNodeProperties_DawnPart p = PartProps;
            if (p == null || p.isGroupRoot || !p.useGraphic || p.texPath.NullOrEmpty())
            {
                return null;
            }

            return GetCachedGraphic(p.texPath);
        }

        protected override IEnumerable<Graphic> GraphicsFor(Pawn pawn)
        {
            PawnRenderNodeProperties_DawnPart p = PartProps;
            if (p == null || p.isGroupRoot || !p.useGraphic)
            {
                yield break;
            }

            foreach (string candidate in new[] { p.texPath, p.texPathLeft, p.altTexPath, p.altTexPathLeft })
            {
                Graphic g = GetCachedGraphic(candidate);
                if (g != null)
                {
                    yield return g;
                }
            }
        }
    }

    /// <summary>考验多部件 Spine 渲染节点工作者：处理钻地隐身、警戒显示/切图、左右对称翻转、核心蓄力膨胀与 360° 发射器旋转。</summary>
    public class PawnRenderNodeWorker_DawnPart : PawnRenderNodeWorker
    {
        public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
        {
            if (!base.CanDrawNow(node, parms))
            {
                return false;
            }

            if (!parms.Portrait)
            {
                IOrdealArticulatedComp comp = PawnRenderNode_DawnPart.FindArticulatedComp(parms.pawn);
                if (comp != null)
                {
                    if (comp.IsBurrowedHidden)
                    {
                        return false;
                    }

                    PawnRenderNodeProperties_DawnPart p = (node as PawnRenderNode_DawnPart)?.PartProps;
                    if (p != null && p.showOnlyWhenAlert && !comp.IsAlertVisual)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        protected override Material GetMaterial(PawnRenderNode node, PawnDrawParms parms)
        {
            PawnRenderNode_DawnPart dNode = node as PawnRenderNode_DawnPart;
            if (dNode != null)
            {
                Graphic g = dNode.ResolveGraphic(parms);
                if (g != null)
                {
                    PawnDrawParms matParms = parms;
                    matParms.facing = Rot4.South;
                    Material mat = g.NodeGetMat(matParms);
                    if (mat != null && !parms.Portrait && parms.flags.FlagSet(PawnRenderFlags.Invisible))
                    {
                        mat = InvisibilityMatPool.GetInvisibleMat(mat);
                    }
                    if (mat != null)
                    {
                        return mat;
                    }
                }
            }

            parms.facing = Rot4.South;
            return base.GetMaterial(node, parms);
        }

        public override Vector3 OffsetFor(PawnRenderNode node, PawnDrawParms parms, out Vector3 pivot)
        {
            Vector3 offset = base.OffsetFor(node, parms, out pivot);
            PawnRenderNode_DawnPart dNode = node as PawnRenderNode_DawnPart;
            if (dNode != null && dNode.ShouldUseLeftFacing(parms))
            {
                offset.x = -offset.x;
                pivot.x = -pivot.x;
            }

            return offset;
        }

        public override Quaternion RotationFor(PawnRenderNode node, PawnDrawParms parms)
        {
            float angle = node.DebugAngleOffset;
            if (node.Props.drawData != null)
            {
                angle += node.Props.drawData.RotationOffsetForRot(parms.facing);
            }

            if (!parms.flags.FlagSet(PawnRenderFlags.Portrait) && node.TryGetAnimationRotation(parms, out float animRot))
            {
                angle += animRot;
            }

            PawnRenderNode_DawnPart dNode = node as PawnRenderNode_DawnPart;
            PawnRenderNodeProperties_DawnPart p = dNode?.PartProps;
            if (!parms.Portrait && p != null && p.useEmitterSpin)
            {
                IOrdealArticulatedComp comp = PawnRenderNode_DawnPart.FindArticulatedComp(parms.pawn);
                if (comp != null)
                {
                    angle += comp.EmitterSpinAngle;
                }
            }

            if (dNode != null && dNode.ShouldUseLeftFacing(parms))
            {
                angle = -angle;
            }

            return Quaternion.AngleAxis(angle, Vector3.up);
        }

        public override Vector3 ScaleFor(PawnRenderNode node, PawnDrawParms parms)
        {
            Vector3 scale = base.ScaleFor(node, parms);
            PawnRenderNodeProperties_DawnPart p = (node as PawnRenderNode_DawnPart)?.PartProps;
            if (p == null)
            {
                return scale;
            }

            if (p.isGroupRoot)
            {
                float s = p.drawScale;
                if (!parms.Portrait)
                {
                    IOrdealArticulatedComp comp = PawnRenderNode_DawnPart.FindArticulatedComp(parms.pawn);
                    if (comp != null && comp.ChargeProgress > 0.05f)
                    {
                        s *= 1f + comp.ChargeProgress * 0.24f;
                    }
                }

                if (!Mathf.Approximately(s, 1f))
                {
                    scale.x *= s;
                    scale.z *= s;
                }
            }
            else if (p.isChargeCore && !parms.Portrait)
            {
                IOrdealArticulatedComp comp = PawnRenderNode_DawnPart.FindArticulatedComp(parms.pawn);
                if (comp != null)
                {
                    float pulse = 1f + comp.ChargeProgress * 0.35f;
                    scale.x *= pulse;
                    scale.z *= pulse;
                }
            }

            return scale;
        }
    }
}
