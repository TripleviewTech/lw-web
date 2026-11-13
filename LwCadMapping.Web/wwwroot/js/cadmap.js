// LW CAD Mapping - the CadMap component's browser half.
// ArcGIS Maps SDK for JavaScript 5.1 from the CDN (loaded by App.razor with
// <script type="module" src="https://js.arcgis.com/5.1/">); core classes come
// through $arcgis.import, which resolves "@arcgis/core/..." against the CDN build.
// Everything ArcGIS-specific lives here; C# reaches it only through CadMap (IMapView).

const contexts = new Map();   // map id -> context
const focusViews = new Map(); // focus id -> { view, ctx }
let seq = 0;
let modsPromise = null;

const OVERLAY_ORDER = [ // bottom to top
    "Premise_Locations", "Pending_Calls", "Delayed_Calls", "Active_Calls", "Stacked_Calls",
    "Support_Units", "EMS_Units", "Fire_Units", "Police_Units", "Found_Locations",
];
const FEATURE_IDS = ["Police_Sectors", "Fire_Districts", "Hydrants", "Schools"];

function waitFor(test, timeoutMs) {
    return new Promise((resolve, reject) => {
        const start = Date.now();
        (function poll() {
            const v = test();
            if (v) return resolve(v);
            if (Date.now() - start > timeoutMs) return reject(new Error("The ArcGIS Maps SDK did not load from js.arcgis.com"));
            setTimeout(poll, 50);
        })();
    });
}

async function mods() {
    if (!modsPromise) {
        modsPromise = (async () => {
            await waitFor(() => globalThis.$arcgis && globalThis.$arcgis.import, 30000);
            const names = [
                "@arcgis/core/Map.js", "@arcgis/core/views/MapView.js", "@arcgis/core/Graphic.js",
                "@arcgis/core/layers/GraphicsLayer.js", "@arcgis/core/geometry/Extent.js", "@arcgis/core/config.js",
                "@arcgis/core/core/reactiveUtils.js", "@arcgis/core/layers/FeatureLayer.js",
                "@arcgis/core/layers/MapImageLayer.js", "@arcgis/core/layers/VectorTileLayer.js",
                "@arcgis/core/layers/TileLayer.js", "@arcgis/core/geometry/Point.js",
            ];
            const m = await globalThis.$arcgis.import(names);
            const pick = (x) => (x && x.default) ? x.default : x;
            return {
                EsriMap: pick(m[0]), MapView: pick(m[1]), Graphic: pick(m[2]), GraphicsLayer: pick(m[3]),
                Extent: pick(m[4]), esriConfig: pick(m[5]), reactiveUtils: m[6], FeatureLayer: pick(m[7]),
                MapImageLayer: pick(m[8]), VectorTileLayer: pick(m[9]), TileLayer: pick(m[10]), Point: pick(m[11]),
            };
        })();
        modsPromise.catch(() => { modsPromise = null; });
    }
    return modsPromise;
}

// ------------------------------------------------------------------ colours

function token(name, fallback) {
    const v = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
    return v || fallback;
}

function palette() {
    return {
        PD: token("--lw-agency-police", "#0085E6"),
        FD: token("--lw-agency-fire", "#EF4444"),
        EMS: token("--lw-agency-ems", "#0D9488"),
        OTH: token("--lw-agency-other", "#B8860B"),
        danger: token("--lw-danger", "#EF4444"),
        warning: token("--lw-warning", "#D97706"),
        muted: token("--lw-text-muted", "#6B7280"),
        accent: token("--lw-accent", "#0085E6"),
    };
}

const svgUrl = (svg) => "data:image/svg+xml;charset=UTF-8," + encodeURIComponent(svg);
const esc = (s) => String(s ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));

function callColour(g, pal) {
    if (g.layer === "Pending_Calls") return pal.muted;
    if (g.layer === "Delayed_Calls") return pal.warning;
    return pal[g.agency] || pal.PD;
}

