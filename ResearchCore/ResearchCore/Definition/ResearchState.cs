using System;
using System.Collections.Generic;
using System.Text;
using Equinox.ResearchCore.Definition.ObjectBuilders.Triggers;

namespace Equinox.ResearchCore.Definition
{
    /// <summary>
    /// Used as flags for <see cref="Ob_Trigger_ResearchState"/>
    /// </summary>
    [Flags]
    public enum ResearchState
    {
        NotStarted = (1 << 0),
        InProgress = (1 << 1),
        Completed = (1 << 2),
        Failed = (1 << 3),
        InProgressOrCompleted = InProgress | Completed,
        FailedOrNotStarted = NotStarted | Failed,
        NotFailed = ~Failed,
    }
}