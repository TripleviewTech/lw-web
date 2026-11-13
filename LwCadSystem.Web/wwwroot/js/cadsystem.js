// LW CAD System - the browser half of the console.
//
// The WPF ShellWindow handled keys on PreviewKeyDown, before any control saw
// them. A browser has to decide synchronously whether to swallow a key (F5 would
// reload the page, Ctrl+D would bookmark it), so this module decides which keys
// belong to the console, prevents their default, and hands them to .NET, where
// ShellViewModel and the panels act on them exactly as the WPF handlers did.
// It also runs the board's grab bars (drag and arrow keys), focus, scrolling,
// the clipboard and the theme attribute.

let dotnet = null;
let state = { modal: false, confirm: false };
let awaitingChord = false;

const navKeys = new Set(["ArrowUp", "ArrowDown", "Home", "End", "PageUp", "PageDown", " "]);
const ctrlKeys = new Set(["s", "a", "d", "u", "t", "w"]);

function isTextEntry(el) {
    if (!el) return false;
    if (el.isContentEditable) return true;
    const tag = el.tagName;
    if (tag === "TEXTAREA") return true;
    if (tag !== "INPUT") return false;
    const type = (el.getAttribute("type") || "text").toLowerCase();
    return !["checkbox", "radio", "button", "submit", "reset", "range", "color", "file"].includes(type);
}

function send(message) {
    if (!dotnet) return;
    dotnet.invokeMethodAsync("OnConsoleKey", message).catch(() => { });
}

function onKeyDown(e) {
    // The confirmation dialog owns the keyboard while it is up (Enter = Yes, Esc = No).
    if (state.confirm) return;

    const key = e.key;
    if (key === "Control" || key === "Shift" || key === "Alt" || key === "Meta") return;

    const ctrl = e.ctrlKey || e.metaKey;
    const lower = key.length === 1 ? key.toLowerCase() : key;
    const active = document.activeElement;
    const inText = isTextEntry(active);

    // Ctrl+K, then the Quick Menu letter. Holding Ctrl abandons the chord.
    if (awaitingChord && !ctrl && !e.altKey && /^[a-z]$/.test(lower)) {
        awaitingChord = false;
        e.preventDefault();
        send({ kind: "chord", key: lower.toUpperCase() });
        return;
    }

    if (ctrl && !e.altKey && !e.shiftKey && lower === "k") {
        awaitingChord = !state.modal;
        e.preventDefault();
        send({ kind: "ctrlK" });
        return;
    }

    const fkey = /^F([1-9]|1[0-2])$/.exec(key);
    if (fkey) {
        awaitingChord = false;
        if (e.altKey) return;                               // Alt+F4 and friends stay the browser's
        if (key === "F12" && !state.modal) return;          // developer tools, unless a panel wants F12
        e.preventDefault();
        send({ kind: "fkey", number: parseInt(fkey[1], 10), shift: e.shiftKey, ctrl: ctrl, inText: inText });
        return;
    }

    if (key === "Escape") {
        awaitingChord = false;
        // An open drop-down closes on Esc first, as the operator expects.
        if (active && active.getAttribute && active.getAttribute("aria-expanded") === "true") return;
        e.preventDefault();
        send({ kind: "esc" });
        return;
    }

    // The legacy CadMain Ctrl functions. Ctrl+T and Ctrl+W are reserved by every
    // browser (new tab / close tab), so Alt+T and Alt+W are offered as well.
    const altFallback = e.altKey && !ctrl && !e.shiftKey && (lower === "t" || lower === "w");
    if ((ctrl && !e.altKey && !e.shiftKey && ctrlKeys.has(lower)) || altFallback) {
        awaitingChord = false;
        if (inText) return;                                 // Ctrl+A is select-all in a text field
        e.preventDefault();
        send({ kind: "ctrl", key: lower.toUpperCase() });
        return;
    }

    // Cursoring a monitor.
    const list = active && active.closest ? active.closest("[data-lw-list]") : null;

    // Tab moves between the monitors and the grab bars, as the WPF ListBoxes did -
    // not from cell to cell inside a monitor's DxGrid.
    if (list && key === "Tab" && !ctrl && !e.altKey && !state.modal) {
        e.preventDefault();
        e.stopPropagation();
        tabFrom(list, e.shiftKey);
        return;
    }

    if (list && !inText && !ctrl && !e.altKey && navKeys.has(key)) {
        // The monitors are DxGrids, whose own keyboard navigation would also act
        // on these keys; the console's cursor is the highlighted call, so the
        // grid does not see them.
        e.preventDefault();
        e.stopPropagation();
        const rowHeight = 27;
        const page = Math.max(1, Math.floor(list.clientHeight / rowHeight) - 1);
        send({ kind: "nav", list: list.getAttribute("data-lw-list"), key: key === " " ? "Space" : key, page: page });
        return;
    }

    // Enter on a monitor row opens nothing in WPF; leave it alone.
    awaitingChord = false;
}

