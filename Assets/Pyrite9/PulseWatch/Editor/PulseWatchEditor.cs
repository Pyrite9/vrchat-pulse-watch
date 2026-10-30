// Pyrite9 Pulse Watch — PulseWatch inspector (UI Toolkit, design 'Assay' — _common/inspector-assay.md, copied from Drink Can's DrinkCanEditor)
//   about + features (fold) · 01 basics (menu language, wrist) · 02 include (feature switches, D32 — off = left out of the build)
//   · 03 placement (gauges) · 04 detection (open / close angles, dwell, debug meter) · 05 time (this PC's zone or a fixed offset)
//   · 06 last build (PwBuildRecord) · 07 changelog (fold — this version's line, read from the README '변경 기록 / Changelog / 更新履歴'
//   section). Fold state in EditorPrefs (Pyrite9PW.). Text follows the menu language. Tooltips start with the English name the README uses.
//   Style: PulseWatchInspector.uss next to this script — without it everything still works (Unity's default look).
//   Button groups, gauges, checks and the rows that show or hide with a setting refresh in place every 300 ms (rebuilding would
//   drop a gauge drag); only the menu language and a new build record rebuild.
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using CompareFunction = UnityEngine.Rendering.CompareFunction;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;

namespace Pyrite9.PulseWatch
{
    [CustomEditor(typeof(PulseWatch))]
    public class PulseWatchEditor : Editor
    {
        static readonly Color Gold = new Color32(0xE2, 0xCE, 0x74, 0xFF);

        VisualElement root;
        int lang = -1;
        string key;
        int secNo;
        readonly List<System.Action> live = new List<System.Action>();
        string Num() => (++secNo).ToString("00");

        // ---------------------------------------------------------------- text (English / Japanese / Korean = PulseWatchMenuLanguage order)
        static string T(int l, string en, string jp, string ko) => l == 1 ? jp : l == 2 ? ko : en;

        PulseWatch Pw => (PulseWatch)target;

        public override VisualElement CreateInspectorGUI()
        {
            root = new VisualElement();
            root.AddToClassList("pw-root");
            var dir = EditorDir();
            var uss = dir != null ? AssetDatabase.LoadAssetAtPath<StyleSheet>(dir + "/PulseWatchInspector.uss") : null;
            if (uss != null) root.styleSheets.Add(uss);
            Build();
            root.schedule.Execute(Poll).Every(300);
            // the scene view's wrist gizmo (OnSceneGUI) follows a gauge drag at once, not on the 300 ms poll
            string placed = null;
            root.schedule.Execute(() =>
            {
                if (target == null) return;
                var k = $"{Pw.alongForearm}|{Pw.wristRadius}|{(int)Pw.side}|{Pw.accentColor}";
                if (k != placed) { placed = k; SceneView.RepaintAll(); }
            }).Every(50);
            System.Action onChange = () => { if (root.panel != null) Build(); };
            PwUpdateCheck.Changed += onChange;
            root.RegisterCallback<DetachFromPanelEvent>(_ => PwUpdateCheck.Changed -= onChange);
            PwUpdateCheck.CheckIfDue();
            return root;
        }

        // rebuild only for what changes the layout; everything else refreshes in place
        string Key() => $"{(int)Pw.menuLanguage}|{PwBuildRecord.Stamp()}";

        void Poll()
        {
            if (target == null) return;
            serializedObject.UpdateIfRequiredOrScript();
            if (Key() != key) { Build(); return; }
            foreach (var a in live) a();
        }

        void Build()
        {
            serializedObject.Update();
            lang = (int)Pw.menuLanguage;
            key = Key();
            secNo = 0;
            live.Clear();
            root.Clear();
            root.Add(Header());
            var body = new VisualElement(); body.AddToClassList("pw-body");
            root.Add(body);
            body.Add(AboutSection());
            body.Add(Divider());
            body.Add(BasicSection());
            body.Add(Divider());
            body.Add(IncludeSection());
            body.Add(Divider());
            body.Add(PlacementSection());
            body.Add(Divider());
            body.Add(DetectionSection());
            body.Add(Divider());
            body.Add(TimeSection());
            body.Add(Divider());
            body.Add(BuildSection());
            body.Add(Divider());
            body.Add(ChangelogSection());
            root.Bind(serializedObject);
            foreach (var a in live) a();
        }

        // ---------------------------------------------------------------- header
        VisualElement Header()
        {
            var h = new VisualElement(); h.AddToClassList("pw-header");
            var left = new VisualElement(); left.AddToClassList("pw-header__left");
            var glyph = new VisualElement(); glyph.AddToClassList("pw-glyph");
            glyph.generateVisualContent += DrawGlyph;
            left.Add(glyph);
            var titles = new VisualElement(); titles.AddToClassList("pw-header__titles");
            titles.Add(Label("PYRITE9", "pw-kicker"));
            titles.Add(Label("Pulse Watch", "pw-title"));
            left.Add(titles);
            h.Add(left);
            h.Add(VersionBox());
            return h;
        }

        // version + new version check (PwUpdateCheck): click the version to check again, click the new-version line for the release page.
        // Until the public mirror has a Release (PwUpdateCheck.Enabled) only the version shows
        VisualElement VersionBox()
        {
            var box = new VisualElement(); box.AddToClassList("pw-versionbox");
            var ver = Label("v" + PulseWatchConst.Version, "pw-version");
            if (PwUpdateCheck.Enabled)
            {
                ver.tooltip = T(lang, "Click to check for updates", "クリックで更新を確認", "누르면 새 버전 확인");
                ver.RegisterCallback<ClickEvent>(_ => PwUpdateCheck.Check());
            }
            box.Add(ver);
            Label st;
            switch (PwUpdateCheck.Current)
            {
                case PwUpdateCheck.State.Checking: st = Label(T(lang, "checking…", "確認中…", "확인 중…"), "pw-update"); break;
                case PwUpdateCheck.State.Latest: st = Label(T(lang, "up to date", "最新", "최신"), "pw-update"); break;
                case PwUpdateCheck.State.Failed: st = Label(T(lang, "couldn't check", "確認できません", "확인 못 함"), "pw-update"); break;
                case PwUpdateCheck.State.Newer:
                    st = Label(T(lang, PwUpdateCheck.LatestTag + " available", PwUpdateCheck.LatestTag + " があります", PwUpdateCheck.LatestTag + " 있음"), "pw-update");
                    st.AddToClassList("pw-update--new");
                    st.tooltip = PwUpdateCheck.LatestUrl;
                    st.RegisterCallback<ClickEvent>(_ => Application.OpenURL(PwUpdateCheck.LatestUrl));
                    break;
                default: st = null; break;
            }
            if (st != null) box.Add(st);
            return box;
        }

        // watch from the front (34x34): strap ends top and bottom, the case with a crown, two hands, and a radar sweep arc inside
        static void DrawGlyph(MeshGenerationContext mgc)
        {
            var p = mgc.painter2D;
            p.strokeColor = Gold;
            p.lineCap = LineCap.Round; p.lineJoin = LineJoin.Round;
            void Line(float x0, float y0, float x1, float y1, float w)
            {
                p.lineWidth = w;
                p.BeginPath(); p.MoveTo(new Vector2(x0, y0)); p.LineTo(new Vector2(x1, y1)); p.Stroke();
            }
            void Arc(float r, float a0, float a1, float w)
            {
                p.lineWidth = w;
                p.BeginPath();
                const int n = 32;
                for (int i = 0; i <= n; i++)
                {
                    float a = Mathf.Lerp(a0, a1, i / (float)n) * Mathf.Deg2Rad;
                    var v = new Vector2(17f + r * Mathf.Cos(a), 17f + r * Mathf.Sin(a));
                    if (i == 0) p.MoveTo(v); else p.LineTo(v);
                }
                p.Stroke();
            }
            Line(12.5f, 9.5f, 13.5f, 2f, 1.2f); Line(21.5f, 9.5f, 20.5f, 2f, 1.2f);       // strap, top
            Line(12.5f, 24.5f, 13.5f, 32f, 1.2f); Line(21.5f, 24.5f, 20.5f, 32f, 1.2f);   // strap, bottom
            Arc(9f, 0f, 360f, 1.5f);                                                      // case
            Line(26.5f, 17f, 28.5f, 17f, 1.5f);                                           // crown
            Arc(5.5f, 200f, 300f, 0.8f);                                                  // radar sweep
            Line(17f, 17f, 17f, 11.5f, 1.2f);                                             // hour hand
            Line(17f, 17f, 21.5f, 18.5f, 1.2f);                                           // minute hand
        }

