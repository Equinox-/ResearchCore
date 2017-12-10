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
            if (other==null) return false;
            if (ReferenceEquals(other, this)) return true;
            return string.Equals(Type, other.Type) && string.Equals(Subtype, other.Subtype);
        }

        public override bool Equals(object other)
        {
            if (other == null) return false;
            if (ReferenceEquals(other, this)) return true;
            return other.GetType() == GetType() && Equals((NullableDefinitionId) other);
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