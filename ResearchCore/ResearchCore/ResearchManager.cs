using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Equinox.ResearchCore.Definition;
using Equinox.ResearchCore.Definition.ObjectBuilders;
using Equinox.ResearchCore.Modules;
using Equinox.ResearchCore.Network;
using Equinox.ResearchCore.State;
using Equinox.ResearchCore.State.ObjectBuilders;
using Equinox.ResearchCore.Utils;
using Equinox.Utils;
using Equinox.Utils.Logging;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;

namespace Equinox.ResearchCore
{
    public class ResearchManager
    {
        private const ushort MOD_COMM_ID = 58720;

        private const string PLAYER_DATA_STORAGE = "players_state.xml";
        private const string AUX_RESEARCH_STORAGE = "aux_research.xml";


        public ResearchCore Core { get; }
        public ResearchDefinitionManager Definitions { get; }
        public ILogging Logger { get; }
        private readonly ModuleBase[] _modules;
        private readonly Dictionary<ulong, PlayerState> _playerStates = new Dictionary<ulong, PlayerState>();

        #region Events

        public delegate void DelPlayerResearchStateChanged(PlayerResearchState research, ResearchState old,
            ResearchState @new);

        public delegate void DelPlayerUnlockStateChanged(PlayerState player, MyDefinitionId unlock, bool wasUnlocked,
            bool nowUnlocked);

        public delegate void DelPlayerResearchStatefulStorageAddRemove(PlayerResearchState research, string key,
            bool removed);

        public delegate void DelPlayerResearchStatefulStorageUpdate(PlayerResearchState research, string key, bool old,
            bool @new);

        public delegate void DelPlayerAdded(PlayerState state);

        public event DelPlayerAdded PlayerAdded;
        public event DelPlayerResearchStateChanged PlayerResearchStateChanged;
        public event DelPlayerUnlockStateChanged PlayerUnlockStateChanged;
        public event DelPlayerResearchStatefulStorageAddRemove PlayerResearchStatefulStorageAddRemove;
        public event DelPlayerResearchStatefulStorageUpdate PlayerResearchStatefulStorageUpdate;

        #endregion

        public ResearchManager(ResearchCore core)
        {
            Core = core;
            Definitions = new ResearchDefinitionManager();
            Logger = core.Logger.CreateProxy(nameof(ResearchManager));
            _modules = new ModuleBase[]
            {
                new QuestLifetimeModule(this), new InventoryScanModule(this), new ResearchUiModule(this),
                new InteractionModuleClient(this), new InteractionModuleServer(this),
                new ReplicationModuleClient(this), new ReplicationModuleServer(this),
                new BlueprintProhibitor(this), new PlayerResearchStateControlServer(this),
            };
        }

        #region Load Data

