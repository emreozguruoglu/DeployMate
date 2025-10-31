using DeployMate.Core;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DeployMate.Hooks;

public sealed class HookRunner : IHookRunner
{
    private static readonly HttpClient Http = new HttpClient();

    public async Task RunAsync(HookSet hooks, TargetConfig target, CancellationToken ct)
    {
        foreach (var hook in hooks.Hooks)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(hook.Timeout);
                switch (hook.Type)
                {
                    case HookType.Http:
                        await RunHttpAsync(hook, target, cts.Token);
                        break;
                    case HookType.PowerShell:
                    case HookType.Command:
                        await RunProcessAsync(hook, cts.Token);
                        break;
                    case HookType.StopSessions:
                        // Placeholder: strategy selection via parameters
                        await Task.CompletedTask;
                        break;
                }
            }
            catch when (hook.ContinueOnError)
            {
                // swallow per config
            }
        }
    }

    private static async Task RunHttpAsync(HookConfig hook, TargetConfig target, CancellationToken ct)
    {
        string method = hook.Parameters.TryGetValue("Method", out var m) ? m : "POST";
        string url = hook.Parameters["Url"];
        var req = new HttpRequestMessage(new HttpMethod(method), url);
        if (hook.Parameters.TryGetValue("Body", out var body))
        {
            body = body.Replace("{TargetName}", target.Name);
            req.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }
        foreach (var kv in hook.Parameters)
        {
            if (kv.Key.StartsWith("Header:", StringComparison.OrdinalIgnoreCase))
            {
                req.Headers.TryAddWithoutValidation(kv.Key.Substring(7), kv.Value);
            }
        }
        using var resp = await Http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
    }

    private static async Task RunProcessAsync(HookConfig hook, CancellationToken ct)
    {
        string file = hook.Parameters["File"];
        string args = hook.Parameters.TryGetValue("Args", out var a) ? a : string.Empty;
        var psi = new ProcessStartInfo
        {
            FileName = file,
            Arguments = args,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        proc.Start();
        await Task.WhenAny(proc.WaitForExitAsync(ct), Task.Delay(-1, ct));
        if (!proc.HasExited)
        {
            try { proc.Kill(true); } catch { }
            throw new OperationCanceledException("Hook process timed out or canceled", ct);
        }
        if (proc.ExitCode != 0)
        {
            string err = await proc.StandardError.ReadToEndAsync(ct);
            throw new InvalidOperationException($"Hook failed: {err}");
        }
    }
}


