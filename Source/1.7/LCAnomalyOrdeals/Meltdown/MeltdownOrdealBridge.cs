using System;
using System.Collections.Generic;
using LCAnomalyCore.Buildings;
using LCAnomalyCore.GameComponent;
using LCAnomalyCore.Settings;
using LCAnomalyOrdeals.Utilities;
using LCAnomalyStory;
using LCAnomalyStory.Components;
using LCAnomalyStory.Defs;
using RimWorld;
using Verse;

namespace LCAnomalyOrdeals.Meltdown
{
    /// <summary>
    /// 逆卡巴拉熔毁过载波次 × 考验里程碑联动桥接器：
    /// 当当日熔毁等级推进至 II / IV / VI / VIII 时，在公司发展阶段满足前置条件且无进行中考核的情况下自动触发黎明 / 正午 / 黄昏 / 午夜考验，
    /// 并向顶部熔毁监控 HUD 注入下一考验里程碑预报文案。
    /// </summary>
    public static class MeltdownOrdealBridge
    {
        private readonly struct OrdealMilestone
        {
            public readonly int meltdownLevel;
            public readonly int minStoryPhase;
            public readonly string examDefName;
            public readonly string tierNameKey;

            public OrdealMilestone(int meltdownLevel, int minStoryPhase, string examDefName, string tierNameKey)
            {
                this.meltdownLevel = meltdownLevel;
                this.minStoryPhase = minStoryPhase;
                this.examDefName = examDefName;
                this.tierNameKey = tierNameKey;
            }
        }

        private static readonly OrdealMilestone[] Schedule =
        {
            new OrdealMilestone(2, 1, "LCOrdeal_Dawn", "LCOrdeal_Milestone_Dawn"),
            new OrdealMilestone(4, 2, "LCOrdeal_Noon", "LCOrdeal_Milestone_Noon"),
            new OrdealMilestone(6, 3, "LCOrdeal_Dusk", "LCOrdeal_Milestone_Dusk"),
            new OrdealMilestone(8, 4, "LCOrdeal_Midnight", "LCOrdeal_Milestone_Midnight")
        };

        private static bool registered;

        /// <summary>向 LC Anomaly Core 注册熔毁事件订阅与里程碑预报回调（幂等）。</summary>
        public static void Register()
        {
            if (registered)
            {
                return;
            }

            registered = true;
            MapComponent_LCQliphothMeltdown.OnMeltdownWaveTriggered += OnMeltdownWaveTriggered;
            MapComponent_LCQliphothMeltdown.NextMilestonePreviewProvider = GetNextMilestonePreview;
        }

        /// <summary>计算当前熔毁等级下即将到来的下一个可触发考验里程碑提示文案。</summary>
        public static string GetNextMilestonePreview(Map map, int currentMeltdownLevel)
        {
            if (Setting_LCAnomalyCore_Main.Settings?.If_EnableMeltdownOrdealTrigger != true)
            {
                return null;
            }

            GameComponent_CompanyDevelopment dev = StoryComponents.Development;
            if (dev == null)
            {
                return null;
            }

            for (int i = 0; i < Schedule.Length; i++)
            {
                OrdealMilestone milestone = Schedule[i];
                if (milestone.meltdownLevel <= currentMeltdownLevel)
                {
                    continue;
                }

                if (!IsMilestoneEligible(dev, milestone, out _))
                {
                    continue;
                }

                string tierLabel = milestone.tierNameKey.Translate().ToString();
                return "LCOrdeal_MeltdownNextMilestone".Translate(tierLabel, ToRoman(milestone.meltdownLevel)).ToString();
            }

            return null;
        }

