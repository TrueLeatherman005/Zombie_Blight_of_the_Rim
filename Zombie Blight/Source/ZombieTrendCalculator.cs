using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using UnityEngine;

namespace ZombieBlight
{
    public class ZombieTrendCalculator
    {
        private float baseLine = 0.5f; // Базовый уровень (50%)
        private float amplitude = 0.4f; // Амплитуда колебаний (±40%)
        private float currentValue = 0f;
        private float randomSeed;
        
        private const float UPDATE_INTERVAL = 900f; // 15 игровых минут
        private int lastUpdateTick = -1;

        public ZombieTrendCalculator()
        {
            randomSeed = Rand.Range(0f, 10000f);
            UpdateTrend();
        }

        public void UpdateTrend()
        {
            if (Find.TickManager == null || 
                (lastUpdateTick > 0 && Find.TickManager.TicksGame - lastUpdateTick < UPDATE_INTERVAL))
                return;

            lastUpdateTick = Find.TickManager.TicksGame;
            
            // Базовое значение тренда использует время и seed для Perlin noise
            float timeScale = Find.TickManager.TicksGame / (GenDate.TicksPerDay * 2f);
            float baseNoise = Mathf.PerlinNoise(timeScale, randomSeed);
            
            // Добавляем базовые колебания
            currentValue = baseLine + (baseNoise * 2f - 1f) * amplitude;

            // Применяем температурные модификаторы
            if (Find.CurrentMap != null)
            {
                float ambientTemperature = Find.CurrentMap.mapTemperature.OutdoorTemp;
                currentValue *= GetTemperatureMultiplier(ambientTemperature);
            }

            // Применяем погодные модификаторы
            if (Find.CurrentMap?.weatherManager?.curWeather != null)
            {
                currentValue *= GetWeatherMultiplier(Find.CurrentMap.weatherManager.curWeather);
            }

            // Ограничиваем значение
            currentValue = Mathf.Clamp01(currentValue);
        }

        private float GetTemperatureMultiplier(float temperature)
        {
            if (temperature < -40f) return 0.5f;        // -50% активности
            if (temperature < -20f) return 0.7f;        // -30% активности
            if (temperature < 0f) return 0.95f;          // -15% активности
            if (temperature < 35f) return 1f;           // Нормальная активность
            if (temperature < 45f) return 1.15f;        // +15% активности
            return 1.3f;                                // +30% активности
        }

        private float GetWeatherMultiplier(WeatherDef weather)
        {
            // Проверяем наличие токсичных осадков как игрового условия
            if (Find.CurrentMap?.gameConditionManager?.ConditionIsActive(GameConditionDefOf.ToxicFallout) ?? false)
                return 0.9f; // -10% во время токсичных осадков
                
            if (weather == WeatherDefOf.Thunder) return 1.1f;     // +10% во время грозы
            if (weather == WeatherDefOf.Rain) return 0.95f;       // -5% во время дождя
            return 1f;
        }

        public float GetCurrentTrendValue()
        {
            UpdateTrend();
            return currentValue;
        }

        public void SetBaseLine(float newBaseLine)
        {
            baseLine = Mathf.Clamp01(newBaseLine);
            UpdateTrend();
        }

        public void SetAmplitude(float newAmplitude)
        {
            amplitude = Mathf.Clamp01(newAmplitude);
            UpdateTrend();
        }

        public void AddTemporaryAnomaly(float strength, int durationTicks)
        {
            // TODO: Реализовать систему временных аномалий
        }
    }
}