// SPDX-License-Identifier: MIT
namespace LibAottg
{
    public enum HeroSound { HookLaunch, BladeSwing, AhssFire, BladeHit, Death }

    /// <summary>A playback request. The host loads the asset and owns playback, mixing and spatialization.
    /// Cues are one-shots; hosts can keep one independent voice per sound.</summary>
    public readonly struct HeroSoundCue
    {
        public HeroSound Sound { get; }
        /// <summary>Hero position in host world units, already multiplied by WorldScale.</summary>
        public Vec3 Position { get; }
        public float Volume { get; }
        public float Pitch { get; }
        public HeroSoundCue(HeroSound sound, Vec3 position, float volume = 1, float pitch = 1)
        { Sound = sound; Position = position; Volume = volume; Pitch = pitch; }
    }
}
