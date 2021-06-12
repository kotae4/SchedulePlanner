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

namespace kotae.SchedulePlanner
{
    public class CalendarMenu : IClickableMenu
    {
        Texture2D m_CalendarTex;
        string nightMarketLocalized = "", wizardBirthdayLocalized = "", hoverText = "";
        ClickableTextureComponent m_PrevSeasonButton, m_NextSeasonButton, m_PrevYearButton, m_NextYearButton;
        AgendaMenu m_AgendaMenu;
        // game uses:
        // "spring", "summer", "fall", "winter" case sensitive, non-localized
        Dictionary<string, List<ClickableTextureComponent>> m_CalendarDaysBySeasonName;

        string m_ActiveSeason = "spring";
        int m_ActiveYear = 1;


        public CalendarMenu() : base(0, 0, 0, 0, false)
        {
            // NOTE:
            // we kinda need to instantiate a new instance each time because the base ctor sets game state (time stops, player movement halts, etc)
            // but instantiating all these assets each and every time is really inefficient, so i'm thinking we make them static and have a static bool HasInitializedAssets and check that each time.
            // should speed things up. not that they're slow right now, but faster is faster than fast, so let's aim for faster always.
            // NOTE:
            // so, we want our calendar to show everything the game's calendar shows, BUT also:
            // 1. left / right arrows next to the Season that'll load up the prev / next season (do you like writing obvious things? no)
            //     * the season text will probably have to be moved a bit to the right to accomodate the left button (i thought the season was centered, but nope it's left-aligned)
            // 2. a new icon that denotes tasks scheduled for that day (probably in lower left of the day, below where the night market icon shows in winter)
            // 3. when hovering over a day with scheduled task, the hoverText should show "X tasks planned" or something in addition to the game's usual hoverText (if any)
            // it's worth noting that the festival and the night market icons are animated...
            // also that villager sprites (to denote birthdays) vary in height, so top right might not be a good idea for our icon
            // finally, we don't need to display the wedding icon on the calendar since the ceremony isn't miss-able, skippable, nor does it effect gameplay in any way (time freezes at 6am while the cutscene happens)
            m_ActiveSeason = Game1.currentSeason;
            m_ActiveYear = Game1.year;
            base.width = 1204;
            base.height = 792;
            Vector2 topLeftCornerWhenCentered = Utility.getTopLeftPositionForCenteringOnScreen(base.width, base.height, 0, 0);
            base.xPositionOnScreen = (int)topLeftCornerWhenCentered.X;
            base.yPositionOnScreen = (int)topLeftCornerWhenCentered.Y;
            base.upperRightCloseButton = new ClickableTextureComponent(new Rectangle((base.xPositionOnScreen + base.width) - 20, base.yPositionOnScreen, 48, 48), Game1.mouseCursors, new Rectangle(337, 494, 12, 12), 4f, false);
            m_CalendarTex = Game1.temporaryContent.Load<Texture2D>(@"LooseSprites\Billboard");
            // NOTE:
            // initialize buttons to cycle through season & year
            // right arrow icon in cursors.png (Game1.mouseCursors), at sourceRect (365, 495, 12, 11)
            // left arrow icon in cursors.png (Game1.mouseCursors), at sourceRect (352, 495, 12, 11)
            // the season string is drawn at xPos + 165, yPos + 80
            // year string is drawn at xPos + 448, yPos + 80
            m_PrevSeasonButton = new ClickableTextureComponent(new Rectangle(this.xPositionOnScreen + 113, this.yPositionOnScreen + 80, 48, 44), Game1.mouseCursors, new Rectangle(352, 495, 12, 11), 4f, false)
            {
                myID = 1000,
                rightNeighborID = 1001,
                leftNeighborID = -1,
                upNeighborID = -1,
                downNeighborID = 0
            };
            m_NextSeasonButton = new ClickableTextureComponent(new Rectangle(this.xPositionOnScreen + 300, this.yPositionOnScreen + 80, 48, 44), Game1.mouseCursors, new Rectangle(365, 495, 12, 11), 4f, false)
            {
                myID = 1001,
                rightNeighborID = 1002,
                leftNeighborID = 1000,
                upNeighborID = -1,
                downNeighborID = 0
            };
            m_PrevYearButton = new ClickableTextureComponent(new Rectangle(this.xPositionOnScreen + 395, this.yPositionOnScreen + 80, 48, 44), Game1.mouseCursors, new Rectangle(352, 495, 12, 11), 4f, false)
            {
                myID = 1002,
                rightNeighborID = 1003,
                leftNeighborID = 1001,
                upNeighborID = -1,
                downNeighborID = 0
            };
            m_NextYearButton = new ClickableTextureComponent(new Rectangle(this.xPositionOnScreen + 600, this.yPositionOnScreen + 80, 48, 44), Game1.mouseCursors, new Rectangle(365, 495, 12, 11), 4f, false)
            {
                myID = 1003,
                rightNeighborID = -1,
                leftNeighborID = 1002,
                upNeighborID = -1,
                downNeighborID = 0
            };

            m_CalendarDaysBySeasonName = new Dictionary<string, List<ClickableTextureComponent>>();
            Dictionary<SDate, NPC> birthdayDict = new Dictionary<SDate, NPC>();
            foreach (NPC npc in Utility.getAllCharacters())
            {
                if ((npc.isVillager()) && (!string.IsNullOrEmpty(npc.Birthday_Season)) && ((Game1.player.friendshipData.ContainsKey(npc.Name)) || ((!npc.Name.Equals("Dwarf") && !npc.Name.Equals("Sandy")) && !npc.Name.Equals("Krobus"))))
                {
                    SDate npcBirthdate = new SDate(npc.Birthday_Day, npc.Birthday_Season);
                    if (!birthdayDict.ContainsKey(npcBirthdate))
                        birthdayDict.Add(npcBirthdate, npc);
                }
            }
            nightMarketLocalized = Game1.content.LoadString(@"Strings\UI:Billboard_NightMarket");
            wizardBirthdayLocalized = Game1.content.LoadString(@"Strings\UI:Billboard_Birthday", Game1.getCharacterFromName("Wizard", true).displayName);
            for (int seasonIndex = 0; seasonIndex <= 3; seasonIndex++)
            {
                string seasonName = (seasonIndex == 0 ? "spring" : seasonIndex == 1 ? "summer" : seasonIndex == 2 ? "fall" : "winter");
                m_CalendarDaysBySeasonName.Add(seasonName, new List<ClickableTextureComponent>());
                for (int dayIndex = 1; dayIndex <= 28; dayIndex++)
                {
                    SDate curDate = new SDate(dayIndex, seasonName);
                    string name = "";
                    string buttonHoverText = "";
                    NPC birthdayNPC = birthdayDict.ContainsKey(curDate) ? birthdayDict[curDate] : null;
                    if (Utility.isFestivalDay(dayIndex, seasonName))
                    {
                        name = Game1.temporaryContent.Load<Dictionary<string, string>>(@"Data\Festivals\" + seasonName + dayIndex)["name"];
                    }
                    else
                    {
                        if (birthdayNPC != null)
                        {
                            if ((birthdayNPC.displayName.Last<char>() == 's') ||
                                ((LocalizedContentManager.CurrentLanguageCode == LocalizedContentManager.LanguageCode.de) &&
                                    (((birthdayNPC.displayName.Last<char>() == 'x') ||
                                    (birthdayNPC.displayName.Last<char>() == '\x00df')) ||
                                    (birthdayNPC.displayName.Last<char>() == 'z'))))
                            {
                                buttonHoverText = Game1.content.LoadString(@"Strings\UI:Billboard_SBirthday", birthdayNPC.displayName);
                            }
                            else
                            {
                                buttonHoverText = Game1.content.LoadString(@"Strings\UI:Billboard_Birthday", birthdayNPC.displayName);
                            }
                        }
                        if ((seasonName == "winter") && (dayIndex >= 15) && (dayIndex <= 17))
                        {
                            name = nightMarketLocalized;
                        }
                    }
                    // TO-DO:
                    // paginate the seasons? nah, lose efficiency that way since each day would need to be checked for current season's holidays.
                    ClickableTextureComponent calendarDayButton = new ClickableTextureComponent(name, new Rectangle((base.xPositionOnScreen + 152) + ((((dayIndex - 1) % 7) * 32) * 4), (base.yPositionOnScreen + 200) + ((((dayIndex - 1) / 7) * 32) * 4), 124, 124), name, buttonHoverText, (birthdayNPC != null) ? birthdayNPC.Sprite.Texture : null, (birthdayNPC != null) ? new Rectangle(0, 0, 16, 24) : Rectangle.Empty, 1f, false)
                    {
                        myID = dayIndex,
                        rightNeighborID = ((dayIndex % 7) != 0) ? (dayIndex + 1) : -1,
                        leftNeighborID = ((dayIndex % 7) != 1) ? (dayIndex - 1) : -1,
                        downNeighborID = dayIndex + 7,
                        upNeighborID = (dayIndex > 7) ? (dayIndex - 7) : 1000
                    };
                    // we're skipping wedding days. remember your own wedding.
                    m_CalendarDaysBySeasonName[seasonName].Add(calendarDayButton);
                }
            }
        }

        private void PositionControls()
        {
            base.width = 1204;
            base.height = 792;
            Vector2 topLeftCornerWhenCentered = Utility.getTopLeftPositionForCenteringOnScreen(base.width, base.height, 0, 0);
            base.xPositionOnScreen = (int)topLeftCornerWhenCentered.X;
            base.yPositionOnScreen = (int)topLeftCornerWhenCentered.Y;

            base.upperRightCloseButton.bounds.X = (base.xPositionOnScreen + base.width) - 20;
            base.upperRightCloseButton.bounds.Y = (base.yPositionOnScreen);
            m_PrevSeasonButton.bounds.X = this.xPositionOnScreen + 113;
            m_PrevSeasonButton.bounds.Y = this.yPositionOnScreen + 80;
            m_NextSeasonButton.bounds.X = this.xPositionOnScreen + 300;
            m_NextSeasonButton.bounds.Y = this.yPositionOnScreen + 80;
            m_PrevYearButton.bounds.X = this.xPositionOnScreen + 395;
            m_PrevYearButton.bounds.Y = this.yPositionOnScreen + 80;
            m_NextYearButton.bounds.X = this.xPositionOnScreen + 600;
            m_NextYearButton.bounds.Y = this.yPositionOnScreen + 80;

            for (int seasonIndex = 0; seasonIndex <= 3; seasonIndex++)
            {
                string seasonName = (seasonIndex == 0 ? "spring" : seasonIndex == 1 ? "summer" : seasonIndex == 2 ? "fall" : "winter");
                for (int dayIndex = 1; dayIndex <= 28; dayIndex++)
                {
                    ClickableTextureComponent dayButton = m_CalendarDaysBySeasonName[seasonName][dayIndex - 1];
                    dayButton.bounds.X = (base.xPositionOnScreen + 152) + ((((dayIndex - 1) % 7) * 32) * 4);
                    dayButton.bounds.Y = (base.yPositionOnScreen + 200) + ((((dayIndex - 1) / 7) * 32) * 4);
                }
            }
        }

        public void BringToFront()
        {
            Utils.PrepareGameForInteractableMenu();
            m_ActiveSeason = Game1.currentSeason;
            m_ActiveYear = Game1.year;
            PositionControls();
        }

        public override void draw(SpriteBatch b)
        {
            // background and main panel
            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, (Color)(Color.Black * 0.75f));
            b.Draw(m_CalendarTex, new Vector2((float)base.xPositionOnScreen, (float)base.yPositionOnScreen), new Rectangle?(new Rectangle(0, 198, 301, 198)), Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 1f);

            // season + year header
            string seasonHeaderStr = Utility.getSeasonNameFromNumber(Utility.getSeasonNumber(m_ActiveSeason));
            string yearHeaderStr = Game1.content.LoadString(@"Strings\UI:Billboard_Year", m_ActiveYear);
            b.DrawString(Game1.dialogueFont, seasonHeaderStr, new Vector2((float)(base.xPositionOnScreen + 165), (float)(base.yPositionOnScreen + 80)), Game1.textColor);
            // this draws the number and the "Year" string. i think the number part is always an arabic numeral.
            b.DrawString(Game1.dialogueFont, yearHeaderStr, new Vector2((float)(base.xPositionOnScreen + 448), (float)(base.yPositionOnScreen + 80)), Game1.textColor);

            // draws left / right arrow buttons next to season and year (so 4 buttons here)
            // so, since i want the arrows next to the strings and the strings vary in size, i have to re-set the bounds before drawing
            // and calculate the width of the strings
            int seasonStrWidth = (int)Game1.dialogueFont.MeasureString(seasonHeaderStr).X;
            int yearStrWidth = (int)Game1.dialogueFont.MeasureString(yearHeaderStr).X;
            // yeah there's an extra arithmetic operation, but separating it out makes it clear that that's the padding.
            m_NextSeasonButton.bounds.X = ((base.xPositionOnScreen + 165) + seasonStrWidth + 5);
            m_NextYearButton.bounds.X = ((base.xPositionOnScreen + 448) + yearStrWidth + 5);
            // NOTE:
            // i *think* we have to call draw on the button itself for the hover scale to work
            if (m_ActiveSeason != "spring")
                m_PrevSeasonButton.draw(b);
            if (m_ActiveSeason != "winter")
                m_NextSeasonButton.draw(b);
            if (m_ActiveYear > 1)
                m_PrevYearButton.draw(b);
            m_NextYearButton.draw(b);

            // meat of the draw function, draws each day button
            List<ClickableTextureComponent> calendarDayButtons = m_CalendarDaysBySeasonName[m_ActiveSeason];
            ClickableTextureComponent activeCalendarDayButton;
            SDate activeDate, gameDate;
            for (int dayIndex = 0; dayIndex < calendarDayButtons.Count; dayIndex++)
            {
                activeDate = new SDate(dayIndex + 1, m_ActiveSeason, m_ActiveYear);
                gameDate = new SDate(Game1.dayOfMonth, Game1.currentSeason, Game1.year);
                activeCalendarDayButton = calendarDayButtons[dayIndex];
                if (activeCalendarDayButton.name.Length > 0)
                {
                    if (activeCalendarDayButton.name.Equals(nightMarketLocalized))
                    {
                        Utility.drawWithShadow(b, Game1.mouseCursors, new Vector2((float)(activeCalendarDayButton.bounds.X + 12), (activeCalendarDayButton.bounds.Y + 60) - (Game1.dialogueButtonScale / 2f)), new Rectangle(346, 392, 8, 8), Color.White, 0f, Vector2.Zero, 4f, false, 1f, -1, -1, 0.35f);
                    }
                    else
                    {
                        // NOTE:
                        // this draws the festival days that aren't night market days. it's animated in the sprite, that's what the game ticks are used for i think.
                        Utility.drawWithShadow(b, m_CalendarTex, new Vector2((float)(activeCalendarDayButton.bounds.X + 40), (activeCalendarDayButton.bounds.Y + 56) - (Game1.dialogueButtonScale / 2f)), new Rectangle(1 + (((int)((Game1.currentGameTime.TotalGameTime.TotalMilliseconds % 600.0) / 100.0)) * 14), 398, 14, 12), Color.White, 0f, Vector2.Zero, 4f, false, 1f, -1, -1, 0.35f);
                    }
                }
                if (activeCalendarDayButton.hoverText.Length > 0)
                {
                    // pretty sure this draws the villager icon if it's a birthday, the festival icon if it's a festival day, and the night market icon if... 
                    // well you see, it draws the night market icon if the night market icon should be drawn which is true under certain circumstances and those circumstances include whether the game's current season is winter and if the current day is between the integer 15 representing the 15th day of the winter season and the integer 17 representing the 17th day of the season the game is currently in which as mentioned before must be winter and only under these circumstances will the night market icon be drawn
                    b.Draw(activeCalendarDayButton.texture, new Vector2((float)(activeCalendarDayButton.bounds.X + 48), (float)(activeCalendarDayButton.bounds.Y + 28)), new Rectangle?(activeCalendarDayButton.sourceRect), Color.White, 0f, Vector2.Zero, (float)4f, SpriteEffects.None, 1f);
                }
                // draws icon if there are any tasks scheduled for this day
                if ((ModEntry.Instance != null) && (ModEntry.Instance.Schedule != null) && ((ModEntry.Instance.Schedule.GetNumTasksForDate(activeDate)) > 0))
                {
                    // draw like an exclamation point or something on the days that have tasks scheduled. in lower left probably.
                    // there's a suitable clock icon in cursors.png (Game1.mouseCursors), at sourceRect (434,475,9,9).
                    b.Draw(Game1.mouseCursors, new Vector2((float)(activeCalendarDayButton.bounds.X + 5), (float)(activeCalendarDayButton.bounds.Bottom - 31)), new Rectangle?(new Rectangle(434, 475, 9, 9)), Color.White, 0f, Vector2.Zero, 3f, SpriteEffects.None, 1f);
                }

                // overlays like... dim background if it's a previous day, blue border if it's the current day
                if (gameDate > activeDate)
                {
                    b.Draw(Game1.staminaRect, activeCalendarDayButton.bounds, (Color)(Color.Gray * 0.25f));
                }
                else if (gameDate == activeDate)
                {
                    int curDayBtnScale = (int)((4f * Game1.dialogueButtonScale) / 8f);
                    IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(379, 357, 3, 3), activeCalendarDayButton.bounds.X - curDayBtnScale, activeCalendarDayButton.bounds.Y - curDayBtnScale, activeCalendarDayButton.bounds.Width + (curDayBtnScale * 2), activeCalendarDayButton.bounds.Height + (curDayBtnScale * 2), Color.Blue, 4f, false);
                }
            }

