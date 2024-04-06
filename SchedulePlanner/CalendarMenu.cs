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
using StardewValley.GameData.Characters;
using StardewValley.GameData;
using StardewValley.TokenizableStrings;
using StardewValley.Network;
using static StardewValley.Menus.Billboard;

namespace kotae.SchedulePlanner
{
    public class CalendarMenu : IClickableMenu
    {
        // NOTE: see StardewValley.Utility.getDaysOfBooksellerThisSeason()
        static readonly int[] PossibleBookSellerDays_Spring = new int[5] { 11, 12, 21, 22, 25 };
        static readonly int[] PossibleBookSellerDays_Summer = new int[5] { 9, 12, 18, 25, 27 };
        static readonly int[] PossibleBookSellerDays_Fall = new int[8] { 4, 7, 8, 9, 12, 19, 22, 25 };
        static readonly int[] PossibleBookSellerDays_Winter = new int[6] { 5, 11, 12, 19, 22, 24 };

        Texture2D m_CalendarTex;
        string nightMarketLocalized = "", wizardBirthdayLocalized = "", hoverText = "";
        ClickableTextureComponent m_PrevSeasonButton, m_NextSeasonButton, m_PrevYearButton, m_NextYearButton;
        AgendaMenu m_AgendaMenu;
        // game uses:
        // "spring", "summer", "fall", "winter" case sensitive, non-localized
        Dictionary<Season, List<ClickableTextureComponent>> m_CalendarDaysBySeasonName;
        Dictionary<SDate, List<NPC>> m_BirthdayDict;
        List<Billboard.BillboardEvent> m_BillboardEventsByDay = new List<Billboard.BillboardEvent>();
        // a BillboardDay is a collection of BillboardEvents with aggregate HoverText and Type properties
        Dictionary<int, Billboard.BillboardDay> m_BillboardDayByDay = new Dictionary<int, BillboardDay>();
        List<int> m_BookSellerDaysThisSeason = new List<int>();

        Season m_ActiveSeason = Season.Spring;
        int m_ActiveYear = 1;


        public CalendarMenu() : base(0, 0, 0, 0, false)
        {
            // NOTE:
            // we kinda need to instantiate a new instance each time because the base ctor sets game state (time stops, player movement halts, etc)
            // but instantiating all these assets each and every time is really inefficient, so i'm thinking we make them static and have a static bool HasInitializedAssets and check that each time.
            // should speed things up. not that they're slow right now, but faster is faster than fast, so let's aim for faster always.
            // REVISIT: i did not do the above and i can't remember why.
            // NOTE:
            // so, we want our calendar to show everything the game's calendar shows, BUT also:
            // 1. left / right arrows next to the Season that'll load up the prev / next season (do you like writing obvious things? no)
            //     * the season text will probably have to be moved a bit to the right to accomodate the left button (i thought the season was centered, but nope it's left-aligned)
            // 2. a new icon that denotes tasks scheduled for that day (probably in lower left of the day, below where the night market icon shows in winter)
            // 3. when hovering over a day with scheduled task, the hoverText should show "X tasks planned" or something in addition to the game's usual hoverText (if any)
            // it's worth noting that the festival and the night market icons are animated...
            // also that villager sprites (to denote birthdays) vary in height, so top right might not be a good idea for our icon
            // finally, we don't need to display the wedding icon on the calendar since the ceremony isn't miss-able, skippable, nor does it effect gameplay in any way (time freezes at 6am while the cutscene happens)
            m_ActiveSeason = Game1.season;
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

            m_CalendarDaysBySeasonName = new Dictionary<Season, List<ClickableTextureComponent>>();
            m_BirthdayDict = new Dictionary<SDate, List<NPC>>();
            PopulateBirthdays(ref m_BirthdayDict);
            PopulateEventsForSeason(m_ActiveYear, m_ActiveSeason, m_BirthdayDict, ref m_BillboardDayByDay);
            for (int seasonIndex = 0; seasonIndex <= 3; seasonIndex++)
            {
                Season season = (seasonIndex == 0 ? Season.Spring : seasonIndex == 1 ? Season.Summer : seasonIndex == 2 ? Season.Fall : Season.Winter);
                //string seasonName = season.ToString().ToLower();
                m_CalendarDaysBySeasonName.Add(season, new List<ClickableTextureComponent>());
                for (int dayIndex = 1; dayIndex <= 28; dayIndex++)
                {
                    SDate curDate = new SDate(dayIndex, season);
                    ClickableTextureComponent calendarDayButton = new ClickableTextureComponent(curDate.ToString(), new Rectangle((base.xPositionOnScreen + 152) + ((((dayIndex - 1) % 7) * 32) * 4), (base.yPositionOnScreen + 200) + ((((dayIndex - 1) / 7) * 32) * 4), 124, 124), string.Empty, string.Empty, null, Rectangle.Empty, 1f, false)
                    {
                        myID = dayIndex,
                        rightNeighborID = ((dayIndex % 7) != 0) ? (dayIndex + 1) : -1,
                        leftNeighborID = ((dayIndex % 7) != 1) ? (dayIndex - 1) : -1,
                        downNeighborID = dayIndex + 7,
                        upNeighborID = (dayIndex > 7) ? (dayIndex - 7) : -1
                    };
                    // we're skipping wedding days. remember your own wedding.
                    m_CalendarDaysBySeasonName[season].Add(calendarDayButton);
                }
            }
        }

