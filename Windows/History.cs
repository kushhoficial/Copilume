using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Copilume;

public sealed record StoredImage(byte[] Png, int Width, int Height)
{
    [JsonIgnore] public bool IsValid => Width > 0 && Height > 0 && Width <= 8192 && Height <= 8192 &&
        (long)Width * Height <= 16_000_000 && Png is { Length: > 0 and <= 8_000_000 };
}

public sealed record Clip
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Text { get; init; } = "";
    public DateTimeOffset Date { get; init; } = DateTimeOffset.Now;
    public StoredImage? Image { get; init; }
    public bool Pinned { get; set; }
    [JsonIgnore] public bool IsImage => Image != null;
    [JsonIgnore] public bool IsLink => !IsImage && (Text.Trim().StartsWith("https://", StringComparison.OrdinalIgnoreCase) || Text.Trim().StartsWith("http://", StringComparison.OrdinalIgnoreCase));
    [JsonIgnore] public string DisplayText => Image is { } i ? $"Imagem • {i.Width} × {i.Height}" : Text;
    [JsonIgnore] public string Details => $"{(Pinned ? "Fixado • " : "")}{Date:dd/MM HH:mm} • {(IsImage ? "Imagem" : IsLink ? "Link" : "Texto")}";
    [JsonIgnore] public int ByteCount => Encoding.UTF8.GetByteCount(Text) + (Image?.Png.Length ?? 0);
    [JsonIgnore] public string SingleLine => Regex.Replace(Text, @"\s+", " ").Trim();
    [JsonIgnore] public bool IsValid => Text != null && (Image is { } i ? i.IsValid && Text.Length == 0 : Text.Length > 0 && ByteCount <= 2_000_000);
}

public enum Scope { All, Pinned, Links, Text, Images }

public sealed class History
{
    private readonly List<Clip> items = new();
    public IReadOnlyList<Clip> Items => items;
    private int limit = 200;
    public int Limit { get => limit; set { limit = Math.Clamp(value, 1, 5000); Trim(); } }
    public bool Paused { get; set; }
    public bool Capture(Clip clip)
    {
        if (Paused || !clip.IsValid) return false;
        var first = items.FirstOrDefault();
        if (first != null && (clip.Image is { } image && first.Image is { } previous
            ? image.Width == previous.Width && image.Height == previous.Height && image.Png.AsSpan().SequenceEqual(previous.Png)
            : !clip.IsImage && !first.IsImage && clip.Text == first.Text)) return false;
        items.Insert(0, clip);
        Trim();
        return items.Any(x => x.Id == clip.Id);
    }
    private void Trim()
    {
        long bytes = items.Sum(x => (long)x.ByteCount);
        while (items.Count > limit || bytes > 24_000_000)
        {
            int index = items.FindLastIndex(x => !x.Pinned);
            if (index < 0) index = items.Count - 1;
            bytes -= items[index].ByteCount;
            items.RemoveAt(index);
        }
    }
    public void Remove(Guid id) => items.RemoveAll(x => x.Id == id);
    public void Clear(bool includePinned = false) => items.RemoveAll(x => includePinned || !x.Pinned);
    public IEnumerable<Clip> Search(string query, Scope scope) => items.Where(x =>
        CultureInfo.InvariantCulture.CompareInfo.IndexOf(x.DisplayText, query, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0 &&
        (scope == Scope.All || scope == Scope.Pinned && x.Pinned || scope == Scope.Links && x.IsLink ||
         scope == Scope.Text && !x.IsImage && !x.IsLink || scope == Scope.Images && x.IsImage));
    public void Save(string path)
    {
        var data = JsonSerializer.SerializeToUtf8Bytes(items);
        if (data.Length > 50_000_000) throw new IOException("Histórico excede o limite de armazenamento.");
        AtomicFile.Write(path, DataProtection.Protect(data));
    }
    public void Load(string path)
    {
        if (!File.Exists(path)) return;
        using var stream = File.OpenRead(path);
        if (stream.Length > 51_000_000) throw new IOException("Arquivo de histórico muito grande.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        var data = DataProtection.Unprotect(buffer.ToArray());
        if (data.Length > 50_000_000) throw new IOException("Histórico muito grande.");
        var decoded = JsonSerializer.Deserialize<List<Clip?>>(data) ?? throw new JsonException();
        items.Clear();
        items.AddRange(decoded.Where(x => x != null && x.IsValid).Select(x => x!));
        Trim();
    }
}

internal static class AtomicFile
{
    internal static void Write(string path, byte[] data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + ".tmp";
        try { File.WriteAllBytes(temporary, data); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

public sealed class Settings
{
    public int Limit { get; set; } = 200;
    public bool Persistent { get; set; }
    public bool AutoPaste { get; set; }
    public string Appearance { get; set; } = "Sistema";
    public uint Modifiers { get; set; } = 1;
    public uint Key { get; set; } = 0x56;
    public static string DataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Copilume");
    public static string HistoryPath => Path.Combine(DataDirectory, "history.dat");
    private static string PathName => Path.Combine(DataDirectory, "settings.json");
    public static Settings Load() => File.Exists(PathName) ? JsonSerializer.Deserialize<Settings>(File.ReadAllBytes(PathName)) ?? new() : new();
    public void Save() => AtomicFile.Write(PathName, JsonSerializer.SerializeToUtf8Bytes(this));
}
