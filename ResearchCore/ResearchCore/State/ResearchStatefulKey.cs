using System;

namespace Equinox.ResearchCore.State
{
    public struct ResearchStatefulKey : IEquatable<ResearchStatefulKey>
    {
        public readonly string ResearchKey, StatefulKey;

        public ResearchStatefulKey(string researchKey, string statefulKey)
        {
            ResearchKey = researchKey;
            StatefulKey = statefulKey;
        }

        public bool Equals(ResearchStatefulKey other)
        {
            return string.Equals(ResearchKey, other.ResearchKey) && string.Equals(StatefulKey, other.StatefulKey);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is ResearchStatefulKey && Equals((ResearchStatefulKey) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((ResearchKey != null ? ResearchKey.GetHashCode() : 0) * 397) ^ (StatefulKey != null ? StatefulKey.GetHashCode() : 0);
            }
        }
    }
}