        private void PopulateBirthdays(ref Dictionary<SDate, List<NPC>> birthdayDict)
        {
            birthdayDict.Clear();
            foreach (NPC npc in Utility.getAllCharacters())
            {
                CalendarBehavior? calendarBehavior = npc.GetData()?.Calendar;
                if ((calendarBehavior == null)
                    || (calendarBehavior == CalendarBehavior.HiddenAlways)
                    || ((calendarBehavior == CalendarBehavior.HiddenUntilMet) && (Game1.player.friendshipData.ContainsKey(npc.Name) == false))
                    || (npc.IsVillager == false)
                    || (string.IsNullOrEmpty(npc.Birthday_Season)))
                {
                    continue;
                }
                SDate npcBirthdate = new SDate(npc.Birthday_Day, npc.Birthday_Season);
                if (birthdayDict.ContainsKey(npcBirthdate))
                {
                    birthdayDict[npcBirthdate].Add(npc);
                }
                else
                {
                    birthdayDict.Add(npcBirthdate, new List<NPC>() { npc });
                }
            }
        }

        private void PopulateBookSellerDaysForSeason(int year, int seasonIndex, ref List<int> bookSellerDaysThisSeason)
        {
            bookSellerDaysThisSeason.Clear();
            Random rnd = Utility.CreateRandom(year * 11, Game1.uniqueIDForThisGame, seasonIndex);
            int[] possibleDays;
            switch (seasonIndex)
            {
                case 0:
                    possibleDays = PossibleBookSellerDays_Spring;
                    break;
                case 1:
                    possibleDays = PossibleBookSellerDays_Summer;
                    break;
                case 2:
                    possibleDays = PossibleBookSellerDays_Fall;
                    break;
                case 3:
                    possibleDays = PossibleBookSellerDays_Winter;
                    break;
                default:
                    throw new Exception($"Invalid season index: {seasonIndex}");
            }
            int randIndex = rnd.Next(possibleDays.Length);
            bookSellerDaysThisSeason.Add(possibleDays[randIndex]);
            bookSellerDaysThisSeason.Add(possibleDays[(randIndex + possibleDays.Length / 2) % possibleDays.Length]);
        }

