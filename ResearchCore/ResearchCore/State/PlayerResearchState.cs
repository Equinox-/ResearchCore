using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Equinox.ResearchCore.Definition;
using Equinox.ResearchCore.State.ObjectBuilders;
using Equinox.ResearchCore.Utils;
using Equinox.Utils.Logging;
using VRage.Game.ModAPI;

namespace Equinox.ResearchCore.State
{
    public class PlayerResearchState
    {
        public readonly PlayerState Player;
        public readonly ResearchDefinition Definition;

        public PlayerResearchState(PlayerState player, ResearchDefinition def)
        {
            Player = player;
            Definition = def;
        }

        public void Init(Ob_PlayerResearchState info)
        {
            Utilities.Assert(Definition.Id == info.Id, "Id mismatch");
            _statefulStorage.Clear();
            State = info.State;
            if (info.SetStates != null)
                foreach (var k in info.SetStates)
                    CreateStatefulStorage(k, true);
            if (info.UnsetStates != null)
                foreach (var k in info.UnsetStates)
                    CreateStatefulStorage(k, false);
        }

        public Ob_PlayerResearchState GetObjectBuilder()
        {
            return new Ob_PlayerResearchState()
            {
                Id = Definition.Id,
                State = _state,
                SetStates = _statefulStorage.Where(x => x.Value).Select(x => x.Key).ToArray(),
                UnsetStates = _statefulStorage.Where(x => !x.Value).Select(x => x.Key).ToArray()
            };
        }

        private ResearchState _state = ResearchState.NotStarted;

        public ResearchState State
        {
            get { return _state; }
            set
            {
                if (_state == value)
                    return;
                var old = _state;
                _state = value;

                var dpin = 0;
                if (_state == ResearchState.Completed)
                    dpin = 1;
                else if (old == ResearchState.Completed)
                    dpin = -1;
                Player.Manager.RaisePlayerResearchStateChanged(this, old, _state);
                if (dpin != 0)
                    foreach (var r in Definition.Unlocks)
                        Player.ChangePin(r, dpin);
            }
        }

        public IReadOnlyCollection<string> StatefulKeys => _statefulStorage.Keys;

        public bool? StatefulStorage(string key)
        {
            bool res;
            return _statefulStorage.TryGetValue(key, out res) ? (bool?) res : null;
        }

        public void CreateStatefulStorage(string key, bool initVal = false)
        {
            _statefulStorage.Add(key, false);
            Player.Manager.RaisePlayerResearchStatefulStorageAddRemove(this, key, false);
            if (initVal)
                UpdateStatefulStorage(key, true);
        }

        public void RemoveStatefulStorage(string key)
        {
            if (_statefulStorage.Remove(key))
                Player.Manager.RaisePlayerResearchStatefulStorageAddRemove(this, key, true);
        }

        private readonly Dictionary<string, bool> _statefulStorage = new Dictionary<string, bool>();

        public void UpdateStatefulStorage(string key, bool val)
        {
            bool old;
            if (!_statefulStorage.TryGetValue(key, out old))
            {
                CreateStatefulStorage(key, val);
                return;
            }
            if (old == val)
                return;
            _statefulStorage[key] = val;
            Player.Manager.RaisePlayerResearchStatefulStorageUpdate(this, key, old, val);
        }
    }
}