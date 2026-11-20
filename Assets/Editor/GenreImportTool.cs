/*
================================================================================
Unity ジャンル別インポートツール (Genre Import Tool)
早瀬ユウカ式・計算通りのアセット整理
================================================================================


自動判定はしません。インポートする「あなた」がジャンルを決めます。
誤判定ゼロが、このツールの合理性です。

1) ツールの「インポート」ボタンから入れる場合
ジャンルを選ぶ（または新規作成する）→ ファイル／フォルダ／
.unitypackage を選ぶ → そのジャンルのフォルダへ入ります。

2) Projectウィンドウへ直接ドラッグ＆ドロップした場合
Unity標準ではインポート前に止める仕組みがないため、インポート直後に
「まだジャンル分けされていない新しいトップフォルダ」を検出し、
後からジャンルを選ぶ確認画面（GenreAssignmentPrompt）を自動で開きます。
そこでジャンルを選ぶと、そのフォルダごとジャンルフォルダへ移動します。


1. このファイル GenreImportTool.cs を、Unityプロジェクトの
Assets/Editor/ フォルダに入れてください。
（Editorフォルダがない場合は Assets の直下に「Editor」フォルダを
作成してください。ファイル名と置き場所がこれ以外だと動きません）
2. Unityに戻るとコンパイルが走ります。エラーが出なければ完了です。
3. メニュー Tools > ジャンル別インポート を開いてください。
4. 初回起動時に設定ファイルが自動作成されます：
Assets/Editor/GenreImportSettings.asset
ジャンル一覧・保留リスト・無視リストはここに保存されます。
この.asset は削除しないでください（ジャンル設定が初期化されます）。


[ジャンル管理]
- 既定ジャンルが8つ入っています。名前の変更・削除ができます。
- 「新しいジャンル名」に文字を入れて［ジャンルを追加］を押すと、
ジャンルと、そのフォルダ Assets/Sorted/<ジャンル>/ が作られます。
[インポート（推奨はこちら）]
- 対象ジャンルをプルダウンで選んでから：
・［ファイルを選んでインポート］ 外部のファイルをコピーして取り込む
・［フォルダを選んでインポート］ 外部のフォルダを丸ごと取り込む
・［.unitypackage をインポート］ 下記「制限」を必ず読んでください
[保留（未分類）の処理]
- 直接ドラッグで入れた未分類フォルダは「保留」リストに溜まります。
- 各行でジャンルを選んで［このジャンルへ移動］／
［そのまま置く（無視リストへ）］／［後で決める］を選べます。


Assets/
Sorted/ ← このツールが管理するルート（変更可）
アバター/
3Dモデル/
衣装/
ギミック/
シェーダー/
アニメーション/
テクスチャ・マテリアル/
ツール・スクリプト/
Editor/
GenreImportTool.cs ← このファイル
GenreImportSettings.asset ← 自動生成される設定


- 移動は必ず AssetDatabase.MoveAsset で行います。ファイルをOS上で
直接動かさないので、GUIDと参照（.meta）は保たれます。
- 移動の前に、必ず「何を → どこへ」動かすかの一覧ダイアログを出します。
キャンセルすれば何も動きません。
- それでも注意：アセット内部のスクリプト等がパスを文字列で決め打ち
（例: "Assets/○○/..." とハードコード）している場合、移動で壊れる
ことがあります。心配なアセットは［そのまま置く］を選んでください。


-.unitypackage は Unity の仕様上、パッケージ内に記録された元のパスへ
展開されます。任意のフォルダへ直接展開する公式APIはありません。
そのため本ツールでは ImportPackage 実行後、新しくできたトップフォルダを
「保留」として検出し、続けてジャンルへ移動する流れにしています。
- 直接ドラッグ時の検出は「Assets直下の新しいフォルダ」と
「Assets/Imported直下の新しいフォルダ」が対象です。Assets直下へ
単体ファイルを直接置いた場合は対象外です（フォルダ単位で整理する設計）。
- 対応バージョン目安： Unity 2020.3 以降（使用APIはそれ以前からある
ものが中心です）。URP/HDRP・VRChat SDK 等とは競合しません。

バージョン: 1.0
================================================================================
*/
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace YuukaTools.GenreImport
{
// ========================================================================
// 設定（ScriptableObjectとして Assets/Editor/ に保存される）
// ========================================================================
public class GenreImportSettings: ScriptableObject
{
public const string SettingsPath = "Assets/Editor/GenreImportSettings.asset";
public const string DefaultSortedRoot = "Assets/Sorted";

public List<string> genres = new List<string>();
public List<string> pendingFolders = new List<string>();
public List<string> ignoredFolders = new List<string>();
public string sortedRoot = DefaultSortedRoot;
public bool autoPromptOnImport = true;

public static readonly string[] DefaultGenres =
{
"アバター", "3Dモデル", "衣装", "ギミック",
"シェーダー", "アニメーション", "テクスチャ・マテリアル", "ツール・スクリプト"
};

// 常に無視するシステム系フォルダ（検出対象から除外）
public static readonly string[] SystemFolders =
{
"Assets/Editor", "Assets/Plugins", "Assets/Gizmos",
"Assets/Resources", "Assets/StreamingAssets", "Assets/WebGLTemplates"
};

private static GenreImportSettings _instance;

public static GenreImportSettings GetOrCreate()
{
if (_instance!= null) return _instance;
_instance = AssetDatabase.LoadAssetAtPath<GenreImportSettings>(SettingsPath);
if (_instance!= null) return _instance;

// フォルダとアセットを自動生成する
if (!AssetDatabase.IsValidFolder("Assets/Editor"))
AssetDatabase.CreateFolder("Assets", "Editor");
_instance = CreateInstance<GenreImportSettings>();
_instance.genres.AddRange(DefaultGenres);
AssetDatabase.CreateAsset(_instance, SettingsPath);
AssetDatabase.SaveAssets();
return _instance;
}

public void Save()
{
EditorUtility.SetDirty(this);
AssetDatabase.SaveAssets();
}

public string GenreFolder(string genre)
{
return sortedRoot.TrimEnd('/') + "/" + SanitizeFolderName(genre);
}

public static string SanitizeFolderName(string name)
{
if (string.IsNullOrWhiteSpace(name)) return "未分類";
var s = name.Trim();
foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
return s.Replace("/", "_").Replace("\\", "_");
}

public void EnsureGenreFolder(string genre)
{
EnsureFolder(sortedRoot);
var target = GenreFolder(genre);
if (!AssetDatabase.IsValidFolder(target))
{
var parent = Path.GetDirectoryName(target).Replace("\\", "/");
var leaf = Path.GetFileName(target);
AssetDatabase.CreateFolder(parent, leaf);
}
}

public static void EnsureFolder(string assetPath)
{
if (AssetDatabase.IsValidFolder(assetPath)) return;
var parts = assetPath.Split('/');
var cur = parts[0]; // "Assets"
for (int i = 1; i < parts.Length; i++)
{
var next = cur + "/" + parts[i];
if (!AssetDatabase.IsValidFolder(next))
AssetDatabase.CreateFolder(cur, parts[i]);
cur = next;
}
}
}

    // ========================================================================
    // 移動ロジック（全ウィンドウ共通）
    // ========================================================================
    public static class GenreMover
    {
        // 自分自身の移動・コピー中にポストプロセッサを止めるフラグ
        public static bool IsBusy;

        /// <summary>確認ダイアログを出してからフォルダをジャンルへ移動する。</summary>
        public static bool MoveFolderToGenre(string sourceFolder, string genre, bool showConfirm = true)
        {
            var settings = GenreImportSettings.GetOrCreate();
            if (!AssetDatabase.IsValidFolder(sourceFolder))
            {
                EditorUtility.DisplayDialog("ジャンル別インポート",
                    "移動元のフォルダが見つかりません:\n" + sourceFolder, "OK");
                return false;
            }
            settings.EnsureGenreFolder(genre);
            var leaf = Path.GetFileName(sourceFolder);
            var dest = settings.GenreFolder(genre) + "/" + leaf;
            dest = UniqueDestination(dest);

            if (showConfirm)
            {
                var msg = "以下を移動します。GUIDは保持されます。\n\n" +
                          "移動元: " + sourceFolder + "\n" +
                          "移動先: " + dest + "\n\n" +
                          "※ パスを決め打ちしているアセットは移動で壊れる場合があります。";
                if (!EditorUtility.DisplayDialog("移動の確認", msg, "移動する", "キャンセル"))
                    return false;
            }

            IsBusy = true;
            string error;
            try { error = AssetDatabase.MoveAsset(sourceFolder, dest); }
            finally { IsBusy = false; }

            if (!string.IsNullOrEmpty(error))
            {
                EditorUtility.DisplayDialog("移動に失敗しました", error + "\n\n元: " + sourceFolder + "\n先: " + dest, "OK");
                return false;
            }
            settings.pendingFolders.Remove(sourceFolder);
            settings.Save();
            AssetDatabase.Refresh();
            Debug.Log("[GenreImport] 移動しました: " + sourceFolder + " -> " + dest);
            return true;
        }

        private static string UniqueDestination(string dest)
        {
            if (!AssetDatabase.IsValidFolder(dest) &&
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(dest) == null) return dest;
            for (int i = 1; i < 100; i++)
            {
                var c = dest + "_" + i;
                if (!AssetDatabase.IsValidFolder(c) &&
                    AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(c) == null) return c;
            }
            return dest + "_" + Guid.NewGuid().ToString("N").Substring(0, 6);
        }

        /// <summary>外部フォルダをジャンルフォルダ配下へコピーして取り込む。</summary>
        public static bool CopyExternalFolder(string externalFolder, string genre)
        {
            var settings = GenreImportSettings.GetOrCreate();
            settings.EnsureGenreFolder(genre);
            var leaf = GenreImportSettings.SanitizeFolderName(Path.GetFileName(externalFolder.TrimEnd('/', '\\')));
            var destAsset = settings.GenreFolder(genre) + "/" + leaf;
            destAsset = UniqueDestination(destAsset);
            var destFull = ToFullPath(destAsset);
            IsBusy = true;
            try
            {
                CopyDirectory(new DirectoryInfo(externalFolder), new DirectoryInfo(destFull));
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("インポートに失敗しました", ex.Message, "OK");
                return false;
            }
            finally { IsBusy = false; }
            AssetDatabase.Refresh();
            Debug.Log("[GenreImport] フォルダを取り込みました: " + destAsset);
            return true;
        }

        /// <summary>外部ファイル（複数可）をジャンルフォルダ直下へ取り込む。</summary>
        public static bool CopyExternalFiles(string[] externalFiles, string genre)
        {
            var settings = GenreImportSettings.GetOrCreate();
            settings.EnsureGenreFolder(genre);
            IsBusy = true;
            try
            {
                foreach (var src in externalFiles)
                {
                    if (string.IsNullOrEmpty(src) || !File.Exists(src)) continue;
                    var name = GenreImportSettings.SanitizeFolderName(Path.GetFileNameWithoutExtension(src))
                               + Path.GetExtension(src);
                    var destFull = ToFullPath(settings.GenreFolder(genre) + "/" + name);
                    File.Copy(src, destFull, false);
                }
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("インポートに失敗しました", ex.Message, "OK");
                return false;
            }
            finally { IsBusy = false; }
            AssetDatabase.Refresh();
            return true;
        }

        public static string ToFullPath(string assetPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, assetPath.Replace("/", Path.DirectorySeparatorChar.ToString()));
        }

        private static void CopyDirectory(DirectoryInfo src, DirectoryInfo dest)
        {
            Directory.CreateDirectory(dest.FullName);
            foreach (var f in src.GetFiles())
            {
                if (f.Extension.Equals(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                f.CopyTo(Path.Combine(dest.FullName, f.Name), false);
            }
            foreach (var d in src.GetDirectories())
            {
                if (d.Name.StartsWith(".")) continue;
                CopyDirectory(d, new DirectoryInfo(Path.Combine(dest.FullName, d.Name)));
            }
        }
    }

    // ========================================================================
    // メインウィンドウ : Tools > ジャンル別インポート
    // ========================================================================
    public class GenreImportWindow : EditorWindow
    {
        private GenreImportSettings _s;
        private Vector2 _scroll;
        private string _newGenre = "";
        private int _importGenreIndex;
        private readonly Dictionary<string, int> _pendingGenreIndex = new Dictionary<string, int>();
        private readonly Dictionary<int, string> _renameBuffer = new Dictionary<int, string>();

        [MenuItem("Tools/ジャンル別インポート")]
        public static void Open()
        {
            var w = GetWindow<GenreImportWindow>("ジャンル別インポート");
            w.minSize = new Vector2(520, 560);
            w.Show();
        }

        private void OnEnable() { _s = GenreImportSettings.GetOrCreate(); }

        private void OnGUI()
        {
            if (_s == null) _s = GenreImportSettings.GetOrCreate();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.LabelField("ジャンル別インポート", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "自動判定はしません。入れる時にあなたがジャンルを決めるので誤判定はゼロです。\n" +
                "直接ドラッグで入れた未分類フォルダは、下の「保留」に溜まります。", MessageType.Info);

            DrawSettings();
            DrawGenreManager();
            DrawImporter();
            DrawPending();
            EditorGUILayout.EndScrollView();
        }

        private void DrawSettings()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("設定", EditorStyles.boldLabel);
            var root = EditorGUILayout.TextField("整理ルート", _s.sortedRoot);
            if (root != _s.sortedRoot && !string.IsNullOrWhiteSpace(root))
            { _s.sortedRoot = root.Trim(); _s.Save(); }
            var auto = EditorGUILayout.Toggle("インポート時に確認画面を自動で開く", _s.autoPromptOnImport);
            if (auto != _s.autoPromptOnImport) { _s.autoPromptOnImport = auto; _s.Save(); }
        }

        private void DrawGenreManager()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("ジャンル管理（" + _s.genres.Count + "件）", EditorStyles.boldLabel);
            for (int i = 0; i < _s.genres.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                if (!_renameBuffer.ContainsKey(i)) _renameBuffer[i] = _s.genres[i];
                _renameBuffer[i] = EditorGUILayout.TextField(_renameBuffer[i]);
                if (GUILayout.Button("名前を変更", GUILayout.Width(80)) &&
                    !string.IsNullOrWhiteSpace(_renameBuffer[i]) && _renameBuffer[i] != _s.genres[i])
                {
                    // 安全のためフォルダ自体は自動リネームしない。
                    // 既存フォルダ名を変えたい場合は Project ウィンドウで
                    // 手動リネームしてください（Unity経由ならGUIDは保持されます）。
                    _s.genres[i] = _renameBuffer[i].Trim();
                    _s.Save();
                    _s.EnsureGenreFolder(_s.genres[i]);
                }
                if (GUILayout.Button("フォルダ作成", GUILayout.Width(80))) _s.EnsureGenreFolder(_s.genres[i]);
                if (GUILayout.Button("開く", GUILayout.Width(44)))
                {
                    _s.EnsureGenreFolder(_s.genres[i]);
                    var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(_s.GenreFolder(_s.genres[i]));
                    EditorGUIUtility.PingObject(obj);
                }
                if (GUILayout.Button("削除", GUILayout.Width(44)) &&
                    EditorUtility.DisplayDialog("ジャンルを削除しますか?",
                        "ジャンル「" + _s.genres[i] + "」を一覧から削除します。\nフォルダと中身は削除されません。", "削除する", "キャンセル"))
                { _s.genres.RemoveAt(i); _s.Save(); _renameBuffer.Clear(); EditorGUILayout.EndHorizontal(); break; }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.BeginHorizontal();
            _newGenre = EditorGUILayout.TextField("新しいジャンル名", _newGenre);
            if (GUILayout.Button("ジャンルを追加", GUILayout.Width(110)) && !string.IsNullOrWhiteSpace(_newGenre))
            {
                var g = _newGenre.Trim();
                if (_s.genres.Contains(g)) EditorUtility.DisplayDialog("ジャンル別インポート", "同名のジャンルが既にあります。", "OK");
                else { _s.genres.Add(g); _s.Save(); _s.EnsureGenreFolder(g); _newGenre = ""; }
            }
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("全ジャンルのフォルダを作成"))
            { foreach (var g in _s.genres) _s.EnsureGenreFolder(g); AssetDatabase.Refresh(); }
        }

        private void DrawImporter()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("インポート（ジャンルを選んでから入れる）", EditorStyles.boldLabel);
            if (_s.genres.Count == 0) { EditorGUILayout.HelpBox("ジャンルがありません。先に追加してください。", MessageType.Warning); return; }
            _importGenreIndex = Mathf.Clamp(_importGenreIndex, 0, _s.genres.Count - 1);
            _importGenreIndex = EditorGUILayout.Popup("対象ジャンル", _importGenreIndex, _s.genres.ToArray());
            var genre = _s.genres[_importGenreIndex];
            EditorGUILayout.LabelField("取り込み先: " + _s.GenreFolder(genre) + "/");

            if (GUILayout.Button("ファイルを選んでインポート"))
            {
                var path = EditorUtility.OpenFilePanel("取り込むファイル", "", "");
                if (!string.IsNullOrEmpty(path)) GenreMover.CopyExternalFiles(new[] { path }, genre);
            }
            if (GUILayout.Button("フォルダを選んでインポート"))
            {
                var path = EditorUtility.OpenFolderPanel("取り込むフォルダ", "", "");
                if (!string.IsNullOrEmpty(path)) GenreMover.CopyExternalFolder(path, genre);
            }
            if (GUILayout.Button(".unitypackage をインポート"))
            {
                var path = EditorUtility.OpenFilePanelWithFilters(
                    "取り込む unitypackage", "", new[] { "Unity Package", "unitypackage" });
                if (!string.IsNullOrEmpty(path))
                {
                    EditorUtility.DisplayDialog("ジャンル別インポート",
                        ".unitypackage は元のパスへ展開されます（Unityの仕様）。\n" +
                        "展開後にできた新しいフォルダは「保留」に検出されるので、\n" +
                        "続けてジャンル「" + genre + "」へ移動してください。", "理解してインポートする");
                    // 第2引数 false = 標準のインポート確認ダイアログを出す
                    AssetDatabase.ImportPackage(path, true);
                }
            }
            EditorGUILayout.HelpBox("ヒント: 複数ファイルはフォルダにまとめて「フォルダを選んでインポート」が確実です。", MessageType.None);
        }

        private void DrawPending()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("保留 ― 未分類フォルダ（" + _s.pendingFolders.Count + "件）", EditorStyles.boldLabel);
            if (_s.pendingFolders.Count == 0)
            { EditorGUILayout.LabelField("保留はありません。計算通り、整理済みです。"); return; }

            // 消えたフォルダを掃除する
            _s.pendingFolders.RemoveAll(p => !AssetDatabase.IsValidFolder(p));

            foreach (var folder in new List<string>(_s.pendingFolders))
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField(folder, EditorStyles.boldLabel);
                if (!_pendingGenreIndex.ContainsKey(folder)) _pendingGenreIndex[folder] = 0;
                if (_s.genres.Count > 0)
                    _pendingGenreIndex[folder] = EditorGUILayout.Popup("ジャンル",
                        Mathf.Clamp(_pendingGenreIndex[folder], 0, _s.genres.Count - 1), _s.genres.ToArray());
                EditorGUILayout.BeginHorizontal();
                if (_s.genres.Count > 0 && GUILayout.Button("このジャンルへ移動"))
                    GenreMover.MoveFolderToGenre(folder, _s.genres[Mathf.Clamp(_pendingGenreIndex[folder], 0, _s.genres.Count - 1)]);
                if (GUILayout.Button("そのまま置く"))
                {
                    if (EditorUtility.DisplayDialog("そのまま置きますか?",
                        folder + "\n\nこのフォルダは無視リストに入り、今後は検出されません。", "そのまま置く", "キャンセル"))
                    { _s.pendingFolders.Remove(folder); if (!_s.ignoredFolders.Contains(folder)) _s.ignoredFolders.Add(folder); _s.Save(); }
                }
                if (GUILayout.Button("後で決める")) { /* 保留のまま残す。何もしないのが正解 */ }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }
            if (GUILayout.Button("無視リストをリセット（再検出できるようにする）"))
            { _s.ignoredFolders.Clear(); _s.Save(); }
            if (_s.ignoredFolders.Count > 0)
                EditorGUILayout.LabelField("無視中: " + string.Join(", ", _s.ignoredFolders));
        }
    }

    // ========================================================================
    // 割り当て確認ウィンドウ（直接ドラッグで入れた直後に自動で開く）
    // ========================================================================
    public class GenreAssignmentPrompt : EditorWindow
    {
        private GenreImportSettings _s;
        private int _genreIndex;
        private string _newGenre = "";

        public static void OpenIfPending()
        {
            var s = GenreImportSettings.GetOrCreate();
            s.pendingFolders.RemoveAll(p => !AssetDatabase.IsValidFolder(p));
            if (s.pendingFolders.Count == 0) return;
            var w = GetWindow<GenreAssignmentPrompt>("ジャンルの割り当て");
            w.minSize = new Vector2(480, 300);
            w.Show();
            w.Focus();
        }

        private void OnEnable() { _s = GenreImportSettings.GetOrCreate(); }

        private void OnGUI()
        {
            if (_s == null) _s = GenreImportSettings.GetOrCreate();
            _s.pendingFolders.RemoveAll(p => !AssetDatabase.IsValidFolder(p));
            EditorGUILayout.LabelField("新しいフォルダがインポートされました", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("ジャンルを選ぶと、そのジャンルのフォルダへ移動します。移動は AssetDatabase.MoveAsset で行うため GUID は保持されます。", MessageType.Info);
            if (_s.pendingFolders.Count == 0) { EditorGUILayout.LabelField("保留はありません。"); return; }

            var folder = _s.pendingFolders[0];
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("対象フォルダ:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(folder);
            if (_s.pendingFolders.Count > 1)
                EditorGUILayout.LabelField("ほか保留 " + (_s.pendingFolders.Count - 1) + " 件（処理後に順に表示されます）");

            if (_s.genres.Count > 0)
            {
                _genreIndex = Mathf.Clamp(_genreIndex, 0, _s.genres.Count - 1);
                _genreIndex = EditorGUILayout.Popup("ジャンル", _genreIndex, _s.genres.ToArray());
            }
            EditorGUILayout.BeginHorizontal();
            _newGenre = EditorGUILayout.TextField("新しいジャンルを作る", _newGenre);
            if (GUILayout.Button("作成して使う", GUILayout.Width(100)) && !string.IsNullOrWhiteSpace(_newGenre))
            {
                var g = _newGenre.Trim();
                if (!_s.genres.Contains(g)) { _s.genres.Add(g); _s.Save(); }
                _s.EnsureGenreFolder(g);
                _genreIndex = _s.genres.IndexOf(g);
                _newGenre = "";
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);
            if (_s.genres.Count > 0 && GUILayout.Button("このジャンルへ移動する", GUILayout.Height(30)))
            {
                if (GenreMover.MoveFolderToGenre(folder, _s.genres[_genreIndex]))
                { if (_s.pendingFolders.Count == 0) Close(); }
            }
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("そのまま置く（今後検出しない）"))
            {
                _s.pendingFolders.Remove(folder);
                if (!_s.ignoredFolders.Contains(folder)) _s.ignoredFolders.Add(folder);
                _s.Save();
                if (_s.pendingFolders.Count == 0) Close();
            }
            if (GUILayout.Button("後で決める"))
            {
                // 保留リストに残したまま閉じる。メインウィンドウの「保留」からいつでも処理できる。
                Close();
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    // ========================================================================
    // 検出 : AssetPostprocessor
    //   Assets直下 / Assets/Imported直下 の新しいトップフォルダを「保留」にする
    // ========================================================================
    public class GenreAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (GenreMover.IsBusy) return;
            GenreImportSettings s;
            try { s = GenreImportSettings.GetOrCreate(); }
            catch { return; } // 設定生成自体がインポート中の場合は静かに抜ける

            var changed = false;
            foreach (var path in importedAssets)
            {
                var top = GetUnclassifiedTopFolder(path, s);
                if (top == null) continue;
                if (s.pendingFolders.Contains(top) || s.ignoredFolders.Contains(top)) continue;
                if (GenreImportSettings.SystemFolders != null &&
                    Array.IndexOf(GenreImportSettings.SystemFolders, top) >= 0) continue;
                s.pendingFolders.Add(top);
                changed = true;
                Debug.Log("[GenreImport] 未分類フォルダを検出しました: " + top);
            }
            if (!changed) return;
            s.Save();
            if (s.autoPromptOnImport)
            {
                // インポート処理中にウィンドウを開くと不安定になるため、次フレームで開く
                EditorApplication.delayCall += () =>
                {
                    if (!GenreMover.IsBusy) GenreAssignmentPrompt.OpenIfPending();
                };
            }
        }

        /// <summary>path が未分類のトップフォルダ配下なら、そのトップフォルダを返す。対象外は null。</summary>
        private static string GetUnclassifiedTopFolder(string path, GenreImportSettings s)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/")) return null;
            if (path.StartsWith(s.sortedRoot.TrimEnd('/') + "/")) return null;      // 整理済み
            if (path.StartsWith("Assets/Editor/")) return null;                    // 自分自身と設定
            if (path == GenreImportSettings.SettingsPath) return null;

            var parts = path.Split('/');
            // Assets/<Folder>/...  → Assets/<Folder>
            if (parts.Length >= 2)
            {
                var top = "Assets/" + parts[1];
                // Assets/Imported/<Folder>/... → Assets/Imported/<Folder> を1単位として扱う
                if (parts[1] == "Imported" && parts.Length >= 3)
                    top = "Assets/Imported/" + parts[2];
                if (top == s.sortedRoot) return null;
                if (AssetDatabase.IsValidFolder(top)) return top;
                // フォルダ自体のインポート通知がまだ来ていない場合は、親として扱う
                if (parts.Length >= 3) return top;
            }
            return null; // Assets直下の単体ファイルは対象外（フォルダ単位で整理する設計）
        }
    }
}
