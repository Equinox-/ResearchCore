using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace Equinox.ResearchCore.Definition.ObjectBuilders.Triggers
{
    public abstract class Ob_Trigger_Composite : Ob_Trigger
    {
        [XmlElement("All", typeof(Ob_Trigger_All))]
        [XmlElement("Any", typeof(Ob_Trigger_Any))]
        [XmlElement("HasItem", typeof(Ob_Trigger_HasItem))]
        [XmlElement("Interact", typeof(Ob_Trigger_Interact))]
        [XmlElement("Research", typeof(Ob_Trigger_ResearchState))]
        [XmlElement("Unlocked", typeof(Ob_Trigger_Unlocked))]
        public List<Ob_Trigger> Elements;

        protected override string ComputeStorageKey()
        {
            return null;
        }

        public Ob_Trigger Simplify()
        {
            if (Elements == null || Elements.Count == 0)
                return null;
            var selectedSimple = Elements.Select(res =>
            {
                var comp = res as Ob_Trigger_Composite;
                return comp?.Simplify() ?? res;
            }).Where(x => x != null).ToArray();
            if (selectedSimple.Length == 0)
                return null;
            return selectedSimple.Length == 1 ? selectedSimple[0] : this;
        }
    }

    public class Ob_Trigger_Any : Ob_Trigger_Composite
    {
        public override string ToString()
        {
            return "(" + string.Join(" || ", Elements) + ")";
        }
    }

    public class Ob_Trigger_All : Ob_Trigger_Composite
    {
        public override string ToString()
        {
            return "(" + string.Join(" && ", Elements) + ")";
        }
    }
}