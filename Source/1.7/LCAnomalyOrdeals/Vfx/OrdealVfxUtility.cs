using UnityEngine;
using Verse;

namespace LCAnomalyOrdeals.Vfx
{
    /// <summary>
    /// 考验签名战斗特效调用门面：统一封装激光、冲击波、传送门、触手、苍白之眼、白雾、血清光环与斩击特效。
    /// </summary>
    public static class OrdealVfxUtility
    {
        public const string TexLaserBeam = "FX/Ordeals/LaserBeam";
        public const string TexLaserCore = "FX/Ordeals/LaserCore";
        public const string TexLaserHit = "FX/Ordeals/LaserHit";
        public const string TexSparkMidnight = "FX/Ordeals/SparkMidnight";
        public const string TexBossPortal = "FX/Ordeals/BossPortal";
        public const string TexHollowRing = "FX/Ordeals/HollowRing";
        public const string TexSoftGlow = "FX/Ordeals/SoftGlow";
        public const string TexCraterDecal = "FX/Ordeals/CraterDecal";
        public const string TexSlashArc = "FX/Ordeals/SlashArc";
        public const string TexTargetMark = "FX/Ordeals/TargetMark";
        public const string TexTentacleRed = "FX/Ordeals/TentacleRed";
        public const string TexTentacleWhite = "FX/Ordeals/TentacleWhite";
        public const string TexTentacleBlack = "FX/Ordeals/TentacleBlack";
        public const string TexPaleEye = "FX/Ordeals/PaleEye";
        public const string TexCircusTent = "FX/Ordeals/CircusTent";

        public static void SpawnBeam(
            Map map, Vector3 origin, float angleDegrees, float length, float width,
            Color color, int durationTicks, bool withCore = true)
        {
            MapComponent_OrdealVfx comp = map?.GetComponent<MapComponent_OrdealVfx>();
            if (comp == null || length <= 0.1f) return;

            comp.Add(new OrdealVfxInstance
            {
                Kind = OrdealVfxKind.Beam,
                TexPath = TexLaserBeam,
                CoreTexPath = withCore ? TexLaserCore : null,
                Origin = origin,
                AngleDegrees = angleDegrees,
                Length = length,
                Width = width,
                Color = color,
                DurationTicks = durationTicks,
                Additive = true
            });
        }

        public static void SpawnBeamBetween(
            Map map, Vector3 origin, Vector3 target, float width,
            Color color, int durationTicks, float extendLength = 0f)
        {
            Vector3 delta = target - origin;
            delta.y = 0f;
            float dist = delta.magnitude;
            if (dist < 0.05f) return;
            float angle = Mathf.Atan2(delta.z, delta.x) * Mathf.Rad2Deg;
            float length = extendLength > 0f ? extendLength : dist;
            SpawnBeam(map, origin, angle, length, width, color, durationTicks, withCore: true);
        }

        public static void SpawnShockwave(
            Map map, Vector3 center, float startRadius, float endRadius,
            Color color, int durationTicks = 32, float rotateSpeed = 90f)
        {
            MapComponent_OrdealVfx comp = map?.GetComponent<MapComponent_OrdealVfx>();
            if (comp == null) return;

            comp.Add(new OrdealVfxInstance
            {
                Kind = OrdealVfxKind.ExpandingRing,
                TexPath = TexHollowRing,
                Origin = center,
                StartRadius = startRadius,
                EndRadius = endRadius,
                Color = color,
                DurationTicks = durationTicks,
                RotateSpeed = rotateSpeed,
                Additive = true
            });
        }

        public static void SpawnPortal(
            Map map, Vector3 center, float radius, Color color,
            int durationTicks, float rotateSpeed = 160f)
        {
            MapComponent_OrdealVfx comp = map?.GetComponent<MapComponent_OrdealVfx>();
            if (comp == null) return;

            comp.Add(new OrdealVfxInstance
            {
                Kind = OrdealVfxKind.Portal,
                TexPath = TexBossPortal,
                Origin = center,
                EndRadius = radius,
                Color = color,
                DurationTicks = durationTicks,
                RotateSpeed = rotateSpeed,
                Additive = true
            });
        }