        private void PopulateEventsForSeason(int year, Season season, Dictionary<SDate, List<NPC>> cachedBirthdays, ref Dictionary<int, Billboard.BillboardDay> eventsData)
        {
            int seasonIndex = (int)season;
            string seasonName = Utility.getSeasonNameFromNumber(seasonIndex);
            eventsData.Clear();
            PopulateBookSellerDaysForSeason(year, seasonIndex, ref m_BookSellerDaysThisSeason);
            SDate activeDate;
            PassiveFestivalData passiveFestivalData;
            string festivalId;
            string festivalName;
            for (int day = 1; day <= 28; day++)
            {
                m_BillboardEventsByDay.Clear();
                activeDate = new SDate(day, seasonName);
                if (Utility.isFestivalDay(day, season))
                {
                    festivalId = seasonName + day.ToString();
                    festivalName = Game1.temporaryContent.Load<Dictionary<string, string>>(@"Data\Festivals\" + festivalId)["name"];
                    m_BillboardEventsByDay.Add(new Billboard.BillboardEvent(Billboard.BillboardEventType.Festival, new string[1] { festivalId }, festivalName));
                }
                if ((Utility.TryGetPassiveFestivalDataForDay(day, season, null, out festivalId, out passiveFestivalData, ignoreConditionsCheck: true))
                    && (passiveFestivalData?.ShowOnCalendar ?? false))
                {
                    festivalName = TokenParser.ParseText(passiveFestivalData.DisplayName);
                    if (GameStateQuery.CheckConditions(passiveFestivalData.Condition) == false)
                    {
                        m_BillboardEventsByDay.Add(new Billboard.BillboardEvent(Billboard.BillboardEventType.PassiveFestival, new string[1] { festivalId }, "???")
                        {
                            locked = true
                        });
                    }
                    else
                    {
                        m_BillboardEventsByDay.Add(new Billboard.BillboardEvent(Billboard.BillboardEventType.PassiveFestival, new string[1] { festivalId }, festivalName));
                    }
                }
                if ((season == Season.Summer) && ((day == 20) || (day == 21)))
                {
                    festivalName = Game1.content.LoadString(@"Strings\1_6_Strings:TroutDerby");
                    m_BillboardEventsByDay.Add(new Billboard.BillboardEvent(Billboard.BillboardEventType.FishingDerby, Array.Empty<string>(), festivalName));
                }
                else if ((season == Season.Winter) && ((day == 12) || (day == 13)))
                {
                    festivalName = Game1.content.LoadString(@"Strings\1_6_Strings:SquidFest");
                    m_BillboardEventsByDay.Add(new Billboard.BillboardEvent(Billboard.BillboardEventType.FishingDerby, Array.Empty<string>(), festivalName));
                }
                if (m_BookSellerDaysThisSeason.Contains(day))
                {
                    festivalName = Game1.content.LoadString(@"Strings\1_6_Strings:Bookseller");
                    m_BillboardEventsByDay.Add(new Billboard.BillboardEvent(Billboard.BillboardEventType.Bookseller, Array.Empty<string>(), festivalName));
                }
                foreach (KeyValuePair<SDate, List<NPC>> kv in cachedBirthdays)
                {
                    if ((kv.Key.Season != season) || (kv.Key.Day != day))
                        continue;
                    foreach (NPC npc in kv.Value)
                    {
                        char lastChar = npc.displayName.Last();
                        string displayText = (((lastChar == 's')
                            || ((LocalizedContentManager.CurrentLanguageCode == LocalizedContentManager.LanguageCode.de)
                            && ((lastChar == 'x') || (lastChar == 'ß') || (lastChar == 'z'))))
                            ? Game1.content.LoadString("Strings\\UI:Billboard_SBirthday", npc.displayName)
                            : Game1.content.LoadString("Strings\\UI:Billboard_Birthday", npc.displayName));
                        Texture2D npcTexture;
                        try
                        {
                            npcTexture = Game1.content.Load<Texture2D>(@"Characters\" + npc.getTextureName());
                        }
                        catch
                        {
                            npcTexture = npc.Sprite.Texture;
                        }
                        m_BillboardEventsByDay.Add(new Billboard.BillboardEvent(Billboard.BillboardEventType.Birthday, new string[1] { npc.Name }, displayText, npcTexture, npc.getMugShotSourceRect()));
                    }
                }
                HashSet<Farmer> seenFarmers = new HashSet<Farmer>();
                FarmerCollection onlineFarmers = Game1.getOnlineFarmers();
                foreach (Farmer farmer in onlineFarmers)
                {
                    if ((seenFarmers.Contains(farmer)) || (farmer.isEngaged() == false) || (farmer.hasCurrentOrPendingRoommate()))
                        continue;
                    string spouseName = "";
                    WorldDate weddingDate = null;
                    NPC spouse = Game1.getCharacterFromName(farmer.spouse);
                    if (spouse != null)
                    {
                        weddingDate = farmer.friendshipData[farmer.spouse].WeddingDate;
                        spouseName = spouse.displayName;
                    }
                    else
                    {
                        long? spouseId = farmer.team.GetSpouse(farmer.UniqueMultiplayerID);
                        if (spouseId.HasValue)
                        {
                            Farmer spouseFarmer = Game1.getFarmerMaybeOffline(spouseId.Value);
                            if ((spouseFarmer != null) && (onlineFarmers.Contains(spouseFarmer)))
                            {
                                weddingDate = farmer.team.GetFriendship(farmer.UniqueMultiplayerID, spouseId.Value).WeddingDate;
                                spouseName = spouseFarmer.Name;
                                seenFarmers.Add(spouseFarmer);
                            }
                        }
                    }
                    if (weddingDate != null)
                    {
                        if (weddingDate.TotalDays < Game1.Date.TotalDays)
                        {
                            // wedding is still pending, will occur next day
                            // does not check for rain or festival days
                            weddingDate = new WorldDate(Game1.Date);
                            weddingDate.TotalDays++;
                        }
                        if ((weddingDate.Season == season) && (weddingDate.DayOfMonth == day))
                        {
                            m_BillboardEventsByDay.Add(new Billboard.BillboardEvent(Billboard.BillboardEventType.Wedding, new string[2] { farmer.Name, spouseName }, Game1.content.LoadString(@"Strings\UI:Calendar_Wedding", farmer.Name, spouseName)));
                            seenFarmers.Add(farmer);
                        }
                    }
                }
                if (m_BillboardEventsByDay.Count > 0)
                {
                    Billboard.BillboardDay billboardDay = new Billboard.BillboardDay(m_BillboardEventsByDay.ToArray());
                    eventsData.Add(day, billboardDay);
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
                Season seasonName = (seasonIndex == 0 ? Season.Spring : seasonIndex == 1 ? Season.Summer : seasonIndex == 2 ? Season.Fall : Season.Winter);
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
            bool hasSeasonChanged = false;
            if (m_ActiveSeason != Game1.season)
                hasSeasonChanged = true;
            m_ActiveSeason = Game1.season;
            m_ActiveYear = Game1.year;
            PositionControls();
            PopulateBirthdays(ref m_BirthdayDict);
            if (hasSeasonChanged)
                PopulateEventsForSeason(m_ActiveYear, m_ActiveSeason, m_BirthdayDict, ref m_BillboardDayByDay);
        }

        public override void draw(SpriteBatch b)
        {
            // background and main panel
            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, (Color)(Color.Black * 0.75f));
            b.Draw(m_CalendarTex, new Vector2((float)base.xPositionOnScreen, (float)base.yPositionOnScreen), new Rectangle(0, 198, 301, 198), Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 1f);

            // season + year header
            string seasonHeaderStr = Utility.getSeasonNameFromNumber((int)m_ActiveSeason);
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
            if (m_ActiveSeason != Season.Spring)
                m_PrevSeasonButton.draw(b);
            if (m_ActiveSeason != Season.Winter)
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
                gameDate = new SDate(Game1.dayOfMonth, Game1.season, Game1.year);
                activeCalendarDayButton = calendarDayButtons[dayIndex];
                Billboard.BillboardDay billboardDay;
                if (m_BillboardDayByDay.TryGetValue(activeCalendarDayButton.myID, out billboardDay))
                {
                    // hovertext is event.DisplayName
                    if (billboardDay.Texture != null)
                    {
                        b.Draw(billboardDay.Texture,
                            new Vector2(activeCalendarDayButton.bounds.X + 48, activeCalendarDayButton.bounds.Y + 28), 
                            billboardDay.TextureSourceRect, Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 1f);
                    }
                    if (billboardDay.Type.HasFlag(Billboard.BillboardEventType.PassiveFestival))
                    {
                        Utility.drawWithShadow(b, Game1.mouseCursors, 
                            new Vector2(activeCalendarDayButton.bounds.X + 12, (float)(activeCalendarDayButton.bounds.Y + 60) - Game1.dialogueButtonScale / 2f), 
                            new Rectangle(346, 392, 8, 8), 
                            billboardDay.GetEventOfType(BillboardEventType.PassiveFestival).locked ? (Color.Black * 0.3f) : Color.White, 
                            0f, Vector2.Zero, 4f, flipped: false, 1f);
                    }
                    if (billboardDay.Type.HasFlag(Billboard.BillboardEventType.Festival))
                    {
                        Utility.drawWithShadow(b, m_CalendarTex, 
                            new Vector2(activeCalendarDayButton.bounds.X + 40, (float)(activeCalendarDayButton.bounds.Y + 56) - Game1.dialogueButtonScale / 2f), 
                            new Rectangle(1 + (int)(Game1.currentGameTime.TotalGameTime.TotalMilliseconds % 600.0 / 100.0) * 14, 398, 14, 12), 
                            Color.White, 0f, Vector2.Zero, 4f, flipped: false, 1f);
                    }
                    if (billboardDay.Type.HasFlag(Billboard.BillboardEventType.FishingDerby))
                    {
                        Utility.drawWithShadow(b, Game1.mouseCursors_1_6, 
                            new Vector2(activeCalendarDayButton.bounds.X + 8, (float)(activeCalendarDayButton.bounds.Y + 60) - Game1.dialogueButtonScale / 2f), 
                            new Rectangle(103, 2, 10, 11), 
                            Color.White, 0f, Vector2.Zero, 4f, flipped: false, 1f);
                    }
                    if (billboardDay.Type.HasFlag(Billboard.BillboardEventType.Wedding))
                    {
                        b.Draw(Game1.mouseCursors2, 
                            new Vector2(activeCalendarDayButton.bounds.Right - 56, activeCalendarDayButton.bounds.Top - 12), 
                            new Rectangle(112, 32, 16, 14), 
                            Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 1f);
                    }
                    if (billboardDay.Type.HasFlag(Billboard.BillboardEventType.Bookseller))
                    {
                        b.Draw(Game1.mouseCursors_1_6, 
                            new Vector2((float)(activeCalendarDayButton.bounds.Right - 72) - 2f * (float)Math.Sin((Game1.currentGameTime.TotalGameTime.TotalSeconds + (double)dayIndex * 0.3) * 3.0), (float)(activeCalendarDayButton.bounds.Top + 52) - 2f * (float)Math.Cos((Game1.currentGameTime.TotalGameTime.TotalSeconds + (double)dayIndex * 0.3) * 2.0)), 
                            new Rectangle(71, 63, 8, 15), 
                            Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 1f);
                    }
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
                IClickableMenu.drawHoverText(b, hoverText, Game1.dialogueFont);
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
                    BillboardDay dayEventData;
                    if (dayButton.bounds.Contains(x, y))
                    {
                        hoverText = (m_BillboardDayByDay.TryGetValue(dayButton.myID, out dayEventData) ? dayEventData.HoverText : string.Empty);
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
            bool hasSeasonChanged = false;
            if (m_PrevSeasonButton.containsPoint(x, y))
            {
                m_ActiveSeason = m_ActiveSeason == Season.Summer ? Season.Spring : m_ActiveSeason == Season.Fall ? Season.Summer : m_ActiveSeason == Season.Winter ? Season.Fall : Season.Spring;
                hasSeasonChanged = true;
            }
            else if (m_NextSeasonButton.containsPoint(x, y))
            {
                m_ActiveSeason = m_ActiveSeason == Season.Spring ? Season.Summer : m_ActiveSeason == Season.Summer ? Season.Fall : Season.Winter;
                hasSeasonChanged = true;
            }
            else if (m_PrevYearButton.containsPoint(x, y))
            {
                m_ActiveYear = m_ActiveYear > 1 ? m_ActiveYear - 1 : 1;
                m_ActiveSeason = Season.Winter;
                hasSeasonChanged = true;
            }
            else if (m_NextYearButton.containsPoint(x, y))
            {
                m_ActiveYear += 1;
                m_ActiveSeason = Season.Spring;
                hasSeasonChanged = true;
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
            if (hasSeasonChanged)
            {
                PopulateEventsForSeason(m_ActiveYear, m_ActiveSeason, m_BirthdayDict, ref m_BillboardDayByDay);
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
