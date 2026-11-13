# LwCadSystem.Web

The LW CAD console - the WPF **LwCadSystemUX** (`C:\Development\RandD\ClaudeCode\LW\Projects\LeverageUX_CAD\LwCadSystemUX`) - as a Blazor Server app on .NET 10 with DevExpress Blazor 26.1.4.

The domain is unchanged. Models, the keyboard-table function catalog, the La*/Lw* table classes, the LaGeo040 location engine, recommendation, write-back and the view models all live in `..\LwCad.Core`. This project is the UI layer only: one `ShellViewModel` per user circuit, rendered in Razor.

> **This console writes to the CAD tables.** With a La.cfg (and `Demo` off) it reads **and writes** the live CAD database, as the WPF build does. Take Call, Assign / Assist, Unit Status, Location Change, Release Unit, Self Initiate / Traffic Stop, Activate Unit and CAD Alerts all commit. Without a La.cfg, or with `Demo` on, it runs on the in-memory demo board and writes nothing. **Test with demo mode on.**

## Running it

```
dotnet run --project LwCadSystem.Web -- --demo            demo board, no database at all  (use this for testing)
dotnet run --project LwCadSystem.Web                      live: La.cfg from appsettings / LWCAD_LA_CFG / beside the app
```

- HTTPS runs on `https://localhost:7241` and HTTP on 5272 (`Properties\launchSettings.json`).
- The status line always says which back end is in use. It names the database and server only, never credentials.
- The server log does the same at start-up: demo mode, or live mode with the La.cfg search paths. It never logs a connection string.

## Configuration

The `CadSystem` section of appsettings replaces the WPF command-line switches. The switches are still accepted, and the command line wins over appsettings. Environment variables work too (`CadSystem__Demo=true`).

| Setting | Switch | WPF equivalent | Applied to |
| --- | --- | --- | --- |
| `Demo` (bool) | `--demo` | `--demo` | `CadHost.ForceDemoBoard` |
| `LaCfgPath` | `--cfg <path>` | `--cfg` | `LaGetConfig.OverridePath`. Blank falls back to LWCAD_LA_CFG, then beside the app, then the working directory |
| `EmpId` | `--emp <id>` | `--emp` / LWCAD_EMP_ID | `CadSession.Current.EmpId` (default 0) |
| `SessId` | `--sess <id>` | `--sess` / LWCAD_SESS_ID | `CadSession.Current.SessId` (default 1) |
| `LegacyFolder` | `--legacy <dir>` | `--legacy` / LWCAD_LEGACY_DIR | `LegacyLauncher.Folder` |
| `Keyboard` | `--kbrd <id>` | `--kbrd` | keyboard template instead of DAT1_KBRD_ID |
| `OpType` | `--optype <c>` | `--optype` | R / C / D / S / U instead of DAT1_OP_TYPE |
| `SecurityLevel` | `--sec <0-9>` | `--sec` | instead of DAT1_ACC_LEVEL (0 sees every key) |

The session values go through the WPF parser (`CadSession.FromArgs`), so the `LWCAD_*` environment variables still work as fallbacks.

`appsettings.Development.json` sets `"LaCfgPath": "C:\\Development\\Git\\Mass\\LW\\Development\\La.cfg"` with `"Demo": false`, so a development run behaves like production. That La.cfg names the local development Lowell database (CHANDLER-NB2\SQL2022); nothing in this solution points at live data. Run with `--demo` (or `CadSystem__Demo=true`) for the in-memory demo board. No credentials are kept in appsettings; they stay in La.cfg.

These settings are process-wide in Core, as they were per process in WPF. Every circuit on one server uses the same La.cfg, employee and session. There is no logon yet, the same as WPF.

### Legacy programs

A web server never starts a program: there is no `Process.Start` anywhere. `CadHost.LaunchLegacy` is set at start-up, and Core's `LegacyLauncher` now only goes through it.

- When a key's program is installed in `LegacyFolder` (or beside the app), the status line says "<program> runs on the desktop; open it from the Larimore desktop menu."
- When it is not installed, the key opens its notes stub, as in WPF.
- The WPF "already running" check for Map / Monitor read the local process list. It only runs when no host hook is set, so it never runs here.

