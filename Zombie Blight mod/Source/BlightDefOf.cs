using System;
using RimWorld;
using Verse;

namespace ZombieBlight
{
    [DefOf]
    public static class BlightDefOf
    {
        static BlightDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(BlightDefOf));
        }

        // Hediffs
        public static HediffDef Hediff_BlightInfection;
        public static HediffDef Hediff_BlightZombieState;
        public static HediffDef Hediff_BlightCorpseTaint;
        public static HediffDef Hediff_PostBlightWeakness;
        public static HediffDef Hediff_BlightResistance;
        public static HediffDef Hediff_BlightImmunity;
        public static HediffDef Hediff_BlightStarvation;
        public static HediffDef Hediff_BlightFoodRestriction;

        // Вещи
        public static ThingDef Item_BlightSlimePack;
        public static ThingDef Item_BlightCells;
        public static ThingDef Item_BlightResistantAdditive;
        public static ThingDef BlightCleanseSerum_Herbal;
        public static ThingDef BlightCleanseSerum_Industrial;
        public static ThingDef BlightCleanseSerum_Glitter;

        // Загрязнение
        public static ThingDef Filth_BlightSlime;

        // Исследования
        public static ResearchProjectDef BlightStudies;
        public static ResearchProjectDef BlightTreatmentBasic;
        public static ResearchProjectDef BlightTreatmentAdvanced;
        public static ResearchProjectDef BlightTreatmentGlitterworld;
        public static ResearchProjectDef BlightWeaponryBasic;
        public static ResearchProjectDef BlightWeaponryAdvanced;

        // Фракция зомби
        public static FactionDef BlightSwarmFaction;


        
    }
}
