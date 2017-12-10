using System;
using System.Collections.Generic;
using System.Text;
using Equinox.ResearchCore.Definition.ObjectBuilders.Triggers;
using Equinox.ResearchCore.State;
using Equinox.Utils.Logging;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Interfaces;
using VRage.ModAPI;

namespace Equinox.ResearchCore.Modules
{
    public class InventoryScanModule : ModuleBase
    {
        private const int SPREAD_TICKS = 10;

        public InventoryScanModule(ResearchManager mgr) : base(mgr)
        {
        }

        private class PlayerBindData
        {
            public readonly Dictionary<MyDefinitionId, HashSet<BindEntry>> Binds =
                new Dictionary<MyDefinitionId, HashSet<BindEntry>>();

            public void Bind(PlayerResearchState rs, string key)
            {
                var entry = new BindEntry(rs, key);
                HashSet<BindEntry> set;
                if (!Binds.TryGetValue(entry.Trigger.DefinitionId, out set))
                    Binds.Add(entry.Trigger.DefinitionId, set = new HashSet<BindEntry>());
                set.Add(entry);
            }

            public void Unbind(PlayerResearchState rs, string key)
            {
                var entry = new BindEntry(rs, key);
                var set = Binds[entry.Trigger.DefinitionId];
                if (!set.Remove(entry))
                    return;
                if (set.Count == 0)
                    Binds.Remove(entry.Trigger.DefinitionId);
            }
        }

        private readonly Dictionary<IMyPlayer, PlayerBindData> _listeningPlayers =
            new Dictionary<IMyPlayer, PlayerBindData>();

        private readonly HashSet<IMyControllableEntity> _listeningEntities = new HashSet<IMyControllableEntity>();
        private readonly Dictionary<IMyInventory, int> _listeningInventories = new Dictionary<IMyInventory, int>();
        private readonly List<IMyInventory> _listeningInventoriesOrder = new List<IMyInventory>();

        public override void Attach()
        {
            Manager.PlayerResearchStatefulStorageAddRemove += OnStatefulStorageAddRemove;
        }

        public override void Detach()
        {
            Manager.PlayerResearchStatefulStorageAddRemove -= OnStatefulStorageAddRemove;
        }

        private int _spreadOffset;

        private readonly HashSet<BindEntry> _statefulStorageToSet = new HashSet<BindEntry>();

        public override void Update()
        {
            base.Update();
            _statefulStorageToSet.Clear();
            for (var i = _spreadOffset; i < _listeningInventoriesOrder.Count; i += SPREAD_TICKS)
            {
                var inv = _listeningInventoriesOrder[i];
                var ent = (inv as MyEntityComponentBase)?.Entity;
                if (ent == null)
                    continue;
                var player = MyAPIGateway.Players.GetPlayerControllingEntity(ent);
                if (player == null)
                    continue;

                var set = _listeningPlayers.GetValueOrDefault(player);
                if (set == null)
                    continue;
                foreach (var idBind in set.Binds)
                {
                    var removed = 0d;
                    var amnt = (double) inv.GetItemAmount(idBind.Key);
                    foreach (var k in idBind.Value)
                    {
                        if (amnt >= k.Trigger.Count - removed)
                        {
                            if (_statefulStorageToSet.Add(k) && !(k.Research.StatefulStorage(k.Point) ?? false))
                                removed += k.Trigger.Count;
                        }
                    }
                    if (removed > 0)
                        inv.RemoveItemsOfType((MyFixedPoint) removed, idBind.Key);
                }
            }
            foreach (var k in _statefulStorageToSet)
                k.Research.UpdateStatefulStorage(k.Point, true);
            _statefulStorageToSet.Clear();
            _spreadOffset = (_spreadOffset + 1) % SPREAD_TICKS;
        }

        private void OnStatefulStorageAddRemove(PlayerResearchState research, string key, bool removed)
        {
            var trigger = research.Definition.Trigger.StateStorageProvider(key) as Ob_Trigger_HasItem;
            if (trigger == null)
                return;
            if (removed)
                UnwatchPlayer(research, key);
            else
                WatchPlayer(research, key);
        }

