using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Equinox.ResearchCore.Definition.ObjectBuilders;
using Equinox.ResearchCore.Definition.ObjectBuilders.Triggers;
using Equinox.ResearchCore.Utils;
using VRage.Game;

namespace Equinox.ResearchCore.Definition
{
    public class ResearchDefinitionManager
    {
        private readonly Dictionary<MyDefinitionId, HashSet<ResearchDefinition>> _researchsForUnlock =
            new Dictionary<MyDefinitionId, HashSet<ResearchDefinition>>(MyDefinitionId.Comparer);

        private readonly List<ResearchDefinition> _autostartResearch = new List<ResearchDefinition>();

        private readonly Dictionary<string, ResearchDefinition> _researchForId =
            new Dictionary<string, ResearchDefinition>();

        private readonly Dictionary<MyDefinitionId, HashSet<ResearchDefinition>> _depResearchForUnlock =
            new Dictionary<MyDefinitionId, HashSet<ResearchDefinition>>();

        private readonly Dictionary<string, HashSet<ResearchDefinition>> _depResearchForResearch =
            new Dictionary<string, HashSet<ResearchDefinition>>();

        private Dictionary<string, DefinitionBuilder> _builders;

        /// <summary>
        /// Get researches unlocking the given definition
        /// </summary>
        /// <param name="id">unlockable definition ID</param>
        /// <returns>researches</returns>
        public IEnumerable<ResearchDefinition> ResearchUnlocking(MyDefinitionId id)
        {
            return _researchsForUnlock.GetValueOrDefault(id) ?? Enumerable.Empty<ResearchDefinition>();
        }

        /// <summary>
        /// Gets research depending on the unlockable definition
        /// </summary>
        /// <param name="id">unlockable definition ID</param>
        /// <returns>researches</returns>
        public IEnumerable<ResearchDefinition> ResearchDepending(MyDefinitionId id)
        {
            return _depResearchForUnlock.GetValueOrDefault(id) ?? Enumerable.Empty<ResearchDefinition>();
        }

        /// <summary>
        /// Gets research depending on the state of another research
        /// </summary>
        /// <param name="id">parent research ID</param>
        /// <returns>researches</returns>
        public IEnumerable<ResearchDefinition> ResearchDepending(string id)
        {
            return _depResearchForResearch.GetValueOrDefault(id) ?? Enumerable.Empty<ResearchDefinition>();
        }

        /// <summary>
        /// Research with the given ID, or null if none
        /// </summary>
        /// <param name="id">research ID</param>
        /// <returns>the research, or null</returns>
        public ResearchDefinition ResearchById(string id)
        {
            return _researchForId.GetValueOrDefault(id);
        }

        /// <summary>
        /// Everything that can be unlocked
        /// </summary>
        public IReadOnlyCollection<MyDefinitionId> Unlocks => _researchsForUnlock.Keys;

        /// <summary>
        /// Every research
        /// </summary>
        public IReadOnlyCollection<ResearchDefinition> Research => _researchForId.Values;

        /// <summary>
        /// Every research that <see cref="ResearchDefinition.AutoStart"/>s.
        /// </summary>
        public IReadOnlyCollection<ResearchDefinition> AutostartResearch => _autostartResearch;

        /// <summary>
        /// Begins loading research data and remove all currently loaded research
        /// </summary>
        public void BeginLoading()
        {
            _researchsForUnlock.Clear();
            _researchForId.Clear();
            _builders = new Dictionary<string, DefinitionBuilder>();
        }

        /// <summary>
        /// Loads one piece of research data
        /// </summary>
        /// <param name="ob">Data to load</param>
        public void Load(Ob_ResearchDefinition ob)
        {
            DefinitionBuilder builder;
            if (!_builders.TryGetValue(ob.Id, out builder))
                builder = _builders[ob.Id] = new DefinitionBuilder();
            builder.Add(ob);
        }

