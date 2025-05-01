using System; // Добавлен из ваших BlightDefOf и BlightUtility
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using UnityEngine;

namespace ZombieBlight
{
    // Свойства компонента
    public class CompProperties_BlightTreatedFood : CompProperties
    {
        public CompProperties_BlightTreatedFood()
        {
            compClass = typeof(CompBlightTreatedFood);
        }
    }

    // Компонент для блюд с добавкой
    public class CompBlightTreatedFood : ThingComp
    {
        // Поля
        public bool IsTreated = false;
        public float AdditiveAmount = 0f;

        // Сохранение данных
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref IsTreated, "isTreated", false);
            Scribe_Values.Look(ref AdditiveAmount, "additiveAmount", 0f);
        }

        // Метод установки значений (может пригодиться, если не только рецептом добавляется)
        public void SetTreated(float amount)
        {
            IsTreated = true;
            AdditiveAmount = amount;
        }

        // Информация для интерфейса
        public override string CompInspectStringExtra()
        {
            if (!IsTreated) return null;

            // Предполагаем наличие перевода "BlightTreated" и "ImmunityEffectiveness"
            return "BlightTreated".Translate() + " (" +
                   "ImmunityEffectiveness".Translate((AdditiveAmount * 100f).ToString("F0")) + "%)";
        }

        // Логика при поедании
        public override void PostIngested(Pawn ingester)
        {
            base.PostIngested(ingester);

            if (IsTreated && ingester != null && AdditiveAmount > 0f)
            {
                // Рассчитываем длительность эффекта
                int durationTicks = (int)(AdditiveAmount * 60000f); // ~1 день при значении 1.0

                // Применяем эффект сопротивления к пешке, используя BlightUtility
                // Предполагаем, что BlightUtility и метод ApplyBlightResistanceEffect существуют
                BlightUtility.ApplyBlightResistanceEffect(ingester, AdditiveAmount, durationTicks);
            }
        }
    }

    // Класс для обработки блюд с добавкой (специальный фильтр)
    public class SpecialThingFilterWorker_BlightTreated : SpecialThingFilterWorker
    {
        public override bool Matches(Thing t)
        {
            // Проверяем, что это еда
            if (!t.def.IsNutritionGivingIngestible) return false;

            // Проверяем наличие компа с добавкой
            CompBlightTreatedFood comp = t.TryGetComp<CompBlightTreatedFood>();
            // Используем публичное поле IsTreated объединенного класса
            return comp != null && comp.IsTreated;
        }

        public override bool CanEverMatch(ThingDef def)
        {
            // Проверяем, что это еда и может иметь компонент добавки
            return def.IsNutritionGivingIngestible && def.HasComp(typeof(CompBlightTreatedFood));
        }
    }

    // Класс обработчика рецепта добавления добавки к еде
    public class Recipe_AddBlightAdditiveToMeal : RecipeWorker
    {
        public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
        {
            // Проверяем, что это еда
            if (!thing.def.IsNutritionGivingIngestible) return false;

            // Проверяем наличие нашего компонента BlightTreatedFood
            CompBlightTreatedFood blightComp = thing.TryGetComp<CompBlightTreatedFood>();
            // Рецепт доступен, только если компонент есть и добавка еще НЕ была добавлена
            if (blightComp == null || blightComp.IsTreated) return false;

            // Опциональная проверка: Убеждаемся, что у предмета есть CompIngredients для добавления в список
            CompIngredients gameIngredientsComp = thing.TryGetComp<CompIngredients>();
            if (gameIngredientsComp == null) return false;


            return true; // Если все проверки пройдены, рецепт доступен
        }

        public override void Notify_IterationCompleted(Pawn billDoer, List<Thing> ingredients)
        {
            base.Notify_IterationCompleted(billDoer, ingredients);

            // Получаем блюдо из ингредиентов рецепта (оно здесь "ингредиент" для этого рецепта)
            // Исключаем саму добавку, чтобы не взять ее как блюдо
            // Предполагаем, что BlightDefOf.Item_BlightResistantAdditive существует
            Thing meal = ingredients.FirstOrDefault(t => t.def.IsNutritionGivingIngestible && !(t.def == BlightDefOf.Item_BlightResistantAdditive));
            if (meal == null)
            {
                Log.Error("Recipe_AddBlightAdditiveToMeal: Could not find meal in ingredients list."); // Логируем ошибку, если не нашли блюдо
                return;
            }

            // Получаем добавку из ингредиентов рецепта
            Thing additive = ingredients.FirstOrDefault(t => t.def == BlightDefOf.Item_BlightResistantAdditive);
            if (additive == null)
            {
                Log.Error("Recipe_AddBlightAdditiveToMeal: Could not find additive in ingredients list."); // Логируем ошибку, если не нашли добавку
                return;
            }

            // Получаем наш компонент BlightTreatedFood с блюда
            CompBlightTreatedFood blightComp = meal.TryGetComp<CompBlightTreatedFood>();

            // Получаем стандартный компонент CompIngredients с блюда
            CompIngredients gameIngredientsComp = meal.TryGetComp<CompIngredients>();


            // Выполняем действия, только если оба компонента найдены (должны быть найдены по AvailableOnNow)
            if (blightComp != null && gameIngredientsComp != null)
            {
                // Устанавливаем значения в нашем компоненте BlightTreatedFood
                blightComp.IsTreated = true;
                blightComp.AdditiveAmount = 0.5f; // Базовое значение эффективности

                // Модификатор от навыка повара
                if (billDoer != null && billDoer.skills != null)
                {
                    // Используем SkillDefOf из RimWorld
                    int cookingSkill = billDoer.skills.GetSkill(SkillDefOf.Cooking).Level;
                    blightComp.AdditiveAmount += cookingSkill / 20f; // +0.05 за каждый уровень навыка (максимум +0.5 при навыке 20)
                }

                // *** ДОБАВЛЯЕМ ADDITIVE В СПИСОК ИНГРЕДИЕНТОВ CompIngredients ***
                // Убеждаемся, что Def добавки существует
                if (BlightDefOf.Item_BlightResistantAdditive != null)
                {
                    // Добавляем ThingDef добавки в публичный список ингредиентов стандартного компонента игры
                    // Используем стандартный метод RegisterIngredient для правильной обработки (проверка на наличие, сброс кеша)
                    gameIngredientsComp.RegisterIngredient(BlightDefOf.Item_BlightResistantAdditive);
                }
                else
                {
                    Log.Error("Recipe_AddBlightAdditiveToMeal: BlightDefOf.Item_BlightResistantAdditive is null!"); // Логируем ошибку, если Def не загрузился
                }
                // *** КОНЕЦ ДОБАВЛЕНИЯ В CompIngredients ***


                // Удаленная ранее строка SetLabelNoRestoreOriginal - больше не нужна.

                // Выводим сообщение
                // Предполагаем, что BlightAdditiveAddedToMeal существует как ключ перевода
                Messages.Message(
                    "BlightAdditiveAddedToMeal".Translate(meal.LabelShort),
                    meal,
                    MessageTypeDefOf.PositiveEvent);
            }
            else
            {
                // Логируем ошибку, если компоненты пропали между AvailableOnNow и Notify_IterationCompleted (маловероятно)
                Log.Error("Recipe_AddBlightAdditiveToMeal: Missing CompBlightTreatedFood or CompIngredients on meal after AvailableOnNow check.");
            }
        }
    }

    // Предполагаем, что BlightDefOf где-то определен и содержит:
    // public static HediffDef Hediff_BlightResistance;
    // public static HediffDef Hediff_BlightInfection; // Если еще используется
    // public static ThingDef Item_BlightResistantAdditive;
    // А также класс BlightUtility с методом ApplyBlightResistanceEffect
    // SkillDefOf.Cooking и MessageTypeDefOf.PositiveEvent - стандартные DefOf RimWorld

}