function pinSymbol(colour, text, badge, scale) {
    const w = 30, h = 40;
    const b = badge ? `<circle cx="24" cy="7" r="7" fill="#EF4444" stroke="#fff" stroke-width="1.5"/>
        <text x="24" y="10.5" font-family="Segoe UI,Arial" font-size="9.5" font-weight="700" fill="#fff" text-anchor="middle">${esc(badge)}</text>` : "";
    const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}">
        <path d="M15 39C15 39 3 24.5 3 15.5a12 12 0 0 1 24 0C27 24.5 15 39 15 39z" fill="${colour}" stroke="#fff" stroke-width="2"/>
        <circle cx="15" cy="15.5" r="7.5" fill="#fff" fill-opacity=".22"/>
        <text x="15" y="19.5" font-family="Segoe UI,Arial" font-size="11" font-weight="700" fill="#fff" text-anchor="middle">${esc(text)}</text>${b}</svg>`;
    return { type: "picture-marker", url: svgUrl(svg), width: w * 0.8 * scale, height: h * 0.8 * scale, yoffset: h * 0.4 * scale };
}

function dotSymbol(colour, size, ring) {
    return ring
        ? { type: "simple-marker", style: "circle", size: size + 4, color: [0, 0, 0, 0], outline: { color: colour, width: 3 } }
        : { type: "simple-marker", style: "circle", size, color: colour, outline: { color: "#ffffff", width: 1.5 } };
}

function unitSymbol(g, pal, scale) {
    const colour = pal[g.agency] || pal.PD;
    const text = String(g.label || "");
    const w = Math.max(26, 10 + text.length * 7), h = 18;
    const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h + 6}" viewBox="0 0 ${w} ${h + 6}">
        <rect x="1" y="1" width="${w - 2}" height="${h - 2}" rx="4" fill="${colour}" stroke="#fff" stroke-width="1.5"/>
        <path d="M${w / 2 - 4} ${h - 1}L${w / 2} ${h + 5}L${w / 2 + 4} ${h - 1}z" fill="${colour}"/>
        <text x="${w / 2}" y="${h - 5}" font-family="Segoe UI,Arial" font-size="10.5" font-weight="700" fill="#fff" text-anchor="middle">${esc(text)}</text></svg>`;
    return { type: "picture-marker", url: svgUrl(svg), width: w * scale, height: (h + 6) * scale, yoffset: (h + 6) / 2 * scale };
}

function starSymbol(colour, scale) {
    const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="28" height="28" viewBox="0 0 24 24">
        <path d="M12 2l2.9 6.6 7.1.6-5.4 4.7 1.6 7L12 17.3 5.8 20.9l1.6-7L2 9.2l7.1-.6z" fill="${colour}" stroke="#fff" stroke-width="1.4" stroke-linejoin="round"/></svg>`;
    return { type: "picture-marker", url: svgUrl(svg), width: 26 * scale, height: 26 * scale };
}

function stickPinSymbol(scale) {
    const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="22" height="36" viewBox="0 0 22 36">
        <line x1="11" y1="14" x2="11" y2="35" stroke="#5B5B5B" stroke-width="2"/>
        <circle cx="11" cy="9" r="8" fill="#FACC15" stroke="#8A6D00" stroke-width="1.5"/>
        <circle cx="8.5" cy="6.5" r="2.4" fill="#fff" fill-opacity=".7"/></svg>`;
    return { type: "picture-marker", url: svgUrl(svg), width: 18 * scale, height: 30 * scale, yoffset: 15 * scale };
}

