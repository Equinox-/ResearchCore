using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Equinox.ResearchCore.Definition;

namespace Equinox.ResearchCore.State.ObjectBuilders
{
    public class Ob_PlayerResearchState
    {
        public string Id;
        public ResearchState State;
        [XmlElement("Set")]
        public string[] SetStates;
        [XmlElement("Unset")]
        public string[] UnsetStates;
    }
}
