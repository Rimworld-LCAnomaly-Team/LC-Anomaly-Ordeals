using HarmonyLib;
using LCAnomalyOrdeals.Meltdown;
using Verse;

namespace LCAnomalyOrdeals
{
    [StaticConstructorOnStartup]
    internal static class Bootstrap
    {
        static Bootstrap()
        {
            new Harmony("DarthCY.LC.AnomalyOrdeals").PatchAll(typeof(Bootstrap).Assembly);
            MeltdownOrdealBridge.Register();
            Log.Message("[LC Anomaly Ordeals] RimWorld 1.7 ordeal content initialized.");
        }
    }
}
