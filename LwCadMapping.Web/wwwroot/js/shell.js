// LW CAD Mapping - window-level helpers: keyboard shortcuts and the theme.

let handler = null;

const SHORTCUTS = [
    { key: "k", ctrl: true, name: "search" },
    { key: "m", ctrl: true, name: "mark" },
    { key: "g", ctrl: true, name: "gps" },
    { key: "r", ctrl: true, name: "reset" },
    { key: "b", ctrl: true, name: "focusPanel" },
    { key: "l", ctrl: true, name: "controlsPanel" },
    { key: "c", ctrl: true, shift: true, name: "copy" },
    { key: "F1", name: "about" },
    { key: "Escape", name: "escape" },
];

/** Ctrl+K, Ctrl+M, Ctrl+G, Ctrl+R, Ctrl+B, Ctrl+L, Ctrl+Shift+C, F1 and Esc - as the mock-up's KeyBindings. */
export function registerShortcuts(dotnet) {
    unregisterShortcuts();
    handler = (e) => {
        const key = e.key.length === 1 ? e.key.toLowerCase() : e.key;
        const s = SHORTCUTS.find((x) => x.key === key && !!x.ctrl === (e.ctrlKey || e.metaKey) && !!x.shift === e.shiftKey && !e.altKey);
        if (!s) return;
        // Esc inside a dropdown or an editor closes that first.
        if (s.name === "escape" && document.querySelector(".dxbl-dropdown, .dxbl-popup-root .dxbl-dropdown-dialog")) return;
        e.preventDefault();
        e.stopPropagation();
        if (s.name === "search") {
            const input = document.querySelector("#lw-search input, input#lw-search");
            if (input) { input.focus(); input.select(); }
            return;
        }
        dotnet.invokeMethodAsync("OnShortcut", s.name);
    };
    window.addEventListener("keydown", handler, true);
}

export function unregisterShortcuts() {
    if (handler) window.removeEventListener("keydown", handler, true);
    handler = null;
}

export function setTheme(dark) {
    document.documentElement.setAttribute("data-theme", dark ? "dark" : "light");
}

export function prefersDark() {
    return window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches;
}

// Splitter pane sizes, kept in this browser only (Home.razor, DxSplitter).
export function loadLayout(key) {
    try { return localStorage.getItem("lwcadmap.layout." + key); } catch { return null; }
}

export function saveLayout(key, value) {
    try { localStorage.setItem("lwcadmap.layout." + key, value); } catch { }
}
// The rendered size of a splitter pane, [width, height], or null.
export function paneSize(selector) {
    const el = document.querySelector(selector);
    if (!el) return null;
    const r = el.getBoundingClientRect();
    return [r.width, r.height];
}
// The width the Focus Maps section needs so GPS Tracking shows whole rows: per column the widest
// content (a fixed track never shrinks below its size), the gaps and padding, the list's scroll bar
// and the card border - and at least what the two section headers need. null before it is shown.
export function focusPanelWidth() {
    const panel = document.querySelector(".focus-panel");
    if (!panel) return null;
    const border = panel.offsetWidth - panel.clientWidth;
    let need = 0;

    const rows = [...panel.querySelectorAll(".gps-row")];
    if (rows.length) {
        const cs = getComputedStyle(rows[0]);
        const gap = parseFloat(cs.columnGap) || 0;
        const pad = parseFloat(cs.paddingLeft) + parseFloat(cs.paddingRight);
        const tracks = cs.gridTemplateColumns.split(" ").map(parseFloat);
        const declared = ["12px", "56px", "1fr", "48px", "62px"];   // .gps-row in app.css
        const cols = declared.map((d, i) => d.endsWith("fr") ? 0 : (tracks[i] || 0));
        for (const r of rows) {
            [...r.children].forEach((c, i) => { cols[i] = Math.max(cols[i] || 0, c.scrollWidth); });
        }
        const list = panel.querySelector(".gps-list");
        const scrollbar = list ? list.offsetWidth - list.clientWidth : 0;
        need = cols.reduce((a, b) => a + b, 0) + gap * (cols.length - 1) + pad + scrollbar;
    }

    for (const head of panel.querySelectorAll(".panel-head, .gps-head")) {
        const s = getComputedStyle(head);
        const kids = [...head.children].filter(k => !k.classList.contains("spacer"));
        const w = kids.reduce((a, k) => a + k.getBoundingClientRect().width, 0)
            + (parseFloat(s.columnGap) || 0) * Math.max(0, kids.length - 1)
            + parseFloat(s.paddingLeft) + parseFloat(s.paddingRight);
        need = Math.max(need, w);
    }

    return need > 0 ? need + border + 2 : null;
}