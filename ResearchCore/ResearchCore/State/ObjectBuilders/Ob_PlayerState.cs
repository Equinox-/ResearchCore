using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace Equinox.ResearchCore.State.ObjectBuilders
{
    public class Ob_PlayerState
    {
        public ulong SteamId;

        [XmlElement("Resesarch")]
        public Ob_PlayerResearchState[] States;
    }
}
