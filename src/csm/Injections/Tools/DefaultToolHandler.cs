using CSM.API;
using CSM.API.Commands;
using CSM.Networking;
using CSM.Panels;
using ProtoBuf;
using HarmonyLib;
using UnityEngine;
using ColossalFramework;
using CSM.Container;
using ColossalFramework.Math;

namespace CSM.Injections.Tools
{
    [HarmonyPatch(typeof(DefaultTool))]
    [HarmonyPatch("SimulationStep")]
    public class DefaultToolHandler {

        private static InstanceID lastInstanceId = InstanceID.Empty;
        private static int lastSubIndex = -1;

        public static void Postfix(DefaultTool __instance)
        {

            if (MultiplayerManager.Instance.CurrentRole != MultiplayerRole.None) { 

                InstanceID hoverInstance;
                int subIndex = -1;
                __instance.GetHoverInstance(out hoverInstance, out subIndex);
                if (hoverInstance != lastInstanceId || lastSubIndex != subIndex) {
                    lastInstanceId = hoverInstance;
                    lastSubIndex = subIndex;

                    // Set the correct playerName if our currentRole is SERVER, else use the CurrentClient Username
                    string playerName;
                    if (MultiplayerManager.Instance.CurrentRole == MultiplayerRole.Server)
                    {
                        playerName = MultiplayerManager.Instance.CurrentServer.Config.Username;
                    }
                    else
                    {
                        playerName = MultiplayerManager.Instance.CurrentClient.Config.Username;
                    }

                    // Send info to all clients
                    Command.SendToAll(new PlayerDefaultToolCommandHandler.Command
                    {
                        PlayerName = playerName,
                        HoveredInstanceID = hoverInstance,
                        SubIndex = subIndex
                    });

                    // Chat.Instance.("___m_hoverInstance = " + ___m_hoverInstance);
                }
            }
        }    
    }

    public class PlayerDefaultToolCommandHandler : CommandHandler<PlayerDefaultToolCommandHandler.Command>
    {

        [ProtoContract]
        public class Command : CommandBase, IPlayerToolRenderer
        {
            [ProtoMember(1)]
            public string PlayerName { get; set; }
            [ProtoMember(2)]
            public InstanceID HoveredInstanceID { get; set; }

            [ProtoMember(3)]
            public InstanceID HoveredInstanceID2 { get; set; }

            [ProtoMember(4)]
            public int SubIndex { get; set; }

