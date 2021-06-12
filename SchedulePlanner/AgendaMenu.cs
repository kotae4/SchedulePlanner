using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Menus;
using StardewValley.BellsAndWhistles;
using Microsoft.Xna.Framework.Input;

namespace kotae.SchedulePlanner
{
    public class AgendaMenu : IClickableMenu
    {
        private enum EMenuState { Viewing, EditingTask, AddingTask };
        string hoverText = "";
        SDate m_ActiveDate;
        EMenuState _State = EMenuState.Viewing;

        ETaskType m_TypeOfActiveTask = ETaskType.Nonrecurrent;
        string m_OldTaskOfEditingTask = "";
        int m_IndexOfEditingTask = -1;

        const int K_MAXTASKSPERPAGE = 8;
        const int K_NUMSPECIALBUTTONS = 3;
        int heightOfTaskBtn = 0;
        int m_NumPages = 1, m_ActivePageIndex = 0;

        int m_TotalTasks = 0;
        List<TaskData> m_NonrecurrentTasks, m_DailyTasks, m_WeeklyTasks;

        TextBox m_TaskTxtbox;
        ClickableTextureComponent m_SubmitTextBtn;
        ClickableTextureComponent m_PageUpBtn;
        ClickableTextureComponent m_PageDownBtn;
        List<ClickableComponent> m_TaskBtns = new List<ClickableComponent>();

        public AgendaMenu(SDate date) : base(0, 0, 0, 0, false)
        {
            m_ActiveDate = date;
            if ((ModEntry.Instance != null) && (ModEntry.Instance.Schedule != null))
            {
                ModEntry.Instance.Schedule.GetTasksForDate(date, out m_NonrecurrentTasks, out m_DailyTasks, out m_WeeklyTasks);
                m_TotalTasks = m_NonrecurrentTasks.Count + m_DailyTasks.Count + m_WeeklyTasks.Count;
            }
            this.width = 832;
            this.height = 768; // was 576
            if (LocalizedContentManager.CurrentLanguageCode == LocalizedContentManager.LanguageCode.ko || LocalizedContentManager.CurrentLanguageCode == LocalizedContentManager.LanguageCode.fr)
                this.height += 64;
            Vector2 centeringOnScreen = Utility.getTopLeftPositionForCenteringOnScreen(this.width, this.height, 0, 0);
            this.xPositionOnScreen = (int)centeringOnScreen.X;
            this.yPositionOnScreen = (int)centeringOnScreen.Y + 32;
            heightOfTaskBtn = (this.height - 32) / K_MAXTASKSPERPAGE;
            this.upperRightCloseButton = new ClickableTextureComponent(new Rectangle(this.xPositionOnScreen + this.width - 20, this.yPositionOnScreen - 8, 48, 48), Game1.mouseCursors, new Rectangle(337, 494, 12, 12), 4f, false);
            // TO-DO:
            // ID and neighbor IDs for all these controls.
            m_PageUpBtn = new ClickableTextureComponent(new Rectangle(this.xPositionOnScreen + this.width + 20, this.yPositionOnScreen + (this.height / 2) - 55, 48, 44), Game1.mouseCursors, new Rectangle(421, 459, 11, 12), 4f, false);
            m_PageDownBtn = new ClickableTextureComponent(new Rectangle(this.xPositionOnScreen + this.width + 20, this.yPositionOnScreen + (this.height / 2) + 55, 48, 44), Game1.mouseCursors, new Rectangle(421, 472, 11, 12), 4f, false);
            // we need a special <Add New Task> button that's always drawn after the last task
            // we need collection of buttons for all tasks (even those on other pages, we'll handle that during drawing)
            ClickableComponent newTaskBtn;
            for (int taskIndex = 0; taskIndex < K_MAXTASKSPERPAGE; taskIndex++)
            {
                newTaskBtn = new ClickableComponent(new Rectangle(this.xPositionOnScreen + 16, this.yPositionOnScreen + 16 + (taskIndex * heightOfTaskBtn), this.width - 32, heightOfTaskBtn), taskIndex.ToString());
                m_TaskBtns.Add(newTaskBtn);
            }
            m_NumPages = (int)Math.Ceiling((double)(m_TotalTasks + K_NUMSPECIALBUTTONS) / K_MAXTASKSPERPAGE);

            m_TaskTxtbox = new TextBox(null, null, Game1.dialogueFont, Game1.textColor);
            // NOTE:
            // when ready to edit make sure to:
            // m_TaskTxtbox.X = blahblah;
            // m_TaskTxtbox.Y = blahblah;
            // Game1.keyboardDispatcher.Subscriber = m_TaskTxtbox;
            // m_TaskTxtbox.Text = "";
            // m_TaskTxtbox.Selected = true;

            m_SubmitTextBtn = new ClickableTextureComponent(new Rectangle(m_TaskTxtbox.X + m_TaskTxtbox.Width + 32 + 4, Game1.viewport.Height / 2 - 8, 64, 64), Game1.mouseCursors, Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 46, -1, -1), 1f, false)
            {
                myID = 1,
                rightNeighborID = -1,
                leftNeighborID = -1
            };
        }

