using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Equinox.ResearchCore.State;
using Equinox.ResearchCore.Utils;
using Equinox.Utils;
using Equinox.Utils.Logging;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace Equinox.ResearchCore.Modules
{
    public class BlueprintProhibitor : ModuleBase
    {
        public BlueprintProhibitor(ResearchManager mgr) : base(mgr)
        {
        }

        public override void Attach()
        {
            if (!MyAPIGateway.Session.IsServerDecider())
                return;
            MyAPIGateway.Entities.OnEntityAdd += OnEntityAdd;
            MyAPIGateway.Entities.OnEntityRemove += OnEntityRemove;
            MyAPIGateway.Entities.GetEntities(null, (x) =>
            {
                OnEntityAdd(x);
                return false;
            });
        }

        public override void Detach()
        {
            if (!MyAPIGateway.Session.IsServerDecider())
                return;
            MyAPIGateway.Entities.OnEntityAdd -= OnEntityAdd;
            MyAPIGateway.Entities.OnEntityRemove -= OnEntityRemove;
            MyAPIGateway.Entities.GetEntities(null, (x) =>
            {
                OnEntityRemove(x);
                return false;
            });
        }

        #region Listeners

        private void OnEntityAdd(IMyEntity e)
        {
            var grid = e as IMyCubeGrid;
            if (grid != null)
            {
                grid.OnBlockAdded += OnBlockAdd;
                grid.OnBlockRemoved += OnBlockRemove;
                grid.GetBlocks(null, (x) =>
                {
                    if (x.FatBlock != null)
                        OnEntityAdd(x.FatBlock);
                    return false;
                });
            }
            foreach (var c in e.Components.GetComponentTypes())
            {
                MyComponentBase b;
                if (e.Components.TryGet(c, out b) && b is MyEntityComponentBase)
                    OnComponentAdded(c, (MyEntityComponentBase) b);
            }
            e.Components.ComponentAdded += OnComponentAdded;
            e.Components.ComponentRemoved += OnComponentRemoved;
        }

        private void OnEntityRemove(IMyEntity e)
        {
            var grid = e as IMyCubeGrid;
            if (grid != null)
            {
                grid.OnBlockAdded -= OnBlockAdd;
                grid.OnBlockRemoved -= OnBlockRemove;
                grid.GetBlocks(null, (x) =>
                {
                    if (x.FatBlock != null)
                        OnEntityRemove(x.FatBlock);
                    return false;
                });
            }
            foreach (var c in e.Components.GetComponentTypes())
            {
                MyComponentBase b;
                if (e.Components.TryGet(c, out b) && b is MyEntityComponentBase)
                    OnComponentRemoved(c, (MyEntityComponentBase) b);
            }
            e.Components.ComponentAdded -= OnComponentAdded;
            e.Components.ComponentRemoved -= OnComponentRemoved;
        }

        private void OnBlockAdd(IMySlimBlock block)
        {
            if (block.FatBlock != null)
                OnEntityAdd(block.FatBlock);
        }

        private void OnBlockRemove(IMySlimBlock block)
        {
            if (block.FatBlock != null)
                OnEntityRemove(block.FatBlock);
        }

        private void OnComponentAdded(Type type, MyEntityComponentBase c)
        {
            (c as MyGameLogicComponent)?.GetAs<ProductionBlueprintProhibitor>()?.SetController(this);
        }

        private void OnComponentRemoved(Type type, MyEntityComponentBase c)
        {
            (c as MyGameLogicComponent)?.GetAs<ProductionBlueprintProhibitor>()?.SetController(null);
        }

        #endregion

        public void OnProductionBlockUpdate(ProductionBlueprintProhibitor prohibit)
        {
            var block = prohibit.Entity;
            var player = Manager.Core.Players.TryGetPlayerByIdentity(block.OwnerId);
            var owner = player != null ? Manager.GetOrCreatePlayer(player) : null;

            var changed = true;
            while (changed)
            {
                changed = false;
                var list = prohibit.QueueItems;
                for (var i = 0; i < list.Count; i++)
                    if (Manager.Definitions.Unlocks.Contains(list[i].BlueprintId))
                    {
                        if (owner != null && owner.HasUnlocked(list[i].BlueprintId))
                            continue;
                        var asm = prohibit.Entity as IMyAssembler;
                        if (i == 0 && asm != null)
                            asm.Repeating = false;
                        prohibit.Entity.RemoveQueueItem(i, list[i].Amount);
                        changed = true;
                    }
            }

            var refinery = prohibit.Entity as IMyRefinery;
            if (refinery != null)
                OnRefineryUpdate(prohibit, refinery, owner);
        }

        private void OnRefineryUpdate(ProductionBlueprintProhibitor prohibit, IMyRefinery refinery, PlayerState owner)
        {
            var refineryDef =
                MyDefinitionManager.Static.GetCubeBlockDefinition(refinery.BlockDefinition) as MyRefineryDefinition;
            if (refineryDef == null)
                return;
            var canBeLocked = false;
            foreach (var clazz in refineryDef.BlueprintClasses)
            {
                if (Manager.Definitions.Unlocks.Contains(clazz.Id))
                {
                    canBeLocked = true;
                    break;
                }
                foreach (var sub in clazz)
                    if (Manager.Definitions.Unlocks.Contains(sub.Id))
                    {
                        canBeLocked = true;
                        break;
                    }
                if (canBeLocked)
                    break;
            }
            if (!canBeLocked)
                return;

            var isLockedForOwner = owner == null || !owner.HasUnlockedAnything;
            foreach (var clazz in refineryDef.BlueprintClasses)
            {
                if (isLockedForOwner)
                    break;
                isLockedForOwner |= !owner.HasUnlocked(clazz.Id);
            }
            foreach (var clazz in refineryDef.BlueprintClasses)
            {
                if (isLockedForOwner)
                    break;
                foreach (var sub in clazz)
                {
                    if (isLockedForOwner)
                        break;
                    isLockedForOwner |= !owner.HasUnlocked(sub.Id);
                }
            }

            if (!isLockedForOwner)
            {
                if (_wasRefineryEnabled.Remove(refinery.EntityId))
                    refinery.Enabled = true;
                return;
            }


            // Move first processable slot to start of inventory.

            // Disgusting allocation.  Can we do better?  Track inventory changes?  Something similar?
            var lockEverything = false;
            if (owner != null)
            {
                var items = refinery.InputInventory.GetItems();
                int firstUnlockedSlot;
                for (firstUnlockedSlot = 0; firstUnlockedSlot < items.Count; firstUnlockedSlot++)
                {
                    MyBlueprintDefinitionBase blueprint = null;
                    var id = new MyDefinitionId(items[firstUnlockedSlot].Content.TypeId,
                        items[firstUnlockedSlot].Content.SubtypeId);
                    foreach (var clazz in refineryDef.BlueprintClasses)
                    {
                        blueprint = BlueprintLookup(clazz, id);
                        if (blueprint != null)
                            break;
                    }
                    if (blueprint != null && owner.HasUnlocked(blueprint.Id))
                        break;
                }
                if (firstUnlockedSlot >= items.Count)
                    lockEverything = true;
                else if (firstUnlockedSlot > 0)
                {
                    var amount = items[firstUnlockedSlot].Amount;
                    var content = items[firstUnlockedSlot].Content as MyObjectBuilder_PhysicalObject;
                    if (content == null)
                        lockEverything = true;
                    else
                    {
                        refinery.InputInventory.RemoveItemsAt(firstUnlockedSlot);
                        refinery.InputInventory.AddItems(amount, content, 0);
                    }
                }
            }
            else
                lockEverything = true;

            if (lockEverything)
            {
                if (refinery.Enabled)
                {
                    _wasRefineryEnabled.Add(refinery.EntityId);
                    refinery.Enabled = false;
                }
            }
            else if (_wasRefineryEnabled.Remove(refinery.EntityId))
                refinery.Enabled = true;
        }

        private readonly HashSet<long> _wasRefineryEnabled = new HashSet<long>();

        private readonly Dictionary<MyBlueprintClassDefinition, Dictionary<MyDefinitionId, MyBlueprintDefinitionBase>>
            _classBlueprintLookup =
                new Dictionary<MyBlueprintClassDefinition, Dictionary<MyDefinitionId, MyBlueprintDefinitionBase>>();

        private MyBlueprintDefinitionBase BlueprintLookup(MyBlueprintClassDefinition clazz, MyDefinitionId id)
        {
            Dictionary<MyDefinitionId, MyBlueprintDefinitionBase> tmp;
            if (!_classBlueprintLookup.TryGetValue(clazz, out tmp))
            {
                tmp = new Dictionary<MyDefinitionId, MyBlueprintDefinitionBase>(MyDefinitionId.Comparer);
                foreach (var entry in clazz)
                foreach (var preq in entry.Prerequisites)
                {
                    if (!tmp.ContainsKey(preq.Id))
                        tmp.Add(preq.Id, entry);
                }
                _classBlueprintLookup.Add(clazz, tmp);
            }
            return tmp.GetValueOrDefault(id);
        }
    }
}