// The board's tab stops with each monitor counted once (its list wrapper), in
// document order; focus moves to the one after (or before) the given monitor.
const TABBABLE = "a[href], button:not([disabled]), input:not([disabled]):not([type=hidden]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex='-1'])";

function tabFrom(list, back) {
    const stops = Array.from(document.querySelectorAll(TABBABLE + ", [data-lw-list]")).filter(el => {
        if (el.closest("[inert]") || el.offsetParent === null) return false;
        const owner = el.closest("[data-lw-list]");
        return owner === null || owner === el;
    });
    const i = stops.indexOf(list);
    if (i < 0) return;
    const next = stops[(i + (back ? -1 : 1) + stops.length) % stops.length];
    if (next) next.focus();
}

// ------------------------------------------------------------ grab bars

function tracks(container, axis) {
    const css = getComputedStyle(container)[axis === "rows" ? "gridTemplateRows" : "gridTemplateColumns"];
    return css.split(" ").map(t => parseFloat(t));
}

function setTracks(container, axis, values) {
    container.style[axis === "rows" ? "gridTemplateRows" : "gridTemplateColumns"] = values.map(v => v + "px").join(" ");
}

// A bar sits in its own grid track between the two it resizes.
function barInfo(bar) {
    const container = bar.parentElement;
    const axis = bar.getAttribute("data-lw-split-axis") || "rows";
    const children = Array.from(container.children).filter(c => getComputedStyle(c).display !== "none");
    const index = children.indexOf(bar);
    const before = children[index - 1];
    const after = children[index + 1];
    return {
        container, axis, index, before, after,
        minBefore: parseFloat(before?.getAttribute("data-lw-min") || "0"),
        minAfter: parseFloat(after?.getAttribute("data-lw-min") || "0")
    };
}

function resize(info, start, delta) {
    const total = start[info.index - 1] + start[info.index + 1];
    let a = start[info.index - 1] + delta;
    a = Math.max(info.minBefore, Math.min(a, total - info.minAfter));
    const b = total - a;
    const values = start.slice();
    values[info.index - 1] = a;
    values[info.index + 1] = b;
    setTracks(info.container, info.axis, values);
    return [a, b];
}

function commit(bar, sizes) {
    if (!dotnet) return;
    dotnet.invokeMethodAsync("OnSplitterMoved", bar.getAttribute("data-lw-split"), sizes[0], sizes[1]).catch(() => { });
}

function onPointerDown(e) {
    const bar = e.target.closest ? e.target.closest("[data-lw-split]") : null;
    if (!bar || e.button !== 0) return;
    e.preventDefault();

    const info = barInfo(bar);
    if (!info.before || !info.after) return;

    const start = tracks(info.container, info.axis);
    const origin = info.axis === "rows" ? e.clientY : e.clientX;
    let sizes = [start[info.index - 1], start[info.index + 1]];

    bar.setPointerCapture(e.pointerId);
    bar.classList.add("dragging");

    const move = ev => {
        const now = info.axis === "rows" ? ev.clientY : ev.clientX;
        sizes = resize(info, start, now - origin);
    };
    const up = () => {
        bar.releasePointerCapture(e.pointerId);
        bar.classList.remove("dragging");
        bar.removeEventListener("pointermove", move);
        bar.removeEventListener("pointerup", up);
        bar.removeEventListener("pointercancel", up);
        commit(bar, sizes);
    };

    bar.addEventListener("pointermove", move);
    bar.addEventListener("pointerup", up);
    bar.addEventListener("pointercancel", up);
}

