using System.Linq;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System;
using CSM.API.Commands;
using CSM.Networking;
using ICities;
using ColossalFramework;
using ColossalFramework.Math;
using ProtoBuf;
using HarmonyLib;
using UnityEngine;
using CSM.BaseGame.Helpers;
using CSM.API.Helpers;

namespace CSM.Injections.Tools
{
    [HarmonyPatch(typeof(NetTool))]
    [HarmonyPatch("OnToolLateUpdate")]
    public class NetToolHandler {

        private static PlayerNetToolCommandHandler.Command lastCommand;
        // private static 

        public static void Postfix(NetTool __instance, ToolController ___m_toolController, NetTool.ControlPoint[] ___m_cachedControlPoints, int ___m_cachedControlPointCount, ushort[] ___m_upgradedSegments)
        {
            if (MultiplayerManager.Instance.CurrentRole != MultiplayerRole.None) {

                if (___m_toolController != null && ___m_toolController.IsInsideUI) {
                    return;
                }

                // with the NetTool the world piosition of the cursor will match the current last control point base on the following logic
                // See NetTool::OnToolUpdate
                Vector3 worldPosition;                
                if (__instance.m_mode == NetTool.Mode.Upgrade && ___m_cachedControlPointCount >= 2)
                {
                    worldPosition = ___m_cachedControlPoints[1].m_position;
                }
                else
                {
                    worldPosition = ___m_cachedControlPoints[___m_cachedControlPointCount].m_position;
                }

                // Send info to all clients
                var newCommand = new PlayerNetToolCommandHandler.Command
                {
                    Prefab = (ushort)Mathf.Clamp(__instance.m_prefab.m_prefabDataIndex, 0, 65535),
                    Mode = (int) __instance.m_mode,
                    ControlPoints = ___m_cachedControlPoints,
                    ControlPointCount = ___m_cachedControlPointCount,
                    UpgradedSegments = ___m_upgradedSegments,
                    CursorWorldPosition = worldPosition,
                    PlayerName = MultiplayerManager.Instance.CurrentUsername()
                };
                if(!object.Equals(newCommand, lastCommand)) {
                    lastCommand = newCommand;
                    Command.SendToAll(newCommand);
                }
                if(ToolSimulatorCursorManager.ShouldTest()) {
                    Command.GetCommandHandler(typeof(PlayerNetToolCommandHandler.Command)).Parse(newCommand);
                }

            }
        }    
    }

    public class PlayerNetToolCommandHandler : BaseToolCommandHandler<PlayerNetToolCommandHandler.Command, NetTool>
    {

        [ProtoContract]
        public class Command : ToolCommandBase, IEquatable<Command>
        {
            [ProtoMember(1)]
            public ushort Prefab { get; set; }
            [ProtoMember(2)]
            public int Mode { get; set; }
            [ProtoMember(3)]
            public NetTool.ControlPoint[] ControlPoints { get; set; } 
            [ProtoMember(4)]
            public int ControlPointCount { get; set; }
            [ProtoMember(5)]
            public ushort[] UpgradedSegments { get; set; }

            // TODO: Transmit errors

            public bool Equals(Command other)
            {
                return base.Equals(other) &&
                object.Equals(this.Prefab, other.Prefab) &&
                object.Equals(this.Mode, other.Mode) &&
                object.Equals(this.ControlPoints, other.ControlPoints) &&
                object.Equals(this.ControlPointCount, other.ControlPointCount) &&
                object.Equals(this.UpgradedSegments, other.UpgradedSegments);
            }
            
        }

        protected override void Configure(NetTool tool, ToolController toolController, Command command) {
            // Note: Some private fields are already initialised by the ToolSimulator
            // These fields here are the important ones to transmit between game sessions
            NetInfo prefab = PrefabCollection<NetInfo>.GetPrefab(command.Prefab);
            ReflectionHelper.SetAttr(tool, "m_prefab", prefab);
            NetTool.Mode mode = (NetTool.Mode) Enum.GetValues(typeof(NetTool.Mode)).GetValue(command.Mode);
            tool.m_mode = mode;
            ReflectionHelper.SetAttr(tool, "m_cachedControlPoints", command.ControlPoints);
            ReflectionHelper.SetAttr(tool, "m_cachedControlPointCount", command.ControlPointCount);

            ushort[] segments;
            if(command.UpgradedSegments != null) {
                segments = command.UpgradedSegments;
            } else {
                segments = new ushort[0];
            }

            ReflectionHelper.SetAttr(tool, "m_upgradedSegments", new HashSet<ushort>(segments));
        }

        protected override CursorInfo GetCursorInfo(NetTool tool)
        {
            if (tool.m_mode == NetTool.Mode.Upgrade)
            {
                return tool.Prefab.m_upgradeCursor ?? tool.m_upgradeCursor;
            } else {
                return tool.Prefab.m_placementCursor ?? tool.m_placementCursor;
            }
        }
    }

    
}