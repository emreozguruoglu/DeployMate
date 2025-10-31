using DeployMate.Core;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DeployMate.Storage;

public sealed class JsonConfigurationStore : IConfigurationStore
{
    private readonly string _appDir;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public JsonConfigurationStore(string appName = "DeployMate")
    {
        _appDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), appName);
        Directory.CreateDirectory(_appDir);
    }

    public async Task SaveTargetsAsync(TargetConfig[] targets, CancellationToken ct)
    {
        // clone and encrypt sensitive fields
        var shaped = new TargetConfig[targets.Length];
        for (int i = 0; i < targets.Length; i++)
        {
            var t = targets[i];
            shaped[i] = new TargetConfig
            {
                Id = t.Id,
                Name = t.Name,
                Environment = t.Environment,
                LocalDestination = t.LocalDestination,
                Protocol = t.Protocol,
                Host = DpapiUtil.ProtectString(t.Host),
                Port = t.Port, // store separately as encryptedPort
                RemotePath = t.RemotePath,
                Credential = t.Credential,
                Transfer = t.Transfer,
                PreDeploy = t.PreDeploy,
                PostDeploy = t.PostDeploy,
                DefaultDryRun = t.DefaultDryRun,
                Disabled = t.Disabled
            };
        }
        // write combined json with encryptedPort
        string path = Path.Combine(_appDir, "targets.json");
        await using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        await JsonSerializer.SerializeAsync(fs, shaped, _jsonOptions, ct);
        // write a sidecar with encrypted ports
        var ports = targets.ToDictionary(t => t.Id.Value.ToString(), t => DpapiUtil.ProtectString(t.Port.ToString()));
        await File.WriteAllTextAsync(Path.Combine(_appDir, "targets.ports.json"), JsonSerializer.Serialize(ports, _jsonOptions), ct);
    }

    public async Task<TargetConfig[]> LoadTargetsAsync(CancellationToken ct)
    {
        string path = Path.Combine(_appDir, "targets.json");
        if (!File.Exists(path)) return Array.Empty<TargetConfig>();
        await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var targets = await JsonSerializer.DeserializeAsync<TargetConfig[]>(fs, _jsonOptions, ct) ?? Array.Empty<TargetConfig>();
        // load sidecar encrypted ports if present
        var portMap = new System.Collections.Generic.Dictionary<string, string>();
        string portsPath = Path.Combine(_appDir, "targets.ports.json");
        if (File.Exists(portsPath))
        {
            var json = await File.ReadAllTextAsync(portsPath, ct);
            portMap = JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, string>>(json, _jsonOptions) ?? portMap;
        }
        for (int i = 0; i < targets.Length; i++)
        {
            var t = targets[i];
            // decrypt host
            t.Host = DpapiUtil.UnprotectString(t.Host);
            // decrypt port
            if (portMap.TryGetValue(t.Id.Value.ToString(), out var encPort))
            {
                var s = DpapiUtil.UnprotectString(encPort);
                if (int.TryParse(s, out var p)) t.Port = p;
            }
        }
        return targets;
    }

    public async Task SaveAppSettingsAsync(AppSettings settings, CancellationToken ct)
    {
        string path = Path.Combine(_appDir, "appsettings.json");
        await using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        await JsonSerializer.SerializeAsync(fs, settings, _jsonOptions, ct);
    }

    public async Task<AppSettings> LoadAppSettingsAsync(CancellationToken ct)
    {
        string path = Path.Combine(_appDir, "appsettings.json");
        if (!File.Exists(path)) return new AppSettings();
        await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var settings = await JsonSerializer.DeserializeAsync<AppSettings>(fs, _jsonOptions, ct);
        return settings ?? new AppSettings();
    }

    // Export/Import full config (targets + settings) to a single file with DPAPI-encrypted sensitive fields
    private sealed class ConfigPack
    {
        public TargetConfig[] Targets { get; set; } = Array.Empty<TargetConfig>();
        public AppSettings Settings { get; set; } = new AppSettings();
        public Dictionary<string, string> Ports { get; set; } = new(); // TargetId -> enc port
    }

    public async Task ExportAsync(string filePath, CancellationToken ct = default)
    {
        var targets = await LoadTargetsAsync(ct);
        var settings = await LoadAppSettingsAsync(ct);
        var shaped = new TargetConfig[targets.Length];
        var ports = new Dictionary<string, string>();
        for (int i = 0; i < targets.Length; i++)
        {
            var t = targets[i];
            shaped[i] = new TargetConfig
            {
                Id = t.Id,
                Name = t.Name,
                Environment = t.Environment,
                LocalDestination = t.LocalDestination,
                Protocol = t.Protocol,
                Host = DpapiUtil.ProtectString(t.Host),
                Port = t.Port,
                RemotePath = t.RemotePath,
                Credential = t.Credential,
                Transfer = t.Transfer,
                PreDeploy = t.PreDeploy,
                PostDeploy = t.PostDeploy,
                DefaultDryRun = t.DefaultDryRun,
                Disabled = t.Disabled
            };
            ports[t.Id.Value.ToString()] = DpapiUtil.ProtectString(t.Port.ToString());
        }
        var pack = new ConfigPack { Targets = shaped, Settings = settings, Ports = ports };
        await using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        await JsonSerializer.SerializeAsync(fs, pack, _jsonOptions, ct);
    }

    public async Task ImportAsync(string filePath, CancellationToken ct = default)
    {
        await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var pack = await JsonSerializer.DeserializeAsync<ConfigPack>(fs, _jsonOptions, ct) ?? new ConfigPack();
        // decrypt host/port
        foreach (var t in pack.Targets)
        {
            t.Host = DpapiUtil.UnprotectString(t.Host);
            if (pack.Ports != null && pack.Ports.TryGetValue(t.Id.Value.ToString(), out var enc))
            {
                var s = DpapiUtil.UnprotectString(enc);
                if (int.TryParse(s, out var p)) t.Port = p;
            }
        }
        await SaveTargetsAsync(pack.Targets, ct);
        await SaveAppSettingsAsync(pack.Settings ?? new AppSettings(), ct);
    }
}

