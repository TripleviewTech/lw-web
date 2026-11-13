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
