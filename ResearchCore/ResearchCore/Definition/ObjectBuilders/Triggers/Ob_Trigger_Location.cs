using System.ComponentModel;
using System.Xml.Serialization;
using VRage;

namespace Equinox.ResearchCore.Definition.ObjectBuilders.Triggers
{
    public class Ob_Trigger_Location : Ob_Trigger
    {
        [XmlElement]
        public SerializableVector3D Position;

        [XmlElement]
        public float Radius;

        [XmlElement]
        public bool ObscureLocation;

        protected override string ComputeStorageKey()
        {
            return $"Location/{Position.X},{Position.Y},{Position.Z}/{Radius}";
        }
    }
}