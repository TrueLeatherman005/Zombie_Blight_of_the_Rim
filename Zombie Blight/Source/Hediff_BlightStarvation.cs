using System;
using RimWorld;
using Verse;

namespace ZombieBlight
{
    public class Hediff_BlightStarvation : HediffWithComps
    {
        // Константы для расчета голодания
        private const float StarvationPerDayBase = 0.1f;  // Базовая скорость голодания
        private const float StarvationMaxSeverity = 1.0f; // Максимальная тяжесть
        private const float CriticalStarvationThreshold = 0.85f; // Порог критического голодания
        
        // Стадии голодания:
        // 0.01-0.24: Незначительное - Небольшой дебафф к скорости
        // 0.25-0.49: Легкое - Средний дебафф к скорости и боевым навыкам
        // 0.50-0.74: Среднее - Сильный дебафф к скорости и боевым навыкам
        // 0.75-0.84: Тяжелое - Очень сильный дебафф и шанс оцепенения
        // 0.85-1.00: Критическое - Максимальный дебафф + эффекты
        
        public override void PostMake()
        {
            base.PostMake();
            this.Severity = 0.01f; // Начальная тяжесть голодания
        }
        
        // Прирост тяжести на каждый день без "кормления"
        public override float SeverityChangePerDay()
        {
            if (pawn.Dead) return 0f;
            
            // Базовый прирост голодания
            float change = StarvationPerDayBase;
            
            // Изменение скорости голодания в зависимости от температуры
            if (pawn.Spawned)
            {
                float ambientTemperature = pawn.Position.GetTemperature(pawn.Map);
                
                // При высоких температурах голодание ускоряется (разложение быстрее)
                if (ambientTemperature > 30f)
                {
                    change *= 1f + ((ambientTemperature - 30f) / 30f);
                }
                // При низких температурах замедляется (консервация холодом)
                else if (ambientTemperature < 0f)
                {
                    change *= Math.Max(0.2f, 1f - (Math.Abs(ambientTemperature) / 30f));
                }
            }
            
            return change;
        }
        
        // Уменьшение голодания при "кормлении"
        public void ReduceStarvation(float amount)
        {
            Severity = Math.Max(0f, Severity - amount);
            
            // Проверка достижения нуля - можно удалить хедифф
            if (Severity <= 0f)
            {
                pawn.health.RemoveHediff(this);
            }
        }
        
        // Проверка на достижение критической стадии голодания
        public bool IsCriticallyStarved()
        {
            return Severity >= CriticalStarvationThreshold;
        }
        
        // Генерация рандомных действий при критическом голодании
        public override void Tick()
        {
            base.Tick();
            
            // Проверяем критическое голодание
            if (IsCriticallyStarved() && pawn.Spawned && !pawn.Dead)
            {
                // Раз в 1000 тиков (16.6 секунд)
                if (Find.TickManager.TicksGame % 1000 == 0)
                {
                    TryStarvationEffect();
                }
            }
        }
        
        // Эффекты критического голодания
        private void TryStarvationEffect()
        {
            // Шанс эффектов растет с тяжестью голодания
            float effectChance = (Severity - CriticalStarvationThreshold) * 0.6f;
            
            if (!Rand.Chance(effectChance)) return;
            
            // Случайный выбор эффекта с разными весами
            float rand = Rand.Value;
            
            if (rand < 0.5f) // 50% шанс
            {
                // Эффект 1: Временная гибернация или оцепенение
                ApplyStunnedEffect();
            }
            else if (rand < 0.8f) // 30% шанс
            {
                // Эффект 2: Агрессивный поиск пищи
                ActivateHungerFrenzy();
            }
            else // 20% шанс
            {
                // Эффект 3: Повреждение самого зомби (разложение)
                ApplySelfDamage();
            }
        }
        
        private void ApplyStunnedEffect()
        {
            // Эффект оглушения/гибернации
            if (pawn.jobs != null)
            {
                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }
            
            // Добавляем временный хедифф оглушения если есть
            HediffDef stunHediff = DefDatabase<HediffDef>.GetNamed("Stunned", false);
            if (stunHediff != null)
            {
                float stunDuration = Rand.Range(2500f, 5000f); // 41-83 секунды
                Hediff hediff = HediffMaker.MakeHediff(stunHediff, pawn);
                hediff.Severity = 1f;
                pawn.health.AddHediff(hediff);
            }
            
            if (Prefs.DevMode)
            {
                Log.Message($"[Zombie Blight] Zombie {pawn.LabelShort} is stunned from starvation");
            }
        }
        
        private void ActivateHungerFrenzy()
        {
            // Добавляем временный бонус к скорости движения и поиску целей
            HediffDef frenzyHediff = DefDatabase<HediffDef>.GetNamed("BlightFrenzy", false);
            if (frenzyHediff != null)
            {
                float frenzyDuration = Rand.Range(7500f, 15000f); // 2-4 минуты
                Hediff hediff = HediffMaker.MakeHediff(frenzyHediff, pawn);
                hediff.Severity = 1f;
                pawn.health.AddHediff(hediff);
            }
            
            if (Prefs.DevMode)
            {
                Log.Message($"[Zombie Blight] Zombie {pawn.LabelShort} entered hunger frenzy");
            }
        }
        
        private void ApplySelfDamage()
        {
            float damageAmount = Rand.Range(2f, 5f);
            
            // Наносим случайное повреждение одной из частей тела
            BodyPartRecord randomPart = pawn.health.hediffSet.GetRandomNotMissingPart();
            if (randomPart != null)
            {
                DamageInfo damageInfo = new DamageInfo(DamageDefOf.Rotting, damageAmount, 0f, -1f, null, randomPart);
                pawn.TakeDamage(damageInfo);
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[Zombie Blight] Zombie {pawn.LabelShort} is taking {damageAmount} rot damage from starvation");
                }
            }
        }
    }
}
