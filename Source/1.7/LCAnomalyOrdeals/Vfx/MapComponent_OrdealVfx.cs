using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace LCAnomalyOrdeals.Vfx
{
    internal enum OrdealVfxKind
    {
        Beam,
        ExpandingRing,
        Portal,
        StrikeSprite,
        PawnAura,
        FanSweep
    }

    internal sealed class OrdealVfxInstance
    {
        public OrdealVfxKind Kind;
        public string TexPath;
        public string CoreTexPath;
        public Vector3 Origin;
        public float AngleDegrees;
        public float Length;
        public float Width;
        public float StartRadius;
        public float EndRadius;
        public float ArcDegrees;
        public float RotateSpeed;
        public float RiseHeight;
        public Color Color;
        public int StartTick;
        public int DurationTicks;
        public bool Additive;
        public Pawn AttachedPawn;
    }

    /// <summary>
    /// 考验实时特效地图组件：负责在主渲染周期绘制激光束、传送门、冲击波环、地面预警、触手/苍白之眼、白雾与血清光环。
    /// </summary>
    public sealed class MapComponent_OrdealVfx : MapComponent
    {
        private readonly List<OrdealVfxInstance> activeEffects = new List<OrdealVfxInstance>();
        private static readonly Dictionary<string, Material> MaterialCache = new Dictionary<string, Material>();

        public MapComponent_OrdealVfx(Map map) : base(map)
        {
        }

        internal int ActiveCount => activeEffects.Count;

        internal void Add(OrdealVfxInstance instance)
        {
            if (instance == null || instance.DurationTicks <= 0)
            {
                return;
            }

            instance.StartTick = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            activeEffects.Add(instance);
            if (activeEffects.Count > 240)
            {
                activeEffects.RemoveRange(0, activeEffects.Count - 240);
            }
        }

        public override void MapComponentUpdate()
        {
            base.MapComponentUpdate();
            if (activeEffects.Count == 0 || Find.CurrentMap != map || Find.TickManager == null)
            {
                return;
            }

            int now = Find.TickManager.TicksGame;
            float altBase = AltitudeLayer.MoteOverhead.AltitudeFor();

            for (int i = activeEffects.Count - 1; i >= 0; i--)
            {
                OrdealVfxInstance fx = activeEffects[i];
                int elapsed = now - fx.StartTick;
                if (elapsed >= fx.DurationTicks
                    || (fx.Kind == OrdealVfxKind.PawnAura && (fx.AttachedPawn == null || fx.AttachedPawn.Dead || !fx.AttachedPawn.Spawned)))
                {
                    activeEffects.RemoveAt(i);
                    continue;
                }

                float progress = Mathf.Clamp01(elapsed / (float)fx.DurationTicks);
                DrawEffect(fx, progress, altBase + (i % 16) * 0.002f);
            }
        }

        private static void DrawEffect(OrdealVfxInstance fx, float progress, float altitude)
        {
            switch (fx.Kind)
            {
                case OrdealVfxKind.Beam:
                    DrawBeam(fx, progress, altitude);
                    break;
                case OrdealVfxKind.ExpandingRing:
                    DrawExpandingRing(fx, progress, altitude);
                    break;
                case OrdealVfxKind.Portal:
                    DrawPortal(fx, progress, altitude);
                    break;
                case OrdealVfxKind.StrikeSprite:
                    DrawStrikeSprite(fx, progress, altitude);
                    break;
                case OrdealVfxKind.PawnAura:
                    DrawPawnAura(fx, progress, altitude);
                    break;
                case OrdealVfxKind.FanSweep:
                    DrawFanSweep(fx, progress, altitude);
                    break;
            }
        }

        private static void DrawBeam(OrdealVfxInstance fx, float progress, float altitude)
        {
            float alpha = progress < 0.15f ? progress / 0.15f : 1f - Mathf.Pow((progress - 0.15f) / 0.85f, 1.4f);
            alpha = Mathf.Clamp01(alpha) * fx.Color.a;
            if (alpha <= 0.02f) return;

            float rad = fx.AngleDegrees * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
            Vector3 center = fx.Origin + dir * (fx.Length * 0.5f);
            center.y = altitude;

            // RimWorld MeshPool.plane10 在 XZ 平面，+X 轴旋转角为 -AngleDegrees
            Quaternion rot = Quaternion.AngleAxis(-fx.AngleDegrees, Vector3.up);
            float pulse = 0.88f + 0.24f * Mathf.Sin(progress * Mathf.PI * 6f);
            float curWidth = fx.Width * pulse;

            Material outerMat = GetMaterial(fx.TexPath, new Color(fx.Color.r, fx.Color.g, fx.Color.b, alpha), fx.Additive);
            if (outerMat != null)
            {
                Matrix4x4 m = Matrix4x4.TRS(center, rot, new Vector3(fx.Length, 1f, curWidth));
                Graphics.DrawMesh(MeshPool.plane10, m, outerMat, 0);
            }

            if (!string.IsNullOrEmpty(fx.CoreTexPath))
            {
                Material coreMat = GetMaterial(fx.CoreTexPath, new Color(1f, 1f, 1f, alpha * 0.85f), true);
                if (coreMat != null)
                {
                    Vector3 coreCenter = center;
                    coreCenter.y = altitude + 0.01f;
                    Matrix4x4 cm = Matrix4x4.TRS(coreCenter, rot, new Vector3(fx.Length, 1f, curWidth * 0.42f));
                    Graphics.DrawMesh(MeshPool.plane10, cm, coreMat, 0);
                }
            }
        }

        private static void DrawExpandingRing(OrdealVfxInstance fx, float progress, float altitude)
        {
            float eased = 1f - Mathf.Pow(1f - progress, 2f);
            float radius = Mathf.Lerp(fx.StartRadius, fx.EndRadius, eased);
            float alpha = (1f - progress) * fx.Color.a;
            if (alpha <= 0.02f || radius <= 0.05f) return;

            Vector3 pos = fx.Origin;
            pos.y = altitude;
            float size = radius * 2f;
            Quaternion rot = Quaternion.AngleAxis(progress * fx.RotateSpeed, Vector3.up);
            Material mat = GetMaterial(fx.TexPath, new Color(fx.Color.r, fx.Color.g, fx.Color.b, alpha), fx.Additive);
            if (mat != null)
            {
                Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(pos, rot, new Vector3(size, 1f, size)), mat, 0);
            }
        }

        private static void DrawPortal(OrdealVfxInstance fx, float progress, float altitude)
        {
            float scaleIn = progress < 0.12f ? progress / 0.12f : (progress > 0.88f ? (1f - progress) / 0.12f : 1f);
            float pulse = 0.92f + 0.10f * Mathf.Sin(progress * Mathf.PI * 10f);
            float size = fx.EndRadius * 2f * Mathf.Clamp01(scaleIn) * pulse;
            float alpha = Mathf.Clamp01(scaleIn) * fx.Color.a;
            if (alpha <= 0.02f || size <= 0.1f) return;

            Vector3 pos = fx.Origin;
            pos.y = altitude;
            Quaternion rot = Quaternion.AngleAxis(progress * fx.RotateSpeed, Vector3.up);
            Material mat = GetMaterial(fx.TexPath, new Color(fx.Color.r, fx.Color.g, fx.Color.b, alpha), fx.Additive);
            if (mat != null)
            {
                Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(pos, rot, new Vector3(size, 1f, size)), mat, 0);
            }
        }

        private static void DrawStrikeSprite(OrdealVfxInstance fx, float progress, float altitude)
        {
            float fade = progress < 0.15f ? progress / 0.15f : 1f - Mathf.Pow((progress - 0.15f) / 0.85f, 1.6f);
            float alpha = Mathf.Clamp01(fade) * fx.Color.a;
            if (alpha <= 0.02f) return;

            float scale = Mathf.Lerp(fx.StartRadius, fx.EndRadius, Mathf.Clamp01(progress * 2.5f));
            Vector3 pos = fx.Origin;
            pos.z += fx.RiseHeight * Mathf.Sin(progress * Mathf.PI * 0.5f);
            pos.y = altitude + 0.015f;

            Quaternion rot = Quaternion.AngleAxis(fx.AngleDegrees + progress * fx.RotateSpeed, Vector3.up);
            Material mat = GetMaterial(fx.TexPath, new Color(fx.Color.r, fx.Color.g, fx.Color.b, alpha), fx.Additive);
            if (mat != null)
            {
                Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(pos, rot, new Vector3(scale, 1f, scale)), mat, 0);
            }
        }

        private static void DrawPawnAura(OrdealVfxInstance fx, float progress, float altitude)
        {
            if (fx.AttachedPawn == null || !fx.AttachedPawn.Spawned) return;
            float alpha = (progress > 0.85f ? (1f - progress) / 0.15f : 1f) * fx.Color.a;
            if (alpha <= 0.02f) return;

            float pulse = 0.90f + 0.14f * Mathf.Sin(progress * Mathf.PI * 8f);
            float size = fx.EndRadius * 2f * pulse;
            Vector3 pos = fx.AttachedPawn.DrawPos;
            pos.z += fx.RiseHeight;
            pos.y = altitude + 0.02f;

            Quaternion rot = Quaternion.AngleAxis(progress * fx.RotateSpeed, Vector3.up);
            Material mat = GetMaterial(fx.TexPath, new Color(fx.Color.r, fx.Color.g, fx.Color.b, alpha), fx.Additive);
            if (mat != null)
            {
                Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(pos, rot, new Vector3(size, 1f, size)), mat, 0);
            }
        }

        private static void DrawFanSweep(OrdealVfxInstance fx, float progress, float altitude)
        {
            // 用一条高速扫射的激光束演绎 240° 死亡激光横扫
            float curAngle = fx.AngleDegrees - fx.ArcDegrees * 0.5f + fx.ArcDegrees * progress;
            float alpha = (1f - progress * 0.65f) * fx.Color.a;
            if (alpha <= 0.02f) return;

            float rad = curAngle * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
            Vector3 center = fx.Origin + dir * (fx.Length * 0.5f);
            center.y = altitude + 0.02f;
            Quaternion rot = Quaternion.AngleAxis(-curAngle, Vector3.up);

            Material mat = GetMaterial(fx.TexPath, new Color(fx.Color.r, fx.Color.g, fx.Color.b, alpha), true);
            if (mat != null)
            {
                Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(center, rot, new Vector3(fx.Length, 1f, fx.Width)), mat, 0);
            }
        }

        private static Material GetMaterial(string texPath, Color color, bool additive)
        {
            if (string.IsNullOrEmpty(texPath) || !UnityData.IsInMainThread)
            {
                return null;
            }

            // 颜色量化到 16 级防止材质缓存膨胀
            int rq = Mathf.RoundToInt(color.r * 15f);
            int gq = Mathf.RoundToInt(color.g * 15f);
            int bq = Mathf.RoundToInt(color.b * 15f);
            int aq = Mathf.RoundToInt(color.a * 15f);
            string key = $"{texPath}|{(additive ? 1 : 0)}|{rq},{gq},{bq},{aq}";
            if (MaterialCache.TryGetValue(key, out Material cached))
            {
                return cached;
            }

            if (ContentFinder<Texture2D>.Get(texPath, reportFailure: false) == null)
            {
                MaterialCache[key] = null;
                return null;
            }

            Color quant = new Color(rq / 15f, gq / 15f, bq / 15f, aq / 15f);
            Shader shader = additive ? ShaderDatabase.MoteGlow : ShaderDatabase.Transparent;
            Material created = MaterialPool.MatFrom(texPath, shader, quant);
            MaterialCache[key] = created;
            return created;
        }
    }
}
