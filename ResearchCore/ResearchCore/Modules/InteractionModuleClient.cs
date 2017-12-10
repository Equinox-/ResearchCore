using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Equinox.ResearchCore.Definition.ObjectBuilders.Triggers;
using Equinox.ResearchCore.Network;
using Equinox.ResearchCore.State;
using Equinox.ResearchCore.Utils;
using Equinox.Utils;
using Equinox.Utils.Logging;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Weapons;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Interfaces;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

namespace Equinox.ResearchCore.Modules
{
    public class InteractionModuleClient : ModuleBase
    {
        public InteractionModuleClient(ResearchManager mgr) : base(mgr)
        {
        }

        public override void Attach()
        {
            if (!MyAPIGateway.Session.IsPlayerController())
                return;
            Manager.PlayerResearchStatefulStorageAddRemove += OnStatefulStorageAddRemove;
        }

        public override void Detach()
        {
            if (!MyAPIGateway.Session.IsPlayerController())
                return;
            Manager.PlayerResearchStatefulStorageAddRemove -= OnStatefulStorageAddRemove;
        }

        public struct BindEntry : IEquatable<BindEntry>
        {
            public readonly PlayerResearchState Research;
            public readonly string Point;
            public readonly Ob_Trigger_Interact Trigger;

            public BindEntry(PlayerResearchState rs, string key)
            {
                Research = rs;
                Point = key;
                Trigger = (Ob_Trigger_Interact) rs.Definition.Trigger.StateStorageProvider(key);
            }

            public bool Equals(BindEntry other)
            {
                return Equals(Research, other.Research) && string.Equals(Point, other.Point);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                return obj is BindEntry && Equals((BindEntry) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((Research != null ? Research.GetHashCode() : 0) * 397) ^
                           (Point != null ? Point.GetHashCode() : 0);
                }
            }
        }

        private readonly Dictionary<MyDefinitionId, HashSet<BindEntry>> _bindsByHandItem =
            new Dictionary<MyDefinitionId, HashSet<BindEntry>>();

        private readonly HashSet<BindEntry> _bindsForAllHands = new HashSet<BindEntry>();

        private void OnStatefulStorageAddRemove(PlayerResearchState research, string key, bool removed)
        {
            var trigger = research.Definition.Trigger.StateStorageProvider(key) as Ob_Trigger_Interact;
            if (trigger == null)
                return;
            var entry = new BindEntry(research, key);
            if (trigger.HandItem.Count == 0)
            {
                if (removed)
                    _bindsForAllHands.Remove(entry);
                else
                    _bindsForAllHands.Add(entry);
                return;
            }
            foreach (var hand in trigger.HandItem)
            {
                HashSet<BindEntry> set;
                if (!_bindsByHandItem.TryGetValue(hand, out set))
                    set = _bindsByHandItem[hand] = new HashSet<BindEntry>();
                if (removed)
                    set.Remove(entry);
                else
                    set.Add(entry);
                if (set.Count == 0)
                    _bindsByHandItem.Remove(hand);
            }
        }

        private readonly HashSet<Ob_Trigger_Interact> _sentInteractionForTrigger = new HashSet<Ob_Trigger_Interact>();

        public override void Update()
        {
            if (!MyAPIGateway.Session.IsPlayerController())
                return;
            var player = MyAPIGateway.Session.LocalHumanPlayer;
            var character = player?.Controller.ControlledEntity as IMyCharacter;
            var tool = character?.EquippedTool;
            var caster = tool?.Components.Get<MyCasterComponent>();
            if (caster?.HitCharacter == null && caster?.HitBlock == null)
                return;
            var id = ((IMyHandheldGunObject<MyToolBase>) tool).PhysicalItemDefinition.Id;
            var extraBinds = _bindsByHandItem.GetValueOrDefault(id);
            IEnumerable<BindEntry> binds = _bindsForAllHands;
            if (extraBinds != null)
                binds = binds.Concat(extraBinds);
            var hitBlock = (IMySlimBlock) caster.HitBlock;
            var hitCharacter = (IMyCharacter) caster.HitCharacter;

            foreach (var bind in binds)
                if (MyAPIGateway.Input.IsGameControlPressed(bind.Trigger.GameControlId))
                {
                    if (_sentInteractionForTrigger.Contains(bind.Trigger))
                        continue;
                    if (bind.Trigger.OnCharacterInteract && hitCharacter != null
                        && MyAPIGateway.Players.GetPlayerControllingEntity(hitCharacter) != player
                        && (string.IsNullOrWhiteSpace(bind.Trigger.RequiredStorageValue) ||
                            Equals(
                                hitCharacter.Storage?.GetValue(Ob_Trigger_Interact.InteractResearchStorageComponent),
                                bind.Trigger.RequiredStorageValue)))
                    {
                        TriggerBinding(bind.Research, bind.Point, hitCharacter.EntityId);
                        _sentInteractionForTrigger.Add(bind.Trigger);
                    }
                    else if (hitBlock != null &&
                             bind.Trigger.BlockInteractTarget.Contains(hitBlock.BlockDefinition.Id)
                             && (string.IsNullOrWhiteSpace(bind.Trigger.RequiredStorageValue) ||
                                 Equals(
                                     hitBlock.FatBlock?.Storage?.GetValue(Ob_Trigger_Interact
                                         .InteractResearchStorageComponent),
                                     bind.Trigger.RequiredStorageValue)))
                    {
                        TriggerBinding(bind.Research, bind.Point, hitBlock.CubeGrid.EntityId,
                            hitBlock.Position);
                        _sentInteractionForTrigger.Add(bind.Trigger);
                    }
                }
                else
                {
                    _sentInteractionForTrigger.Remove(bind.Trigger);
                }
        }

        private void TriggerBinding(PlayerResearchState state, string key, long entityId, Vector3I? blockCoord = null)
        {
            Manager.SendMessageToServer(new PlayerInteractionUnlockedMsg()
            {
                ResearchId = state.Definition.Id,
                StateStorage = key,
                InteractionTargetEntity = entityId,
                BlockPosition = blockCoord ?? Vector3I.Zero
            });
        }
    }
}