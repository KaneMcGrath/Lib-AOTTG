// SPDX-License-Identifier: MIT
using System.Numerics;

namespace LibAottg.Assets
{
    internal static class RuntimeMath
    {
        public const float PI = (float)Math.PI;
        public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(max, value));
        public static float Asin(float value) => (float)Math.Asin(value);
        public static float Atan2(float y, float x) => (float)Math.Atan2(y, x);
        public static float Component(Vector4 v, int index) => index == 0 ? v.X : index == 1 ? v.Y : index == 2 ? v.Z : v.W;
    }
}
#if NETSTANDARD2_0
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
#endif
