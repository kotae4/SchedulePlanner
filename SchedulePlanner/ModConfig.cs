using StardewModdingAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using kotae.SchedulePlanner.Networking;

namespace kotae.SchedulePlanner
{
    public class ModConfig
    {
        public bool CalendarAnywhere { get; set; } = true;
        public SButton CalendarHotkey { get; set; } = SButton.F2;
        public ETaskPermission DefaultTaskPermissions { get; set; } = ETaskPermission.All;
    }
}