        public static void SpawnStrikeSprite(
            Map map, Vector3 center, string texPath, float startSize, float endSize,
            Color color, int durationTicks = 45, float riseHeight = 0.5f,
            float angleDegrees = 0f, float rotateSpeed = 0f, bool additive = false)
        {
            MapComponent_OrdealVfx comp = map?.GetComponent<MapComponent_OrdealVfx>();
            if (comp == null) return;

            comp.Add(new OrdealVfxInstance
            {
                Kind = OrdealVfxKind.StrikeSprite,
                TexPath = texPath,
                Origin = center,
                StartRadius = startSize,
                EndRadius = endSize,
                Color = color,
                DurationTicks = durationTicks,
                RiseHeight = riseHeight,
                AngleDegrees = angleDegrees,
                RotateSpeed = rotateSpeed,
                Additive = additive
            });
        }

        public static void SpawnCrater(Map map, Vector3 center, float size, Color color, int durationTicks = 150)
        {
            SpawnStrikeSprite(map, center, TexCraterDecal, size * 0.7f, size, color, durationTicks, riseHeight: 0f, additive: false);
        }

        public static void SpawnSlash(Map map, Vector3 center, float size, Color color, int durationTicks = 24)
        {
            SpawnStrikeSprite(map, center, TexSlashArc, size * 0.6f, size, color, durationTicks, riseHeight: 0.1f, angleDegrees: Rand.Range(0f, 360f), rotateSpeed: 90f, additive: true);
        }

        public static void SpawnCircusTent(Map map, Vector3 center, float size = 2.6f, int durationTicks = 54)
        {
            SpawnStrikeSprite(map, center, TexCircusTent, size * 0.45f, size, Color.white, durationTicks, riseHeight: 0.35f, additive: false);
            SpawnShockwave(map, center, 0.4f, size * 0.95f, new Color(0.95f, 0.18f, 0.24f, 0.85f), Mathf.Min(36, durationTicks));
        }

        public static void SpawnPawnAura(
            Map map, Pawn pawn, string texPath, float radius, Color color,
            int durationTicks, float rotateSpeed = 120f, float riseHeight = 0f, bool additive = true)
        {
            MapComponent_OrdealVfx comp = map?.GetComponent<MapComponent_OrdealVfx>();
            if (comp == null || pawn == null) return;

            comp.Add(new OrdealVfxInstance
            {
                Kind = OrdealVfxKind.PawnAura,
                TexPath = texPath,
                AttachedPawn = pawn,
                EndRadius = radius,
                Color = color,
                DurationTicks = durationTicks,
                RotateSpeed = rotateSpeed,
                RiseHeight = riseHeight,
                Additive = additive
            });
        }

        public static void SpawnFogLine(Map map, Vector3 origin, Vector3 target, float width, int durationTicks)
        {
            MapComponent_OrdealVfx comp = map?.GetComponent<MapComponent_OrdealVfx>();
            if (comp == null) return;

            Vector3 delta = target - origin;
            delta.y = 0f;
            float dist = Mathf.Max(6f, delta.magnitude);
            Vector3 dir = dist > 0.01f ? delta / dist : Vector3.forward;
            int steps = Mathf.Clamp(Mathf.CeilToInt(dist / 2.2f), 3, 14);
            for (int i = 1; i <= steps; i++)
            {
                Vector3 p = origin + dir * (i * (dist / steps));
                comp.Add(new OrdealVfxInstance
                {
                    Kind = OrdealVfxKind.Portal,
                    TexPath = TexSoftGlow,
                    Origin = p,
                    EndRadius = width * 0.85f,
                    Color = new Color(0.94f, 0.96f, 1f, 0.58f),
                    DurationTicks = durationTicks,
                    RotateSpeed = (i % 2 == 0 ? 1f : -1f) * 35f,
                    Additive = true
                });
            }
        }

        public static void SpawnFanSweep(
            Map map, Vector3 center, float facingDegrees, float arcDegrees,
            float radius, float width, Color color, int durationTicks = 42)
        {
            MapComponent_OrdealVfx comp = map?.GetComponent<MapComponent_OrdealVfx>();
            if (comp == null) return;

            comp.Add(new OrdealVfxInstance
            {
                Kind = OrdealVfxKind.FanSweep,
                TexPath = TexLaserBeam,
                Origin = center,
                AngleDegrees = facingDegrees,
                ArcDegrees = arcDegrees,
                Length = radius,
                Width = width,
                Color = color,
                DurationTicks = durationTicks,
                Additive = true
            });
        }
    }
}
