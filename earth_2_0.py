"""Implementation of PLAN.md — 'Where Earth 2.0 Could Hide'.

Run from the directory containing exoplanets.csv (or anywhere; paths anchor on this file).
Outputs:
  chart_v1_base.png        — Phase 3 review render
  chart_v2_annotated.png   — Phase 4 review render
  earth_2_0.png            — Phase 5 final, 1600x1000 @ 200 dpi
  earth_2_0.svg            — Phase 5 final, vector
Stdout includes per-phase verification numbers and a final summary verdict.
"""
from __future__ import annotations

from pathlib import Path

import matplotlib.colors as mcolors
import matplotlib.pyplot as plt
import numpy as np
import pandas as pd
from matplotlib.colors import LinearSegmentedColormap, Normalize
from matplotlib.patches import Rectangle
from matplotlib.ticker import FuncFormatter, LogLocator

HERE = Path(__file__).parent
CSV_PATH = HERE / "exoplanets.csv"
SUN_TEFF = 5778

REQUIRED = ["pl_rade", "pl_orbsmax", "st_rad", "st_teff"]
BOX_INSOL = (0.3, 1.7)
BOX_RADIUS = (0.5, 1.6)
X_LIM = (1e4, 1e-3)   # inverted: high flux on left
Y_LIM = (0.3, 30.0)
TEFF_RANGE = (2500, 8000)  # K — colormap domain


def blackbody_cmap() -> LinearSegmentedColormap:
    """Approximate stellar blackbody locus colors over [2500, 8000] K."""
    lo, hi = TEFF_RANGE
    span = hi - lo
    # (T_eff K, hex) — stops sampled along the standard blackbody locus.
    anchors = [
        (2500, "#ff3800"), (3000, "#ff5d10"), (3500, "#ff7818"),
        (4000, "#ff9239"), (4500, "#ffab6c"), (5000, "#ffc18b"),
        (5500, "#ffd7b0"), (5778, "#fff4ea"),
        (6000, "#fdfdff"), (6500, "#f1f3ff"), (7000, "#dbe2ff"),
        (7500, "#c9d5ff"), (8000, "#bbc9ff"),
    ]
    stops = [((t - lo) / span, c) for t, c in anchors]
    return LinearSegmentedColormap.from_list("blackbody", stops, N=256)


# ---------- Phase 1 ----------
def phase1_load() -> tuple[pd.DataFrame, int, int, float]:
    raw = pd.read_csv(CSV_PATH)
    n_raw = len(raw)

    df = raw.dropna(subset=REQUIRED).copy()
    df["insolation"] = (
        df["st_rad"] ** 2 * (df["st_teff"] / SUN_TEFF) ** 4
    ) / (df["pl_orbsmax"] ** 2)
    n_dropped = n_raw - len(df)

    earth_check = (1.0 ** 2 * (SUN_TEFF / SUN_TEFF) ** 4) / (1.0 ** 2)
    assert abs(earth_check - 1.0) < 1e-9, f"Earth sanity failed: {earth_check}"
    assert (df["insolation"] > 0).all(), "Non-positive insolation values"
    assert len(df) + n_dropped == n_raw, "Row accounting mismatch"

    transit_pct = 100.0 * (raw["discoverymethod"] == "Transit").sum() / n_raw

    print(f"[Phase 1] raw rows           : {n_raw}")
    print(f"[Phase 1] kept               : {len(df)}")
    print(f"[Phase 1] dropped (NaN cols) : {n_dropped}")
    print(
        f"[Phase 1] insolation min/med/max : "
        f"{df['insolation'].min():.3g} / "
        f"{df['insolation'].median():.3g} / "
        f"{df['insolation'].max():.3g}"
    )
    print(f"[Phase 1] Earth sanity (1.0?)    : {earth_check:.6f}")
    print(f"[Phase 1] transit fraction       : {transit_pct:.1f}%")

    return df, n_dropped, n_raw, transit_pct


