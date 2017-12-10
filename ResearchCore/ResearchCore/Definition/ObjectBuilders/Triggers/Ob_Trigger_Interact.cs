using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using Equinox.ResearchCore.Utils;
using Sandbox.Game;
using VRage.Game;
using VRage.ObjectBuilders;
using VRage.Utils;

namespace Equinox.ResearchCore.Definition.ObjectBuilders.Triggers
{
    public class Ob_Trigger_Interact : Ob_Trigger
    {
        public static readonly Guid InteractResearchStorageComponent =
            Guid.Parse("b4583514-48c4-47c1-94d8-3330cc1bd209");

        /// <summary>
        /// The HandItem id that is required to be held, or empty for any item;
        /// </summary>
        [XmlElement(nameof(HandItem))]
        public NullableDefinitionId[] HandItemSerial
        {
            get { return HandItem.Select(x => new NullableDefinitionId() {Id = x}).ToArray(); }
            set
            {
                HandItem.Clear();
                if (value != null)
                    foreach (var x in value)
                        HandItem.Add(x.Id);
            }
        }

        /// <summary>
        /// The target block(s) that must be interacted with
        /// </summary>
        [XmlElement(nameof(BlockInteractTarget))]
        public NullableDefinitionId[] InteractTargetSerial
        {
            get { return BlockInteractTarget.Select(x => new NullableDefinitionId() {Id = x}).ToArray(); }
            set
            {
                BlockInteractTarget.Clear();
                if (value != null)
                    foreach (var x in value)
                        BlockInteractTarget.Add(x.Id);
            }
        }

        /// <summary>
        /// Unlock on character interaction
        /// </summary>
        [DefaultValue(false)]
        public bool OnCharacterInteract = false;


        [XmlIgnore]
        public readonly HashSet<MyDefinitionId> HandItem =
            new HashSet<MyDefinitionId>(MyDefinitionId.Comparer);

        [XmlIgnore]
        public readonly HashSet<MyDefinitionId> BlockInteractTarget =
            new HashSet<MyDefinitionId>(MyDefinitionId.Comparer);

        /// <summary>
        /// The control ID from <see cref="MyControlsSpace"/>
        /// </summary>
        [XmlElement(nameof(GameControlId))]
        public string GameControlIdSerial
        {
            get { return GameControlId.String; }
            set { GameControlId = MyStringId.GetOrCompute(value); }
        }

        /// <summary>
        /// The control ID from <see cref="MyControlsSpace"/>
        /// </summary>
        [XmlIgnore]
        public MyStringId GameControlId;

        /// <summary>
        /// The mod storage component value for <see cref="InteractResearchStorageComponent"/> on the target must contain this value.
        /// If null the component is ignored.  If empty string the component 
        /// </summary>
        [XmlElement(nameof(RequiredStorageValue), IsNullable = true)]
        public string RequiredStorageValue;


        protected override string ComputeStorageKey()
        {
            var sb = new StringBuilder("Interact/");
            if (HandItem.Count > 0)
                sb.Append("HandItem/");
            foreach (var tmp in HandItem.GroupBy(x => x.TypeId))
            {
                var first = true;
                foreach (var sub in tmp)
                {
                    string typeName, subtypeName;
                    Utilities.Convert(sub, out typeName, out subtypeName);
                    if (first)
                        sb.Append(typeName).Append('/');
                    first = false;
                    sb.Append(subtypeName ?? "null").Append('/');
                }
            }
            if (BlockInteractTarget.Count > 0)
                sb.Append("InteractTarget/");
            foreach (var tmp in BlockInteractTarget)
            {
                string typeName, subtypeName;
                Utilities.Convert(tmp, out typeName, out subtypeName);
                sb.Append(typeName).Append('/').Append(subtypeName ?? "null").Append('/');
            }
            if (OnCharacterInteract)
                sb.Append("CharacterInteract/");
            sb.Append("Control/").Append(GameControlId.String).Append('/');
            if (RequiredStorageValue != null)
                sb.Append("ReqStorage/").Append(RequiredStorageValue).Append('/');
            return sb.ToString();
        }
    }
}