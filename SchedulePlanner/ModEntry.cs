using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using StardewModdingAPI.Utilities;
using StardewModdingAPI.Events;
using kotae.SchedulePlanner.Networking;

namespace kotae.SchedulePlanner
{
    public class ModEntry : Mod
    {
        private static ModEntry _instance;
        public static ModEntry Instance { get { return _instance; } }

        Color _WeeklyTaskColor = new Color(110, 0x2b, 0xff);

        private IModHelper _Helper;

        // savedata, serialized to/from json file
        private ModData _Data;
        // config data, editable by user, handled internally by smapi
        private ModConfig _Config;
        // the schedule for the currently loaded save file
        public ScheduleData Schedule;

        // our UI menu instances.
        CalendarMenu _Calendar;

        // data for the HUD overlay
        SDate _CurrentDay;
        int _TotalTasksForToday = 0;
        // cached tasks just for the day, helps avoid expensive dictionary lookups every frame
        List<TaskData> _NonrecurrentTasksForToday, _DailyTasksForToday, _WeeklyTasksForToday;
        Rectangle BoundsForTasksHUD = new Rectangle(0, 0, 0, 0);
        bool IsBoundsDirty = true;
        // for semi-transparent background on HUD overlay
        Texture2D pixelTex;
        Color semiTransColor = new Color(0, 0, 0, 80);

        public Dictionary<long, ETaskPermission> PlayerPermissionsDict = new Dictionary<long, ETaskPermission>();

        // TO-DO 13/9/20:
        // schedule rework is done
        // handling net msgs is done
        // --> have to send net msgs
        // --> have to make sidebar menu on the calendar for host to set permissions of clients

        /// <summary>
        /// TO-DO:
        /// add multiplayer support. sync tasks between all players (whether host or client, everyone has create permissions. maybe only host should have edit/remove permissions)
        /// </summary>
        /// <param name="helper"></param>

        public override void Entry(IModHelper helper)
        {
            pixelTex = new Texture2D(Game1.graphics.GraphicsDevice, 1, 1, false, SurfaceFormat.Color);
            pixelTex.SetData(new Color[] { Color.White });

            _instance = this;
            _Helper = helper;
            helper.Events.GameLoop.SaveLoaded += GameLoop_SaveLoaded;
            helper.Events.Input.ButtonPressed += Input_ButtonPressed;
            helper.Events.GameLoop.DayStarted += GameLoop_DayStarted;
            helper.Events.Display.RenderedHud += Display_RenderedHud;
            helper.Events.Display.MenuChanged += Display_MenuChanged;
            helper.Events.Multiplayer.PeerConnected += Net_PeerConnected;
            helper.Events.Multiplayer.PeerDisconnected += Net_PeerDisconnected;
            helper.Events.Multiplayer.ModMessageReceived += Net_MessageReceived;

            _Config = _Helper.ReadConfig<ModConfig>();
            _Data = helper.Data.ReadJsonFile<ModData>("schedule.json") ?? new ModData();
        }

        private void GameLoop_SaveLoaded(object sender, StardewModdingAPI.Events.SaveLoadedEventArgs e)
        {
            if (!_Data.SchedulesBySavefile.TryGetValue(Constants.SaveFolderName, out Schedule))
            {
                Schedule = new ScheduleData();
                _Data.SchedulesBySavefile.Add(Constants.SaveFolderName, Schedule);
            }
            Schedule.Process();
        }

        private void Display_MenuChanged(object sender, StardewModdingAPI.Events.MenuChangedEventArgs e)
        {
            if ((e.NewMenu is Billboard) && (Game1.activeClickableMenu is Billboard))
            {
                bool billboardIsCalendar = !(_Helper.Reflection.GetField<bool>(e.NewMenu, "dailyQuestBoard").GetValue());
                if (billboardIsCalendar)
                {
                    if (_Calendar == null)
                    {
                        _Calendar = new CalendarMenu();
                    }
                    else
                    {
                        _Calendar.BringToFront();
                    }
                    Game1.activeClickableMenu = _Calendar;
                }
            }
        }