        private static void OnMeltdownWaveTriggered(Map map, int meltdownLevel, List<Building_AbnormalityContainmentTerminal> triggered)
        {
            if (map == null || Setting_LCAnomalyCore_Main.Settings?.If_EnableMeltdownOrdealTrigger != true)
            {
                return;
            }

            GameComponent_CompanyDevelopment dev = StoryComponents.Development;
            if (dev == null || dev.ActiveExamination != null)
            {
                return;
            }

            for (int i = 0; i < Schedule.Length; i++)
            {
                OrdealMilestone milestone = Schedule[i];
                if (milestone.meltdownLevel != meltdownLevel)
                {
                    continue;
                }

                if (!IsMilestoneEligible(dev, milestone, out CompanyExaminationDef exam))
                {
                    return;
                }

                bool started = dev.IsExaminationPassed(exam)
                    ? dev.CanStartRepeatableExamination(exam, out _) && dev.StartRepeatableExamination(exam)
                    : dev.CanStartExamination(exam) && dev.StartExamination(exam);

                if (started)
                {
                    Messages.Message(
                        "LCOrdeal_MeltdownTriggeredOrdeal".Translate(exam.LabelCap, ToRoman(meltdownLevel)),
                        MessageTypeDefOf.ThreatBig);
                }
                return;
            }
        }

        private static bool IsMilestoneEligible(GameComponent_CompanyDevelopment dev, OrdealMilestone milestone, out CompanyExaminationDef exam)
        {
            exam = null;
            if (dev == null || dev.CurrentPhase < milestone.minStoryPhase)
            {
                return false;
            }

            exam = DefDatabase<CompanyExaminationDef>.GetNamedSilentFail(milestone.examDefName);
            if (exam == null)
            {
                return false;
            }

            return dev.IsExaminationPassed(exam) || dev.IsExaminationNodePrerequisitesMet(exam);
        }

        private static string ToRoman(int number)
        {
            switch (number)
            {
                case 2: return "II";
                case 4: return "IV";
                case 6: return "VI";
                case 8: return "VIII";
                default: return number.ToString();
            }
        }
    }

    /// <summary>
    /// 考验实体警报联动组件：当场存在在场高阶考验实体时，抬升全设施最低警报点数并触发对应阶梯警报（一/二/三阶警报音乐与横幅）。
    /// </summary>
    public class GameComponent_OrdealWarningBridge : Verse.GameComponent
    {
        public GameComponent_OrdealWarningBridge(Game game)
        {
        }

        /// <inheritdoc />
        public override void GameComponentTick()
        {
            if (Find.TickManager == null
                || Find.TickManager.TicksGame % 90 != 0
                || Setting_LCAnomalyCore_Main.Settings?.If_EnableLCWarning != true
                || GameComponent_LC.Instance == null
                || Find.Maps == null)
            {
                return;
            }

            int peakPoints = 0;
            for (int m = 0; m < Find.Maps.Count; m++)
            {
                IReadOnlyList<Pawn> pawns = Find.Maps[m]?.mapPawns?.AllPawnsSpawned;
                if (pawns == null)
                {
                    continue;
                }

                for (int p = 0; p < pawns.Count; p++)
                {
                    Pawn pawn = pawns[p];
                    if (pawn == null || pawn.Dead || pawn.Destroyed || !OrdealTargetUtility.IsOrdealPawn(pawn))
                    {
                        continue;
                    }

                    int pts = GetWarningPointsForOrdealKind(pawn.kindDef?.defName);
                    if (pts > peakPoints)
                    {
                        peakPoints = pts;
                    }
                }
            }

            if (peakPoints > 0 && GameComponent_LC.Instance.CurWarningPoints < peakPoints)
            {
                GameComponent_LC.Instance.CurWarningPoints = peakPoints;
            }
        }

        /// <summary>根据考验实体 PawnKind defName 返回对应警报点数下限。</summary>
        public static int GetWarningPointsForOrdealKind(string kindDefName)
        {
            if (string.IsNullOrEmpty(kindDefName))
            {
                return 0;
            }

            if (kindDefName.StartsWith("LCOrdeal_White", StringComparison.Ordinal)
                || kindDefName.IndexOf("Midnight", StringComparison.Ordinal) >= 0)
            {
                return 85;
            }

            if (kindDefName.IndexOf("Dusk", StringComparison.Ordinal) >= 0)
            {
                return 55;
            }

            if (kindDefName.IndexOf("Noon", StringComparison.Ordinal) >= 0)
            {
                return 35;
            }

            if (kindDefName.IndexOf("Dawn", StringComparison.Ordinal) >= 0)
            {
                return 15;
            }

            return 0;
        }
    }
}
