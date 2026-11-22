using System;
using CircuitCube.Core;
using UnityEngine;

namespace CircuitCube.Game
{
    /// <summary>
    /// Sample leaderboards for the editor and development builds (debug menu F4), so the leaderboard screens can be
    /// checked and screenshotted before the backend is live: a weekly challenge for every shape that ends in 2d 14h,
    /// levels 1 and 2 of the current shape solved, and boards with the rows of the approved mockup.
    /// Nothing here is sent or saved.
    /// </summary>
    public static class RanksDebug
    {
        public static bool On;
        public const string WeekId = "debug-week";
        public const string MyName = "Tuan_Pro#4821";

        static readonly string[] Names =
        {
            "LinhNeon#0193", "Khoa.dev#7720", "mai_cube#3304", "Volt#5561", "PhucTran#1288", "Sparky#9047", "an_an#2210", "Circuitry#6402",
        };

        /// <summary>When the sample week opened and closes (UTC): it ends 2 days 14 hours from now.</summary>
        public static void Window(out DateTime opens, out DateTime closes)
        {
            closes = DateTime.UtcNow.AddDays(2).AddHours(14).AddMinutes(30);
            opens = closes.AddDays(-7);
        }

        /// <summary>The sample best run of a weekly slot, or false when it is "not solved yet" (slot 3).</summary>
        public static bool Best(int slot, out int moves, out int tenths)
        {
            moves = slot == 1 ? 14 : 27;
            tenths = slot == 1 ? 214 : 729;
            return slot <= 2;
        }

        /// <summary>A sample page of <paramref name="board"/>; <paramref name="levels"/> sizes the shape boards.</summary>
        public static BoardPage Page(string board, int shapeIndex, int levels)
        {
            var page = new BoardPage { Board = board, Ok = true };
            int k = Mathf.Max(0, shapeIndex);
            if (board.StartsWith("shape_"))
            {
                int[] behind = { 0, 0, 1, 3, 4, 4, 6, 9 };
                page.Players = 3207;
                for (int i = 0; i < 8; i++)
                {
                    bool me = i == 4;
                    int experts = Mathf.Max(0, levels - behind[i]);
                    long moves = experts * 24L + i * 7L, tenths = experts * 268L + i * 413L;
                    var row = new BoardRow { Rank = i + 1, Name = me ? MyName : Names[i], Score = RankScore.Shape(experts, moves, tenths), Me = me };
                    page.Top.Add(row);
                    if (me) page.Mine = row;
                }
                return page;
            }
            bool total = !char.IsDigit(board[board.Length - 1]);
            int m = total ? 61 + k * 9 : 21 + k * 3, t = total ? 1450 + k * 300 : 312 + k * 60, step = total ? 260 : 37;
            page.Players = total ? 640 : 902;
            for (int i = 0; i < 7; i++)
                page.Top.Add(new BoardRow { Rank = i + 1, Name = Names[i], Score = RankScore.Weekly(m + (i < 3 ? 0 : i < 5 ? 1 : 2), t + i * step) });
            if (board.EndsWith("_1")) page.Mine = new BoardRow { Rank = 8, Name = MyName, Score = RankScore.Weekly(14, 214), Me = true };
            else if (board.EndsWith("_2")) page.Mine = new BoardRow { Rank = 19, Name = MyName, Score = RankScore.Weekly(m + 6, t + 417), Me = true };
            if (board.EndsWith("_1")) page.Players = 1412;
            return page;
        }
    }
}
