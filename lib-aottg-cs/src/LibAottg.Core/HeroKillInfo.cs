// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;

namespace LibAottg
{
    /// <summary>One original KillInfoComponent, including its pop, row motion and fade.</summary>
    public sealed class HeroKillInfoEntry
    {
        public bool KillerIsTitan { get; private set; }
        public bool VictimIsTitan { get; private set; }
        public string Killer { get; private set; }
        public string Victim { get; private set; }
        public int Damage { get; private set; }
        public int Row { get; private set; }
        public bool Compact { get; private set; }
        public float Age { get; private set; }
        public float Scale { get; private set; }
        public float Y { get; private set; }
        public float Alpha { get; private set; }
        public bool Expired { get { return Age > 8.5f; } }

        internal HeroKillInfoEntry(bool killerIsTitan, string killer, bool victimIsTitan, string victim, int damage, float height)
        {
            KillerIsTitan = killerIsTitan; VictimIsTitan = victimIsTitan;
            Killer = killer ?? ""; Victim = victim ?? ""; Damage = damage;
            Scale = 0.85f; Alpha = 1; Y = -100f + height * 0.5f;
        }
        internal void MoveOn()
        {
            Row++; Compact = true;
            if (Row > 4) Dismiss();
        }
        public void Dismiss() { Age = 8; }

        internal void Advance(float dt, float height)
        {
            // Mono uses extended intermediate precision before storing each float.
            Age = (float)(Age + (double)dt);
            float lerp = Math.Max(0, Math.Min(1, dt * 10));
            if (Age < 0.2f) Scale = (float)(Scale + (1.5 - Scale) * lerp);
            else if (Age < 1) Scale = (float)(Scale + (1.0 - Scale) * lerp);
            if (Age > 8)
            {
                // The source translates in world space. With an orthographic camera
                // and UIRoot scaling this is activeHeight / 2 source pixels per unit.
                float activeHeight = Math.Max(320, Math.Min(1536, height));
                Y = (float)(Y + dt * 0.15 * activeHeight * 0.5);
                Alpha = (float)((1.0 - dt * 45.0) + 8.0 - Age);
            }
            else
            {
                float target = -((int)(100f - height * 0.5f) + Row * 24);
                Y = (float)(Y + (target - (double)Y) * lerp);
            }
        }
    }

    /// <summary>Portable kill feed. Hosts report confirmed kills (or source-style hit notices),
    /// independently of weapon contacts. Keeps five rows plus departing fade animations.</summary>
    public sealed class HeroKillInfo
    {
        private readonly List<HeroKillInfoEntry> entries = new List<HeroKillInfoEntry>();
        private readonly List<HeroKillInfoEntry> tracked = new List<HeroKillInfoEntry>();
        private readonly IReadOnlyList<HeroKillInfoEntry> view;
        public IReadOnlyList<HeroKillInfoEntry> Entries { get { return view; } }
        public HeroKillInfo() { view = entries.AsReadOnly(); }

        public HeroKillInfoEntry Show(bool killerIsTitan, string killer, bool victimIsTitan, string victim, int damage = 0, float screenHeight = 720)
        {
            Height(screenHeight);
            foreach (var entry in tracked) entry.MoveOn();
            if (tracked.Count > 4) { tracked[0].Dismiss(); tracked.RemoveAt(0); }
            var newest = new HeroKillInfoEntry(killerIsTitan, killer, victimIsTitan, victim, damage, screenHeight);
            tracked.Add(newest); entries.Add(newest);
            return newest;
        }
        public void Advance(float deltaTime, float screenHeight = 720, bool paused = false)
        {
            Height(screenHeight);
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0) throw new ArgumentOutOfRangeException("deltaTime");
            if (paused) return;
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var entry = entries[i]; entry.Advance(deltaTime, screenHeight);
                if (entry.Expired) { tracked.Remove(entry); entries.RemoveAt(i); }
            }
        }
        public void Clear() { entries.Clear(); tracked.Clear(); }
        private static void Height(float value)
        { if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0) throw new ArgumentOutOfRangeException("screenHeight"); }
    }
}
