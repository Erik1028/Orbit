using System;
using System.Collections.Generic;
using System.Drawing;
using Vortice.DirectWrite;

namespace Orbit;

// The watch editor and the folder widget (WatchFolders): a folder is added from the palette like any widget. Selected: Open folder - the
// editor then works inside it (its widgets on its own grid, the same palette, selection, drag and sizes; the watch shows it open), the
// strip over the watch says so: Back to the page, Rename..., Move out of the folder (the selected widget onto the page). Into a folder:
// drag a widget onto it. Rename: the desktop window asks for the name (in VR it is shown only). Remove a folder that has widgets: asked
// first - keep them (onto the page, where they fit) or remove them too.
public sealed partial class SettingsView
{
    WatchWidget _wfolder;               // the folder the editor works in (null: the page)
    WatchWidget _wfolderAsk;            // a folder whose Remove was clicked: keep its widgets or not?

    // the list the editor works on: the open folder's widgets, else the page's; the watch is told which folder to show open
    List<WatchWidget> EditList()
    {
        var page = WatchLayout();
        if (_wfolder != null && !page.Contains(_wfolder)) { _wfolder = null; _wselW = null; }
        _h.Wrist.EditFolder = _wfolder;
        return _wfolder == null ? page : _wfolder.Children ??= new List<WatchWidget>();
    }

    public bool InFolder => _wfolder != null;           // --snapshot: the editor works in a folder

    void OpenFolder(WatchWidget f)
    {
        _wfolder = f;
        _wselW = null;
        _wcell = (-1, -1);
        _wmsg = null;
        _wfolderAsk = null;
        _wshare = _wadd = false;
        _h.Wrist.EditFolder = f;
    }

    void LeaveFolder()
    {
        var f = _wfolder;
        _wfolder = null;
        _wselW = f;
        _wcell = (-1, -1);
        _wmsg = null;
        _h.Wrist.EditFolder = null;
    }

    // the desktop window asks for the folder's name (empty: "Folder" again)
    void RenameFolder(WatchWidget f)
    {
        AskText?.Invoke("Folder name", "The folder's name on the watch (empty: Folder).", f.Name ?? "", text =>
        {
            if (text == null) return;
            f.Name = WatchFolders.CleanName(text);
            _h.Cfg.MarkDirty();
        });
    }

    // the selected widget out of the folder onto the page: where it fits, in the page's nearest size
    void MoveOutSelected(Ui ui)
    {
        var sel = _wselW;
        if (_wfolder == null || sel == null) return;
        var page = WatchLayout();
        if (WatchFolders.MoveOut(page, WatchPages.Grid(C.Wrist, EditPage), _wfolder, sel, Theme.Classic)) { _wselW = null; _wmsg = null; ui.Changed = true; }
        else _wmsg = "No room on the page: make room there first.";
    }

    // a widget dropped onto a folder (the drag let go over it): into the folder. True when it went in (or said why not)
    bool FolderDrop(Ui ui, List<WatchWidget> list, int cx, int cy)
    {
        if (_wfolder != null || _wdrag == null || WatchFolders.Is(_wdrag)) return false;
        var f = WatchWidgets.At(list, cx, cy);
        if (!WatchFolders.Is(f) || f == _wdrag) return false;
        if (WatchFolders.MoveIn(list, f, _wdrag, Theme.Classic)) { _wselW = f; _wmsg = null; ui.Changed = true; }
        else _wmsg = "That folder is full.";
        return true;
    }

    // Remove on a folder with widgets in it: ask first (true: asked, nothing removed yet)
    bool FolderRemoveAsk(WatchWidget sel)
    {
        if (!WatchFolders.Is(sel) || WatchFolders.Kids(sel).Count == 0) return false;
        _wfolderAsk = sel;
        return true;
    }

    void FolderRemove(Ui ui, List<WatchWidget> list, bool keep)
    {
        var f = _wfolderAsk;
        _wfolderAsk = null;
        if (f == null) return;
        int lost = WatchFolders.Remove(list, EditGridSpec, f, keep, Theme.Classic);
        _wselW = null;
        _wmsg = lost > 0 ? Lang.F(lost == 1 ? "{0} widget did not fit on the page." : "{0} widgets did not fit on the page.", lost) : null;
        ui.Changed = true;
    }

    // ---- Modern / Cyber ----

