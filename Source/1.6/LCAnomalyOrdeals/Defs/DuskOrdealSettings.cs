using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace LCAnomalyOrdeals.Defs
{
    public sealed class DuskOrdealSettings : DefModExtension
    {
        public float amberColonistsPerTarget = 5f;
        public int amberCountMin = 2;
        public int amberCountMax = 5;
        public int amberBurrowIntervalTicks = 1800;
        public int amberBurrowRadius = 12;
        public int amberDawnSpawnPerCycle = 3;
        public int amberDawnCapPerParent = 3;
        public float amberEmergenceRadius = 4f;
        public float amberEmergenceDamage = 18f;
        public float amberSlowDamageThreshold = 28f;
        public IntRange amberSlowDurationTicks = new IntRange(60, 120);

        public int crimsonCount = 2;
        public int crimsonNoonSplitCount = 1;
        public int crimsonDawnSplitCount = 3;
        public int crimsonRollIntervalTicks = 900;
        public int crimsonRollArrivalRadius = 6;
        public float crimsonRollDamageRadius = 3f;
        public float crimsonRollDamage = 20f;
        public int crimsonDawnRetargetIntervalTicks = 600;

        public int greenFactoryCount = 4;
        public int greenFactoryInitialDelayTicks = 600;
        public IntRange greenFactoryProductionIntervalTicks = new IntRange(2700, 3600);
        public int greenSpawnPerCycle = 3;
        public int greenSpawnedUnitCap = 15;
        public float greenNoonChance = 0.5f;
        public int greenSpawnRadius = 5;
        public IntRange greenNoonShutdownIntervalTicks = new IntRange(1200, 1800);
        public IntRange greenNoonShutdownDurationTicks = new IntRange(240, 390);
        public int greenNoonSawIntervalTicks = 150;
        public float greenNoonSawRadius = 2.2f;
        public float greenNoonSawDamage = 6f;

        public int rewardBase = 12;
        public int rewardPerColonist = 3;

        public float presentationFadeInSeconds = 0.35f;
        public float presentationHoldSeconds = 2.25f;
        public float presentationFadeOutSeconds = 0.75f;
        public float presentationInputBlockSeconds = 1.5f;
        public float presentationOverlayAlpha = 0.92f;
        public float presentationBandAlpha = 0.42f;
        public List<DuskOrdealPresentation> presentations = new List<DuskOrdealPresentation>();

        public override IEnumerable<string> ConfigErrors()
        {
            if (amberColonistsPerTarget <= 0f) yield return "amberColonistsPerTarget must be greater than zero";
            foreach (string error in ValidateCount("amber", amberCountMin, amberCountMax)) yield return error;
            if (amberBurrowIntervalTicks < 1 || amberBurrowRadius < 1) yield return "amber burrow values must be at least 1";
            if (amberDawnSpawnPerCycle < 1 || amberDawnCapPerParent < 1) yield return "amber Dawn spawn values must be at least 1";
            if (amberEmergenceRadius <= 0f || amberEmergenceDamage < 0f || amberSlowDamageThreshold < 0f) yield return "amber radius and damage values are invalid";
            foreach (string error in ValidateRange("amberSlowDurationTicks", amberSlowDurationTicks, 1)) yield return error;
            if (crimsonCount < 1 || crimsonNoonSplitCount < 1 || crimsonDawnSplitCount < 1) yield return "crimson count and split values must be at least 1";
            if (crimsonRollIntervalTicks < 1 || crimsonRollArrivalRadius < 1 || crimsonDawnRetargetIntervalTicks < 1) yield return "crimson interval and arrival values must be at least 1";
            if (crimsonRollDamageRadius <= 0f || crimsonRollDamage < 0f) yield return "crimson roll radius and damage values are invalid";
            if (greenFactoryCount < 1 || greenFactoryInitialDelayTicks < 0 || greenSpawnPerCycle < 1 || greenSpawnedUnitCap < 1 || greenSpawnRadius < 1) yield return "green factory values are invalid";
            foreach (string error in ValidateRange("greenFactoryProductionIntervalTicks", greenFactoryProductionIntervalTicks, 1)) yield return error;
            if (greenNoonChance < 0f || greenNoonChance > 1f) yield return "greenNoonChance must be between 0 and 1";
            foreach (string error in ValidateRange("greenNoonShutdownIntervalTicks", greenNoonShutdownIntervalTicks, 1)) yield return error;
            foreach (string error in ValidateRange("greenNoonShutdownDurationTicks", greenNoonShutdownDurationTicks, 1)) yield return error;
            if (greenNoonSawIntervalTicks < 1 || greenNoonSawRadius <= 0f || greenNoonSawDamage < 0f) yield return "green Noon saw values are invalid";
            if (rewardBase < 0 || rewardPerColonist < 0) yield return "reward values must not be negative";
            if (presentationFadeInSeconds < 0f || presentationHoldSeconds < 0f || presentationFadeOutSeconds < 0f || presentationInputBlockSeconds < 0f) yield return "presentation durations must not be negative";
            if (presentationFadeInSeconds + presentationHoldSeconds + presentationFadeOutSeconds <= 0f) yield return "presentation total duration must be greater than zero";
            if (presentationOverlayAlpha < 0f || presentationOverlayAlpha > 1f || presentationBandAlpha < 0f || presentationBandAlpha > 1f) yield return "presentation alpha values must be between 0 and 1";
            if (presentations == null || presentations.Count == 0) yield return "presentations must contain at least one entry";
            if (presentations != null)
            {
                foreach (DuskOrdealPresentation presentation in presentations.Where(item => item != null))
                    foreach (string error in presentation.ConfigErrors()) yield return "presentation: " + error;
                foreach (IGrouping<string, DuskOrdealPresentation> duplicate in presentations.Where(item => item != null && !item.variant.NullOrEmpty()).GroupBy(item => item.variant.ToLowerInvariant()).Where(group => group.Count() > 1))
                    yield return "presentation variant is duplicated: " + duplicate.Key;
            }
        }

        public DuskOrdealPresentation PresentationFor(string variant) => presentations?.FirstOrDefault(item => item != null && item.variant.Equals(variant, System.StringComparison.OrdinalIgnoreCase));

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

    public sealed class DuskOrdealPresentation
    {
        public string variant;
        public ColorInt color = new ColorInt(255, 255, 255);
        public string startKickerKey = "LCOrdeal_PresentationDusk";
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
