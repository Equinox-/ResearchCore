using System;
using System.Text;
using Equinox.ResearchCore.Definition;

namespace Equinox.ResearchCore.Modules
{
    public static class CommandUtils
    {
        public static ResearchDefinition TryFindResearch(this ResearchManager manager, string key, out string msg)
        {
            var research = manager.Definitions.ResearchById(key);
            if (research == null)
            {
                StringBuilder msgResult = null;
                foreach (var res in manager.Definitions.Research)
                {
                    if (!res.Id.StartsWith(key, StringComparison.OrdinalIgnoreCase) &&
                        !res.DisplayName.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (research == null && msgResult == null)
                    {
                        research = res;
                        continue;
                    }

                    if (msgResult == null)
                        msgResult = new StringBuilder("Could not find single research: ");
                    if (research != null)
                        msgResult.Append(research.Id);
                    research = null;
                    msgResult.Append(", ").Append(res.Id);
                }

                if (msgResult != null)
                {
                    msg = msgResult.ToString();
                    return null;
                }
            }

            if (research == null)
            {
                msg = $"Could not find research: {key}";
                return null;
            }

            msg = null;
            return research;
        }
    }
}