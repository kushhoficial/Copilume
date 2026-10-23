using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace Copilume;

public sealed class ClipRow(Clip clip, Func<Clip, BitmapSource?> thumbnail)
{
    public Clip Clip => clip;
    public BitmapSource? Thumbnail => thumbnail(clip);
    public string DisplayTitle => clip.IsImage ? clip.DisplayText : clip.SingleLine;
    public string Metadata => $"{(clip.IsImage ? "IMAGEM" : clip.IsLink ? "LINK" : "TEXTO")}  ·  {clip.Date:HH:mm}  ·  {(clip.IsImage ? $"{Math.Max(1, clip.ByteCount / 1000):N0} KB" : $"{clip.Text.Length:N0} caracteres")}";
    public Geometry Glyph => (Geometry)Application.Current.Resources[clip.Pinned ? "PinIcon" : clip.IsLink ? "LinkIcon" : "TextIcon"];
}

public partial class MainWindow : Window
{
    private readonly bool demo;
    private readonly History history = new();
    private Settings settings = new();
    private ClipboardService? clipboard;
    private IntPtr handle, target;
    private HwndSource? source;
    private Forms.NotifyIcon? tray;
    private GlassMenuWindow? activeMenu;
    private DispatcherTimer? foregroundTimer;
    private bool quitting, ready, saveWarning;
    private int hotKeyId;
    private int pasteEpoch;
    private readonly Dictionary<Guid, BitmapSource> thumbnails = new();
    private readonly List<Window> auxiliary = new();

    public MainWindow(bool demo = false, string? appearance = null)
    {
        this.demo = demo;
        InitializeComponent();
        WindowContour.Attach(this, 24);
        if (demo) Title += " — Prévia isolada";
        if (demo && Environment.GetCommandLineArgs().Contains("--inspect-ui")) ShowInTaskbar = true;
        Icon = new BitmapImage(new Uri("pack://application:,,,/Copilume;component/Assets/AppIcon.png"));
        if (!demo)
        {
            try { settings = Settings.Load(); }
            catch (Exception error) when (IsStorageError(error)) { Tell("Preferências inválidas. Os padrões serão usados nesta sessão."); }
        }
        if (appearance != null) settings.Appearance = appearance;
        history.Limit = settings.Limit;
        if (settings.Persistent && !demo)
        {
            try { history.Load(Settings.HistoryPath); }
            catch (Exception error) when (IsStorageError(error))
            {
                settings.Persistent = false;
                Tell("O histórico salvo não pôde ser aberto. Arquivo preservado; captura somente em memória. Faça uma cópia antes de reativar a persistência.");
            }
        }
        if (demo)
        {
            history.Capture(new Clip { Text = "Copie agora. Reencontre depois.", Pinned = true });
            history.Capture(new Clip { Text = "https://github.com/kushhoficial/Copilume" });
            history.Capture(new Clip { Text = "Ideias que merecem ficar por perto.\nTextos, links e imagens, em um só lugar." });
            history.Capture(new Clip { Image = ImageCodec.Demo() });
            Tell("Prévia segura. Captura e colagem desativadas.");
            FooterButton.Content = "Prévia • dados fictícios";
        }
        ApplyAppearance();
        Closing += OnClosing;
        Closed += (_, _) => { if (demo && !quitting) Application.Current.Shutdown(); };
        Deactivated += (_, _) => { if (auxiliary.Count == 0 && activeMenu == null && !(demo && Environment.GetCommandLineArgs().Contains("--inspect-ui"))) Hide(); };
        ready = true;
        Refresh();
    }

