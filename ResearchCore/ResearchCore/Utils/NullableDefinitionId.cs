using System;
using System.Xml.Serialization;
using VRage.Game;

namespace Equinox.ResearchCore.Utils
{
    public class NullableDefinitionId : IEquatable<NullableDefinitionId>
    {
        [XmlAttribute("Type")]
        public string Type;

        [XmlAttribute("Subtype")]
        public string Subtype;


        [XmlIgnore]
        public MyDefinitionId Id
        {
            get { return Utilities.Convert(Type, Subtype); }
            set { Utilities.Convert(value, out Type, out Subtype); }
        }

        public bool Equals(NullableDefinitionId other)
        {
            if (Equals(null, other)) return false;
            if (Equals(this, other)) return true;
            return string.Equals(Type, other.Type) && string.Equals(Subtype, other.Subtype);
        }

        public override bool Equals(object obj)
        {
            if (Equals(null, obj)) return false;
            if (Equals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((NullableDefinitionId) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                // ReSharper disable NonReadonlyMemberInGetHashCode
                return ((Type != null ? Type.GetHashCode() : 0) * 397) ^ (Subtype != null ? Subtype.GetHashCode() : 0);
                // ReSharper restore NonReadonlyMemberInGetHashCode
            }
        }
    }
}