function premiseSymbol(colour, scale) {
    const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="22" height="22" viewBox="0 0 22 22">
        <rect x="3" y="3" width="16" height="16" rx="3" transform="rotate(45 11 11)" fill="${colour}" stroke="#fff" stroke-width="1.5"/>
        <text x="11" y="14.5" font-family="Segoe UI,Arial" font-size="9" font-weight="700" fill="#fff" text-anchor="middle">P</text></svg>`;
    return { type: "picture-marker", url: svgUrl(svg), width: 20 * scale, height: 20 * scale };
}

function symbolFor(g, ctx, pal, scale) {
    switch (g.kind) {
        case "call": {
            const colour = g.stack > 1 ? pal.danger : callColour(g, pal);
            if (ctx.markerTheme === "Dots") {
                const size = Math.max(9, 20 - g.priority * 1.4) * scale;
                return dotSymbol(colour, size, g.stack > 1);
            }
            return pinSymbol(colour, g.stack > 1 ? String(g.stack) : String(g.priority), g.stack > 1 ? String(g.stack) : null, scale);
        }
        case "unit":
            return ctx.markerTheme === "Dots"
                ? { type: "simple-marker", style: "square", size: 11 * scale, color: pal[g.agency] || pal.PD, outline: { color: "#fff", width: 1.5 } }
                : unitSymbol(g, pal, scale);
        case "premise": return premiseSymbol(pal.OTH, scale);
        case "found": return starSymbol(pal.accent, scale);
        case "mark": return stickPinSymbol(scale);
        default: return dotSymbol(pal.muted, 10 * scale, false);
    }
}

function labelSymbol(text, colour, yoffset) {
    return {
        type: "text", text: String(text || "").slice(0, 40), color: colour, haloColor: "#ffffff", haloSize: 1.5,
        yoffset, font: { size: 10, family: "sans-serif", weight: "bold" },
    };
}

// ------------------------------------------------------------------ create

export async function createMap(host, dotnet, opts) {
    const m = await mods();
    const id = "map" + (++seq);
    if (opts.apiKey) m.esriConfig.apiKey = opts.apiKey;

    const map = new m.EsriMap({ basemap: opts.basemap ?? null });

    // Operational layers (replacements for the desktop LocalServer .mpk layers).
    const opLayers = (opts.operationalLayers || []).map((d) => {
        const props = { url: d.url, title: d.title, visible: d.visible, ...(d.minScale ? { minScale: d.minScale } : {}) };
        try {
            switch ((d.type || "feature").toLowerCase()) {
                case "map-image": return new m.MapImageLayer(props);
                case "vector-tile": return new m.VectorTileLayer(props);
                case "tile": return new m.TileLayer(props);
                default: return new m.FeatureLayer(props);
            }
        } catch (e) {
            console.warn("Operational layer could not be created", d.title, e);
            return null;
        }
    });
    map.addMany(opLayers.filter(Boolean));

    const features = {};
    for (const fid of FEATURE_IDS) {
        features[fid] = new m.GraphicsLayer({ id: fid, title: fid, visible: false, ...(fid === "Hydrants" ? { minScale: 18100 } : {}) });
    }
    map.addMany(FEATURE_IDS.map((f) => features[f]));

    const routeLayer = new m.GraphicsLayer({ id: "route", title: "Route" });
    map.add(routeLayer);

    const overlays = {};
    for (const lid of OVERLAY_ORDER) overlays[lid] = new m.GraphicsLayer({ id: lid, title: lid });
    map.addMany(OVERLAY_ORDER.map((l) => overlays[l]));

    const labelLayer = new m.GraphicsLayer({ id: "labels", title: "Labels" });
    const selectionLayer = new m.GraphicsLayer({ id: "selection", title: "Selection" });
    map.addMany([labelLayer, selectionLayer]);

    const [xmin, ymin, xmax, ymax] = opts.extent;
    const extent = new m.Extent({ xmin, ymin, xmax, ymax, spatialReference: { wkid: 4326 } });

    const container = document.createElement("div");
    container.className = "cadmap-view";
    host.appendChild(container);

    const view = new m.MapView({
        container, map, extent,
        constraints: { snapToZoom: false, minZoom: 8, maxZoom: 20, rotationEnabled: false },
        // No default UI: DevExpress's preload script wraps appendChild and breaks the 5.1 attribution widget; the page shows the attribution text itself.
        ui: { components: [] },
        popupEnabled: false,
    });

    const ctx = {
        id, m, map, view, host, dotnet, extent, overlays, features, opLayers, routeLayer, labelLayer, selectionLayer,
        graphics: [], layers: [], markerTheme: "Pins", marking: false, callout: null, selected: null,
        coords: { dec: true, deg: false, map: false }, handles: [], apiKey: opts.apiKey, routeUrl: opts.routeUrl,
    };
    contexts.set(id, ctx);

    buildOverlayDom(ctx);
    drawFeatures(ctx, opts.features || {});

    view.on("click", (e) => onClick(ctx, view, e, false));
    view.on("pointer-move", (e) => {
        const p = view.toMap({ x: e.x, y: e.y });
        if (p && p.latitude != null) showCoords(ctx, p.latitude, p.longitude);
    });
    view.on("pointer-leave", () => showCoords(ctx, null, null));

    // Do not hold the page on view.when(): it can resolve well after the first tiles
    // are drawn. Graphics can be added before the view is ready; navigation waits for it.
    ctx.ready = m.reactiveUtils.whenOnce(() => view.ready);
    ctx.handles.push(m.reactiveUtils.watch(() => [view.extent, view.zoom], () => {
        positionCallout(ctx);
        ctx.zoomEl.textContent = "z" + (view.zoom ?? 0).toFixed(1);
    }, { initial: true }));

    return id;
}

function buildOverlayDom(ctx) {
    const coords = document.createElement("div");
    coords.className = "cadmap-coords";
    coords.innerHTML = `
        <div data-line="dec"><span class="k">Lat / Long (Dec)</span><span class="v">-</span></div>
        <div data-line="deg"><span class="k">Lat / Long (Deg)</span><span class="v">-</span></div>
        <div data-line="map"><span class="k">Map Coords</span><span class="v">-</span></div>`;
    ctx.host.appendChild(coords);
    ctx.coordsEl = coords;
    applyCoordFormats(ctx);

    const zoom = document.createElement("div");
    zoom.className = "cadmap-zoom";
    ctx.host.appendChild(zoom);
    ctx.zoomEl = zoom;

    const callout = document.createElement("div");
    callout.className = "cadmap-callout";
    callout.hidden = true;
    ctx.host.appendChild(callout);
    ctx.calloutEl = callout;
    callout.addEventListener("click", (e) => {
        if (e.target.closest("[data-close]")) {
            callout.hidden = true;
            ctx.callout = null;
            ctx.dotnet.invokeMethodAsync("OnCalloutClosed");
        }
    });
}

async function onClick(ctx, view, e, fromFocus) {
    const p = e.mapPoint;
    if (!p) return;
    if (ctx.marking && !fromFocus) {
        ctx.dotnet.invokeMethodAsync("OnMapClick", p.latitude, p.longitude, null, false);
        return;
    }
    let gid = null;
    try {
        const hit = await view.hitTest(e, { include: Object.values(ctx.overlays) });
        const r = hit.results.find((x) => x.graphic && x.graphic.attributes && x.graphic.attributes.gid);
        gid = r ? r.graphic.attributes.gid : null;
    } catch { /* view destroyed */ }
    ctx.dotnet.invokeMethodAsync("OnMapClick", p.latitude, p.longitude, gid, fromFocus);
}

// ------------------------------------------------------------------ drawing

export function draw(id, graphics, layers, markerTheme) {
    const ctx = contexts.get(id);
    if (!ctx) return;
    ctx.graphics = graphics;
    ctx.layers = layers;
    ctx.markerTheme = markerTheme;
    render(ctx);
}

/** Re-applies the colours after a theme change. */
export function refresh(id) {
    const ctx = contexts.get(id);
    if (ctx) { render(ctx); drawSelection(ctx); }
}

function render(ctx) {
    const { m } = ctx;
    const pal = palette();
    const byId = Object.fromEntries(ctx.layers.map((l) => [l.id, l]));
    const perLayer = {};
    const labels = [];

    for (const g of ctx.graphics) {
        const layer = byId[g.layer] || { visible: true, labels: false, fade: 1, size: 1 };
        const scale = layer.size || 1;
        const graphic = new m.Graphic({
            geometry: { type: "point", latitude: g.lat, longitude: g.lon },
            symbol: symbolFor(g, ctx, pal, scale),
            attributes: { gid: g.id },
        });
        (perLayer[g.layer] ||= []).push(graphic);
        if (layer.labels && layer.visible) {
            const dy = g.kind === "call" && ctx.markerTheme === "Pins" ? -14 : g.kind === "unit" ? -14 : -18;
            labels.push(new m.Graphic({
                geometry: { type: "point", latitude: g.lat, longitude: g.lon },
                symbol: labelSymbol(g.label, "#1F2937", dy * scale),
                attributes: { label: g.layer },
            }));
        }
    }

    for (const lid of OVERLAY_ORDER) {
        const gl = ctx.overlays[lid];
        const st = byId[lid];
        gl.removeAll();
        if (perLayer[lid]) gl.addMany(perLayer[lid]);
        gl.visible = st ? st.visible : true;
        gl.opacity = st ? st.fade : 1;
    }
    ctx.labelLayer.removeAll();
    ctx.labelLayer.addMany(labels);

    for (const fid of FEATURE_IDS) {
        const st = byId[fid];
        const fl = ctx.features[fid];
        if (!st) continue;
        fl.visible = st.visible;
        fl.opacity = st.fade;
        for (const g of fl.graphics.toArray()) {
            if (g.attributes && g.attributes.isLabel) g.visible = !!st.labels;
        }
    }
    for (const st of ctx.layers) {
        if (st.opIndex >= 0 && ctx.opLayers[st.opIndex]) {
            ctx.opLayers[st.opIndex].visible = st.visible;
            ctx.opLayers[st.opIndex].opacity = st.fade;
        }
    }
}

function drawFeatures(ctx, f) {
    const { m } = ctx;
    const pal = palette();
    const poly = (layer, areas, colour, dash) => {
        for (const a of areas || []) {
            layer.add(new m.Graphic({
                geometry: { type: "polygon", rings: [a.ring.map((p) => [p[1], p[0]])], spatialReference: { wkid: 4326 } },
                symbol: { type: "simple-fill", color: [0, 0, 0, 0.03], outline: { color: colour, width: 2, style: dash } },
            }));
            layer.add(new m.Graphic({
                geometry: { type: "point", latitude: a.label[0], longitude: a.label[1] },
                symbol: labelSymbol(a.name, colour, 0),
                attributes: { isLabel: true },
            }));
        }
    };
    poly(ctx.features.Police_Sectors, f.sectors, pal.PD, "dash");
    poly(ctx.features.Fire_Districts, f.districts, pal.FD, "short-dot");
    for (const h of f.hydrants || []) {
        ctx.features.Hydrants.add(new m.Graphic({
            geometry: { type: "point", latitude: h.p[0], longitude: h.p[1] },
            symbol: { type: "simple-marker", style: "circle", size: 6, color: pal.FD, outline: { color: "#fff", width: 1 } },
        }));
    }
    for (const s of f.schools || []) {
        ctx.features.Schools.add(new m.Graphic({
            geometry: { type: "point", latitude: s.p[0], longitude: s.p[1] },
            symbol: { type: "simple-marker", style: "square", size: 10, color: pal.accent, outline: { color: "#fff", width: 1.5 } },
        }));
        ctx.features.Schools.add(new m.Graphic({
            geometry: { type: "point", latitude: s.p[0], longitude: s.p[1] },
            symbol: labelSymbol(s.name, pal.accent, -14),
            attributes: { isLabel: true },
        }));
    }
}

// ------------------------------------------------------------------ selection and callout

export function select(id, g, bringIntoView) {
    const ctx = contexts.get(id);
    if (!ctx) return;
    ctx.selected = g ? { lat: g.lat, lon: g.lon } : null;
    drawSelection(ctx);
    if (g && bringIntoView) {
        const p = ctx.view.toScreen(new ctx.m.Point({ latitude: g.lat, longitude: g.lon }));
        const inside = p && p.x > 40 && p.y > 40 && p.x < ctx.view.width - 40 && p.y < ctx.view.height - 40;
        if (!inside) ctx.view.goTo({ center: [g.lon, g.lat] }, { duration: 400 }).catch(() => { });
    }
}

function drawSelection(ctx) {
    const { m } = ctx;
    ctx.selectionLayer.removeAll();
    if (!ctx.selected) return;
    const pal = palette();
    ctx.selectionLayer.add(new m.Graphic({
        geometry: { type: "point", latitude: ctx.selected.lat, longitude: ctx.selected.lon },
        symbol: { type: "simple-marker", style: "circle", size: 34, color: [0, 133, 230, 0.12], outline: { color: pal.accent, width: 2.5 } },
    }));
}

export function setCallout(id, c) {
    const ctx = contexts.get(id);
    if (!ctx) return;
    ctx.callout = c;
    const el = ctx.calloutEl;
    if (!c) { el.hidden = true; return; }
    el.innerHTML = `
        <button type="button" class="close" data-close aria-label="Close the popup" title="Close">&#x2715;</button>
        <div class="title">${esc(c.title)}</div>
        ${c.subtitle ? `<div class="sub">${esc(c.subtitle)}</div>` : ""}
        ${c.address ? `<div class="addr">${esc(c.address)}</div>` : ""}
        ${c.footer ? `<div class="foot">${esc(c.footer)}</div>` : ""}`;
    el.hidden = false;
    positionCallout(ctx);
}

function positionCallout(ctx) {
    const c = ctx.callout;
    if (!c || ctx.calloutEl.hidden) return;
    const p = ctx.view.toScreen(new ctx.m.Point({ latitude: c.lat, longitude: c.lon }));
    if (!p) return;
    const el = ctx.calloutEl;
    const w = el.offsetWidth, h = el.offsetHeight;
    let x = p.x - w / 2, y = p.y - h - 44;
    x = Math.max(8, Math.min(x, ctx.view.width - w - 8));
    if (y < 8) y = p.y + 22;
    el.style.transform = `translate(${Math.round(x)}px, ${Math.round(y)}px)`;
    el.style.visibility = p.x < -50 || p.y < -50 || p.x > ctx.view.width + 50 || p.y > ctx.view.height + 50 ? "hidden" : "visible";
}

// ------------------------------------------------------------------ navigation

export function goTo(id, lat, lon, zoom) {
    const ctx = contexts.get(id);
    if (!ctx) return;
    const target = { center: [lon, lat] };
    if (zoom != null) target.zoom = zoom;
    return ctx.ready.then(() => ctx.view.goTo(target, { duration: 500 })).catch(() => { });
}

export function zoomBy(id, levels) {
    const ctx = contexts.get(id);
    if (!ctx) return;
    return ctx.ready.then(() => ctx.view.goTo({ zoom: ctx.view.zoom + levels }, { duration: 250 })).catch(() => { });
}

export function fullExtent(id) {
    const ctx = contexts.get(id);
    if (!ctx) return;
    return ctx.ready.then(() => ctx.view.goTo(ctx.extent, { duration: 500 })).catch(() => { });
}

export function fitTo(id, points) {
    const ctx = contexts.get(id);
    if (!ctx || !points || !points.length) return;
    if (points.length === 1) return goTo(id, points[0][0], points[0][1], Math.max(ctx.view.zoom, 16));
    const lats = points.map((p) => p[0]), lons = points.map((p) => p[1]);
    const e = new ctx.m.Extent({
        xmin: Math.min(...lons), ymin: Math.min(...lats), xmax: Math.max(...lons), ymax: Math.max(...lats),
        spatialReference: { wkid: 4326 },
    });
    return ctx.ready.then(() => ctx.view.goTo(e.expand(1.5), { duration: 500 })).catch(() => { });
}

export function getViewpoint(id) {
    const ctx = contexts.get(id);
    if (!ctx || !ctx.view.center) return null;
    return { lat: ctx.view.center.latitude, lon: ctx.view.center.longitude, zoom: ctx.view.zoom };
}

export function setBasemap(id, style) {
    const ctx = contexts.get(id);
    if (ctx) ctx.map.basemap = style || null;
}

export function setMarking(id, on) {
    const ctx = contexts.get(id);
    if (!ctx) return;
    ctx.marking = !!on;
    ctx.host.classList.toggle("marking", ctx.marking);
}

// ------------------------------------------------------------------ coordinate box

export function setCoordinateFormats(id, dec, deg, map) {
    const ctx = contexts.get(id);
    if (!ctx) return;
    ctx.coords = { dec, deg, map };
    applyCoordFormats(ctx);
}

function applyCoordFormats(ctx) {
    const c = ctx.coords;
    ctx.coordsEl.querySelector('[data-line="dec"]').hidden = !c.dec;
    ctx.coordsEl.querySelector('[data-line="deg"]').hidden = !c.deg;
    ctx.coordsEl.querySelector('[data-line="map"]').hidden = !c.map;
    ctx.coordsEl.hidden = !(c.dec || c.deg || c.map);
}

function dms(v, pos, neg) {
    const h = v >= 0 ? pos : neg;
    v = Math.abs(v);
    const d = Math.floor(v), mf = (v - d) * 60, mm = Math.floor(mf), s = (mf - mm) * 60;
    return `${d}°${String(mm).padStart(2, "0")}'${s.toFixed(1).padStart(4, "0")}"${h}`;
}

