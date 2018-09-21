using System.Collections.Generic;
using Equinox.ResearchCore.Definition;
using Equinox.ResearchCore.Definition.ObjectBuilders.Triggers;
using Equinox.ResearchCore.Network;
using Equinox.Utils;
using Equinox.Utils.Logging;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace Equinox.ResearchCore.Modules
{
    public class InteractionModuleServer : ModuleBase
    {
        private const double VALIDATION_DISTANCE_TOLERANCE = 25;

        private readonly MyConcurrentQueue<KeyValuePair<ulong, PlayerInteractionUnlockedMsg>> _pending =
            new MyConcurrentQueue<KeyValuePair<ulong, PlayerInteractionUnlockedMsg>>();

        public InteractionModuleServer(ResearchManager mgr) : base(mgr)
        {
        }

        public override void Attach()
        {
            if (!MyAPIGateway.Session.IsServerDecider())
                return;

            Manager.NetworkMessageRecieved += OnMsg;
        }

        public override void Detach()
        {
            if (!MyAPIGateway.Session.IsServerDecider())
                return;

            Manager.NetworkMessageRecieved -= OnMsg;
        }

        public override void Update()
        {
            KeyValuePair<ulong, PlayerInteractionUnlockedMsg> process;
            if (_pending.TryDequeue(out process))
            {
                var msg = process.Value;
                var player = Manager.Core.Players.TryGetPlayerBySteamId(process.Key);
                if (player == null)
                {
                    Logger.Debug(
                        $"{process.Key}/{msg.ResearchId}/{msg.StateStorage} validation: player not found");
                    return;
                }
                var playerEntity = player.Controller.ControlledEntity as IMyEntity;
                if (playerEntity == null)
                {
                    Logger.Debug(
                        $"{player.DisplayName}/{msg.ResearchId}/{msg.StateStorage} validation: player entity not found");
                    return;
                }
                var playerPosition = playerEntity.WorldMatrix.Translation;
                var playerData = Manager.GetOrCreatePlayer(player);
                if (playerData == null)
                {
                    Logger.Debug(
                        $"{player.DisplayName}/{msg.ResearchId}/{msg.StateStorage} validation: player data not found");
                    return;
                }
                var researchData = playerData.PlayerResearchState(msg.ResearchId, true);
                if (researchData == null)
                {
                    Logger.Debug(
                        $"{player.DisplayName}/{msg.ResearchId}/{msg.StateStorage} validation: research does not exist");
                    return;
                }
                if (researchData.State != ResearchState.InProgress)
                {
                    Logger.Debug(
                        $"{player.DisplayName}/{msg.ResearchId}/{msg.StateStorage} validation: research in wrong state ({researchData.State})");
                    return;
                }
                var trigger =
                    researchData.Definition.Trigger.StateStorageProvider(msg.StateStorage) as Ob_Trigger_Interact;
                if (trigger == null)
                {
                    var noTypeTrigger = researchData?.Definition.Trigger.StateStorageProvider(msg.StateStorage);
                    var info = noTypeTrigger == null
                        ? "doesn't exist"
                        : $"is wrong type ({noTypeTrigger.GetType().Name})";
                    Logger.Debug(
                        $"{player.DisplayName}/{msg.ResearchId}/{msg.StateStorage} validation: state storage {info}");
                    return;
                }
                IMyEntity entity;
                if (!MyAPIGateway.Entities.TryGetEntityById(msg.InteractionTargetEntity, out entity))
                {
                    Logger.Debug(
                        $"{player.DisplayName}/{msg.ResearchId}/{msg.StateStorage} validation: entity not found");
                    return;
                }
                var character = entity as IMyCharacter;
                if (trigger.OnCharacterInteract && character != null
                    && (string.IsNullOrWhiteSpace(trigger.RequiredStorageValue) ||
                        Equals(
                            character.Storage?.GetValue(Ob_Trigger_Interact.InteractResearchStorageComponent),
                            trigger.RequiredStorageValue)))
                {
                    if (MyAPIGateway.Players.GetPlayerControllingEntity(character) != player
                        && (character.WorldMatrix.Translation - playerPosition).Length() <
                        VALIDATION_DISTANCE_TOLERANCE)
                        researchData.UpdateStatefulStorage(msg.StateStorage, true);
                    else
                        Logger.Warning(
                            $"Interaction validation failed for {player.SteamUserId} ({player.DisplayName}) on {researchData.Definition.Id}/{msg.StateStorage}");
                    return;
                }
                var grid = entity as IMyCubeGrid;
                var block = grid?.GetCubeBlock(msg.BlockPosition);
                if (block != null && trigger.BlockInteractTarget.Contains(block.BlockDefinition.Id)
                    && (string.IsNullOrWhiteSpace(trigger.RequiredStorageValue) ||
                        Equals(
                            block.FatBlock?.Storage?.GetValue(Ob_Trigger_Interact.InteractResearchStorageComponent),
                            trigger.RequiredStorageValue)))
                {
                    Vector3D blockCenter;
                    block.ComputeWorldCenter(out blockCenter);
                    Vector3 extent;
                    block.ComputeScaledHalfExtents(out extent);
                    if ((blockCenter - playerPosition).Length() - extent.Length() < VALIDATION_DISTANCE_TOLERANCE)
                        researchData.UpdateStatefulStorage(msg.StateStorage, true);
                    else
                        Logger.Warning(
                            $"Interaction validation failed for {player.SteamUserId} ({player.DisplayName}) on {researchData.Definition.Id}/{msg.StateStorage}");
                    return;
                }
                Logger.Debug(
                    $"{player.DisplayName}/{msg.ResearchId}/{msg.StateStorage} validation: unknown error (character: {character?.DisplayName ?? "null"}, block: {block?.BlockDefinition.Id.ToString() ?? "null"}");
            }
        }

        private void OnMsg(ulong playerSteamId, IMsg msg)
        {
            var im = msg as PlayerInteractionUnlockedMsg;
            if (im == null)
                return;
            _pending.Enqueue(new KeyValuePair<ulong, PlayerInteractionUnlockedMsg>(playerSteamId, im));
        }
    }
}