public sealed class DpapiCredentialVault : ICredentialVault
{
    private readonly string _vaultDir;

    public DpapiCredentialVault(string appName = "DeployMate")
    {
        _vaultDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), appName, "vault");
        Directory.CreateDirectory(_vaultDir);
    }

    public async Task StoreAsync(string key, string username, string? password, string? privateKeyPath, string? passphrase, CancellationToken ct)
    {
        var payload = string.Join("\n",
            username ?? string.Empty,
            password ?? string.Empty,
            privateKeyPath ?? string.Empty,
            passphrase ?? string.Empty);
        byte[] data = Encoding.UTF8.GetBytes(payload);
        byte[] enc = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
        string path = Path.Combine(_vaultDir, Sanitize(key) + ".bin");
        await File.WriteAllBytesAsync(path, enc, ct);
    }

    public async Task<(string username, string? password, string? privateKeyPath, string? passphrase)> GetAsync(string key, CancellationToken ct)
    {
        string path = Path.Combine(_vaultDir, Sanitize(key) + ".bin");
        if (!File.Exists(path)) throw new FileNotFoundException("Credential not found", path);
        byte[] enc = await File.ReadAllBytesAsync(path, ct);
        byte[] data = ProtectedData.Unprotect(enc, null, DataProtectionScope.CurrentUser);
        var lines = Encoding.UTF8.GetString(data).Split('\n');
        string username = lines.ElementAtOrDefault(0) ?? string.Empty;
        string? password = string.IsNullOrEmpty(lines.ElementAtOrDefault(1)) ? null : lines[1];
        string? privateKeyPath = string.IsNullOrEmpty(lines.ElementAtOrDefault(2)) ? null : lines[2];
        string? passphrase = string.IsNullOrEmpty(lines.ElementAtOrDefault(3)) ? null : lines[3];
        return (username, password, privateKeyPath, passphrase);
    }

    private static string Sanitize(string key)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) key = key.Replace(c, '_');
        return key;
    }

}

internal static class DpapiUtil
{
    public static string ProtectString(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        byte[] data = Encoding.UTF8.GetBytes(value);
        byte[] enc = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(enc);
    }

    public static string UnprotectString(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        try
        {
            byte[] enc = Convert.FromBase64String(value);
            byte[] data = ProtectedData.Unprotect(enc, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(data);
        }
        catch
        {
            return value; // already plain
        }
    }
}


