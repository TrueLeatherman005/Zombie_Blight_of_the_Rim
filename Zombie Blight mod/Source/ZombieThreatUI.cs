using UnityEngine;
using Verse;
using RimWorld;

namespace ZombieBlight 
{
    public class ZombieThreatUI : MonoBehaviour
    {
        private static ZombieThreatUI instance;
        private ZombieThreatGraph graph;
        private ZombieTrendCalculator trendCalculator;

        public static void Initialize()
        {
            if (instance == null)
            {
                var go = new GameObject("ZombieThreatUI");
                instance = go.AddComponent<ZombieThreatUI>();
                DontDestroyOnLoad(go);
            }
        }

        private void Awake()
        {
            trendCalculator = new ZombieTrendCalculator();
            graph = new ZombieThreatGraph(trendCalculator);
        }

        private void OnGUI()
        {
            if (Find.CurrentMap == null) return;
            
            // Рисуем график в правом верхнем углу
            float rightEdge = UI.screenWidth - 20f;
            float topEdge = 20f;
            
            graph.DrawAt(rightEdge - 360f, topEdge);
        }

        public void UpdateThreat()
        {
            if (trendCalculator != null)
            {
                trendCalculator.UpdateTrend();
            }
        }
    }
}