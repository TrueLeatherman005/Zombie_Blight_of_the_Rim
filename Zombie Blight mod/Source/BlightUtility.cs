using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ZombieBlight
{
    public static class BlightUtility
    {
        // Проверка, имеет ли пешка иммунитет к заражению Порчей
        public static bool IsSomehowImmuneToBlight(Pawn pawn)
        {
            if (pawn == null) return true;
            
            // Проверка на механоидов и другие неорганические расы
            if (!pawn.RaceProps.IsFlesh) return true;
            
            // Проверка на активный хедифф иммунитета
            if (pawn.health?.hediffSet?.HasHediff(BlightDefOf.Hediff_BlightImmunity) == true)
                return true;
                
            // Проверка на состояние зомби (зомби не может заразиться повторно)
            if (pawn.health?.hediffSet?.HasHediff(BlightDefOf.Hediff_BlightZombieState) == true)
                return true;
                
            // Можно добавить проверки на стойкость (например, через Comp или трейты)
            
            return false;
        }
        
        // Получение уровня сопротивления заражению
        public static float GetBlightResistance(Pawn pawn)
        {
            if (pawn == null) return 0f;
            
            float resistance = 0f;
            
            // Проверка на хедифф временного сопротивления
            Hediff resistanceHediff = pawn.health?.hediffSet?.GetFirstHediffOfDef(BlightDefOf.Hediff_BlightResistance);
            if (resistanceHediff != null)
            {
                resistance += resistanceHediff.Severity;
            }
            
            // Можно добавить проверки на одежду, оборудование, трейты и т.д.
            
            return resistance;
        }
        
        // Попытка заразить пешку Порчей
        public static bool TryInfectPawn(Pawn pawn, float initialSeverity = 0.1f)
        {
            if (pawn == null || IsSomehowImmuneToBlight(pawn))
                return false;
                
            // Если у пешки уже есть заражение, усиливаем его
            Hediff existingInfection = pawn.health.hediffSet.GetFirstHediffOfDef(BlightDefOf.Hediff_BlightInfection);
            if (existingInfection != null)
            {
                // Увеличиваем тяжесть существующего заражения
                existingInfection.Severity += initialSeverity * 0.5f;
                return true;
            }
            
            // Создаем новое заражение
            Hediff blightInfection = HediffMaker.MakeHediff(BlightDefOf.Hediff_BlightInfection, pawn);
            blightInfection.Severity = initialSeverity;
            
            // Применяем сопротивление к начальной тяжести
            float resistance = GetBlightResistance(pawn);
            if (resistance > 0f)
            {
                blightInfection.Severity *= Math.Max(0.1f, 1f - resistance);
            }
            
            // Добавляем хедифф
            pawn.health.AddHediff(blightInfection);
            
            // Выводим сообщение, если это колонист
            if (pawn.Faction == Faction.OfPlayer)
            {
                Messages.Message(
                    "BlightInfection".Translate(pawn.LabelShort), 
                    pawn, 
                    MessageTypeDefOf.NegativeHealthEvent);
            }
            
            return true;
        }
        
        // Расчет шанса заражения от контакта со слизью
        public static float CalculateInfectionChanceFromSlime(Pawn pawn)
        {
            if (pawn == null || IsSomehowImmuneToBlight(pawn))
                return 0f;
                
            // Базовый шанс заражения
            float baseChance = 0.05f;
            
            // Уменьшаем шанс в зависимости от сопротивления
            float resistance = GetBlightResistance(pawn);
            float finalChance = baseChance * Math.Max(0.1f, 1f - resistance);
            
            // Можно добавить модификаторы от одежды, погоды и т.д.
            
            return finalChance;
        }
        
        // Попытка заразить от контакта со слизью
        public static void TryInfectFromSlimeContact(Pawn pawn)
        {
            if (!pawn.Spawned || pawn.Map == null) return;
            
            // Проверяем наличие слизи на клетке
            if (pawn.Position.GetThingList(pawn.Map).Any(t => t.def == BlightDefOf.Filth_BlightSlime))
            {
                float chance = CalculateInfectionChanceFromSlime(pawn);
                
                if (Rand.Chance(chance))
                {
                    TryInfectPawn(pawn, 0.05f);
                }
            }
        }
        
        // Применение эффекта добавки (сопротивления) к пешке
        public static void ApplyBlightResistanceEffect(Pawn pawn, float amount, int durationTicks)
        {
            if (pawn == null || pawn.Dead) return;
            
            // Если уже есть хедифф сопротивления, обновляем его
            Hediff existingResistance = pawn.health.hediffSet.GetFirstHediffOfDef(BlightDefOf.Hediff_BlightResistance);
            
            if (existingResistance != null)
            {
                // Увеличиваем тяжесть (уровень сопротивления)
                existingResistance.Severity = Math.Min(1f, existingResistance.Severity + amount);
                
                // Обновляем длительность, если класс поддерживает это
                if (existingResistance is HediffWithComps hwc)
                {
                    HediffComp_Disappears disappears = hwc.TryGetComp<HediffComp_Disappears>();
                    if (disappears != null)
                    {
                        // В реальной реализации здесь может быть более сложная логика
                        // для обновления времени через отражение или интерфейс
                        disappears.ticksToDisappear = Math.Max(disappears.ticksToDisappear, durationTicks);
                    }
                }
            }
            else
            {
                // Создаем новое сопротивление
                Hediff resistance = HediffMaker.MakeHediff(BlightDefOf.Hediff_BlightResistance, pawn);
                resistance.Severity = amount;
                
                // Добавляем компонент для исчезновения через время
                HediffComp_Disappears disappears = resistance.TryGetComp<HediffComp_Disappears>();
                if (disappears != null)
                {
                    disappears.ticksToDisappear = durationTicks;
                }
                
                // Добавляем хедифф
                pawn.health.AddHediff(resistance);
            }
            
            // Выводим сообщение для игрока
            if (pawn.Faction == Faction.OfPlayer)
            {
                Messages.Message(
                    "BlightResistanceApplied".Translate(pawn.LabelShort), 
                    pawn, 
                    MessageTypeDefOf.PositiveEvent);
            }
        }
        
        // Применение эффекта иммунитета к пешке
        public static void ApplyBlightImmunityEffect(Pawn pawn, int durationTicks)
        {
            if (pawn == null || pawn.Dead) return;
            
            // Удаляем текущее заражение, если оно есть
            Hediff existingInfection = pawn.health.hediffSet.GetFirstHediffOfDef(BlightDefOf.Hediff_BlightInfection);
            if (existingInfection != null)
            {
                pawn.health.RemoveHediff(existingInfection);
            }
            
            // Удаляем текущее сопротивление, так как иммунитет сильнее
            Hediff existingResistance = pawn.health.hediffSet.GetFirstHediffOfDef(BlightDefOf.Hediff_BlightResistance);
            if (existingResistance != null)
            {
                pawn.health.RemoveHediff(existingResistance);
            }
            
            // Проверяем существующий иммунитет
            Hediff existingImmunity = pawn.health.hediffSet.GetFirstHediffOfDef(BlightDefOf.Hediff_BlightImmunity);
            
            if (existingImmunity != null)
            {
                // Обновляем длительность, если класс поддерживает это
                if (existingImmunity is HediffWithComps hwc)
                {
                    HediffComp_Disappears disappears = hwc.TryGetComp<HediffComp_Disappears>();
                    if (disappears != null)
                    {
                        disappears.ticksToDisappear = Math.Max(disappears.ticksToDisappear, durationTicks);
                    }
                }
            }
            else
            {
                // Создаем новый иммунитет
                Hediff immunity = HediffMaker.MakeHediff(BlightDefOf.Hediff_BlightImmunity, pawn);
                
                // Добавляем компонент для исчезновения через время
                HediffComp_Disappears disappears = immunity.TryGetComp<HediffComp_Disappears>();
                if (disappears != null)
                {
                    disappears.ticksToDisappear = durationTicks;
                }
                
                // Добавляем хедифф
                pawn.health.AddHediff(immunity);
            }
            
            // Выводим сообщение для игрока
            if (pawn.Faction == Faction.OfPlayer)
            {
                Messages.Message(
                    "BlightImmunityApplied".Translate(pawn.LabelShort), 
                    pawn, 
                    MessageTypeDefOf.PositiveEvent);
            }
        }
        
        // Проверка наличия трупного тлена (для извлечения слизи)
        public static bool HasBlightCorpseTaint(Thing corpse)
        {
            if (corpse == null) return false;
            
            // Получаем труп как Corpse
            Corpse corpseThing = corpse as Corpse;
            if (corpseThing == null) return false;
            
            // Проверяем наличие хедиффа BlightCorpseTaint на трупе
            Pawn innerPawn = corpseThing.InnerPawn;
            if (innerPawn == null) return false;
            
            return innerPawn.health?.hediffSet?.HasHediff(BlightDefOf.Hediff_BlightCorpseTaint) == true;
        }
    }
}
