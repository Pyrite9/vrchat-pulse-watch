// Pyrite9 Pulse Watch — wearer's clock and glyph text (docs/pulse-watch.md 4-2)
//   time: VRChat's UTC Unix seconds + the wearer's offset baked into the material (D2: wearer time, not viewer time).
//         All integer math — Unix seconds (~1.8e9) in a float would lose whole seconds.
//   text: one signed-distance-field atlas (Textures/PulseWatchGlyphs.png, Source/glyph_atlas.py), monospaced-ish cursor layout.
#ifndef PULSEWATCH_TIME_CGINC
#define PULSEWATCH_TIME_CGINC

#include "Packages/com.vrchat.base/ShaderLibrary/VRCTime.cginc"

// glyph indices = atlas cells (Source/glyph_atlas.py GLYPHS)
#define PW_COLON 10
#define PW_DOT 11
#define PW_LPAR 12
#define PW_RPAR 13
#define PW_DASH 14
#define PW_DOW_KO 15       // 15..21 = Mon..Sun
#define PW_X 22            // 22..24 = X, Y, Z
#define PW_F 25            // 25..27 = F, P, S
#define PW_N 28            // 28..30 = N, E, W (compass, D26)
#define PW_M 31            // "m" (metres)
#define PW_DOW_JA 32       // 32..38 = 月..日 (D73)
#define PW_A 39            // 39..47 = A, D, H, I, M, O, R, T, U — with N, E, W, F, S above: MON..SUN (D73)
#define PW_SPACE 255       // no cell: never sampled (PwGlyph), the atlas is full
#define PW_ATLAS_COLS 8.0
#define PW_ATLAS_ROWS 6.0
#define PW_SPREAD 0.125    // distance range of the atlas, in cells (16 px of 128)
#define PW_BASELINE 0.20   // baseline above the cell bottom, in cells
#define PW_MAX_GLYPHS 10

sampler2D _Glyphs;
int _UtcOffsetMin;         // wearer's standard offset (KST = 540)
int _DateLang;             // D73: the menu language (PulseWatchMenuLanguage: 0 English, 1 Japanese, 2 Korean)
// D72: the build PC's daylight saving rule (PulseWatchPlugin.DstRule) — each transition (month, week 1-5 with 5 = last or 0 = a
// fixed date, day of week with 0 = Sunday or the fixed day, minutes into the local day: the start's standard, the end's daylight)
float4 _DstStart, _DstEnd;
int _DstMin;               // the shift in minutes, 0 = no daylight saving

struct PwTime
{
    uint hour, minute, second;
    uint month, day;
    uint weekday;          // 0 = Monday
    bool live;             // false where VRChat does not set the clock (editor outside Play)
};

// civil date from days since 1970-01-01 (H. Hinnant), unsigned
void PwCivil(uint days, out uint y, out uint m, out uint d)
{
    uint z = days + 719468u;
    uint era = z / 146097u;
    uint doe = z - era * 146097u;
    uint yoe = (doe - doe / 1460u + doe / 36524u - doe / 146096u) / 365u;
    uint doy = doe - (365u * yoe + yoe / 4u - yoe / 100u);
    uint mp = (5u * doy + 2u) / 153u;
    d = doy - (153u * mp + 2u) / 5u + 1u;
    m = mp < 10u ? mp + 3u : mp - 9u;
    y = yoe + era * 400u + (m <= 2u ? 1u : 0u);
}

// days since 1970-01-01 from a civil date (H. Hinnant), unsigned (years from 1970)
uint PwDays(uint y, uint m, uint d)
{
    y -= m <= 2u ? 1u : 0u;
    uint era = y / 400u;
    uint yoe = y - era * 400u;
    uint doy = (153u * (m > 2u ? m - 3u : m + 9u) + 2u) / 5u + d - 1u;
    return era * 146097u + yoe * 365u + yoe / 4u - yoe / 100u + doy - 719468u;
}

