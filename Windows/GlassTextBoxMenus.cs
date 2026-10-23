using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;

namespace Copilume;

internal static class GlassTextBoxMenus
{
    internal static GlassMenuWindow Create(TextBox box)
    {
        void Edit(Action action)
        {
            try { box.Focus(); action(); }
            catch (ExternalException) { box.ToolTip = "Área de transferência ocupada. Tente novamente."; }
        }
        return new GlassMenuWindow("Editar texto", box.IsReadOnly ? "Conteúdo completo • somente leitura" : "Busca e edição", new MenuAction?[]
        {
            new("Desfazer", "UndoIcon", () => Edit(() => box.Undo()), "Ctrl + Z", !box.IsReadOnly && box.CanUndo),
            null,
            new("Recortar", "CutIcon", () => Edit(box.Cut), "Ctrl + X", !box.IsReadOnly && box.SelectionLength > 0),
            new("Copiar", "CopyIcon", () => Edit(box.Copy), "Ctrl + C", box.SelectionLength > 0),
            new("Colar", "PasteIcon", () => Edit(box.Paste), "Ctrl + V", !box.IsReadOnly),
            null,
            new("Selecionar tudo", "TextIcon", () => Edit(box.SelectAll), "Ctrl + A", box.Text.Length > 0)
        });
    }
    internal static void Open(object sender, ContextMenuEventArgs e)
    {
        if (sender is not TextBox box || Window.GetWindow(box) is not { } owner) return;
        e.Handled = true;
        var menu = Create(box);
        var anchor = box.PointToScreen(e.CursorLeft >= 0 ? new Point(e.CursorLeft, e.CursorTop) : new Point(12, box.ActualHeight - 4));
        if (Application.Current.MainWindow is MainWindow main) main.OpenMenu(menu, anchor, owner: owner);
        else { menu.Owner = owner; menu.ShowAt(anchor); }
    }
}