            // draws permission box if in multiplayer
            /*
            IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(384, 373, 18, 18), this.xPositionOnScreen - 300, this.yPositionOnScreen, 300, this.height, Color.White, 4f, true);
            SpriteText.drawStringWithScrollCenteredAt(b, "Permissions", this.xPositionOnScreen - 150, this.yPositionOnScreen - 64, "", 1f, -1, 0, 0.88f, false);
            Vector2 farmerPermissionsStartOffset = new Vector2(this.xPositionOnScreen - 288, this.yPositionOnScreen + 24);
            int farmerPermissionsHeight = 75;
            int curFarmerDrawIndex = 0;
            foreach (Farmer farmer in Game1.getOnlineFarmers())
            {
                farmer.FarmerRenderer.drawMiniPortrat(b, new Vector2(farmerPermissionsStartOffset.X, farmerPermissionsStartOffset.Y + (farmerPermissionsHeight * curFarmerDrawIndex)), 0.00011f, 3f, 2, farmer);
                Vector2 farmerNameDimensions = Game1.dialogueFont.MeasureString(farmer.Name);
                Utility.drawTextWithShadow(b, farmer.Name, Game1.dialogueFont, new Vector2(farmerPermissionsStartOffset.X + 48f, farmerPermissionsStartOffset.Y + (farmerPermissionsHeight * curFarmerDrawIndex)), Game1.textColor, 1f, -1f, -1, -1, 1f, 3);

                bool shouldCheckboxesBeDisabled = false;
                if (farmer.UniqueMultiplayerID == Game1.MasterPlayer.UniqueMultiplayerID)
                {
                    // the host should have full permissions and the checkboxes should be disabled but visible
                    shouldCheckboxesBeDisabled = true;
                }
                IMultiplayerPeer smapiPeer = ModEntry.Instance.Helper.Multiplayer.GetConnectedPlayer(farmer.UniqueMultiplayerID);
                if ((smapiPeer == null) || (!smapiPeer.HasSmapi) || (smapiPeer.GetMod("kotae.SchedulePlanner") == null))
                {
                    // instead of checkboxes, draw a "-- NO MOD --" string.
                }
                else
                {
                    // draw checkboxes w/ tick state set already
                    //b.Draw(Game1.mouseCursors, new Vector2(farmerPermissionsStartOffset.X, farmerPermissionsStartOffset.Y + (farmerPermissionsHeight * curFarmerDrawIndex) + 48f), new Rectangle?(1 == 1 ? OptionsCheckbox.sourceRectChecked : OptionsCheckbox.sourceRectUnchecked), (Color)(Color.White * (shouldCheckboxesBeDisabled ? 1f : 0.33f)), 0f, Vector2.Zero, (float)4f, SpriteEffects.None, 0.4f);
                }
                // NOTE:
                // just to debug
                b.Draw(Game1.mouseCursors, new Vector2(farmerPermissionsStartOffset.X + 12f, farmerPermissionsStartOffset.Y + (farmerPermissionsHeight * curFarmerDrawIndex) + 52f), new Rectangle?(1 == 1 ? OptionsCheckbox.sourceRectChecked : OptionsCheckbox.sourceRectUnchecked), (Color)(Color.White * (shouldCheckboxesBeDisabled ? 1f : 0.33f)), 0f, Vector2.Zero, (float)4f, SpriteEffects.None, 0.4f);
                curFarmerDrawIndex++;
            }
            */