// A focused grab bar resizes from the arrow keys, as a WPF GridSplitter does (10px a press).
function onBarKey(e) {
    const bar = e.target.closest ? e.target.closest("[data-lw-split]") : null;
    if (!bar || state.modal) return;
    const info = barInfo(bar);
    const back = info.axis === "rows" ? "ArrowUp" : "ArrowLeft";
    const forward = info.axis === "rows" ? "ArrowDown" : "ArrowRight";
    if (e.key !== back && e.key !== forward) return;
    e.preventDefault();
    e.stopPropagation();
    const start = tracks(info.container, info.axis);
    commit(bar, resize(info, start, e.key === back ? -10 : 10));
}

// ------------------------------------------------------------ column resize

// ------------------------------------------------------------ exports

export function init(ref) {
    dotnet = ref;
    document.addEventListener("keydown", onBarKey, true);
    document.addEventListener("keydown", onKeyDown, true);
    document.addEventListener("pointerdown", onPointerDown, true);
}

export function dispose() {
    document.removeEventListener("keydown", onBarKey, true);
    document.removeEventListener("keydown", onKeyDown, true);
    document.removeEventListener("pointerdown", onPointerDown, true);
    dotnet = null;
}

export function setState(next) {
    state = Object.assign({}, state, next);
    if (state.modal) awaitingChord = false;
}

export function setTheme(dark) {
    document.documentElement.setAttribute("data-theme", dark ? "dark" : "light");
}

export function focusElement(selector) {
    const el = document.querySelector(selector);
    if (!el) return false;
    el.focus({ preventScroll: false });
    return true;
}

// Puts the cursor in a panel that has just come up (the WPF FocusLocation /
// FocusUnit / FocusList helpers). The panel marks the field with the attribute:
// data-lw-autofocus on opening, data-lw-refocus when a panel over it closes.
// DevExpress popups render after an animation, so this retries for a moment.
function findTarget(root, attr) {
    const marked = root.querySelector("[" + attr + "]");
    if (marked) {
        if (marked.matches("input, textarea, button, [tabindex]")) return marked;
        const inner = marked.querySelector("input:not([type=hidden]), textarea, button, [tabindex]");
        if (inner) return inner;
    }
    return null;
}

export function focusFirst(selector, attr) {
    let tries = 0;
    const attempt = () => {
        const root = document.querySelector(selector);
        const target = root && (findTarget(root, attr || "data-lw-autofocus") || findTarget(root, "data-lw-autofocus")
            || root.querySelector("input:not([disabled]):not([type=hidden]), textarea:not([disabled]), button:not([disabled])"));
        if (target && target.offsetParent !== null) {
            target.focus();
            if (target.select && target.tagName === "INPUT") target.select();
            // A DxPopup puts the focus on its first focusable element when its show
            // animation ends, which can be after this. Take it back while the focus
            // is still somewhere else inside the same panel (never from elsewhere).
            for (const delay of [200, 450, 800]) {
                setTimeout(() => {
                    const now = document.activeElement;
                    if (now !== target && document.body.contains(target) && (root.contains(now) || now === document.body)) {
                        target.focus();
                    }
                }, delay);
            }
            return;
        }
        if (++tries < 20) setTimeout(attempt, 50);
    };
    attempt();
}

export function reveal(id) {
    const el = document.getElementById(id);
    if (el) el.scrollIntoView({ block: "nearest" });
}

export function rect(selector) {
    const el = document.querySelector(selector);
    if (!el) return null;
    const r = el.getBoundingClientRect();
    return { left: r.left, top: r.top, right: r.right, bottom: r.bottom, width: r.width, height: r.height };
}

export async function copyText(text) {
    try {
        await navigator.clipboard.writeText(text);
        return true;
    } catch {
        return false;
    }
}

// A dispatcher's monitor column layout, kept in this browser only.
export function loadLayout(key) {
    try { return localStorage.getItem(key); } catch { return null; }
}

export function saveLayout(key, json) {
    try { localStorage.setItem(key, json); } catch { }
}
