// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;

namespace LibAottg
{
    public struct SmokeCurveKey
    {
        public float Time, Value, InSlope, OutSlope;
        public SmokeCurveKey(float time, float value, float inSlope, float outSlope)
        { Time = time; Value = value; InSlope = inSlope; OutSlope = outSlope; }
    }

    /// <summary>Original-unit settings extracted from HERO/3dmg_smoke. Assets can load the manifest.</summary>
    public sealed class HeroSmokeSettings
    {
        public float Rate = 60, LifetimeMin = 3, LifetimeMax = 4;
        public float SpeedMin = 1, SpeedMax = 1.35f, SizeMin = 1, SizeMax = 2;
        public float RotationMin = -(float)Math.PI, RotationMax = (float)Math.PI;
        public float Radius = 0.31f, ConeAngle = 21.44f;
        public Vec3 LocalPosition = new Vec3(0.112854f, 0.58514f, -0.343079f);
        public Vec3 LocalDirection = Vec3.Up, LocalRight = new Vec3(1, 0, 0), LocalUp = new Vec3(0, 0, -1);
        // Texture * initial particle color * material tint * 2, as in the source shader.
        public Vec3 Color = new Vec3(0.987006f, 0.987006f, 0.987006f);
        public float Alpha = 70f / 255 * 2;
        public int MaxParticles = 10000;
        public SmokeCurveKey[] SizeCurve = {
            new SmokeCurveKey(0, 0.41176465f, 11.783682f, 11.783682f),
            new SmokeCurveKey(0.124273434f, 1, 0, 0),
            new SmokeCurveKey(1, 0, 0.5305652f, 0.5305652f) };

        public void Validate()
        {
            float[] values = { Rate, LifetimeMin, LifetimeMax, SpeedMin, SpeedMax, SizeMin, SizeMax, Radius, ConeAngle, Alpha };
            foreach (float v in values) if (float.IsNaN(v) || float.IsInfinity(v) || v < 0) throw new ArgumentOutOfRangeException("HeroSmokeSettings");
            if (LifetimeMin <= 0 || LifetimeMin > LifetimeMax || SpeedMin > SpeedMax || SizeMin > SizeMax ||
                MaxParticles <= 0 || MaxParticles > 100000 || ConeAngle > 90 || SizeCurve == null || SizeCurve.Length == 0 ||
                !WorldBox.Finite(LocalPosition) || !WorldBox.Finite(LocalDirection) || !WorldBox.Finite(LocalRight) ||
                !WorldBox.Finite(LocalUp) || !WorldBox.Finite(Color) || !WorldBox.Finite(new Vec3(RotationMin, RotationMax, 0)) || RotationMin > RotationMax)
                throw new ArgumentOutOfRangeException("HeroSmokeSettings");
            for (int n = 0; n < SizeCurve.Length; n++)
            {
                var k = SizeCurve[n];
                if (!WorldBox.Finite(new Vec3(k.Time, k.Value, k.InSlope)) || float.IsNaN(k.OutSlope) || float.IsInfinity(k.OutSlope) ||
                    k.Time < 0 || k.Time > 1 || (n > 0 && k.Time <= SizeCurve[n - 1].Time))
                    throw new ArgumentOutOfRangeException("SizeCurve");
            }
        }
        internal float SizeAt(float t)
        {
            if (t <= SizeCurve[0].Time) return SizeCurve[0].Value;
            for (int n = 1; n < SizeCurve.Length; n++)
            {
                var a = SizeCurve[n - 1]; var b = SizeCurve[n];
                if (t > b.Time) continue;
                float span = b.Time - a.Time, u = (t - a.Time) / span, u2 = u * u, u3 = u2 * u;
                return Math.Max(0, (2 * u3 - 3 * u2 + 1) * a.Value + (u3 - 2 * u2 + u) * span * a.OutSlope +
                    (-2 * u3 + 3 * u2) * b.Value + (u3 - u2) * span * b.InSlope);
            }
            return SizeCurve[SizeCurve.Length - 1].Value;
        }
    }

    public struct HeroSmokeParticle
    {
        public Vec3 Position, Velocity, Color;
        public float Age, Lifetime, Rotation, Size, Alpha;
        internal float StartSize;
    }