        private void LoadPlayerData()
        {
            var playerDict = new Dictionary<ulong, IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(null, (x) =>
            {
                if (!x.IsBot)
                    playerDict.Add(x.SteamUserId, x);
                return false;
            });
            try
            {
                _playerStates.Clear();
                var states = Core.ReadXml<Ob_PlayerState[]>(PLAYER_DATA_STORAGE);
                if (states != null)
                {
                    foreach (var state in states)
                    {
                        IMyPlayer player;
                        if (!playerDict.TryGetValue(state.SteamId, out player))
                            continue;
                        var res = new PlayerState(this, player, state);
                        _playerStates.Add(player.SteamUserId, res);
                        RaisePlayerAdded(res);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error($"Load states error: \n{e}");
            }

            foreach (var p in playerDict.Values)
                if (!_playerStates.ContainsKey(p.SteamUserId))
                    AddPlayer(p);
        }

        private void LoadResearchData()
        {
            Definitions.BeginLoading();
            foreach (var kv in MyDefinitionManager.Static.GetPrefabDefinitions())
            {
                if (!kv.Key.StartsWith("EqResearch_"))
                    continue;
                foreach (var grid in kv.Value.CubeGrids)
                foreach (var block in grid.CubeBlocks.OfType<MyObjectBuilder_MyProgrammableBlock>())
                {
                    try
                    {
                        Ob_ResearchDefinition[] defs;
                        try
                        {
                            try
                            {
                                defs = MyAPIGateway.Utilities.SerializeFromXML<Ob_ResearchDefinition[]>(block.Program);
                            }
                            catch
                            {
                                defs = new[] {MyAPIGateway.Utilities.SerializeFromXML<Ob_ResearchDefinition>(block.Program)};
                            }
                        }
                        catch
                        {
                            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(block.Program));
                            try
                            {
                                defs = MyAPIGateway.Utilities.SerializeFromXML<Ob_ResearchDefinition[]>(raw);
                            }
                            catch
                            {
                                defs = new[] {MyAPIGateway.Utilities.SerializeFromXML<Ob_ResearchDefinition>(raw)};
                            }
                        }

                        foreach (var k in defs)
                            Definitions.Load(k);
                    }
                    catch (Exception e)
                    {
                        Logger.Error(
                            $"Load definition in {kv.Key}/{grid.DisplayName}/{block.CustomName} error: \n{e}");
                    }
                }
            }

            {
                var aux = Core.ReadXml<Ob_ResearchDefinition[]>(AUX_RESEARCH_STORAGE);
                if (aux != null)
                    foreach (var k in aux)
                        Definitions.Load(k);
            }
            Definitions.FinishLoading();
        }

        #endregion

        #region Save Data

        public void SaveData()
        {
            if (!MyAPIGateway.Session.IsServerDecider())
                return;
            Core.WriteXml(PLAYER_DATA_STORAGE, _playerStates.Values.Select(x => x.GetObjectBuilder()).ToArray());
        }

        #endregion

        #region Attach

        public void Attach()
        {
            Logger.Info("Research manager attached");
            Core.Players.PlayerJoined += AddPlayer;
            PlayerResearchStateChanged += OnPlayerResearchStateChanged;
            foreach (var module in _modules)
                module.Attach();

            MyAPIGateway.Multiplayer?.RegisterMessageHandler(MOD_COMM_ID, NetworkMessageHandler);

            if (MyAPIGateway.Session.IsServerDecider())
            {
                SetUiHidden(true);
                LoadResearchData();
                LoadPlayerData();
                LockAllResearch();
                foreach (var player in _playerStates.Values)
                foreach (var id in Definitions.Unlocks)
                    player.UnlockRefresh(id);
                NetworkMessageRecieved += ProcessDefinitionRequest;
                SetUiHidden(false);
            }
            else
            {
                NetworkMessageRecieved += ProcessDefinitionValue;
                SendMessageToServer(new ResearchDefinitionRequestMessage());
            }
        }

        private void LockAllResearch()
        {
            MyVisualScriptLogicProvider.ResearchListClear();
            MyVisualScriptLogicProvider.PlayerResearchClearAll();
            foreach (var k in Definitions.Unlocks)
                if (MyDefinitionManager.Static.GetDefinitionAny(k) is MyCubeBlockDefinition)
                    MyVisualScriptLogicProvider.ResearchListAddItem(k);
        }

        private void OnPlayerResearchStateChanged(PlayerResearchState research, ResearchState old, ResearchState @new)
        {
            Logger.Debug($"{research.Player.Player.DisplayName} / {research.Definition.Id} {old} -> {@new}");
        }

        #endregion

        public void Update()
        {
            foreach (var module in _modules)
                module.Update();
        }

        private void SetUiHidden(bool b)
        {
            foreach (var module in _modules)
            {
                var k = module as ResearchUiModule;
                if (k != null)
                    k.HideMessages = b;
            }
        }

        #region Detach

        public void Detach()
        {
            for (var i = _modules.Length - 1; i >= 0; i--)
                _modules[i].Detach();
            PlayerResearchStateChanged -= OnPlayerResearchStateChanged;
            Core.Players.PlayerJoined -= AddPlayer;

            MyAPIGateway.Multiplayer?.UnregisterMessageHandler(MOD_COMM_ID, NetworkMessageHandler);

            if (MyAPIGateway.Session.IsServerDecider())
                NetworkMessageRecieved -= ProcessDefinitionRequest;
            else
                NetworkMessageRecieved -= ProcessDefinitionValue;
            Logger.Info("Research manager detached");
        }

        #endregion

        #region Definition Communication

        private void ProcessDefinitionValue(ulong sender, IMsg msg)
        {
            var value = msg as ResearchDefinitionValueMessage;
            if (value == null)
                return;
            SetUiHidden(true);
            Definitions.BeginLoading();
            foreach (var o in value.Definitions)
                Definitions.Load(o);
            Definitions.FinishLoading();
            if (value.ResearchStates != null && value.ResearchIds != null &&
                MyAPIGateway.Session.LocalHumanPlayer != null)
            {
                var owner = GetOrCreatePlayer(MyAPIGateway.Session.LocalHumanPlayer);
                for (var i = 0; i < value.ResearchIds.Length; i++)
                    owner.PlayerResearchState(value.ResearchIds[i], true).State = value.ResearchStates[i];
            }

            SetUiHidden(false);
        }

        private void ProcessDefinitionRequest(ulong sender, IMsg msg)
        {
            if (msg is ResearchDefinitionRequestMessage)
            {
                var response = new ResearchDefinitionValueMessage();
                foreach (var def in Definitions.Research)
                    response.Definitions.Add(def.GetObjectBuilder());
                var player = Core.Players.TryGetPlayerBySteamId(sender);
                var owner = player == null ? null : _playerStates.GetValueOrDefault(player.SteamUserId);
                if (owner != null)
                {
                    response.ResearchIds = new string[owner.ResearchStates.Count];
                    response.ResearchStates = new ResearchState[owner.ResearchStates.Count];
                    var i = 0;
                    foreach (var state in owner.ResearchStates)
                    {
                        response.ResearchStates[i] = state.State;
                        response.ResearchIds[i] = state.Definition.Id;
                        i++;
                    }
                }

                SendMessage(sender, response);
            }
        }

        #endregion

        public void AddPlayer(IMyPlayer player)
        {
            if (!MyAPIGateway.Session.IsServerDecider() && player != MyAPIGateway.Session.LocalHumanPlayer)
                return;
            if (_playerStates.ContainsKey(player.SteamUserId))
                return;
            var state = new PlayerState(this, player, new Ob_PlayerState()
            {
                SteamId = player.SteamUserId
            });
            _playerStates.Add(player.SteamUserId, state);
            RaisePlayerAdded(state);
            foreach (var id in Definitions.Unlocks)
                state.UnlockRefresh(id);
        }

        public void RemovePlayer(IMyPlayer player)
        {
            _playerStates.Remove(player.SteamUserId);
        }

        public PlayerState GetOrCreatePlayer(IMyPlayer player)
        {
            PlayerState state;
            if (_playerStates.TryGetValue(player.SteamUserId, out state))
                return state;
            AddPlayer(player);
            return _playerStates.GetValueOrDefault(player.SteamUserId);
        }

        #region Network

        public event Action<ulong, IMsg> NetworkMessageRecieved;

        private void NetworkMessageHandler(byte[] bytes)
        {
            var container = MyAPIGateway.Utilities.SerializeFromBinary<MsgContainer>(bytes);
            var msg = container.Message;
            Utilities.Assert(msg != null, "Message is null");
            if (msg != null)
            {
                var src = MyAPIGateway.Multiplayer == null || container.Sender == MyAPIGateway.Multiplayer.MyId
                    ? "local"
                    : (Core.Players.TryGetPlayerBySteamId(container.Sender)?.DisplayName ??
                       $"Steam64={container.Sender}");
                Logger.Debug($"Recieved {msg.GetType().Name} from {src}");
                NetworkMessageRecieved?.Invoke(container.Sender, msg);
            }
        }

        public void SendMessageToServer(IMsg msg)
        {
            SendMessage(MyAPIGateway.Multiplayer?.ServerId ?? 0, msg);
        }

        public void SendMessage(ulong endpoint, IMsg msg)
        {
            var target = MyAPIGateway.Multiplayer == null || endpoint == MyAPIGateway.Multiplayer.MyId
                ? "local"
                : (Core.Players.TryGetPlayerBySteamId(endpoint)?.DisplayName ?? $"Steam64={endpoint}");
            Logger.Debug($"Sending {msg.GetType().Name} to {target}");
            if (MyAPIGateway.Multiplayer == null || endpoint == MyAPIGateway.Multiplayer.MyId)
            {
                NetworkMessageRecieved?.Invoke(endpoint, msg);
                return;
            }

            Utilities.Assert(MyAPIGateway.Multiplayer.IsServer || endpoint == MyAPIGateway.Multiplayer.ServerId,
                "Endpoint must be server for clients, and a client for server");
            var data = MyAPIGateway.Utilities.SerializeToBinary(new MsgContainer
            {
                Sender = MyAPIGateway.Multiplayer.MyId,
                Message = msg
            });
            if (MyAPIGateway.Multiplayer.IsServer)
                MyAPIGateway.Multiplayer.SendMessageTo(MOD_COMM_ID, data, endpoint);
            else
                MyAPIGateway.Multiplayer.SendMessageToServer(MOD_COMM_ID, data);
        }

        #endregion

        #region Event Raising

        internal void RaisePlayerAdded(PlayerState state)
        {
            Logger.Debug($"event {nameof(RaisePlayerAdded)}({state.Player.DisplayName})");
            Core.Logger.Flush();
            PlayerAdded?.Invoke(state);
        }

        internal void RaisePlayerResearchStateChanged(PlayerResearchState research, ResearchState old,
            ResearchState @new)
        {
            Logger.Debug(
                $"event {nameof(RaisePlayerResearchStateChanged)}({research.Player.Player.DisplayName}/{research.Definition.Id}, {old}, {@new})");
            Core.Logger.Flush();
            PlayerResearchStateChanged?.Invoke(research, old, @new);
        }

        internal void RaisePlayerUnlockStateChanged(PlayerState player, MyDefinitionId unlock, bool wasUnlocked,
            bool nowUnlocked)
        {
            Logger.Debug(
                $"event {nameof(PlayerUnlockStateChanged)}({player.Player.DisplayName}, {unlock}, {wasUnlocked}, {nowUnlocked})");
            Core.Logger.Flush();
            PlayerUnlockStateChanged?.Invoke(player, unlock, wasUnlocked, nowUnlocked);
        }

        internal void RaisePlayerResearchStatefulStorageAddRemove(PlayerResearchState research, string key, bool remove)
        {
            Logger.Debug(
                $"event {nameof(RaisePlayerResearchStatefulStorageAddRemove)}({research.Player.Player.DisplayName}/{research.Definition.Id}, {key}, {remove})");
            Core.Logger.Flush();
            PlayerResearchStatefulStorageAddRemove?.Invoke(research, key, remove);
        }

        internal void RaisePlayerResearchStatefulStorageUpdate(PlayerResearchState research, string key, bool old,
            bool @new)
        {
            Logger.Debug(
                $"event {nameof(RaisePlayerResearchStatefulStorageUpdate)}({research.Player.Player.DisplayName}/{research.Definition.Id}, {key}, {old}, {@new})");
            Core.Logger.Flush();
            PlayerResearchStatefulStorageUpdate?.Invoke(research, key, old, @new);
        }

        #endregion
    }
}