    // The strip over the watch while the editor is in a folder (in place of the pages): Back, the folder's name, Rename..., Move out
    bool FolderStrip(Ui ui, float x, float y, float w, float h)
    {
        if (_wfolder == null) return false;
        if (ui.Button("wf.back", new RectangleF(x, y, 96, h), "Back", Theme.IcoLeft, false, true, 13)) { LeaveFolder(); return true; }
        bool desk = AskText != null;
        if (ui.Button("wf.rename", new RectangleF(x + 102, y, 150, h), WatchFolders.Name(_wfolder), "\uE70F", false, desk, 13)) RenameFolder(_wfolder);
        bool sel = _wselW != null && _wfolder.Children?.Contains(_wselW) == true;
        if (ui.Button("wf.out", new RectangleF(x + w - 228, y, 228, h), "Move out of the folder", Theme.IcoUp, false, sel, 13)) MoveOutSelected(ui);
        return true;
    }

    // a folder selected on the page: Open folder and Rename... in a row (or, Remove clicked, keep its widgets?); how far the rest goes down
    float FolderActions(Ui ui, List<WatchWidget> list, WatchWidget sel, float rx, float rw)
    {
        if (!WatchFolders.Is(sel) || _wfolder != null) return 0;
        if (_wfolderAsk == sel)
        {
            ui.Label(new RectangleF(rx, 176, rw, 18), Lang.F("Remove the folder. Its {0} widgets:", WatchFolders.Kids(sel).Count), Theme.Warn, 12);
            float bw3 = (rw - 16) / 3;
            if (ui.Button("wf.keep", new RectangleF(rx, 196, bw3, 30), "Keep them", null, true, true, 12)) FolderRemove(ui, list, true);
            if (ui.Button("wf.drop", new RectangleF(rx + bw3 + 8, 196, bw3, 30), "Remove all", Theme.IcoDelete, false, true, 12)) FolderRemove(ui, list, false);
            if (ui.Button("wf.cancel", new RectangleF(rx + 2 * (bw3 + 8), 196, bw3, 30), "Cancel", null, false, true, 12)) _wfolderAsk = null;
            return 54;
        }
        if (ui.Button("wf.open", new RectangleF(rx, 180, 150, 34), "Open folder", WatchFolders.Icon, true, true, 13)) OpenFolder(sel);
        if (ui.Button("wf.rename", new RectangleF(rx + 158, 180, rw - 158, 34), "Rename...", null, false, AskText != null, 13)) RenameFolder(sel);
        return 42;
    }

    // ---- the 95 family ----

    // in a folder: where the page's row was (Page, New page, Remove page), Back and Move out; Rename... beside them
    bool FolderRow95(Ui ui)
    {
        if (_wfolder == null) return false;
        if (ui.Button95("wf95.back", new RectangleF(16, 326, 70, 23), "< &Back")) { LeaveFolder(); return true; }
        if (ui.Button95("wf95.ren", new RectangleF(92, 326, 86, 23), "Re&name...", AskText != null)) RenameFolder(_wfolder);
        bool sel = _wselW != null && _wfolder.Children?.Contains(_wselW) == true;
        if (ui.Button95("wf95.out", new RectangleF(184, 326, 182, 23), "Move &out of the folder", sel)) MoveOutSelected(ui);
        return true;
    }

    // a folder selected (95): Open folder, Rename...; or, Remove clicked, keep its widgets?
    void FolderSelected95(Ui ui, List<WatchWidget> list, WatchWidget sel)
    {
        if (!WatchFolders.Is(sel) || _wfolder != null) return;
        if (_wfolderAsk == sel)
        {
            ui.Text95(Lang.F("Keep its {0} widgets?", WatchFolders.Kids(sel).Count), new RectangleF(388, 352, 150, 13), 0xFF800000);
            if (ui.Button95("wf95.keep", new RectangleF(540, 348, 60, 23), "&Keep")) FolderRemove(ui, list, true);
            if (ui.Button95("wf95.drop", new RectangleF(604, 348, 60, 23), "&Delete")) FolderRemove(ui, list, false);
            if (ui.Button95("wf95.cancel", new RectangleF(668, 348, 54, 23), "Cancel")) _wfolderAsk = null;
            return;
        }
        if (ui.Button95("wf95.open", new RectangleF(388, 348, 100, 23), "&Open folder", true, true)) OpenFolder(sel);
        if (ui.Button95("wf95.rename", new RectangleF(494, 348, 86, 23), "Re&name...", AskText != null)) RenameFolder(sel);
    }
}
