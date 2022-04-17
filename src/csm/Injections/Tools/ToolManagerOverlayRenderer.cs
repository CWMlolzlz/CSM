using ColossalFramework;
using System.Collections.Generic;
using HarmonyLib;


namespace CSM.Injections.Tools
{
    
    public interface IPlayerToolRenderer
    {
        void RenderOverlay(RenderManager.CameraInfo cameraInfo);
    }

    [HarmonyPatch(typeof(ToolManager))]
    [HarmonyPatch("EndOverlayImpl")]
    public class ToolManagerOverlayRenderer {

        private static Dictionary<string, IPlayerToolRenderer> playerToolRenderers = new Dictionary<string, IPlayerToolRenderer>();
        
        public static void Postfix(RenderManager.CameraInfo cameraInfo)
        {
            foreach (var playerHoveredState in playerToolRenderers.Values)
            {
                playerHoveredState.RenderOverlay(cameraInfo);
            }
        }

        public static void SetOverlayForPlayer(string playerName, IPlayerToolRenderer renderer) {
            playerToolRenderers[playerName] = renderer;
        }

    }
}