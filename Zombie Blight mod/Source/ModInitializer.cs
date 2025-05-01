using Verse;
using HarmonyLib;
using UnityEngine;

namespace ZombieBlight
{
    [StaticConstructorOnStartup]
    public static class ModInitializer
    {
        static ModInitializer()
        {
            var harmony = new Harmony("ZombieBlight.ThreatGraph");
            harmony.PatchAll();
            
            // Инициализация UI при запуске мода
            ZombieThreatUI.Initialize();
        }
    }

    // Патч для обновления графика каждый тик
    [HarmonyPatch(typeof(TickManager), "DoSingleTick")]
    public static class TickManager_DoSingleTick_Patch
    {
        public static void Postfix()
        {
            if (Time.frameCount % 60 == 0) // Обновляем раз в секунду
            {
                GameObject uiObject = GameObject.Find("ZombieThreatUI");
                if (uiObject != null)
                {
                    var ui = uiObject.GetComponent<ZombieThreatUI>();
                    ui?.UpdateThreat();
                }
            }
        }
    }
}