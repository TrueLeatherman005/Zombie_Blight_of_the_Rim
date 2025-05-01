using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using UnityEngine; // Needed for Mathf

namespace ZombieBlight
{
    public class Hediff_BlightInfection : HediffWithComps
    {
        // Настройки скорости прогрессии (множители)
        private static readonly float ProgressBoostWhenDead = 1.5f;
        private static readonly float SlimeContactMultiplier = 1.3f; // При контакте со слизью

        // Кэшированные данные для проверки DLC Anomaly (статические)
        private static bool checkedForAnomalyDLC = false;
        private static bool hasAnomalyDLC = false;

        // Кэшированные данные для расчета множителя температуры (для каждого экземпляра хедиффа)
        private int lastSeverityCalcTick = -99999; // Тик последнего расчета температурного множителя
        private float cachedTemperatureProgressMultiplier = 1f; // Кэшированный множитель от температуры

        // Удалено неиспользуемое поле:
        // private float progress = 0f;

        public override void PostMake()
        {
            base.PostMake();
            pawn.health.hediffSet.DirtyCache(); // Убеждаемся, что кэши обновлены после создания
        }

        public override void ExposeData()
        {
            base.ExposeData();
            // Сохранение/загрузка специфичных для экземпляра кэшированных данных
            Scribe_Values.Look(ref lastSeverityCalcTick, "lastSeverityCalcTick", -99999);
            Scribe_Values.Look(ref cachedTemperatureProgressMultiplier, "cachedTemperatureProgressMultiplier", 1f);

            // Статические поля checkedForAnomalyDLC и hasAnomalyDLC не сохраняются/загружаются для каждого экземпляра хедиффа.
        }

        // Вызывается при добавлении хедиффа к пешке
        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);

            // Проверяем наличие DLC Anomaly один раз при первом добавлении хедиффа в игре (статическая проверка)
            if (!checkedForAnomalyDLC)
            {
                hasAnomalyDLC = ModsConfig.AnomalyActive; // Проверка наличия DLC Anomaly
                checkedForAnomalyDLC = true;
            }

            // Если пешка мертва И DLC Anomaly активно, пытаемся добавить Hediff_DeathRefusal
            // Note: Проверка на Anomaly DLC и pawn.Dead уже есть внутри AddDeathRefusalIfNeeded,
            // но вызов здесь гарантирует, что проверка/добавление произойдет сразу при добавлении хедиффа к мертвому.
            if (hasAnomalyDLC && pawn.Dead)
            {
                AddDeathRefusalIfNeeded();
            }

            // Инициализируем тик для первого расчета температурного множителя
            lastSeverityCalcTick = Find.TickManager.TicksGame;

