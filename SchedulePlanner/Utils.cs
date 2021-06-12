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

namespace kotae.SchedulePlanner
{
    // the class everyone creates at some point in their life.. different every time.
    // this one just does all the background work associated with bringing up an interactable menu (pauses time, halts player movement, etc)
    public static class Utils
    {
        /// <summary>
        /// copied from game's IClickableMenu::ctor(...), sets up game state for the interactable menu.
        /// </summary>
        public static void PrepareGameForInteractableMenu()
        {
            Game1.mouseCursorTransparency = 1f;
            // other things here
            if (((Game1.player != null) && (!Game1.player.UsingTool)) && (!Game1.eventUp))
            {
                Game1.player.forceCanMove();
            }
            for (int i = 0; i < 4; i++)
            {
                Game1.directionKeyPolling[i] = 250;
            }
            // end other things
            if (((Game1.gameMode == 3) && (Game1.player != null)) && (!Game1.eventUp))
            {
                Game1.player.Halt();
            }
        }

        public static bool GetDateFromString(string dateString, out SDate date)
        {
            // TO-DO:
            // clean this up. exceptions or return false, one or the other.
            // i also don't like instantiating a default date just to return false.
            date = new SDate(1, "spring", 1);
            string[] parts = dateString.Split(' ');
            if ((parts.Length != 3) || (parts[2][0] != 'Y')) throw new ArgumentException("dateString is not an SDate");
            int day = -1;
            if (!int.TryParse(parts[0], out day))
            {
                return false;
            }
            string yearSubStr = parts[2].Substring(1);
            int year = -1;
            if (!int.TryParse(yearSubStr, out year))
            {
                return false;
            }
            date = new SDate(day, parts[1], year);
            return true;
        }
    }
}
