using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StardewModdingAPI.Utilities;

namespace kotae.SchedulePlanner
{
    [Flags]
    public enum ETaskType
    {
        Nonrecurrent = 1,
        Daily = 2,
        Weekly = 4,
        All = 7
    };

    public class TaskData : IEquatable<TaskData>
    {
        public string Date = "";
        public string Task = "";
        public ETaskType Type = ETaskType.Nonrecurrent;
        // other fields to be added

        public override int GetHashCode()
        {
            return (((Date.GetHashCode() << 2) ^ Task.GetHashCode()) << 2) ^ (int)Type;
        }

        public override bool Equals(object other)
        {
            if (other == null) return false;
            TaskData otherAsTask = other as TaskData;
            if (otherAsTask == null) return false;
            else return Equals(otherAsTask);
        }

        public bool Equals(TaskData other)
        {
            return ((Date == other.Date) && 
                (Task == other.Task) && 
                (Type == other.Type));
        }
    }
}