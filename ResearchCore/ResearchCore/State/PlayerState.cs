using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Equinox.ResearchCore.Definition;
using Equinox.ResearchCore.State.ObjectBuilders;
using Equinox.ResearchCore.Utils;
using Equinox.Utils.Logging;
using Sandbox.Definitions;
using Sandbox.Game;
using VRage.Game;
using VRage.Game.ModAPI;

namespace Equinox.ResearchCore.State
{
    public class PlayerState
    {
        public ResearchManager Manager { get; }
        public IMyPlayer Player { get; }

        private readonly Dictionary<string, PlayerResearchState> _researchStates =
            new Dictionary<string, PlayerResearchState>();

        private readonly Dictionary<MyDefinitionId, int> _unlockPins =
            new Dictionary<MyDefinitionId, int>(MyDefinitionId.Comparer);

        public IReadOnlyCollection<PlayerResearchState> ResearchStates
        {
            get { return _researchStates.Values; }
        }

        public PlayerResearchState PlayerResearchState(string id, bool create = false)
        {
            PlayerResearchState res;
            if (!_researchStates.TryGetValue(id, out res))
            {
                if (create)
                    _researchStates.Add(id, res = new PlayerResearchState(this, Manager.Definitions.ResearchById(id)));
                else
                    res = null;
            }
            return res;
        }

        public PlayerState(ResearchManager manager, IMyPlayer player, Ob_PlayerState info)
        {
            Manager = manager;
            Player = player;
            if (info?.States != null)
            {
                foreach (var state in info.States)
                {
                    var def = Manager.Definitions.ResearchById(state.Id);
                    if (def == null)
                        continue;
                    var ps = new PlayerResearchState(this, def);
                    _researchStates.Add(def.Id, ps);
                }
                foreach (var state in info.States)
                    if (_researchStates.ContainsKey(state.Id))
                        _researchStates[state.Id].Init(state);
            }
        }

        internal void ChangePin(MyDefinitionId def, int count)
        {
            int pval = _unlockPins.GetValueOrDefault(def, 0);
            int cval = pval + count;
            if (cval <= 0)
                _unlockPins.Remove(def);
            else
                _unlockPins[def] = cval;
            if ((pval > 0) != (cval > 0))
                UnlockRefresh(def);
            Manager.RaisePlayerUnlockStateChanged(this, def, pval > 0, cval > 0);
        }

        public bool HasUnlockedAnything
        {
            get { return _unlockPins.Any(x => x.Value > 0); }
        }

        public bool HasUnlocked(MyDefinitionId id)
        {
            if (!Manager.Definitions.Unlocks.Contains(id))
                return true;
            return _unlockPins.GetValueOrDefault(id, 0) > 0;
        }

        public ResearchState ResearchState(string id)
        {
            return _researchStates.GetValueOrDefault(id)?.State ?? Definition.ResearchState.NotStarted;
        }

        public void UnlockRefresh(MyDefinitionId id)
        {
            if (MyDefinitionManager.Static.GetDefinitionAny(id) is MyCubeBlockDefinition)
            {
                if (HasUnlocked(id))
                    MyVisualScriptLogicProvider.PlayerResearchUnlock(Player.IdentityId, id);
                else
                    MyVisualScriptLogicProvider.PlayerResearchLock(Player.IdentityId, id);
            }
        }

        public Ob_PlayerState GetObjectBuilder()
        {
            return new Ob_PlayerState()
            {
                SteamId = Player.SteamUserId,
                States = _researchStates.Values
                    .Where(x => x.State != Definition.ResearchState.NotStarted || x.StatefulKeys.Count > 0)
                    .Select(x => x.GetObjectBuilder()).ToArray()
            };
        }
    }
}