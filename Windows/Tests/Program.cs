using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Copilume;

internal static class Program
{
    private static int checks;
    private static void Check(bool value, string name)
    { if (!value) throw new Exception("FAIL: " + name); checks++; Console.WriteLine("PASS: " + name); }
    private static void Reject(Action action, string name)
    { bool failed = false; try { action(); } catch { failed = true; } Check(failed, name); }
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length >= 2 && args[0] == "--render") { Render(args[1], args.Contains("--dark")); return 0; }
            Model(); Policy(); Images(); Persistence(); CaptureTests().GetAwaiter().GetResult();
            Console.WriteLine($"{checks} checks passed; synthetic data only. Clipboard, hotkey and startup unchanged.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    private static Clip Text(string value, bool pinned = false) => new() { Text = value, Pinned = pinned };
    private static void Model()
    {
        var h = new History();
        Check(h.Limit == 200 && !h.Paused, "defaults");
        Check(!h.Capture(Text("")), "empty ignored");
        Check(!h.Capture(Text(new string('é', 1_000_001))), "UTF8 limit");
        Check(h.Capture(Text("Ação")), "text capture");
        Check(!h.Capture(Text("Ação")), "consecutive deduplication");
        Check(h.Search("ACAO", Scope.All).Count() == 1, "accent/case insensitive search");
        h.Paused = true; Check(!h.Capture(Text("paused")), "pause"); h.Paused = false;
        h.Capture(Text("  HTTPS://example.com  "));
        Check(h.Search("", Scope.Links).Count() == 1, "link classification");
        Check(h.Search("", Scope.Text).Count() == 1, "text scope excludes links");
        Check(Text(" a\t b\r\n c ").SingleLine == "a b c", "single line");
        h = new History { Limit = 2 }; var pin = Text("pin", true); h.Capture(pin); h.Capture(Text("old")); h.Capture(Text("new"));
        Check(h.Items.Count == 2 && h.Items.Contains(pin) && h.Items[0].Text == "new", "pins protected during eviction");
        Check(h.Search("pin", Scope.Pinned).Single().Id == pin.Id, "search combines pin scope");
        h.Clear(); Check(h.Items.Single().Id == pin.Id, "clear preserves pins");
        h.Capture(Text("p2", true)); Check(!h.Capture(Text("overflow")), "all pinned capacity rejects incoming clip");
        h.Limit = 1; Check(h.Items.Count == 1 && h.Items[0].Text == "p2", "hard count cap applies to pins");
        h.Limit = 0; Check(h.Limit == 1, "minimum limit"); h.Limit = 6000; Check(h.Limit == 5000, "maximum limit");
        h.Remove(h.Items[0].Id); Check(h.Items.Count == 0, "delete");
        h.Capture(pin); h.Clear(true); Check(h.Items.Count == 0, "clear including pins");
        var image = ImageCodec.Demo(); h.Capture(new Clip { Image = image });
        Check(!h.Capture(new Clip { Image = image with { Png = image.Png.ToArray() } }), "image deduplication compares bytes");
        Check(h.Search("320", Scope.Images).Count() == 1 && !h.Items[0].IsLink, "image dimensions searchable");
        Check(!h.Capture(new Clip { Image = image, Text = "invalid mixed item" }), "mixed stored payload rejected");
        Check(!h.Capture(new Clip { Image = new StoredImage(new byte[8_000_001], 1, 1) }), "PNG size limit");
        Check(!h.Capture(new Clip { Image = new StoredImage(new byte[1], 8193, 1) }), "image side limit");
        Check(!h.Capture(new Clip { Image = new StoredImage(new byte[1], 4001, 4000) }), "image pixel limit");
        h = new History();
        for (int i = 0; i < 13; i++) h.Capture(Text(i.ToString("D2") + new string('x', 1_999_998)));
        Check(h.Items.Sum(x => (long)x.ByteCount) <= 24_000_000 && h.Items.Count == 12, "aggregate byte budget");
    }
    private static void Policy()
    {
        Check(!new Settings().Persistent && !new Settings().AutoPaste, "privacy options off by default");
        var formats = new Dictionary<string, byte[]>();
        bool Excluded() => ClipboardPolicy.ShouldExclude(formats.ContainsKey, name => formats.GetValueOrDefault(name));
        Check(!Excluded(), "ordinary clipboard accepted");
        foreach (string name in ClipboardPolicy.ExcludedFormats)
        { formats[name] = new byte[] { 1 }; Check(Excluded(), "exclude " + name); formats.Clear(); }
        foreach (string name in new[] { "CanIncludeInClipboardHistory", "CanUploadToCloudClipboard" })
        {
            formats[name] = new byte[4]; Check(Excluded(), name + " disabled");
            formats[name] = new byte[1]; Check(Excluded(), name + " malformed fails closed");
            formats[name] = BitConverter.GetBytes(1u); Check(!Excluded(), name + " allowed"); formats.Clear();
        }
    }
    private static void Images()
    {
        var demo = ImageCodec.Demo(); var normalized = ImageCodec.Normalize(demo.Png, false);
        Check(normalized.Width == 320 && normalized.Height == 180 && normalized.IsValid, "PNG normalized");
        var thumbnail = ImageCodec.Thumbnail(normalized);
        Check(thumbnail.PixelWidth == 256 && thumbnail.IsFrozen, "bounded frozen thumbnail");
        Reject(() => ImageCodec.Normalize(new byte[20], false), "invalid raster rejected");
        Reject(() => ImageCodec.DibToBmp(new byte[39]), "short DIB rejected");
        byte[] dib = new byte[44];
        void Int(int offset, int value) => BitConverter.GetBytes(value).CopyTo(dib, offset);
        Int(0, 40); Int(4, 1); Int(8, 1); dib[12] = 1; dib[14] = 24; dib[40] = 144;
        var result = ImageCodec.Normalize(dib, true);
        Check(result.Width == 1 && result.Height == 1, "DIB converted to PNG");
        Int(8, -1); Check(ImageCodec.Normalize(dib, true).Height == 1, "top-down DIB");
        Int(4, 8193); Reject(() => ImageCodec.DibToBmp(dib), "DIB dimensions checked before decoding");
        Int(4, 1); Int(8, int.MinValue); Reject(() => ImageCodec.DibToBmp(dib), "DIB integer overflow rejected");
        Int(8, 2); Reject(() => ImageCodec.DibToBmp(dib), "truncated DIB rejected");
        Int(8, 1); Int(16, 5); Reject(() => ImageCodec.DibToBmp(dib), "unsupported DIB compression rejected");
        var bitmap = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 10, 20, 30, 90 }, 4);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using var output = new MemoryStream(); png.Save(output);
        var transparent = ImageCodec.Decode(ImageCodec.Normalize(output.ToArray(), false).Png);
        var pixels = new byte[4]; new FormatConvertedBitmap(transparent, PixelFormats.Bgra32, null, 0).CopyPixels(pixels, 4, 0);
        Check(pixels[3] == 90, "PNG alpha retained");
    }
    private static void Persistence()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Copilume-tests-" + Guid.NewGuid());
        var path = Path.Combine(directory, "history.dat");
        try
        {
            var h = new History(); h.Capture(Text("synthetic persistence", true)); h.Capture(new Clip { Image = ImageCodec.Demo() }); h.Save(path);
            Check(!Encoding.UTF8.GetString(File.ReadAllBytes(path)).Contains("synthetic persistence"), "history encrypted on disk");
            var restored = new History(); restored.Load(path);
            Check(restored.Items.Count == 2 && restored.Items[1].Pinned && restored.Items[1].Text == "synthetic persistence", "mixed history and pins round trip");
            Check(restored.Items[0].Image!.Png.SequenceEqual(h.Items[0].Image!.Png), "image bytes round trip");
            File.WriteAllBytes(path, new byte[40]); Reject(() => restored.Load(path), "corrupt DPAPI rejected");
            Check(restored.Items.Count == 2, "failed load preserves in-memory history");
            File.WriteAllBytes(path, DataProtection.Protect(Encoding.UTF8.GetBytes("[null, {\"Text\":\"valid\"}, {\"Text\":\"\"}]")));
            restored.Load(path); Check(restored.Items.Count == 1 && restored.Items[0].Text == "valid", "invalid persisted entries filtered");
            h.Save(path); h.Capture(Text("next")); h.Save(path); restored.Load(path);
            Check(restored.Items.Count == 3 && !File.Exists(path + ".tmp"), "atomic replacement");
            using (var file = File.Create(path)) file.SetLength(51_000_001);
            Reject(() => restored.Load(path), "oversized persisted file rejected before reading");
            Reject(() => DataProtection.Unprotect(new byte[] { 1, 2, 3 }), "invalid encrypted data rejected");
        }
        finally { if (File.Exists(path)) File.Delete(path); if (Directory.Exists(directory)) Directory.Delete(directory); }
    }
    private static void Render(string path, bool dark)
    {
        var app = new App(); app.InitializeComponent();
        var window = new MainWindow(true, dark ? "Escuro" : "Claro");
        window.Initialize(); window.Show(); window.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        using (var file = File.Create(path)) png.Save(file);
        Check(window.FindName("Clips") is ListBox { Items.Count: 4 }, "demo renders four synthetic clips");
        ((TextBox)window.FindName("SearchBox")).Text = "github";
        Check(((ListBox)window.FindName("Clips")).Items.Count == 1, "UI search binding");
        ((TextBox)window.FindName("SearchBox")).Text = "";
        ((ComboBox)window.FindName("Filter")).SelectedIndex = 4;
        Check(((ListBox)window.FindName("Clips")).Items.Count == 1, "UI image filter binding");
        window.Quit();
    }

    private sealed class FakeClipboard : IClipboardAccess
    {
        public uint Sequence { get; set; } = 1;
        public bool Available { get; set; } = true;
        public int Opens, Closes;
        public readonly Dictionary<uint, byte[]> Data = new();
        private readonly Dictionary<string, uint> formats = new();
        public bool Open() { Opens++; return Available; }
        public void Close() { Closes++; }
        public uint Format(string name) { if (!formats.TryGetValue(name, out uint id)) formats[name] = id = (uint)(0xC000 + formats.Count); return id; }
        public bool Has(uint format) => Data.ContainsKey(format);
        public byte[]? Read(uint format, int maximum) => Data.TryGetValue(format, out var data) && data.Length <= maximum ? data : null;
        public void Text(string text) { Data.Clear(); Data[13] = Encoding.Unicode.GetBytes(text + '\0'); Sequence++; }
        public void Image() { Data.Clear(); Data[Format("PNG")] = new byte[] { 1 }; Sequence++; }
    }
    private static async Task Drain(ClipboardService service)
    {
        for (int i = 0; i < 200 && service.IsBusy; i++) await Task.Delay(10);
        if (service.IsBusy) throw new Exception("Capture failed to become idle.");
    }
    private static async Task CaptureTests()
    {
        var access = new FakeClipboard(); access.Text("before startup");
        var history = new History(); int changes = 0, decodes = 0, warnings = 0;
        var image = ImageCodec.Demo();
        TaskCompletionSource<StoredImage>? gate = null;
        Task<StoredImage> Decode(byte[] data, bool dib) { decodes++; return gate?.Task ?? Task.FromResult(image); }
        var service = new ClipboardService(IntPtr.Zero, history, () => changes++, _ => warnings++, access, Decode);
        service.Capture(); await Drain(service);
        Check(history.Items.Count == 0 && access.Opens == 0, "capture does not import existing clipboard");
        access.Text("new text"); service.Capture(); await Drain(service);
        Check(history.Items.Single().Text == "new text" && changes == 1, "capture service delivers Unicode text");
        service.Capture(); await Drain(service); Check(changes == 1, "unchanged sequence ignored");
        access.Image(); access.Data[13] = Encoding.Unicode.GetBytes("image companion\0"); service.Capture(); await Drain(service);
        Check(history.Items[0].IsImage && decodes == 1 && changes == 2, "capture prioritizes image over companion text");
        access.Image(); access.Data[access.Format("ExcludeClipboardContentFromMonitorProcessing")] = new byte[] { 1 };
        service.Capture(); await Drain(service); Check(decodes == 1 && changes == 2, "confidential image rejected before decode");
        access.Text("file path companion"); access.Data[15] = new byte[] { 1 }; service.Capture(); await Drain(service);
        Check(changes == 2, "file drops ignored without reading paths");
        history.Paused = true; service.Invalidate(); access.Text("during pause"); service.Capture(); await Drain(service);
        Check(changes == 2, "paused capture does not read clipboard");
        history.Paused = false; service.Invalidate(); service.Capture(); await Drain(service);
        Check(changes == 2, "resume does not import paused clipboard");
        gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        access.Image(); service.Capture();
        Check(service.IsBusy && decodes == 2, "image decode begins asynchronously");
        access.Text("latest text"); service.Capture();
        Check(decodes == 2, "only one decode in flight");
        gate.SetResult(image); await Drain(service);
        Check(history.Items[0].Text == "latest text" && changes == 3, "stale image discarded and newest clipboard captured");
        gate = new(TaskCreationOptions.RunContinuationsAsynchronously); access.Image(); service.Capture();
        history.Paused = true; service.Invalidate(); gate.SetResult(image); await Drain(service);
        Check(changes == 3, "pausing invalidates in-flight image");
        history.Paused = false; service.Invalidate();
        gate = new(TaskCreationOptions.RunContinuationsAsynchronously); access.Image(); service.Capture();
        service.Invalidate(); gate.SetResult(image); await Drain(service);
        Check(changes == 3, "clearing epoch invalidates in-flight capture");
        gate = new(TaskCreationOptions.RunContinuationsAsynchronously); access.Image(); service.Capture();
        gate.SetException(new InvalidDataException()); await Drain(service);
        Check(warnings == 1 && changes == 3, "decode failure reports status without storing content");
        access.Available = false; access.Text("locked"); service.Capture(); await Drain(service);
        Check(warnings == 2 && !service.IsBusy && changes == 3, "locked clipboard retries are bounded");
        access.Available = true; gate = null; access.Text("recovered"); service.Capture(); await Drain(service);
        Check(history.Items[0].Text == "recovered", "capture recovers on next clipboard change");
        Check(access.Closes == access.Opens - 5, "every successful clipboard open is closed");
    }
}
