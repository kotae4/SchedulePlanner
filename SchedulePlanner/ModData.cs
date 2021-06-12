using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StardewModdingAPI.Utilities;

namespace kotae.SchedulePlanner
{
    public class ModData
    {
        // each date contains a list of strings. each string represents the user-inputted task.
        public Dictionary<string, ScheduleData> SchedulesBySavefile { get; set; } = new Dictionary<string, ScheduleData>();
    }
}