        private void PositionControls()
        {
            this.width = 832;
            this.height = 768; // was 576
            if (LocalizedContentManager.CurrentLanguageCode == LocalizedContentManager.LanguageCode.ko || LocalizedContentManager.CurrentLanguageCode == LocalizedContentManager.LanguageCode.fr)
                this.height += 64;
            Vector2 centeringOnScreen = Utility.getTopLeftPositionForCenteringOnScreen(this.width, this.height, 0, 0);
            this.xPositionOnScreen = (int)centeringOnScreen.X;
            this.yPositionOnScreen = (int)centeringOnScreen.Y + 32;
            heightOfTaskBtn = (this.height - 32) / K_MAXTASKSPERPAGE;

            base.upperRightCloseButton.bounds.X = this.xPositionOnScreen + this.width - 20;
            base.upperRightCloseButton.bounds.Y = this.yPositionOnScreen - 8;

            m_PageUpBtn.bounds.X = this.xPositionOnScreen + this.width + 20;
            m_PageUpBtn.bounds.Y = this.yPositionOnScreen + (this.height / 2) - 55;
            m_PageDownBtn.bounds.X = this.xPositionOnScreen + this.width + 20;
            m_PageDownBtn.bounds.Y = this.yPositionOnScreen + (this.height / 2) + 55;

            for (int taskIndex = 0; taskIndex < K_MAXTASKSPERPAGE; taskIndex++)
            {
                ClickableComponent taskBtn = m_TaskBtns[taskIndex];
                taskBtn.bounds.X = this.xPositionOnScreen + 16;
                taskBtn.bounds.Y = this.yPositionOnScreen + 16 + (taskIndex * heightOfTaskBtn);
                taskBtn.bounds.Width = this.width - 32;
                taskBtn.bounds.Height = heightOfTaskBtn;
            }
        }

        public void BringToFront(SDate date)
        {
            Utils.PrepareGameForInteractableMenu();
            m_ActiveDate = date;
            if ((ModEntry.Instance != null) && (ModEntry.Instance.Schedule != null))
            {
                ModEntry.Instance.Schedule.GetTasksForDate(date, out m_NonrecurrentTasks, out m_DailyTasks, out m_WeeklyTasks);
                m_TotalTasks = m_NonrecurrentTasks.Count + m_DailyTasks.Count + m_WeeklyTasks.Count;
            }
            m_NumPages = (int)Math.Ceiling((double)(m_TotalTasks + K_NUMSPECIALBUTTONS) / K_MAXTASKSPERPAGE);
            PositionControls();
        }

