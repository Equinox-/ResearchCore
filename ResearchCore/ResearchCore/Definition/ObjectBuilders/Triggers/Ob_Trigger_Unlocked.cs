using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;
using Equinox.ResearchCore.Utils;
using VRage.Game;

namespace Equinox.ResearchCore.Definition.ObjectBuilders.Triggers
{
    public class Ob_Trigger_Unlocked : Ob_Trigger
    {
        [XmlAttribute("Type")]
        public string Type;

        [XmlAttribute("Subtype")]
        public string Subtype;

        [XmlIgnore]
        public MyDefinitionId DefinitionId
        {
            get { return Utilities.Convert(Type, Subtype); }
            set { Utilities.Convert(value, out Type, out Subtype); }
        }

        protected override string ComputeStorageKey()
        {
            return null;
        }
    }
}
