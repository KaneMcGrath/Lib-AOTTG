// SPDX-License-Identifier: MIT
#pragma once
#include <algorithm>
#include <array>
#include <cctype>
#include <cmath>
#include <cstdint>
#include <functional>
#include <limits>
#include <memory>
#include <stdexcept>
#include <string>
#include <type_traits>
#include <unordered_map>
#include <unordered_set>
#include <vector>

namespace LibAottg {
inline constexpr double cs_pi = 3.14159265358979323846;
// C# Math promotes float trig arguments to double. Keep that rounding boundary.
template <class A, class B> auto cs_min(A a, B b) {
    using T = std::common_type_t<A, B>;
    return std::min(T(a), T(b));
}
template <class A, class B> auto cs_max(A a, B b) {
    using T = std::common_type_t<A, B>;
    return std::max(T(a), T(b));
}
template <class A, class B> auto cs_rem(A a, B b) {
    using T = std::conditional_t<std::is_same_v<A, double> || std::is_same_v<B, double>, double, float>;
    return std::fmod(T(a), T(b));
}
template <class T> int cs_sign(T v) {
    if (std::isnan(v))
        throw std::invalid_argument("NaN sign");
    return (v > 0) - (v < 0);
}
inline std::string cs_string(const std::string &v) {
    return v;
}
inline std::string cs_string(const char *v) {
    return v;
}
template <class T> std::string cs_string(T v) {
    return std::to_string(v);
}
inline std::string cs_lower(std::string v) {
    for (char &c : v)
        c = static_cast<char>(std::tolower(static_cast<unsigned char>(c)));
    return v;
}
inline int cs_index(const std::string &v, const std::string &part) {
    auto n = v.find(part);
    return n == std::string::npos ? -1 : static_cast<int>(n);
}
template <class E> bool cs_defined(E v) {
    return static_cast<int>(v) >= 0 && static_cast<int>(v) <= 2;
}
struct Vec3 {
    float X{}, Y{}, Z{};
    Vec3() = default;
    Vec3(float x, float y, float z) : X(x), Y(y), Z(z) {}
    static Vec3 Zero() { return {}; }
    static Vec3 Up() { return {0, 1, 0}; }
    float LengthSquared() const { return X * X + Y * Y + Z * Z; }
    float Length() const { return static_cast<float>(std::sqrt(static_cast<double>(LengthSquared()))); }
    Vec3 Normalized() const {
        float n = Length();
        return n > 0.00001f ? *this / n : Zero();
    }
    static float Dot(Vec3 a, Vec3 b) { return a.X * b.X + a.Y * b.Y + a.Z * b.Z; }
    static Vec3 Cross(Vec3 a, Vec3 b) {
        return {a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X};
    }
    static Vec3 Lerp(Vec3 a, Vec3 b, float t) { return a + (b - a) * std::clamp(t, 0.0f, 1.0f); }
    friend Vec3 operator+(Vec3 a, Vec3 b) { return {a.X + b.X, a.Y + b.Y, a.Z + b.Z}; }
    friend Vec3 operator-(Vec3 a, Vec3 b) { return {a.X - b.X, a.Y - b.Y, a.Z - b.Z}; }
    friend Vec3 operator-(Vec3 a) { return {-a.X, -a.Y, -a.Z}; }
    friend Vec3 operator*(Vec3 a, float n) { return {a.X * n, a.Y * n, a.Z * n}; }
    friend Vec3 operator*(Vec3 a, int n) { return a * static_cast<float>(n); }
    friend Vec3 operator*(float n, Vec3 a) { return a * n; }
    friend Vec3 operator/(Vec3 a, float n) { return {a.X / n, a.Y / n, a.Z / n}; }
    Vec3 &operator+=(Vec3 b) { return *this = *this + b; }
    Vec3 &operator-=(Vec3 b) { return *this = *this - b; }
    Vec3 &operator*=(float n) { return *this = *this * n; }
    Vec3 &operator/=(float n) { return *this = *this / n; }
};
struct Math3 {
    static float Clamp(float v, float mn, float mx) { return cs_max(mn, cs_min(mx, v)); }
    static float MoveTowards(float current, float target, float delta) {
        return std::abs(target - current) <= delta ? target : current + cs_sign(target - current) * delta;
    }
    static Vec3 Horizontal(Vec3 v) { return {v.X, 0, v.Z}; }
    static Vec3 Direction(float deg) {
        double r = deg * cs_pi / 180.0;
        return {static_cast<float>(std::sin(r)), 0, static_cast<float>(std::cos(r))};
    }
};
} // namespace LibAottg
