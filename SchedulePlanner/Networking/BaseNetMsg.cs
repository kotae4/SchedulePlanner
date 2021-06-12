using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace kotae.SchedulePlanner.Networking
{
    public abstract class BaseNetMsg
    {
        public long SenderClientID;

        public abstract bool Handle();
    }
}