            // Обновляем температурный множитель сразу после добавления
            UpdateTemperatureMultiplier();
        }

        // Добавляет Hediff_DeathRefusal (из DLC Anomaly) если он отсутствует и пешка мертва.
        // Используется для блокировки базовых механизмов оживления (например, шаблеров).
        private void AddDeathRefusalIfNeeded()
        {
            // Проверяем наличие DLC Anomaly и состояние пешки
            if (!hasAnomalyDLC || !pawn.Dead) return; // Только если Anomaly активно И пешка мертва

            // Пытаемся получить HediffDef DeathRefusal. Второй аргумент 'false' предотвращает ошибку, если Def не найден (например, если DLC не активно).
            HediffDef deathRefusalDef = DefDatabase<HediffDef>.GetNamed("DeathRefusal", false);

            // Если Def найден И пешка еще не имеет этого хедиффа
            if (deathRefusalDef != null && !pawn.health.hediffSet.HasHediff(deathRefusalDef))
            {
                // Создаем и добавляем хедифф DeathRefusal
                Hediff deathRefusal = HediffMaker.MakeHediff(deathRefusalDef, pawn);
                pawn.health.AddHediff(deathRefusal);
            }
        }

        // Метод вызывается каждый тик игры для этого хедиффа
        public override void Tick()
        {
            base.Tick(); // Всегда вызываем базовый Tick()

            // Обновляем множитель температуры периодически (каждые 2000 тиков)
            // Делаем это только для мертвых пешек, так как логика множителя заточена под них
            if (pawn.Dead && Find.TickManager.TicksGame - lastSeverityCalcTick >= 2000)
            {
                lastSeverityCalcTick = Find.TickManager.TicksGame;
                UpdateTemperatureMultiplier();
            }

            // Получаем базовый прирост тяжести В ДЕНЬ из компонента HediffComp_SeverityPerDay
            // Этот компонент обычно добавляется в XML HediffDef.
            // Если компонент SeverityPerDay отсутствует в XML, baseChangePerDay будет 0f.
            float baseChangePerDay = this.TryGetComp<HediffComp_SeverityPerDay>()?.SeverityChangePerDay() ?? 0f;

            // Конвертируем базовый прирост в прирост за ОДИН ТИК
            float baseChangePerTick = baseChangePerDay / GenDate.TicksPerDay;

            // Вычисляем итоговый прирост за тик, применяя множители

            float finalChangePerTick = baseChangePerTick; // Начинаем с базового прироста

            // Множитель для трупов: если пешка мертва, ускоряем прогрессию
            if (pawn.Dead)
            {
                finalChangePerTick *= ProgressBoostWhenDead;
            }

            // Множитель температуры: применяем кэшированный множитель
            finalChangePerTick *= cachedTemperatureProgressMultiplier;

            // Множитель контакта со слизью: если пешка контактирует со слизью, ускоряем
            // Note: SlimeContactMultiplier - прямой множитель.
            if (IsInContactWithSlime())
            {
                finalChangePerTick *= SlimeContactMultiplier;
            }

            // Применяем изменение к тяжести и ограничиваем в пределах 0-1
            Severity = Mathf.Clamp01(Severity + finalChangePerTick);

            // Если достигли 100% (терминальной стадии) на мертвой пешке, превращаем в зомби
            // Проверяем после применения SeverithChange, чтобы удостовериться, что 1f достигнут.
            // Важно: Превращаем только если пешка мертва!
            if (pawn.Dead && Severity >= 1f)
            {
                TransformToZombie();
            }
        }

        // Обновляет множитель прогрессии в зависимости от температуры
        private void UpdateTemperatureMultiplier()
        {

            // Обновляем только для мертвых пешек, которые заспавнены на карте
            if (!pawn.Dead || pawn?.Map == null)
            {
                cachedTemperatureProgressMultiplier = 0f; // Или 1f, зависит от логики вне смерти
                return;
            }


            float ambientTemperature = pawn.Position.GetTemperature(pawn.Map); // Температура в клетке пешки
            // Получаем комфортную температуру из статов пешки (используем комфорт живого для определения влияния)
            float minComfortTemp = pawn.GetStatValue(StatDefOf.ComfyTemperatureMin);
            float maxComfortTemp = pawn.GetStatValue(StatDefOf.ComfyTemperatureMax);

            // Рассчитываем множитель: 1x в комфортном диапазоне, замедление вне его.
            // Замедление линейное: 0 при температуре на 10 градусов ниже/выше комфорта.
            // Это соответствует логике кода, но, как обсуждалось, может быть не идеальным для трупа.
            // Для трупа, возможно, стоит использовать фиксированные пороги для остановки (0C, 60C?)
            // и множитель, зависящий от диапазона (оптимальный, нормальный, замедленный).
            // Текущая логика: полная скорость в комфорте, линейное падение до 0 за 10 градусов вне комфорта.
            if (ambientTemperature < minComfortTemp)
            {
                float tempDiff = minComfortTemp - ambientTemperature; // Насколько ниже комфорта
                cachedTemperatureProgressMultiplier = Mathf.Max(0f, 1f - (tempDiff / 10f)); // Замедление, до 0 при -10 от minComfort
            }
            else if (ambientTemperature > maxComfortTemp)
            {
                float tempDiff = ambientTemperature - maxComfortTemp; // Насколько выше комфорта
                cachedTemperatureProgressMultiplier = Mathf.Max(0f, 1f - (tempDiff / 10f)); // Замедление, до 0 при +10 от maxComfort
            }
            else // В комфортном диапазоне (minComfortTemp <= ambientTemperature <= maxComfortTemp)
            {
                cachedTemperatureProgressMultiplier = 1f; // Полная скорость
            }

            // Дополнительная проверка на экстремальные температуры, которые должны полностью останавливать прогресс (если нужно)
            // Например, полное замерзание или сгорание
            if (ambientTemperature < -5f || ambientTemperature > 70f) // Примерные пороги, можно уточнить
            {
                cachedTemperatureProgressMultiplier = 0f;
            }
        }

        // Проверяет контакт со слизью
        private bool IsInContactWithSlime()
        {
            if (!pawn.Spawned) return false; // Только для заспавненных пешек

            // Проверка на наличие предмета типа "Filth_BlightSlime" на клетке пешки
            List<Thing> thingsOnTile = pawn.Position.GetThingList(pawn.Map);
            // Используем LINQ Any() для быстрой проверки наличия по defName
            // Убедитесь, что Filth_BlightSlime - корректный DefName для вашей слизи.
            return thingsOnTile.Any(t => t.def.defName == "Filth_BlightSlime");
        }

        // Превращает мертвую пешку в зомби при достижении 100% тяжести инфекции
        private void TransformToZombie()
        {
            // Проверяем, что пешка мертва и еще не имеет зомби-состояния
            if (!pawn.Dead || pawn.health.hediffSet.HasHediff(BlightDefOf.Hediff_BlightZombieState))
                return;

            // Проверяем, что определение хедиффа зомби доступно
            if (BlightDefOf.Hediff_BlightZombieState == null)
            {
                Log.ErrorOnce("ZombieBlight: Cannot transform to zombie. Hediff_BlightZombieState Def is null.", -9999999);
                return;
            }


            // Добавляем хедифф зомби-состояния
            Hediff zombieState = HediffMaker.MakeHediff(BlightDefOf.Hediff_BlightZombieState, pawn);
            pawn.health.AddHediff(zombieState); // Добавляем хедифф пешке

            // Удаляем этот хедифф (инфекцию), так как он выполнил свою роль превращения
            pawn.health.RemoveHediff(this);
        }

        // Переопределяем, чтобы обрабатывать события смерти пешки, пока на ней есть этот хедифф
        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit)
        {
            // Всегда вызываем базовый метод первым! Это важно для корректной работы наследования.
            base.Notify_PawnDied(dinfo, culprit);

            // Добавляем DeathRefusal для блокировки базовых механизмов оживления (например, шамблеров), если DLC Anomaly активно
            // Проверка hasAnomalyDLC и pawn.Dead делается внутри AddDeathRefusalIfNeeded().
            AddDeathRefusalIfNeeded(); // Вызываем метод для добавления DeathRefusal при смерти

            // Обновляем множитель температуры сразу после смерти.
            // Смерть может изменить условия (например, пешка перестала двигаться, температура тела выравнивается с окружающей).
            UpdateTemperatureMultiplier();

            // При смерти инфекция может резко прогрессировать до 100% или быть уже близка к этому.
            // Проверяем, не нужно ли сразу превратить в зомби, если тяжесть уже достигла 100% при смерти.
            // Это важно, если хедифф сохраняется на мертвой пешке (через KeepOnDeath).
            if (pawn.Dead && Severity >= 1f)
            {
                TransformToZombie();
            }
        }

        // Можно добавить другие методы, если нужно обрабатывать что-то еще (например, OnIntervalPassed для не-тиковой логики)
        // public override void CompInterval()
        // {
        //     base.CompInterval();
        //     // Логика, выполняемая реже, чем каждый тик (например, каждые 2500 тиков)
        // }

    }
}