// the day a transition falls on in year y: a fixed date, or the n-th (5 = last) given weekday of the month
uint PwTransitionDay(uint y, float4 r)
{
    uint m = (uint)r.x, w = (uint)r.y, k = (uint)r.z;
    uint first = PwDays(y, m, 1u);
    if (w == 0u) return first + k - 1u;
    uint next = m == 12u ? PwDays(y + 1u, 1u, 1u) : PwDays(y, m + 1u, 1u);
    uint d = first + (k + 7u - (first + 4u) % 7u) % 7u + 7u * (w - 1u);   // (days + 4) % 7 = day of week, 0 = Sunday
    return d >= next ? d - 7u : d;                                        // a 5th that is not there = the last
}

// daylight saving at this standard local time (seconds since 1970 in the wearer's standard time)
bool PwDst(uint std)
{
    if (_DstMin == 0) return false;
    uint y, m, d;
    PwCivil(std / 86400u, y, m, d);
    uint s = PwTransitionDay(y, _DstStart) * 86400u + (uint)_DstStart.w * 60u;
    uint e = PwTransitionDay(y, _DstEnd) * 86400u + (uint)_DstEnd.w * 60u - (uint)(_DstMin * 60);   // daylight -> standard
    return s < e ? (std >= s && std < e) : (std >= s || std < e);                                   // south: across new year
}

PwTime PwGetTime()
{
    PwTime o;
    uint t = _VRChatTimeUTCUnixSeconds;
    o.live = t > 946684800u;                                    // after 2000-01-01
    uint std = t + (uint)(_UtcOffsetMin * 60);                  // wraps correctly for negative offsets
    uint local = std + (PwDst(std) ? (uint)(_DstMin * 60) : 0u);
    uint sod = local % 86400u;
    uint days = local / 86400u;
    o.hour = sod / 3600u;
    o.minute = (sod / 60u) % 60u;
    o.second = sod % 60u;
    o.weekday = (days + 3u) % 7u;                               // 1970-01-01 was a Thursday
    uint y;
    PwCivil(days, y, o.month, o.day);
    return o;
}

// A, D, H, I, M, O, R, T, U: the font's advance + ~0.07 like the other capitals
static const float PW_ADV_LATIN[9] = { 0.56, 0.62, 0.64, 0.32, 0.72, 0.66, 0.58, 0.55, 0.64 };

// advance in cells
float PwAdvance(uint g)
{
    if (g < 10u) return 0.52;
    if (g == PW_COLON || g == PW_DOT) return 0.28;
    if (g == PW_LPAR || g == PW_RPAR) return 0.34;
    if (g == PW_DASH) return 0.52;
    if (g == PW_SPACE) return 0.26;
    if (g >= PW_X && g < PW_F + 3u) return 0.62;
    if (g == PW_N) return 0.62;
    if (g == PW_N + 1u) return 0.56;
    if (g == PW_N + 2u) return 0.78;
    if (g == PW_M) return 0.74;
    if (g >= PW_A && g < PW_A + 9u) return PW_ADV_LATIN[g - PW_A];
    return 0.98;
}

// signed distance (text units, + outside) from p to glyph g whose cell is centred at cx with its baseline at by;
// dpx / dpy = screen derivatives of p (taken outside any branch)
float PwGlyph(float2 p, float2 dpx, float2 dpy, float cx, float by, float size, uint g)
{
    float2 q = (p - float2(cx - 0.5 * size, by - PW_BASELINE * size)) / size;
    if (g == PW_SPACE || any(q < 0.0) || any(q > 1.0)) return PW_SPREAD * size;
    float2 cell = float2(g % 8u, g / 8u);
    float2 uv = float2((cell.x + q.x) / PW_ATLAS_COLS, 1.0 - (cell.y + 1.0 - q.y) / PW_ATLAS_ROWS);
    float2 k = 1.0 / (size * float2(PW_ATLAS_COLS, PW_ATLAS_ROWS));
    float v = tex2Dgrad(_Glyphs, uv, dpx * k, dpy * k).r;
    return (0.5 - v) * 2.0 * PW_SPREAD * size;
}

