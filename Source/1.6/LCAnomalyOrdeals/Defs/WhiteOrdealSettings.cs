using System.Collections.Generic;
using RimWorld;
using Verse;

namespace LCAnomalyOrdeals.Defs
{
    public sealed class WhiteOrdealSettings : DefModExtension
    {
        public int fixerTelegraphTicks = 180;
        public IntRange redInitialSpecialDelayTicks = new IntRange(720, 900);
        public IntRange redSpecialIntervalTicks = new IntRange(1140, 1260);
        public IntRange whiteInitialSpecialDelayTicks = new IntRange(720, 900);
        public IntRange whiteSpecialIntervalTicks = new IntRange(1140, 1260);
        public IntRange blackInitialSpecialDelayTicks = new IntRange(1740, 1860);
        public IntRange blackSpecialIntervalTicks = new IntRange(3000, 3600);
        public IntRange paleInitialSpecialDelayTicks = new IntRange(720, 900);
        public IntRange paleSpecialIntervalTicks = new IntRange(1020, 1140);
        public float redSpinRadius = 4f;
        public float redSpinDamage = 28f;
        public float redLaserWidth = 2f;
        public float redLaserDamage = 80f;
        public float redDeathSweepAngle = 240f;
        public float redDeathSweepRadius = 18f;
        public float redDeathSweepDamage = 80f;
        public float whiteBeamWidth = 2f;
        public float whiteBeamDamage = 11f;
        public float whiteFogWidth = 3f;
        public float whiteFogDamage = 5f;
        public int whiteFogDurationTicks = 300;
        public int whiteFogTickIntervalTicks = 18;
        public float whitePrayerHealthStep = 0.3f;
        public int whitePrayerDurationTicks = 600;
        public float whiteDeathRadius = 8f;
        public float whiteDeathDamage = 14f;
        public int blackResonancePulseCount = 6;
        public int blackResonancePulseIntervalTicks = 60;
        public float blackResonanceRadius = 10f;
        public float blackResonanceDamage = 11f;
        public float blackPlatformResetRadius = 14f;
        public IntRange paleTeleportIntervalTicks = new IntRange(1500, 2100);
        public float paleBurstRadius = 9f;
        public float paleBurstDamage = 20f;
        public float paleDeathRadius = 2f;
        public float paleDeathDamage = 60f;

        public int clawInitialSpecialDelayTicks = 1800;
        public int clawBasicAttackIntervalTicks = 90;
        public float clawBasicAttackRadius = 3.5f;
        public float clawBasicAttackDamage = 20f;
        public float clawBasicDoubleAttackChance = 0.4f;
        public IntRange clawSpecialIntervalTicks = new IntRange(2400, 2700);
        public float clawOrangeChance = 0.6f;
        public int clawOrangeTelegraphTicks = 150;
        public float clawOrangeDashWidth = 2.5f;
        public float clawOrangeDashDamage = 100f;
        public int clawBlueTelegraphTicks = 900;
        public int clawBlueMarkCount = 8;
        public float clawBlueDamage = 30f;
        public float clawGreenDamageTrigger = 120f;
        public int clawGreenTelegraphTicks = 540;
        public float clawGreenHealAmount = 150f;
        public float clawInterruptDamage = 200f;
        public IntRange clawInterruptStunTicks = new IntRange(600, 720);

        public int rewardBase = 12;
        public int rewardPerColonist = 3;
        public float presentationFadeInSeconds = 0.35f;
        public float presentationHoldSeconds = 2.75f;
        public float presentationFadeOutSeconds = 0.85f;
        public float presentationInputBlockSeconds = 1.75f;
        public float presentationOverlayAlpha = 0.95f;
        public float presentationBandAlpha = 0.32f;
        public List<WhiteOrdealPresentation> presentations = new List<WhiteOrdealPresentation>();

        public override IEnumerable<string> ConfigErrors()
        {
            if (fixerTelegraphTicks < 1 || !Valid(redInitialSpecialDelayTicks) || !Valid(redSpecialIntervalTicks) || !Valid(whiteInitialSpecialDelayTicks) || !Valid(whiteSpecialIntervalTicks) || !Valid(blackInitialSpecialDelayTicks) || !Valid(blackSpecialIntervalTicks) || !Valid(paleInitialSpecialDelayTicks) || !Valid(paleSpecialIntervalTicks)) yield return "fixer timing values are invalid";
            if (redSpinRadius <= 0f || redSpinDamage < 0f || redLaserWidth <= 0f || redLaserDamage < 0f || redDeathSweepAngle <= 0f || redDeathSweepAngle > 360f || redDeathSweepRadius <= 0f || redDeathSweepDamage < 0f) yield return "red Fixer values are invalid";
            if (whiteBeamWidth <= 0f || whiteBeamDamage < 0f || whiteFogWidth <= 0f || whiteFogDamage < 0f || whiteFogDurationTicks < 1 || whiteFogTickIntervalTicks < 1 || whitePrayerHealthStep <= 0f || whitePrayerHealthStep >= 1f || whitePrayerDurationTicks < 1 || whiteDeathRadius <= 0f || whiteDeathDamage < 0f) yield return "white Fixer values are invalid";
            if (blackResonancePulseCount < 1 || blackResonancePulseIntervalTicks < 1 || blackResonanceRadius <= 0f || blackResonanceDamage < 0f || blackPlatformResetRadius <= 0f) yield return "black Fixer values are invalid";
            if (paleTeleportIntervalTicks.min < 1 || paleTeleportIntervalTicks.max < paleTeleportIntervalTicks.min || paleBurstRadius <= 0f || paleBurstDamage < 0f || paleDeathRadius <= 0f || paleDeathDamage < 0f) yield return "pale Fixer values are invalid";
            if (clawInitialSpecialDelayTicks < 0 || clawBasicAttackIntervalTicks < 1 || clawBasicAttackRadius <= 0f || clawBasicAttackDamage < 0f || clawBasicDoubleAttackChance < 0f || clawBasicDoubleAttackChance > 1f || !Valid(clawSpecialIntervalTicks) || clawOrangeChance < 0f || clawOrangeChance > 1f || clawOrangeTelegraphTicks < 1 || clawOrangeDashWidth <= 0f || clawOrangeDashDamage < 0f) yield return "Claw orange values are invalid";
            if (clawBlueTelegraphTicks < 1 || clawBlueMarkCount < 1 || clawBlueDamage < 0f || clawGreenDamageTrigger <= 0f || clawGreenTelegraphTicks < 1 || clawGreenHealAmount < 0f || clawInterruptDamage <= 0f || clawInterruptStunTicks.min < 1 || clawInterruptStunTicks.max < clawInterruptStunTicks.min) yield return "Claw special values are invalid";
            if (rewardBase < 0 || rewardPerColonist < 0) yield return "reward values are invalid";
            if (presentations == null || presentations.Count != 4) yield return "presentations must contain four entries";
        }
        private static bool Valid(IntRange range) => range.min >= 1 && range.max >= range.min;
        public WhiteOrdealPresentation PresentationFor(string stage) => presentations?.Find(item => item != null && item.stage == stage);
    }

    public sealed class WhiteOrdealPresentation
    {
        public string stage;
        public ColorInt color = new ColorInt(225, 225, 215);
        public string startTitleKey;
        public string startTextKey;
        public string successTitleKey;
        public string successTextKey;
        public string failureTitleKey;
        public string failureTextKey;
        public SoundDef startSound;
        public SoundDef successSound;
        public SoundDef failureSound;
    }
}