        // ---------------------------------------------------------------- about + features
        // short inspector text (the README 'Features' section is longer). Change both together.
        static readonly string[][] Features =
        {
            new[] { "Turn your wrist to your eyes and a hologram opens over the watch — clock, date and radar",
                    "手首を目に向けると時計の上にホログラムが開く — 時刻・日付・レーダー",
                    "손목을 눈 쪽으로 돌리면 시계 위에 홀로그램이 열림 — 시각·날짜·레이더" },
            new[] { "Radar: people around you out to 14 m on a 12-direction grid, with the count — only you see it",
                    "レーダー：14 m までの周りの人を 12 方向の格子で、人数とともに — 自分にだけ見える",
                    "레이더: 14 m 안 주변 사람을 12방향 격자로, 인원수와 함께 — 나에게만 보임" },
            new[] { "A scan wave on opening that runs over floors, walls and people — in any world, with its depth light",
                    "開くとスキャンの波が床・壁・人の上を走る — 深度ライトでどのワールドでも",
                    "열 때 스캔 파동이 바닥·벽·사람 위를 지나감 — 깊이 라이트로 어느 월드에서나" },
            new[] { "Waypoint: point at the button with your other hand to press it, aim the laser and mark a spot on the floor, a wall or the ceiling — for you, or shown to everyone; the disc shows where it is",
                    "ウェイポイント：反対の手を指さしの形にしてボタンを押し、レーザーで床・壁・天井に印を置く — 自分だけ、またはみんなに見せる。円盤に方向と距離",
                    "웨이포인트: 반대 손을 가리키는 자세로 버튼을 눌러 레이저로 바닥·벽·천장에 표시 — 나만, 또는 모두에게. 원판에 방향·거리" },
            new[] { "Flashlight: a second button lights a spot lamp from the watch toward where you look — only you see it",
                    "ライト：もう一つのボタンで時計から視線の方へスポットライト — 自分にだけ見える",
                    "손전등: 버튼 하나 더 — 시계에서 보는 쪽으로 스포트라이트, 나에게만 보임" },
            new[] { "A compass on the disc's ring, and how long you have been in the world on the clock",
                    "円盤の輪に方位、時計にワールドの滞在時間",
                    "원판 링에 나침반, 시계에 월드 체류 시간" },
            new[] { "The clock shows your own time zone and your world position to everyone; your frame rate is for your eyes only",
                    "時計はみんなに自分のタイムゾーンの時刻とワールド座標を表示、フレームレートは自分にだけ",
                    "시계는 모두에게 내 시간대 시각과 월드 좌표를, 프레임 레이트는 나에게만" },
            new[] { "Left or right wrist, back or inside of the wrist (switch both from the menu), and hide the hologram from others",
                    "左右の手首、手首の外側・内側（どちらもメニューで切り替え）、ホログラムを他の人に隠せる",
                    "왼손·오른손, 손목 바깥·안쪽(둘 다 메뉴로 전환), 홀로그램을 남에게 숨기기" },
            new[] { "Frosted glass behind the hologram, and its light running down to the wrist seen from the side",
                    "ホログラムの後ろはすりガラス、横から見ると光が手首まで流れ落ちる",
                    "홀로그램 뒤는 유리 블러, 옆에서 보면 빛이 손목까지 흘러내림" },
            new[] { "Fits the avatar's forearm at build time; each feature can be left out below",
                    "ビルド時にアバターの前腕に合わせる。機能ごとに外せる",
                    "빌드 때 아바타 팔뚝에 맞춤. 기능마다 뺄 수 있음" },
        };

        VisualElement AboutSection()
        {
            var sec = new VisualElement(); sec.AddToClassList("pw-section");
            sec.Add(Label(T(lang, "Drop it into a humanoid avatar — it fits itself to that avatar's wrist when you build. Your scene is not changed.",
                "ヒューマノイドのアバターに入れるだけで、ビルド時にそのアバターの手首に合わせます。シーンは変わりません。",
                "휴머노이드 아바타에 넣기만 하면 빌드할 때 그 아바타의 손목에 맞춰집니다. 씬은 바뀌지 않습니다."), "pw-intro"));
            var list = new VisualElement(); list.AddToClassList("pw-features");
            foreach (var f in Features)
            {
                var row = new VisualElement(); row.AddToClassList("pw-feature");
                var dot = new VisualElement(); dot.AddToClassList("pw-feature__dot"); row.Add(dot);
                row.Add(Label(f[Mathf.Clamp(lang, 0, 2)], "pw-feature__text"));
                list.Add(row);
            }
            sec.Add(Fold("Pyrite9PW.ShowFeatures", true, T(lang, "FEATURES", "機能", "기능"), list));
            return sec;
        }

        // fold: click the head (small title + chevron), state in EditorPrefs
        static VisualElement Fold(string prefKey, bool defaultOpen, string title, VisualElement content)
        {
            var box = new VisualElement(); box.AddToClassList("pw-fold");
            var head = new VisualElement(); head.AddToClassList("pw-fold__head");
            head.Add(Label(title, "pw-fold__title"));
            var chev = new VisualElement(); chev.AddToClassList("pw-fold__chevron");
            bool open = EditorPrefs.GetBool(prefKey, defaultOpen);
            chev.generateVisualContent += mgc =>
            {
                var p = mgc.painter2D;
                p.strokeColor = new Color32(0x7D, 0x83, 0x91, 0xFF); p.lineWidth = 1.5f; p.lineCap = LineCap.Round; p.lineJoin = LineJoin.Round;
                p.BeginPath();
                if (open) { p.MoveTo(new Vector2(2f, 4f)); p.LineTo(new Vector2(5f, 7f)); p.LineTo(new Vector2(8f, 4f)); }
                else { p.MoveTo(new Vector2(4f, 2f)); p.LineTo(new Vector2(7f, 5f)); p.LineTo(new Vector2(4f, 8f)); }
                p.Stroke();
            };
            head.Add(chev);
            box.Add(head);
            box.Add(content);
            content.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            head.RegisterCallback<ClickEvent>(_ =>
            {
                open = !open;
                EditorPrefs.SetBool(prefKey, open);
                content.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
                chev.MarkDirtyRepaint();
            });
            return box;
        }

        // ---------------------------------------------------------------- 01 basics
        VisualElement BasicSection()
        {
            var sec = Section(Num(), T(lang, "BASICS", "基本", "기본"), null);
            var grid = new VisualElement(); grid.AddToClassList("pw-rows");
            sec.Add(grid);
            grid.Add(Row(T(lang, "Menu language", "メニュー言語", "메뉴 언어"),
                "Menu Language — " + T(lang, "Expression Menu labels and the watch's date (THU 10.29 / 10.29 (木) / 10.29 (목)), applied at build.", "Expression Menu の表示と時計の日付（THU 10.29 / 10.29 (木) / 10.29 (목)）。ビルド時に反映。", "Expression Menu 표시와 시계 날짜(THU 10.29 / 10.29 (木) / 10.29 (목)). 빌드 때 적용."),
                Seg("menuLanguage", new[] { 0, 1, 2 }, new[] { "EN", "日本語", "한국어" })));
            grid.Add(Row(T(lang, "Wrist", "着ける手首", "차는 손목"),
                "Wrist — " + T(lang, "The wrist the watch is on at first. 'Settings > Wear on right wrist' in the menu switches it while playing (remembered).",
                    "最初に着ける手首。メニューの「設定 > 右手に着ける」でプレイ中も切り替えられます（保存されます）。",
                    "처음 차는 손목. 메뉴 '설정 > 오른손에 차기'로 실행 중에도 바꿀 수 있습니다(저장됨)."),
                Seg("side", new[] { (int)PulseWatchSide.Left, (int)PulseWatchSide.Right },
                    new[] { T(lang, "Left", "左手", "왼손"), T(lang, "Right", "右手", "오른손") })));
            // colours (D68): baked at build
            grid.Add(ColorRow("accentColor", T(lang, "Accent colour", "アクセント色", "액센트 색"),
                "Accent Color — " + T(lang, "The hologram's accent (hands, gauges, highlights) and the scan wave. Applied at build.",
                    "ホログラムのアクセント（針・ゲージ・強調）とスキャンの波。ビルド時に反映。",
                    "홀로그램 액센트(바늘·게이지·강조)와 스캔 파동. 빌드 때 적용."),
                PulseWatchConst.Accent));
            var wpColorRow = ColorRow("waypointColor", T(lang, "Waypoint colour", "ウェイポイント色", "웨이포인트 색"),
                "Waypoint Color — " + T(lang, "The waypoint's laser, mark, name tag and reticle, its arrow on the disc and the waypoint button. Applied at build.",
                    "ウェイポイントのレーザー・印・名札・照準、円盤の矢印とウェイポイントボタン。ビルド時に反映。",
                    "웨이포인트 레이저·표시·이름표·조준점, 원판의 화살표와 웨이포인트 버튼. 빌드 때 적용."),
                PulseWatchConst.WaypointColor);
            grid.Add(wpColorRow);
            var dimWarn = Warn(T(lang, "A dark colour fades: the hologram is light on dark glass, so dark colours show dim. Keep the colour bright.",
                "暗い色は沈みます：ホログラムは暗いガラスの上の光なので、暗い色はかすれて見えます。明るい色にしてください。",
                "어두운 색은 묻힙니다: 홀로그램은 어두운 유리 위의 빛이라 어두운 색은 흐리게 보입니다. 밝은 색으로 하세요."));
            sec.Add(dimWarn);
            live.Add(() =>
            {
                wpColorRow.SetEnabled(Pw.waypoint);
                bool dim = Dim(Pw.accentColor) || (Pw.waypoint && Dim(Pw.waypointColor));
                dimWarn.style.display = dim ? DisplayStyle.Flex : DisplayStyle.None;
            });
            sec.Add(Label(T(lang, "In VR, turn the watch face to your eyes and look at it. On desktop use 'Keep open' in the menu.",
                "VR では文字盤を目に向けて見ると開きます。デスクトップはメニューの「開いたままにする」。",
                "VR에서는 시계 면을 눈 쪽으로 돌려 보면 열립니다. 데스크톱은 메뉴 '계속 열기'."), "pw-note"));
            sec.Add(Label(T(lang, "'Settings > Wear on inside of wrist' in the menu moves it to the palm side (the tactical way) — then turn the palm up to read it.",
                "メニューの「設定 > 手首の内側に着ける」で手のひら側（タクティカル式）へ — そのときは手のひらを上に向けて見ます。",
                "메뉴 '설정 > 손목 안쪽에 차기'로 손바닥 쪽(택티컬 방식)으로 옮깁니다 — 그때는 손바닥을 위로 돌려서 봅니다."), "pw-note"));
            return sec;
        }

