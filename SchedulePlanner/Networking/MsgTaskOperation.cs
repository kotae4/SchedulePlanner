using StardewModdingAPI.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace kotae.SchedulePlanner.Networking
{
    public class MsgTaskOperation : BaseNetMsg
    {
        public enum EOperationType : byte
        {
            Create = 1,
            Edit = 2,
            Delete = 4
        };

        public EOperationType Operation;

        public string Date;
        public ETaskType TaskType;
        public string Task;
        public string EditTaskTo;

        public override bool Handle()
        {
            ETaskPermission senderPermissions;
            if (ModEntry.Instance.PlayerPermissionsDict.TryGetValue(SenderClientID, out senderPermissions))
            {
                if (((int)senderPermissions & (int)Operation) == (int)Operation)
                {
                    SDate taskDate;
                    if (Utils.GetDateFromString(Date, out taskDate))
                    {
                        switch (Operation)
                        {
                            case EOperationType.Create:
                                ModEntry.Instance.Schedule.AddNewTask(taskDate, Task, TaskType);
                                break;
                            case EOperationType.Edit:
                                ModEntry.Instance.Schedule.EditTask(taskDate, Task, TaskType, false, EditTaskTo);
                                break;
                            case EOperationType.Delete:
                                ModEntry.Instance.Schedule.EditTask(taskDate, Task, TaskType, true, "-REMOVED-");
                                break;
                        }
                        return true;
                    }
                }
            }
            return false;
        }
    }
}