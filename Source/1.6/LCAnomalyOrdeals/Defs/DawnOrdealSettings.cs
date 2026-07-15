using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace LCAnomalyOrdeals.Defs
{
    public sealed class DawnOrdealSettings : DefModExtension
    {
        public int amberCountOffset = 2;
        public int amberCountMin = 4;
        public int amberCountMax = 10;
        public int amberBurrowIntervalTicks = 2500;
        public int amberBurrowRadius = 14;

        public float crimsonColonistsPerTarget = 2f;
        public int crimsonCountMin = 2;
        public int crimsonCountMax = 5;
        public int crimsonRetargetIntervalTicks = 600;
        public float crimsonExplosionRadius = 2.4f;
        public int crimsonExplosionDamage = 18;
        public float crimsonExplosionArmorPenetration = 0.15f;

        public float greenColonistsPerTarget = 3f;
        public int greenCountMin = 1;
        public int greenCountMax = 4;
        public float greenExecutionPsychicRadius = 8f;
        public float greenExecutionPsychicDamage = 5f;

        public float violetColonistsPerTarget = 3f;
        public int violetCountMin = 1;
        public int violetCountMax = 3;
        public int violetMatureDelayTicks = 7500;
        public float violetBlastRadius = 18f;
        public float violetPsychicDamage = 10f;
        public IntRange violetChaseDurationTicks = new IntRange(600, 900);
        public int violetWanderRadius = 12;
        public int violetWanderJobExpiryTicks = 600;

        public int rewardBase = 5;
        public int rewardPerColonist = 1;

        public float presentationFadeInSeconds = 0.35f;
        public float presentationHoldSeconds = 2.25f;
        public float presentationFadeOutSeconds = 0.75f;
        public float presentationInputBlockSeconds = 1.5f;
        public float presentationOverlayAlpha = 0.92f;
        public float presentationBandAlpha = 0.42f;
        public List<DawnOrdealPresentation> presentations = new List<DawnOrdealPresentation>();

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in ValidateCount("amber", amberCountMin, amberCountMax)) yield return error;
            foreach (string error in ValidateCount("crimson", crimsonCountMin, crimsonCountMax)) yield return error;
            foreach (string error in ValidateCount("green", greenCountMin, greenCountMax)) yield return error;
            foreach (string error in ValidateCount("violet", violetCountMin, violetCountMax)) yield return error;
            if (crimsonColonistsPerTarget <= 0f) yield return "crimsonColonistsPerTarget must be greater than zero";
            if (greenColonistsPerTarget <= 0f) yield return "greenColonistsPerTarget must be greater than zero";
            if (violetColonistsPerTarget <= 0f) yield return "violetColonistsPerTarget must be greater than zero";
            if (amberBurrowIntervalTicks < 1) yield return "amberBurrowIntervalTicks must be at least 1";
            if (amberBurrowRadius < 1) yield return "amberBurrowRadius must be at least 1";
            if (crimsonRetargetIntervalTicks < 1) yield return "crimsonRetargetIntervalTicks must be at least 1";
            if (crimsonExplosionRadius <= 0f) yield return "crimsonExplosionRadius must be greater than zero";
            if (crimsonExplosionDamage < 0) yield return "crimsonExplosionDamage must not be negative";
            if (greenExecutionPsychicRadius <= 0f) yield return "greenExecutionPsychicRadius must be greater than zero";
            if (greenExecutionPsychicDamage < 0f) yield return "greenExecutionPsychicDamage must not be negative";
            if (violetMatureDelayTicks < 1) yield return "violetMatureDelayTicks must be at least 1";
            if (violetBlastRadius <= 0f) yield return "violetBlastRadius must be greater than zero";
            if (violetPsychicDamage < 0f) yield return "violetPsychicDamage must not be negative";
            if (violetChaseDurationTicks.min < 0 || violetChaseDurationTicks.max < violetChaseDurationTicks.min) yield return "violetChaseDurationTicks must be a non-negative ordered range";
            if (violetWanderRadius < 1) yield return "violetWanderRadius must be at least 1";
            if (violetWanderJobExpiryTicks < 1) yield return "violetWanderJobExpiryTicks must be at least 1";
            if (rewardBase < 0 || rewardPerColonist < 0) yield return "reward values must not be negative";
            if (presentationFadeInSeconds < 0f || presentationHoldSeconds < 0f || presentationFadeOutSeconds < 0f) yield return "presentation durations must not be negative";
            if (presentationFadeInSeconds + presentationHoldSeconds + presentationFadeOutSeconds <= 0f) yield return "presentation total duration must be greater than zero";
            if (presentationInputBlockSeconds < 0f) yield return "presentationInputBlockSeconds must not be negative";
            if (presentationOverlayAlpha < 0f || presentationOverlayAlpha > 1f) yield return "presentationOverlayAlpha must be between 0 and 1";
            if (presentationBandAlpha < 0f || presentationBandAlpha > 1f) yield return "presentationBandAlpha must be between 0 and 1";
            if (presentations == null || presentations.Count == 0) yield return "presentations must contain at least one entry";
            if (presentations != null)
            {
                foreach (DawnOrdealPresentation presentation in presentations.Where(item => item != null))
                {
                    foreach (string error in presentation.ConfigErrors()) yield return "presentation: " + error;
                }
                foreach (IGrouping<string, DawnOrdealPresentation> duplicate in presentations.Where(item => item != null && !item.variant.NullOrEmpty()).GroupBy(item => item.variant.ToLowerInvariant()).Where(group => group.Count() > 1))
                {
                    yield return "presentation variant is duplicated: " + duplicate.Key;
                }
            }
        }

        public DawnOrdealPresentation PresentationFor(string variant)
        {
            return presentations?.FirstOrDefault(item => item != null && item.variant.Equals(variant, System.StringComparison.OrdinalIgnoreCase));
        }

        private static IEnumerable<string> ValidateCount(string name, int min, int max)
        {
            if (min < 1) yield return name + "CountMin must be at least 1";
            if (max < min) yield return name + "CountMax must be greater than or equal to its minimum";
        }
    }

    public sealed class DawnOrdealPresentation
    {
        public string variant;
        public ColorInt color = new ColorInt(255, 255, 255);
        public string startKickerKey = "LCOrdeal_PresentationDawn";
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
            if (startTitleKey.NullOrEmpty() || startTextKey.NullOrEmpty()) yield return variant + " start title/text keys are required";
            if (successTitleKey.NullOrEmpty() || successTextKey.NullOrEmpty()) yield return variant + " success title/text keys are required";
            if (failureTitleKey.NullOrEmpty() || failureTextKey.NullOrEmpty()) yield return variant + " failure title/text keys are required";
        }
    }
}