        // ---------------------------------------------------------------- 02 include (D32)
        VisualElement IncludeSection()
        {
            var sec = Section(Num(), T(lang, "INCLUDE", "入れる機能", "넣을 기능"), null);
            var grid = new VisualElement(); grid.AddToClassList("pw-rows");
            sec.Add(grid);
            var radar = new VisualElement(); radar.AddToClassList("pw-group");
            grid.Add(radar);
            radar.Add(Row(T(lang, "Radar", "レーダー", "레이더"),
                "Radar — " + T(lang, "People's heads around you out to 14 m on the disc under the clock, with the count — to you only. 120 Local Only contacts, no FX layer of its own (in the shared value layer).",
                    "14 m までの周りの人の頭を時計の下の円盤に、人数とともに — 自分にだけ。Local Only コンタクト 120、専用の FX レイヤーなし（共通の値レイヤー内）。",
                    "14 m 안 주변 사람의 머리를 시계 아래 원판에, 인원수와 함께 — 나에게만. Local Only Contact 120개, 전용 FX 레이어 없음(공용 값 레이어 안)."),
                Check("radar", T(lang, "People within 14 m", "14 m 以内の人", "14 m 안 사람"))));
            // the depth light (10-96) only serves the wave: a child row of the scan, dimmed while the scan is off
            var scanGroup = new VisualElement(); scanGroup.AddToClassList("pw-group");
            grid.Add(scanGroup);
            scanGroup.Add(Row(T(lang, "Scan", "スキャン", "스캔"),
                "Scan — " + T(lang, "Opening sends a wave from your eyes out to 14 m — to you only. With a depth texture it runs over floors, walls and people; without one, a ring and a floor grid. Not again within 5 s. 1 FX layer.",
                    "開くと目から 14 m まで波が広がります — 自分にだけ。深度テクスチャがあれば床・壁・人の上を、なければ輪と床の格子。5 秒以内は再生しません。FX レイヤー 1。",
                    "열면 눈에서 14 m까지 파동이 퍼집니다 — 나에게만. 깊이 텍스처가 있으면 바닥·벽·사람 위로, 없으면 고리와 바닥 격자. 5초 안엔 다시 안 나옴. FX 레이어 1개."),
                Check("scan", T(lang, "Wave on opening", "開くときの波", "열 때 파동"))));
            var depthRow = Child(Row(T(lang, "Depth light", "深度ライト", "깊이 라이트"),
                "Depth Light — " + T(lang, "Makes the depth texture in worlds that have none, so the wave runs over floors and walls everywhere: a directional light with shadows, on for the wave only and on your side only. It lights nothing and casts no visible shadow. 1 real Light — Performance Very Poor together with the flashlight's.",
                    "深度テクスチャのないワールドでも作り、波がどこでも床・壁の上を走るように：影付きのディレクショナルライトを波の間だけ、自分の側だけで点けます。何も照らさず、見える影も出ません。本物の Light 1 つ — ライトと合わせてパフォーマンス Very Poor。",
                    "깊이 텍스처가 없는 월드에서도 만들어 파동이 어디서나 바닥·벽 위를 지나가게: 그림자 있는 디렉셔널 라이트를 파동 동안만, 내 쪽에서만 켭니다. 아무것도 비추지 않고 보이는 그림자도 없음. 진짜 Light 1개 — 손전등과 합쳐 퍼포먼스 Very Poor."),
                Check("scanDepth", T(lang, "Over surfaces in any world", "どのワールドでも表面に", "어느 월드에서나 표면 따라"))));
            scanGroup.Add(depthRow);
            live.Add(() => depthRow.SetEnabled(Pw.scan));
            var wpGroup = new VisualElement(); wpGroup.AddToClassList("pw-group");
            grid.Add(wpGroup);
            wpGroup.Add(Row(T(lang, "Waypoint", "ウェイポイント", "웨이포인트"),
                "Waypoint — " + T(lang, "A button under the clock: press it with the other index, that hand pointing, for a laser; pull that hand's trigger to mark where it hits — floor, wall or ceiling (menu on desktop); the disc shows its direction and distance. 4 synced bits, 2 VRC Raycasts, 5 FX layers.",
                    "時計の下のボタン：反対の手を指さしの形にして人差し指で押すとレーザー、その手のトリガーで当たった所（床・壁・天井）に印（デスクトップはメニュー）。円盤に方向と距離。同期 4 bit、VRC Raycast 2、FX レイヤー 5。",
                    "시계 아래 버튼: 반대 손을 가리키는 자세로 검지로 누르면 레이저, 그 손 트리거로 닿은 곳(바닥·벽·천장)에 표시(데스크톱은 메뉴). 원판에 방향·거리. 동기화 4 bit, VRC Raycast 2개, FX 레이어 5개."),
                Check("waypoint", T(lang, "Button, laser and mark", "ボタン・レーザー・印", "버튼·레이저·표시"))));
            // name tag (D61): the check + the name; empty = the SDK account's name, shown as the hint under it
            var tagCtl = new VisualElement(); tagCtl.AddToClassList("pw-field-row");
            tagCtl.Add(Check("waypointName", ""));
            var tagName = new TextField { bindingPath = "waypointNameText", maxLength = PulseWatchConst.TagMaxChars };
            tagName.AddToClassList("pw-field");
            tagCtl.Add(tagName);
            var tagRow = Child(Row(T(lang, "Name tag", "名札", "이름표"),
                "Waypoint Name — " + T(lang, "Your name in a small dark tab above the mark, bobbing with it and keeping its size in the world like the mark (others see it when the mark is shown to them). Baked at build: empty = the display name signed in to the VRChat SDK — rebuild after a name change. 1 quad, 1 small texture.",
                    "印の上の小さな暗いタブにあなたの名前。印と一緒に上下し、印と同じく実際の大きさのまま（印を見せているときは他の人にも）。ビルド時に焼き込み：空欄 = VRChat SDK にサインイン中の表示名 — 名前を変えたら再ビルド。クアッド 1、小さなテクスチャ 1。",
                    "표시 위 작은 어두운 탭에 내 이름. 표시와 같이 오르내리고, 표시처럼 실제 크기 그대로(표시를 남에게 보일 땐 남에게도). 빌드 때 구움: 비우면 = VRChat SDK에 로그인된 표시 이름 — 이름을 바꾸면 다시 빌드. 쿼드 1개, 작은 텍스처 1개."),
                tagCtl));
            wpGroup.Add(tagRow);
            var tagHint = Label("", "pw-note");
            wpGroup.Add(tagHint);
            live.Add(() =>
            {
                tagRow.SetEnabled(Pw.waypoint);
                tagName.SetEnabled(Pw.waypoint && Pw.waypointName);
                bool show = Pw.waypoint && Pw.waypointName && string.IsNullOrWhiteSpace(Pw.waypointNameText);
                string sdk = show ? PulseWatchPlugin.SdkUserName(out _) : "";
                tagHint.text = !show ? "" : sdk.Length > 0
                    ? T(lang, "Empty: the SDK account's name — ", "空欄：SDK アカウントの名前 — ", "비움: SDK 계정 이름 — ") + sdk
                    : T(lang, "Empty and the VRChat SDK signed out: no tag until you sign in (or type a name).",
                        "空欄で VRChat SDK 未サインイン：サインイン（または名前を入力）するまで名札なし。",
                        "비었고 VRChat SDK 로그아웃: 로그인(또는 이름 입력)할 때까지 이름표 없음.");
                tagHint.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            });
            grid.Add(Row(T(lang, "Flashlight", "ライト", "손전등"),
                "Flashlight — " + T(lang, "A second tab beside the waypoint button: press it with the other index, that hand pointing (or the menu), to light a spot lamp from the watch toward where you look, with a short beam from the watch. You see it, others don't. 1 real Light (Performance: Poor), 2 FX layers.",
                    "ウェイポイントボタンの隣にもう一つのタブ：反対の手を指さしの形にして人差し指（またはメニュー）で押すと、時計から視線の方へスポットライト（時計から短い光の筋）。自分にだけ見えます。実ライト 1（パフォーマンス Poor）、FX レイヤー 2。",
                    "웨이포인트 버튼 옆에 탭 하나 더: 반대 손을 가리키는 자세로 검지(또는 메뉴)로 누르면 시계에서 내가 보는 쪽으로 스포트라이트(시계에서 짧은 빛줄기). 나에게만 보임. 진짜 Light 1개(퍼포먼스 Poor), FX 레이어 2개."),
                Check("flashlight", T(lang, "Tab and spot lamp", "タブとスポットライト", "탭·스포트라이트"))));
            grid.Add(Row(T(lang, "Voice ring", "声のリング", "목소리 링"),
                "Voice Ring — " + T(lang, "The disc's outer ring glows while you speak and turns red with a mic mark while you are muted. 2 FX layers.", "話している間、円盤の外側の輪が光り、ミュート中は赤くなってマイクの印が出ます。FX レイヤー 2。", "말하는 동안 원판 바깥 고리가 빛나고, 음소거 중엔 빨갛게 바뀌고 마이크 표시가 뜸. FX 레이어 2개."),
                Check("voiceRing", T(lang, "Glow while speaking, red when muted", "話すと光る・ミュートで赤", "말하면 빛남·음소거면 빨강"))));
            grid.Add(Row("FPS",
                "FPS — " + T(lang, "Your frame rate above the seconds — on your own screen only, no FX layer of its own (in the shared value layer).", "秒の上に自分のフレームレート — 自分の画面だけ。専用の FX レイヤーなし（共通の値レイヤー内）。", "초 위에 내 프레임 레이트 — 내 화면에만. 전용 FX 레이어 없음(공용 값 레이어 안)."),
                Check("fps", T(lang, "Frame rate on the clock", "時計にフレームレート", "시계에 프레임 레이트"))));
            grid.Add(Row(T(lang, "Coordinates", "座標", "좌표"),
                "Coordinates — " + T(lang, "The watch's world position (X Y Z, 0.1 m) next to the date. Shader only.", "日付の横に時計のワールド座標（X Y Z、0.1 m）。シェーダーのみ。", "날짜 옆에 시계의 월드 좌표(X Y Z, 0.1 m). 셰이더만."),
                Check("coordinates", T(lang, "Position next to the date", "日付の横に座標", "날짜 옆에 좌표"))));
            grid.Add(Row(T(lang, "Compass", "方位", "나침반"),
                "Compass — " + T(lang, "North on the disc's ring — a triangle and an N that turn as you turn (the world's +Z is north). One skinned quad over the disc, no FX layer.",
                    "円盤の輪に北 — 向きを変えると回る三角と N（ワールドの +Z が北）。円盤に重ねたスキンメッシュ 1、FX レイヤーなし。",
                    "원판 링에 북쪽 — 돌면 따라 도는 삼각형과 N(월드 +Z가 북쪽). 원판에 겹친 스킨 메시 1개, FX 레이어 없음."),
                Check("compass", T(lang, "North on the ring", "輪に北", "링에 북쪽"))));
            grid.Add(Row(T(lang, "Session time", "滞在時間", "세션 시간"),
                "Session Time — " + T(lang, "How long you have been in this world (from the avatar's load) right of the seconds gauge — on your own screen only. 1 FX layer.",
                    "このワールドにいる時間（アバターの読み込みから）を秒のゲージの右に — 自分の画面だけ。FX レイヤー 1。",
                    "이 월드에 있은 시간(아바타 로드부터)을 초 게이지 오른쪽에 — 내 화면에만. FX 레이어 1개."),
                Check("sessionTime", T(lang, "Time in this world", "ワールドの滞在時間", "월드 체류 시간"))));
            // light drip (D41) reads the screen the blur copied: a child row of the blur, dimmed while the blur is off
            var glass = new VisualElement(); glass.AddToClassList("pw-group");
            grid.Add(glass);
            glass.Add(Row(T(lang, "Glass blur", "すりガラス", "유리 블러"),
                "Glass Blur — " + T(lang, "Frosted glass: what shows through the disc and the clock is blurred. One GrabPass (the screen copied once a frame for everyone who sees it — a GPU cost on their side), 2 quads.",
                    "すりガラス：円盤と時計の向こうに見えるものをぼかします。GrabPass 1 つ（見ている全員の画面で毎フレーム 1 回コピー — その人の GPU 負荷）、クアッド 2。",
                    "유리 블러: 원판과 시계 너머로 비치는 것을 흐리게. GrabPass 1개(보는 모든 사람 화면에서 프레임마다 한 번 복사 — 그 사람 GPU 부담), 쿼드 2개."),
                Check("frost", T(lang, "Blur what shows through", "透けた所をぼかす", "비치는 곳 흐리기"))));
            var dripRow = Child(Row(T(lang, "Light drip", "光のにじみ", "빛 번짐"),
                "Light Drip — " + T(lang, "Seen from the side, the hologram's light runs straight down to your wrist; gone when you look at the watch, so the radar stays sharp. Needs the glass blur. One more GrabPass (a screen copy a frame for everyone who sees it), 1 quad.",
                    "横から見ると、ホログラムの光が手首までまっすぐ流れ落ちます。時計を見ているときは消えるので、レーダーはくっきりのまま。すりガラスが必要。GrabPass もう 1 つ（見ている全員の画面で毎フレーム 1 回コピー）、クアッド 1。",
                    "옆에서 보면 홀로그램 빛이 손목까지 곧게 흘러내림. 시계를 볼 때는 사라져서 레이더는 선명. 유리 블러가 필요. GrabPass 1개 더(보는 모든 사람 화면에서 프레임마다 한 번 복사), 쿼드 1개."),
                Check("drip", T(lang, "Down to the wrist", "手首まで", "손목까지"))));
            glass.Add(dripRow);
            live.Add(() => dripRow.SetEnabled(Pw.frost));
            sec.Add(Label(T(lang, "A feature switched off is left out of the build — no objects, contacts, layers or parameters.",
                "オフにした機能はビルドに入りません — オブジェクト・コンタクト・レイヤー・パラメーターなし。",
                "끈 기능은 빌드에서 빠집니다 — 오브젝트·Contact·레이어·파라미터 없음."), "pw-note"));
            return sec;
        }