# ---------- Phase 2 ----------
def phase2_box(df: pd.DataFrame) -> tuple[pd.DataFrame, int, float]:
    box = df[
        df["insolation"].between(*BOX_INSOL)
        & df["pl_rade"].between(*BOX_RADIUS)
    ].copy()
    n_in_box = len(box)
    median_teff = float(box["st_teff"].median()) if n_in_box else float("nan")

    print(f"\n[Phase 2] planets in habitable box : {n_in_box}")
    print(f"[Phase 2] median host T_eff in box  : {median_teff:.0f} K")
    if n_in_box:
        print("[Phase 2] subset:")
        print(
            box[["pl_name", "hostname", "pl_rade", "insolation", "st_teff"]]
            .sort_values("insolation")
            .to_string(index=False)
        )

    if n_in_box == 0 or n_in_box > 100:
        print(
            f"[Phase 2] WARNING: n_in_box={n_in_box} is outside expected range — "
            "verify formula and bounds."
        )
    if not np.isnan(median_teff) and median_teff >= 5000:
        print(
            f"[Phase 2] WARNING: median T_eff {median_teff:.0f} K >= 5000 K — "
            "M-dwarf framing not supported by the data."
        )

    return box, n_in_box, median_teff


# ---------- Phase 3 + 4 (shared rendering) ----------
# Manually tuned label offsets for the 7 box residents (data coords for xytext).
HIGHLIGHT_LABELS = {
    "Gliese 12 b":   dict(xytext=(6.0, 4.5),  ha="center"),
    "K2-3 d":        dict(xytext=(2.2, 6.5),  ha="center"),
    "LP 890-9 c":    dict(xytext=(0.85, 9.0), ha="center"),
    "Kepler-62 f":   dict(xytext=(0.30, 6.5), ha="center"),
    "TRAPPIST-1 f":  dict(xytext=(0.10, 4.5), ha="left"),
    # TRAPPIST-1 d and e are absorbed into the system label below.
}


