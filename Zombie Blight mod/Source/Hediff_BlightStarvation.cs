using System;
using RimWorld;
using Verse;
using Verse.AI; // Might be needed for JobGiver/JobDriver logic if applicable, but not directly in this Hediff.
using UnityEngine; // Needed for Mathf

namespace ZombieBlight
{
    // Hediff, представляющий голодание зомби
    public class Hediff_BlightStarvation : HediffWithComps
    {
        // Константы для расчета голодания
        private const float StarvationPerDayBase = 0.1f;  // Базовая скорость голодания (изменения тяжести в день)
        private const float StarvationMaxSeverity = 1.0f; // Максимальная тяжесть (полное голодание)
        private const float CriticalStarvationThreshold = 0.85f; // Порог тяжести для начала критических эффектов

        // Стадии голодания (описание для удобства)
        // 0.01-0.24: Незначительное - Небольшой дебафф к скорости
        // 0.25-0.49: Легкое - Средний дебафф к скорости и боевым навыкам
        // 0.50-0.74: Среднее - Сильный дебафф к скорости и боевым навыкам
        // 0.75-0.84: Тяжелое - Очень сильный дебафф и шанс оцепенения
        // 0.85-1.00: Критическое - Максимальный дебафф + эффекты


        // Вызывается при создании хедиффа (после загрузки или создания нового)
        public override void PostMake()
        {
            base.PostMake();
            this.Severity = 0.01f; // Устанавливаем начальную тяжесть голодания
        }

        // *** УДАЛЕНА ОШИБКА CS0115: Удалено переопределение SeverityChangePerDay() ***
        // *** ЛОГИКА ПРИРОСТА ТЯЖЕСТИ ПЕРЕМЕЩЕНА В Tick() ***
        // public override float SeverityChangePerDay() { ... }


        // Уменьшение голодания при "кормлении" (или другом внешнем воздействии)
        public void ReduceStarvation(float amount)
        {
            // Уменьшаем тяжесть, но не ниже 0
            Severity = Math.Max(0f, Severity - amount);

            // Если тяжесть достигла или опустилась ниже 0, удаляем хедифф
            // Базовая система Hediff обычно делает это автоматически, но можно удалить явно.
            if (Severity <= 0f)
            {
                pawn.health.RemoveHediff(this);
            }
        }

        // Проверка на достижение критической стадии голодания
        public bool IsCriticallyStarved()
        {
            // Возвращаем true, если текущая тяжесть достигла или превысила порог критического голодания
            return Severity >= CriticalStarvationThreshold;
        }

        // Метод вызывается каждый тик игры для этого хедиффа
        public override void Tick()
        {
            base.Tick(); // Всегда вызываем базовый Tick()

            // *** ЛОГИКА ПРИРОСТА ТЯЖЕСТИ ПЕРЕМЕЩЕНА СЮДА ИЗ SeverityChangePerDay ***

            // Рассчитываем базовый прирост голодания за один тик
            float baseChangePerTick = StarvationPerDayBase / GenDate.TicksPerDay;

            // Вычисляем итоговый прирост за тик, применяя температурные модификаторы
            float finalChangePerTick = baseChangePerTick; // Начинаем с базового прироста

            // Применяем модификаторы скорости голодания в зависимости от температуры, если пешка заспавнена
            if (pawn.Spawned) // Проверяем, что пешка на карте для получения температуры
            {
                // Получаем температуру в клетке пешки
                float ambientTemperature = pawn.Position.GetTemperature(pawn.Map);

                // При высоких температурах голодание ускоряется (разложение быстрее)
                if (ambientTemperature > 30f)
                {
                    // Линейное ускорение: при 30f множитель 1x, при 60f множитель 2x
                    finalChangePerTick *= 1f + ((ambientTemperature - 30f) / 30f);
                }
                // При низких температурах замедляется (консервация холодом)
                else if (ambientTemperature < 0f)
                {
                    // Линейное замедление: при 0f множитель 1x, при -30f множитель 0.2x (минимум 0.2f)
                    finalChangePerTick *= Math.Max(0.2f, 1f - (Math.Abs(ambientTemperature) / 30f)); // Math.Abs для получения положительного значения разницы
                }
                // В остальных случаях (0f до 30f) множитель остается 1x (т.е. finalChangePerTick = baseChangePerTick)
            }

            // Применяем вычисленный итоговый прирост тяжести за этот тик
            // Увеличиваем Severity. Mathf.Clamp01 гарантирует, что значение останется в диапазоне [0, 1].
            Severity = Mathf.Clamp01(Severity + finalChangePerTick);

            // Проверяем на достижение критической стадии голодания после обновления тяжести
            if (IsCriticallyStarved() && pawn.Spawned && !pawn.Dead) // Проверяем, что пешка заспавнена и жива перед применением эффектов
            {
                // Если достигнута критическая стадия и прошло достаточно тиков, пытаемся применить эффект
                // Проверяем раз в 1000 тиков (16.6 секунд)
                if (Find.TickManager.TicksGame % 1000 == 0)
                {
                    TryStarvationEffect(); // Вызываем метод для случайного эффекта голодания
                }
            }
        }


