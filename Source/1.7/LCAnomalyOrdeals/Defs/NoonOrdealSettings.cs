using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace LCAnomalyOrdeals.Defs
{
    public sealed class NoonOrdealSettings : DefModExtension
    {
        public float crimsonColonistsPerTarget = 8f;
        public int crimsonCountMin = 1;
        public int crimsonCountMax = 2;
        public int crimsonDawnSplitCount = 3;
        public int crimsonDawnRetargetIntervalTicks = 600;

        public float greenColonistsPerTarget = 4f;
        public int greenCountMin = 2;
        public int greenCountMax = 6;
        public IntRange greenShutdownIntervalTicks = new IntRange(1200, 1800);
        public IntRange greenShutdownDurationTicks = new IntRange(240, 390);
        public int greenSawIntervalTicks = 150;
        public float greenSawRadius = 2.2f;
        public float greenSawDamage = 6f;

        public float violetColonistsPerTarget = 4f;
        public int violetCountMin = 2;
        public int violetCountMax = 5;
        public float violetImpactRadius = 6f;
        public float violetImpactDamage = 35f;
        public int violetPulseIntervalTicks = 90;
        public float violetPulseRadius = 5f;
        public float violetPulsePsychicDamage = 1f;
        public int violetCounterIntervalTicks = 1800;
        public int violetCounterReduction = 1;

        public int indigoGroupCount = 4;
        public int indigoGroupSize = 3;
        public int indigoGroupScatterRadius = 5;
        public int indigoCorpseCheckIntervalTicks = 180;
        public float indigoCorpseSearchRadius = 5f;
        public int indigoChargeIntervalTicks = 750;
        public float indigoChargeRadius = 2.5f;
        public float indigoChargePsychicDamage = 18f;
        public float indigoLifestealFactor = 0.35f;

        public int rewardBase = 8;
        public int rewardPerColonist = 2;

        public float presentationFadeInSeconds = 0.35f;
        public float presentationHoldSeconds = 2.25f;
        public float presentationFadeOutSeconds = 0.75f;
        public float presentationInputBlockSeconds = 1.5f;
        public float presentationOverlayAlpha = 0.92f;
        public float presentationBandAlpha = 0.42f;
        public List<NoonOrdealPresentation> presentations = new List<NoonOrdealPresentation>();

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in ValidateCount("crimson", crimsonCountMin, crimsonCountMax)) yield return error;
            foreach (string error in ValidateCount("green", greenCountMin, greenCountMax)) yield return error;
            foreach (string error in ValidateCount("violet", violetCountMin, violetCountMax)) yield return error;
            if (crimsonColonistsPerTarget <= 0f || greenColonistsPerTarget <= 0f || violetColonistsPerTarget <= 0f) yield return "colonists-per-target values must be greater than zero";
            if (crimsonDawnSplitCount < 1) yield return "crimsonDawnSplitCount must be at least 1";
            if (crimsonDawnRetargetIntervalTicks < 1) yield return "crimsonDawnRetargetIntervalTicks must be at least 1";
            foreach (string error in ValidateRange("greenShutdownIntervalTicks", greenShutdownIntervalTicks, 1)) yield return error;
            foreach (string error in ValidateRange("greenShutdownDurationTicks", greenShutdownDurationTicks, 1)) yield return error;
            if (greenSawIntervalTicks < 1 || greenSawRadius <= 0f || greenSawDamage < 0f) yield return "green saw interval/radius/damage values are invalid";
            if (violetImpactRadius <= 0f || violetPulseRadius <= 0f) yield return "violet radii must be greater than zero";
            if (violetImpactDamage < 0f || violetPulsePsychicDamage < 0f) yield return "violet damage values must not be negative";
            if (violetPulseIntervalTicks < 1 || violetCounterIntervalTicks < 1) yield return "violet intervals must be at least 1";
            if (violetCounterReduction < 0) yield return "violetCounterReduction must not be negative";
            if (indigoGroupCount < 1 || indigoGroupSize < 1) yield return "indigo group values must be at least 1";
            if (indigoGroupScatterRadius < 1 || indigoCorpseCheckIntervalTicks < 1 || indigoChargeIntervalTicks < 1) yield return "indigo interval and scatter values must be at least 1";
            if (indigoCorpseSearchRadius <= 0f || indigoChargeRadius <= 0f) yield return "indigo radii must be greater than zero";
            if (indigoChargePsychicDamage < 0f || indigoLifestealFactor < 0f) yield return "indigo damage and lifesteal values must not be negative";
            if (rewardBase < 0 || rewardPerColonist < 0) yield return "reward values must not be negative";
            if (presentationFadeInSeconds < 0f || presentationHoldSeconds < 0f || presentationFadeOutSeconds < 0f) yield return "presentation durations must not be negative";
            if (presentationFadeInSeconds + presentationHoldSeconds + presentationFadeOutSeconds <= 0f) yield return "presentation total duration must be greater than zero";
            if (presentationInputBlockSeconds < 0f) yield return "presentationInputBlockSeconds must not be negative";
            if (presentationOverlayAlpha < 0f || presentationOverlayAlpha > 1f || presentationBandAlpha < 0f || presentationBandAlpha > 1f) yield return "presentation alpha values must be between 0 and 1";
            if (presentations == null || presentations.Count == 0) yield return "presentations must contain at least one entry";
            if (presentations != null)
            {
                foreach (NoonOrdealPresentation presentation in presentations.Where(item => item != null))
                {
                    foreach (string error in presentation.ConfigErrors()) yield return "presentation: " + error;
                }
                foreach (IGrouping<string, NoonOrdealPresentation> duplicate in presentations.Where(item => item != null && !item.variant.NullOrEmpty()).GroupBy(item => item.variant.ToLowerInvariant()).Where(group => group.Count() > 1))
                {
                    yield return "presentation variant is duplicated: " + duplicate.Key;
                }
            }
        }

        public NoonOrdealPresentation PresentationFor(string variant)
        {
            return presentations?.FirstOrDefault(item => item != null && item.variant.Equals(variant, System.StringComparison.OrdinalIgnoreCase));
        }

        private static IEnumerable<string> ValidateCount(string name, int min, int max)
        {
            if (min < 1) yield return name + "CountMin must be at least 1";
            if (max < min) yield return name + "CountMax must be greater than or equal to its minimum";
        }

        private static IEnumerable<string> ValidateRange(string name, IntRange range, int minimum)
        {
            if (range.min < minimum || range.max < range.min) yield return name + " must be an ordered range with minimum " + minimum;
        }
    }

    public sealed class NoonOrdealPresentation
    {
        public string variant;
        public ColorInt color = new ColorInt(255, 255, 255);
        public string startKickerKey = "LCOrdeal_PresentationNoon";
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
