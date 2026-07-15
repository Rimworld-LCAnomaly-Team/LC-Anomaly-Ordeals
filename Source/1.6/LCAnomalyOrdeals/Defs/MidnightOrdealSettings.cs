using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace LCAnomalyOrdeals.Defs
{
    public sealed class MidnightOrdealSettings : DefModExtension
    {
        public int amberMidnightCount = 2;
        public int amberInitialSpawnDelayTicks = 900;
        public IntRange amberSpawnIntervalTicks = new IntRange(900, 1200);
        public int amberDuskSpawnPerCycle = 2;
        public int amberDuskLifetimeCapPerParent = 6;
        public int amberSpawnRadius = 5;
        public int amberBurrowArrivalRadius = 10;
        public float amberEmergenceRadius = 5f;
        public float amberEmergenceDamage = 35f;
        public int amberDuskActionIntervalTicks = 1800;
        public int amberDawnSpawnPerCycle = 3;
        public int amberDawnCapPerParent = 3;

        public int greenTowerCount = 1;
        public int greenActivationDelayTicks = 600;
        public int greenBeamTickIntervalTicks = 12;
        public int greenRotationDurationTicks = 7200;
        public int greenCooldownTicks = 1800;
        public float greenFixedBeamAngleDegrees = 90f;
        public float greenBeamWidthCells = 1.35f;
        public float greenBeamDamage = 15f;
        public int greenBeamSlowDurationTicks = 120;
        public float greenDamageSlowThreshold = 405f;
        public int greenRotationSlowDurationTicks = 600;
        public int greenMaximumRotationSlowTicks = 6000;
        public float greenRotationSlowFactor = 0.5f;
        public int greenBeamVisualSpacingCells = 8;

        public int violetShrinesPerColor = 1;
        public IntRange violetInitialAttackDelayTicks = new IntRange(600, 900);
        public IntRange violetAttackIntervalTicks = new IntRange(1500, 1800);
        public int violetTelegraphTicks = 480;
        public float violetRedDamage = 45f;
        public float violetRedRadius = 3.5f;
        public float violetWhiteDamage = 18f;
        public float violetWhiteRadius = 9f;
        public float violetBlackDamage = 30f;
        public float violetBlackLineWidth = 2.5f;
        public float violetPaleDamage = 12f;
        public float violetPaleRadius = 5f;
        public float violetDefenseThresholdOne = 0.7f;
        public float violetDefenseThresholdTwo = 0.4f;
        public float violetDefenseThresholdThree = 0.1f;
        public int violetDefenseExtraAttacks = 1;

        public int rewardBase = 15;
        public int rewardPerColonist = 4;
        public float presentationFadeInSeconds = 0.35f;
        public float presentationHoldSeconds = 2.75f;
        public float presentationFadeOutSeconds = 0.85f;
        public float presentationInputBlockSeconds = 1.75f;
        public float presentationOverlayAlpha = 0.94f;
        public float presentationBandAlpha = 0.44f;
        public List<MidnightOrdealPresentation> presentations = new List<MidnightOrdealPresentation>();

        public override IEnumerable<string> ConfigErrors()
        {
            if (amberMidnightCount < 1 || amberInitialSpawnDelayTicks < 0 || amberDuskSpawnPerCycle < 1 || amberDuskLifetimeCapPerParent < 1 || amberSpawnRadius < 1 || amberBurrowArrivalRadius < 1) yield return "amber Midnight spawn values are invalid";
            if (amberSpawnIntervalTicks.min < 1 || amberSpawnIntervalTicks.max < amberSpawnIntervalTicks.min) yield return "amberSpawnIntervalTicks is invalid";
            if (amberEmergenceRadius <= 0f || amberEmergenceDamage < 0f || amberDuskActionIntervalTicks < 1 || amberDawnSpawnPerCycle < 1 || amberDawnCapPerParent < 1) yield return "amber Midnight combat values are invalid";
            if (greenTowerCount < 1 || greenActivationDelayTicks < 0 || greenBeamTickIntervalTicks < 1 || greenRotationDurationTicks < 1 || greenCooldownTicks < 0 || greenBeamWidthCells <= 0f || greenBeamDamage < 0f) yield return "green Midnight beam values are invalid";
            if (greenBeamSlowDurationTicks < 1 || greenDamageSlowThreshold <= 0f || greenRotationSlowDurationTicks < 1 || greenMaximumRotationSlowTicks < greenRotationSlowDurationTicks || greenRotationSlowFactor <= 0f || greenRotationSlowFactor > 1f || greenBeamVisualSpacingCells < 1) yield return "green Midnight slow values are invalid";
            if (violetShrinesPerColor < 1 || violetInitialAttackDelayTicks.min < 0 || violetInitialAttackDelayTicks.max < violetInitialAttackDelayTicks.min || violetAttackIntervalTicks.min < 1 || violetAttackIntervalTicks.max < violetAttackIntervalTicks.min || violetTelegraphTicks < 1) yield return "violet Midnight timing values are invalid";
            if (violetRedDamage < 0f || violetWhiteDamage < 0f || violetBlackDamage < 0f || violetPaleDamage < 0f || violetRedRadius <= 0f || violetWhiteRadius <= 0f || violetBlackLineWidth <= 0f || violetPaleRadius <= 0f) yield return "violet Midnight attack values are invalid";
            if (!(violetDefenseThresholdOne > violetDefenseThresholdTwo && violetDefenseThresholdTwo > violetDefenseThresholdThree && violetDefenseThresholdThree > 0f) || violetDefenseThresholdOne > 1f || violetDefenseExtraAttacks < 0) yield return "violet Midnight defense thresholds are invalid";
            if (rewardBase < 0 || rewardPerColonist < 0) yield return "reward values must not be negative";
            if (presentationFadeInSeconds < 0f || presentationHoldSeconds < 0f || presentationFadeOutSeconds < 0f || presentationInputBlockSeconds < 0f || presentationFadeInSeconds + presentationHoldSeconds + presentationFadeOutSeconds <= 0f) yield return "presentation durations are invalid";
            if (presentationOverlayAlpha < 0f || presentationOverlayAlpha > 1f || presentationBandAlpha < 0f || presentationBandAlpha > 1f) yield return "presentation alpha values must be between 0 and 1";
            if (presentations == null || presentations.Count == 0) yield return "presentations must contain at least one entry";
            if (presentations != null)
            {
                foreach (MidnightOrdealPresentation item in presentations.Where(item => item != null)) foreach (string error in item.ConfigErrors()) yield return "presentation: " + error;
            }
        }

        public MidnightOrdealPresentation PresentationFor(string variant) => presentations?.FirstOrDefault(item => item != null && item.variant.Equals(variant, System.StringComparison.OrdinalIgnoreCase));
    }

    public sealed class MidnightOrdealPresentation
    {
        public string variant;
        public ColorInt color = new ColorInt(255, 255, 255);
        public string startKickerKey = "LCOrdeal_PresentationMidnight";
        public string startTitleKey;
        public string startTextKey;
        public string successKickerKey = "LCOrdeal_PresentationSuppressed";
        public string successTitleKey;
        public string successTextKey;
        public string failureKickerKey = "LCOrdeal_PresentationFailed";
        public string failureTitleKey;
        public string failureTextKey;
        public SoundDef startSound;
        public SoundDef successSound;
        public SoundDef failureSound;
        public IEnumerable<string> ConfigErrors()
        {
            if (variant.NullOrEmpty()) yield return "variant is required";
            if (startTitleKey.NullOrEmpty() || startTextKey.NullOrEmpty() || successTitleKey.NullOrEmpty() || successTextKey.NullOrEmpty() || failureTitleKey.NullOrEmpty() || failureTextKey.NullOrEmpty()) yield return variant + " presentation keys are incomplete";
        }
    }
}
