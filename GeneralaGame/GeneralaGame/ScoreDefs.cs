using System;
using System.Collections.Generic;
using System.Linq;

namespace GeneralaGame
{
    public enum ScoreCategory
    {
        Ones, Twos, Threes, Fours, Fives, Sixes,
        Straight, FullHouse, FourOfAKind, Generala
    }

    public enum AIDifficulty { Easy, Medium, Hard }

    // Вимога ТЗ: стартові 1-1-1-1-1 не приймати як валідний «кидок» (заглушка)
    public class InvalidInitialRollException : Exception
    {
        public InvalidInitialRollException()
            : base("Initial placeholder 1-1-1-1-1 cannot be accepted as a rolled result.") { }
    }

    public static class DiceUtil
    {
        public static readonly Random Rng = new Random();

        public static int[] Copy(int[] a)
        {
            var b = new int[a.Length];
            Array.Copy(a, b, a.Length);
            return b;
        }

        public static int[] RollFresh(int n = 5)
        {
            var d = new int[n];
            for (int i = 0; i < n; i++) d[i] = Rng.Next(1, 7);
            return d;
        }

        // holdMask[i] == true => утримати (не кидати), false => кидати
        public static int[] RollWithMask(int[] dice, bool[] holdMask)
        {
            var res = new int[5];
            for (int i = 0; i < 5; i++)
                res[i] = holdMask[i] ? dice[i] : Rng.Next(1, 7);
            return res;
        }

        public static string DiceToString(int[] d)
        {
            char Face(int v) { return (char)('\u2680' + (v - 1)); } // ⚀..⚅
            return string.Concat(d.Select(Face));
        }

        public static int[] Counts(int[] d)
        {
            var c = new int[7];
            for (int i = 0; i < d.Length; i++) c[d[i]]++;
            return c;
        }

        public static bool AllOnes(int[] d)
        {
            for (int i = 0; i < d.Length; i++) if (d[i] != 1) return false;
            return true;
        }
    }

    public static class ScoreCalculator
    {
        public static int Score(ScoreCategory cat, int[] dice, bool firstRollServed)
        {
            switch (cat)
            {
                case ScoreCategory.Ones: return SumOf(1, dice);
                case ScoreCategory.Twos: return SumOf(2, dice);
                case ScoreCategory.Threes: return SumOf(3, dice);
                case ScoreCategory.Fours: return SumOf(4, dice);
                case ScoreCategory.Fives: return SumOf(5, dice);
                case ScoreCategory.Sixes: return SumOf(6, dice);

                case ScoreCategory.Straight:
                    if (IsStraight(dice)) return 20 + (firstRollServed ? 5 : 0);
                    return 0;

                case ScoreCategory.FullHouse:
                    if (IsFullHouse(dice)) return 30 + (firstRollServed ? 5 : 0);
                    return 0;

                case ScoreCategory.FourOfAKind:
                    if (HasCount(dice, 4)) return 40 + (firstRollServed ? 10 : 0);
                    return 0;

                case ScoreCategory.Generala:
                    if (HasCount(dice, 5)) return firstRollServed ? 60 : 50;
                    return 0;
            }
            return 0;
        }

        public static Dictionary<ScoreCategory, int> AllScores(int[] dice, bool firstRollServed, HashSet<ScoreCategory> alreadyUsed)
        {
            var res = new Dictionary<ScoreCategory, int>();
            foreach (ScoreCategory c in Enum.GetValues(typeof(ScoreCategory)))
                if (!alreadyUsed.Contains(c))
                    res[c] = Score(c, dice, firstRollServed);
            return res;
        }

        private static int SumOf(int face, int[] dice)
        {
            int s = 0;
            for (int i = 0; i < dice.Length; i++)
                if (dice[i] == face) s += face;
            return s;
        }

        private static bool IsStraight(int[] dice)
        {
            var s = dice.Distinct().OrderBy(x => x).ToArray();
            if (s.Length != 5) return false;
            for (int i = 1; i < 5; i++)
                if (s[i] != s[i - 1] + 1) return false;
            return s[0] == 1 || s[0] == 2; // 1-5 або 2-6
        }

        private static bool IsFullHouse(int[] dice)
        {
            var cnt = DiceUtil.Counts(dice);
            bool has3 = false, has2 = false;
            for (int v = 1; v <= 6; v++)
            {
                if (cnt[v] == 3) has3 = true;
                else if (cnt[v] == 2) has2 = true;
            }
            return has3 && has2;
        }

        private static bool HasCount(int[] dice, int k)
        {
            var cnt = DiceUtil.Counts(dice);
            for (int v = 1; v <= 6; v++)
                if (cnt[v] >= k) return true;
            return false;
        }
    }

    public class ScoreCard
    {
        private readonly Dictionary<ScoreCategory, int> _scores = new Dictionary<ScoreCategory, int>();
        public HashSet<ScoreCategory> Used = new HashSet<ScoreCategory>();

        public bool IsUsed(ScoreCategory c) { return Used.Contains(c); }
        public int? Get(ScoreCategory c) { return _scores.ContainsKey(c) ? (int?)_scores[c] : null; }

        public void Set(ScoreCategory c, int value)
        {
            _scores[c] = value;
            Used.Add(c);
        }

        public int Total() { return _scores.Values.Sum(); }
        public bool Completed() { return Used.Count == Enum.GetValues(typeof(ScoreCategory)).Length; }
    }
}
