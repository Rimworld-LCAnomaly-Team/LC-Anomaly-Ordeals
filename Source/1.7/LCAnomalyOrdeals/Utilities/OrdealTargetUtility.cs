using System;
using System.Collections.Generic;
using System.Linq;
using LCAnomalyCore.GameComponent;
using LCAnomalyStory;
using LCAnomalyStory.Components;
using Verse;

namespace LCAnomalyOrdeals.Utilities
{
    internal static class OrdealTargetUtility
    {
        internal static bool IsOrdealPawn(Pawn pawn)
        {
            return pawn?.kindDef?.defName?.StartsWith("LCOrdeal_", StringComparison.Ordinal) == true;
        }

        internal static bool IsValidTarget(Pawn pawn)
        {
            return pawn != null
                && pawn.Spawned
                && !pawn.Dead
                && !pawn.Destroyed
                && !IsOrdealPawn(pawn);
        }

        internal static List<Pawn> AllTargets(Map map)
        {
            return map == null
                ? new List<Pawn>()
                : map.mapPawns.AllPawnsSpawned.Where(IsValidTarget).ToList();
        }

        /// <summary>
        /// 考验镇压成功奖励闭环：记录考验镇压次数、向已开放部门发放分层经验、并补满主管战术子弹。
        /// </summary>
        internal static void GrantOrdealSuppressionLoopRewards(Map map, int tier)
        {
            GameComponent_CompanyDevelopment dev = StoryComponents.Development;
            if (dev != null)
            {
                dev.RecordStatistic("ordealsSuppressed", 1L);
                long expGain = Math.Max(1, tier) * 2L;
                foreach (string deptDefName in dev.OpenDepartments.ToList())
                {
                    dev.RecordStatistic("departmentExperience." + deptDefName, expGain);
                }
            }

            if (map != null)
            {
                GameComponent_LC.Instance?.RefillManagerBullets(map);
            }
        }
    }
}