        private void GameLoop_DayStarted(object sender, StardewModdingAPI.Events.DayStartedEventArgs e)
        {
            _CurrentDay = SDate.Now();
            Schedule.GetTasksForDate(SDate.Now(), out _NonrecurrentTasksForToday, out _DailyTasksForToday, out _WeeklyTasksForToday);
            _TotalTasksForToday = _NonrecurrentTasksForToday.Count + _DailyTasksForToday.Count + _WeeklyTasksForToday.Count;

            // send full schedule to every player at start of each day just in case players fall out of sync somehow
            if (Schedule != null)
            {
                MsgSyncOperation syncMsg = new MsgSyncOperation();
                syncMsg.schedule = Schedule;
                _Helper.Multiplayer.SendMessage<MsgSyncOperation>(syncMsg, "SyncOperation", new string[] { ModManifest.UniqueID }, null);
            }
        }

        public void SaveSchedule()
        {
            _Helper.Data.WriteJsonFile<ModData>("schedule.json", _Data);
            Schedule.GetTasksForDate(SDate.Now(), out _NonrecurrentTasksForToday, out _DailyTasksForToday, out _WeeklyTasksForToday);
            _TotalTasksForToday = _NonrecurrentTasksForToday.Count + _DailyTasksForToday.Count + _WeeklyTasksForToday.Count;
            IsBoundsDirty = true;
        }

        // yoink'd this method from UIInfoSuite (mod page: https://www.nexusmods.com/stardewvalley/mods/1150)
        // source: https://github.com/cdaragorn/Ui-Info-Suite/blob/master/SDVModTest/Tools.cs
        // credits: cdaragorn (https://github.com/cdaragorn : https://www.nexusmods.com/stardewvalley/users/7264904)
        private int GetWidthInPlayArea()
        {
            if (Game1.isOutdoorMapSmallerThanViewport())
            {
                int right = Game1.graphics.GraphicsDevice.Viewport.TitleSafeArea.Right;
                int num3 = Game1.currentLocation.map.Layers[0].LayerWidth * 0x40;
                int num4 = Game1.graphics.GraphicsDevice.Viewport.TitleSafeArea.Right - num3;
                return (right - (num4 / 2));
            }
            return Game1.graphics.GraphicsDevice.Viewport.TitleSafeArea.Right;
        }

        Rectangle GetEncompassingBoundsForTasks(int screenWidth, int startingY)
        {
            Vector2 displaySizeOfStr = Game1.smallFont.MeasureString("-- TASKS --");

            float largestX = displaySizeOfStr.X, largestY = displaySizeOfStr.Y + 3f;
            foreach (TaskData task in _DailyTasksForToday)
            {
                displaySizeOfStr = Game1.smallFont.MeasureString(task.Task);
                if (displaySizeOfStr.X > largestX)
                    largestX = displaySizeOfStr.X;
                largestY += displaySizeOfStr.Y;
            }
            foreach (TaskData task in _WeeklyTasksForToday)
            {
                displaySizeOfStr = Game1.smallFont.MeasureString(task.Task);
                if (displaySizeOfStr.X > largestX)
                    largestX = displaySizeOfStr.X;
                largestY += displaySizeOfStr.Y;
            }
            foreach (TaskData task in _NonrecurrentTasksForToday)
            {
                displaySizeOfStr = Game1.smallFont.MeasureString(task.Task);
                if (displaySizeOfStr.X > largestX)
                    largestX = displaySizeOfStr.X;
                largestY += displaySizeOfStr.Y;
            }

            return new Rectangle((int)(screenWidth - largestX - 20), startingY, (int)largestX, (int)largestY);
        }

