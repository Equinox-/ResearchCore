using Equinox.Utils.Logging;

namespace Equinox.ResearchCore.Modules
{
    public abstract class ModuleBase
    {
        public ResearchManager Manager { get; }
        public ILogging Logger { get; }

        protected ModuleBase(ResearchManager mgr)
        {
            Manager = mgr;
            Logger = mgr.Core.Logger.CreateProxy(GetType().Name);
        }

        public virtual void Update()
        {
        }

        public abstract void Attach();

        public abstract void Detach();
    }
}