        private void TaskTxtbox_OnEnterPressed(TextBox sender)
        {
            if (_State != EMenuState.Viewing)
            {
                if ((Game1.activeClickableMenu != null) && (Game1.activeClickableMenu is AgendaMenu) && (sender.Text.Length > 0))
                {
                    // do something with it
                    if (_State == EMenuState.AddingTask)
                    {
                        ModEntry.Instance.Schedule.AddNewTask(m_ActiveDate, sender.Text, m_TypeOfActiveTask);
                        switch (m_TypeOfActiveTask)
                        {
                            case ETaskType.Nonrecurrent:
                                m_NonrecurrentTasks.Add(new TaskData() { Task = sender.Text, Date = m_ActiveDate.ToString(), Type = ETaskType.Nonrecurrent });
                                break;
                            case ETaskType.Daily:
                                m_DailyTasks.Add(new TaskData() { Task = sender.Text, Date = m_ActiveDate.ToString(), Type = ETaskType.Daily });
                                break;
                            case ETaskType.Weekly:
                                m_WeeklyTasks.Add(new TaskData() { Task = sender.Text, Date = m_ActiveDate.ToString(), Type = ETaskType.Weekly });
                                break;
                        }
                        m_TotalTasks += 1;
                        m_NumPages = (int)Math.Ceiling((double)(m_TotalTasks + K_NUMSPECIALBUTTONS) / K_MAXTASKSPERPAGE);
                    }
                    else if (_State == EMenuState.EditingTask)
                    {
                        ModEntry.Instance.Schedule.EditTask(m_ActiveDate, m_OldTaskOfEditingTask, m_TypeOfActiveTask, false, sender.Text);
                        switch(m_TypeOfActiveTask)
                        {
                            case ETaskType.Nonrecurrent:
                                m_NonrecurrentTasks[m_IndexOfEditingTask].Task = sender.Text;
                                break;
                            case ETaskType.Daily:
                                m_DailyTasks[m_IndexOfEditingTask - (m_NonrecurrentTasks.Count + m_WeeklyTasks.Count)].Task = sender.Text;
                                break;
                            case ETaskType.Weekly:
                                m_WeeklyTasks[m_IndexOfEditingTask - m_NonrecurrentTasks.Count].Task = sender.Text;
                                break;
                        }
                    }
                }
                this.m_TaskTxtbox.OnEnterPressed -= TaskTxtbox_OnEnterPressed;
                _State = EMenuState.Viewing;
                m_IndexOfEditingTask = -1;
                m_TaskTxtbox.Selected = false;
            }
        }