        private void WatchPlayer(PlayerResearchState data, string key)
        {
            var player = data.Player.Player;
            PlayerBindData attach;
            if (_listeningPlayers.TryGetValue(player, out attach))
            {
                attach.Bind(data, key);
                return;
            }
            _listeningPlayers.Add(player, attach = new PlayerBindData());
            attach.Bind(data, key);
            player.Controller.ControlledEntityChanged += OnControlledEntityChanged;
            if (player.Controller.ControlledEntity != null)
                WatchEntity(player.Controller.ControlledEntity);
        }

        private void UnwatchPlayer(PlayerResearchState data, string key)
        {
            var player = data.Player.Player;
            PlayerBindData attach;
            if (!_listeningPlayers.TryGetValue(player, out attach))
                return;
            attach.Unbind(data, key);
            if (attach.Binds.Count != 0)
                return;
            _listeningPlayers.Remove(player);
            player.Controller.ControlledEntityChanged -= OnControlledEntityChanged;
            if (player.Controller.ControlledEntity != null)
                UnwatchEntity(player.Controller.ControlledEntity);
        }

        private void OnControlledEntityChanged(IMyControllableEntity old,
            IMyControllableEntity @new)
        {
            if (old != null)
                UnwatchEntity(old);
            if (@new != null)
                WatchEntity(@new);
            ;
        }

        private void WatchEntity(IMyControllableEntity entity)
        {
            var container = (entity as IMyEntity)?.Components;
            if (container == null)
                return;
            if (!_listeningEntities.Add(entity))
                return;

            container.ComponentAdded += OnComponentAdded;
            container.ComponentRemoved += OnComponentRemoved;
            foreach (var c in container.GetComponentTypes())
            {
                MyComponentBase b;
                if (container.TryGet(c, out b) && b is MyEntityComponentBase)
                    OnComponentAdded(c, (MyEntityComponentBase) b);
            }
        }

        private void UnwatchEntity(IMyControllableEntity entity)
        {
            var container = (entity as IMyEntity)?.Components;
            if (container == null)
                return;
            if (!_listeningEntities.Remove(entity))
                return;

            container.ComponentAdded -= OnComponentAdded;
            container.ComponentRemoved -= OnComponentRemoved;
            foreach (var c in container.GetComponentTypes())
            {
                MyComponentBase b;
                if (container.TryGet(c, out b) && b is MyEntityComponentBase)
                    OnComponentRemoved(c, (MyEntityComponentBase) b);
            }
        }

        private void OnComponentAdded(Type type, MyEntityComponentBase cmp)
        {
            var inv = cmp as IMyInventory;
            if (inv == null)
                return;
            if (_listeningInventories.ContainsKey(inv))
                return;
            _listeningInventories.Add(inv, _listeningInventoriesOrder.Count);
            _listeningInventoriesOrder.Add(inv);
        }

        private void OnComponentRemoved(Type type, MyEntityComponentBase cmp)
        {
            var inv = cmp as IMyInventory;
            if (inv == null)
                return;
            int index;
            if (!_listeningInventories.TryGetValue(inv, out index))
                return;
            _listeningInventories.Remove(inv);
            _listeningInventoriesOrder.RemoveAtFast(index);
            if (_listeningInventoriesOrder.Count > 0)
                _listeningInventories[_listeningInventoriesOrder[index]] = index;
        }

        private struct BindEntry : IEquatable<BindEntry>
        {
            public readonly PlayerResearchState Research;
            public readonly string Point;
            public readonly Ob_Trigger_HasItem Trigger;

            public BindEntry(PlayerResearchState rs, string key)
            {
                Research = rs;
                Point = key;
                Trigger = (Ob_Trigger_HasItem) rs.Definition.Trigger.StateStorageProvider(key);
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
    }
}