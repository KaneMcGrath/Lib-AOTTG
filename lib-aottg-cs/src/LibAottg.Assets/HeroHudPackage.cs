// SPDX-License-Identifier: MIT
namespace LibAottg.Assets;
/// <summary>Original AOTTG GasUI, cooldown icons and aiming widgets.</summary>
public sealed class HeroHudPackage
{
    private readonly DamageHudPackage package;
    private HeroHudPackage(string directory) { package = new(directory, "libaottg.hero-hud"); }
    public static HeroHudPackage Load(string directory) => new(directory);
    public IReadOnlyDictionary<string, byte[]> Textures => package.Textures;
    public IReadOnlyList<DamageHudQuad> Compose(HeroHud state, float width, float height) => package.Compose(state, width, height);
}