## What maps to which WPF view

| WPF | Razor | Notes |
| --- | --- | --- |
| `ShellWindow.xaml(.cs)` | `Components\Cad\CadShell.razor(.cs)` | Board layout, modal layer, one-second clock (`ShellViewModel.Tick` via `InvokeAsync`), key routing, row menus, confirmations |
| top bar (in ShellWindow) | `Cad\TopBar.razor` | |
| `QuickMenuPanel` | `Cad\QuickMenu.razor` | KYB3 rail, search, collapse to 40px |
| `StandardFunctionsBar` | `Cad\FunctionBar.razor` | KYB2 F / Shift / Ctrl banks, folds to header |
| `CallListPanel` | `Cad\CallMonitor.razor` | Stacked / Active / Delayed - a DxGrid each |
| `UnitMonitorPanel` | `Cad\UnitMonitor.razor` | DxGrid; All / Available / Overdue, police funnel |
| `ColumnStrip.cs` | DxGrid columns + `Cad\MonitorLayout.cs` | Order, widths and visibility per monitor, saved per browser |
| `Converters\CellConverters.cs` | `Cad\MonitorColumns.cs` | Cell text per column key |
| column chooser popups | `Cad\ColumnChooser.razor` | DxFlyout + DxCheckBox, Reset to default (visibility, widths, order) |
| `RowMenu.cs`, `MonitorFocus.cs` | `CadShell` (DxContextMenu) + `wwwroot\js\cadsystem.js` | |
| `ShellWindow.OnPreviewKeyDown` | `wwwroot\js\cadsystem.js` + `CadShell.HandleKey` | See Keyboard |
| `TakeCallPanel` | `Panels\TakeCallPanel.razor(.cs)` | Slide-over plus parked rail |
| `LocationPickerPanel` | `Panels\LocationPickerPanel.razor` | LaGeo041-045 select screens |
| `SelfInitPanel` | `Panels\SelfInitPanel.razor(.cs)` | Self Initiate and Traffic Stop |
| `UnitActivatePanel` | `Panels\UnitActivatePanel.razor(.cs)` | |
| `AssignUnitsPanel` | `Panels\AssignUnitsPanel.razor` (+ `Panels\Assign\PriorityDot.razor`) | PD / FD / EMS / SPT |
| `UnitFunctionPanel` | `Panels\UnitFunctionPanel.razor` | Status / Location Change / Release |
| `CadAlertsPanel` | `Panels\CadAlertsPanel.razor` | |
| `FunctionStubPanel` | `Panels\FunctionStubPanel.razor` | |
| `Themes\Palette.*.xaml`, `Controls.xaml` | `..\LwCad.Web.Shared\wwwroot\lwcad.css` (tokens), `wwwroot\cadsystem.css`, `wwwroot\css\*.css` | |
| `Themes\Icons.xaml` | `..\LwCad.Web.Shared\LwIcons.cs` + `LwIcon.razor` | Inline SVG, same path data |
| `App.xaml.cs` switches | `Services\CadStartup.cs`, `Services\CadSystemOptions.cs` | |
| `ShellWindow` constructor | `Services\CadConsole.cs` | Scoped: the data service and ShellViewModel for each circuit |

## DevExpress components used

| Component | Used for |
| --- | --- |
| DxGrid | The four monitors: Stacked, Active, Delayed and the Unit Monitor (see *The monitors*) |
| DxPopup (via `Panels\PanelPopup.razor`) | Every card panel: Assign, Unit Functions, Location Picker, Activate Unit, CAD Alerts, Function Stub |
| DxDialogProvider / IDialogService.ConfirmAsync | The legacy MessageBox confirmations ("Save record?", "Cancel without creating record?", "Assign Unit(s)/Apparatus Immediately?", the cross-jurisdiction question ...) |
| DxContextMenu | The call and unit row "..." menus, and the Ctrl+W / Alt+W working-call menu |
| DxFlyout + DxCheckBox | Column choosers |
| DxTextBox, DxComboBox, DxMemo, DxSpinEdit, DxCheckBox, DxButton, DxTabs | Panel editors and buttons (DxTabs on Unit Functions) |
| IThemeChangeService | Switches Fluent light / dark with the board's theme toggle |