        // Эффекты критического голодания (случайный выбор и применение)
        private void TryStarvationEffect()
        {
            // Рассчитываем шанс эффекта. Шанс растет от 0% (при CriticalStarvationThreshold) до 60% (при Severity = 1.0f).
            // effectChance = (текущая_тяжесть - порог) * 0.6f
            // Например: при 0.85f шанс = (0.85 - 0.85) * 0.6 = 0
            // при 1.00f шанс = (1.00 - 0.85) * 0.6 = 0.15 * 0.6 = 0.09 (9%)
            // Это немного отличается от описания в комментарии "шанс эффектов растет с тяжестью голодания",
            // но соответствует коду (0.6f - это множитель, а не максимальный шанс).
            // Если вы хотите, чтобы при Severity = 1.0f шанс был 60%, множитель должен быть 0.6f / (1.0f - 0.85f) = 0.6 / 0.15 = 4f.
            // Давайте оставим как в исходном коде (множитель 0.6f).
            float effectChance = (Severity - CriticalStarvationThreshold) * 0.6f;

            // Проверяем, сработал ли эффект на основе шанса
            if (!Rand.Chance(effectChance)) return; // Если не выпал шанс, выходим

            // Случайный выбор одного из трех эффектов с разными весами (50%, 30%, 20%)
            float rand = Rand.Value;

            if (rand < 0.5f) // 50% шанс
            {
                // Эффект 1: Временная гибернация или оцепенение
                ApplyStunnedEffect();
            }
            else if (rand < 0.8f) // 30% шанс (между 0.5 и 0.8)
            {
                // Эффект 2: Агрессивный поиск пищи (бешенство голода)
                ActivateHungerFrenzy();
            }
            else // 20% шанс (между 0.8 и 1.0)
            {
                // Эффект 3: Повреждение самого зомби (саморазложение)
                ApplySelfDamage();
            }
        }

