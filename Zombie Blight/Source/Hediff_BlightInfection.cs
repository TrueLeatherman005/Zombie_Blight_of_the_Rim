using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ZombieBlight
{
    public class Hediff_BlightInfection : HediffWithComps
    {
        // Настройки скорости прогрессии
        private static readonly float ProgressBoostWhenDead = 1.5f;
        private static readonly float SlimeContactMultiplier = 1.3f; // При контакте со слизью

        // Кэшированные данные
        private bool checkedForAnomalyDLC = false;
        private bool hasAnomalyDLC = false;
        private int lastSeverityCalcTick = -99999;
        private float cachedTemperatureProgressMultiplier = 1f;

        public override void PostMake()
        {
            base.PostMake();
            pawn.health.hediffSet.DirtyCache();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref lastSeverityCalcTick, "lastSeverityCalcTick", -99999);
            Scribe_Values.Look(ref cachedTemperatureProgressMultiplier, "cachedTemperatureProgressMultiplier", 1f);
        }

        // Вызывается при добавлении хедиффа
        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            
            // Проверяем наличие DLC Anomaly, если ещё не проверяли
            if (!checkedForAnomalyDLC)
            {
                hasAnomalyDLC = ModsConfig.AnomalyActive; // Проверка наличия DLC Anomaly
                checkedForAnomalyDLC = true;
            }
            
            // Если пешка мертва, добавляем DeathRefusal чтобы блокировать шамблеров
            if (hasAnomalyDLC && pawn.Dead)
            {
                AddDeathRefusalIfNeeded();
            }
            
            lastSeverityCalcTick = Find.TickManager.TicksGame;
        }

        // Добавляем DeathRefusal для блокировки шамблеров
        private void AddDeathRefusalIfNeeded()
        {
            // В реальной реализации нужно проверить наличие HediffDefOf.DeathRefusal
            // и добавить, если его нет
            if (hasAnomalyDLC && !pawn.health.hediffSet.HasHediff(DefDatabase<HediffDef>.GetNamed("DeathRefusal", false)))
            {
                Hediff deathRefusal = HediffMaker.MakeHediff(DefDatabase<HediffDef>.GetNamed("DeathRefusal", false), pawn);
                pawn.health.AddHediff(deathRefusal);
            }
        }

        public override void Tick()
        {
            base.Tick();
            
            // Обновляем множитель температуры каждые 2000 тиков
            if (Find.TickManager.TicksGame - lastSeverityCalcTick >= 2000)
            {
                lastSeverityCalcTick = Find.TickManager.TicksGame;
                UpdateTemperatureMultiplier();
            }

            // Если достигли 100% (терминальной стадии) на мертвой пешке, превращаем в зомби
            if (pawn.Dead && Severity >= 1f)
            {
                TransformToZombie();
            }
        }

        // Обновляет множитель прогрессии в зависимости от температуры
        private void UpdateTemperatureMultiplier()
        {
            if (!pawn.Dead || pawn?.Map == null) return;

            float ambientTemperature = pawn.Position.GetTemperature(pawn.Map);
            float minComfortTemp = pawn.GetStatValue(StatDefOf.ComfortableTemperatureMin);
            float maxComfortTemp = pawn.GetStatValue(StatDefOf.ComfortableTemperatureMax);

            // Слишком холодно или слишком жарко - инфекция замедляется или останавливается
            if (ambientTemperature < minComfortTemp)
            {
                // Линейное замедление от 1.0 до 0 по мере понижения температуры
                float tempDiff = minComfortTemp - ambientTemperature;
                cachedTemperatureProgressMultiplier = Math.Max(0f, 1f - (tempDiff / 10f)); // Полная остановка при температуре на 10 градусов ниже минимальной комфортной
            }
            else if (ambientTemperature > maxComfortTemp)
            {
                // Линейное замедление от 1.0 до 0 по мере повышения температуры
                float tempDiff = ambientTemperature - maxComfortTemp;
                cachedTemperatureProgressMultiplier = Math.Max(0f, 1f - (tempDiff / 10f)); // Полная остановка при температуре на 10 градусов выше максимальной комфортной
            }
            else
            {
                cachedTemperatureProgressMultiplier = 1f;
            }
        }

        // Проверяет контакт со слизью
        private bool IsInContactWithSlime()
        {
            if (!pawn.Spawned) return false;

            // Проверка на наличие слизи на клетке пешки
            List<Thing> thingsOnTile = pawn.Position.GetThingList(pawn.Map);
            return thingsOnTile.Any(t => t.def.defName == "Filth_BlightSlime");
        }

        // Превращает мертвую пешку в зомби
        private void TransformToZombie()
        {
            // Проверяем уже наличие зомби-состояния
            if (pawn.health.hediffSet.HasHediff(BlightDefOf.Hediff_BlightZombieState))
                return;
            
            // Добавляем зомби-состояние
            Hediff zombieState = HediffMaker.MakeHediff(BlightDefOf.Hediff_BlightZombieState, pawn);
            pawn.health.AddHediff(zombieState);
            
            // Удаляем инфекцию, так как она выполнила свою роль
            pawn.health.RemoveHediff(this);
        }

        // Изменяет прирост тяжести
        public override float SeverityChangePerDay()
        {
            // Получаем базовый прирост из компонента SeverityPerDay
            float change = this.TryGetComp<HediffComp_SeverityPerDay>()?.SeverityChangePerDay() ?? 0f;
            
            // Множитель для трупов
            if (pawn.Dead)
            {
                change *= ProgressBoostWhenDead;
            }
            
            // Множитель температуры
            change *= cachedTemperatureProgressMultiplier;
            
            // Множитель контакта со слизью
            if (IsInContactWithSlime())
            {
                change *= SlimeContactMultiplier;
            }
            
            return change;
        }

        // Вызывается когда пешка умирает
        public override void Notify_PawnDied()
        {
            base.Notify_PawnDied();
            
            // Добавляем DeathRefusal чтобы блокировать шамблеров, если активно DLC Anomaly
            if (hasAnomalyDLC)
            {
                AddDeathRefusalIfNeeded();
            }
            
            // Обновляем множитель температуры сразу
            UpdateTemperatureMultiplier();
        }
    }
}
