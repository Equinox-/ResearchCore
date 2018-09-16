using System.Collections.Generic;
using System.ComponentModel;
using System.Xml.Serialization;
using Equinox.ResearchCore.Definition.ObjectBuilders.Triggers;
using Equinox.ResearchCore.Utils;
using VRage.Game;
using VRage.Network;

namespace Equinox.ResearchCore.Definition.ObjectBuilders
{
    public class Ob_ResearchDefinition
    {
        [XmlAttribute("Id")]
        public string Id;

        [DefaultValue(ListMergeStrategy.Add)]
        public ListMergeStrategy UnlockMergeStrategy = ListMergeStrategy.Add;

        [XmlElement("Unlock")]
        public List<NullableDefinitionId> Unlocks;

        [DefaultValue(LogicalMergeStrategy.And)]
        public LogicalMergeStrategy TriggerMergeStrategy = LogicalMergeStrategy.And;

        public Ob_Trigger_All Trigger;

        /// <summary>
        /// Auto start this research once all preqreqs (unlocks and research) are complete.
        /// </summary>
        [XmlIgnore]
        public bool? AutoStart;

        [DefaultValue(null)]
        public string DisplayName;

        [DefaultValue(null)]
        public string Description;

        [DefaultValue(null)]
        public string CompletionMessage;

        [XmlIgnore]
        public bool? UpdatesAsNotifications;

        [XmlIgnore]
        public bool? ShowCompletionWindow;
        
        [DefaultValue(null)]
        [XmlElement(nameof(AutoStart), IsNullable = true)]
        public string AutoStartSerial
        {
            get { return AutoStart?.ToString(); }
            set
            {
                if (value == null) AutoStart = null;
                else AutoStart = bool.Parse(value);
            }
        }
        
        [DefaultValue(null)]
        [XmlElement(nameof(UpdatesAsNotifications), IsNullable = true)]
        public string UpdatesAsNotificationsSerial
        {
            get { return UpdatesAsNotifications?.ToString(); }
            set
            {
                if (value == null) UpdatesAsNotifications = null;
                else UpdatesAsNotifications = bool.Parse(value);
            }
        }

        [DefaultValue(null)]
        [XmlElement(nameof(ShowCompletionWindow), IsNullable = true)]
        public string ShowCompletionWindowSerial
        {
            get { return ShowCompletionWindow?.ToString(); }
            set
            {
                if (value == null) ShowCompletionWindow = null;
                else ShowCompletionWindow = bool.Parse(value);
            }
        }
    }
}