// SPDX-License-Identifier: MIT
using System;
namespace LibAottg
{
    // Durations from the reference character capture.
    // HERO.customAnimationSpeed overrides are kept beside the clip timing.
    public static class HeroAnimations
    {
        public static float Duration(string clip)
        {
            switch (clip)
            {
                case "wallrun": return 0.5f;
                case "toRoof": return 0.683333337f;
                case "supply": return 2.00000024f;
                case "stand_levi": return 1.01666665f;
                case "stand": return 1.01666665f;
                case "special_sasha": return 1.00000012f;
                case "special_petra": return 0.850000024f;
                case "special_marco_1": return 1.33333337f;
                case "special_marco_0": return 1.31666672f;
                case "special_armin": return 2.85000014f;
                case "slide": return 0.516666651f;
                case "salute": return 1.00000012f;
                case "run_sasha": return 0.266666681f;
                case "run_levi": return 0.5f;
                case "run": return 0.5f;
                case "onWall": return 0.333333373f;
                case "jump": return 0.516666651f;
                case "horse_run": return 0.416666716f;
                case "horse_idle": return 1.00000012f;
                case "horse_geton": return 0.616666675f;
                case "horse_getoff": return 0.450000048f;
                case "grabbed_jean": return 1.10000002f;
                case "grabbed": return 0.350000024f;
                case "dodge": return 1.10000002f;
                case "dash_land": return 0.833333373f;
                case "dash": return 0.666666687f;
                case "changeBlade_air": return 0.516666651f;
                case "changeBlade": return 1.75000012f;
                case "attack5": return 1.50000012f;
                case "attack4": return 0.433333367f;
                case "attack3_2": return 0.483333379f;
                case "attack3_1": return 0.416666687f;
                case "attack2": return 0.5f;
                case "attack1_hook_r2": return 0.5f;
                case "attack1_hook_r1": return 0.5f;
                case "attack1_hook_l2": return 0.5f;
                case "attack1_hook_l1": return 0.5f;
                case "attack1": return 0.5f;
                case "air_rise": return 1.00000012f;
                case "air_release": return 0.5f;
                case "air_hook_r_just": return 0.666666687f;
                case "air_hook_r": return 0.666666687f;
                case "air_hook_l_just": return 0.666666687f;
                case "air_hook_l": return 0.666666687f;
                case "air_hook": return 0.666666687f;
                case "air_fall": return 1.00000012f;
                case "air_circle": return 1.00000012f;
                case "air2_right": return 1.00000012f;
                case "air2_left": return 1.00000012f;
                case "air2_backward": return 1.00000012f;
                case "air2": return 0.333333373f;
                case "T-pose": return 1f;
                case "AHSS_stand_gun": return 1.00000012f;
                case "AHSS_shoot_r_air": return 0.683333337f;
                case "AHSS_shoot_r": return 0.683333337f;
                case "AHSS_shoot_l_air": return 0.683333337f;
                case "AHSS_shoot_l": return 0.683333337f;
                case "AHSS_shoot_both_air": return 0.833333373f;
                case "AHSS_shoot_both": return 0.833333373f;
                case "AHSS_hook_forward_r": return 0.666666687f;
                case "AHSS_hook_forward_l": return 0.666666687f;
                case "AHSS_hook_forward_both": return 0.666666687f;
                case "AHSS_gun_reload_r_air": return 0.76666671f;
                case "AHSS_gun_reload_r": return 0.76666671f;
                case "AHSS_gun_reload_l_air": return 0.76666671f;
                case "AHSS_gun_reload_l": return 0.76666671f;
                case "AHSS_gun_reload_both_air": return 0.76666671f;
                case "AHSS_gun_reload_both": return 0.76666671f;
                default: throw new ArgumentException("Unknown HERO animation: " + clip);
            }
        }
        public static float Speed(string clip)
        {
            switch (clip)
            {
                case "attack5": return 1.85f;
                case "changeBlade": return 1.2f;
                case "air_release": return 0.6f;
                case "changeBlade_air": return 0.8f;
                case "AHSS_gun_reload_both": return 0.38f;
                case "AHSS_gun_reload_l": case "AHSS_gun_reload_r": return 0.4f;
                case "AHSS_gun_reload_both_air": case "AHSS_gun_reload_l_air": case "AHSS_gun_reload_r_air": return 0.5f;
                default: return 1f;
            }
        }
    }
}