float PwWidth(uint gs[PW_MAX_GLYPHS], uint n, float size)
{
    float w = 0.0;
    [unroll] for (uint i = 0; i < PW_MAX_GLYPHS; i++) if (i < n) w += PwAdvance(gs[i]);
    return w * size;
}

// min distance over a run of glyphs starting at x0 (left edge) on baseline by
float PwText(float2 p, float2 dpx, float2 dpy, float x0, float by, float size, uint gs[PW_MAX_GLYPHS], uint n)
{
    float d = PW_SPREAD * size;
    float x = x0;
    [unroll] for (uint i = 0; i < PW_MAX_GLYPHS; i++)
    {
        if (i < n)
        {
            float a = PwAdvance(gs[i]) * size;
            if (abs(p.x - (x + 0.5 * a)) < 0.5 * size) d = min(d, PwGlyph(p, dpx, dpy, x + 0.5 * a, by, size, gs[i]));
            x += a;
        }
    }
    return d;
}

// coverage of a distance field at the current pixel
float PwFill(float d, float2 dpx, float2 dpy) { return saturate(0.5 - d / max(length(abs(dpx) + abs(dpy)) * 0.5, 1e-6)); }

// "HH:MM"
void PwHourMinute(PwTime t, out uint gs[PW_MAX_GLYPHS], out uint n)
{
    [unroll] for (uint i = 0; i < PW_MAX_GLYPHS; i++) gs[i] = PW_SPACE;
    gs[0] = t.live ? t.hour / 10u : PW_DASH;
    gs[1] = t.live ? t.hour % 10u : PW_DASH;
    gs[2] = PW_COLON;
    gs[3] = t.live ? t.minute / 10u : PW_DASH;
    gs[4] = t.live ? t.minute % 10u : PW_DASH;
    n = 5u;
}

// "SS"
void PwSeconds(PwTime t, out uint gs[PW_MAX_GLYPHS], out uint n)
{
    [unroll] for (uint i = 0; i < PW_MAX_GLYPHS; i++) gs[i] = PW_SPACE;
    gs[0] = t.live ? t.second / 10u : PW_DASH;
    gs[1] = t.live ? t.second % 10u : PW_DASH;
    n = 2u;
}

// MON..SUN, three glyphs each (A 39, D 40, H 41, I 42, M 43, O 44, R 45, T 46, U 47 / N 28, E 29, W 30, F 25, S 27)
static const uint PW_DOW_EN[21] = { 43, 44, 28,  46, 47, 29,  30, 29, 40,  46, 41, 47,  25, 45, 42,  27, 39, 46,  27, 47, 28 };

// D73, by the menu language: "MM.DD (목)" Korean, "MM.DD (木)" Japanese, "THU MM.DD" English
void PwDate(PwTime t, out uint gs[PW_MAX_GLYPHS], out uint n)
{
    uint mo0 = t.live ? t.month / 10u : PW_DASH;
    uint mo1 = t.live ? t.month % 10u : PW_DASH;
    uint d0 = t.live ? t.day / 10u : PW_DASH;
    uint d1 = t.live ? t.day % 10u : PW_DASH;
    gs[9] = PW_SPACE;
    n = 9u;
    if (_DateLang == 0)
    {
        gs[0] = t.live ? PW_DOW_EN[t.weekday * 3u] : PW_DASH;
        gs[1] = t.live ? PW_DOW_EN[t.weekday * 3u + 1u] : PW_DASH;
        gs[2] = t.live ? PW_DOW_EN[t.weekday * 3u + 2u] : PW_DASH;
        gs[3] = PW_SPACE;
        gs[4] = mo0; gs[5] = mo1; gs[6] = PW_DOT; gs[7] = d0; gs[8] = d1;
        return;
    }
    gs[0] = mo0; gs[1] = mo1; gs[2] = PW_DOT; gs[3] = d0; gs[4] = d1;
    gs[5] = PW_SPACE;
    gs[6] = PW_LPAR;
    gs[7] = t.live ? (_DateLang == 1 ? PW_DOW_JA : PW_DOW_KO) + t.weekday : PW_DASH;
    gs[8] = PW_RPAR;
}

