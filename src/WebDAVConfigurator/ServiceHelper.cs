using System.Diagnostics;
using System.IO;
using System.ServiceProcess;

namespace WebDAVConfigurator;

/// <summary>
/// 纯 C# 服务管理，不依赖 nssm.exe 和 .bat
/// 使用 Windows 内置 sc.exe 管理服务
/// </summary>
public class ServiceHelper
{
    private const string ServiceName = "WebDAVService";
    private const string DisplayName = "WebDAV Service";

    public void InstallService(string baseDir)
    {
        var exePath = Path.Combine(baseDir, "WebDAVService.exe");
        if (!File.Exists(exePath))
            throw new FileNotFoundException($"服务程序未找到: {exePath}");

        StopService();
        DeleteService();
        Thread.Sleep(2000);

        try { Process.Start("taskkill", "/f /im WebDAVService.exe")?.WaitForExit(); } catch { }
        Thread.Sleep(1000);

        RunSc($"create {ServiceName} binPath= \"{exePath}\" start= auto DisplayName= \"{DisplayName}\"");
        RunSc($"description {ServiceName} \"WebDAV 文件共享服务\"");
        RunSc($"failure {ServiceName} reset= 86400 actions= restart/5000/restart/5000/restart/5000");

        StartService();
    }

    public void UninstallService()
    {
        // 清理 HTTPS 证书绑定
        try
        {
            var cfg = WebDAVConfigData.Load();
            if (cfg.EnableHttps)
            {
                var psi = new ProcessStartInfo("netsh", $"http delete sslcert ipport=0.0.0.0:{cfg.HttpsPort}")
                {
                    RedirectStandardOutput = true, RedirectStandardError = true,
                    UseShellExecute = false, CreateNoWindow = true
                };
                using var p = Process.Start(psi)!;
                p.WaitForExit(5000);
            }
        }
        catch { }

        StopService();
        Thread.Sleep(3000);
        DeleteService();
    }

    public void RestartService()
    {
        try { Process.Start("taskkill", "/f /im WebDAVService.exe")?.WaitForExit(); } catch { }
        Thread.Sleep(1000);
        StopService();
        Thread.Sleep(3000);
        StartService();
    }

    public bool IsServiceRunning()
    {
        try
        {
            using var sc = new ServiceController(ServiceName);
            sc.Refresh();
            return sc.Status == ServiceControllerStatus.Running;
        }
        catch { return false; }
    }

    public bool WaitForRunning(int timeoutMs = 15000)
    {
        try
        {
            using var sc = new ServiceController(ServiceName);
            sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromMilliseconds(timeoutMs));
            return sc.Status == ServiceControllerStatus.Running;
        }
        catch { return false; }
    }

    private void StartService()
    {
        try
        {
            using var sc = new ServiceController(ServiceName);
            if (sc.Status != ServiceControllerStatus.Running)
            {
                sc.Start();
                sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(15));
            }
        }
        catch (Exception ex) { throw new Exception($"启动服务失败: {ex.Message}"); }
    }

    private void StopService()
    {
        try
        {
            using var sc = new ServiceController(ServiceName);
            if (sc.Status != ServiceControllerStatus.Stopped)
            {
                sc.Stop();
                sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(15));
            }
        }
        catch (InvalidOperationException) { }
        catch (Exception ex) { throw new Exception($"停止服务失败: {ex.Message}"); }
    }

    private void DeleteService()
    {
        try { RunSc($"delete {ServiceName}"); }
        catch { }
    }

    private static void RunSc(string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "sc.exe",
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        using var process = Process.Start(psi)!;
        process.WaitForExit(10000);
        if (process.ExitCode != 0)
        {
            var error = process.StandardError.ReadToEnd();
            if (!string.IsNullOrEmpty(error) &&
                !error.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
                throw new Exception($"sc.exe 失败 (code={process.ExitCode}): {error}");
        }
    }
}