DevExpress is themed through the `--lw-*` tokens: Fluent's resting surface, text, border, accent and backdrop tokens are mapped onto the CAD palette in `cadsystem.css`.

**Where DevExpress was not used, and why:**
- **Take Call and Self Initiate** are custom slide-overs, not DxPopup. They dock to the right edge, and Take Call parks on a non-modal rail, which a centred modal popup cannot do.
- **Location Picker, the Assign candidates and the CAD Alerts lists** are styled lists:
  - the picker's columns change per select screen, and its keyboard follows the legacy list;
  - the candidates and alerts are cards with badges and wrapping remarks.
- **Agency tabs on Assign** are buttons, as in WPF.

## The monitors

Stacked, Active, Delayed and the Unit Monitor are DevExpress **DxGrid**s (`Cad\CallMonitor.razor`, `Cad\UnitMonitor.razor`).

**The highlight is the call, never a row position.** Several dispatchers work one board, and another console can assign, release, re-order, restack or close the call this operator is working.
- The grid's selected item is whichever call in that monitor has `CadCall.IsSelected` (unit: `CadUnit.IsSelected`), with `KeyFieldName` `EventKey` / `UnitNo`. The grid never selects on its own (`AllowSelectRowByClick` off); a click selects the call through the shell.
- The data services update calls in place by key, so the same call object moves between monitors and the highlight goes with it. When it arrives in another monitor, that grid scrolls it into view.
- The console cursor (Ctrl+S / A / D / U, arrows) starts from the highlighted call in whichever monitor it now sits.
- If a refresh replaces the call object for the same event, the highlight is re-attached to the new object.
- If the highlighted call leaves the board (closed or removed by another console), the status line says so: "Event 2026-0000008 (911 HANG UP) is no longer on the board - it was closed or removed, possibly by another console." The highlight is never moved to a neighbouring call.
- The panel lock still holds a monitor's rows and order.

**Rows and order.** The order is the view model's (priority, then received time); grid sorting and grouping are off. A row's preview is the grid's detail row, driven by `CadCall.IsExpanded` (chevron or Space).

**Columns.**
- `ColumnResizeMode="ColumnsContainer"`: dragging a header edge resizes that column, and the grid scrolls sideways when the columns outgrow the monitor.
- `AllowColumnReorder`: headers can be dragged into a new order.
- `Gridlines.All`: column separators in the header and the rows.
- The chooser keeps the per-list default sets. Each column's `Visible`, `Width` and `VisibleIndex` are bound two-way to `ColumnSpec` / `MonitorLayout`, so **Reset to default** restores visibility, widths **and** order.
- Each dispatcher's layout is saved in the browser's localStorage (`lwcad.layout.<monitor>`) and restored next time.
- The chevron (left) and the row "..." menu (right) are fixed columns, so they stay on screen however wide the columns are dragged.

**Sizing.** The board's CSS-grid sizing is kept around the grids. Grab bars and the chevron work as before: a fitted monitor grows with its rows up to 12, then scrolls, and Delayed collapses to its header. An empty or collapsed monitor renders no grid (no column header), as WPF showed none; its layout is kept and comes back with the rows.

**Keyboard.** The console keys stay the console's. Arrows / Home / End / PgUp / PgDn move the highlight, Space toggles the preview, and Tab moves between monitors and grab bars. The browser module stops these keys reaching the grid's own keyboard navigation, which would otherwise move a cell focus independently of the highlight.

## Keyboard

`wwwroot\js\cadsystem.js` decides in the browser which keys belong to the console, prevents their default (F5 would reload, Ctrl+D would bookmark), and hands them to `CadShell.HandleKey`. That is a port of `ShellWindow.OnPreviewKeyDown`.

