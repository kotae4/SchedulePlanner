using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StardewModdingAPI;
using StardewValley;

namespace kotae.SchedulePlanner.Networking
{
    [Flags]
    public enum ETaskPermission
    {
        Create = 1,
        Edit = 2,
        Delete = 4,
        All = 7
    };

    public class MsgPermissionsOperation : BaseNetMsg
    {
        // TO-DO:
        // NOTE:
        // so, my original plan of having permissions be restricted to the host, the creator, or the guest (or any combination) won't work
        // the reason is because once a client disconnects it's impossible to detect when that specific player reconnects
        // there is no unique data that will persist between client disconnect & reconnect and getting access to the steamID isn't feasible
        // could maybe get IP address but i'm not going to count that as persistent
        // === SO, the new plan ===
        // permissions should instead be given per-player. so there should be 3 checkboxes next to each client's name: create, edit, delete.
        // and if a player has any of those permissions, then they can perform those actions on any task.
        // if that player disconnects and later reconnects then the host will need to give them their permissions back. slightly inconvenient, but oh well.
        public long TargetClientID;
        public ETaskPermission NewPermissions;

        public override bool Handle()
        {
            if (Game1.MasterPlayer.UniqueMultiplayerID == SenderClientID)
            {
                if (ModEntry.Instance.PlayerPermissionsDict.ContainsKey(TargetClientID))
                {
                    ModEntry.Instance.PlayerPermissionsDict[TargetClientID] = NewPermissions;
                    return true;
                }
            }
            return false;
        }
    }
}