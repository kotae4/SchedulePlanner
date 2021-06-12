using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StardewModdingAPI.Utilities;

namespace kotae.SchedulePlanner
{
    public class ScheduleData
    {
        // each date contains a list of strings. each string represents the user-inputted task.
        // NOTE:
        // WARNING:
        // i was originally using SDate as the inner key, but smapi's json serialization doesn't know how to deserialize SDates
        // it would be an easy fix, but none of the JSON stuff is exposed through smapi so i can't register custom converters or modify any serialization settings.
        // NOTE:
        // i know i should now class-out the Dictionary<string, List<string>> part but it's all being serialized to json anyway,
        // so i feel like keeping it this way more succinctly describes the layout
        public List<TaskData> Tasks { get; set; } = new List<TaskData>();
        private Dictionary<string, Dictionary<string, List<TaskData>>> ScheduleByTypeAndDateDict = new Dictionary<string, Dictionary<string, List<TaskData>>>();


        public ScheduleData()
        {
            Tasks = new List<TaskData>();
        }

        public void Process()
        {
            // NOTE:
            // we serialize the ScheduleByDateDict, and instantiate and fill the ScheduleByTypeAndDateDict here.
            // the extra granularity isn't necessary to store on disk, but does make some runtime operations a lot easier
            ScheduleByTypeAndDateDict = new Dictionary<string, Dictionary<string, List<TaskData>>>() {
                { "nonrecurrent",
                    new Dictionary<string, List<TaskData>>()
                },
                { "daily",
                    new Dictionary<string, List<TaskData>>()
                },
                { "weekly",
                    new Dictionary<string, List<TaskData>>()
                }
            };
            foreach (TaskData task in Tasks)
            {
                Dictionary<string, List<TaskData>> dateDict;
                switch (task.Type)
                {
                    case ETaskType.Nonrecurrent:
                        dateDict = ScheduleByTypeAndDateDict["nonrecurrent"];
                        break;
                    case ETaskType.Weekly:
                        dateDict = ScheduleByTypeAndDateDict["weekly"];
                        break;
                    case ETaskType.Daily:
                    default:
                        dateDict = ScheduleByTypeAndDateDict["daily"];
                        break;
                }
                if (!dateDict.ContainsKey(task.Date))
                {
                    dateDict.Add(task.Date, new List<TaskData>());
                }
                dateDict[task.Date].Add(task);
            }
        }

        public List<TaskData> GetFlattenedTasksForDate(SDate date, ETaskType type = ETaskType.All)
        {
            List<TaskData> retVal = new List<TaskData>();
            string dateString = date.ToString();
            if ((type & ETaskType.Nonrecurrent) == ETaskType.Nonrecurrent)
            {
                // add nonrecurrent tasks for this specific date
                if ((ScheduleByTypeAndDateDict.ContainsKey("nonrecurrent")) && (ScheduleByTypeAndDateDict["nonrecurrent"].ContainsKey(dateString)))
                {
                    retVal.AddRange(ScheduleByTypeAndDateDict["nonrecurrent"][dateString]);
                }
            }
            if ((type & ETaskType.Daily) == ETaskType.Daily)
            {
                // add all daily tasks, regardless of the date they were created
                if ((ScheduleByTypeAndDateDict.ContainsKey("daily")) && (ScheduleByTypeAndDateDict["daily"].Count > 0))
                {
                    foreach (KeyValuePair<string, List<TaskData>> kv in ScheduleByTypeAndDateDict["daily"])
                    {
                        if ((kv.Value != null) && (kv.Value.Count > 0))
                            retVal.AddRange(kv.Value);
                    }
                }
            }
            if ((type & ETaskType.Weekly) == ETaskType.Weekly)
            {
                // add all weekly tasks that match its creation date to the same day of week as this specific date
                SDate weeklyDate;
                if ((ScheduleByTypeAndDateDict.ContainsKey("weekly")) && (ScheduleByTypeAndDateDict["weekly"].Count > 0))
                {
                    foreach (KeyValuePair<string, List<TaskData>> kv in ScheduleByTypeAndDateDict["weekly"])
                    {
                        if (Utils.GetDateFromString(kv.Key, out weeklyDate))
                        {
                            if (weeklyDate.DayOfWeek == date.DayOfWeek)
                            {
                                retVal.AddRange(kv.Value);
                            }
                        }
                    }
                }
            }
            return retVal;
        }

