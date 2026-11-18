// Pyrite9 Pulse Watch — inspector section MENU (D75): where the 'Pulse Watch' submenu goes, after Sleeve Roll's 'Install to'
//   (SleeveRollEditor.Settings / .MenuTree). Two fields — a menu asset (installTo) or an MA Menu Item submenu of the avatar
//   (installToItem, Submenu Source: Children; used first when both are set) — and the avatar's Expression Menu as a tree: click a
//   row to put the submenu there, the chevron folds it.
//   The tree is what Modular Avatar builds before the build: the descriptor's menu and its submenus, what MA installers add to each
//   menu (a menu they append is an alias of the menu it goes into — installing into it lands there), and MA Menu Items with their
//   children (a Children submenu reads its direct children: items, groups, install targets). Unlike Sleeve Roll, a Menu Item
//   submenu can be picked. The count is the menu's items before the build (8 = full: MA moves the 8th on into 'More') — about,
//   since what other tools add at build isn't known here; Last build shows where the submenu really went.
//   Cached: rebuilt when the dirty counts of what it was built from, the hierarchy or the language change.
#if UNITY_EDITOR
using System.Collections.Generic;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace Pyrite9.PulseWatch
{
    public partial class PulseWatchEditor
    {
        sealed class MenuNode
        {
            public VRCExpressionsMenu menu;          // an asset menu (null for the root of an avatar without one, and for an item)
            public ModularAvatarMenuItem item;       // an MA Menu Item submenu (Submenu Source: Children)
            public string label;
            public int count;
            public int depth;
            public MenuNode parent;
            public readonly List<VRCExpressionsMenu> alias = new List<VRCExpressionsMenu>();   // menus MA installers append into this one
            public readonly List<MenuNode> kids = new List<MenuNode>();
            public Object Key => item != null ? (Object)item : menu;
        }

        // MA's Menu Install Target is internal (MA 1.18) — read by name, like the build plugin
        static readonly System.Type InstallTargetType = typeof(ModularAvatarMenuInstaller).Assembly.GetType("nadena.dev.modular_avatar.core.ModularAvatarMenuInstallTarget");
        static readonly System.Reflection.FieldInfo InstallTargetField = InstallTargetType?.GetField("installer");

        static int hierVersion;
        [InitializeOnLoadMethod] static void WatchHierarchy() => EditorApplication.hierarchyChanged += () => hierVersion++;

        MenuNode menuTree;
        int menuTreeKey = int.MinValue;
        readonly List<Object> menuTreeObjs = new List<Object>();
        readonly HashSet<Object> menuOpen = new HashSet<Object>();
        Object menuOpenFor;

        // the field is a Component (no MA reference in the runtime assembly); the object field only takes an MA Menu Item
        ModularAvatarMenuItem PwItem => Pw.installToItem as ModularAvatarMenuItem;

        VRCAvatarDescriptor Desc => Pw.GetComponentInParent<VRCAvatarDescriptor>(true);

        int MenuTreeKey()
        {
            var d = Desc;
            int k = unchecked(((hierVersion * 31 + lang) * 31) + (d != null ? d.GetInstanceID() : 0));
            foreach (var o in menuTreeObjs) k = unchecked(k * 31 + (o != null ? EditorUtility.GetDirtyCount(o) : -1));
            var it = PwItem;   // its warning reads it even when the tree doesn't reach it
            if (it != null) k = unchecked(k * 31 + EditorUtility.GetDirtyCount(it));
            return k;
        }

        MenuNode MenuTreeNow()
        {
            if (MenuTreeKey() == menuTreeKey) return menuTree;
            menuTreeObjs.Clear();
            menuTree = BuildMenuTree();
            menuTreeKey = MenuTreeKey();
            return menuTree;
        }

        MenuNode BuildMenuTree()
        {
            var d = Desc;
            if (d == null) return null;
            menuTreeObjs.Add(d);
            // installers taken by an install target are placed there, not by their own target menu (MA VirtualMenu.FreezeMenu)
            var targeted = new HashSet<ModularAvatarMenuInstaller>();
            if (InstallTargetType != null && InstallTargetField != null)
                foreach (var t in d.GetComponentsInChildren(InstallTargetType, true))
                {
                    menuTreeObjs.Add(t);
                    if (InstallTargetField.GetValue(t) is ModularAvatarMenuInstaller i && i != null) targeted.Add(i);
                }
            var appendTo = new Dictionary<VRCExpressionsMenu, List<ModularAvatarMenuInstaller>>();
            var toRoot = new List<ModularAvatarMenuInstaller>();
            foreach (var i in d.GetComponentsInChildren<ModularAvatarMenuInstaller>(true))
            {
                if (i == null) continue;
                menuTreeObjs.Add(i);
                if (targeted.Contains(i)) continue;
                var t = i.installTargetMenu;
                if (t == null || t == d.expressionsMenu) toRoot.Add(i);
                else
                {
                    if (!appendTo.TryGetValue(t, out var l)) appendTo[t] = l = new List<ModularAvatarMenuInstaller>();
                    l.Add(i);
                }
            }
            var root = new MenuNode { menu = d.expressionsMenu, label = T(lang, "Root", "ルート", "루트") };
            var seen = new HashSet<Object>();
            if (root.menu != null) seen.Add(root.menu);
            // breadth first, so a menu used twice shows where the radial menu reaches it first
            var queue = new Queue<MenuNode>();
            queue.Enqueue(root);

            void Kid(MenuNode n, MenuNode k)
            {
                if (k.Key == null || !seen.Add(k.Key)) return;
                k.depth = n.depth + 1;
                k.parent = n;
                n.kids.Add(k);
                queue.Enqueue(k);
            }
            // one MA menu source under n: an item (a submenu becomes a node), a group's items, or an install target's installer
            void Source(MenuNode n, Transform t, int guard)
            {
                if (t == null || guard > 16) return;
                var mi = t.GetComponent<ModularAvatarMenuItem>();
                if (mi != null)
                {
                    menuTreeObjs.Add(mi);
                    n.count++;
                    var c = mi.Control;
                    if (c == null || c.type != VRCExpressionsMenu.Control.ControlType.SubMenu) return;
                    string label = string.IsNullOrEmpty(mi.label) ? mi.gameObject.name : mi.label;   // MA's name for it
                    if (mi.MenuSource == SubmenuSource.Children) Kid(n, new MenuNode { item = mi, label = label });
                    else if (c.subMenu != null) Kid(n, new MenuNode { menu = c.subMenu, label = label });
                    return;
                }
                var g = t.GetComponent<ModularAvatarMenuGroup>();
                if (g != null)
                {
                    menuTreeObjs.Add(g);
                    var under = g.targetObject != null ? g.targetObject.transform : g.transform;
                    foreach (Transform k in under) Source(n, k, guard + 1);
                    return;
                }
                if (InstallTargetType != null && InstallTargetField != null)
                {
                    var it = t.GetComponent(InstallTargetType);
                    if (it != null && InstallTargetField.GetValue(it) is ModularAvatarMenuInstaller i && i != null) Install(n, i, null, guard + 1);
                }
            }
            // what an installer adds to n: its menu's controls (an alias of n), or its own MA menu source
            void Install(MenuNode n, ModularAvatarMenuInstaller i, List<VRCExpressionsMenu> sources, int guard)
            {
                // MA (NodeContextImpl.PushNode): the object's own Menu Item / Group first — a stale Menu To Append is ignored then
                bool ownSource = i.GetComponent<ModularAvatarMenuItem>() != null || i.GetComponent<ModularAvatarMenuGroup>() != null
                    || (InstallTargetType != null && i.GetComponent(InstallTargetType) != null);
                if (!ownSource && i.menuToAppend != null)
                {
                    if (sources != null && sources.Contains(i.menuToAppend)) return;
                    if (sources != n.alias) sources?.Add(i.menuToAppend);
                    n.alias.Add(i.menuToAppend);
                    Controls(n, i.menuToAppend);
                }
                else Source(n, i.transform, guard);
            }
            void Controls(MenuNode n, VRCExpressionsMenu m)
            {
                menuTreeObjs.Add(m);
                if (m.controls == null) return;
                foreach (var c in m.controls)
                {
                    if (c == null) continue;
                    n.count++;
                    if (c.type == VRCExpressionsMenu.Control.ControlType.SubMenu && c.subMenu != null)
                        Kid(n, new MenuNode { menu = c.subMenu, label = string.IsNullOrWhiteSpace(c.name) ? c.subMenu.name : c.name });
                }
            }

            while (queue.Count > 0)
            {
                var n = queue.Dequeue();
                if (n.item != null)
                {
                    var under = n.item.menuSource_otherObjectChildren != null ? n.item.menuSource_otherObjectChildren.transform : n.item.transform;
                    foreach (Transform k in under) Source(n, k, 0);
                    for (int s = 0; s < n.alias.Count; s++)
                        if (appendTo.TryGetValue(n.alias[s], out var al)) foreach (var i in al) Install(n, i, n.alias, 0);
                    continue;
                }
                // the menu's own controls first, then what installers add (MA's order): into this menu, and into the menus they
                // append here (those land here too)
                var sources = new List<VRCExpressionsMenu>();
                if (n.menu != null) { sources.Add(n.menu); Controls(n, n.menu); }
                if (n == root) foreach (var i in toRoot) Install(n, i, sources, 0);
                for (int s = 0; s < sources.Count; s++)
                    if (appendTo.TryGetValue(sources[s], out var l)) foreach (var i in l) Install(n, i, sources, 0);
            }
            return root;
        }

        static MenuNode FindNode(MenuNode n, VRCExpressionsMenu m, ModularAvatarMenuItem item)
        {
            if (n == null) return null;
            if (item != null ? n.item == item : m != null && (n.menu == m || n.alias.Contains(m))) return n;
            foreach (var k in n.kids) { var f = FindNode(k, m, item); if (f != null) return f; }
            return null;
        }

        // the MA Menu Item the build would use (D75): under this avatar, outside the watch, a submenu reading its children
        string ItemProblem(ModularAvatarMenuItem item)
        {
            var d = Desc;
            if (d == null || !item.transform.IsChildOf(d.transform))
                return T(lang, "is not in this avatar", "はこのアバターの中にありません", "은(는) 이 아바타 안에 없습니다");
            if (item.transform.IsChildOf(Pw.transform))
                return T(lang, "is inside the watch", "は時計の中にあります", "은(는) 시계 안에 있습니다");
            if (item.Control == null || item.Control.type != VRCExpressionsMenu.Control.ControlType.SubMenu || item.MenuSource != SubmenuSource.Children)
                return T(lang, "is not a submenu with Submenu Source: Children", "は Submenu Source: Children のサブメニューではありません", "은(는) Submenu Source: Children 하위 메뉴가 아닙니다");
            var other = item.menuSource_otherObjectChildren;
            if (other != null && (!other.transform.IsChildOf(d.transform) || other.transform.IsChildOf(Pw.transform)))
                return T(lang, $"reads the children of '{other.name}', which is not in this avatar",
                    $"が読む子の親「{other.name}」がこのアバターの中にありません",
                    $"이(가) 읽는 자식의 부모 '{other.name}'이(가) 이 아바타 안에 없습니다");
            return null;
        }

        // ---------------------------------------------------------------- 02 menu
        VisualElement MenuSection()
        {
            var sec = Section(Num(), T(lang, "MENU", "メニュー", "메뉴"), null);
            var grid = new VisualElement(); grid.AddToClassList("pw-rows");
            sec.Add(grid);
            var assetField = new ObjectField { objectType = typeof(VRCExpressionsMenu), allowSceneObjects = false, bindingPath = "installTo" };
            assetField.AddToClassList("pw-field");
            grid.Add(Row(T(lang, "Install to", "インストール先", "설치 위치"),
                "Install To — " + T(lang, "The menu the 'Pulse Watch' submenu goes in. Empty (both): the Expression Menu root. Pick a menu in this avatar's Expression Menu (one of its submenus, or one a Modular Avatar installer adds) — with any other menu the submenu is left out.",
                    "「Pulse Watch」サブメニューを入れるメニュー。両方空ならExpression Menu のルート。このアバターの Expression Menu にあるメニュー（そのサブメニュー、または Modular Avatar のインストーラーが追加するもの）を選んでください — それ以外のメニューではサブメニューが入りません。",
                    "'Pulse Watch' 하위 메뉴를 넣을 메뉴. 둘 다 비우면 Expression Menu 루트. 이 아바타의 Expression Menu에 있는 메뉴(그 하위 메뉴, 또는 Modular Avatar 인스톨러가 넣는 메뉴)를 고르세요 — 다른 메뉴면 하위 메뉴가 안 들어갑니다."),
                assetField));
            var itemField = new ObjectField { objectType = typeof(ModularAvatarMenuItem), allowSceneObjects = true, bindingPath = "installToItem" };
            itemField.AddToClassList("pw-field");
            grid.Add(Row(T(lang, "or MA Menu Item", "または MA Menu Item", "또는 MA Menu Item"),
                "Install To Item — " + T(lang, "Or an MA Menu Item submenu in this avatar (Submenu Source: Children), for a menu made of objects with no menu asset. The 'Pulse Watch' submenu goes in it. Used before Install to when both are set.",
                    "または、このアバターの MA Menu Item のサブメニュー（Submenu Source: Children）— メニューアセットのない、オブジェクトで作ったメニュー用。「Pulse Watch」サブメニューをその中に入れます。両方あるとこちらが優先。",
                    "또는 이 아바타의 MA Menu Item 하위 메뉴(Submenu Source: Children) — 메뉴 에셋 없이 오브젝트로 만든 메뉴용. 'Pulse Watch' 하위 메뉴를 그 안에 넣습니다. 둘 다 있으면 이쪽이 먼저."),
                itemField));
            var treeRow = Row(T(lang, "Avatar menu", "アバターのメニュー", "아바타 메뉴"),
                T(lang, "This avatar's Expression Menu. Click a menu to put the 'Pulse Watch' submenu there. n/8: the menu's items before the build — at 8 Modular Avatar moves the overflow into a 'More' submenu.",
                    "このアバターの Expression Menu。メニューをクリックすると「Pulse Watch」サブメニューをそこに入れます。n/8：ビルド前のそのメニューの項目数 — 8 になると Modular Avatar があふれた分を「More」サブメニューに移します。",
                    "이 아바타의 Expression Menu. 메뉴를 누르면 'Pulse Watch' 하위 메뉴를 거기 넣습니다. n/8: 빌드 전 그 메뉴의 항목 수 — 8이 되면 Modular Avatar가 넘친 항목을 'More' 하위 메뉴로 옮깁니다."),
                MenuTreeBox());
            treeRow.AddToClassList("pw-row--stack");
            grid.Add(treeRow);
            var warn = Warn("");
            var warnText = warn.Q<Label>(className: "pw-warn__text");
            sec.Add(warn);
            // what the build would do with these settings, said before the build — keyed on the cached tree, so an edited menu updates it
            int shown = int.MinValue;
            live.Add(() =>
            {
                MenuTreeNow();
                var a = Pw.installTo;
                var it = PwItem;
                int k = unchecked(menuTreeKey * 31 + (a != null ? a.GetInstanceID() : 0) * 961 + (it != null ? it.GetInstanceID() : 0));
                if (k == shown) return;
                shown = k;
                var lines = new List<string>();
                if (it != null)
                {
                    var bad = ItemProblem(it);
                    if (bad != null)
                        lines.Add(T(lang, $"MA Menu Item '{it.name}' {bad} — the build uses " + (a != null ? $"'{a.name}'." : "the root."),
                            $"MA Menu Item「{it.name}」{bad} — ビルドでは" + (a != null ? $"「{a.name}」を使います。" : "ルートを使います。"),
                            $"MA Menu Item '{it.name}'{bad} — 빌드는 " + (a != null ? $"'{a.name}'을(를) 씁니다." : "루트를 씁니다.")));
                    else
                    {
                        if (a != null)
                            lines.Add(T(lang, $"Both are set — the MA Menu Item '{it.name}' is used, '{a.name}' is not.",
                                $"両方設定されています — MA Menu Item「{it.name}」を使い、「{a.name}」は使いません。",
                                $"둘 다 설정됨 — MA Menu Item '{it.name}'을(를) 쓰고 '{a.name}'은(는) 안 씁니다."));
                        if (menuTree != null && FindNode(menuTree, null, it) == null)
                            lines.Add(T(lang, $"'{it.name}' is not reached from this avatar's Expression Menu — the submenu would be left out. Check Last build after Play.",
                                $"「{it.name}」はこのアバターの Expression Menu から届きません — サブメニューが入りません。Play のあと 最後のビルド で確認してください。",
                                $"'{it.name}'은(는) 이 아바타의 Expression Menu에서 닿지 않습니다 — 하위 메뉴가 안 들어갑니다. Play 뒤 마지막 빌드에서 확인하세요."));
                        a = null;   // not used
                    }
                }
                if (a != null && menuTree != null && FindNode(menuTree, a, null) == null)
                    lines.Add(T(lang, $"'{a.name}' is not in this avatar's Expression Menu — the submenu would be left out. Pick a menu from the avatar's menu, or check Last build after Play (a menu another tool adds at build time can still work).",
                        $"「{a.name}」はこのアバターの Expression Menu にありません — サブメニューが入りません。アバターのメニューにあるものを選ぶか、Play のあと 最後のビルド で確認してください（ビルド時に別のツールが追加するメニューなら動くことがあります）。",
                        $"'{a.name}'은(는) 이 아바타의 Expression Menu에 없습니다 — 하위 메뉴가 안 들어갑니다. 아바타 메뉴에 있는 것을 고르거나, Play 뒤 마지막 빌드에서 확인하세요(빌드 때 다른 도구가 넣는 메뉴면 될 수도 있습니다)."));
                warnText.text = string.Join("\n", lines);
                warn.style.display = lines.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            });
            return sec;
        }

        VisualElement MenuTreeBox()
        {
            var box = new VisualElement(); box.AddToClassList("pw-mtree");
            System.Action draw = null;
            draw = () =>
            {
                box.Clear();
                var t = menuTree;
                if (t == null)
                {
                    box.Add(Label(T(lang, "Put the watch under an avatar to see its menu.", "アバターの中に置くとメニューが表示されます。", "아바타 안에 두면 메뉴가 보입니다."), "pw-note"));
                    return;
                }
                var item = PwItem != null && ItemProblem(PwItem) == null ? PwItem : null;
                var menu = item != null ? null : Pw.installTo;
                bool rootSel = item == null && (menu == null || menu == t.menu);
                var selNode = rootSel ? t : FindNode(t, menu, item);
                Object sel = item != null ? (Object)item : menu;
                // open the path to the selection once per new selection — a fold closed afterwards stays closed
                if (sel != menuOpenFor)
                {
                    menuOpenFor = sel;
                    for (var p = selNode != null ? selNode.parent : null; p != null; p = p.parent) if (p.Key != null) menuOpen.Add(p.Key);
                }
                void Add(MenuNode n)
                {
                    bool open = n == t || (n.Key != null && menuOpen.Contains(n.Key));
                    box.Add(NodeRow(n, n == selNode, open, n == t, draw));
                    if (open) foreach (var k in n.kids) Add(k);
                }
                Add(t);
            };
            int drawn = int.MinValue;
            live.Add(() =>
            {
                MenuTreeNow();
                var a = Pw.installTo;
                var it = PwItem;
                int k = unchecked(menuTreeKey * 31 + (a != null ? a.GetInstanceID() : 0) * 961 + (it != null ? it.GetInstanceID() : 0));
                if (k == drawn) return;
                drawn = k;
                draw();
            });
            return box;
        }

        VisualElement NodeRow(MenuNode n, bool on, bool open, bool isRoot, System.Action redraw)
        {
            var row = new VisualElement(); row.AddToClassList("pw-mtree__row");
            row.EnableInClassList("pw-mtree__row--on", on);
            row.style.paddingLeft = 4 + n.depth * 16;
            var chev = Label(n.kids.Count == 0 || isRoot ? "" : open ? "▾" : "▸", "pw-mtree__chev");
            if (n.kids.Count > 0 && !isRoot)
                chev.RegisterCallback<ClickEvent>(e =>
                {
                    if (!menuOpen.Remove(n.Key)) menuOpen.Add(n.Key);
                    e.StopPropagation();
                    redraw();
                });
            row.Add(chev);
            row.Add(Label((on ? "✓ " : "") + n.label, "pw-mtree__name"));
            if (n.item != null) row.Add(Label("MA", "pw-mtree__asset"));
            else if (isRoot && n.menu != null) row.Add(Label(n.menu.name, "pw-mtree__asset"));
            bool full = n.count >= VRCExpressionsMenu.MAX_CONTROLS;
            var cnt = Label(full ? $"{n.count}/{VRCExpressionsMenu.MAX_CONTROLS} → More" : $"{n.count}/{VRCExpressionsMenu.MAX_CONTROLS}", "pw-mtree__count");
            cnt.EnableInClassList("pw-mtree__count--full", full);
            row.Add(cnt);
            string where = n.item != null ? T(lang, "MA Menu Item: ", "MA Menu Item：", "MA Menu Item: ") + n.item.name + "\n"
                : n.menu != null ? AssetDatabase.GetAssetPath(n.menu) + "\n" : "";
            row.tooltip = where
                + T(lang, $"{n.count} items before the build (about — tools that add items at build aren't counted).",
                    $"ビルド前の項目数 {n.count}（目安 — ビルド時に他のツールが足す項目は含みません）。",
                    $"빌드 전 항목 {n.count}개(대략 — 빌드 때 다른 도구가 넣는 항목은 안 셉니다).")
                + (full ? T(lang, "\nFull: Modular Avatar moves the overflow into a 'More' submenu at the end — the submenu ends up there.",
                    "\n満杯：Modular Avatar があふれた分を末尾の「More」サブメニューに移します — サブメニューはそこに入ります。",
                    "\n가득 참: Modular Avatar가 넘친 항목을 끝의 'More' 하위 메뉴로 옮깁니다 — 하위 메뉴는 거기 들어갑니다.") : "")
                + (on ? "" : T(lang, "\nClick to put the 'Pulse Watch' submenu here.", "\nクリックで「Pulse Watch」サブメニューをここに入れます。", "\n누르면 'Pulse Watch' 하위 메뉴를 여기에 넣습니다."));
            row.RegisterCallback<ClickEvent>(_ =>
            {
                serializedObject.Update();
                serializedObject.FindProperty("installTo").objectReferenceValue = isRoot || n.item != null ? null : n.menu;
                serializedObject.FindProperty("installToItem").objectReferenceValue = isRoot ? null : n.item;
                serializedObject.ApplyModifiedProperties();
                redraw();
            });
            return row;
        }
    }
}
#endif