    /// <summary>Optional world-space smoke simulation. Instantiate separately from the motor;
    /// a host draws Particles as alpha-blended billboards using the extracted smoke texture.</summary>
    public sealed class HeroSmokeTrail
    {
        public bool Enabled = true;
        public HeroSmokeSettings Settings { get; private set; }
        public IReadOnlyList<HeroSmokeParticle> Particles { get { return view; } }
        private readonly List<HeroSmokeParticle> particles = new List<HeroSmokeParticle>();
        private readonly IReadOnlyList<HeroSmokeParticle> view;
        private uint random, initialSeed;
        private double fraction;
        private bool hasEmitter;
        private Vec3 previousEmitter;

        public HeroSmokeTrail(HeroSmokeSettings settings = null, uint seed = 1)
        {
            Settings = settings ?? new HeroSmokeSettings(); Settings.Validate();
            initialSeed = seed == 0 ? 1u : seed; random = initialSeed;
            view = particles.AsReadOnly();
        }
        public void Reset()
        { particles.Clear(); fraction = 0; hasEmitter = false; random = initialSeed; }

        /// <summary>Call after advancing the hero. Emission follows gas thrust/spin; existing
        /// particles continue fading when thrust stops. Reset after teleports/respawn.</summary>
        public void Advance(float dt, CharacterMotor motor, HeroPose pose)
        {
            if (motor == null) throw new ArgumentNullException("motor");
            if (float.IsNaN(dt) || float.IsInfinity(dt) || dt <= 0) throw new ArgumentOutOfRangeException("dt");
            Vec3 emitter = pose.Position + pose.Rotate(Settings.LocalPosition * motor.Scale);
            if (!hasEmitter) previousEmitter = emitter;
            for (int n = particles.Count - 1; n >= 0; n--)
            {
                var p = particles[n]; p.Age += dt;
                if (p.Age >= p.Lifetime) { particles.RemoveAt(n); continue; }
                p.Position += p.Velocity * dt; UpdateAppearance(ref p); particles[n] = p;
            }
            if (Enabled && motor.GasThrustActive)
            {
                double total = fraction + Settings.Rate * (double)dt;
                int births = (int)Math.Min(total, Settings.MaxParticles);
                double first = Settings.Rate > 0 ? (1 - fraction) / Settings.Rate : dt;
                fraction = total - Math.Floor(total);
                for (int n = 0; n < births && particles.Count < Settings.MaxParticles; n++)
                {
                    float time = (float)(first + n / (double)Settings.Rate);
                    float age = Math.Max(0, dt - time);
                    float lifetime = Range(Settings.LifetimeMin, Settings.LifetimeMax);
                    if (age >= lifetime) continue;
                    float azimuth = Range(0, (float)Math.PI * 2);
                    float radius = (float)Math.Sqrt(Next()) * Settings.Radius;
                    Vec3 radial = Settings.LocalRight * (float)Math.Cos(azimuth) + Settings.LocalUp * (float)Math.Sin(azimuth);
                    float angle = Settings.ConeAngle * radius / Math.Max(Settings.Radius, 0.00001f) * (float)Math.PI / 180;
                    Vec3 direction = Settings.LocalDirection * (float)Math.Cos(angle) + radial * (float)Math.Sin(angle);
                    var p = new HeroSmokeParticle { Position = Vec3.Lerp(previousEmitter, emitter, time / dt) + pose.Rotate(radial * (radius * motor.Scale)),
                        Velocity = pose.Rotate(direction) * (Range(Settings.SpeedMin, Settings.SpeedMax) * motor.Scale),
                        Age = age, Lifetime = lifetime, StartSize = Range(Settings.SizeMin, Settings.SizeMax) * motor.Scale,
                        Rotation = Range(Settings.RotationMin, Settings.RotationMax), Color = Settings.Color };
                    p.Position += p.Velocity * age; UpdateAppearance(ref p); particles.Add(p);
                }
            }
            else fraction = 0;
            previousEmitter = emitter; hasEmitter = true;
        }
        private void UpdateAppearance(ref HeroSmokeParticle p)
        { float t = p.Age / p.Lifetime; p.Size = p.StartSize * Settings.SizeAt(t); p.Alpha = Settings.Alpha * (1 - t); }
        private float Next()
        { random ^= random << 13; random ^= random >> 17; random ^= random << 5; return (random >> 8) / 16777216f; }
        private float Range(float min, float max) { return min + (max - min) * Next(); }
    }
}
