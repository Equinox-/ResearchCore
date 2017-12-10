using System;
using System.Collections.Generic;
using System.Text;
using Equinox.Utils.Logging;
using Sandbox.Definitions;
using VRage.Game;
using VRage.Network;
using VRage.ObjectBuilders;

namespace Equinox.ResearchCore.Utils
{
    public static class Utilities
    {
        public static void Assert(bool @true, string message)
        {
            if (!@true)
                throw new Exception(message);
        }

        private static readonly Func<MyDefinitionManager, MyDefinitionId, MyDefinitionBase>[] _defGetters =
        {
            (a, b) => a.GetDefinition(b),
            (a, b) => a.GetBlueprintDefinition(b),
            (a, b) => b.TypeId == typeof(MyObjectBuilder_BlueprintClassDefinition)
                ? a.GetBlueprintClass(b.SubtypeName)
                : null
        };

        public static MyDefinitionBase GetDefinitionAny(this MyDefinitionManager mgr, MyDefinitionId id)
        {
            foreach (var getter in _defGetters)
                try
                {
                    var res = getter(mgr, id);
                    if (res != null)
                        return res;
                }
                catch
                {
                    // ignored
                }
            return null;
        }

        public static bool ValidateDefinition<T>(MyDefinitionId id) where T : MyDefinitionBase
        {
            if (!ResearchCore.SafeDefinitionLoading)
                return true;
            try
            {
                var def = MyDefinitionManager.Static.GetDefinitionAny(id);
                if (def is T)
                    return true;
                if (def != null)
                {
                    ResearchCore.LoggerStatic.Error($"Definition {id} is type {def.GetType()}, expected {typeof(T)}");
                    return false;
                }
            }
            catch
            {
                // ignored
            }
            ResearchCore.LoggerStatic.Error($"No definition for {id}");
            return false;
        }

        public static bool ValidateDefinition(MyDefinitionId id)
        {
            if (!ResearchCore.SafeDefinitionLoading)
                return true;
            try
            {
                if (MyDefinitionManager.Static.GetDefinitionAny(id) != null)
                    return true;
            }
            catch
            {
                // ignored
            }
            ResearchCore.LoggerStatic.Error($"No definition for {id}");
            return false;
        }

        public static MyDefinitionId Convert(string type, string subtype)
        {
            try
            {
                return new MyDefinitionId(MyObjectBuilderType.ParseBackwardsCompatible(type), subtype);
            }
            catch
            {
                throw new Exception($"Failed to parse definition ID {type}/{subtype}");
            }
        }

        public static void Convert(MyDefinitionId value, out string type, out string subtype)
        {
            // ReSharper disable once ImpureMethodCallOnReadonlyValueField
            type = value.TypeId.ToString();
            subtype = value.SubtypeName;
        }
    }
}