        private void Display_RenderedHud(object sender, StardewModdingAPI.Events.RenderedHudEventArgs e)
        {
            if ((!Context.IsWorldReady) || (Game1.eventUp) || (_TotalTasksForToday == 0)) return;

            Vector2 displaySizeOfStr = Game1.smallFont.MeasureString("-- TASKS --");
            int screenWidth = GetWidthInPlayArea();
            Vector2 drawPosition = new Vector2(screenWidth - displaySizeOfStr.X - 20, 370);

            if (IsBoundsDirty == true)
            {
                BoundsForTasksHUD = GetEncompassingBoundsForTasks(screenWidth, 370);
                IsBoundsDirty = false;
            }

            e.SpriteBatch.Draw(pixelTex, BoundsForTasksHUD, semiTransColor);

            // center the tasks header. everything else will be aligned to the right edge of the screen.
            e.SpriteBatch.DrawString(Game1.smallFont, "-- TASKS --", new Vector2(BoundsForTasksHUD.Center.X - (displaySizeOfStr.X * 0.5f), 370), Color.White);
            float yOffset = displaySizeOfStr.Y + 3f;
            foreach (TaskData task in _DailyTasksForToday)
            {
                displaySizeOfStr = Game1.smallFont.MeasureString(task.Task);
                drawPosition.X = screenWidth - displaySizeOfStr.X - 20;
                drawPosition.Y = 370 + yOffset;
                e.SpriteBatch.DrawString(Game1.smallFont, task.Task, drawPosition, Color.OrangeRed);
                yOffset += displaySizeOfStr.Y;
            }
            foreach (TaskData task in _WeeklyTasksForToday)
            {
                displaySizeOfStr = Game1.smallFont.MeasureString(task.Task);
                drawPosition.X = screenWidth - displaySizeOfStr.X - 20;
                drawPosition.Y = 370 + yOffset;
                e.SpriteBatch.DrawString(Game1.smallFont, task.Task, drawPosition, _WeeklyTaskColor);
                yOffset += displaySizeOfStr.Y;
            }
            foreach (TaskData task in _NonrecurrentTasksForToday)
            {
                displaySizeOfStr = Game1.smallFont.MeasureString(task.Task);
                drawPosition.X = screenWidth - displaySizeOfStr.X - 20;
                drawPosition.Y = 370 + yOffset;
                e.SpriteBatch.DrawString(Game1.smallFont, task.Task, drawPosition, Color.White);
                yOffset += displaySizeOfStr.Y;
            }
        }

        private void Input_ButtonPressed(object sender, StardewModdingAPI.Events.ButtonPressedEventArgs e)
        {
            if ((!Context.IsWorldReady) || (!Context.IsPlayerFree)) return;

            if ((e.Button == _Config.CalendarHotkey) && (_Config.CalendarAnywhere == true))
            {
                if (_Calendar == null)
                {
                    _Calendar = new CalendarMenu();
                }
                else
                {
                    _Calendar.BringToFront();
                }
                Game1.activeClickableMenu = _Calendar;
            }
            else if (e.Button == SButton.F4)
            {
                SaveSchedule();
            }
        }

        private void Net_PeerConnected(object sender, PeerConnectedEventArgs e)
        {
            ETaskPermission permissions = (e.Peer.IsHost ? ETaskPermission.All : _Config.DefaultTaskPermissions);
            PlayerPermissionsDict.Add(e.Peer.PlayerID, permissions);
            // send full schedule (if it exists - can you join a multiplayer session before the host selects a save?)
            if (Schedule != null)
            {
                MsgSyncOperation syncMsg = new MsgSyncOperation();
                syncMsg.schedule = Schedule;
                _Helper.Multiplayer.SendMessage<MsgSyncOperation>(syncMsg, "SyncOperation", new string[] { ModManifest.UniqueID }, new long[] { e.Peer.PlayerID });
            }
        }

        private void Net_PeerDisconnected(object sender, PeerDisconnectedEventArgs e)
        {
            PlayerPermissionsDict.Remove(e.Peer.PlayerID);
        }

        private void Net_MessageReceived(object sender, ModMessageReceivedEventArgs e)
        {
            if (e.FromModID == this.ModManifest.UniqueID)
            {
                BaseNetMsg unpackedNetMsg = null;
                if (e.Type == "TaskOperation")
                {
                    unpackedNetMsg = e.ReadAs<MsgTaskOperation>();
                }
                else if (e.Type == "PermissionsOperation")
                {
                    unpackedNetMsg = e.ReadAs<MsgPermissionsOperation>();
                }
                else if (e.Type == "SyncOperation")
                {
                    unpackedNetMsg = e.ReadAs<MsgSyncOperation>();
                    // _Data is private so we can't access it in the handler and i don't feel like redesigning this
                    // so just do the handling here and return early
                    if (!_Data.SchedulesBySavefile.ContainsKey(Constants.SaveFolderName))
                    {
                        _Data.SchedulesBySavefile.Add(Constants.SaveFolderName, ((MsgSyncOperation)unpackedNetMsg).schedule);
                    }
                    else
                    {
                        _Data.SchedulesBySavefile[Constants.SaveFolderName] = ((MsgSyncOperation)unpackedNetMsg).schedule;
                    }
                    Schedule.Process();
                    SaveSchedule();
                    return;
                }

                if (unpackedNetMsg != null)
                {
                    unpackedNetMsg.SenderClientID = e.FromPlayerID;
                    unpackedNetMsg.Handle();
                }
            }
        }
    }
}