        public void GetTasksForDate(SDate date, out List<TaskData> nonrecurrent, out List<TaskData> daily, out List<TaskData> weekly)
        {
            string dateString = date.ToString();
            nonrecurrent = new List<TaskData>();
            daily = new List<TaskData>();
            weekly = new List<TaskData>();
            // nonrecurrent tasks
            if ((ScheduleByTypeAndDateDict["nonrecurrent"].ContainsKey(dateString)) && (ScheduleByTypeAndDateDict["nonrecurrent"][dateString].Count > 0))
                nonrecurrent.AddRange(ScheduleByTypeAndDateDict["nonrecurrent"][dateString]);
            // daily tasks
            foreach (KeyValuePair<string, List<TaskData>> kv in ScheduleByTypeAndDateDict["daily"])
            {
                if ((kv.Value != null) && (kv.Value.Count > 0))
                    daily.AddRange(kv.Value);
            }
            // weekly tasks
            SDate weeklyDate;
            foreach (KeyValuePair<string, List<TaskData>> kv in ScheduleByTypeAndDateDict["weekly"])
            {
                if (Utils.GetDateFromString(kv.Key, out weeklyDate))
                {
                    if (weeklyDate.DayOfWeek == date.DayOfWeek)
                    {
                        weekly.AddRange(kv.Value);
                    }
                }
            }
        }

        public int GetNumTasksForDate(SDate date)
        {
            int numTasks = 0;
            string dateString = date.ToString();
            // add nonrecurrent tasks for this specific date
            if ((ScheduleByTypeAndDateDict.ContainsKey("nonrecurrent")) && (ScheduleByTypeAndDateDict["nonrecurrent"].ContainsKey(dateString)))
            {
                numTasks += ScheduleByTypeAndDateDict["nonrecurrent"][dateString].Count;
            }
            // add all daily tasks, regardless of the date they were created
            if ((ScheduleByTypeAndDateDict.ContainsKey("daily")) && (ScheduleByTypeAndDateDict["daily"].Count > 0))
            {
                foreach (KeyValuePair<string, List<TaskData>> kv in ScheduleByTypeAndDateDict["daily"])
                {
                    if ((kv.Value != null) && (kv.Value.Count > 0))
                        numTasks += kv.Value.Count;
                }
            }
            // add all weekly tasks that match its creation date to the same day of week as this specific date
            SDate weeklyDate;
            if ((ScheduleByTypeAndDateDict.ContainsKey("weekly")) && (ScheduleByTypeAndDateDict["weekly"].Count > 0))
            {
                foreach (KeyValuePair<string, List<TaskData>> kv in ScheduleByTypeAndDateDict["weekly"])
                {
                    if (Utils.GetDateFromString(kv.Key, out weeklyDate))
                    {
                        if (weeklyDate.DayOfWeek == date.DayOfWeek)
                        {
                            numTasks += kv.Value.Count;
                        }
                    }
                }
            }
            return numTasks;
        }

        public int GetNumTasksForDate(SDate date, out int numNonrecurrent, out int numWeeklies, out int numDailies)
        {
            int numTotalTasks = 0;
            numNonrecurrent = 0;
            numWeeklies = 0;
            numDailies = 0;
            string dateString = date.ToString();
            // add nonrecurrent tasks for this specific date
            if ((ScheduleByTypeAndDateDict.ContainsKey("nonrecurrent")) && (ScheduleByTypeAndDateDict["nonrecurrent"].ContainsKey(dateString)))
            {
                numTotalTasks += ScheduleByTypeAndDateDict["nonrecurrent"][dateString].Count;
                numNonrecurrent += ScheduleByTypeAndDateDict["nonrecurrent"][dateString].Count;
            }
            // add all daily tasks, regardless of the date they were created
            if ((ScheduleByTypeAndDateDict.ContainsKey("daily")) && (ScheduleByTypeAndDateDict["daily"].Count > 0))
            {
                foreach (KeyValuePair<string, List<TaskData>> kv in ScheduleByTypeAndDateDict["daily"])
                {
                    if ((kv.Value != null) && (kv.Value.Count > 0))
                    {
                        numTotalTasks += kv.Value.Count;
                        numDailies += kv.Value.Count;
                    }
                }
            }
            // add all weekly tasks that match its creation date to the same day of week as this specific date
            SDate weeklyDate;
            if ((ScheduleByTypeAndDateDict.ContainsKey("weekly")) && (ScheduleByTypeAndDateDict["weekly"].Count > 0))
            {
                foreach (KeyValuePair<string, List<TaskData>> kv in ScheduleByTypeAndDateDict["weekly"])
                {
                    if (Utils.GetDateFromString(kv.Key, out weeklyDate))
                    {
                        if (weeklyDate.DayOfWeek == date.DayOfWeek)
                        {
                            numTotalTasks += kv.Value.Count;
                            numWeeklies += kv.Value.Count;
                        }
                    }
                }
            }
            return numTotalTasks;
        }

