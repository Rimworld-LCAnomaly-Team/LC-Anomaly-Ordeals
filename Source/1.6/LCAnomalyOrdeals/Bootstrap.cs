using HarmonyLib;
using Verse;

namespace LCAnomalyOrdeals
{
    [StaticConstructorOnStartup]
    internal static class Bootstrap
    {
        static Bootstrap()
        {
            new Harmony("DarthCY.LC.AnomalyOrdeals").PatchAll();
            Log.Message("[LC Anomaly Ordeals] RimWorld 1.6 ordeal content initialized.");
        }
    }
}
