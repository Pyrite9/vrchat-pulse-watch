// Pyrite9 Pulse Watch — the light drip's pictures, baked at build time from this avatar's own watch (docs/pulse-watch.md 10-119).
//   The drip (PulseWatchDrip) reads, for each streak tap, what the hologram shows where the line of sight meets the disc / plate
//   plane. That picture is this watch as built — size, colours, features, language — shot from above by an orthographic camera,
//   one for the disc and one for the plate. What changes is left out (Proto keyword PW_BAKE: plate text and gauge fill; disc sweep,
//   scan front, outer ring, ticks, +, count tab) and drawn live by the drip, so one shot is enough. Session time off (others' view:
//   the full gauge). Face / Panel are opened for the shot (ancestors on, scale 1) and put back.
//   Async shader compilation is off while shooting (on, a variant seen for the first time comes out as the cyan stand-in).
//   Textures: sRGB (the GPU reads them back linear), BC7, no mips (the shader reads mip 0) — linear 8-bit / BC1 crushed the dark
//   backing tints and shifted their hue; BC7 sRGB is under half a step off a float shot.
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static Pyrite9.PulseWatch.PulseWatchConst;

namespace Pyrite9.PulseWatch
{
    internal static class PulseWatchBake
    {
        const int Layer = 31;
        const int Res = 512;

        internal class Target { public Texture2D tex; public Vector4 rect; public float h; }
        internal class Result
        {
            public Target disc, plate;
            public Vector4 plateM, plateT;   // plate: board units -> text units (p = M b + T), T.zw = aspect, gauge right end
            public float platePx;
            public string log;
        }

        static bool IsProto(Renderer r, params float[] modes) => r.sharedMaterial && r.sharedMaterial.shader.name == ShaderName && modes.Contains(r.sharedMaterial.GetFloat("_Mode"));

        // face: Holo/Face, panel: Holo/Panel, plate: Panel/Plate; the drip boards (Face/Drip, Plate/Drip) give the units — the drip
        // shader measures in its board's Y scale, which carries every scale above it at run time
        internal static Result Bake(Transform avatar, Transform face, Transform panel, Transform plate, Transform discBoard, Transform plateBoard)
        {
            var gos = new List<(GameObject go, bool active)>();
            for (var t = face; t != null && t != avatar; t = t.parent) gos.Add((t.gameObject, t.gameObject.activeSelf));
            for (var t = panel; t != null && t != avatar; t = t.parent) gos.Add((t.gameObject, t.gameObject.activeSelf));
            var scales = new[] { face, panel }.Select(t => (t, t.localScale)).ToArray();
            var discR = face.GetComponentsInChildren<Renderer>(true).Where(r => IsProto(r, 1f)).ToArray();
            var plateR = panel.GetComponentsInChildren<Renderer>(true).Where(r => IsProto(r, 0f, 4f) && r.gameObject.activeSelf).ToArray();
            var quad = plateR.FirstOrDefault(r => r.sharedMaterial.GetFloat("_Mode") < 0.5f);
            if (discR.Length == 0 || quad == null) return null;
            var all = discR.Concat(plateR).ToArray();
            var enabled = all.Select(r => r.enabled).ToArray();
            var mats = all.Select(r => r.sharedMaterial).ToArray();
            var layers = all.Select(r => r.gameObject.layer).ToArray();
            var temp = new Dictionary<Material, Material>();
            bool async = ShaderUtil.allowAsyncCompilation;
            var res = new Result();
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                foreach (var (go, _) in gos) go.SetActive(true);
                foreach (var (t, _) in scales) t.localScale = Vector3.one;
                for (int i = 0; i < all.Length; i++)
                {
                    var m = mats[i];
                    if (!temp.TryGetValue(m, out var b))
                    {
                        b = new Material(m) { hideFlags = HideFlags.HideAndDontSave };
                        b.EnableKeyword("PW_BAKE");
                        b.SetFloat("_SesOn", 0f); b.SetFloat("_Muted", 0f); b.SetFloat("_Voice", 0f);
                        temp[m] = b;
                    }
                    all[i].sharedMaterial = b; all[i].enabled = true;   // the layer per shot (Shot): only its own renderers
                }
                float discUnit = discBoard.lossyScale.y, plateUnit = plateBoard.lossyScale.y;
                float discH = DiscLayers[4];   // the outer ring's layer, the brightest
                res.disc = Shot(discR, face.position + face.up * (discH * discUnit), face.right, face.up, face.forward, discUnit, discH, 0.004f);
                res.plate = Shot(plateR, plate.position, plate.right, plate.up, plate.forward, plateUnit, 0f, 0.003f);
                PlateMap(res, quad, mats[System.Array.IndexOf(all, quad)], plate.position, plate.right, plate.forward, plateUnit);
                res.log = System.FormattableString.Invariant($"disc {discR.Length} unit {discUnit:F4} {res.disc.tex.width}x{res.disc.tex.height}, plate {plateR.Length} unit {plateUnit:F4} {res.plate.tex.width}x{res.plate.tex.height}, text scale {res.plateM.x:F2}");
            }
            finally
            {
                for (int i = 0; i < all.Length; i++) { all[i].sharedMaterial = mats[i]; all[i].enabled = enabled[i]; all[i].gameObject.layer = layers[i]; }
                foreach (var (t, s) in scales) t.localScale = s;
                for (int i = gos.Count - 1; i >= 0; i--) gos[i].go.SetActive(gos[i].active);
                foreach (var b in temp.Values) Object.DestroyImmediate(b);
                ShaderUtil.allowAsyncCompilation = async;
            }
            return res;
        }

