using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Equinox.ResearchCore.Definition;
using Equinox.ResearchCore.Definition.ObjectBuilders;
using Equinox.ResearchCore.State;
using Equinox.ResearchCore.State.ObjectBuilders;
using Equinox.Utils;
using Equinox.Utils.Logging;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI;

namespace Equinox.ResearchCore
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class ResearchCore : MySessionComponentBase
    {
        public static bool SafeDefinitionLoading = true;

        public PlayerCollection Players { get; private set; }
        public ResearchManager Manager { get; private set; }
        public CustomLogger Logger { get { return LoggerStatic;  } }
        private readonly Dictionary<IMyPlayer, PlayerState> _playerStates = new Dictionary<IMyPlayer, PlayerState>();

        private bool _init = false;

        private static CustomLogger _loggerStatic;

        public static CustomLogger LoggerStatic
        {
            get { return _loggerStatic ?? (_loggerStatic = new CustomLogger()); }
        }

        public override void UpdateAfterSimulation()
        {
            if (!_init)
                DoInit();
            Logger.UpdateAfterSimulation();
            Manager.Update();
        }

        private void DoInit()
        {
            _init = true;
            Players = new PlayerCollection();
            Manager = new ResearchManager(this);
            Manager.Attach();
        }
        
        public override void SaveData()
        {
            Manager.SaveData();
            Logger.Flush();
        }

        protected override void UnloadData()
        {
            Manager.Detach();
            Manager = null;
            Logger.Detach();
            _loggerStatic = null;
            Players.Dispose();
            Players = null;
            _init = false;
        }

        public T ReadXml<T>(string name) where T : class
        {
            try
            {
                if (!MyAPIGateway.Utilities.FileExistsInWorldStorage(name, typeof(ResearchCore)))
                    return null;
                string content;
                using (var reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(name, typeof(ResearchCore)))
                    content = reader.ReadToEnd();
                return MyAPIGateway.Utilities.SerializeFromXML<T>(content);
            }
            catch (Exception e)
            {
                Logger?.Error($"Read error on {name}: \n{e}");
                return null;
            }
        }

        public void WriteXml<T>(string name, T value)
        {
            try
            {
                var content = MyAPIGateway.Utilities.SerializeToXML(value);
                using (var writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(name, typeof(ResearchCore)))
                    writer.Write(content);
            }
            catch (Exception e)
            {
                Logger?.Error($"Write error on {name}: \n{e}");
            }
        }
    }
}