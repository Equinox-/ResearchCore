using System;
using System.Collections.Generic;
using System.Text;

namespace Equinox.ResearchCore.Definition.ObjectBuilders.Triggers
{
    public abstract class Ob_Trigger
    {
        private bool _stateStorageKeyComputed = false;
        private string _stateStorageKeyCache = null;

        public string StateStorageKey
        {
            get
            {
                // ReSharper disable once InvertIf
                if (!_stateStorageKeyComputed)
                {
                    _stateStorageKeyCache = ComputeStorageKey();
                    _stateStorageKeyComputed = true;
                }
                return _stateStorageKeyCache;
            }
        }

        protected abstract string ComputeStorageKey();

        public void UpdateKey()
        {
            _stateStorageKeyComputed = false;
        }

        public override string ToString()
        {
            return StateStorageKey ?? base.ToString();
        }
    }
}