        // the plate quad's mesh: board coordinates of three corners against their text coordinates (uv.x * aspect, uv.y)
        static void PlateMap(Result res, Renderer quad, Material m, Vector3 o, Vector3 ax, Vector3 az, float unit)
        {
            var mesh = quad.GetComponent<MeshFilter>().sharedMesh;
            var v = mesh.vertices; var uv = mesh.uv;
            float A = m.GetFloat("_Aspect");
            Vector2 B(int i) { var w = quad.transform.TransformPoint(v[i]) - o; return new Vector2(Vector3.Dot(w, ax), Vector3.Dot(w, az)) / unit; }
            Vector2 P(int i) => new Vector2(uv[i].x * A, uv[i].y);
            int i0 = 0, i1 = 1, i2 = 2;
            for (int k = 2; k < v.Length; k++) { var e1 = B(i1) - B(i0); var e2 = B(k) - B(i0); if (Mathf.Abs(e1.x * e2.y - e1.y * e2.x) > 1e-8f) { i2 = k; break; } }
            Vector2 b1 = B(i1) - B(i0), b2 = B(i2) - B(i0), p1 = P(i1) - P(i0), p2 = P(i2) - P(i0);
            float det = b1.x * b2.y - b2.x * b1.y;
            // M [b1 b2] = [p1 p2] -> M = [p1 p2] [b1 b2]^-1
            float i00 = b2.y / det, i01 = -b2.x / det, i10 = -b1.y / det, i11 = b1.x / det;
            res.plateM = new Vector4(p1.x * i00 + p2.x * i10, p1.x * i01 + p2.x * i11, p1.y * i00 + p2.y * i10, p1.y * i01 + p2.y * i11);
            var b0 = B(i0);
            var t = P(i0) - new Vector2(res.plateM.x * b0.x + res.plateM.y * b0.y, res.plateM.z * b0.x + res.plateM.w * b0.y);
            res.plateT = new Vector4(t.x, t.y, A, A - 0.20f);   // the gauge's end with the session time off
            res.platePx = res.plate.rect.z / res.plate.tex.width * new Vector2(res.plateM.x, res.plateM.y).magnitude;
        }

        static Target Shot(Renderer[] rs, Vector3 o, Vector3 ax, Vector3 up, Vector3 az, float unit, float h, float pad)
        {
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var r in rs)
            {
                var b = r is SkinnedMeshRenderer sk ? sk.sharedMesh.bounds : r.GetComponent<MeshFilter>().sharedMesh.bounds;
                for (int k = 0; k < 8; k++)
                {
                    var c = b.center + Vector3.Scale(b.extents, new Vector3((k & 1) != 0 ? 1 : -1, (k & 2) != 0 ? 1 : -1, (k & 4) != 0 ? 1 : -1));
                    var p = r.transform.TransformPoint(c) - o;
                    float x = Vector3.Dot(p, ax) / unit, z = Vector3.Dot(p, az) / unit;
                    minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minZ = Mathf.Min(minZ, z); maxZ = Mathf.Max(maxZ, z);
                }
            }
            minX -= pad; maxX += pad; minZ -= pad; maxZ += pad;
            float sx = maxX - minX, sz = maxZ - minZ;
            int w = sx >= sz ? Res : Mathf.CeilToInt(Res * sx / sz), hgt = sz >= sx ? Res : Mathf.CeilToInt(Res * sz / sx);
            w = (w + 3) / 4 * 4; hgt = (hgt + 3) / 4 * 4;   // block compression wants multiples of 4
            var rect = new Vector4(minX, minZ, sx, sz);
            var center = o + (ax * (minX + sx * 0.5f) + az * (minZ + sz * 0.5f)) * unit;
            var go = new GameObject("PulseWatch bake camera") { hideFlags = HideFlags.HideAndDontSave };
            var cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.transform.SetPositionAndRotation(center + up * 0.2f * unit, Quaternion.LookRotation(-up, az));
            cam.orthographic = true; cam.orthographicSize = sz * 0.5f * unit; cam.aspect = sx / sz;
            cam.nearClipPlane = 0.01f * unit; cam.farClipPlane = 0.4f * unit;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.cullingMask = 1 << Layer; cam.allowMSAA = true; cam.allowHDR = true;
            var rt = new RenderTexture(w, hgt, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear) { antiAliasing = 8 };
            cam.targetTexture = rt;
            // only this target's renderers on the bake layer: with both on it (0.1.1) the disc's picture caught the plate's date tab
            // and the plate's the disc's rim where they sat at build time, and the plate moves with the eyes — a stray block of drip
            // at the disc's edge, worst on the inside of the wrist (10-121). Bake's finally puts every layer back
            var lay = rs.Select(r => r.gameObject.layer).ToArray();
            foreach (var r in rs) r.gameObject.layer = Layer;
            cam.Render();
            for (int i = 0; i < rs.Length; i++) rs[i].gameObject.layer = lay[i];
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var read = new Texture2D(w, hgt, TextureFormat.RGBAHalf, false, true);
            read.ReadPixels(new Rect(0, 0, w, hgt), 0, 0); read.Apply();
            RenderTexture.active = prev;
            var px = read.GetPixels().Select(c => new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), 1f).gamma).ToArray();
            var tex = new Texture2D(w, hgt, TextureFormat.RGBA32, false, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            tex.SetPixels(px); tex.Apply();
            EditorUtility.CompressTexture(tex, TextureFormat.BC7, TextureCompressionQuality.Best);
            Object.DestroyImmediate(read); cam.targetTexture = null; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(go);
            return new Target { tex = tex, rect = rect, h = h };
        }
    }
}
#endif