| Key | Behaviour |
| --- | --- |
| F1-F9, Shift+F1-F9, Ctrl+F1-F9 | Standard Functions (KYB2). While a panel is open, the panel's own keys (Take Call F3 / F5 / F11 / F12 / Shift+F1-F4, Self Initiate, Activate Unit F3) |
| Ctrl+K, then a letter | Quick Menu (KYB3). Refused while a panel is open |
| Esc | Closes the topmost panel, in the WPF order. With nothing open it locks every list (Lowell). An open drop-down closes first |
| Ctrl+S / A / D / U | Cursor to Stacked / Active / Delayed / Unit Monitor. Ctrl+D opens a collapsed Delayed. Ignored in text fields, refused while a panel is open |
| Ctrl+T / Alt+T | Cursor to the top bar |
| Ctrl+W / Alt+W | Opens the selected call's row menu (the WPF re-map) |
| Arrows, Home, End, PgUp, PgDn | Cursor the focused monitor (selects the row) |
| Space | Toggles the preview of the selected row |
| Tab | Moves between monitors and the grab bars (not from cell to cell inside a monitor's grid). A focused grab bar resizes with the arrow keys, 10px a press |
| Enter | Saves Take Call / Self Initiate from a single-line field; picks in the Location Picker; adds the manual unit on Assign |

## Core changes

All are small and documented in the code.

1. **Confirmations are awaited.** `ShellViewModel.Confirm` (a blocking `Func<string,bool>` that WPF answered with a MessageBox) became `ConfirmAsync` (`Func<string, Task<bool>>`). A Blazor Server circuit cannot block on the user. The questions, their order and what each answer does are unchanged. The flows that ask became async:
   - `TakeCallViewModel.CancelAsync` / `DispatchAsync`;
   - `SelfInitViewModel.CancelAsync` / `SaveAsync`;
   - `AssignUnitsViewModel.AssignAsync`;
   - the "UNIT entered is not active" question in `SelfInitViewModel`, which now runs `AskToActivate` after the check returns "not valid yet", exactly as WPF returned on either answer.

   Each flow guards against a second Esc or save while a question is up, and re-checks that the panel is still open after the answer.
2. **`LegacyLauncher.Launch`** no longer calls `Process.Start`. It goes through `CadHost.LaunchLegacy`, or reports that the program cannot be started from this host. The "already running" process check in `ShellViewModel.LaunchLegacyProgram` runs only when no host hook is set.
3. **`ShellViewModel.LockAllLists`** went from internal to public, so Esc can call it.
4. **The highlighted call leaving the board is reported** (`ShellViewModel`, "the highlight is the call"). When a refresh removes the selected call, the status line says so; WPF cleared the selection silently. A call closed in place (queue Closed) is reported the same way. A refresh that re-creates the same event as a new object keeps the highlight on it. The operator's own actions (assign, unit change, Self Initiate, Take Call) report themselves, so they do not get the message.
5. **`ColumnSpec.Width` is settable, plus `DefaultWidth`**, so a resized column can be reset. The team lead added these; `ResetColumns` puts back visibility and widths.

## Parity

Checked in a browser in demo mode (see Test evidence):
- the three monitors (DxGrid) with counts, ordering, the detail-row preview, and selection following the call (see the two-dispatcher test below);
- durations (shown as hours:minutes and refreshed once a minute, at the user's request, 2026-11-12; WPF showed seconds and ticked every second) and the panel lock;
- board sizing: grab-bar drag (star weights kept), the chevron fit, Delayed collapsed at start and opened by Ctrl+D;
- the column chooser (hide and reset); column resize by dragging, reorder by dragging, Reset to default restoring widths and order, and the layout surviving a reload; agency badges, the police funnel and the All / Available / Overdue filter;
- call and unit row menus, including the Assign / Assist rename;
- the Quick Menu, Standard Functions, F-keys, Ctrl+K chords, Ctrl+S / D, Alt+W and arrow / Space cursoring;
- Esc closing panels and locking the lists;
- Take Call: location validation with the picker, the call code list (Shift+F1), save with "Save record?", then "Assign Unit(s)/Apparatus Immediately?" opening Assign on the EMS tab;
- parking Take Call on its rail (board live) and expanding it (board modal);
- Assign queue and commit; Unit Status from a unit row (next-status default ENR to ARV); CAD Alerts tick-all and acknowledge (badge 3 to 2);
- Self Initiate: busy-unit release, and the unknown-unit question opening Activate Unit;
- Traffic Stop opening; a notes stub (Ctrl+F9); the light / dark theme; the status line; autofocus in every panel.

**Partial, or different from WPF:**
- **Monitors are DxGrids:**
  - the preview is a grid detail row rather than WPF's in-row expander (same content);
  - the column separators, resize and reorder are DevExpress's;
  - columns can now be resized (ColumnsContainer: the monitor scrolls sideways when they outgrow it) and reordered, which WPF could not do;
  - WPF clipped the row on the right instead of scrolling, and its fixed widths could squeeze Location to nothing;
  - the flexible column (Location) has a 100px floor, so a narrow monitor scrolls sideways rather than hiding it.
- **The highlighted call leaving the board** is announced on the status line; WPF dropped the highlight silently.
- **Ctrl+T and Ctrl+W are reserved by browsers** (new tab / close tab) and cannot be intercepted, so **Alt+T and Alt+W** do the same. Ctrl+T / Ctrl+W still work where the browser passes them through.
- **F12** is left to the browser's developer tools while no panel is open. Panels that use F12 get it.
- **Esc with a DevExpress drop-down open** closes the drop-down first. WPF's window handler cancelled the panel.
- **The Ctrl+W / Alt+W menu** opens below the row's ellipsis, aligned to its left edge. WPF hung it from the button's right edge.
- **Take Call:**
  - Location (50) and Other (36) are capped in the setter rather than with `maxlength`;
  - names / plate are upper-cased in the setter, so the caret can jump to the end when you edit mid-word.
- **Self Initiate:**
  - Enter in a closed list does not save (WPF did); only an open list was skipped there, and the server cannot reliably tell whether a DevExpress list is open;
  - F12 opens a lookup on Location only, not on any focused list.
- **Activate Unit / Self Initiate field checks** also run when focus leaves the browser window. Blazor's focus-out does not say where focus went.
- **Assign:** the manual unit box is a DxTextBox without the WPF editable combo's unit-number autocomplete. Enter or Add queues the unit.
- **Pop-out monitors and the monitor "..." options** say "not enabled in this build", as in WPF.
- **Map and Monitor on the top bar** open their notes stub, as in WPF. The separate `LwCadMapping.Web` app is not linked from here.
- **Board proportions and theme** are not remembered between sessions (WPF did not remember them either).

**Two dispatchers on the local development database** (2026-11-12): Development\La.cfg (CHANDLER-NB2\SQL2022, Lowell, a local development copy). Two isolated browser contexts, A and B; A held a call highlighted while B changed the board. After each 5-second refresh:

| Case | What B did | Result in A |
| --- | --- | --- |
| Re-order | Stacked a priority-1 call, which sorted above A's highlighted priority-7 call | The highlight stayed on the same call, now one row lower |
| Move | Assigned unit BOB to A's highlighted stacked call | The call moved Stacked -> Active with the highlight; Ctrl+A put the cursor on it |
| Restack | Released BOB from it (call code restacks) | The call moved Active -> Stacked with the highlight |
| Closed | Stepped the last unit to complete, closing the call | No call highlighted; the status line named the closed event |

Screenshots: `..\Docs\cad-system-highlight-1-selected.png` to `cad-system-highlight-5-closed.png`.

Release Unit and Unit Status (ENR -> ARV -> C/S -> complete) ran against those tables in this test.

**Not verified:**
- **Not exercised in a browser:** Location Change, Send Alert, a Traffic Stop or Self Initiate save, Activate Unit save, splitter keyboard resizing, and the E911 load (F11).

## Test evidence

Screenshots, in demo mode at 1500x900:
- `..\Docs\cad-system-light.png` and `cad-system-dark.png` (the DxGrid monitors)
- `..\Docs\cad-system-takecall.png`
- `..\Docs\cad-system-takecall-parked.png`

The browser scenarios ran in a private headless Chrome (Playwright). The only console error was the favicon 404, since fixed.
