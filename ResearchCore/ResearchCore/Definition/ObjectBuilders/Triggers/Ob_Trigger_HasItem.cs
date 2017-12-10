using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Xml.Serialization;
using Equinox.ResearchCore.Utils;
using ProtoBuf;
using VRage.Game;
using VRage.ObjectBuilders;
using VRage.Serialization;

namespace Equinox.ResearchCore.Definition.ObjectBuilders.Triggers
{
    public class Ob_Trigger_HasItem : Ob_Trigger
    {
        [XmlAttribute("Type")]
        public string Type;

        [XmlAttribute("Subtype")]
        public string Subtype;

        [XmlAttribute("Count"), DefaultValue(1d)]
        public double Count = 1d;

        [XmlAttribute("Consume"), DefaultValue(false)]
        public bool Consume = false;

        protected override string ComputeStorageKey()
        {
            return $"HasItem/{Type ?? "null"}/{Subtype ?? "null"}/{Count}" + (Consume ? "/Consume" : "");
        }

        [XmlIgnore]
        public MyDefinitionId DefinitionId
        {
            get { return Utilities.Convert(Type, Subtype); }
            set { Utilities.Convert(value, out Type, out Subtype); }
        }
    }
}