using UnityEngine;
using Verse;
using RimWorld;
using System.Collections.Generic;
using System.Linq;

namespace ZombieBlight
{
    public class ZombieThreatGraph
    {
        private static readonly Color GraphLineColor = new Color(0.2f, 0.4f, 0.8f);
        private static readonly Color GridLineColor = new Color(1f, 1f, 1f, 0.1f);
        private static readonly Color CurrentTimeMarkerColor = Color.yellow;
        private static readonly Vector2 DefaultSize = new Vector2(360f, 100f);

        private readonly ZombieTrendCalculator trendCalculator;
        private bool isExpanded = false;
        private Rect lastDrawnRect;

        public ZombieThreatGraph(ZombieTrendCalculator calculator)
        {
            trendCalculator = calculator;
        }

        public void DrawAt(float x, float y)
        {
            Vector2 size = isExpanded ? DefaultSize * 1.5f : DefaultSize;
            Rect graphRect = new Rect(x, y, size.x, size.y);
            lastDrawnRect = graphRect;

            // Фон
            Widgets.DrawBoxSolid(graphRect, new Color(0f, 0f, 0f, 0.3f));

            // Кнопка разворачивания
            Rect expandButton = new Rect(graphRect.x + 5, graphRect.y + 5, 20f, 20f);
            if (Widgets.ButtonText(expandButton, isExpanded ? "-" : "+"))
            {
                isExpanded = !isExpanded;
            }

            // Сетка
            DrawGrid(graphRect);

            // График
            DrawThreatLine(graphRect);

            // Текущее время
            DrawCurrentTimeLine(graphRect);

            // Подписи
            DrawLabels(graphRect);

            // Тултип
            HandleTooltip(graphRect);
        }

        private void DrawGrid(Rect rect)
        {
            // Горизонтальные линии (проценты)
            for (int i = 0; i <= 10; i++)
            {
                float y = rect.y + rect.height * (1f - i / 10f);
                Widgets.DrawLineHorizontal(rect.x, y, rect.width);
            }

            // Вертикальные линии (дни)
            float dayWidth = rect.width / 8f; // -1 to +7 days
            for (int i = 0; i <= 8; i++)
            {
                float x = rect.x + i * dayWidth;
                Widgets.DrawLineVertical(x, rect.y, rect.height);
            }
        }

        private void DrawThreatLine(Rect rect)
        {
            var history = trendCalculator.GetHistory().ToList();
            var forecast = trendCalculator.GetForecast();

            if (!history.Any()) return;

            // История
            for (int i = 1; i < history.Count; i++)
            {
                float x1 = GetXForTick(history[i - 1].Key, rect);
                float y1 = GetYForValue(history[i - 1].Value, rect);
                float x2 = GetXForTick(history[i].Key, rect);
                float y2 = GetYForValue(history[i].Value, rect);

                Widgets.DrawLine(
                    new Vector2(x1, y1),
                    new Vector2(x2, y2),
                    GraphLineColor,
                    1f
                );
            }

            // Прогноз
            int lastHistoryTick = history.Last().Key;
            for (int i = 1; i < forecast.Length; i++)
            {
                float x1 = GetXForTick(lastHistoryTick + ((i - 1) * GenDate.TicksPerHour), rect);
                float y1 = GetYForValue(forecast[i - 1], rect);
                float x2 = GetXForTick(lastHistoryTick + (i * GenDate.TicksPerHour), rect);
                float y2 = GetYForValue(forecast[i], rect);

                Widgets.DrawLine(
                    new Vector2(x1, y1),
                    new Vector2(x2, y2),
                    new Color(GraphLineColor.r, GraphLineColor.g, GraphLineColor.b, 0.5f),
                    1f
                );
            }
        }

        private void DrawCurrentTimeLine(Rect rect)
        {
            int currentTick = Find.TickManager.TicksGame;
            float x = GetXForTick(currentTick, rect);
            
            Widgets.DrawLine(
                new Vector2(x, rect.y),
                new Vector2(x, rect.y + rect.height),
                CurrentTimeMarkerColor,
                1f
            );
        }

        private void DrawLabels(Rect rect)
        {
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperCenter;

            // Подписи дней
            float dayWidth = rect.width / 8f;
            for (int i = -1; i <= 7; i++)
            {
                Widgets.Label(
                    new Rect(rect.x + (i + 1) * dayWidth - 15f, rect.y + rect.height + 2f, 30f, 20f),
                    i.ToString("+0;-0")
                );
            }

            // Подписи процентов
            Text.Anchor = TextAnchor.MiddleRight;
            for (int i = 0; i <= 10; i++)
            {
                Widgets.Label(
                    new Rect(rect.x - 25f, rect.y + rect.height * (1f - i / 10f) - 10f, 20f, 20f),
                    (i * 10).ToString()
                );
            }

            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void HandleTooltip(Rect rect)
        {
            if (!Mouse.IsOver(rect)) return;

            Vector2 mousePos = Event.current.mousePosition;
            float mouseX = mousePos.x - rect.x;
            float relativeX = mouseX / rect.width;

            int tickAtMouse = GetTickFromX(mouseX, rect);
            float threatAtMouse = GetThreatAtTick(tickAtMouse);

            string tooltip = $"День {GenDate.ToDays(tickAtMouse - Find.TickManager.TicksGame):+0;-0}\n" +
                           $"Угроза: {threatAtMouse:P0}";

            TooltipHandler.TipRegion(rect, tooltip);
        }

        private float GetXForTick(int tick, Rect rect)
        {
            int currentTick = Find.TickManager.TicksGame;
            float daysDiff = (tick - currentTick) / (float)GenDate.TicksPerDay;
            return rect.x + rect.width * ((daysDiff + 1) / 8f);
        }

        private float GetYForValue(float value, Rect rect)
        {
            return rect.y + rect.height * (1f - value);
        }

        private int GetTickFromX(float x, Rect rect)
        {
            float relativeX = x / rect.width;
            float dayOffset = (relativeX * 8f) - 1f;
            return Find.TickManager.TicksGame + (int)(dayOffset * GenDate.TicksPerDay);
        }

        private float GetThreatAtTick(int tick)
        {
            int currentTick = Find.TickManager.TicksGame;
            
            if (tick <= currentTick)
            {
                var history = trendCalculator.GetHistory();
                var closest = history
                    .OrderBy(kvp => Mathf.Abs(kvp.Key - tick))
                    .FirstOrDefault();
                return closest.Value;
            }
            else
            {
                var forecast = trendCalculator.GetForecast();
                int hourIndex = (tick - currentTick) / GenDate.TicksPerHour;
                if (hourIndex >= 0 && hourIndex < forecast.Length)
                    return forecast[hourIndex];
            }
            
            return 0f;
        }
    }
}