    public void Initialize()
    {
        handle = new WindowInteropHelper(this).EnsureHandle();
        ApplyAppearance();
        if (demo)
        {
            if (Environment.GetCommandLineArgs().Contains("--demo-tray")) InitializeTray();
            return;
        }
        source = HwndSource.FromHwnd(handle);
        source.AddHook(WindowMessage);
        clipboard = new ClipboardService(handle, history, Changed, Tell);
        if (!Native.AddClipboardFormatListener(handle)) Tell("Falha ao iniciar captura. Reinicie o Copilume.");
        if (!ChangeShortcut(settings.Modifiers, settings.Key)) Tell("Atalho ocupado. Escolha outro nas preferências.");
        InitializeTray();
        foregroundTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        foregroundTimer.Tick += (_, _) => RememberTarget();
        foregroundTimer.Start();
        RememberTarget();
    }
    private void InitializeTray()
    {
        using var imageStream = Application.GetResourceStream(new Uri("pack://application:,,,/Copilume;component/Assets/AppIcon.png")).Stream;
        using var bitmap = new System.Drawing.Bitmap(imageStream);
        var iconHandle = bitmap.GetHicon();
        using var borrowedIcon = System.Drawing.Icon.FromHandle(iconHandle);
        tray = new Forms.NotifyIcon { Text = demo ? "Copilume — prévia isolada" : "Copilume — histórico local", Icon = (System.Drawing.Icon)borrowedIcon.Clone(), Visible = true };
        DestroyIcon(iconHandle);
        tray.DoubleClick += (_, _) => OpenHistory();
        tray.MouseUp += (_, e) =>
        {
            if (e.Button != Forms.MouseButtons.Right) return;
            var point = Forms.Cursor.Position;
            Dispatcher.BeginInvoke(new Action(() => OpenMenu(BuildTrayMenu(), new Point(point.X, point.Y))));
        };
    }
    internal GlassMenuWindow BuildTrayMenu() => new("Copilume", demo ? "Prévia isolada • dados fictícios" : "Seu histórico, sempre por perto.", new MenuAction?[]
    {
        new("Abrir histórico", "CopyIcon", OpenHistory, ShortcutName()),
        new(history.Paused ? "Retomar captura" : "Pausar captura", history.Paused ? "PlayIcon" : "PauseIcon", TogglePause),
        null,
        new("Preferências", "SettingsIcon", () => { OpenHistory(); ShowSettings(); }),
        new("Conheça o Copilume", "InfoIcon", () => { OpenHistory(); About(); }),
        null,
        new("Sair do Copilume", "ExitIcon", Quit, Destructive: true)
    });
    internal void OpenMenu(GlassMenuWindow menu, Point screenPixels, bool animate = true, Window? owner = null)
    {
        activeMenu?.Close(); activeMenu = menu;
        if (owner != null) menu.Owner = owner;
        else if (IsVisible) menu.Owner = this;
        var returnOwner = menu.Owner;
        bool ownerWasVisible = returnOwner?.IsVisible == true;
        menu.Closed += (_, _) =>
        {
            if (menu.ActionInvoked || menu.RestoreOwnerFocus)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
                {
                    if (!quitting && ownerWasVisible && returnOwner != null)
                    {
                        if (!returnOwner.IsVisible) returnOwner.Show();
                        returnOwner.Activate();
                    }
                    if (activeMenu == menu) activeMenu = null;
                }));
                return;
            }
            if (activeMenu == menu) activeMenu = null;
            if (demo && Environment.GetCommandLineArgs().Contains("--inspect-ui")) return;
            if (!menu.ActionInvoked) Dispatcher.BeginInvoke(new Action(() => { if (activeMenu == null && auxiliary.Count == 0 && !IsActive) Hide(); }));
        };
        menu.ShowAt(screenPixels, animate);
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr icon);
    private IntPtr WindowMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == Native.ClipboardUpdate) clipboard?.Capture();
        if (message == Native.HotKey)
        {
            if (IsVisible && IsActive) { pasteEpoch++; Hide(); }
            else OpenHistory();
            handled = true;
        }
        return IntPtr.Zero;
    }
    private void RememberTarget()
    {
        var foreground = Native.GetForegroundWindow();
        if (foreground == IntPtr.Zero) return;
        Native.GetWindowThreadProcessId(foreground, out uint process);
        if (process != Environment.ProcessId) target = foreground;
    }
    public void OpenHistory()
    {
        pasteEpoch++;
        activeMenu?.Close();
        if (!demo) RememberTarget();
        foreach (var window in auxiliary.ToArray()) window.Close();
        if (!IsVisible) Show();
        WindowState = WindowState.Normal;
        Activate();
        ApplyAppearance();
        SearchBox.Focus();
    }
    private void Tell(string message) { StatusText.Text = message; if (tray != null) tray.Text = history.Paused ? "Copilume — captura pausada" : "Copilume — histórico local"; }
    private static bool IsStorageError(Exception error) => error is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or Win32Exception or ArgumentException;
    private void Changed() { Persist(); Refresh(); }
    private void Persist()
    {
        if (demo || !settings.Persistent) return;
        try { history.Save(Settings.HistoryPath); saveWarning = false; }
        catch (Exception error) when (IsStorageError(error))
        {
            if (!saveWarning) { Tell("Falha ao salvar. A captura continua em memória; verifique espaço e permissões."); saveWarning = true; }
        }
    }
    private void Refresh()
    {
        if (!ready) return;
        var id = (Clips.SelectedItem as ClipRow)?.Clip.Id;
        foreach (var key in thumbnails.Keys.Where(key => !history.Items.Any(x => x.Id == key)).ToArray()) thumbnails.Remove(key);
        var rows = history.Search(SearchBox.Text, (Scope)Filter.SelectedIndex).Select(clip => new ClipRow(clip, Thumbnail)).ToArray();
        Clips.ItemsSource = rows;
        Clips.SelectedIndex = Math.Max(0, Array.FindIndex(rows, row => row.Clip.Id == id));
        EmptyState.Visibility = rows.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyTitle.Text = history.Items.Count == 0 ? "Seu próximo favorito começa com uma cópia." : "Nenhum item encontrado.";
        EmptyDetail.Text = history.Items.Count == 0 ? "Copie textos, links ou imagens para começar." : "Tente outra busca ou escolha um filtro diferente.";
        CountLabel.Text = $"{rows.Length} {(rows.Length == 1 ? "item" : "itens")}  ·  mais recentes primeiro";
        UpdateActions();
        ShortcutBadge.Content = ShortcutName();
    }
    private BitmapSource? Thumbnail(Clip clip)
    {
        if (clip.Image is not { } image) return null;
        if (thumbnails.TryGetValue(clip.Id, out var thumbnail)) return thumbnail;
        try
        {
            thumbnail = ImageCodec.Thumbnail(image);
            if (thumbnails.Count >= 64) thumbnails.Remove(thumbnails.Keys.First());
            thumbnails[clip.Id] = thumbnail;
            return thumbnail;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or FileFormatException or NotSupportedException or ArgumentException or InvalidOperationException) { return null; }
    }
    private Clip? Selected => (Clips.SelectedItem as ClipRow)?.Clip;
    private void SearchChanged(object sender, TextChangedEventArgs e) => Refresh();
    private void FilterChanged(object sender, SelectionChangedEventArgs e) => Refresh();
    private void ClipSelectionChanged(object sender, SelectionChangedEventArgs e) { if (ready) UpdateActions(); }
    private void UpdateActions()
    {
        PinButton.IsEnabled = PreviewButton.IsEnabled = DeleteButton.IsEnabled = Selected != null;
        ClearButton.IsEnabled = history.Items.Count > 0;
        PinButton.ToolTip = Selected?.Pinned == true ? "Desafixar item" : "Fixar item";
    }
    private void DragPanel(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        // Buttons inside the header retain their normal click behavior.
        for (DependencyObject? current = e.OriginalSource as DependencyObject; current != null; current = current is Visual ? VisualTreeHelper.GetParent(current) : LogicalTreeHelper.GetParent(current))
            if (current is Button) return;
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
    private void ClosePanel(object sender, RoutedEventArgs e) { pasteEpoch++; if (demo) Close(); else Hide(); }
    private void ChooseClick(object sender, RoutedEventArgs e) { if (Selected is { } clip) Use(clip, false, false); }
    private async void Use(Clip clip, bool copyOnly, bool singleLine)
    {
        if (demo) { Tell("A prévia não altera a área de transferência."); return; }
        int ticket = ++pasteEpoch;
        if (clipboard?.Write(clip, singleLine) != true) return;
        uint sequence = Native.GetClipboardSequenceNumber();
        var destination = target;
        if (copyOnly || !settings.AutoPaste) { Tell("Copiado. Use Ctrl + V no destino."); return; }
        Hide();
        bool restored = Native.IsWindow(destination) && Native.SetForegroundWindow(destination);
        if (restored)
        {
            for (int attempt = 0; attempt < 30; attempt++)
            {
                await Task.Delay(30);
                if (ticket != pasteEpoch || sequence != Native.GetClipboardSequenceNumber() || Native.GetForegroundWindow() != destination) return;
                if (Native.ModifiersReleased())
                {
                    if (Native.Paste()) return;
                    break;
                }
            }
        }
        tray?.ShowBalloonTip(3000, "Copilume", "Copiado. Use Ctrl + V no destino. A colagem automática não foi concluída.", Forms.ToolTipIcon.Info);
    }
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape) { ClosePanel(sender, e); e.Handled = true; }
        else if (e.Key == System.Windows.Input.Key.Enter && (SearchBox.IsKeyboardFocusWithin || Clips.IsKeyboardFocusWithin) && Selected is { } clip) { Use(clip, false, false); e.Handled = true; }
        else if ((e.Key == System.Windows.Input.Key.Apps || e.Key == System.Windows.Input.Key.F10 && Keyboard.Modifiers == ModifierKeys.Shift) && Selected is { } selected)
        {
            Clips.ScrollIntoView(Clips.SelectedItem); Clips.UpdateLayout();
            if (Clips.ItemContainerGenerator.ContainerFromItem(Clips.SelectedItem) is ListBoxItem row) ShowRowMenu(row, selected);
            e.Handled = true;
        }
        else if ((e.Key == System.Windows.Input.Key.Down || e.Key == System.Windows.Input.Key.Up) && SearchBox.IsKeyboardFocusWithin)
        {
            if (Clips.Items.Count > 0) { Clips.SelectedIndex = Math.Clamp(Clips.SelectedIndex + (e.Key == System.Windows.Input.Key.Down ? 1 : -1), 0, Clips.Items.Count - 1); Clips.ScrollIntoView(Clips.SelectedItem); }
            e.Handled = true;
        }
    }
    private void Pin(Clip clip) { clip.Pinned = !clip.Pinned; Changed(); }
    private void PinClick(object sender, RoutedEventArgs e) { if (Selected is { } clip) Pin(clip); }
    private void DeleteClick(object sender, RoutedEventArgs e) { if (Selected is { } clip) { history.Remove(clip.Id); Changed(); } }
    private void PauseClick(object sender, RoutedEventArgs e) => TogglePause();
    private void TogglePause()
    {
        history.Paused = !history.Paused; clipboard?.Invalidate();
        PauseLabel.Text = history.Paused ? "Retomar" : "Pausar";
        PauseGlyph.Data = (Geometry)FindResource(history.Paused ? "PlayIcon" : "PauseIcon");
        CaptureLabel.Text = history.Paused ? "Captura pausada" : "Somente neste computador";
        Tell(history.Paused ? "Captura pausada." : "Captura retomada. Copie algo novo para guardar.");
    }
    private void ClearClick(object sender, RoutedEventArgs e)
    {
        bool? includePinned = null;
        var window = Auxiliary("Limpar histórico", 460, 360);
        var panel = new StackPanel { Margin = new Thickness(26, 12, 26, 24) };
        panel.Children.Add(new TextBlock { Text = "Um novo começo?", FontSize = 25, FontWeight = FontWeights.Bold });
        var detail = new TextBlock { Text = "Limpe as cópias recentes e mantenha seus fixados. Você também pode escolher excluir todos os itens.", TextWrapping = TextWrapping.Wrap, LineHeight = 21, Margin = new Thickness(0, 14, 0, 20) };
        detail.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); panel.Children.Add(detail);
        void Choice(string label, bool? value)
        {
            var button = new Button { Content = label, Margin = new Thickness(0, 0, 0, 8), IsCancel = value == null };
            button.Click += (_, _) => { includePinned = value; window.Close(); }; panel.Children.Add(button);
        }
        Choice("Limpar recentes • preservar fixados", false);
        Choice("Excluir tudo, inclusive fixados", true);
        Choice("Cancelar", null);
        window.Body.Content = panel;
        window.ShowDialog();
        if (includePinned == null) return;
        clipboard?.Invalidate(); history.Clear(includePinned.Value); Changed();
    }
    private void RowMenu(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject element || ItemsControl.ContainerFromElement(Clips, element) is not ListBoxItem row || row.DataContext is not ClipRow data) return;
        ShowRowMenu(row, data.Clip);
        e.Handled = true;
    }
    private void ShowRowMenu(ListBoxItem row, Clip clip)
    {
        Clips.SelectedItem = row.DataContext;
        OpenMenu(BuildItemMenu(clip), row.PointToScreen(new Point(30, row.ActualHeight - 8)));
    }
    internal GlassMenuWindow BuildItemMenu(Clip clip) => new("Ações do item", clip.IsImage ? clip.DisplayText : clip.IsLink ? "Link da área de transferência" : "Texto da área de transferência", new MenuAction?[]
    {
        new("Copiar sem colar", "CopyIcon", () => Use(clip, true, false)),
        new("Copiar em uma linha", "TextIcon", () => Use(clip, true, true), Enabled: !clip.IsImage),
        null,
        new(clip.Pinned ? "Desafixar" : "Fixar", "PinIcon", () => Pin(clip)),
        new("Visualizar conteúdo", "EyeIcon", () => Preview(clip)),
        null,
        new("Excluir item", "TrashIcon", () => { history.Remove(clip.Id); Changed(); }, Destructive: true)
    });
    private void PreviewClick(object sender, RoutedEventArgs e) { if (Selected is { } clip) Preview(clip); }
    private SurfaceWindow Auxiliary(string title, double width, double height)
    {
        var window = new SurfaceWindow(title) { Width = width, Height = height, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        auxiliary.Add(window);
        window.Closed += (_, _) => { auxiliary.Remove(window); if (!quitting) { Activate(); SearchBox.Focus(); } };
        return window;
    }
    private void Preview(Clip clip) => BuildPreview(clip).ShowDialog();
    internal SurfaceWindow BuildPreview(Clip clip)
    {
        var window = Auxiliary("Visualizar — Copilume", 660, 520);
        if (clip.Image is { } image)
        {
            try { window.Body.Content = new ScrollViewer { Style = (Style)FindResource("SoftScrollViewer"), Content = new System.Windows.Controls.Image { Source = ImageCodec.Decode(image.Png), Stretch = Stretch.Uniform, Margin = new Thickness(24) } }; }
            catch (Exception error) when (error is IOException or InvalidDataException or FileFormatException or NotSupportedException or ArgumentException or InvalidOperationException) { window.Body.Content = new TextBlock { Text = "Imagem inválida.", Margin = new Thickness(24) }; }
        }
        else window.Body.Content = new TextBox { Text = clip.Text, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(24) };
        return window;
    }
    private void AboutClick(object sender, RoutedEventArgs e) => About();
    private void About() => BuildAbout().ShowDialog();
    internal SurfaceWindow BuildAbout()
    {
        var window = Auxiliary("Conheça o Copilume", 490, 530);
        var panel = new StackPanel { Margin = new Thickness(28, 20, 28, 24) };
        panel.Children.Add(new System.Windows.Controls.Image { Source = Icon, Width = 68, Height = 68, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 20) });
        panel.Children.Add(new TextBlock { Text = "Copie agora.\nReencontre depois.", FontSize = 29, FontWeight = FontWeights.Bold });
        void Detail(string text) { var label = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 18, 0, 0), LineHeight = 22 }; label.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); panel.Children.Add(label); }
        Detail("Textos, links e imagens sempre por perto. Um histórico local, simples e aberto, feito para acompanhar seu ritmo.");
        Detail("Sem conta, nuvem, telemetria ou IA. Histórico apenas em memória por padrão.");
        Detail("Copilume • por kushhoficial • licença MIT");
        var project = new Button { Content = "Conhecer o projeto no GitHub", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 22, 0, 0) };
        project.Click += (_, _) => Process.Start(new ProcessStartInfo("https://github.com/kushhoficial/Copilume") { UseShellExecute = true });
        panel.Children.Add(project); window.Body.Content = new ScrollViewer { Style = (Style)FindResource("SoftScrollViewer"), Content = panel };
        return window;
    }
    private string ShortcutName()
    {
        string modifiers = ((settings.Modifiers & 2) != 0 ? "Ctrl + " : "") + ((settings.Modifiers & 1) != 0 ? "Alt + " : "") + ((settings.Modifiers & 4) != 0 ? "Shift + " : "");
        return modifiers + KeyInterop.KeyFromVirtualKey((int)settings.Key);
    }
    private bool ChangeShortcut(uint modifiers, uint key)
    {
        if (modifiers == 0 || (modifiers & ~7u) != 0 || key < 0x30 || key > 0x5A || key is > 0x39 and < 0x41) return false;
        if (demo) { settings.Modifiers = modifiers; settings.Key = key; return true; }
        // Try the replacement under another ID before releasing the working binding.
        int candidate = hotKeyId == 1 ? 2 : 1;
        if (!Native.RegisterHotKey(handle, candidate, modifiers | 0x4000, key)) return false;
        if (hotKeyId != 0) Native.UnregisterHotKey(handle, hotKeyId);
        hotKeyId = candidate; settings.Modifiers = modifiers; settings.Key = key;
        return true;
    }
    private void SettingsClick(object sender, RoutedEventArgs e) => ShowSettings();
    private void ShowSettings() => BuildSettings().ShowDialog();
    internal SurfaceWindow BuildSettings()
    {
        var window = Auxiliary("Preferências — Copilume", 510, 740);
        var panel = new StackPanel { Margin = new Thickness(26, 12, 26, 24) };
        panel.Children.Add(new TextBlock { Text = "Do seu jeito.", FontSize = 28, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 16) });
        void Label(string text)
        {
            var label = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 8), FontSize = 12, LineHeight = 18 };
            label.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); panel.Children.Add(label);
        }
        Label("Atalho global (Ctrl, Alt ou Shift + letra/número)");
        var shortcut = new TextBox { Text = ShortcutName(), IsReadOnly = true };
        shortcut.PreviewKeyDown += (_, e) =>
        {
            var key = e.Key == System.Windows.Input.Key.System ? e.SystemKey : e.Key;
            if (key is System.Windows.Input.Key.LeftAlt or System.Windows.Input.Key.RightAlt or System.Windows.Input.Key.LeftCtrl or System.Windows.Input.Key.RightCtrl or System.Windows.Input.Key.LeftShift or System.Windows.Input.Key.RightShift) return;
            e.Handled = true;
            if ((Keyboard.Modifiers & ModifierKeys.Windows) != 0) { MessageBox.Show(window, "Atalhos com a tecla Windows são reservados pelo sistema."); return; }
            uint modifiers = (uint)(((Keyboard.Modifiers & ModifierKeys.Alt) != 0 ? 1 : 0) | ((Keyboard.Modifiers & ModifierKeys.Control) != 0 ? 2 : 0) | ((Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? 4 : 0));
            uint virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
            if (settings.Modifiers == modifiers && settings.Key == virtualKey) return;
            if (ChangeShortcut(modifiers, virtualKey)) { shortcut.Text = ShortcutName(); SaveSettings(); Refresh(); }
            else MessageBox.Show(window, "Atalho inválido ou ocupado. O atalho anterior foi mantido.");
        };
        panel.Children.Add(shortcut);
        Label("Limite de itens (1 a 5000)");
        var limit = new TextBox { Text = history.Limit.ToString() }; panel.Children.Add(limit);
        var applyLimit = new Button { Content = "Aplicar limite", Margin = new Thickness(0, 8, 0, 8), HorizontalAlignment = HorizontalAlignment.Left };
        applyLimit.Click += (_, _) => { if (int.TryParse(limit.Text, out int value) && value is >= 1 and <= 5000) { settings.Limit = value; history.Limit = value; clipboard?.Invalidate(); SaveSettings(); Changed(); } else MessageBox.Show(window, "Informe um número entre 1 e 5000."); };
        panel.Children.Add(applyLimit);
        var persistence = new CheckBox { Content = "Salvar histórico entre reinicializações", IsChecked = settings.Persistent, IsEnabled = !demo };
        persistence.Click += (_, _) =>
        {
            bool enabled = persistence.IsChecked == true;
            bool previous = settings.Persistent;
            try
            {
                if (enabled) history.Save(Settings.HistoryPath); else if (File.Exists(Settings.HistoryPath)) File.Delete(Settings.HistoryPath);
                settings.Persistent = enabled; settings.Save();
            }
            catch (Exception error) when (IsStorageError(error)) { persistence.IsChecked = settings.Persistent = previous; Tell("Não foi possível alterar o armazenamento. Verifique espaço e permissões."); }
        };
        panel.Children.Add(persistence);
        Label("Desativado por padrão. Quando habilitado, o histórico é criptografado para seu usuário do Windows. Desativar remove o arquivo salvo.");
        var paste = new CheckBox { Content = "Colar automaticamente no aplicativo anterior", IsChecked = settings.AutoPaste, IsEnabled = !demo };
        paste.Click += (_, _) => { settings.AutoPaste = paste.IsChecked == true; SaveSettings(); }; panel.Children.Add(paste);
        var startup = new CheckBox { Content = "Iniciar ao entrar no Windows", IsChecked = !demo && StartupEnabled(), IsEnabled = !demo };
        startup.Click += (_, _) =>
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
                if (startup.IsChecked == true) key.SetValue("Copilume", $"\"{Environment.ProcessPath}\" --background"); else key.DeleteValue("Copilume", false);
            }
            catch (Exception error) when (error is UnauthorizedAccessException or System.Security.SecurityException or IOException) { startup.IsChecked = StartupEnabled(); MessageBox.Show(window, "Não foi possível alterar a inicialização."); }
        }; panel.Children.Add(startup);
        Label("Aparência");
        var appearance = new ListBox { ItemsSource = new[] { "Sistema", "Claro", "Escuro" }, SelectedItem = settings.Appearance, BorderThickness = new Thickness(0), Background = Brushes.Transparent, ItemsPanel = (ItemsPanelTemplate)FindResource("SegmentedPanel"), ItemContainerStyle = (Style)FindResource("Segment") };
        System.Windows.Automation.AutomationProperties.SetName(appearance, "Aparência");
        appearance.SelectionChanged += (_, _) => { settings.Appearance = appearance.SelectedItem as string ?? "Sistema"; ApplyAppearance(); SaveSettings(); };
        var appearanceSurface = new Border { CornerRadius = new CornerRadius(17), Padding = new Thickness(3), BorderThickness = new Thickness(1), Child = appearance };
        appearanceSurface.SetResourceReference(Border.BackgroundProperty, "Hover"); appearanceSurface.SetResourceReference(Border.BorderBrushProperty, "Hairline"); panel.Children.Add(appearanceSurface);
        Label("Pause a captura antes de copiar segredos. Marcações de privacidade dependem do aplicativo de origem.");
        var close = new Button { Content = "Concluído", Margin = new Thickness(0, 12, 0, 0) }; close.Click += (_, _) => window.Close(); panel.Children.Add(close);
        window.Body.Content = new ScrollViewer { Style = (Style)FindResource("SoftScrollViewer"), Content = panel };
        return window;
    }
    private static bool StartupEnabled()
    {
        try { using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"); return key?.GetValue("Copilume") != null; }
        catch (System.Security.SecurityException) { return false; }
    }
    private void SaveSettings()
    {
        if (demo) return;
        try { settings.Save(); } catch (Exception error) when (IsStorageError(error)) { Tell("Preferências não salvas. Verifique as permissões da pasta de dados."); }
    }
    private void ApplyAppearance()
    {
        bool dark = Theme.Apply(settings.Appearance);
        bool acrylic = handle != IntPtr.Zero && WindowMaterial.Apply(this, dark);
        ShellBorder.SetResourceReference(Border.BackgroundProperty, acrylic ? "WindowTint" : "Surface");
        Ambient.Visibility = SystemParameters.HighContrast || !WindowMaterial.TransparencyEnabled() ? Visibility.Collapsed : Visibility.Visible;
        foreach (var window in auxiliary.OfType<SurfaceWindow>()) window.RefreshMaterial();
    }
    private void OnClosing(object? sender, CancelEventArgs e) { if (!quitting && !demo) { e.Cancel = true; pasteEpoch++; Hide(); } }
    public void Quit()
    {
        quitting = true; pasteEpoch++; clipboard?.Invalidate();
        activeMenu?.Close();
        foreach (var window in auxiliary.ToArray()) window.Close();
        foregroundTimer?.Stop();
        if (!demo) { Native.RemoveClipboardFormatListener(handle); if (hotKeyId != 0) Native.UnregisterHotKey(handle, hotKeyId); source?.RemoveHook(WindowMessage); }
        if (tray != null) { tray.Visible = false; tray.Icon?.Dispose(); tray.Dispose(); }
        Application.Current.Shutdown();
    }
}
