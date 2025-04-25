using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ZombieBlight
{
    // Класс для обработки блюд с добавкой
    public class SpecialThingFilterWorker_BlightTreated : SpecialThingFilterWorker
    {
        public override bool Matches(Thing t)
        {
            // Проверяем, что это еда
            if (!t.def.IsNutritionGivingIngestible) return false;
            
            // Проверяем наличие компа с добавкой
            CompBlightTreatedFood comp = t.TryGetComp<CompBlightTreatedFood>();
            return comp != null && comp.IsTreated;
        }
        
        public override bool CanEverMatch(ThingDef def)
        {
            // Проверяем, что это еда и имеет возможность быть с добавкой
            return def.IsNutritionGivingIngestible && def.HasComp(typeof(CompBlightTreatedFood));
        }
    }
    
    // Компонент для блюд с добавкой
    public class CompBlightTreatedFood : ThingComp
    {
        // Флаг наличия добавки
        public bool IsTreated = false;
        
        // Количество добавки (влияет на эффективность)
        public float AdditiveAmount = 0f;
        
        // Сохранение данных
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref IsTreated, "isTreated", false);
            Scribe_Values.Look(ref AdditiveAmount, "additiveAmount", 0f);
        }
        
        // Информация для интерфейса
        public override string CompInspectStringExtra()
        {
            if (!IsTreated) return null;
            
            return "BlightTreated".Translate() + " (" + 
                   "ImmunityEffectiveness".Translate((AdditiveAmount * 100f).ToString("F0")) + "%)";
        }
        
        // Изменение подсказки с информацией о ингредиентах
        public override void PostIngested(Pawn ingester)
        {
            base.PostIngested(ingester);
            
            if (IsTreated && ingester != null && AdditiveAmount > 0f)
            {
                // Рассчитываем длительность эффекта в зависимости от количества
                int durationTicks = (int)(AdditiveAmount * 60000f); // ~1 день при значении 1.0
                
                // Применяем эффект сопротивления к пешке
                BlightUtility.ApplyBlightResistanceEffect(ingester, AdditiveAmount, durationTicks);
            }
        }
    }
    
    // Класс обработчика рецепта добавления добавки к еде
    public class Recipe_AddBlightAdditiveToMeal : RecipeWorker
    {
        public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
        {
            // Проверяем, что это еда
            if (!thing.def.IsNutritionGivingIngestible) return false;
            
            // Проверяем наличие компонента для добавки
            CompBlightTreatedFood comp = thing.TryGetComp<CompBlightTreatedFood>();
            if (comp == null) return false;
            
            // Проверяем, что добавка ещё не была добавлена
            return !comp.IsTreated;
        }
        
        public override void Notify_IterationCompleted(Pawn billDoer, List<Thing> ingredients)
        {
            base.Notify_IterationCompleted(billDoer, ingredients);
            
            // Получаем блюдо из ингредиентов
            Thing meal = ingredients.FirstOrDefault(t => t.def.IsNutritionGivingIngestible && !(t.def == BlightDefOf.Item_BlightResistantAdditive));
            if (meal == null) return;
            
            // Получаем добавку из ингредиентов
            Thing additive = ingredients.FirstOrDefault(t => t.def == BlightDefOf.Item_BlightResistantAdditive);
            if (additive == null) return;
            
            // Применяем добавку к блюду
            CompBlightTreatedFood comp = meal.TryGetComp<CompBlightTreatedFood>();
            if (comp != null)
            {
                comp.IsTreated = true;
                comp.AdditiveAmount = 0.5f; // Базовое значение эффективности
                
                // Модификатор от навыка повара
                if (billDoer != null)
                {
                    int cookingSkill = billDoer.skills.GetSkill(SkillDefOf.Cooking).Level;
                    comp.AdditiveAmount += cookingSkill / 20f; // +0.05 за каждый уровень навыка (максимум +0.5 при навыке 20)
                }
                
                // Изменяем метку блюда, чтобы показать наличие добавки
                meal.SetLabelNoRestoreOriginal(meal.Label + " (" + "BlightResistant".Translate() + ")");
                
                // Выводим сообщение
                Messages.Message(
                    "BlightAdditiveAddedToMeal".Translate(meal.LabelShort), 
                    meal, 
                    MessageTypeDefOf.PositiveEvent);
            }
        }
    }
}
