using System.Diagnostics;

namespace WebDAVConfigurator;

public static class FirewallHelper
{
    private const string RuleName = "WebDAV Service";

    public static (bool success, string message) EnsureFirewallRule(int port)
    {
        try
        {
            RunNetsh($"advfirewall firewall delete rule name=\"{RuleName}\"");
            var result = RunNetsh(
                $"advfirewall firewall add rule name=\"{RuleName}\" dir=in action=allow protocol=TCP localport={port} profile=any");
            return (result.exitCode == 0,
                result.exitCode == 0 ? $"防火墙已放行端口 {port}" : $"防火墙规则添加失败: {result.stderr}");
        }
        catch (Exception ex) { return (false, $"防火墙操作异常: {ex.Message}"); }
    }

    private static (int exitCode, string stdout, string stderr) RunNetsh(string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "netsh.exe",
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit(10000);
        return (process.ExitCode, stdout, stderr);
    }
}
