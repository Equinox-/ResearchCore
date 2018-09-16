using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Equinox.ResearchCore.Definition.ObjectBuilders;
using Equinox.ResearchCore.Definition.ObjectBuilders.Triggers;
using Equinox.ResearchCore.Utils;
using Equinox.Utils.Logging;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game;

namespace Equinox.ResearchCore.Definition
{
    public class ResearchDefinition
    {
        public readonly string Id;
        public readonly IReadOnlyCollection<MyDefinitionId> Unlocks;
        public readonly IReadOnlyCollection<MyDefinitionId> UnlocksOriginal;
        public readonly ResearchTrigger Trigger;
        public readonly string DisplayName;
        public readonly string Description;
        public readonly string CompletionMessage;
        public readonly bool UpdatesAsNotifications;
        public readonly bool ShowCompletionWindow;
        public readonly bool AutoStart;

        private IEnumerable<MyDefinitionId> SelectApplied(MyDefinitionId x)
        {
            var def = MyDefinitionManager.Static.GetDefinitionAny(x);
            var bpClass = def as MyBlueprintClassDefinition;
            return bpClass?.Select(y => y.Id) ?? new[] {x};
        }

        public ResearchDefinition(Ob_ResearchDefinition ob)
        {
            Id = ob.Id;
            UnlocksOriginal = new HashSet<MyDefinitionId>(
                ob.Unlocks?.Select(x => x.Id).Where(Utilities.ValidateDefinition) ??
                Enumerable.Empty<MyDefinitionId>(), MyDefinitionId.Comparer);
            Unlocks = new HashSet<MyDefinitionId>(UnlocksOriginal.Concat(UnlocksOriginal
                .Where(Utilities.ValidateDefinition)
                .SelectMany(SelectApplied)), MyDefinitionId.Comparer);
            Trigger = new ResearchTrigger(ob.Trigger);
            DisplayName = string.IsNullOrWhiteSpace(ob.DisplayName) ? ob.Id : ob.DisplayName;
            Description = string.IsNullOrWhiteSpace(ob.Description) ? null : ob.Description;
            AutoStart = ob.AutoStart ?? false;
            CompletionMessage = ob.CompletionMessage;
            UpdatesAsNotifications = ob.UpdatesAsNotifications ?? false;
            ShowCompletionWindow = ob.ShowCompletionWindow ?? true;
        }

        public Ob_ResearchDefinition GetObjectBuilder()
        {
            Ob_Trigger trigger = Trigger.GetObjectBuilder();
            if (!(trigger is Ob_Trigger_All))
                trigger = new Ob_Trigger_All() {Elements = new List<Ob_Trigger> {trigger}};
            return new Ob_ResearchDefinition()
            {
                Id = Id,
                Unlocks = UnlocksOriginal.Select(x => new NullableDefinitionId() {Id = x}).ToList(),
                AutoStart = AutoStart,
                Description = Description,
                DisplayName = DisplayName,
                CompletionMessage = CompletionMessage,
                UpdatesAsNotifications = UpdatesAsNotifications,
                ShowCompletionWindow = ShowCompletionWindow,
                Trigger = (Ob_Trigger_All) trigger
            };
        }
    }
}