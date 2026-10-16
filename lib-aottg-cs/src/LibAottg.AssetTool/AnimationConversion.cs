// SPDX-License-Identifier: MIT
using System.Text.Json;

namespace LibAottg.AssetTool;

internal static class AnimationConversion
{
    public static (List<object> Animations, List<object> Rules) Build(Capture capture, GlbBuilder glb, List<Dictionary<string, object>> nodes, float unit)
    {
        var animations = new List<object>();
        var rules = new List<object>();
        foreach (var clip in capture.Clips)
        {
            float[] samples = capture.ReadAnimation(clip);
            int frameCount = clip.GetProperty("frameCount").GetInt32(), stride = 1 + capture.Nodes.Length * 10;
            float[] times = Enumerable.Range(0, frameCount).Select(f => samples[f * stride]).ToArray();
            var channels = new List<object>();
            var samplers = new List<object>();
            var ambiguous = new List<object>();
            int? timeAccessor = null;
            for (int node = 0; node < capture.Nodes.Length; node++)
            {
                AddTrack("translation", 0, 3);
                AddTrack("rotation", 3, 4);
                AddTrack("scale", 7, 3);

                void AddTrack(string path, int offset, int components)
                {
                    float[] track = new float[frameCount * components];
                    float[] baseline = (float[])nodes[node][path];
                    string sourceField = path == "translation" ? "position" : path;
                    float[] captured = capture.Nodes[node].GetProperty(sourceField).Floats();
                    captured = Convert(captured);
                    bool changed = false, differsCaptured = false;
                    for (int f = 0; f < frameCount; f++)
                    {
                        int start = f * stride + 1 + node * 10 + offset;
                        float[] value = Convert(samples[start..(start + components)]);
                        if (path == "scale") Capture.Require(value.All(x => Math.Abs(x) > 1e-12f), "Animation contains zero scale.");
                        if (path == "rotation" && f > 0 && value.Select((v, c) => v * track[(f - 1) * components + c]).Sum() < 0)
                            value = value.Select(v => -v).ToArray();
                        value.CopyTo(track, f * components);
                        changed |= !Equivalent(value, baseline, path == "rotation");
                        differsCaptured |= !Equivalent(value, captured, path == "rotation");
                    }
                    // Omit captured defaults that also equal the reconstructed rest
                    // transform. Defaults needed to restore a baked pose are flagged;
                    // their presence does not imply an authored curve or blend mask.
                    if (!changed) return;
                    if (!differsCaptured) ambiguous.Add(new { node, path, reason = "captured default required to restore solo pose from reconstructed rest" });
                    timeAccessor ??= glb.Floats(times, 1, "SCALAR", bounds: true);
                    channels.Add(new { sampler = samplers.Count, target = new { node, path } });
                    samplers.Add(new { input = timeAccessor.Value, output = glb.Floats(track, components, components == 4 ? "VEC4" : "VEC3"), interpolation = "LINEAR" });

                    float[] Convert(float[] v) => path == "translation" ? CoordinateMath.Position(v, unit) : path == "rotation" ? CoordinateMath.ConvertRotation(v) : v;
                }
            }
            // glTF requires at least one channel. Preserve a completely static clip
            // with a harmless root scale channel and its complete time inventory.
            if (channels.Count == 0)
            {
                float[] scale = (float[])nodes[0]["scale"];
                channels.Add(new { sampler = 0, target = new { node = 0, path = "scale" } });
                samplers.Add(new { input = glb.Floats(times, 1, "SCALAR", bounds: true), output = glb.Floats(times.SelectMany(_ => scale).ToArray(), 3, "VEC3"), interpolation = "LINEAR" });
            }
            animations.Add(new { name = clip.Text("name"), channels, samplers, extras = new { sourceClip = clip, channelMaskKnown = false } });
            rules.Add(new { name = clip.Text("name"), animation = animations.Count - 1, duration = clip.GetProperty("duration").GetSingle(), capturedPlaybackSpeed = clip.GetProperty("capturedPlaybackSpeed").GetSingle(), stateWrapMode = clip.Text("stateWrapMode"), clipWrapMode = clip.Text("clipWrapMode"), layer = clip.GetProperty("layer").GetInt32(), blendMode = clip.Text("blendMode"), authoredEventsAvailable = clip.Flag("eventsAvailable", false), events = clip.GetProperty("events"), sampleCount = frameCount, sampleRate = clip.GetProperty("sampleRate").GetSingle(), inferredChannelMask = true, ambiguousDefaultChannels = ambiguous });
        }
        return (animations, rules);
    }

    private static bool Equivalent(float[] a, float[] b, bool quaternion)
    {
        // Float noise from matrix decomposition is not authored animation motion.
        float direct = a.Zip(b, (x, y) => Math.Abs(x - y)).Max();
        return direct <= 2e-6f || (quaternion && a.Zip(b, (x, y) => Math.Abs(x + y)).Max() <= 2e-6f);
    }
}
