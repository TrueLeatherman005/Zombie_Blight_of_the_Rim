using Verse;
using LudeonTK;

namespace ZombieBlight
{
    public static class DebugActions
    {
        [DebugAction("Zombie Blight", "Toggle scent visualization", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ToggleScentVisualization()
        {
            ScentDebugViewComponent.ToggleDebugView();
        }
    }
}