        // ---------------------------------------------------------------- 03 placement
        VisualElement PlacementSection()
        {
            var sec = Section(Num(), T(lang, "PLACEMENT", "位置", "위치"), T(lang, "× forearm", "× 前腕", "× 팔뚝"));
            var grid = new VisualElement(); grid.AddToClassList("pw-rows");
            sec.Add(grid);
            grid.Add(GaugeRow("alongForearm", T(lang, "Along the forearm", "前腕の位置", "팔뚝 위 위치"),
                "Along Forearm — " + T(lang, "Where the watch sits from the elbow (0) to the wrist joint (1).", "肘（0）から手首の関節（1）までのどこに着けるか。", "팔꿈치(0)에서 손목 관절(1) 사이 어디에 차는지."),
                0.6f, 1f, float.NaN, () => 0.88f, 0.01f, "0.00"));
            grid.Add(GaugeRow("wristRadius", T(lang, "Face height", "文字盤の高さ", "시계 면 높이"),
                "Wrist Radius — " + T(lang, "Height of the watch face above the forearm's axis. Raise it if the face sinks into a thick sleeve.",
                    "前腕の軸から文字盤までの高さ。厚い袖に埋まるなら上げてください。", "팔뚝 축에서 시계 면까지 높이. 두꺼운 소매에 묻히면 올리세요."),
                0.03f, 0.2f, float.NaN, () => 0.09f, 0.005f, "0.000"));
            sec.Add(Label(T(lang, "Both are shares of the forearm length, so they fit any avatar size. Double-click a gauge to reset it.",
                "どちらも前腕の長さに対する割合なので、アバターの大きさによりません。ゲージをダブルクリックで元に戻ります。",
                "둘 다 팔뚝 길이에 대한 비율이라 아바타 크기와 상관없습니다. 게이지를 더블클릭하면 기본값으로."), "pw-note"));
            sec.Add(Label(T(lang, "Scene view: the radar disc's spot shows on both wrists (the default wrist bright, dotted = inside of the wrist). Red = behind or inside a mesh from that view — if it stays red from every side, the face is sunk in the sleeve: raise the face height.",
                "シーンビュー：両手首にレーダー円盤の位置が出ます（既定の手首が明るく、点線＝手首の内側）。赤＝その視点でメッシュの奥か中 — どの向きから見ても赤なら袖に埋まっています。文字盤の高さを上げてください。",
                "Scene 뷰: 두 손목에 레이더 원판 자리가 보입니다(기본 손목이 밝게, 점선 = 손목 안쪽). 빨강 = 그 시점에서 메시 뒤나 안 — 어느 쪽에서 봐도 빨갛다면 소매에 묻힌 것이니 시계 면 높이를 올리세요."), "pw-note"));
            return sec;
        }

