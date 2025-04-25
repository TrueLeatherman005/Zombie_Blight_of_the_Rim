using Verse;
using RimWorld;

namespace ZombieBlight
{
    public class CompProperties_BlightProgress : CompProperties
    {
        public CompProperties_BlightProgress()
        {
            compClass = typeof(Comp_BlightProgress);
        }
    }

    public class Comp_BlightProgress : ThingComp
    {
        private float progressPerTick = 0.00005f; // Базовая скорость прогрессии
        private float progress = 0f;

        public override void CompTick()
        {
            base.CompTick();

            if (parent is Corpse corpse)
            {
                // Проверяем температуру
                float temp = parent.AmbientTemperature;
                float minTemp = corpse.InnerPawn.GetStatValue(StatDefOf.ComfyTemperatureMin);
                float maxTemp = corpse.InnerPawn.GetStatValue(StatDefOf.ComfyTemperatureMax);

                // Остановка при экстремальных температурах
                if (temp < minTemp || temp > maxTemp)
                    return;

                // Ускорение при оптимальной температуре
                float tempMultiplier = 1f;
                if (temp >= 15f && temp <= 30f)
                    tempMultiplier = 1.5f;

                progress += progressPerTick * tempMultiplier;

                // При достижении 100% начинаем воскрешение
                if (progress >= 1f)
                {
                    // Добавляем BlightZombieState хедифф, который сам запустит воскрешение
                    Hediff zombieState = HediffMaker.MakeHediff(BlightDefOf.Hediff_BlightZombieState, corpse.InnerPawn);
                    corpse.InnerPawn.health.AddHediff(zombieState);
                }
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref progress, "blightProgress", 0f);
        }

        public float GetProgress()
        {
            return progress;
        }

        public void SetProgress(float newProgress)
        {
            progress = newProgress;
        }
    }
}