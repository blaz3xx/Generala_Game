using System;
using System.Collections.Generic;
using System.Linq;

namespace GeneralaGame
{
    public class AIRollStep
    {
        public bool[] HoldMask;   // true = утримати (не кидати)
        public int[] AfterDice;   // результат після кидка
        public string Note;       // для дебага/пояснення
    }

    public class AITurnLog
    {
        public List<AIRollStep> Steps = new List<AIRollStep>();
        public int[] FinalDice;
        public ScoreCategory Category;
        public int Score;
    }

    public class AIPlayer
    {
        private int simulationsPerDecision;
        private int maxMasksToTry;

        public AIDifficulty Difficulty { get; private set; }

        public AIPlayer(AIDifficulty diff) { SetDifficulty(diff); }

        public void SetDifficulty(AIDifficulty diff)
        {
            Difficulty = diff;
            switch (diff)
            {
                case AIDifficulty.Easy:
                    simulationsPerDecision = 60; maxMasksToTry = 6; break;
                case AIDifficulty.Medium:
                    simulationsPerDecision = 260; maxMasksToTry = 12; break;
                default: // Hard
                    simulationsPerDecision = 800; maxMasksToTry = 20; break;
            }
        }

        public AITurnLog PlayTurnWithLog(ScoreCard myCard, ScoreCard oppCard)
        {
            var log = new AITurnLog();

            // Кидок №1 (усі кубики)
            var first = DiceUtil.RollFresh();
            log.Steps.Add(new AIRollStep
            {
                HoldMask = new bool[5],
                AfterDice = DiceUtil.Copy(first),
                Note = "Initial roll"
            });

            int[] dice = DiceUtil.Copy(first);
            int rollsLeft = 2;

            while (rollsLeft > 0)
            {
                var masks = GenerateCandidateMasks(dice).Take(maxMasksToTry).ToList();
                if (masks.Count == 0) masks.Add(new bool[5]);

                double bestEv = double.NegativeInfinity;
                bool[] bestMask = masks[0];

                foreach (var m in masks)
                {
                    double ev = EstimateEV(dice, m, rollsLeft, myCard);
                    if (ev > bestEv) { bestEv = ev; bestMask = m; }
                }

                var after = DiceUtil.RollWithMask(dice, bestMask);
                dice = after;
                rollsLeft--;

                log.Steps.Add(new AIRollStep
                {
                    HoldMask = bestMask,
                    AfterDice = DiceUtil.Copy(after),
                    Note = "mask EV=" + bestEv.ToString("0.0")
                });
            }

            var scores = ScoreCalculator.AllScores(dice, false, myCard.Used);
            int maxScore = scores.Count == 0 ? 0 : scores.Values.Max();
            ScoreCategory chosen = scores.Count == 0 ? ScoreCategory.Ones : scores.First(kv => kv.Value == maxScore).Key;

            if (maxScore == 0)
            {
                var order = new List<ScoreCategory> {
                    ScoreCategory.Ones, ScoreCategory.Twos, ScoreCategory.Threes,
                    ScoreCategory.Fours, ScoreCategory.Fives, ScoreCategory.Sixes,
                    ScoreCategory.Straight, ScoreCategory.FullHouse,
                    ScoreCategory.FourOfAKind, ScoreCategory.Generala
                };
                foreach (var c in order) if (!myCard.IsUsed(c)) { chosen = c; break; }
            }

            log.FinalDice = DiceUtil.Copy(dice);
            log.Category = chosen;
            log.Score = ScoreCalculator.Score(chosen, dice, false);
            return log;
        }

        private IEnumerable<bool[]> GenerateCandidateMasks(int[] dice)
        {
            yield return new bool[5]; // кидати все

            var counts = DiceUtil.Counts(dice);
            int bestVal = 1, bestCount = 0;
            for (int v = 1; v <= 6; v++)
                if (counts[v] > bestCount) { bestCount = counts[v]; bestVal = v; }

            if (bestCount >= 2) yield return KeepValue(dice, bestVal);
            for (int v = 1; v <= 6; v++) if (counts[v] == 3) { yield return KeepValue(dice, v); break; }

            var pairs = new List<int>();
            for (int v = 1; v <= 6; v++) if (counts[v] == 2) pairs.Add(v);
            if (pairs.Count >= 1) yield return KeepValue(dice, pairs[0]);
            if (pairs.Count >= 2) yield return KeepPairValues(dice, pairs[0], pairs[1]);

            var uniq = dice.Distinct().OrderBy(x => x).ToArray();
            if (uniq.Length >= 4)
            {
                var straights = new[] { new[] { 1, 2, 3, 4 }, new[] { 2, 3, 4, 5 }, new[] { 3, 4, 5, 6 } };
                foreach (var core in straights)
                    if (core.All(v => uniq.Contains(v))) yield return KeepSet(dice, core);
            }

            if (maxMasksToTry >= 20) yield return new[] { true, true, true, true, true }; // утримати всі
        }

        private static bool[] KeepValue(int[] dice, int val)
        {
            var m = new bool[5];
            for (int i = 0; i < 5; i++) m[i] = (dice[i] == val);
            return m;
        }

        private static bool[] KeepPairValues(int[] dice, int v1, int v2)
        {
            var m = new bool[5];
            for (int i = 0; i < 5; i++) m[i] = (dice[i] == v1 || dice[i] == v2);
            return m;
        }

        private static bool[] KeepSet(int[] dice, IEnumerable<int> values)
        {
            var set = new HashSet<int>(values);
            var m = new bool[5];
            for (int i = 0; i < 5; i++) m[i] = set.Contains(dice[i]);
            return m;
        }

        private double EstimateEV(int[] currentDice, bool[] holdMask, int rollsLeft, ScoreCard myCard)
        {
            int sims = Math.Max(40, simulationsPerDecision);
            double sum = 0.0;
            for (int s = 0; s < sims; s++)
            {
                int[] d = DiceUtil.RollWithMask(currentDice, holdMask);
                int left = rollsLeft - 1;

                while (left > 0)
                {
                    var cnt = DiceUtil.Counts(d);
                    int val = 1, best = 0;
                    for (int v = 1; v <= 6; v++) if (cnt[v] > best) { best = cnt[v]; val = v; }
                    var mask = KeepValue(d, val);
                    d = DiceUtil.RollWithMask(d, mask);
                    left--;
                }

                var all = ScoreCalculator.AllScores(d, false, myCard.Used);
                int bestNow = 0;
                foreach (var kv in all) if (kv.Value > bestNow) bestNow = kv.Value;
                sum += bestNow;
            }
            return sum / sims;
        }
    }
}
