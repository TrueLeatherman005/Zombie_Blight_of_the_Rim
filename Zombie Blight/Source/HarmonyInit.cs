using HarmonyLib;
using Verse;

namespace ZombieBlight
{
    [StaticConstructorOnStartup]
    public static class HarmonyInit
    {
        static HarmonyInit()
        {
            var harmony = new Harmony("com.meast.zombieblight");
            harmony.PatchAll();
        }
    }
}