        public override void draw(SpriteBatch b)
        {
            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.75f);
            IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(384, 373, 18, 18), this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, Color.White, 4f, true);
            string seasonHeaderStr = Utility.getSeasonNameFromNumber(Utility.getSeasonNumber(m_ActiveDate.Season));
            string yearHeaderStr = Game1.content.LoadString(@"Strings\UI:Billboard_Year", m_ActiveDate.Year);
            // need to look up how to get the localized day string
            // ^^^^ am i a fucking dumb? everyone knows arabic numbers. no need to localize them.
            // i would be truly shocked if this game actually does localize numbers
            string dateHeaderStr = m_ActiveDate.Day.ToString() + " " + seasonHeaderStr + " " + yearHeaderStr;
            // the drawStringWithScrollXX draws the string with a fancy ribbon background
            SpriteText.drawStringWithScrollCenteredAt(b, dateHeaderStr, this.xPositionOnScreen + this.width / 2, this.yPositionOnScreen - 64, "", 1f, -1, 0, 0.88f, false);
            // TO-DO:
            // draw page up / page down buttons
            if (m_ActivePageIndex > 0)
                m_PageUpBtn.draw(b);
            if (m_ActivePageIndex < m_NumPages - 1)
                m_PageDownBtn.draw(b);

            // copy how game's quest log draws each quest, and do that for each task.
            // when a task button is clicked, a textbox should be spawned in-place for editing. the textbox's "submit" button should be drawn to the right, outside of main panel.
            string displayStr = "";
            
            int stringPosX, stringPosY;
            bool cursorHighlight;
            Color bgColor;
            int strColorIndex = -1;
            for (int taskBtnIndex = 0; taskBtnIndex < K_MAXTASKSPERPAGE; taskBtnIndex++)
            {
                int taskIndex = (m_ActivePageIndex * K_MAXTASKSPERPAGE) + taskBtnIndex;
                // NOTE:
                // taskIndex has to spill over from m_NonrecurrentTasks to m_WeeklyTasks and then to m_DailyTasks.
                // if it then exceeds m_DailyTasks, then we draw our special buttons.
                if (taskIndex >= m_TotalTasks + K_NUMSPECIALBUTTONS) break;
                if ((_State != EMenuState.Viewing) && (taskIndex == m_IndexOfEditingTask))
                {
                    m_TaskTxtbox.Draw(b, false);
                    continue;
                }
                else
                {
                    // TO-DO:
                    // draw different color background depending on type of task
                    cursorHighlight = _State == EMenuState.Viewing ? m_TaskBtns[taskBtnIndex].containsPoint(Game1.getOldMouseX(), Game1.getOldMouseY()) ? true : false : false;
                    bgColor = cursorHighlight ? Color.Wheat : Color.White;
                    strColorIndex = -1;
                    if (taskIndex < m_TotalTasks)
                    {
                        if (taskIndex >= m_NonrecurrentTasks.Count + m_WeeklyTasks.Count)
                        {
                            // we're on daily tasks now
                            displayStr = m_DailyTasks[taskIndex - (m_NonrecurrentTasks.Count + m_WeeklyTasks.Count)].Task;
                            strColorIndex = 5;
                        }
                        else if (taskIndex >= m_NonrecurrentTasks.Count)
                        {
                            // we're on weekly tasks
                            displayStr = m_WeeklyTasks[taskIndex - m_NonrecurrentTasks.Count].Task;
                            strColorIndex = 3;
                        }
                        else if (taskIndex < m_NonrecurrentTasks.Count)
                        {
                            displayStr = m_NonrecurrentTasks[taskIndex].Task;
                        }
                        stringPosX = m_TaskBtns[taskBtnIndex].bounds.X + 20;
                        stringPosY = m_TaskBtns[taskBtnIndex].bounds.Y + 22;
                    }
                    else
                    {
                        int specialButtonIndex = taskIndex - m_TotalTasks;
                        int strDisplayLength = 0;
                        switch (specialButtonIndex)
                        {
                            // draw the special <AddNewTask> button and then break out of loop
                            case 0:
                                {
                                    displayStr = "--Add New Task--";
                                    break;
                                }
                            case 1:
                                {
                                    displayStr = "--Add New Daily Task--";
                                    break;
                                }
                            case 2:
                                {
                                    displayStr = "--Add New Weekly Task--";
                                    break;
                                }
                        }
                        strDisplayLength = SpriteText.getWidthOfString(displayStr);
                        stringPosX = (m_TaskBtns[taskBtnIndex].bounds.X + (m_TaskBtns[taskBtnIndex].bounds.Width / 2)) - (strDisplayLength / 2);
                        stringPosY = m_TaskBtns[taskBtnIndex].bounds.Y + 22;
                    }
                    IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(384, 396, 15, 15), m_TaskBtns[taskBtnIndex].bounds.X, m_TaskBtns[taskBtnIndex].bounds.Y, m_TaskBtns[taskBtnIndex].bounds.Width, m_TaskBtns[taskBtnIndex].bounds.Height, bgColor, 4f, false);
                    SpriteText.drawString(b, displayStr, stringPosX, stringPosY, 999999, -1, 999999, 1f, 0.88f, false, -1, "", strColorIndex);
                }
            }


            // close button (only thing done in base), the mouse, and hover text
            base.draw(b);
            Game1.mouseCursorTransparency = 1f;
            base.drawMouse(b);
            if (this.hoverText.Length > 0)
            {
                IClickableMenu.drawHoverText(b, this.hoverText, Game1.dialogueFont, 0, 0, -1, null, -1, null, null, 0, -1, -1, -1, -1, 1f, null, null);
            }
        }

        public override void performHoverAction(int x, int y)
        {
            base.performHoverAction(x, y);

            m_PageUpBtn.tryHover(x, y);
            m_PageDownBtn.tryHover(x, y);
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            base.receiveLeftClick(x, y, playSound);

            if (_State == EMenuState.Viewing)
            {
                // TO-DO:
                // handle clicks on page up / page down buttons
                if (m_PageUpBtn.containsPoint(x, y))
                {
                    m_ActivePageIndex = m_ActivePageIndex == 0 ? 0 : m_ActivePageIndex - 1;
                }
                else if (m_PageDownBtn.containsPoint(x, y))
                {
                    m_ActivePageIndex = (m_ActivePageIndex == m_NumPages - 1) ? (m_NumPages - 1) : m_ActivePageIndex + 1;
                }
                else
                {
                    // iterate each existing task button on the activePage and see if they're being clicked
                    for (int taskBtnIndex = 0; taskBtnIndex < K_MAXTASKSPERPAGE; taskBtnIndex++)
                    {
                        ClickableComponent curBtn = m_TaskBtns[taskBtnIndex];
                        int taskIndex = (m_ActivePageIndex * K_MAXTASKSPERPAGE) + taskBtnIndex;
                        if (taskIndex >= m_TotalTasks + K_NUMSPECIALBUTTONS) break;
                        if (curBtn.containsPoint(x, y))
                        {
                            if (taskIndex >= m_TotalTasks)
                            {
                                int specialButtonIndex = taskIndex - m_TotalTasks;
                                switch (specialButtonIndex)
                                {
                                    case 0:
                                        _State = EMenuState.AddingTask;
                                        m_TypeOfActiveTask = ETaskType.Nonrecurrent;
                                        break;
                                    case 1:
                                        _State = EMenuState.AddingTask;
                                        m_TypeOfActiveTask = ETaskType.Daily;
                                        break;
                                    case 2:
                                        _State = EMenuState.AddingTask;
                                        m_TypeOfActiveTask = ETaskType.Weekly;
                                        break;
                                }
                            }
                            else
                            {
                                _State = EMenuState.EditingTask;
                                if (taskIndex >= m_NonrecurrentTasks.Count + m_WeeklyTasks.Count)
                                {
                                    // we're on daily tasks now
                                    m_OldTaskOfEditingTask = m_DailyTasks[taskIndex - (m_NonrecurrentTasks.Count + m_WeeklyTasks.Count)].Task;
                                    m_TypeOfActiveTask = ETaskType.Daily;
                                }
                                else if (taskIndex >= m_NonrecurrentTasks.Count)
                                {
                                    // we're on weekly tasks
                                    m_OldTaskOfEditingTask = m_WeeklyTasks[taskIndex - m_NonrecurrentTasks.Count].Task;
                                    m_TypeOfActiveTask = ETaskType.Weekly;
                                }
                                else if (taskIndex < m_NonrecurrentTasks.Count)
                                {
                                    m_OldTaskOfEditingTask = m_NonrecurrentTasks[taskIndex].Task;
                                    m_TypeOfActiveTask = ETaskType.Nonrecurrent;
                                }
                            }
                            m_IndexOfEditingTask = taskIndex;
                            // TO-DO:
                            // position textbox, register as input subscriber, etc etc
                            m_TaskTxtbox.X = curBtn.bounds.X;
                            m_TaskTxtbox.Y = curBtn.bounds.Y;
                            m_TaskTxtbox.Width = curBtn.bounds.Width - 32;
                            // NOTE:
                            // i haven't looked into why but this height isn't actually its height.
                            // 192 is roughly the same size as the taskBtn components whose height is definitely only 90.
                            // and when i set this textbox height to 90 it like clips and mirrors itself, completely ruining the border graphic.
                            m_TaskTxtbox.Height = 192;

                            m_TaskTxtbox.OnEnterPressed += TaskTxtbox_OnEnterPressed;
                            m_TaskTxtbox.Text = "";
                            m_TaskTxtbox.Selected = true;
                            m_TaskTxtbox.limitWidth = true;
                            // TO-DO:
                            // position & size m_SubmitTextBtn as well (for gamepad controls)
                        }
                    }
                }
            }
            else
            {
                if (!base.isWithinBounds(x, y))
                {
                    // exit the textbox
                    this.m_TaskTxtbox.OnEnterPressed -= TaskTxtbox_OnEnterPressed;
                    _State = EMenuState.Viewing;
                    m_IndexOfEditingTask = -1;
                    m_TaskTxtbox.Selected = false;
                }
                else
                {
                    m_TaskTxtbox.Update();
                }
            }
        }

        public override void receiveRightClick(int x, int y, bool playSound = true)
        {
            if (_State == EMenuState.Viewing)
            {
                for (int taskBtnIndex = 0; taskBtnIndex < K_MAXTASKSPERPAGE; taskBtnIndex++)
                {
                    int taskIndex = (m_ActivePageIndex * K_MAXTASKSPERPAGE) + taskBtnIndex;
                    if (taskIndex >= m_TotalTasks) break;
                    ClickableComponent curBtn = m_TaskBtns[taskBtnIndex];
                    if (curBtn.containsPoint(x, y))
                    {
                        TaskData taskToBeRemoved = null;
                        if (taskIndex >= m_NonrecurrentTasks.Count + m_WeeklyTasks.Count)
                        {
                            // we're on daily tasks now
                            taskToBeRemoved = m_DailyTasks[taskIndex - (m_NonrecurrentTasks.Count + m_WeeklyTasks.Count)];
                            m_TypeOfActiveTask = ETaskType.Daily;
                            m_DailyTasks.RemoveAt(taskIndex - (m_NonrecurrentTasks.Count + m_WeeklyTasks.Count));
                        }
                        else if (taskIndex >= m_NonrecurrentTasks.Count)
                        {
                            // we're on weekly tasks
                            taskToBeRemoved = m_WeeklyTasks[taskIndex - m_NonrecurrentTasks.Count];
                            m_TypeOfActiveTask = ETaskType.Weekly;
                            m_WeeklyTasks.RemoveAt(taskIndex - m_NonrecurrentTasks.Count);
                        }
                        else if (taskIndex < m_NonrecurrentTasks.Count)
                        {
                            taskToBeRemoved = m_NonrecurrentTasks[taskIndex];
                            m_TypeOfActiveTask = ETaskType.Nonrecurrent;
                            m_NonrecurrentTasks.RemoveAt(taskIndex);
                        }
                        ModEntry.Instance.Schedule.EditTask(m_ActiveDate, (taskToBeRemoved == null ? "" : taskToBeRemoved.Task), m_TypeOfActiveTask, true, "-REMOVED-");
                        m_TotalTasks -= 1;
                        m_NumPages = (int)Math.Ceiling((double)(m_TotalTasks + K_NUMSPECIALBUTTONS) / K_MAXTASKSPERPAGE);
                        if (m_ActivePageIndex >= m_NumPages) m_ActivePageIndex = m_NumPages - 1;
                        // save m_Tasks to ModEntry.Instance.Schedule[m_ActiveDate.ToString()]
                        // keep the file clean, don't want empty dates in there taking up space.
                        // TO-DO:
                        // remove entire date from Schedule
                        /*
                        if (m_Tasks.Count == 0)
                            ModEntry.Instance.Schedule.ScheduleByDateString.Remove(m_ActiveDate.ToString());
                        */
                        // TO-DO:
                        // save tasks
                        break;
                    }
                }
            }
        }

        public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
        {
            base.gameWindowSizeChanged(oldBounds, newBounds);
            BringToFront(m_ActiveDate);
            Game1.activeClickableMenu = this;
        }

        public override void receiveKeyPress(Keys key)
        {
            if (_State == EMenuState.Viewing)
            {
                base.receiveKeyPress(key);
            }
        }
    }
}
