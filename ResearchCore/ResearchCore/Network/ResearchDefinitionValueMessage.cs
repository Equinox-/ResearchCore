using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Equinox.ResearchCore.Definition;
using Equinox.ResearchCore.Definition.ObjectBuilders;
using Equinox.Utils.Logging;
using ProtoBuf;
using Sandbox.ModAPI;

namespace Equinox.ResearchCore.Network
{
    [ProtoContract]
    public class ResearchDefinitionValueMessage : IMsg
    {
        public readonly List<Ob_ResearchDefinition> Definitions = new List<Ob_ResearchDefinition>();

        [ProtoMember]
        public string[] ResearchIds;

        [ProtoMember]
        public ResearchState[] ResearchStates;

        [ProtoMember]
        public string Data
        {
            get { return MyAPIGateway.Utilities.SerializeToXML(Definitions); }
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                    Definitions.Clear();
                else
                {
                    try
                    {
                        Definitions.Clear();
                        Definitions.AddRange(
                            MyAPIGateway.Utilities
                                .SerializeFromXML<List<Ob_ResearchDefinition>>(value));
                    }
                    catch (Exception e)
                    {
                        ResearchCore.LoggerStatic?.Error("Failed to deserialize definition data: \n" + e + "\n\n" +
                                                         value);
                    }
                }
            }
        }
    }
}