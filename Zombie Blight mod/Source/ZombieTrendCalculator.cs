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

        // Изменено: Теперь храним пары тик-значение
        private List<KeyValuePair<int, float>> history = new List<KeyValuePair<int, float>>();
        private const int MAX_HISTORY_LENGTH = 96; // Например, 1 день если шаг 15 минут

        public ZombieTrendCalculator()
        {
            randomSeed = Rand.Range(0f, 10000f);
            // Первое обновление будет при первом вызове GetCurrentTrendValue или другом методе,
            // который его вызывает, или при загрузке.
            // Или можно вызвать UpdateTrend() здесь, если нужно начальное значение сразу.
            UpdateTrend(); // Вызываем для инициализации
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

            // В конец метода
            // Изменено: Добавляем пару тик-значение
            history.Add(new KeyValuePair<int, float>(lastUpdateTick, currentValue));
            if (history.Count > MAX_HISTORY_LENGTH)
                history.RemoveAt(0);

        }

        private float GetTemperatureMultiplier(float temperature)
        {
            if (temperature < -40f) return 0.5f;         // -50% активности
            if (temperature < -20f) return 0.7f;         // -30% активности
            if (temperature < 0f) return 0.95f;          // -15% активности
            if (temperature < 35f) return 1f;            // Нормальная активность
            if (temperature < 45f) return 1.15f;         // +15% активности
            return 1.3f;                                 // +30% активности
        }

        private float GetWeatherMultiplier(WeatherDef weather)
        {
            // Проверяем наличие токсичных осадков как игрового условия
            if (Find.CurrentMap?.gameConditionManager?.ConditionIsActive(GameConditionDefOf.ToxicFallout) ?? false)
                return 0.9f; // -10% во время токсичных осадков

            // Используем статические свойства для WeatherDef
            if (weather == RainDef) return 0.95f;
            if (weather == ThunderstormDef) return 1.1f;


            return 1f;
        }
        // Перенесено сюда для удобства и использования в GetWeatherMultiplier
        private static WeatherDef RainDef => DefDatabase<WeatherDef>.GetNamedSilentFail("Rain");
        private static WeatherDef ThunderstormDef => DefDatabase<WeatherDef>.GetNamedSilentFail("Thunderstorm"); // Примерный деф-грозы


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

        // Изменено: Возвращаем IReadOnlyList<KeyValuePair<int, float>>
        public IReadOnlyList<KeyValuePair<int, float>> GetHistory()
        {
            return history.AsReadOnly();
        }

        public List<float> GetForecast(int steps = 8)
        {
            List<float> forecast = new List<float>();
            float timeScale = Find.TickManager.TicksGame / (GenDate.TicksPerDay * 2f);

            for (int i = 1; i <= steps; i++)
            {
                // Учитываем шаг времени в Perlin noise
                // Прогнозируем каждый час для примера (GenDate.TicksPerHour)
                // Масштаб времени должен соответствовать тому, как график отображает прогноз.
                // Если график отображает 8 часов вперед, то каждый шаг i * GenDate.TicksPerHour
                float futureTimeScale = (Find.TickManager.TicksGame + i * GenDate.TicksPerHour) / (GenDate.TicksPerDay * 2f);
                float futureNoise = Mathf.PerlinNoise(futureTimeScale, randomSeed);
                float value = baseLine + (futureNoise * 2f - 1f) * amplitude;

                if (Find.CurrentMap != null)
                {
                    float temp = Find.CurrentMap.mapTemperature.OutdoorTemp;
                    value *= GetTemperatureMultiplier(temp);
                    // Прогноз погоды сложен, здесь используется текущая погода как приближение
                    if (Find.CurrentMap.weatherManager.curWeather != null)
                    {
                        value *= GetWeatherMultiplier(Find.CurrentMap.weatherManager.curWeather);
                    }

                }

                forecast.Add(Mathf.Clamp01(value));
            }

            return forecast;
        }


        public void AddTemporaryAnomaly(float strength, int durationTicks)
        {
            // TODO: Реализовать систему временных аномалий
        }
    }
}