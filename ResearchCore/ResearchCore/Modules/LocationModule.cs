using System.Collections.Generic;
using Equinox.ResearchCore.Definition.ObjectBuilders.Triggers;
using Equinox.ResearchCore.State;
using Equinox.Utils.Logging;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI.Interfaces;
using VRage.ModAPI;
using VRageMath;

namespace Equinox.ResearchCore.Modules
{
    public class LocationModule : ModuleBase
    {
        private readonly ILogging _log;

        public LocationModule(ResearchManager mgr) : base(mgr)
        {
            _log = mgr.Logger.Root().CreateProxy(nameof(LocationModule));
        }

        public override void Attach()
        {
            Manager.PlayerResearchStatefulStorageAddRemove += OnStatefulStorageAddRemove;
        }

        public override void Detach()
        {
            Manager.PlayerResearchStatefulStorageAddRemove -= OnStatefulStorageAddRemove;
        }

        private readonly MyDynamicAABBTreeD _detectorTree = new MyDynamicAABBTreeD();
        private readonly Dictionary<ResearchStatefulKey, DetectorData> _detectors = new Dictionary<ResearchStatefulKey, DetectorData>();

        private class DetectorData
        {
            public readonly ResearchStatefulKey Key;
            public readonly BoundingSphereD Detector;
            public int Handle;

            public DetectorData(ResearchStatefulKey key, Ob_Trigger_Location loc)
            {
                Key = key;
                Detector = new BoundingSphereD(loc.Position, loc.Radius);
            }
        }

        private void EnsureDetector(string researchKey, Ob_Trigger_Location trigger)
        {
            var key = new ResearchStatefulKey(researchKey, trigger.StateStorageKey);
            if (_detectors.ContainsKey(key))
                return;
            var data = new DetectorData(key, trigger);
            _detectors.Add(key, data);
            var aabb = BoundingBoxD.CreateFromSphere(data.Detector);
            data.Handle = _detectorTree.AddProxy(ref aabb, data, 0);
        }

        private void OnStatefulStorageAddRemove(PlayerResearchState research, string key, bool removed)
        {
            var trigger = research.Definition.Trigger.StateStorageProvider(key) as Ob_Trigger_Location;
            if (trigger == null)
                return;
            if (removed)
                UnwatchPlayer(research, key);
            else
            {
                EnsureDetector(research.Definition.Id, trigger);
                WatchPlayer(research, key);
            }
        }

        private readonly Dictionary<long, HashSet<ResearchStatefulKey>> _activatedEntries = new Dictionary<long, HashSet<ResearchStatefulKey>>();

        private void WatchPlayer(PlayerResearchState data, string key)
        {
            var player = data.Player.Player;

            HashSet<ResearchStatefulKey> activated;
            var newlyWatched = !_activatedEntries.TryGetValue(player.IdentityId, out activated);
            if (newlyWatched)
                activated = _activatedEntries[player.IdentityId] = new HashSet<ResearchStatefulKey>();
            activated.Add(new ResearchStatefulKey(data.Definition.Id, key));

            if (!newlyWatched) return;
            _log.Debug($"Beginning to watch player {player.DisplayName}");
            player.Controller.ControlledEntityChanged += OnControlledEntityChanged;
            if (player.Controller.ControlledEntity != null)
                WatchEntity(player.Controller.ControlledEntity);
        }

        private void UnwatchPlayer(PlayerResearchState data, string key)
        {
            var player = data.Player.Player;

            HashSet<ResearchStatefulKey> activated;
            if (!_activatedEntries.TryGetValue(player.IdentityId, out activated))
                return;
            activated.Remove(new ResearchStatefulKey(data.Definition.Id, key));
            var doneWatching = activated.Count == 0;
            if (!doneWatching)
                return;
            _log.Debug($"Done watching player {player.DisplayName}");
            _activatedEntries.Remove(player.IdentityId);
            player.Controller.ControlledEntityChanged -= OnControlledEntityChanged;
            if (player.Controller.ControlledEntity != null)
                UnwatchEntity(player.Controller.ControlledEntity);
        }

        private void OnControlledEntityChanged(IMyControllableEntity old, IMyControllableEntity @new)
        {
            if (old != null)
                UnwatchEntity(old);
            if (@new != null)
                WatchEntity(@new);
        }

        private void WatchEntity(IMyControllableEntity entity)
        {
            var container = (entity as IMyEntity)?.Components;
            if (container == null)
                return;
            var ent = entity.Entity;
            if (ent == null)
                return;
            _log.Debug($"Beginning to watch entity {ent.DisplayName}");
            ent.PositionComp.OnPositionChanged += OnPositionChanged;
            OnPositionChanged(ent.PositionComp);

            var p = ent;
            while (p != null)
            {
                p.NeedsWorldMatrix = true;
                p = p.Parent;
            }
        }

        private void UnwatchEntity(IMyControllableEntity entity)
        {
            var container = (entity as IMyEntity)?.Components;
            if (container == null)
                return;
            var ent = entity.Entity;
            if (ent == null)
                return;
            _log.Debug($"Done watching entity {ent.DisplayName}");
            ent.PositionComp.OnPositionChanged -= OnPositionChanged;
        }

        private void OnPositionChanged(MyPositionComponentBase obj)
        {
            var ent = obj.Container?.Entity;
            if (ent == null)
                return;
            var player = MyAPIGateway.Players.GetPlayerControllingEntity(ent);
            if (player == null)
                return;
            HashSet<ResearchStatefulKey> activated;
            if (!_activatedEntries.TryGetValue(player.IdentityId, out activated) || activated.Count == 0)
                return;
            var worldBox = obj.WorldAABB;
            _detectorTree.OverlapAllBoundingBox(ref worldBox, _matchingDetectors);
            if (_matchingDetectors.Count == 0)
                return;
            var playerState = Manager.GetOrCreatePlayer(player);
            foreach (var k in _matchingDetectors)
                if (worldBox.Intersects(k.Detector) && activated.Contains(k.Key))
                {
                    var prs = playerState.PlayerResearchState(k.Key.ResearchKey);
                    prs?.UpdateStatefulStorage(k.Key.StatefulKey, true);
                }
        }

        private readonly List<DetectorData> _matchingDetectors = new List<DetectorData>();
    }
}