        // Применение эффекта оглушения/гибернации
        private void ApplyStunnedEffect()
        {
            // Прерываем текущее задание пешки
            if (pawn.jobs != null)
            {
                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }

            // Добавляем временный хедифф оглушения (предполагается HediffDef "Stunned" существует)
            HediffDef stunHediff = DefDatabase<HediffDef>.GetNamed("Stunned", false); // false - не генерировать ошибку, если Def не найден
            if (stunHediff != null)
            {
                // Длительность оглушения (в тиках)
                float stunDuration = Rand.Range(2500f, 5000f); // ~41-83 секунды

                // Создаем и добавляем хедифф оглушения
                Hediff hediff = HediffMaker.MakeHediff(stunHediff, pawn);
                // Severity для хедиффа оглушения часто используется для длительности или силы
                // Если "Stunned" - это HediffWithComps с HediffCompProperties_Disappears, Severity=1f может быть некорректно.
                // Обычно для временных хедиффов с Disappears Severity не устанавливается напрямую, а компонент Disappears задает длительность.
                // Если Stunned Def имеет <hediffClass>HediffWithComps</hediffClass> и компонент Disappears,
                // то правильнее сделать так:
                // HediffWithComps hediffWithComps = HediffMaker.MakeHediff(stunHediff, pawn) as HediffWithComps;
                // if (hediffWithComps != null && hediffWithComps.TryGetComp<HediffComp_Disappears>() != null)
                // {
                //     hediffWithComps.TryGetComp<HediffComp_Disappears>().ticksToDisappear = (int)stunDuration;
                //     pawn.health.AddHediff(hediffWithComps);
                // }
                // Иначе, если Stunned - это простой Hediff или HediffWithComps без Disappears, Severity=1f может быть частью его логики.
                // Оставим как в исходном коде с установкой Severity=1f, но имейте в виду, что это может потребовать корректировки в XML HediffDef "Stunned".
                hediff.Severity = 1f; // Устанавливаем тяжесть (возможно, для активации эффектов/стадий)
                pawn.health.AddHediff(hediff); // Добавляем хедифф пешке
            }

            // Логирование в DevMode
            if (Prefs.DevMode)
            {
                Log.Message($"[Zombie Blight] Zombie {pawn.LabelShort} is stunned from starvation");
            }
        }

        // Применение эффекта агрессивного поиска пищи (бешенство голода)
        private void ActivateHungerFrenzy()
        {
            // Добавляем временный хедифф "BlightFrenzy" (предполагается HediffDef "BlightFrenzy" существует)
            HediffDef frenzyHediff = DefDatabase<HediffDef>.GetNamed("BlightFrenzy", false); // false - не генерировать ошибку, если Def не найден
            if (frenzyHediff != null)
            {
                // Длительность эффекта (в тиках)
                float frenzyDuration = Rand.Range(7500f, 15000f); // ~2-4 минуты

                // Создаем и добавляем хедифф бешенства
                Hediff hediff = HediffMaker.MakeHediff(frenzyHediff, pawn);
                // Как и со "Stunned", установка Severity=1f может требовать соответствия в XML HediffDef "BlightFrenzy"
                hediff.Severity = 1f; // Устанавливаем тяжесть (возможно, для активации эффектов/стадий)
                pawn.health.AddHediff(hediff); // Добавляем хедифф пешке
            }

            // Логирование в DevMode
            if (Prefs.DevMode)
            {
                Log.Message($"[Zombie Blight] Zombie {pawn.LabelShort} entered hunger frenzy");
            }
        }

        // Применение самоповреждения от голодания
        private void ApplySelfDamage()
        {
            // Случайное количество урона
            float damageAmount = Rand.Range(2f, 5f);

            // Наносим урон случайной части тела, которая не отсутствует
            // *** ИСПРАВЛЕНА ОШИБКА CS7036: Добавлен отсутствующий аргумент DamageDef ***
            // HediffSet.GetRandomNotMissingPart требует указать тип урона, для которого ищется часть тела (например, для определения, какие части могут быть повреждены этим типом урона).
            // Поскольку вы наносите урон типом Rotting (гниение), используем DamageDefOf.Rotting.
            BodyPartRecord randomPart = pawn.health.hediffSet.GetRandomNotMissingPart(DamageDefOf.Rotting); // Добавлен аргумент DamageDefOf.Rotting

            // Если найдена подходящая часть тела
            if (randomPart != null)
            {
                // Создаем информацию об уроне. DamageDefOf.Rotting - тип урона "гниение".
                DamageInfo damageInfo = new DamageInfo(DamageDefOf.Rotting, damageAmount, 0f, -1f, null, randomPart); // -1f - отсутствие Instigator (источника урона)

                // Применяем урон пешке
                pawn.TakeDamage(damageInfo);

                // Логирование в DevMode
                if (Prefs.DevMode)
                {
                    Log.Message($"[Zombie Blight] Zombie {pawn.LabelShort} is taking {damageAmount} rot damage from starvation");
                }
            }
            // Если не найдена часть тела (например, пешка - просто кусок мяса без частей), урон не наносится.
        }
    }
}