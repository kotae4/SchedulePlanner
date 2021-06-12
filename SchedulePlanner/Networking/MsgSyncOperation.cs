using StardewModdingAPI.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace kotae.SchedulePlanner.Networking
{
    public class MsgSyncOperation : BaseNetMsg
    {
        public ScheduleData schedule;

        public override bool Handle()
        {
            return false;
        }
    }
}