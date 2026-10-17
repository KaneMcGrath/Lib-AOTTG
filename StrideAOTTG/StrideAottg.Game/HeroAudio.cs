// SPDX-License-Identifier: MIT
using LibAottg;
using Stride.Audio;
using Stride.Media;

namespace StrideAottg;

/// <summary>Local hero audio example. One native voice per cue keeps hook, blade, gun and death audio independent.</summary>
internal sealed class HeroAudio : IDisposable
{
    private readonly Dictionary<HeroSound, SoundInstance> instances;
    public readonly Dictionary<HeroSound, int> CueRequests = new();
    public int Requests { get; private set; }
    public int PlayingObservations { get; private set; }

    public HeroAudio(Func<string, Sound> load)
    {
        instances = new Dictionary<HeroSound, SoundInstance>();
        foreach (var entry in new[] { (HeroSound.HookLaunch, "HookLaunch"), (HeroSound.BladeSwing, "BladeSwing"),
            (HeroSound.AhssFire, "AhssFire"), (HeroSound.BladeHit, "BladeHit"), (HeroSound.Death, "Death") })
            instances.Add(entry.Item1, load(entry.Item2).CreateInstance());
    }
    public void Play(HeroSoundCue cue)
    {
        if (!instances.TryGetValue(cue.Sound, out var instance)) return;
        CueRequests[cue.Sound] = CueRequests.GetValueOrDefault(cue.Sound) + 1;
        Requests++;
        instance.Stop();
        instance.Volume = cue.Volume;
        instance.Pitch = cue.Pitch;
        instance.IsLooping = false;
        instance.Play();
        if (instance.PlayState == PlayState.Playing) PlayingObservations++;
    }
    public void Stop() { foreach (var instance in instances.Values) instance.Stop(); }
    public void Dispose() { foreach (var instance in instances.Values) instance.Dispose(); }
}
