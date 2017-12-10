using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Equinox.ResearchCore.Modules;
using Equinox.Utils.Logging;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.ModAPI;

namespace Equinox.ResearchCore.Utils
{
    public class ProductionBlueprintProhibitor : MyGameLogicComponent
    {
        public override string ComponentTypeDebugString => nameof(ProductionBlueprintProhibitor);

        public override void OnAddedToContainer()
        {
            base.OnAddedToContainer();
            NeedsUpdate |= MyEntityUpdateEnum.EACH_10TH_FRAME;
        }

        private BlueprintProhibitor _prohibitor;

        public void SetController(BlueprintProhibitor prohibitor)
        {
            _prohibitor = prohibitor;
        }

        public new IMyProductionBlock Entity
        {
            get { return (IMyProductionBlock) base.Entity; }
        }

        private readonly List<Sandbox.ModAPI.Ingame.MyProductionItem> _queueItems =
            new List<Sandbox.ModAPI.Ingame.MyProductionItem>();

        public List<Sandbox.ModAPI.Ingame.MyProductionItem> QueueItems
        {
            get
            {
                _queueItems.Clear();
                Entity?.GetQueue(_queueItems);
                return _queueItems;
            }
        }

        // This executes immediately before UpdateProduction occurs
        public override void UpdateBeforeSimulation10()
        {
            if (Entity == null)
                return;
            _prohibitor?.OnProductionBlockUpdate(this);
        }
    }

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Refinery), true)]
    public class RefineryBlueprintProhibitor : ProductionBlueprintProhibitor
    {
    }

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Assembler), true)]
    public class AssemblerBlueprintProhibitor : ProductionBlueprintProhibitor
    {
    }
}