            // close button (only thing done in base), the mouse, and hover text
            base.draw(b);
            Game1.mouseCursorTransparency = 1f;
            base.drawMouse(b);
            if (hoverText.Length > 0)
            {
                IClickableMenu.drawHoverText(b, hoverText, Game1.dialogueFont, 0, 0, -1, null, -1, null, null, 0, -1, -1, -1, -1, 1f, null, null);
            }
        }

        private void DrawPermissionsForPlayer(Farmer farmer, int drawIndex)
        {

        }

        public override void performHoverAction(int x, int y)
        {
            // base performs animation on close button i think
            base.performHoverAction(x, y);
            hoverText = "";
            List<ClickableTextureComponent> calendarDayButtons = null;
            if ((m_CalendarDaysBySeasonName != null) && (m_CalendarDaysBySeasonName.TryGetValue(m_ActiveSeason, out calendarDayButtons)))
            {
                ClickableTextureComponent dayButton;
                SDate activeDate;
                List<string> dayTasks;
                for (int dayIndex = 0; dayIndex < calendarDayButtons.Count; dayIndex++)
                {
                    activeDate = new SDate(dayIndex + 1, m_ActiveSeason, m_ActiveYear);
                    dayButton = calendarDayButtons[dayIndex];
                    if (dayButton.bounds.Contains(x, y))
                    {
                        if (dayButton.hoverText.Length > 0)
                        {
                            if (dayButton.hoverText.Equals(wizardBirthdayLocalized))
                            {
                                dayButton.hoverText = dayButton.hoverText + Environment.NewLine + nightMarketLocalized;
                            }
                            hoverText = dayButton.hoverText;
                        }
                        else
                        {
                            hoverText = dayButton.label;
                        }
                        int numNonrecurrent, numWeeklies, numDailies;
                        int numTasks = ModEntry.Instance.Schedule.GetNumTasksForDate(activeDate, out numNonrecurrent, out numWeeklies, out numDailies);
                        if ((ModEntry.Instance != null) && (ModEntry.Instance.Schedule != null) && (numTasks > 0))
                        {
                            if (numDailies > 0)
                                hoverText = hoverText + Environment.NewLine + numDailies.ToString() + " daily task" + (numDailies > 1 ? "s planned" : " planned");
                            if (numWeeklies > 0)
                                hoverText = hoverText + Environment.NewLine + numWeeklies.ToString() + " weekly task" + (numWeeklies > 1 ? "s planned" : " planned");
                            if (numNonrecurrent > 0)
                                hoverText = hoverText + Environment.NewLine + numNonrecurrent.ToString() + " task" + (numNonrecurrent > 1 ? "s planned" : " planned");
                        }

                        hoverText = hoverText.Trim();
                    }
                }
            }
            m_PrevSeasonButton.tryHover(x, y, 0.15f);
            m_NextSeasonButton.tryHover(x, y, 0.15f);
            m_PrevYearButton.tryHover(x, y, 0.15f);
            m_NextYearButton.tryHover(x, y, 0.15f);
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            // base checks if the close button was clicked and handles that
            base.receiveLeftClick(x, y, playSound);
            if (m_PrevSeasonButton.containsPoint(x, y))
            {
                m_ActiveSeason = m_ActiveSeason == "summer" ? "spring" : m_ActiveSeason == "fall" ? "summer" : m_ActiveSeason == "winter" ? "fall" : "spring";
            }
            else if (m_NextSeasonButton.containsPoint(x, y))
            {
                m_ActiveSeason = m_ActiveSeason == "spring" ? "summer" : m_ActiveSeason == "summer" ? "fall" : "winter";
            }
            else if (m_PrevYearButton.containsPoint(x, y))
            {
                m_ActiveYear = m_ActiveYear > 1 ? m_ActiveYear - 1 : 1;
                m_ActiveSeason = "winter";
            }
            else if (m_NextYearButton.containsPoint(x, y))
            {
                m_ActiveYear += 1;
                m_ActiveSeason = "spring";
            }
            else
            {
                List<ClickableTextureComponent> calendarDayButtons = null;
                if ((m_CalendarDaysBySeasonName != null) && (m_CalendarDaysBySeasonName.TryGetValue(m_ActiveSeason, out calendarDayButtons)))
                {
                    ClickableTextureComponent dayButton;
                    SDate activeDate;
                    for (int dayIndex = 0; dayIndex < calendarDayButtons.Count; dayIndex++)
                    {
                        activeDate = new SDate(dayIndex + 1, m_ActiveSeason, m_ActiveYear);
                        dayButton = calendarDayButtons[dayIndex];
                        if (dayButton.bounds.Contains(x, y))
                        {
                            // TO-DO:
                            // pull up the Schedule menu to this date, whether it has tasks already or not.
                            Game1.playSound("smallSelect");
                            if (m_AgendaMenu == null)
                                m_AgendaMenu = new AgendaMenu(activeDate);
                            else
                                m_AgendaMenu.BringToFront(activeDate);
                            Game1.activeClickableMenu = m_AgendaMenu;
                        }
                    }
                }
            }
        }

        public override void receiveRightClick(int x, int y, bool playSound = true)
        {
            // base doesn't have an implementation
            // keeping this in case i decide to implement a context menu or something. i dunno.
        }

        public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
        {
            base.gameWindowSizeChanged(oldBounds, newBounds);
            this.BringToFront();
            Game1.activeClickableMenu = this;
        }
    }
}