        /// <summary>
        /// Finish loading research data
        /// </summary>
        public void FinishLoading()
        {
            foreach (var k in _builders.Values)
            {
                var res = k.Build();
                _researchForId.Add(res.Id, res);
                if (res.AutoStart)
                    _autostartResearch.Add(res);
                foreach (var unlock in res.Unlocks)
                {
                    HashSet<ResearchDefinition> targets;
                    if (!_researchsForUnlock.TryGetValue(unlock, out targets))
                        _researchsForUnlock.Add(unlock, targets = new HashSet<ResearchDefinition>());
                    targets.Add(res);
                }
                foreach (var path in res.Trigger.Branches)
                {
                    foreach (var unlock in path.Unlocked)
                    {
                        HashSet<ResearchDefinition> targets;
                        if (!_depResearchForUnlock.TryGetValue(unlock, out targets))
                            _depResearchForUnlock.Add(unlock, targets = new HashSet<ResearchDefinition>());
                        targets.Add(res);
                    }
                    foreach (var parent in path.ResearchStates.Keys)
                    {
                        HashSet<ResearchDefinition> targets;
                        if (!_depResearchForResearch.TryGetValue(parent, out targets))
                            _depResearchForResearch.Add(parent, targets = new HashSet<ResearchDefinition>());
                        targets.Add(res);
                    }
                }
            }
            _builders = null;
        }


        private class DefinitionBuilder
        {
            private readonly Ob_ResearchDefinition _dest;

            public DefinitionBuilder()
            {
                _dest = new Ob_ResearchDefinition();
            }

            public void Add(Ob_ResearchDefinition ob)
            {
                if (_dest.Id == null)
                    _dest.Id = ob.Id;
                if (!string.IsNullOrWhiteSpace(ob.DisplayName))
                    _dest.DisplayName = ob.DisplayName;
                if (!string.IsNullOrWhiteSpace(ob.Description))
                    _dest.Description = ob.Description;
                if (_dest.Trigger?.Elements == null || _dest.Trigger.Simplify() == null)
                    _dest.Trigger = ob.Trigger;
                if (ob.AutoStart.HasValue)
                    _dest.AutoStart = ob.AutoStart;
                else if (ob.Trigger?.Simplify() != null)
                {
                    switch (_dest.TriggerMergeStrategy)
                    {
                        case LogicalMergeStrategy.Overwrite:
                            _dest.Trigger = ob.Trigger;
                            break;
                        case LogicalMergeStrategy.And:
                            _dest.Trigger.Elements.AddRange(ob.Trigger.Elements);
                            break;
                        case LogicalMergeStrategy.Any:
                            var anyDest = _dest.Trigger?.Simplify();
                            var anySrc = ob.Trigger.Simplify();
                            var anyDestAny = anyDest as Ob_Trigger_Any;
                            var anySrcAny = anySrc as Ob_Trigger_Any;
                            if (anyDestAny != null && anySrcAny != null)
                            {
                                anyDestAny.Elements.AddRange(anySrcAny.Elements);
                                break;
                            }
                            else if (anyDestAny != null)
                            {
                                anyDestAny.Elements.Add(anySrc);
                                break;
                            }
                            if (anySrcAny != null)
                            {
                                anySrcAny.Elements.Add(anyDest);
                            }
                            else
                            {
                                anySrcAny = new Ob_Trigger_Any
                                {
                                    Elements = new List<Ob_Trigger>
                                    {
                                        anyDest,
                                        anySrc
                                    }
                                };
                            }
                            _dest.Trigger = new Ob_Trigger_All
                            {
                                Elements = new List<Ob_Trigger> {anySrcAny}
                            };
                            break;
                        default:
                            throw new InvalidOperationException($"Enum {_dest.TriggerMergeStrategy} out of range");
                    }
                }
                if (_dest.Unlocks == null || _dest.Unlocks.Count == 0)
                    _dest.Unlocks = ob.Unlocks;
                else if (ob.Unlocks != null && ob.Unlocks.Count > 0)
                {
                    switch (ob.UnlockMergeStrategy)
                    {
                        case ListMergeStrategy.Overwrite:
                            _dest.Unlocks = ob.Unlocks;
                            break;
                        case ListMergeStrategy.Add:
                            foreach (var k in ob.Unlocks)
                                if (!_dest.Unlocks.Contains(k))
                                    _dest.Unlocks.Add(k);
                            break;
                        case ListMergeStrategy.Remove:
                            foreach (var k in ob.Unlocks)
                                _dest.Unlocks.Remove(k);
                            break;
                        default:
                            throw new InvalidOperationException($"Enum {_dest.UnlockMergeStrategy} out of range");
                    }
                }
            }

            public ResearchDefinition Build()
            {
                return new ResearchDefinition(_dest);
            }
        }
    }
}