function showCoords(ctx, lat, lon) {
    const set = (k, v) => { ctx.coordsEl.querySelector(`[data-line="${k}"] .v`).textContent = v; };
    if (lat == null) { set("dec", "-"); set("deg", "-"); set("map", "-"); return; }
    set("dec", `${lat.toFixed(6)}, ${lon.toFixed(6)}`);
    set("deg", `${dms(lat, "N", "S")}  ${dms(lon, "E", "W")}`);
    const R = 6378137.0;
    const x = R * lon * Math.PI / 180, y = R * Math.log(Math.tan(Math.PI / 4 + lat * Math.PI / 360));
    const f = (n) => n.toLocaleString("en-US", { minimumFractionDigits: 1, maximumFractionDigits: 1 });
    set("map", `X ${f(x)}  Y ${f(y)}`);
}

// ------------------------------------------------------------------ route

function haversine(a, b) {
    const r = 6371008.8, toR = Math.PI / 180;
    const dLat = (b[0] - a[0]) * toR, dLon = (b[1] - a[1]) * toR;
    const h = Math.sin(dLat / 2) ** 2 + Math.cos(a[0] * toR) * Math.cos(b[0] * toR) * Math.sin(dLon / 2) ** 2;
    return 2 * r * Math.asin(Math.min(1, Math.sqrt(h)));
}

