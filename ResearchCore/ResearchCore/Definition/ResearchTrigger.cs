using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Equinox.ResearchCore.Definition.ObjectBuilders.Triggers;
using Equinox.ResearchCore.Utils;
using Equinox.Utils.Logging;
using Sandbox.Definitions;
using VRage.Game;

namespace Equinox.ResearchCore.Definition
{
    public class ResearchTrigger
    {
        private readonly Dictionary<string, Ob_Trigger> _stateStorageProviders =
            new Dictionary<string, Ob_Trigger>();

        private readonly HashSet<ResearchBranch> _branches = new HashSet<ResearchBranch>();

        public interface IResearchBranch
        {
            IReadOnlyDictionary<string, ResearchState> ResearchStates { get; }
            IReadOnlyCollection<MyDefinitionId> Unlocked { get; }
            IReadOnlyCollection<string> StatefulTriggers { get; }
        }

        private class ResearchBranch : IResearchBranch
        {
            public readonly Dictionary<string, ResearchState> ResearchStates = new Dictionary<string, ResearchState>();
            public readonly HashSet<MyDefinitionId> Unlocked = new HashSet<MyDefinitionId>(MyDefinitionId.Comparer);

            public readonly Dictionary<string, Ob_Trigger> Triggers =
                new Dictionary<string, Ob_Trigger>();

            public ResearchBranch Fork()
            {
                var res = new ResearchBranch();
                foreach (var kv in ResearchStates)
                    res.ResearchStates.Add(kv.Key, kv.Value);
                foreach (var k in Unlocked)
                    res.Unlocked.Add(k);
                foreach (var kv in Triggers)
                    res.Triggers.Add(kv.Key, kv.Value);
                return res;
            }

            IReadOnlyDictionary<string, ResearchState> IResearchBranch.ResearchStates
            {
                get { return ResearchStates; }
            }

            IReadOnlyCollection<MyDefinitionId> IResearchBranch.Unlocked
            {
                get { return Unlocked; }
            }

            IReadOnlyCollection<string> IResearchBranch.StatefulTriggers
            {
                get { return Triggers.Keys; }
            }

            public override string ToString()
            {
                var strStates = string.Join(", ", ResearchStates.Select(x => $"{x.Key}={x.Value}"));
                var strLocked = string.Join(", ", Unlocked);

                return $"States: {strStates}\nUnlocked: {strLocked}\nTrigger: ({string.Join(" && ", Triggers.Values)})";
            }
        }

        internal ResearchTrigger(Ob_Trigger trigger)
        {
            FlattenResearch(trigger, _branches, new ResearchBranch());
            var removed = true;
            var toRemove = new HashSet<ResearchBranch>();
            while (removed)
            {
                foreach (var a in _branches)
                    if (!toRemove.Contains(a))
                        foreach (var b in _branches)
                            if (a != b && (a.ResearchStates.Equals(b.ResearchStates) &&
                                           a.Triggers.Keys.Equals(b.Triggers.Keys) && a.Unlocked.Equals(b.Unlocked)))
                            {
                                toRemove.Add(b);
                            }
                removed = toRemove.Count > 0;
                foreach (var k in toRemove)
                    _branches.Remove(k);
            }
        }

        private void FlattenResearch(Ob_Trigger trigger, ISet<ResearchBranch> branches,
            ResearchBranch currentBranch)
        {
            if (trigger is Ob_Trigger_Composite)
                trigger = ((Ob_Trigger_Composite) trigger).Simplify();
            if (trigger == null)
                return;
            trigger.UpdateKey();
            var all = trigger as Ob_Trigger_All;
            var any = trigger as Ob_Trigger_Any;
            var research = trigger as Ob_Trigger_ResearchState;
            var unlock = trigger as Ob_Trigger_Unlocked;
            if (all != null)
            {
                foreach (var e in all.Elements.Distinct())
                    FlattenResearch(e, branches, currentBranch);
                return;
            }
            if (any != null)
            {
                foreach (var e in any.Elements.Distinct())
                    FlattenResearch(e, branches, currentBranch.Fork());
                return;
            }
            branches.Add(currentBranch);
            if (research != null)
            {
                ResearchState currentVal =
                    currentBranch.ResearchStates.GetValueOrDefault(research.Id, research.RequiredState);
                Utilities.Assert(currentVal == research.RequiredState,
                    $"Research state mismatch: {currentVal} and {research.RequiredState}");
                currentBranch.ResearchStates[research.Id] = research.RequiredState;
                return;
            }
            if (unlock != null)
            {
                currentBranch.Unlocked.Add(unlock.DefinitionId);
                return;
            }
            var hasItem = trigger as Ob_Trigger_HasItem;
            if (hasItem != null && !Utilities.ValidateDefinition<MyPhysicalItemDefinition>(hasItem.DefinitionId))
                return;
            var interact = trigger as Ob_Trigger_Interact;
            if (interact != null)
            {
                var valid = true;
                foreach (var k in interact.HandItem)
                    valid &= Utilities.ValidateDefinition<MyPhysicalItemDefinition>(k);
                foreach (var k in interact.BlockInteractTarget)
                    valid &= Utilities.ValidateDefinition<MyCubeBlockDefinition>(k);
                if (!valid)
                    return;
            }
            var key = trigger.StateStorageKey;
            Utilities.Assert(key != null, $"Trigger type {trigger.GetType().FullName} must have a state storage key");
            Ob_Trigger existingTrigger;
            if (!_stateStorageProviders.TryGetValue(key, out existingTrigger))
                _stateStorageProviders[key] = existingTrigger = trigger;
            currentBranch.Triggers[key] = existingTrigger;
        }

        public IReadOnlyCollection<IResearchBranch> Branches
        {
            get { return _branches; }
        }

        public IEnumerable<IResearchBranch> BranchesWithPrereqs(Func<string, ResearchState> researchStateFunc,
            Func<MyDefinitionId, bool> unlockStateFunc)
        {
            return _branches.Where(x => x.ResearchStates.All(id => (researchStateFunc(id.Key) & id.Value) != 0)
                                        && x.Unlocked.All(unlockStateFunc));
        }

        public Ob_Trigger StateStorageProvider(string key)
        {
            return _stateStorageProviders.GetValueOrDefault(key);
        }

        public Ob_Trigger GetObjectBuilder()
        {
            var any = new Ob_Trigger_Any() {Elements = new List<Ob_Trigger>(Branches.Count)};
            foreach (var branch in Branches)
            {
                var all = new Ob_Trigger_All()
                {
                    Elements = new List<Ob_Trigger>(branch.ResearchStates.Count + branch.Unlocked.Count +
                                                    branch.StatefulTriggers.Count)
                };
                foreach (var state in branch.ResearchStates)
                    all.Elements.Add(new Ob_Trigger_ResearchState() {Id = state.Key, RequiredState = state.Value});
                foreach (var unlock in branch.Unlocked)
                    all.Elements.Add(new Ob_Trigger_Unlocked() {DefinitionId = unlock});
                all.Elements.AddRange(branch.StatefulTriggers.Select(StateStorageProvider));
                any.Elements.Add(all);
            }
            return any.Simplify();
        }
    }
}