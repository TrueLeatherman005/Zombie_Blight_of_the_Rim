using RimWorld;
using Verse;
using UnityEngine;

namespace ZombieBlight
{
    public class CompProperties_BlightTrace : CompProperties
    {
        public float baseStrength = 20f;
        public float decayRateBase = 0.00083f;
        public float decayRateOutdoors = 0.00166f;
        public float decayRateRain = 0.00332f;
        public float decayRateHighTemp = 0.00249f;

        public CompProperties_BlightTrace()
        {
            compClass = typeof(Comp_BlightTrace);
        }
    }

    public class Comp_BlightTrace : ThingComp
    {
        private float strength;
        private TerrainDef lastTerrain;

        public CompProperties_BlightTrace Props => (CompProperties_BlightTrace)props;

        public float Strength => strength;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!respawningAfterLoad)
            {
                strength = Props.baseStrength;
                lastTerrain = parent.Position.GetTerrain(parent.Map);
            }
        }

        public override void CompTick()
        {
            if (parent.Map == null) return;

            // Получаем текущий тип поверхности
            TerrainDef currentTerrain = parent.Position.GetTerrain(parent.Map);
            
            // Базовая скорость затухания
            float decayRate = Props.decayRateBase;

            // Множители затухания
            if (!parent.Position.Roofed(parent.Map))
            {
                decayRate = Props.decayRateOutdoors;

                // Проверяем погоду
                if (parent.Map.weatherManager.RainRate > 0.1f)
                {
                    decayRate = Props.decayRateRain;
                }
            }

            // Проверяем температуру
            if (parent.Position.GetTemperature(parent.Map) > 30f)
            {
                decayRate = Mathf.Max(decayRate, Props.decayRateHighTemp);
            }

            // Множители поверхности
            if (currentTerrain != null)
            {
                if (currentTerrain.HasTag("Marshy") || currentTerrain.HasTag("Water"))
                {
                    // Нельзя оставить след в воде/болоте
                    parent.Destroy();
                    return;
                }
                else if (currentTerrain.HasTag("Soil") || currentTerrain.HasTag("Sand"))
                {
                    decayRate *= 1.5f; // Быстрее на земле/песке
                }
                else if (currentTerrain.HasTag("Stony") || currentTerrain.HasTag("Metallic"))
                {
                    decayRate *= 0.5f; // Медленнее на камне/металле
                }
            }

            // Применяем затухание
            strength -= decayRate;

            // Уничтожаем след если сила исчезла
            if (strength <= 0f)
            {
                parent.Destroy();
                return;
            }
        }

        public void UpdateStrength(float newStrength)
        {
            strength = Mathf.Min(Props.baseStrength, newStrength);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref strength, "strength", 0f);
            Scribe_Defs.Look(ref lastTerrain, "lastTerrain");
        }
    }
}