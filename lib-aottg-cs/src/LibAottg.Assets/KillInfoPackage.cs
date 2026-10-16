// SPDX-License-Identifier: MIT
namespace LibAottg.Assets;

/// <summary>Original KillInfo prefab layout, four bitmap fonts and six icons.</summary>
public sealed class KillInfoPackage
{
    private readonly DamageHudPackage bitmap;
    public IReadOnlyDictionary<string, byte[]> Textures => bitmap.Textures;
    private KillInfoPackage(string directory) { bitmap = new DamageHudPackage(directory, "libaottg.kill-info"); }
    public static KillInfoPackage Load(string directory) => new(directory);
    public IReadOnlyList<DamageHudQuad> Compose(HeroKillInfo state, float viewportWidth, float viewportHeight)
        => bitmap.Compose(state, viewportWidth, viewportHeight);
    public IReadOnlyList<DamageHudQuad> Compose(HeroKillInfoEntry entry, float viewportWidth, float viewportHeight)
        => bitmap.Compose(entry, viewportWidth, viewportHeight);
}
