using System; // Необходимо для Math.Max
using System.Collections.Generic; // Может понадобиться
using System.Linq; // Может понадобиться для LINQ
using RimWorld;
using Verse;
using UnityEngine; // Необходимо для Mathf

namespace ZombieBlight
{
    // Базовый класс для хедиффов сопротивления и иммунитета
    public class Hediff_BlightResistanceBase : HediffWithComps
    {
        // Применяется ли эффект до размножения инфекции
        // Marked virtual so derived classes can override
        protected virtual bool AppliesToExistingInfection => true;

        // Множитель эффективности (1.0 = 100% защита)
        // Marked virtual so derived classes can override
        protected virtual float EffectivenessMultiplier => 1.0f;

        // Вызывается при добавлении хедиффа
        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);

            // Применяем эффект к существующей инфекции, если это предусмотрено производным классом
            if (AppliesToExistingInfection)
            {
                TryReduceExistingInfection();
            }
        }

        // Уменьшение тяжести существующей инфекции (Hediff_BlightInfection)
        protected void TryReduceExistingInfection()
        {
            if (pawn?.health?.hediffSet == null) return;

            // Находим существующую инфекцию (предполагается BlightDefOf.Hediff_BlightInfection существует)
            Hediff blightInfection = pawn.health.hediffSet.GetFirstHediffOfDef(BlightDefOf.Hediff_BlightInfection);

            // Если инфекция найдена
            if (blightInfection != null)
            {
                // Рассчитываем снижение тяжести на основе текущей тяжести этого хедиффа (Severity),
                // множителя эффективности из производного класса и дополнительного коэффициента 0.5f.
                float reductionAmount = Severity * EffectivenessMultiplier * 0.5f; // Коэффициент 0.5f можно настроить для баланса

                // Уменьшаем тяжесть инфекции, но не ниже 0.01f (как было в исходном коде)
                // Math.Max(0.01f, ...) гарантирует, что тяжесть не опустится ниже 0.01f.
                blightInfection.Severity = Math.Max(0.01f, blightInfection.Severity - reductionAmount);

                // Если тяжесть инфекции стала достаточно низкой (ниже или равно 0.05f)
                if (blightInfection.Severity <= 0.05f)
                {
                    // Удаляем хедифф инфекции
                    pawn.health.RemoveHediff(blightInfection);

                    // Если пешка принадлежит игроку, выводим сообщение о выздоровлении
                    // Предполагается, что "BlightInfectionCured" - это ключ для перевода в LanguageData XML.
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

        // Вызывается при создании хедиффа (после загрузки или создания нового)
        public override void PostMake()
        {
            base.PostMake();
            // Уведомляем HealthUtility о изменении хедиффа (может потребоваться для обновления кэшей статов и т.п.)
            // Базовые методы могут вызывать это, но явный вызов здесь не повредит для надежности.
            pawn.health.Notify_HediffChanged(this);
        }

        // Метод Tick() не переопределяется в базовом классе BlightResistanceBase.
        // Логика изменения тяжести со временем будет реализована в методах Tick() производных классов.
        // public override void Tick() { base.Tick(); } // Этот метод будет переопределен в производных классах

        
        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit)
        {
            // Всегда вызываем базовый метод первым в переопределениях!
            base.Notify_PawnDied(dinfo, culprit);

            // Логика, специфичная для смерти, здесь (например, добавление других хедиффов, если нужно).
            // В исходном коде в Hediff_BlightInfection здесь вызывался AddDeathRefusalIfNeeded.
            // Если хедиффы сопротивления/иммунитета должны влиять на DeathRefusal при смерти, эта логика должна быть здесь или в другом месте.
            // В исходном коде этого не было в BlightResistanceBase, так что здесь ничего не добавляем.
        }

        
    }

    // Реализация сопротивления Блайту
    public class Hediff_BlightResistance : Hediff_BlightResistanceBase
    {
        // Переопределения из BlightResistanceBase для настройки поведения
        protected override bool AppliesToExistingInfection => true; // Сопротивление влияет на уже существующую инфекцию
        protected override float EffectivenessMultiplier => 0.7f; // Снижает инфекцию на 70% эффективности сопротивления

        
      
    }

    // Реализация полного иммунитета к Блайту
    public class Hediff_BlightImmunity : Hediff_BlightResistanceBase
    {
        // Переопределения из BlightResistanceBase для настройки поведения
        protected override bool AppliesToExistingInfection => true; // Иммунитет влияет на уже существующую инфекцию
        protected override float EffectivenessMultiplier => 1.0f; // Снижает инфекцию на 100% эффективности (обычно приводит к немедленному излечению при добавлении)

        // Вызывается при добавлении хедиффа иммунитета
        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo); // Вызываем базовый метод BlightResistanceBase.PostAdd (который вызывает TryReduceExistingInfection)

            // Полный иммунитет удаляет хедифф сопротивления, если он есть
            // Предполагается, что BlightDefOf.Hediff_BlightResistance существует.
            if (pawn?.health?.hediffSet != null)
            {
                Hediff resistance = pawn.health.hediffSet.GetFirstHediffOfDef(BlightDefOf.Hediff_BlightResistance);
                if (resistance != null)
                {
                    pawn.health.RemoveHediff(resistance); // Удаляем сопротивление при получении полного иммунитета
                }
            }
        }

       
        
    }

    // Класс для трупного тлена (после смерти зомби)
    public class Hediff_BlightCorpseTaint : HediffWithComps // Не наследует от BlightResistanceBase, это отдельный хедифф
    {
        // Константы для этого хедиффа
        private const float InitialSeverity = 1.0f; // Начальная тяжесть при добавлении (максимальная)
        private const float NaturalDecayPerDay = 0.05f; // Скорость естественного затухания тлена в день

        // Вызывается при создании хедиффа (после загрузки или создания нового)
        public override void PostMake()
        {
            base.PostMake();

            // Устанавливаем начальную тяжесть хедиффа при создании
            Severity = InitialSeverity; // Устанавливаем тяжесть на 1.0f
        }

        

        // Override Tick() для реализации логики затухания тлена со временем
        public override void Tick()
        {
            base.Tick(); // Вызываем базовый Tick()

            // Применяем естественное затухание тлена
            // Переводим скорость затухания из "в день" в "за тик"
            float decayPerTick = NaturalDecayPerDay / GenDate.TicksPerDay;

            // Уменьшаем тяжесть хедиффа. Mathf.Clamp01 гарантирует, что значение останется в диапазоне [0, 1].
            Severity = Mathf.Clamp01(Severity - decayPerTick);

            // Когда тяжесть достигает или опускается ниже 0, хедифф автоматически удаляется базовой системой Hediff.
        }

        // Переопределение TryMergeWith для контроля объединения хедиффов
        // Эта реализация полностью запрещает объединение этого хедиффа с любым другим.
        public override bool TryMergeWith(Hediff other)
        {
            // Не позволяем тлену объединяться с другими хедиффами
            return false; // Всегда возвращаем false, чтобы предотвратить объединение
        }

       
    }

    // Класс для слабости после блайта (для воскрешенных или излеченных в терминальной стадии)
    public class Hediff_PostBlightWeakness : HediffWithComps // Не наследует от BlightResistanceBase, это отдельный хедифф
    {
        // Константы для этого хедиффа
        private const float InitialSeverity = 0.6f; // Начальная тяжесть при добавлении (степень слабости)
        private const float RecoveryPerDay = 0.1f; // Скорость восстановления в день (уменьшения слабости)

        // Вызывается при создании хедиффа (после загрузки или создания нового)
        public override void PostMake()
        {
            base.PostMake();

            // Устанавливаем начальную тяжесть (степень слабости)
            Severity = InitialSeverity; // Устанавливаем тяжесть на 0.6f
        }

       

        // Override Tick() для реализации логики восстановления (уменьшения слабости) со временем
        public override void Tick()
        {
            base.Tick(); // Вызываем базовый Tick()

            // Применяем восстановление (уменьшение тяжести слабости)
            // Переводим скорость восстановления из "в день" в "за тик"
            float recoveryPerTick = RecoveryPerDay / GenDate.TicksPerDay;

            // Уменьшаем тяжесть хедиффа (снижение тяжести означает уменьшение слабости).
            // Mathf.Clamp01 гарантирует, что значение останется в диапазоне [0, 1].
            Severity = Mathf.Clamp01(Severity - recoveryPerTick);

            // Когда тяжесть достигает или опускается ниже 0, хедифф автоматически удаляется базовой системой Hediff.
        }

        // Note: Notify_PawnDied не переопределяется в Hediff_PostBlightWeakness в исходном коде.
        // Это логично, так как этот хедифф, вероятно, добавляется ПЕШКЕ, КОТОРАЯ ВЫЖИЛА или была воскрешена.
    }
}