            public void RenderOverlay(RenderManager.CameraInfo cameraInfo) {
                InstanceID hoverInstance = HoveredInstanceID;
                InstanceID hoverInstance2 = HoveredInstanceID2;
                int subIndex = SubIndex;

                var buildingManager = Singleton<BuildingManager>.instance;
                var vehicleManager = Singleton<VehicleManager>.instance;
                var districtManager = Singleton<DistrictManager>.instance;
                var propManager = Singleton<PropManager>.instance;
                var treeManager = Singleton<TreeManager>.instance;
                var disasterManager = Singleton<DisasterManager>.instance;
                var infoManager = Singleton<InfoManager>.instance;

                Color playerHoverColor = Color.blue;
                var alphaMult = 1.0f;
                switch (hoverInstance.Type) {
                    case InstanceType.Building:
                        int buildingId = hoverInstance.Building;
                        while (buildingId != 0 && buildingId < buildingManager.m_buildings.m_size) {
                            Building building = buildingManager.m_buildings.m_buffer[buildingId];
                            BuildingTool.RenderOverlay(cameraInfo, building.Info, building.Length, building.m_position, building.m_angle, playerHoverColor, false);
                            buildingId = building.m_subBuilding; // repeat by traversing to sub building
                        }
                        break;
                    case InstanceType.Vehicle:
                        var vehicleId = hoverInstance.Vehicle;
                        vehicleManager.m_vehicles.m_buffer[vehicleId].CheckOverlayAlpha(ref alphaMult);
                        playerHoverColor.a *= alphaMult;
                        vehicleManager.m_vehicles.m_buffer[vehicleId].RenderOverlay(cameraInfo, vehicleId, playerHoverColor);
                        break;
                    case InstanceType.ParkedVehicle:
                        var parkedVehicleId = hoverInstance.ParkedVehicle;
                        vehicleManager.m_parkedVehicles.m_buffer[parkedVehicleId].CheckOverlayAlpha(ref alphaMult);
                        playerHoverColor.a *= alphaMult;
                        vehicleManager.m_parkedVehicles.m_buffer[parkedVehicleId].RenderOverlay(cameraInfo, parkedVehicleId, playerHoverColor);
                        break;                        
                    case InstanceType.District:
                        var districtId = hoverInstance.District;
                        districtManager.CheckOverlayAlpha(cameraInfo, districtId, ref alphaMult);
                        playerHoverColor.a *= alphaMult;
                        vehicleManager.m_vehicles.m_buffer[districtId].RenderOverlay(cameraInfo, districtId, playerHoverColor);
                        break;
                    case InstanceType.NetSegment:
                        ushort netSegment = hoverInstance.NetSegment;
                        ushort netSegment2 = hoverInstance2.NetSegment;
                        break;
                    case InstanceType.Prop:
                        ushort propId = hoverInstance.Prop;
                        PropInstance prop = propManager.m_props.m_buffer[(int)propId];
                        Randomizer propRandomizer = new Randomizer((int)propId);
                        float propScale = prop.Info.m_minScale + (float)propRandomizer.Int32(10000U) * (prop.Info.m_maxScale - prop.Info.m_minScale) * 0.0001f;
                        PropTool.CheckOverlayAlpha(prop.Info, propScale, ref alphaMult);
                        playerHoverColor.a *= alphaMult;
                        PropTool.RenderOverlay(cameraInfo, prop.Info, prop.Position, propScale, prop.m_angle, playerHoverColor);
                        break;
                    case InstanceType.Tree:
                        uint treeId = hoverInstance.Tree;
                        TreeInstance tree = treeManager.m_trees.m_buffer[(int)treeId];
                        Randomizer treeRandomizer = new Randomizer((int)treeId);
                        float treeScale = tree.Info.m_minScale + (float)treeRandomizer.Int32(10000U) * (tree.Info.m_maxScale - tree.Info.m_minScale) * 0.0001f;
                        TreeTool.CheckOverlayAlpha(tree.Info, treeScale, ref alphaMult);
                        playerHoverColor.a *= alphaMult;
                        TreeTool.RenderOverlay(cameraInfo, tree.Info, tree.Position, treeScale, playerHoverColor);
                        break;
                    case InstanceType.Disaster:
                        ushort disasterId = hoverInstance.Disaster;
                        DisasterData disaster = disasterManager.m_disasters.m_buffer[disasterId];
                        DisasterTool.CheckOverlayAlpha(disaster.Info, ref alphaMult);
                        playerHoverColor.a *= alphaMult;
                        DisasterTool.RenderOverlay(cameraInfo, disaster.Info, disaster.m_targetPosition, disaster.m_angle, playerHoverColor);
                        break;
                    case InstanceType.Park:
                        byte park = hoverInstance.Park;
                        districtManager.CheckParkOverlayAlpha(cameraInfo, park, ref alphaMult);
                        playerHoverColor.a *= alphaMult;
                        districtManager.RenderParkHighlight(cameraInfo, park, playerHoverColor);                        
                        break;
                    // TODO: handle net segments
                }
            }
            
        }

        public PlayerDefaultToolCommandHandler()
        {
            TransactionCmd = false;
        }

        protected override void Handle(Command command)
        {
            if (!MultiplayerManager.Instance.IsConnected())
            {
                // Ignore packets while not connected
                return;
            }

            ToolManagerOverlayRenderer.SetOverlayForPlayer(command.PlayerName, command); //new PlayerDefaultToolRenderer() //new Tuple<InstanceID, InstanceID, int>(command.HoveredInstanceID, command.HoveredInstanceID2, command.SubIndex);
        }
    }

    
}