// a coordinate in metres to 0.1: "-123.4" (sign only when negative, no leading zeros, up to 9999.9)
void PwMetres(float v, out uint gs[PW_MAX_GLYPHS], out uint n)
{
    [unroll] for (uint i = 0; i < PW_MAX_GLYPHS; i++) gs[i] = PW_SPACE;
    uint t = (uint)min(round(abs(v) * 10.0), 99999.0);
    uint ip = t / 10u;
    bool neg = v <= -0.05;
    uint k = neg ? 1u : 0u;
    gs[0] = neg ? PW_DASH : gs[0];
    uint lead = ip >= 1000u ? 4u : (ip >= 100u ? 3u : (ip >= 10u ? 2u : 1u));
    uint div[4] = { 1000u, 100u, 10u, 1u };
    [unroll] for (uint j = 0; j < 4u; j++)
        if (j >= 4u - lead) { gs[k] = (ip / div[j]) % 10u; k++; }
    gs[k] = PW_DOT; k++;
    gs[k] = t % 10u; k++;
    n = k;
}

// up to two digits
void PwCount(uint c, out uint gs[PW_MAX_GLYPHS], out uint n)
{
    [unroll] for (uint i = 0; i < PW_MAX_GLYPHS; i++) gs[i] = PW_SPACE;
    gs[0] = c >= 10u ? (c / 10u) % 10u : c % 10u;
    gs[1] = c % 10u;
    n = c >= 10u ? 2u : 1u;
}

// this client's frames per second (Unity's smoothed frame time), up to three digits
void PwFps(out uint gs[PW_MAX_GLYPHS], out uint n)
{
    [unroll] for (uint i = 0; i < PW_MAX_GLYPHS; i++) gs[i] = PW_SPACE;
    uint f = (uint)clamp(round(unity_DeltaTime.w), 0.0, 999.0);
    n = f >= 100u ? 3u : (f >= 10u ? 2u : 1u);
    uint k = 0;
    if (n == 3u) { gs[k] = (f / 100u) % 10u; k++; }
    if (n >= 2u) { gs[k] = (f / 10u) % 10u; k++; }
    gs[k] = f % 10u;
}

// whole metres up to 9999 then "m"
void PwDistance(float m, out uint gs[PW_MAX_GLYPHS], out uint n)
{
    [unroll] for (uint i = 0; i < PW_MAX_GLYPHS; i++) gs[i] = PW_SPACE;
    uint v = (uint)clamp(round(m), 0.0, 9999.0);
    uint lead = v >= 1000u ? 4u : (v >= 100u ? 3u : (v >= 10u ? 2u : 1u));
    uint div[4] = { 1000u, 100u, 10u, 1u };
    uint k = 0;
    [unroll] for (uint j = 0; j < 4u; j++)
        if (j >= 4u - lead) { gs[k] = (v / div[j]) % 10u; k++; }
    gs[k] = PW_M; k++;
    n = k;
}

// "H:MM:SS" (hours up to 99)
void PwElapsed(float sec, out uint gs[PW_MAX_GLYPHS], out uint n)
{
    [unroll] for (uint i = 0; i < PW_MAX_GLYPHS; i++) gs[i] = PW_SPACE;
    uint s = (uint)clamp(sec, 0.0, 359999.0);
    uint h = s / 3600u, mi = (s / 60u) % 60u, se = s % 60u;
    uint k = 0;
    if (h >= 10u) { gs[k] = h / 10u; k++; }
    gs[k] = h % 10u; k++;
    gs[k] = PW_COLON; k++;
    gs[k] = mi / 10u; k++; gs[k] = mi % 10u; k++;
    gs[k] = PW_COLON; k++;
    gs[k] = se / 10u; k++; gs[k] = se % 10u; k++;
    n = k;
}

#endif // PULSEWATCH_TIME_CGINC
