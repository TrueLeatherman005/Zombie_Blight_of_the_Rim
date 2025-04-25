using System;
using RimWorld;
using Verse;

namespace ZombieBlight
{
    // Базовый класс для хедиффов сопротивления и иммунитета
    public class Hediff_BlightResistanceBase : HediffWithComps
    {
        // Применяется ли эффект до размножения инфекции
        protected virtual bool AppliesToExistingInfection => true;
        
        // Множитель эффективности (1.0 = 100% защита)
        protected virtual float EffectivenessMultiplier => 1.0f;
        
        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            
            // Применяем эффект к существующей инфекции
            if (AppliesToExistingInfection)
            {
                TryReduceExistingInfection();
            }
        }
        
        // Уменьшение тяжести существующей инфекции
        protected void TryReduceExistingInfection()
        {
            if (pawn?.health?.hediffSet == null) return;
            
            // Находим существующую инфекцию
            Hediff blightInfection = pawn.health.hediffSet.GetFirstHediffOfDef(BlightDefOf.Hediff_BlightInfection);
            if (blightInfection != null)
            {
                // Рассчитываем снижение тяжести в зависимости от уровня сопротивления
                float reductionAmount = Severity * EffectivenessMultiplier * 0.5f;
                blightInfection.Severity = Math.Max(0.01f, blightInfection.Severity - reductionAmount);
                
                // Если достаточно снизили тяжесть, можно удалить инфекцию
                if (blightInfection.Severity <= 0.05f)
                {
                    pawn.health.RemoveHediff(blightInfection);
                    
                    // Выводим сообщение для игрока
                    if (pawn.Faction == Faction.OfPlayer)
                    {
                        Messages.Message(
                            "BlightInfectionCured".Translate(pawn.LabelShort), 
                            pawn, 
                            MessageTypeDefOf.PositiveEvent);
                    }
                }
            }
        }
        
        // Выдача бонусов к иммунитету для расчета статов
        public override void PostMake()
        {
            base.PostMake();
            pawn.health.Notify_HediffChanged(this);
        }
    }
    
    // Реализация сопротивления Блайту
    public class Hediff_BlightResistance : Hediff_BlightResistanceBase
    {
        protected override bool AppliesToExistingInfection => true;
        protected override float EffectivenessMultiplier => 0.7f;
        
        // Дополнительные методы, специфичные для сопротивления
        public override float SeverityChangePerDay()
        {
            // Сопротивление не меняется самостоятельно, 
            // оно управляется через HediffComp_Disappears
            return 0f;
        }
    }
    
    // Реализация полного иммунитета к Блайту
    public class Hediff_BlightImmunity : Hediff_BlightResistanceBase
    {
        protected override bool AppliesToExistingInfection => true;
        protected override float EffectivenessMultiplier => 1.0f;
        
        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            
            // Полный иммунитет полностью удаляет любое сопротивление, так как он сильнее
            if (pawn?.health?.hediffSet != null)
            {
                Hediff resistance = pawn.health.hediffSet.GetFirstHediffOfDef(BlightDefOf.Hediff_BlightResistance);
                if (resistance != null)
                {
                    pawn.health.RemoveHediff(resistance);
                }
            }
        }
        
        // Дополнительные методы, специфичные для иммунитета
        public override float SeverityChangePerDay()
        {
            // Иммунитет не меняется самостоятельно, 
            // он управляется через HediffComp_Disappears
            return 0f;
        }
    }
    
    // Класс для трупного тлена (после смерти зомби)
    public class Hediff_BlightCorpseTaint : HediffWithComps
    {
        // Константы
        private const float InitialSeverity = 1.0f;
        private const float NaturalDecayPerDay = 0.05f;
        
        public override void PostMake()
        {
            base.PostMake();
            
            // Устанавливаем начальную тяжесть
            Severity = InitialSeverity;
        }
        
        public override float SeverityChangePerDay()
        {
            // Естественное разложение тлена со временем
            return -NaturalDecayPerDay;
        }
        
        // Добавляем чуть проверок, чтобы этот хедифф был только на трупах
        public override bool TryMergeWith(Hediff other)
        {
            // Не объединяем тлен с другими хедиффами
            return false;
        }
    }
    
    // Класс для слабости после блайта (для воскрешенных)
    public class Hediff_PostBlightWeakness : HediffWithComps
    {
        // Константы
        private const float InitialSeverity = 0.6f;
        private const float RecoveryPerDay = 0.1f;
        
        public override void PostMake()
        {
            base.PostMake();
            
            // Устанавливаем начальную тяжесть
            Severity = InitialSeverity;
        }
        
        public override float SeverityChangePerDay()
        {
            // Восстановление здоровья со временем
            return -RecoveryPerDay;
        }
    }
}
