// radar cells (D23 / D36), small-code form for the first draw (dev docs 10-119): the same decision as PulseWatchProto, but the
// 120 slice readings are copied once into one array (LoadRad) and the inner loops are not unrolled — unrolled, SliceP was inlined
// 44 times, each with its own array copy, and the program grew to ~85 KB (the driver turns each KB into ~2 ms on the first draw)
// needs the _Rad*, _Pair, _PairFar, _CellR, _CellRFar, _PairK uniforms declared before it
#ifndef PW_CELLS2
#define PW_CELLS2
static float4 gRad[30];   // ring 0: 0-2, ring 1: 3-5, ring 2: 6-11, ring 3: 12-17, ring 4: 18-29
void LoadRad ()
{
    gRad[0] = _Rad0_0; gRad[1] = _Rad0_1; gRad[2] = _Rad0_2; gRad[3] = _Rad1_0; gRad[4] = _Rad1_1; gRad[5] = _Rad1_2;
    gRad[6] = _Rad2_0; gRad[7] = _Rad2_1; gRad[8] = _Rad2_2; gRad[9] = _Rad2_3; gRad[10] = _Rad2_4; gRad[11] = _Rad2_5;
    gRad[12] = _Rad3_0; gRad[13] = _Rad3_1; gRad[14] = _Rad3_2; gRad[15] = _Rad3_3; gRad[16] = _Rad3_4; gRad[17] = _Rad3_5;
    gRad[18] = _Rad4_0; gRad[19] = _Rad4_1; gRad[20] = _Rad4_2; gRad[21] = _Rad4_3; gRad[22] = _Rad4_4; gRad[23] = _Rad4_5;
    gRad[24] = _Rad4_6; gRad[25] = _Rad4_7; gRad[26] = _Rad4_8; gRad[27] = _Rad4_9; gRad[28] = _Rad4_10; gRad[29] = _Rad4_11;
}

// receivers (slices) per cell by ring (D36), and where the ring's slices start in gRad (in readings)
uint Slices (uint band) { return band < 2u ? 1u : (band < 4u ? 2u : 4u); }
uint SliceBase (uint band) { return band < 2u ? band * 12u : (band < 4u ? 24u + (band - 2u) * 24u : 72u); }

// proximity of the nearest head to slice m of the ring (m = dir x slices + slice, wrapped): 0 = none
float SliceP (uint m, uint band)
{
    uint i = SliceBase(band) + m % (12u * Slices(band));
    float4 v = gRad[i >> 2];
    uint k = i & 3u;
    return k == 0u ? v.x : (k == 1u ? v.y : (k == 2u ? v.z : v.w));
}

// the highest reading of ring nb's slices over slice m of the ring band (the same direction)
float SliceAcross (uint m, uint band, uint nb)
{
    uint s = Slices(band), sn = Slices(nb);
    if (sn <= s) return SliceP(m * sn / s, nb);
    float p = 0.0;
    [loop] for (uint i = 0u; i < sn / s; i++) p = max(p, SliceP(m * (sn / s) + i, nb));
    return p;
}

float CellR (uint band) { return band == 0u ? _CellR.x : (band == 1u ? _CellR.y : (band == 2u ? _CellR.z : (band == 3u ? _CellR.w : _CellRFar))); }
static const float CellEdgeM[6] = { 0.4, 1.5, 3.0, 5.0, 8.0, 14.0 };
float CellMid (uint band) { return 0.5 * (CellEdgeM[band] + CellEdgeM[band + 1u]); }

// the ring neighbour nb (same direction) takes the head: it is nearer, and the two are not far enough apart for two heads
bool LosesAcross (float p, uint band, float pn, uint nb)
{
    if (pn <= 0.001) return false;
    float r = CellR(band), rn = CellR(nb);
    float d = (1.0 - p) * r + _PairK.x, dn = (1.0 - pn) * rn + _PairK.x;
    bool two = d + dn < abs(CellMid(nb) - CellMid(band)) - _PairK.y * min(r, rn);
    return !two && (dn < d || (dn == d && nb < band));
}

// a head is in this slice unless a neighbour in the ring reads higher and the pair could be one head (ties go clockwise),
// or the ring inside / outside in the same direction is nearer to it
float SliceShown (uint m, uint band)
{
    float p = SliceP(m, band);
    if (p <= 0.001) return 0.0;
    float pair = band == 0u ? _Pair.x : (band == 1u ? _Pair.y : (band == 2u ? _Pair.z : (band == 3u ? _Pair.w : _PairFar)));
    float ccw = SliceP(m + 12u * Slices(band) - 1u, band), cw = SliceP(m + 1u, band);
    bool keep = (p > ccw || p + ccw >= pair) && (p >= cw || p + cw >= pair);
    if (band > 0u) keep = keep && !LosesAcross(p, band, SliceAcross(m, band, band - 1u), band - 1u);
    if (band < 4u) keep = keep && !LosesAcross(p, band, SliceAcross(m, band, band + 1u), band + 1u);
    return keep ? 1.0 : 0.0;
}

// a cell is occupied when any of its slices is
float CellShown (uint dir, uint band)
{
    uint s = Slices(band);
    [loop] for (uint i = 0u; i < s; i++) if (SliceShown(dir * s + i, band) > 0.5) return 1.0;
    return 0.0;
}
#endif
