using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;

namespace Equinox.ResearchCore.Definition.ObjectBuilders.Triggers
{
    public class Ob_Trigger_ResearchState : Ob_Trigger
    {
        [XmlAttribute("Id")]
        public string Id;

        [XmlAttribute("State")]
        public ResearchState RequiredState;

        protected override string ComputeStorageKey()
        {
            return null;
        }
    }
}