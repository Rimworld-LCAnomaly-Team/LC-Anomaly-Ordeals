using System;
using System.Collections.Generic;
using System.Linq;
using LCAnomalyOrdeals.Examinations;
using LCAnomalyStory;
using LCAnomalyStory.Components;
using LCAnomalyStory.Defs;
using LCAnomalyStory.Examinations;
using LudeonTK;
using RimWorld;
using Verse;

namespace LCAnomalyOrdeals.Debugging
{
    internal static class OrdealDebugActions
    {
        private const string Category = "LC: Anomaly Ordeals";

        private sealed class OrdealChoice
        {
            public string examinationDefName;
            public string variant;
            public string labelKey;

            public OrdealChoice(string examinationDefName, string variant, string labelKey)
            {
                this.examinationDefName = examinationDefName;
                this.variant = variant;
                this.labelKey = labelKey;
            }
        }

        private static readonly OrdealChoice[] Choices =
        {
            new OrdealChoice("LCOrdeal_Dawn", "Amber", "LCOrdeal_DebugDawnAmber"),
            new OrdealChoice("LCOrdeal_Dawn", "Crimson", "LCOrdeal_DebugDawnCrimson"),
            new OrdealChoice("LCOrdeal_Dawn", "Green", "LCOrdeal_DebugDawnGreen"),
            new OrdealChoice("LCOrdeal_Dawn", "Violet", "LCOrdeal_DebugDawnViolet"),
            new OrdealChoice("LCOrdeal_Noon", "Crimson", "LCOrdeal_DebugNoonCrimson"),
            new OrdealChoice("LCOrdeal_Noon", "Green", "LCOrdeal_DebugNoonGreen"),
            new OrdealChoice("LCOrdeal_Noon", "Violet", "LCOrdeal_DebugNoonViolet"),
            new OrdealChoice("LCOrdeal_Noon", "Indigo", "LCOrdeal_DebugNoonIndigo"),
            new OrdealChoice("LCOrdeal_Dusk", "Amber", "LCOrdeal_DebugDuskAmber"),
            new OrdealChoice("LCOrdeal_Dusk", "Crimson", "LCOrdeal_DebugDuskCrimson"),
            new OrdealChoice("LCOrdeal_Dusk", "Green", "LCOrdeal_DebugDuskGreen"),
            new OrdealChoice("LCOrdeal_Midnight", "Amber", "LCOrdeal_DebugMidnightAmber"),
            new OrdealChoice("LCOrdeal_Midnight", "Green", "LCOrdeal_DebugMidnightGreen"),
            new OrdealChoice("LCOrdeal_Midnight", "Violet", "LCOrdeal_DebugMidnightViolet"),
            new OrdealChoice("LCOrdeal_WhiteDawn", null, "LCOrdeal_DebugWhiteDawn"),
            new OrdealChoice("LCOrdeal_WhiteNoon", null, "LCOrdeal_DebugWhiteNoon"),
            new OrdealChoice("LCOrdeal_WhiteDusk", null, "LCOrdeal_DebugWhiteDusk"),
            new OrdealChoice("LCOrdeal_WhiteMidnight", null, "LCOrdeal_DebugWhiteMidnight")
        };

        [DebugAction(Category, "Trigger ordeal...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static List<DebugActionNode> TriggerOrdeal()
        {
            return Choices.Select(choice => new DebugActionNode(choice.labelKey.Translate(), DebugActionType.Action)
            {
                action = () => Start(choice)
            }).ToList();
        }

        [DebugAction(Category, "End active ordeal...", allowedGameStates = AllowedGameStates.Playing)]
        private static List<DebugActionNode> EndActiveOrdeal()
        {
            return new List<DebugActionNode>
            {
                EndNode("LCOrdeal_DebugEndPassed", context => context.Pass("LCOrdeal_DebugForcedPassed".Translate())),
                EndNode("LCOrdeal_DebugEndFailed", context => context.Fail("LCOrdeal_DebugForcedFailed".Translate())),
                EndNode("LCOrdeal_DebugEndCancelled", context => context.Cancel("LCOrdeal_DebugForcedCancelled".Translate()))
            };
        }

        [DebugAction(Category, "Spawn ordeal pawn...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static List<DebugActionNode> SpawnOrdealPawn()
        {
            return DefDatabase<PawnKindDef>.AllDefs
                .Where(def => def.defName.StartsWith("LCOrdeal_", StringComparison.Ordinal))
                .OrderBy(def => def.defName)
                .Select(def => new DebugActionNode(def.LabelCap + " (" + def.defName + ")", DebugActionType.ToolMap)
                {
                    action = () => Spawn(def)
                })
                .ToList();
        }

        private static DebugActionNode EndNode(string labelKey, Func<ExaminationContext, bool> action)
        {
            return new DebugActionNode(labelKey.Translate(), DebugActionType.Action)
            {
                action = () =>
                {
                    ExaminationContext context;
                    if (StoryComponents.Development == null
                        || !StoryComponents.Development.TryGetActiveExaminationContext(out context)
                        || !context.Definition.defName.StartsWith("LCOrdeal_", StringComparison.Ordinal))
                    {
                        Messages.Message("LCOrdeal_DebugNoActive".Translate(), MessageTypeDefOf.RejectInput);
                        return;
                    }

                    action(context);
                }
            };
        }

        private static void Start(OrdealChoice choice)
        {
            GameComponent_CompanyDevelopment component = StoryComponents.Development;
            CompanyExaminationDef examination = DefDatabase<CompanyExaminationDef>.GetNamedSilentFail(choice.examinationDefName);
            if (component == null || examination == null)
            {
                Messages.Message("LCOrdeal_DebugUnavailable".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            ExaminationContext activeContext;
            if (component.TryGetActiveExaminationContext(out activeContext))
            {
                activeContext.Cancel("LCOrdeal_DebugReplaced".Translate());
            }

            ExaminationRuntime runtime = component.GetExaminationRuntime(examination);
            runtime.status = ExaminationStatus.Inactive;
            runtime.retryAvailableTick = -1;
            SetForcedVariant(choice.examinationDefName, choice.variant);
            bool started = component.StartExamination(examination);
            SetForcedVariant(choice.examinationDefName, null);

            if (!started)
            {
                string reason;
                component.CanStartExamination(examination, out reason);
                Messages.Message(
                    "LCOrdeal_DebugStartFailed".Translate(reason ?? "LCOrdeal_DebugUnknown".Translate()),
                    MessageTypeDefOf.RejectInput);
            }
        }

        private static void SetForcedVariant(string examinationDefName, string variant)
        {
            if (examinationDefName == "LCOrdeal_Dawn") DawnOrdealWorker.DebugForceNextVariant(variant);
            else if (examinationDefName == "LCOrdeal_Noon") NoonOrdealWorker.DebugForceNextVariant(variant);
            else if (examinationDefName == "LCOrdeal_Dusk") DuskOrdealWorker.DebugForceNextVariant(variant);
            else if (examinationDefName == "LCOrdeal_Midnight") MidnightOrdealWorker.DebugForceNextVariant(variant);
        }

        private static void Spawn(PawnKindDef pawnKind)
        {
            Faction faction = FactionUtility.DefaultFactionFrom(pawnKind.defaultFactionDef) ?? Faction.OfMechanoids;
            Pawn pawn = PawnGenerator.GeneratePawn(pawnKind, faction, Find.CurrentMap.Tile);
            GenSpawn.Spawn(pawn, UI.MouseCell(), Find.CurrentMap);
        }
    }
}
