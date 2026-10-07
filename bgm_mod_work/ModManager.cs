using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows.Forms;

class ModManager : Form
{
    static readonly string[] Names = { "patch.xp3", "bgm_music_mod/player.tjs", "bgm_music_mod/README.md" };
    static readonly Dictionary<string, byte[]> Payload = new Dictionary<string, byte[]>();
    static readonly Dictionary<string, HashSet<string>> Legacy = new Dictionary<string, HashSet<string>>();
    TextBox directory = new TextBox();
    Label status = new Label();

    static byte[] Resource(string name) {
        using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
        using (MemoryStream data = new MemoryStream()) { stream.CopyTo(data); return data.ToArray(); }
    }
    static string Hash(byte[] data) {
        using (SHA256 sha = SHA256.Create()) { return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant(); }
    }
    static bool Owned(string name, byte[] data) {
        HashSet<string> old;
        string hash = Hash(data);
        return hash == Hash(Payload[name]) || (Legacy.TryGetValue(name, out old) && old.Contains(hash));
    }
    static string Target(string root, string name) { return Path.Combine(root, name.Replace('/', Path.DirectorySeparatorChar)); }
    static void WriteAtomic(string path, byte[] data) {
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try {
            File.WriteAllBytes(temporary, data);
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        } finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    static void Change(string root, bool install, bool test) {
        root = Path.GetFullPath(root);
        if (!File.Exists(Path.Combine(root, "MaitetsuLastRun.exe"))) throw new Exception("请选择包含 MaitetsuLastRun.exe 的游戏目录。");
        if (!test && Process.GetProcessesByName("MaitetsuLastRun").Length != 0) throw new Exception("请先关闭游戏，再安装或卸载 MOD。");
        // Fixed filenames only; never recursively delete user directories.
        Dictionary<string, byte[]> before = new Dictionary<string, byte[]>();
        foreach (string name in Names) {
            string path = Target(root, name);
            if (Directory.Exists(path)) throw new Exception("目标文件名已被文件夹占用：" + name);
            byte[] data = File.Exists(path) ? File.ReadAllBytes(path) : null;
            if (data != null && !Owned(name, data)) throw new Exception("文件属于其他补丁或已被修改，操作已停止：" + name);
            before.Add(name, data);
        }
        string modDir = Path.Combine(root, "bgm_music_mod");
        bool created = !Directory.Exists(modDir);
        List<string> changed = new List<string>();
        try {
            if (install) Directory.CreateDirectory(modDir);
            // Install the hook archive last; remove it first on uninstall.
            string[] order = install ? new string[] { Names[1], Names[2], Names[0] } : Names;
            foreach (string name in order) {
                string path = Target(root, name);
                if (install) WriteAtomic(path, Payload[name]);
                else if (before[name] != null) File.Delete(path);
                changed.Add(name);
            }
        } catch {
            changed.Reverse();
            foreach (string name in changed) {
                string path = Target(root, name);
                if (before[name] != null) WriteAtomic(path, before[name]);
                else if (File.Exists(path)) File.Delete(path);
            }
            if (created && Directory.Exists(modDir) && Directory.GetFileSystemEntries(modDir).Length == 0) Directory.Delete(modDir);
            throw;
        }
        if (!install && Directory.Exists(modDir) && Directory.GetFileSystemEntries(modDir).Length == 0) Directory.Delete(modDir);
    }
    static string Inspect(string root) {
        if (String.IsNullOrWhiteSpace(root) || !File.Exists(Path.Combine(root, "MaitetsuLastRun.exe"))) return "请选择有效游戏目录";
        string patch = Target(root, Names[0]), player = Target(root, Names[1]);
        if (Directory.Exists(patch) || Directory.Exists(player)) return "文件冲突：运行文件名被文件夹占用";
        if (!File.Exists(patch)) return File.Exists(player) ? "未安装／已停用（残留播放器文件）" : "BGM MOD 未安装";
        byte[] patchData = File.ReadAllBytes(patch);
        if (!Owned(Names[0], patchData)) return "文件冲突：其他补丁或 patch.xp3 已修改";
        if (!File.Exists(player)) return "安装不完整：缺少播放器，请安装／更新";
        byte[] playerData = File.ReadAllBytes(player);
        if (!Owned(Names[1], playerData)) return "安装不完整：播放器已修改或损坏";
        return Hash(patchData) == Hash(Payload[Names[0]]) && Hash(playerData) == Hash(Payload[Names[1]])
            ? "BGM MOD 已安装（当前版本）" : "BGM MOD 已安装（旧版，可更新）";
    }
    static string DefaultDirectory(string basePath) {
        if (File.Exists(Path.Combine(basePath, "MaitetsuLastRun.exe"))) return basePath;
        DirectoryInfo parent = Directory.GetParent(basePath);
        return parent != null && File.Exists(Path.Combine(parent.FullName, "MaitetsuLastRun.exe")) ? parent.FullName : basePath;
    }
    void RefreshStatus() {
        try { status.Text = "状态：" + Inspect(directory.Text); }
        catch { status.Text = "状态：无法读取文件，请检查权限或文件占用"; }
    }
    void Run(bool install) {
        try {
            Change(directory.Text, install, false);
            RefreshStatus();
            MessageBox.Show(install ? "安装／更新完成。重新启动游戏，进入 BGM 鉴赏即可。" : "卸载完成。重新启动游戏后恢复原有 BGM 鉴赏。", "BGM MOD", MessageBoxButtons.OK, MessageBoxIcon.Information);
        } catch (Exception e) { MessageBox.Show(e.Message, "操作未完成", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
    ModManager() {
        Text = "BGM MOD 管理器 1.2.3";
        ClientSize = new Size(590, 255);
        Font = new Font("Microsoft YaHei UI", 10);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Label title = new Label { Text = "まいてつ Last Run!! · BGM 鉴赏扩展", Location = new Point(20, 18), AutoSize = true };
        Label info = new Label { Text = "切歌、暂停、单曲循环、顺序循环、随机播放等。\n安装或卸载前请先关闭游戏。", Location = new Point(20, 50), Size = new Size(550, 50) };
        directory.SetBounds(20, 110, 455, 28);
        directory.Text = DefaultDirectory(AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        Button browse = new Button { Text = "选择目录", Location = new Point(485, 109), Size = new Size(85, 30) };
        browse.Click += delegate {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog()) {
                dialog.Description = "选择包含 MaitetsuLastRun.exe 的游戏目录";
                dialog.SelectedPath = directory.Text;
                if (dialog.ShowDialog() == DialogResult.OK) directory.Text = dialog.SelectedPath;
            }
        };
        directory.TextChanged += delegate { try { RefreshStatus(); } catch { status.Text = "状态：请选择有效游戏目录"; } };
        status.SetBounds(20, 150, 550, 28);
        Button add = new Button { Text = "安装／更新", Location = new Point(20, 193), Size = new Size(160, 40) };
        Button remove = new Button { Text = "卸载 MOD", Location = new Point(195, 193), Size = new Size(160, 40) };
        add.Click += delegate { Run(true); };
        remove.Click += delegate { Run(false); };
        Controls.AddRange(new Control[] { title, info, directory, browse, status, add, remove });
        Activated += delegate { RefreshStatus(); };
        RefreshStatus();
    }
    static void Assert(bool value, string message, List<string> report) {
        if (!value) throw new Exception(message);
        report.Add("PASS " + message);
    }
    static void SelfTest(string basePath) {
        string root = Path.Combine(Path.GetFullPath(basePath), "installer-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "MaitetsuLastRun.exe"), "test placeholder");
        List<string> report = new List<string>();
        Assert(Inspect(root) == "BGM MOD 未安装", "clean game detected as not installed", report);
        string backup = Path.Combine(root, "backup"); Directory.CreateDirectory(backup);
        File.WriteAllBytes(Path.Combine(backup, "patch.xp3"), Payload[Names[0]]);
        Assert(Inspect(backup) == "请选择有效游戏目录", "backup folder is not an installed game", report);
        Assert(DefaultDirectory(backup) == root, "manager in subfolder selects game parent", report);
        Assert(DefaultDirectory(root) == root, "manager in game directory keeps target", report);
        Assert(Inspect("") == "请选择有效游戏目录", "empty target rejected", report);
        string legacyPlayer = Path.Combine(basePath, "legacy-player.tjs");
        if (File.Exists(legacyPlayer)) {
            Directory.CreateDirectory(Path.Combine(root, "bgm_music_mod"));
            File.WriteAllBytes(Target(root, Names[0]), Payload[Names[0]]);
            File.Copy(legacyPlayer, Target(root, Names[1]));
            Assert(Inspect(root) == "BGM MOD 已安装（旧版，可更新）", "legacy install detected", report);
            Change(root, true, true);
            Assert(Hash(File.ReadAllBytes(Target(root, Names[1]))) == Hash(Payload[Names[1]]), "legacy version upgrades", report);
        }
        Change(root, true, true);
        foreach (string name in Names) Assert(Hash(File.ReadAllBytes(Target(root, name))) == Hash(Payload[name]), "installed " + name, report);
        Assert(Inspect(root) == "BGM MOD 已安装（当前版本）", "complete current install detected", report);
        File.Delete(Target(root, Names[2]));
        Assert(Inspect(root) == "BGM MOD 已安装（当前版本）", "optional readme does not affect runtime detection", report);
        File.Delete(Target(root, Names[1]));
        Assert(Inspect(root).StartsWith("安装不完整：缺少播放器"), "patch without player is incomplete", report);
        Directory.CreateDirectory(Target(root, Names[1]));
        Assert(Inspect(root).StartsWith("文件冲突"), "directory occupying runtime file detected", report);
        Directory.Delete(Target(root, Names[1]));
        Change(root, true, true);
        Assert(File.Exists(Target(root, Names[0])), "repeat install", report);
        string extra = Path.Combine(root, "bgm_music_mod", "user-note.txt");
        File.WriteAllText(extra, "keep");
        Change(root, false, true);
        Assert(!File.Exists(Target(root, Names[0])) && !File.Exists(Target(root, Names[1])), "uninstall removes mod", report);
        Assert(Inspect(root) == "BGM MOD 未安装", "uninstalled state detected", report);
        File.WriteAllBytes(Target(root, Names[1]), Payload[Names[1]]);
        Assert(Inspect(root).StartsWith("未安装／已停用"), "player without active patch is disabled", report);
        File.Delete(Target(root, Names[1]));
        Assert(File.ReadAllText(extra) == "keep", "uninstall preserves other files", report);
        Change(root, false, true);
        Assert(File.Exists(extra), "repeat uninstall", report);
        File.WriteAllText(Target(root, Names[0]), "foreign patch");
        Assert(Inspect(root).StartsWith("文件冲突"), "foreign or damaged patch detected", report);
        bool rejected = false;
        try { Change(root, true, true); } catch { rejected = true; }
        Assert(rejected && File.ReadAllText(Target(root, Names[0])) == "foreign patch", "foreign patch preserved", report);
        File.Delete(Target(root, Names[0]));
        Change(root, true, true);
        File.WriteAllText(Target(root, Names[1]), "user edit");
        Assert(Inspect(root).StartsWith("安装不完整：播放器已修改"), "modified player is not reported installed", report);
        rejected = false;
        try { Change(root, false, true); } catch { rejected = true; }
        Assert(rejected && File.Exists(Target(root, Names[0])) && File.ReadAllText(Target(root, Names[1])) == "user edit", "modified file blocks uninstall without partial removal", report);
        File.Delete(Target(root, Names[1])); File.Delete(Target(root, Names[2]));
        using (FileStream locked = new FileStream(Target(root, Names[0]), FileMode.Open, FileAccess.Read, FileShare.Read)) {
            rejected = false;
            try { Change(root, true, true); } catch { rejected = true; }
            Assert(rejected && !File.Exists(Target(root, Names[1])) && !File.Exists(Target(root, Names[2])), "failed install rolls back earlier files", report);
        }
        Assert(Hash(File.ReadAllBytes(Target(root, Names[0]))) == Hash(Payload[Names[0]]), "rollback preserves original patch", report);
        Assert(File.ReadAllText(Path.Combine(root, "MaitetsuLastRun.exe")) == "test placeholder", "game executable untouched", report);
        report.Add("SUCCESS");
        File.WriteAllLines(Path.Combine(basePath, "installer-test-result.txt"), report);
        // Leave the small test directory for inspection, no recursive deletion.
    }
    [STAThread]
    static int Main(string[] args) {
        foreach (string name in Names) Payload.Add(name, Resource(name));
        foreach (string line in System.Text.Encoding.UTF8.GetString(Resource("legacy")).Trim('\ufeff').Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)) {
            string[] pair = line.Split('|');
            if (!Legacy.ContainsKey(pair[0])) Legacy.Add(pair[0], new HashSet<string>());
            Legacy[pair[0]].Add(pair[1]);
        }
        if (args.Length == 2 && args[0] == "--self-test") {
            try { SelfTest(args[1]); return 0; }
            catch (Exception e) { File.WriteAllText(Path.Combine(args[1], "installer-test-result.txt"), "FAIL " + e); return 1; }
        }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        if (args.Length == 2 && args[0] == "--preview") {
            using (ModManager form = new ModManager()) {
                form.Show();
                Application.DoEvents();
                using (Bitmap bitmap = new Bitmap(form.Width, form.Height)) {
                    form.DrawToBitmap(bitmap, new Rectangle(0, 0, form.Width, form.Height));
                    bitmap.Save(args[1], System.Drawing.Imaging.ImageFormat.Png);
                }
                form.Close();
            }
            return 0;
        }
        Application.Run(new ModManager());
        return 0;
    }
}
