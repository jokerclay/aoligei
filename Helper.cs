using System.IO;

namespace aoligei;
public static class Helper
{
    /// <summary>
    /// 获取应用程序可执行文件所在目录（发布后放资源于此目录下）。
    /// </summary>
    private static string AppRoot => AppContext.BaseDirectory;
    public static string GetResourceDir(){ return Path.Combine(AppRoot, "resource"); }
    public static string GetMusicDir(){ return Path.Combine(GetResourceDir(), "music"); }
    public static string? GetMusicStartDir(){return Path.Combine(GetMusicDir(), "aoligei");}

    public static string GetMusicBackgroundDir(){ return Path.Combine(GetMusicDir(), "background"); }
    public static string? GetMusicEndDir(){ return Path.Combine(GetMusicDir(), "good"); }
}