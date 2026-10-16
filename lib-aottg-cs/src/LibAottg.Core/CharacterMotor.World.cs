// SPDX-License-Identifier: MIT
namespace LibAottg
{
    public sealed partial class CharacterMotor
    {
        private readonly WorldBoundary _world;
        private ICharacterWorld BindWorld(ICharacterWorld world) { _world.Host = world; return _world; }

        // All HERO math runs in original units. Only this boundary knows the host scale.
        // LocalPoint belongs to the host target, so it is deliberately opaque to the motor.
        private sealed class WorldBoundary : IHookGeometryWorld
        {
            private readonly CharacterMotor _motor;
            public ICharacterWorld Host;
            public WorldBoundary(CharacterMotor motor) { _motor = motor; }
            public bool TryHook(Vec3 origin, Vec3 direction, float distance, HookSide side, out HookHit hit)
            {
                bool found = Host.TryHook(origin * _motor.Scale, direction, distance * _motor.Scale, side, out hit);
                hit.Point /= _motor.Scale;
                return found;
            }
            public Vec3 HookOrigin(CharacterMotor motor, HookSide side)
            {
                var geometry = Host as IHookGeometryWorld;
                return geometry != null ? geometry.HookOrigin(motor, side) / motor.Scale : motor._position + Vec3.Up * 0.85f;
            }
            public bool SweepHook(Vec3 start, Vec3 end, HookSide side, out HookHit hit)
            {
                var geometry = Host as IHookGeometryWorld;
                if (geometry == null)
                {
                    Vec3 segment = end - start;
                    return TryHook(start, segment.Normalized, segment.Length, side, out hit);
                }
                bool found = geometry.SweepHook(start * _motor.Scale, end * _motor.Scale, side, out hit);
                hit.Point /= _motor.Scale;
                return found;
            }
            public bool ResolveHookAnchor(int targetId, Vec3 localPoint, out Vec3 point)
            {
                var geometry = Host as IHookGeometryWorld;
                point = Vec3.Zero;
                if (geometry == null || !geometry.ResolveHookAnchor(targetId, localPoint, out point)) return false;
                point /= _motor.Scale;
                return true;
            }
            public void OnCharacterEvent(CharacterEvent kind) { Host.OnCharacterEvent(kind); }
        }
    }
}