        // ---------------------------------------------------------------- scene view: where the watch sits (03 placement)
        // the build's frame on the rest pose (PulseWatchPlugin.Seat — keep the two in step): forearm axis, the stick out to the face,
        // the radar disc (DiscLift above the face) on each wrist and the inside-of-the-wrist spot (D34) dotted. The disc is drawn twice:
        // in the accent where it is in front, red where a mesh is in front of it (zTest Greater) — a face sunk in a thick sleeve shows red
        // from every side without building. Not in Play mode (the build has run and the arms are posed). Hooked on duringSceneGui, not
        // OnSceneGUI: Unity skips OnSceneGUI while the scene view's Gizmos button is off, which many avatar makers keep off
        static readonly Color Buried = new Color(1f, 0.32f, 0.25f, 0.9f);

        void OnEnable() => SceneView.duringSceneGui += DrawWrists;
        void OnDisable() => SceneView.duringSceneGui -= DrawWrists;

        struct WristFrame { public Vector3 elbow, wrist, pivot, dorsal, seat, seatIn; public float radius, s; }

        static bool Wrist(Animator anim, Transform avatar, PulseWatch pw, bool left, out WristFrame f)
        {
            f = default;
            var lowerArm = anim.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
            var hand = anim.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            if (lowerArm == null || hand == null) return false;
            var forearm = hand.position - lowerArm.position;
            float len = forearm.magnitude;
            if (len < 1e-4f) return false;
            var axis = forearm / len;
            var fingers = anim.GetBoneTransform(left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
            var index = anim.GetBoneTransform(left ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal);
            var little = anim.GetBoneTransform(left ? HumanBodyBones.LeftLittleProximal : HumanBodyBones.RightLittleProximal);
            var fingerDir = fingers != null ? (fingers.position - hand.position).normalized : axis;
            var dorsal = index != null && little != null
                ? Vector3.Cross(fingerDir, (index.position - little.position).normalized) * (left ? 1f : -1f)
                : avatar.up;
            dorsal = Vector3.ProjectOnPlane(dorsal, axis).normalized;
            if (dorsal.sqrMagnitude < 0.5f) dorsal = Vector3.ProjectOnPlane(avatar.up, axis).normalized;
            f.elbow = lowerArm.position;
            f.wrist = hand.position;
            f.pivot = lowerArm.position + forearm * pw.alongForearm;
            f.dorsal = dorsal;
            f.radius = pw.wristRadius * len;
            f.s = len / 0.25f;
            f.seat = f.pivot + dorsal * f.radius;
            f.seatIn = f.pivot - dorsal * f.radius;
            return true;
        }

        void DrawWrists(SceneView view)
        {
            if (target == null || Event.current.type != EventType.Repaint || EditorApplication.isPlayingOrWillChangePlaymode) return;
            var desc = Pw.GetComponentInParent<VRCAvatarDescriptor>();
            var anim = desc != null ? desc.GetComponent<Animator>() : null;
            if (anim == null || !anim.isHuman) return;
            var accent = Pw.accentColor; accent.a = 1f;
            var oldZ = Handles.zTest;
            var oldColor = Handles.color;
            for (int i = 0; i < 2; i++)
            {
                bool left = i == 0;
                if (!Wrist(anim, desc.transform, Pw, left, out var f)) continue;
                bool home = left == (Pw.side == PulseWatchSide.Left);
                float a = home ? 1f : 0.4f;
                float discR = PulseWatchConst.DiscSize * 0.5f * f.s;
                var disc = f.seat + f.dorsal * PulseWatchConst.DiscLift * f.s;
                var discIn = f.seatIn - f.dorsal * PulseWatchConst.DiscLift * f.s;

                // axis and stick: always on top, faint (they run inside the arm)
                Handles.zTest = CompareFunction.Always;
                Handles.color = new Color(1f, 1f, 1f, 0.35f * a);
                Handles.DrawLine(f.elbow, f.wrist);
                Handles.color = new Color(accent.r, accent.g, accent.b, 0.6f * a);
                Handles.DrawLine(f.pivot, disc, home ? 2f : 1f);
                Handles.DrawDottedLine(f.pivot, discIn, 3f);

                foreach (var z in new[] { CompareFunction.LessEqual, CompareFunction.Greater })
                {
                    bool hidden = z == CompareFunction.Greater;
                    Handles.zTest = z;
                    var c = hidden ? Buried : accent;
                    Handles.color = new Color(c.r, c.g, c.b, c.a * a);
                    Handles.DrawWireDisc(disc, f.dorsal, discR, home ? 2.5f : 1f);
                    if (home)
                    {
                        Handles.color = new Color(c.r, c.g, c.b, (hidden ? 0.25f : 0.12f) * a);
                        Handles.DrawSolidDisc(disc, f.dorsal, discR);
                    }
                    // inside of the wrist (menu, D34): dotted ring on the palm side
                    Handles.color = new Color(c.r, c.g, c.b, 0.5f * c.a * a);
                    var side = Vector3.Cross(f.dorsal, (f.wrist - f.elbow).normalized);
                    const int n = 24;
                    for (int k = 0; k < n; k += 2)
                    {
                        var p0 = discIn + Quaternion.AngleAxis(360f * k / n, f.dorsal) * side * discR;
                        var p1 = discIn + Quaternion.AngleAxis(360f * (k + 1) / n, f.dorsal) * side * discR;
                        Handles.DrawLine(p0, p1);
                    }
                }
                if (home)
                {
                    Handles.zTest = CompareFunction.Always;
                    Handles.color = accent;
                    Handles.Label(disc + f.dorsal * discR * 0.5f,
                        $"Pulse Watch {(left ? "L" : "R")} · {T(lang < 0 ? (int)Pw.menuLanguage : lang, "face", "文字盤", "시계 면")} {f.radius * 1000f:0} mm");
                }
            }
            Handles.zTest = oldZ;
            Handles.color = oldColor;
        }

        // ---------------------------------------------------------------- 04 detection
        VisualElement DetectionSection()
        {
            var sec = Section(Num(), T(lang, "DETECTION", "検出", "감지"), "°");
            var grid = new VisualElement(); grid.AddToClassList("pw-rows");
            sec.Add(grid);
            grid.Add(GaugeRow("faceOpen", T(lang, "Face — open", "文字盤 — 開く", "시계 면 — 열림"),
                "Face Open — " + T(lang, "The watch face turned toward your eyes within this angle opens it.", "文字盤が目の方へこの角度以内なら開きます。", "시계 면이 눈 쪽으로 이 각도 안이면 열립니다."),
                10f, 60f, float.NaN, () => 40f, 1f, "0"));
            grid.Add(GaugeRow("faceClose", T(lang, "Face — close", "文字盤 — 閉じる", "시계 면 — 닫힘"),
                "Face Close — " + T(lang, "…and beyond this angle it closes. Keep it larger than 'open'.", "…この角度を超えると閉じます。「開く」より大きく。", "…이 각도를 넘으면 닫힙니다. '열림'보다 크게."),
                20f, 80f, float.NaN, () => 55f, 1f, "0"));
            var faceWarn = Warn(T(lang, "Face: 'close' has to be larger than 'open', or the watch flickers.",
                "文字盤：「閉じる」は「開く」より大きくしてください。でないと点滅します。",
                "시계 면: '닫힘'이 '열림'보다 커야 합니다. 아니면 깜빡입니다."));
            sec.Add(faceWarn);
            live.Add(() => faceWarn.style.display = Pw.faceClose > Pw.faceOpen ? DisplayStyle.None : DisplayStyle.Flex);
            var grid2 = new VisualElement(); grid2.AddToClassList("pw-rows"); grid2.style.marginTop = 8;
            sec.Add(grid2);
            grid2.Add(GaugeRow("gazeOpen", T(lang, "Gaze — open", "視線 — 開く", "시선 — 열림"),
                "Gaze Open — " + T(lang, "The watch near the middle of your view (head forward vs. the direction to the watch) within this angle.",
                    "時計が視界の中心近く（頭の正面と時計の方向）にこの角度以内。", "시계가 시야 가운데 가까이(머리 정면과 시계 방향) 이 각도 안."),
                10f, 70f, float.NaN, () => 45f, 1f, "0"));
            grid2.Add(GaugeRow("gazeClose", T(lang, "Gaze — close", "視線 — 閉じる", "시선 — 닫힘"),
                "Gaze Close — " + T(lang, "…and beyond this angle it closes. Keep it larger than 'open'.", "…この角度を超えると閉じます。「開く」より大きく。", "…이 각도를 넘으면 닫힙니다. '열림'보다 크게."),
                20f, 85f, float.NaN, () => 60f, 1f, "0"));
            var gazeWarn = Warn(T(lang, "Gaze: 'close' has to be larger than 'open', or the watch flickers.",
                "視線：「閉じる」は「開く」より大きくしてください。でないと点滅します。",
                "시선: '닫힘'이 '열림'보다 커야 합니다. 아니면 깜빡입니다."));
            sec.Add(gazeWarn);
            live.Add(() => gazeWarn.style.display = Pw.gazeClose > Pw.gazeOpen ? DisplayStyle.None : DisplayStyle.Flex);
            var grid3 = new VisualElement(); grid3.AddToClassList("pw-rows"); grid3.style.marginTop = 8;
            sec.Add(grid3);
            grid3.Add(GaugeRow("dwell", T(lang, "Hold time (s)", "待ち時間（秒）", "유지 시간(초)"),
                "Dwell — " + T(lang, "Both have to hold this long before the hologram opens.", "両方の条件がこの時間続くと開きます。", "두 조건이 이 시간 동안 이어져야 열립니다."),
                0f, 1f, float.NaN, () => 0.15f, 0.01f, "0.00"));
            sec.Add(Label(T(lang, "'Sensitivity: loose / strict' in the menu adds ±10° to all four angles in game.",
                "メニューの「感度：広く / 狭く」でプレイ中に 4 つの角度を ±10° 変えられます。",
                "메뉴 '감도: 넓게 / 좁게'로 실행 중 네 각도를 ±10° 바꿀 수 있습니다."), "pw-note"));
            var dbg = new VisualElement(); dbg.AddToClassList("pw-rows"); dbg.style.marginTop = 8;
            dbg.Add(Row(T(lang, "Debug meter", "デバッグメーター", "디버그 막대"),
                "Debug Meter — " + T(lang, "For tuning these angles in VR: bars above the forearm show how close the face and gaze are, with the open and close marks — to you only, menu toggle. 1 FX layer.",
                    "VR で角度を合わせるため：前腕の上のバーが文字盤・視線の近さと開く・閉じる位置を表示 — 自分にだけ、メニューで切り替え。FX レイヤー 1。",
                    "VR에서 각도를 맞추기 위한 것: 팔뚝 위 막대가 시계 면·시선의 가까움과 열림·닫힘 위치를 표시 — 나에게만, 메뉴로 전환. FX 레이어 1개."),
                Check("debugMeter", T(lang, "Build it (for tuning)", "作る（調整用）", "만들기(조정용)"))));
            sec.Add(dbg);
            return sec;
        }

        // ---------------------------------------------------------------- 05 time
        VisualElement TimeSection()
        {
            var pc = System.TimeZoneInfo.Local;
            var sec = Section(Num(), T(lang, "TIME", "時刻", "시각"), null);
            var grid = new VisualElement(); grid.AddToClassList("pw-rows");
            sec.Add(grid);
            var zone = new VisualElement(); zone.AddToClassList("pw-group");
            grid.Add(zone);
            zone.Add(Row(T(lang, "Time zone", "タイムゾーン", "시간대"),
                "Auto Time Zone — " + T(lang, "Use this PC's time zone (its standard offset and daylight saving rule) when the avatar is built. Everyone sees this time on your watch.",
                    "アバターをビルドするときこの PC のタイムゾーン（標準時と夏時間の規則）を使います。みんながあなたの時計でこの時刻を見ます。",
                    "아바타를 빌드할 때 이 PC의 시간대(표준시와 서머타임 규칙)를 씁니다. 모두가 내 시계에서 이 시각을 봅니다."),
                Check("autoTimeZone", T(lang, "This PC's time zone", "この PC のタイムゾーン", "이 PC의 시간대"))));
            var manual = Child(GaugeRow("utcOffsetMinutes", T(lang, "UTC offset (min)", "UTC との差（分）", "UTC 차이(분)"),
                "UTC Offset Minutes — " + T(lang, "Used when 'This PC's time zone' is off. Korea / Japan +540, UK 0, US Eastern -300, India +330.",
                    "「この PC のタイムゾーン」がオフのとき使います。韓国・日本 +540、イギリス 0、アメリカ東部 -300、インド +330。",
                    "'이 PC의 시간대'를 껐을 때 씁니다. 한국·일본 +540, 영국 0, 미국 동부 -300, 인도 +330."),
                -720f, 840f, 0f, () => 540f, 15f, "0"));
            zone.Add(manual);
            var utc = Label("", "pw-note");
            var auto = new VisualElement();
            auto.Add(Label(T(lang, "This PC: ", "この PC：", "이 PC: ") + Utc((int)System.Math.Round(pc.BaseUtcOffset.TotalMinutes)) + " · " + pc.StandardName, "pw-note"));
            if (PulseWatchPlugin.DstRule(pc).shift != 0)   // D72
                auto.Add(Label(T(lang, "This zone has daylight saving — the watch switches by date, by this year's rule.",
                    "このタイムゾーンには夏時間があります — 時計は今年の規則で日付に合わせて切り替わります。",
                    "이 시간대엔 서머타임이 있습니다 — 시계가 올해 규칙대로 날짜에 맞춰 바뀝니다."), "pw-note"));
            sec.Add(auto);
            sec.Add(utc);
            live.Add(() =>
            {
                bool a = Pw.autoTimeZone;
                auto.style.display = a ? DisplayStyle.Flex : DisplayStyle.None;
                manual.SetEnabled(!a);
                utc.text = a ? "" : "= " + Utc(Pw.utcOffsetMinutes);
                utc.style.display = a ? DisplayStyle.None : DisplayStyle.Flex;
            });
            return sec;
        }

        // colour field + a Default button (D68); alpha is not used
        VisualElement ColorRow(string path, string label, string tip, Color def)
        {
            var ctl = new VisualElement(); ctl.AddToClassList("pw-field-row");
            var f = new ColorField { bindingPath = path, showAlpha = false, hdr = false };
            f.AddToClassList("pw-field");
            ctl.Add(f);
            var reset = new Button(() =>
            {
                serializedObject.Update();
                serializedObject.FindProperty(path).colorValue = def;
                serializedObject.ApplyModifiedProperties();
            }) { text = T(lang, "Default", "既定", "기본") };
            reset.AddToClassList("pw-button");
            ctl.Add(reset);
            return Row(label, tip, ctl);
        }

        static bool Dim(Color c) => Mathf.Max(c.r, Mathf.Max(c.g, c.b)) < PulseWatchConst.DimColor;

        static string Utc(int min) => $"UTC{(min < 0 ? "-" : "+")}{System.Math.Abs(min) / 60}:{System.Math.Abs(min) % 60:00}";

        // check + text. The checkmark is drawn by hand (Unity's default image covers the gold fill)
        static VisualElement Check(string path, string text)
        {
            var box = new VisualElement(); box.AddToClassList("pw-check");
            var tg = new Toggle { bindingPath = path };
            tg.AddToClassList("pw-toggle");
            var mark = tg.Q(className: "unity-toggle__checkmark");
            if (mark != null)
            {
                mark.generateVisualContent += mgc =>
                {
                    if (!tg.value) return;
                    var p = mgc.painter2D;
                    p.strokeColor = new Color32(0x15, 0x17, 0x1C, 0xFF);
                    p.lineWidth = 2f; p.lineCap = LineCap.Round; p.lineJoin = LineJoin.Round;
                    p.BeginPath();
                    p.MoveTo(new Vector2(3.5f, 7.5f)); p.LineTo(new Vector2(6.5f, 10.5f)); p.LineTo(new Vector2(11.5f, 4.5f));
                    p.Stroke();
                };
                tg.RegisterValueChangedCallback(_ => mark.MarkDirtyRepaint());
            }
            box.Add(tg);
            box.Add(Label(text, "pw-check__text"));
            return box;
        }

        // gauge + value. center = NaN: fills from the left (sizes); otherwise fills from the center (offsets).
        // drag the gauge to change (snapped to step), double-click resets to def(); gold when not at def().
        // Pulse Watch addition: an int property (utcOffsetMinutes) gets an IntegerField and is written as int
        VisualElement GaugeRow(string path, string label, string tip, float min, float max, float center, System.Func<float> def, float step, string format)
        {
            bool centered = !float.IsNaN(center);
            var first = serializedObject.FindProperty(path);
            bool isInt = first != null && first.propertyType == SerializedPropertyType.Integer;
            var row = new VisualElement(); row.AddToClassList("pw-offset");
            row.tooltip = tip;
            row.Add(Label(label, "pw-label"));

            var gauge = new VisualElement(); gauge.AddToClassList("pw-gauge");
            var track = new VisualElement(); track.AddToClassList("pw-gauge__track"); gauge.Add(track);
            var zero = new VisualElement(); zero.AddToClassList("pw-gauge__zero"); gauge.Add(zero);
            if (!centered) zero.style.display = DisplayStyle.None;
            var fill = new VisualElement(); fill.AddToClassList("pw-gauge__fill"); gauge.Add(fill);
            var knob = new VisualElement(); knob.AddToClassList("pw-gauge__knob"); gauge.Add(knob);
            row.Add(gauge);

            VisualElement field = isInt ? (VisualElement)new IntegerField { bindingPath = path } : new FloatField { bindingPath = path, formatString = format };
            field.AddToClassList("pw-value");
            row.Add(field);

            float ToT(float v)
            {
                v = Mathf.Clamp(v, min, max);
                if (!centered) return (v - min) / (max - min);
                return v < center ? 0.5f * (v - min) / (center - min) : 0.5f + 0.5f * (v - center) / (max - center);
            }
            float FromT(float t)
            {
                t = Mathf.Clamp01(t);
                if (!centered) return Mathf.Lerp(min, max, t);
                return t < 0.5f ? Mathf.Lerp(min, center, t * 2f) : Mathf.Lerp(center, max, t * 2f - 1f);
            }
            void Show()
            {
                var prop = serializedObject.FindProperty(path);
                if (prop == null) return;
                float v = isInt ? prop.intValue : prop.floatValue;
                float t = ToT(v);
                float from = centered ? ToT(center) : 0f;
                fill.style.left = Length.Percent(Mathf.Min(t, from) * 100f);
                fill.style.width = Length.Percent(Mathf.Abs(t - from) * 100f);
                knob.style.left = Length.Percent(t * 100f);
                row.EnableInClassList("pw-offset--on", Mathf.Abs(v - def()) > step * 0.5f);
            }
            Show();
            live.Add(Show);

            void Set(float v)
            {
                v = Mathf.Clamp(Mathf.Round(v / step) * step, min, max);
                var prop = serializedObject.FindProperty(path);
                if (isInt)
                {
                    int iv = Mathf.RoundToInt(v);
                    if (prop.intValue == iv) return;
                    prop.intValue = iv;
                }
                else
                {
                    if (Mathf.Approximately(prop.floatValue, v)) return;
                    prop.floatValue = v;
                }
                serializedObject.ApplyModifiedProperties();
                Show();
            }
            // a number typed past the gauge's ends is pulled back in (the build clamps to the field's [Range] as well)
            field.RegisterCallback<ChangeEvent<float>>(e => { if (e.newValue < min || e.newValue > max) Set(e.newValue); else Show(); });
            field.RegisterCallback<ChangeEvent<int>>(e => { if (e.newValue < min || e.newValue > max) Set(e.newValue); else Show(); });
            bool drag = false;
            void SetFrom(Vector2 local)
            {
                float w = gauge.contentRect.width;
                if (w < 1f) return;
                Set(FromT(local.x / w));
            }
            gauge.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0) return;
                if (e.clickCount == 2) { Set(def()); return; }
                drag = true; gauge.CapturePointer(e.pointerId); SetFrom(e.localPosition);
            });
            gauge.RegisterCallback<PointerMoveEvent>(e => { if (drag) SetFrom(e.localPosition); });
            gauge.RegisterCallback<PointerUpEvent>(e => { drag = false; gauge.ReleasePointer(e.pointerId); });
            return row;
        }

        // button group bound to an int/enum property; the 'on' item refreshes in place (Poll)
        VisualElement Seg(string path, int[] values, string[] names, bool small = false)
        {
            var seg = new VisualElement(); seg.AddToClassList("pw-seg");
            if (small) seg.AddToClassList("pw-seg--small");
            var items = new List<(Button b, int v)>();
            void Refresh()
            {
                var prop = serializedObject.FindProperty(path);
                if (prop == null) return;
                foreach (var (b, v) in items) b.EnableInClassList("pw-seg__item--on", v == prop.intValue);
            }
            for (int i = 0; i < values.Length; i++)
            {
                int v = values[i];
                var b = new Button(() =>
                {
                    var prop = serializedObject.FindProperty(path);
                    prop.intValue = v;
                    serializedObject.ApplyModifiedProperties();
                    serializedObject.Update();
                    Refresh();
                }) { text = names[i] };
                b.AddToClassList("pw-seg__item");
                if (i == 0) b.AddToClassList("pw-seg__item--first");
                seg.Add(b);
                items.Add((b, v));
            }
            Refresh();
            live.Add(Refresh);
            return seg;
        }

        // ---------------------------------------------------------------- 06 last build
        VisualElement BuildSection()
        {
            var pw = Pw;
            var desc = pw.GetComponentInParent<VRCAvatarDescriptor>(true);
            var rec = desc != null ? PwBuildRecord.Get(desc.gameObject.name) : null;
            string meta = rec != null ? $"{rec.avatar} · {ShortTime(rec.time)}" : null;
            var sec = Section(Num(), T(lang, "LAST BUILD", "最後のビルド", "마지막 빌드"), meta);

            if (desc == null)
                sec.Add(Label(T(lang, "Put the watch inside an avatar (under the object with the VRC Avatar Descriptor).",
                    "アバターの中（VRC Avatar Descriptor のオブジェクトの下）に入れてください。",
                    "아바타 안(VRC Avatar Descriptor가 있는 오브젝트 밑)에 넣어 주세요."), "pw-note"));
            else if (rec == null)
                sec.Add(Label(T(lang, "No build yet — Play or Build & Test fills this in.",
                    "まだビルドの記録がありません。Play または Build & Test で表示されます。",
                    "아직 빌드 기록이 없습니다. Play 또는 Build & Test 때 채워집니다."), "pw-note"));
            else
            {
                var grid = new VisualElement(); grid.AddToClassList("pw-facts");
                sec.Add(grid);
                if (rec.built)
                {
                    grid.Add(Fact(T(lang, "Wrist", "手首", "손목"),
                        rec.side == (int)PulseWatchSide.Right ? T(lang, "Right", "右手", "오른손") : T(lang, "Left", "左手", "왼손"), null));
                    bool far = rec.scale < 0.4f || rec.scale > 2f;   // the watch scales with the forearm; only an odd rig is worth a look
                    grid.Add(Fact(T(lang, "Size", "大きさ", "크기"),
                        "×" + rec.scale.ToString("0.00") + (far ? T(lang, " — far from the reference forearm", " — 基準の前腕から遠い", " — 기준 팔뚝과 멂") : ""), far ? "pw-fact__value--gold" : null));
                    grid.Add(Fact(T(lang, "Clock", "時計", "시계"),
                        Utc(rec.utcOffsetMinutes) + (rec.autoTimeZone ? T(lang, " (this PC)", "（この PC）", " (이 PC)") : "")
                        + (rec.daylightSaving ? T(lang, " + daylight saving", " + 夏時間", " + 서머타임") : ""), null));
                    grid.Add(Fact(T(lang, "Features", "機能", "기능"),
                        $"{rec.featuresOn} / 13" + (rec.debugMeter ? T(lang, " + debug meter", " + デバッグメーター", " + 디버그 막대") : ""), rec.debugMeter ? "pw-fact__value--gold" : null));
                    grid.Add(Fact(T(lang, "Synced", "同期", "동기화"), rec.syncedBits + " bit", null));
                    grid.Add(Fact(T(lang, "FX layers", "FX レイヤー", "FX 레이어"), rec.fxLayers.ToString(), null));
                    grid.Add(Fact(T(lang, "Contacts", "コンタクト", "Contact"), rec.contacts.ToString(), null));
                }
                else
                    grid.Add(Fact(T(lang, "Watch", "時計", "시계"), T(lang, "not built — needs a humanoid avatar", "作られていません — ヒューマノイドが必要", "안 만들어짐 — 휴머노이드 필요"), "pw-fact__value--gold"));
                grid.Add(Fact(T(lang, "Warnings", "警告", "경고"), rec.warnings.Count == 0 ? T(lang, "none", "なし", "없음") : rec.warnings.Count.ToString(),
                    rec.warnings.Count == 0 ? null : "pw-fact__value--warn"));
                if (rec.version != PulseWatchConst.Version)
                    grid.Add(Fact(T(lang, "Built with", "ビルド時の版", "빌드 버전"), "v" + rec.version + T(lang, " — rebuild", " — 再ビルドを", " — 다시 빌드 필요"), "pw-fact__value--gold"));
                // English, as in the build log. OK folds one per avatar and warning
                for (int i = 0; i < Mathf.Min(6, rec.warnings.Count); i++)
                    sec.Add(Warn(rec.warnings[i], rec.avatar + "/" + rec.warnings[i]));
                if (rec.warnings.Count > 6) sec.Add(Label($"… +{rec.warnings.Count - 6}", "pw-note"));
            }

            var buttons = new VisualElement(); buttons.AddToClassList("pw-buttons");
            var logPath = Path.GetFullPath(PulseWatchConst.LogPath);
            var logBtn = new Button(() => EditorUtility.OpenWithDefaultApp(logPath)) { text = T(lang, "Build log", "ビルドログ", "빌드 로그") };
            logBtn.AddToClassList("pw-button");
            logBtn.SetEnabled(File.Exists(logPath));
            buttons.Add(logBtn);
            var readme = ReadmePath();
            var rmBtn = new Button(() => EditorUtility.OpenWithDefaultApp(readme)) { text = "README" };
            rmBtn.AddToClassList("pw-button");
            rmBtn.SetEnabled(readme != null);
            buttons.Add(rmBtn);
            sec.Add(buttons);
            return sec;
        }

        // ---------------------------------------------------------------- 07 changelog
        VisualElement ChangelogSection()
        {
            var readme = ReadmePath();
            var entries = readme != null ? ReadChangelog(readme) : new List<(string ver, string date, string text)>();
            var cur = entries.FirstOrDefault(e => e.ver == PulseWatchConst.Version);
            if (cur.ver == null && entries.Count > 0) cur = entries[0];
            var sec = Section(Num(), T(lang, "CHANGELOG", "更新履歴", "변경 기록"), cur.ver != null ? "v" + cur.ver + " · " + cur.date : null);
            var content = new VisualElement();
            if (cur.ver == null) content.Add(Label(T(lang, "See the README.", "README を見てください。", "README를 보세요."), "pw-note"));
            else content.Add(Label(cur.text, "pw-changelog"));
            if (readme != null)
            {
                var all = Label(T(lang, "Full changelog — README", "すべての更新履歴 — README", "전체 변경 기록 — README"), "pw-link");
                all.RegisterCallback<ClickEvent>(_ => EditorUtility.OpenWithDefaultApp(readme));
                content.Add(all);
            }
            sec.Add(Fold("Pyrite9PW.ShowChangelog", true, T(lang, "THIS VERSION", "この版", "이번 버전"), content));
            return sec;
        }

        // README '## 변경 기록 / Changelog / 更新履歴' lines "- **0.1.0** (2026-10-21): text" (indented next lines are joined)
        static List<(string ver, string date, string text)> ReadChangelog(string path)
        {
            var list = new List<(string ver, string date, string text)>();
            try
            {
                bool inSec = false;
                var rx = new System.Text.RegularExpressions.Regex(@"^-\s*\*\*(?<v>[0-9][0-9.]*)\*\*\s*[(（](?<d>[^)）]*)[)）]\s*[:：]\s*(?<t>.*)$");
                foreach (var line in File.ReadAllLines(path))
                {
                    if (line.StartsWith("## ")) { inSec = line.Contains("Changelog") || line.Contains("변경 기록") || line.Contains("更新履歴"); continue; }
                    if (!inSec) continue;
                    var m = rx.Match(line.Trim());
                    if (m.Success) list.Add((m.Groups["v"].Value, m.Groups["d"].Value.Trim(), Plain(m.Groups["t"].Value)));
                    else if (list.Count > 0 && line.StartsWith("  ") && line.Trim().Length > 0)
                    {
                        var last = list[list.Count - 1];
                        list[list.Count - 1] = (last.ver, last.date, last.text + " " + Plain(line.Trim()));
                    }
                }
            }
            catch { }
            return list;
        }

        static string Plain(string md) => md.Replace("**", "").Replace("`", "");

        // ---------------------------------------------------------------- paths
        string EditorDir()
        {
            var script = MonoScript.FromScriptableObject(this);
            return script != null ? Path.GetDirectoryName(AssetDatabase.GetAssetPath(script)) : null;
        }

        string PackageDir() { var d = EditorDir(); return d != null ? Path.GetDirectoryName(d) : null; }

        string ReadmePath()
        {
            var pkg = PackageDir();
            if (pkg == null) return null;
            string name = lang == 1 ? "README_jp.md" : lang == 2 ? "README_ko.md" : "README_en.md";
            var p = Path.GetFullPath(Path.Combine(pkg, name));
            return File.Exists(p) ? p : null;
        }

        static string ShortTime(string t)
        {
            if (!System.DateTime.TryParse(t, out var d)) return t;
            return d.Date == System.DateTime.Today ? d.ToString("HH:mm") : d.ToString("MM-dd HH:mm");
        }

        // ---------------------------------------------------------------- shared pieces
        // warning: orange box with a drawn warning sign (triangle + '!') — has to read as a warning at a glance.
        // okKey: an OK button folds the box to one dim line, remembered in EditorPrefs; the folded line's button opens it again. null = no button
        VisualElement Warn(string text, string okKey = null)
        {
            var box = new VisualElement(); box.AddToClassList("pw-warn");
            var icon = new VisualElement(); icon.AddToClassList("pw-warn__icon");
            icon.generateVisualContent += mgc =>
            {
                var p = mgc.painter2D;
                p.fillColor = new Color32(0xFF, 0x8A, 0x4C, 0xFF);
                p.BeginPath(); p.MoveTo(new Vector2(9f, 1f)); p.LineTo(new Vector2(17.5f, 16.5f)); p.LineTo(new Vector2(0.5f, 16.5f)); p.ClosePath(); p.Fill();
                p.strokeColor = new Color32(0x3A, 0x1F, 0x14, 0xFF); p.lineWidth = 2f; p.lineCap = LineCap.Round;
                p.BeginPath(); p.MoveTo(new Vector2(9f, 6.5f)); p.LineTo(new Vector2(9f, 11f)); p.Stroke();
                p.BeginPath(); p.MoveTo(new Vector2(9f, 13.8f)); p.LineTo(new Vector2(9f, 14f)); p.Stroke();
            };
            box.Add(icon);
            var label = Label(text, "pw-warn__text");
            box.Add(label);
            if (okKey == null) return box;

            string pref = "Pyrite9PW.WarnOk." + okKey;
            var btn = new Button(); btn.AddToClassList("pw-warn__ok");
            void Show(bool ok)
            {
                box.EnableInClassList("pw-warn--ok", ok);
                label.tooltip = ok ? text : null;   // folded to one line: the whole text on hover
                btn.text = ok ? T(lang, "Show", "開く", "펼치기") : T(lang, "OK", "確認", "확인");
                btn.tooltip = ok ? null : T(lang, "Seen it — fold this warning for this avatar.", "確認済み — このアバターのこの警告をたたみます。", "확인함 — 이 아바타의 이 경고를 접습니다.");
            }
            btn.clicked += () =>
            {
                bool ok = !EditorPrefs.GetBool(pref, false);
                if (ok) EditorPrefs.SetBool(pref, true); else EditorPrefs.DeleteKey(pref);
                Show(ok);
            };
            Show(EditorPrefs.GetBool(pref, false));
            box.Add(btn);
            return box;
        }

        static Label Label(string text, string cls)
        {
            var l = new Label(text);
            if (cls != null) foreach (var k in cls.Split(' ')) l.AddToClassList(k);
            return l;
        }

        static VisualElement Section(string num, string title, string meta)
        {
            var sec = new VisualElement(); sec.AddToClassList("pw-section");
            var head = new VisualElement(); head.AddToClassList("pw-section__head");
            head.Add(Label(num, "pw-section__num"));
            head.Add(Label(title, "pw-section__title"));
            if (meta != null) head.Add(Label(meta, "pw-section__meta"));
            sec.Add(head);
            return sec;
        }

        static VisualElement Divider() { var d = new VisualElement(); d.AddToClassList("pw-divider"); return d; }

        static VisualElement Row(string label, string tip, VisualElement control)
        {
            var row = new VisualElement(); row.AddToClassList("pw-row");
            row.tooltip = tip;
            row.Add(Label(label, "pw-label"));
            control.AddToClassList("pw-row__control");
            row.Add(control);
            return row;
        }

        // child row: a setting that only works with the row above it (UTC offset under Time zone).
        // Indented with a drawn connector from the parent; the caller dims it (SetEnabled) while the parent is off — it stays in
        // place so the dependency reads at a glance. Wrap the parent row and its children in a 'pw-group' so they sit together
        static VisualElement Child(VisualElement row)
        {
            row.AddToClassList("pw-row--child");
            var link = new VisualElement(); link.AddToClassList("pw-row__link");
            link.pickingMode = PickingMode.Ignore;
            link.generateVisualContent += mgc =>
            {
                var r = link.contentRect;
                var p = mgc.painter2D;
                p.strokeColor = new Color32(0x5A, 0x60, 0x6C, 0xFF); p.lineWidth = 1.2f; p.lineCap = LineCap.Round; p.lineJoin = LineJoin.Round;
                p.BeginPath(); p.MoveTo(new Vector2(1.5f, 0f)); p.LineTo(new Vector2(1.5f, r.height - 1f)); p.LineTo(new Vector2(r.width, r.height - 1f)); p.Stroke();
            };
            row.Insert(0, link);
            return row;
        }

        static VisualElement Fact(string label, string value, string valueCls)
        {
            var row = new VisualElement(); row.AddToClassList("pw-fact");
            row.Add(Label(label, "pw-label"));
            var v = Label(value, "pw-fact__value");
            if (valueCls != null) v.AddToClassList(valueCls);
            row.Add(v);
            return row;
        }
    }
}
#endif