function drawRouteLine(ctx, paths, dashed) {
    const pal = palette();
    ctx.routeLayer.removeAll();
    ctx.routeLayer.add(new ctx.m.Graphic({
        geometry: { type: "polyline", paths, spatialReference: { wkid: 4326 } },
        symbol: { type: "simple-line", color: pal.accent, width: 4, style: dashed ? "dash" : "solid" },
    }));
}

/** Route from -> to: the ArcGIS routing service when a key exists, else a straight line. Returns { miles, km, byService, error }. */
export async function route(id, from, to) {
    const ctx = contexts.get(id);
    if (!ctx) return null;
    if (ctx.apiKey) {
        try {
            const [routeMod, RouteParameters, FeatureSet, Graphic] = await globalThis.$arcgis.import([
                "@arcgis/core/rest/route.js", "@arcgis/core/rest/support/RouteParameters.js",
                "@arcgis/core/rest/support/FeatureSet.js", "@arcgis/core/Graphic.js",
            ]);
            const pick = (x) => (x && x.default) ? x.default : x;
            const RP = pick(RouteParameters), FS = pick(FeatureSet), G = pick(Graphic);
            const stop = (p) => new G({ geometry: { type: "point", latitude: p[0], longitude: p[1], spatialReference: { wkid: 4326 } } });
            const params = new RP({
                apiKey: ctx.apiKey, stops: new FS({ features: [stop(from), stop(to)] }),
                returnDirections: false, outSpatialReference: { wkid: 4326 },
            });
            const res = await routeMod.solve(ctx.routeUrl, params);
            const r = res.routeResults && res.routeResults[0] && res.routeResults[0].route;
            if (r) {
                ctx.routeLayer.removeAll();
                ctx.routeLayer.add(new ctx.m.Graphic({ geometry: r.geometry, symbol: { type: "simple-line", color: palette().accent, width: 4 } }));
                const a = r.attributes || {};
                const miles = a.Total_Miles ?? (a.Total_Kilometers != null ? a.Total_Kilometers / 1.609344 : 0);
                const km = a.Total_Kilometers ?? miles * 1.609344;
                return { miles, km, byService: true, error: null };
            }
        } catch (e) {
            console.warn("Routing service failed - straight line instead", e);
            const metres = haversine(from, to);
            drawRouteLine(ctx, [[[from[1], from[0]], [to[1], to[0]]]], true);
            return { miles: metres / 1609.344, km: metres / 1000, byService: false, error: "The routing service failed (" + (e && e.message || e) + ")." };
        }
    }
    const metres = haversine(from, to);
    drawRouteLine(ctx, [[[from[1], from[0]], [to[1], to[0]]]], true);
    return { miles: metres / 1609.344, km: metres / 1000, byService: false, error: null };
}

