using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ZombieBlight
{
    // Основной класс мода
    public class BlightMod : Mod
    {
        public static Harmony harmony;
        
        public BlightMod(ModContentPack content) : base(content)
        {
            // Инициализация Harmony для патчей
            harmony = new Harmony("ZombieBlight.Mod");
            harmony.PatchAll();
            
            Log.Message("[Zombie Blight] Mod initialized.");
        }
    }
    
    // Статичный класс для Harmony патчей
    [StaticConstructorOnStartup]
    public static class BlightPatches
    {
        static BlightPatches()
        {
            // Регистрация компонентов
            RegisterCompTypes();
            
            // Добавление фракции зомби, если её нет
            CreateZombieFaction();
            
            // Патчи для совместимости с игрой и другими модами будут здесь
            ApplyAnomalyCompatPatches();
            
            Log.Message("[Zombie Blight] Patches applied successfully.");
        }
        
        // Регистрация компонентов для различных ThingDefs
        private static void RegisterCompTypes()
        {
            // Регистрируем CompBlightTreatedFood для всех блюд
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefs)
            {
                // Добавляем компонент к блюдам
                if (def.IsNutritionGivingIngestible && def.ingestible.HumanEdible && def.category == ThingCategory.Item)
                {
                    // Проверяем, есть ли уже компонент
                    bool hasComp = false;
                    if (def.comps != null)
                    {
                        foreach (CompProperties cp in def.comps)
                        {
                            if (cp.compClass == typeof(CompBlightTreatedFood))
                            {
                                hasComp = true;
                                break;
                            }
                        }
                    }
                    
                    // Добавляем, если нет
                    if (!hasComp)
                    {
                        // Инициализируем список компонентов, если он пуст
                        if (def.comps == null)
                        {
                            def.comps = new List<CompProperties>();
                        }
                        
                        // Добавляем компонент для добавки
                        def.comps.Add(new CompProperties { compClass = typeof(CompBlightTreatedFood) });
                    }
                }
            }
        }
        
        // Создаём фракцию зомби, если её ещё нет
        private static void CreateZombieFaction()
        {
            // Проверяем, есть ли уже фракция зомби
            FactionDef blightFaction = DefDatabase<FactionDef>.GetNamed("BlightSwarmFaction", false);
            
            // Если нет, создаём на основе дикарей
            if (blightFaction == null)
            {
                Log.Warning("[Zombie Blight] BlightSwarmFaction not found in DefDatabase, but it should be defined in XML. Mod may not function correctly.");
            }
        }
        
        // Патчи для совместимости с DLC Anomaly
        private static void ApplyAnomalyCompatPatches()
        {
            // Проверяем наличие DLC Anomaly
            if (ModsConfig.AnomalyActive)
            {
                Log.Message("[Zombie Blight] DLC Anomaly detected, applying compatibility patches.");
                
                // Здесь будут патчи для взаимодействия с шамблерами и другими механиками Anomaly
                // Они будут реализованы через Harmony
            }
        }
    }
    
    // Патч для блокировки превращения в шамблеров при наличии Blight инфекции
    [HarmonyPatch(typeof(MutantUtility), "CanResurrectAsShambler")]
    public static class Patch_CanResurrectAsShambler
    {
        // Патчим метод, который проверяет, может ли труп стать шамблером
        public static void Postfix(Corpse corpse, ref bool __result)
        {
            // Если патч ещё не запущен или уже false
            if (!__result || corpse?.InnerPawn == null) return;
            
            // Проверяем наличие Blight инфекции или DeathRefusal хедиффа
            Pawn innerPawn = corpse.InnerPawn;
            
            // Если есть инфекция Порчи, блокируем превращение в шамблера
            if (innerPawn.health?.hediffSet?.HasHediff(BlightDefOf.Hediff_BlightInfection) == true)
            {
                __result = false;
                return;
            }
            
            // Для контроля и дебага
            // Log.Message($"[Zombie Blight] Checking if {innerPawn.LabelShort} can become shambler: {__result}");
        }
    }
}
