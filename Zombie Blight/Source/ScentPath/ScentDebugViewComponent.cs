using UnityEngine;
using Verse;

namespace ZombieBlight 
{
    public class ScentDebugViewComponent : MapComponent 
    {
        private static bool debugViewEnabled;

        public ScentDebugViewComponent(Map map) : base(map) { }

        public static void ToggleDebugView()
        {
            debugViewEnabled = !debugViewEnabled;
        }

        public override void MapComponentOnGUI()
        {
            if (!debugViewEnabled) return;

            var scents = map.GetComponent<ScentsMapComponent>();
            if (scents == null) return;

            // Отрисовка следов на карте
            foreach (IntVec3 cell in map.AllCells)
            {
                float intensity = scents.GetScentIntensityAt(cell);
                if (intensity > 0)
                {
                    Vector3 pos = cell.ToVector3().MapToUIPosition();
                    Color color = new Color(1f, 0f, 0f, intensity);
                    GenUI.DrawMouseoverBracket(pos, color);
                }
            }
        }
    }
}