        public void AddNewTask(SDate date, string task, ETaskType type)
        {
            string dateString = date.ToString();
            TaskData newTask = new TaskData() { Date = date.ToString(), Task = task, Type = type };
            Dictionary<string, List<TaskData>> typeDict;
            switch (type)
            {
                case ETaskType.Nonrecurrent:
                        typeDict = ScheduleByTypeAndDateDict["nonrecurrent"];
                        break;
                case ETaskType.Daily:
                        typeDict = ScheduleByTypeAndDateDict["daily"];
                        break;
                case ETaskType.Weekly:
                        typeDict = ScheduleByTypeAndDateDict["weekly"];
                        break;
                default:
                    typeDict = ScheduleByTypeAndDateDict["nonrecurrent"];
                    break;
            }
            if (Tasks.Contains(newTask))
                return;

            Tasks.Add(newTask);
            if (typeDict.ContainsKey(dateString))
                typeDict[dateString].Add(newTask);
            else
                typeDict.Add(dateString, new List<TaskData>() { newTask });

            ModEntry.Instance.SaveSchedule();
            // broadcast the new event to all multiplayer clients
            Networking.MsgTaskOperation taskMsg = new Networking.MsgTaskOperation();
            taskMsg.Date = dateString;
            taskMsg.Operation = Networking.MsgTaskOperation.EOperationType.Create;
            taskMsg.Task = task;
            taskMsg.TaskType = type;
            ModEntry.Instance.Helper.Multiplayer.SendMessage<Networking.MsgTaskOperation>(taskMsg, "TaskOperation", new string[] { ModEntry.Instance.ModManifest.UniqueID }, null);
        }

        public void EditTask(SDate date, string oldTask, ETaskType type, bool remove, string newTask)
        {
            // NOTE:
            // i hate this. i hate this so much.
            // i really need to design my data structure better, but i'm prioritizing pretty JSON output over actual functionality...
            // and since SMAPI doesn't give me much control over the JSON part, i'm really stuck between a rock and a hard place here.
            TaskData existingTask = null;
            int existingTaskIndex = -1;
            for (int taskIndex = Tasks.Count - 1; taskIndex >= 0; taskIndex--)
            {
                if ((Tasks[taskIndex].Task == oldTask) && (Tasks[taskIndex].Type == type))
                {
                    bool isSame = true;
                    if (type == ETaskType.Nonrecurrent)
                        isSame = Tasks[taskIndex].Date == date.ToString();
                    else if (type == ETaskType.Weekly)
                    {
                        SDate taskDate;
                        if (Utils.GetDateFromString(Tasks[taskIndex].Date, out taskDate))
                            isSame = (taskDate.DayOfWeek == date.DayOfWeek);
                        else
                            isSame = false;
                    }
                    if (isSame)
                    {
                        existingTask = Tasks[taskIndex];
                        existingTaskIndex = taskIndex;
                        break;
                    }
                }
            }
            if (existingTask == null)
                throw new Exception("[SchedulePlanner] Could not find existing task to edit");

            string dateString = date.ToString();
            Dictionary<string, List<TaskData>> dateDict;
            switch (type)
            {
                case ETaskType.Nonrecurrent:
                    dateDict = ScheduleByTypeAndDateDict["nonrecurrent"];
                    break;
                case ETaskType.Weekly:
                    dateDict = ScheduleByTypeAndDateDict["weekly"];
                    break;
                case ETaskType.Daily:
                default:
                    dateDict = ScheduleByTypeAndDateDict["daily"];
                    break;
            }
            if (dateDict.ContainsKey(existingTask.Date))
            {
                List<TaskData> tasksForDateByType = dateDict[existingTask.Date];
                for (int taskIndex = tasksForDateByType.Count - 1; taskIndex >= 0; taskIndex--)
                {
                    if (tasksForDateByType[taskIndex].Equals(existingTask))
                    {
                        if (remove)
                        {
                            dateDict[existingTask.Date].RemoveAt(taskIndex);
                            Tasks.RemoveAt(existingTaskIndex);
                        }
                        else
                        {
                            dateDict[existingTask.Date][taskIndex].Task = newTask;
                            Tasks[existingTaskIndex].Task = newTask;
                        }
                        break;
                    }
                }
            }
            ModEntry.Instance.SaveSchedule();
            // broadcast the new event to all multiplayer clients
            Networking.MsgTaskOperation taskMsg = new Networking.MsgTaskOperation();
            taskMsg.Date = dateString;
            taskMsg.Operation = (remove == true ? Networking.MsgTaskOperation.EOperationType.Delete : Networking.MsgTaskOperation.EOperationType.Edit);
            taskMsg.Task = oldTask;
            taskMsg.TaskType = type;
            taskMsg.EditTaskTo = newTask;
            ModEntry.Instance.Helper.Multiplayer.SendMessage<Networking.MsgTaskOperation>(taskMsg, "TaskOperation", new string[] { ModEntry.Instance.ModManifest.UniqueID }, null);
        }
    }
}