def render(
    df: pd.DataFrame,
    box_df: pd.DataFrame,
    *,
    n_total: int,
    n_dropped: int,
    n_in_box: int,
    median_teff: float,
    transit_pct: float,
    annotated: bool,
    figsize: tuple[float, float],
) -> plt.Figure:
    fig, ax = plt.subplots(figsize=figsize)

    cmap = blackbody_cmap()
    norm = Normalize(vmin=TEFF_RANGE[0], vmax=TEFF_RANGE[1])
    bulk_alpha = 0.20 if annotated else 0.55
    bulk_size = 14 if annotated else 16

    # Bulk catalog — faded when annotated so the 7 highlighted planets dominate.
    sc = ax.scatter(
        df["insolation"], df["pl_rade"],
        c=df["st_teff"], cmap=cmap, norm=norm,
        s=bulk_size, alpha=bulk_alpha, edgecolors="none", rasterized=True,
    )

    ax.set_xscale("log")
    ax.set_yscale("log")
    ax.set_xlim(*X_LIM)
    ax.set_ylim(*Y_LIM)
    ax.set_xlabel("Insolation flux  (Earth = 1, log; high flux on left)")
    ax.set_ylabel("Planet radius  (Earth radii, log)")
    ax.set_facecolor("#0b0d1a")  # dark sky background — makes star colors sing

    pretty = FuncFormatter(lambda v, _: f"{v:g}")
    ax.xaxis.set_major_locator(LogLocator(base=10))
    ax.yaxis.set_major_locator(LogLocator(base=10))
    ax.xaxis.set_major_formatter(pretty)
    ax.yaxis.set_major_formatter(pretty)
    ax.grid(True, which="major", linewidth=0.25, alpha=0.25, color="white")
    ax.tick_params(colors="white")
    for spine in ax.spines.values():
        spine.set_color("white")
    ax.xaxis.label.set_color("white")
    ax.yaxis.label.set_color("white")

    if not annotated:
        ax.set_title("Phase 3 — base scatter (no annotations)", color="white")
        fig.patch.set_facecolor("#0b0d1a")
        fig.tight_layout()
        return fig

    # Habitable box
    x0, x1 = BOX_INSOL
    y0, y1 = BOX_RADIUS
    ax.add_patch(Rectangle(
        (x0, y0), x1 - x0, y1 - y0,
        facecolor="#7CFC00", alpha=0.08, edgecolor="#7CFC00",
        linestyle="--", linewidth=1.2, zorder=1,
    ))
    ax.text(np.sqrt(x0 * x1), y0 * 0.92, "habitable\n(temperate, rocky)",
            ha="center", va="top", fontsize=7.5, color="#9CFC00",
            style="italic")

    # Highlighted box residents — large, edged, sit on top of the fade.
    ax.scatter(
        box_df["insolation"], box_df["pl_rade"],
        c=box_df["st_teff"], cmap=cmap, norm=norm,
        s=180, alpha=1.0,
        edgecolors="white", linewidths=1.2, zorder=4,
    )

    # TRAPPIST-1 grouping — translucent oval drawn around its three planets.
    trap = box_df[box_df["hostname"] == "TRAPPIST-1"]
    if len(trap) >= 2:
        trap_xs = np.log10(trap["insolation"].values)
        trap_ys = np.log10(trap["pl_rade"].values)
        cx_log, cy_log = trap_xs.mean(), trap_ys.mean()
        rx_log = (trap_xs.max() - trap_xs.min()) / 2 + 0.08
        ry_log = (trap_ys.max() - trap_ys.min()) / 2 + 0.10
        theta = np.linspace(0, 2 * np.pi, 120)
        ellipse_x = 10 ** (cx_log + rx_log * np.cos(theta))
        ellipse_y = 10 ** (cy_log + ry_log * np.sin(theta))
        ax.plot(ellipse_x, ellipse_y, color="#ffaa00", linewidth=1.2,
                linestyle="-", alpha=0.85, zorder=3)
        ax.annotate(
            "TRAPPIST-1\nsystem (d, e, f)",
            xy=(10 ** cx_log, 10 ** (cy_log + ry_log)),
            xytext=(0.55, 12),
            textcoords="data", fontsize=8.5, color="#ffaa00",
            ha="center", va="center", fontweight="bold",
            arrowprops=dict(arrowstyle="->", color="#ffaa00", lw=0.9),
        )

    # Per-planet labels for the non-TRAPPIST residents (and TRAPPIST-1 f only,
    # so the "f" label balances the system callout from above).
    for _, row in box_df.iterrows():
        name = row["pl_name"]
        spec = HIGHLIGHT_LABELS.get(name)
        if spec is None:
            continue
        ax.annotate(
            name,
            xy=(row["insolation"], row["pl_rade"]),
            xytext=spec["xytext"],
            textcoords="data",
            fontsize=8, color="white",
            ha=spec.get("ha", "center"), va="center",
            arrowprops=dict(arrowstyle="-", color="white", lw=0.6, alpha=0.7),
        )

    # Earth + Venus (light markers so they read on the dark background)
    ax.scatter([1.0], [1.0], marker="*", s=260, color="white",
               edgecolors="black", linewidths=0.6, zorder=6)
    ax.annotate("Earth", (1.0, 1.0), xytext=(8, 7),
                textcoords="offset points", fontsize=9.5,
                fontweight="bold", color="white")
    ax.scatter([1.91], [0.95], marker="*", s=180, color="#dddddd",
               edgecolors="black", linewidths=0.5, zorder=6)
    ax.annotate("Venus", (1.91, 0.95), xytext=(8, -12),
                textcoords="offset points", fontsize=9, color="#dddddd")

    # Headline summary chip — replaces the in-plot callout
    ax.text(
        0.985, 0.97,
        f"7 of {n_total} planets are\ntemperate and rocky.\n"
        f"Median host T$_{{\\mathrm{{eff}}}}$ ≈ {median_teff:.0f} K (M-dwarf).",
        transform=ax.transAxes, ha="right", va="top",
        fontsize=9, color="white",
        bbox=dict(boxstyle="round,pad=0.5", facecolor="#1a1d2e",
                  edgecolor="#7CFC00", linewidth=1, alpha=0.9),
    )

    cbar = fig.colorbar(sc, ax=ax, ticks=[2500, 3500, SUN_TEFF, 7500], pad=0.02)
    cbar.set_label(r"Host star T$_{\mathrm{eff}}$ (K) — blackbody color",
                   color="white")
    cbar.ax.set_yticklabels(["2500\n(M)", "3500\n(M)", f"{SUN_TEFF}\n(Sun)",
                              "7500\n(A)"], color="white")
    cbar.outline.set_edgecolor("white")
    cbar.ax.tick_params(colors="white")

    fig.suptitle("Where Earth 2.0 Could Hide", fontsize=14, fontweight="bold",
                 x=0.02, ha="left", y=0.98, color="white")
    ax.set_title(
        f"{n_total} confirmed exoplanets — the 7 temperate, rocky candidates highlighted",
        fontsize=9.5, loc="left", color="#bbbbbb",
    )

    caption = (
        f"{transit_pct:.0f}% of points are transit detections. "
        f"Insolation derived from pl_orbsmax, st_rad, st_teff. "
        f"{n_dropped} planets dropped for missing inputs. "
        "Marker color follows stellar blackbody locus."
    )
    fig.text(0.02, 0.015, caption, fontsize=8, color="#bbbbbb")

    fig.patch.set_facecolor("#0b0d1a")
    fig.tight_layout(rect=(0, 0.04, 1, 0.96))
    return fig