export function clearRoute(id) {
    const ctx = contexts.get(id);
    if (ctx) ctx.routeLayer.removeAll();
}

// ------------------------------------------------------------------ snapshot, copy, print

export async function snapshot(id) {
    const ctx = contexts.get(id);
    if (!ctx) return null;
    const shot = await ctx.view.takeScreenshot({ format: "png" });
    return shot.dataUrl;
}

export async function copyToClipboard(id) {
    const ctx = contexts.get(id);
    if (!ctx) return "The map is not ready.";
    const shot = await ctx.view.takeScreenshot({ format: "png" });
    const size = `${shot.data.width} x ${shot.data.height}`;
    try {
        const blob = await (await fetch(shot.dataUrl)).blob();
        await navigator.clipboard.write([new ClipboardItem({ "image/png": blob })]);
        return `Map copied to the clipboard (${size}).`;
    } catch (e) {
        const a = document.createElement("a");
        a.href = shot.dataUrl;
        a.download = "cad-map.png";
        a.click();
        return `The browser refused the clipboard (${e && e.name || e}) - the map was downloaded as cad-map.png (${size}) instead.`;
    }
}

export async function print(id) {
    const ctx = contexts.get(id);
    if (!ctx) return;
    // A WebGL canvas does not print reliably; print a screenshot of it instead.
    const shot = await ctx.view.takeScreenshot({ format: "png" });
    let img = document.getElementById("cadmap-print");
    if (!img) {
        img = document.createElement("img");
        img.id = "cadmap-print";
        img.alt = "CAD map";
        document.body.appendChild(img);
    }
    img.src = shot.dataUrl;
    await img.decode().catch(() => { });
    document.documentElement.classList.add("printing-map");
    const done = () => { document.documentElement.classList.remove("printing-map"); window.removeEventListener("afterprint", done); };
    window.addEventListener("afterprint", done);
    window.print();
    setTimeout(done, 1500);
}

