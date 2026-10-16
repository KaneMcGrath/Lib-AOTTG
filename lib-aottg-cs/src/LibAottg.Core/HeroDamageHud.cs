// SPDX-License-Identifier: MIT
using System;
using System.Globalization;

namespace LibAottg
{
    /// <summary>Portable LabelScore and StylishComponent state. Advance once per rendered,
    /// unpaused frame: the source shake and rank depletion depend on frame ordering.</summary>
    public sealed class HeroDamageHud
    {
        private static readonly int[] thresholds = { 350, 950, 2450, 4550, 7000, 15000, 100000 };
        private static readonly int[] depletion = { 1, 2, 5, 10, 15, 20, 25, 25 };
        private static readonly float[] multipliers = { 1, 1.1f, 1.2f, 1.3f, 1.5f, 1.7f, 2, 2.3f, 2.5f };
        private static readonly string[] letters = { "D", "C", "B", "A", "S", "S", "S", "X" };
        private static readonly string[] suffixes = { "eja Vu", "asual", "oppin!", "mazing!", "ensational!", "pectacular!!", "tylish!!!", "TREEME!!!" };
        private static readonly string[] colors = { "FFFFFF", "ACFA58", "F4FA58", "FAAC58", "FA8258", "BE81F7", "FF0000", "000000" };
        private float chainTime, shakeTime, scoreAge = 2.5f, scoreSize;
        private bool lostRank, flip;
        private float width = 1280;

        public float StylePoints { get; private set; }
        public int StyleRank { get; private set; }
        public int Hits { get; private set; }
        public int TotalDamage { get; private set; }
        public int ChainRank { get; private set; }
        public int DisplayRank { get; private set; }
        public float FillAmount { get; private set; }
        public float StylishX { get; private set; } = 1280;
        public float StylishY { get; private set; }
        public string PointsText { get; private set; } = "";
        public string HitsText { get; private set; } = "";
        public string ChainText { get; private set; } = "";
        public string ScoreText { get; private set; } = "";
        public float ScoreScale { get; private set; }
        public string RankLetter { get { return letters[DisplayRank]; } }
        public string RankSuffix { get { return suffixes[DisplayRank]; } }
        public string RankColor { get { return colors[DisplayRank]; } }

        public void Resize(float screenWidth)
        {
            if (float.IsNaN(screenWidth) || float.IsInfinity(screenWidth) || screenWidth <= 0)
                throw new ArgumentOutOfRangeException("screenWidth");
            if (screenWidth == width) return;
            width = screenWidth;
            StylishX = StylePoints > 0 ? (int)(width * 0.5f - 2) : width;
            StylishY = 0;
        }

        /// <summary>Equivalent to netShowDamage; replaces the previous number and feeds Stylish.</summary>
        public void ShowDamage(int damage)
        {
            if (damage < -1) throw new ArgumentOutOfRangeException("damage");
            Style(damage);
            ScoreText = damage.ToString(CultureInfo.InvariantCulture);
            scoreAge = 0;
            scoreSize = Math.Max(40, Math.Min(150, (int)(damage * 0.1f)));
            ScoreScale = 0;
        }

        /// <summary>The source sentinel -1 wakes the panel without awarding a hit.</summary>
        public void Style(int damage)
        {
            if (damage < -1) throw new ArgumentOutOfRangeException("damage");
            if (damage != -1)
            {
                // Unity 4.6 Mono keeps the product at double precision before the
                // integer cast (e.g. 1700 * stored 1.3f truncates to 2209).
                StylePoints += (int)((damage + 200) * (double)multipliers[ChainRank]);
                TotalDamage += damage;
                ChainRank = Math.Min(ChainRank + 1, multipliers.Length - 1);
                chainTime = 5;
                Hits++;
                SetRank();
            }
            else if (StylePoints == 0) { StylePoints++; SetRank(); }
            if (shakeTime < 0.3f) shakeTime = 0.3f;
            // These strings update on hits only, including the source's stale chain label
            // after timeout and points label during passive depletion.
            PointsText = ((int)StylePoints).ToString(CultureInfo.InvariantCulture);
            HitsText = Hits.ToString(CultureInfo.InvariantCulture) + (Hits <= 1 ? "Hit" : "Hits");
            ChainText = ChainRank == 0 ? "" : "x" + multipliers[ChainRank].ToString(CultureInfo.InvariantCulture) + "!";
        }

        public void Advance(float deltaTime, bool paused = false)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0)
                throw new ArgumentOutOfRangeException("deltaTime");
            if (paused || deltaTime == 0) return;
            scoreAge += deltaTime;
            ScoreScale = scoreAge < 1 ? scoreSize * Elastic(scoreAge)
                : scoreAge < 2 ? scoreSize
                : scoreAge < 2.5f ? scoreSize * Bounce((2.5f - scoreAge) * 2) : 0;
            if (StylePoints > 0)
            {
                DisplayRank = StyleRank;
                int percentage = StyleRank == 0 ? (int)(StylePoints * 100) / thresholds[0]
                    : StyleRank == thresholds.Length ? 100
                    : (int)((StylePoints - thresholds[StyleRank - 1]) * 100 / (thresholds[StyleRank] - thresholds[StyleRank - 1]));
                FillAmount = Math.Max(0, Math.Min(1, percentage * 0.01f));
                StylePoints -= depletion[StyleRank] * deltaTime * 10;
                SetRank();
            }
            else StylishX += (width - StylishX) * Math.Min(1, deltaTime * 3);
            if (chainTime > 0) chainTime -= deltaTime;
            else { chainTime = 0; ChainRank = 0; }
            if (shakeTime > 0)
            {
                shakeTime -= deltaTime;
                StylishX = (int)(width * 0.5f - 2);
                StylishY = flip ? 5 : -5;
                flip = !flip;
                if (shakeTime <= 0) StylishY = 0;
            }
        }

        public void Reset()
        {
            StylePoints = 0; StyleRank = 0; Hits = 0; TotalDamage = 0; ChainRank = 0;
            DisplayRank = 0; FillAmount = 0; chainTime = 0; shakeTime = 0;
            lostRank = false; flip = false; scoreAge = 2.5f; ScoreScale = 0;
            StylishX = width; StylishY = 0;
            PointsText = HitsText = ChainText = ScoreText = "";
        }

        private void SetRank()
        {
            int previous = StyleRank, rank = 0;
            while (rank < thresholds.Length && StylePoints > thresholds[rank]) rank++;
            StyleRank = rank;
            if (rank < previous)
            {
                if (lostRank) { StylePoints = 0; Hits = 0; TotalDamage = 0; StyleRank = 0; }
                else lostRank = true;
            }
            else if (rank > previous) lostRank = false;
        }

        private static float Elastic(float t)
        { return t == 0 || t == 1 ? t : (float)(Math.Pow(2, -10 * t) * Math.Sin((t - 0.075) * 6.283185 / 0.3) + 1); }
        private static float Bounce(float t)
        {
            if (t < 1 / 2.75f) return 7.5625f * t * t;
            if (t < 2 / 2.75f) { t -= 1.5f / 2.75f; return 7.5625f * t * t + 0.75f; }
            if (t < 2.5f / 2.75f) { t -= 2.25f / 2.75f; return 7.5625f * t * t + 0.9375f; }
            t -= 2.625f / 2.75f; return 7.5625f * t * t + 0.984375f;
        }
    }
}