# ---------- main ----------
def main() -> None:
    df, n_dropped, n_raw, transit_pct = phase1_load()
    box_df, n_in_box, median_teff = phase2_box(df)

    common = dict(
        n_total=len(df), n_dropped=n_dropped,
        n_in_box=n_in_box, median_teff=median_teff,
        transit_pct=transit_pct,
    )

    fig_v1 = render(df, box_df, annotated=False, figsize=(10, 6.25), **common)
    fig_v1.savefig(HERE / "chart_v1_base.png", dpi=120,
                   facecolor=fig_v1.get_facecolor())
    plt.close(fig_v1)
    print("\n[Phase 3] wrote chart_v1_base.png")

    fig_v2 = render(df, box_df, annotated=True, figsize=(10, 6.25), **common)
    fig_v2.savefig(HERE / "chart_v2_annotated.png", dpi=120,
                   facecolor=fig_v2.get_facecolor())
    plt.close(fig_v2)
    print("[Phase 4] wrote chart_v2_annotated.png")

    fig_final = render(df, box_df, annotated=True, figsize=(10, 6.25), **common)
    fig_final.savefig(HERE / "earth_2_0.png", dpi=200,
                      facecolor=fig_final.get_facecolor())
    fig_final.savefig(HERE / "earth_2_0.svg",
                      facecolor=fig_final.get_facecolor())
    plt.close(fig_final)
    print("[Phase 5] wrote earth_2_0.png  (2000x1250 @ 200 dpi)")
    print("[Phase 5] wrote earth_2_0.svg")

    verdict = (
        "supports the M-dwarf framing"
        if not np.isnan(median_teff) and median_teff < 5000
        else "does NOT support the M-dwarf framing — revisit story"
    )
    print("\n=== Summary ===")
    print(f"  rows kept       : {len(df)}")
    print(f"  rows dropped    : {n_dropped}")
    print(f"  in habitable box: {n_in_box}")
    if not np.isnan(median_teff):
        print(f"  median T_eff    : {median_teff:.0f} K")
    print(f"  transit_pct     : {transit_pct:.1f}%")
    print(f"  hypothesis      : {verdict}")


if __name__ == "__main__":
    main()