// ------------------------------------------------------------------ focus maps

/** A live close-up sharing the main map's layers. */
export async function createFocus(mainId, el, lat, lon, zoom, dotnet) {
    const ctx = contexts.get(mainId);
    if (!ctx) return null;
    const fid = "focus" + (++seq);
    const view = new ctx.m.MapView({
        container: el, map: ctx.map, center: [lon, lat], zoom,
        constraints: { snapToZoom: false, rotationEnabled: false },
        ui: { components: [] }, popupEnabled: false,
    });
    view.on("click", (e) => {
        onClick(ctx, view, e, true);
        if (dotnet) dotnet.invokeMethodAsync("OnFocusClick");
    });
    focusViews.set(fid, { view, ctx });
    return fid;
}

export function setFocusCentre(fid, lat, lon) {
    const f = focusViews.get(fid);
    if (f) f.view.goTo({ center: [lon, lat] }, { duration: 300 }).catch(() => { });
}

export function destroyFocus(fid) {
    const f = focusViews.get(fid);
    if (!f) return;
    focusViews.delete(fid);
    // A focus view shows the MAIN map (shared layers). MapView.destroy() also destroys its map, which
    // would leave every other view - and any focus map reopened later - with nothing to draw, so
    // detach the shared map first and destroy only the view.
    f.view.map = null;
    f.view.container = null;
    f.view.destroy();
}

export function destroyMap(id) {
    const ctx = contexts.get(id);
    if (!ctx) return;
    contexts.delete(id);
    for (const [fid, f] of focusViews) if (f.ctx === ctx) destroyFocus(fid);
    for (const h of ctx.handles) h.remove();
    ctx.view.destroy();
}

/** True once the SDK has loaded (used by the page to say when js.arcgis.com is unreachable). */
export function sdkLoaded() {
    return !!(globalThis.$arcgis && globalThis.$arcgis.import);
}
