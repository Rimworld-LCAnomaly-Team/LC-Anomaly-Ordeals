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
        }
    }
}
