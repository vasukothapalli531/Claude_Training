# Implementation Plan — "Where Earth 2.0 Could Hide"

Companion to `DESIGN.md`. Five phases, each with a concrete deliverable and a falsifiable verification step. A phase is not "done" until its verification passes.

---

## Phase 1 — Data load and derived insolation

**Work**
- Load `exoplanets.csv` into a DataFrame.
- Drop rows missing any of `pl_rade`, `pl_orbsmax`, `st_rad`, `st_teff`. Record the dropped count.
- Compute `insolation = (st_rad**2 * (st_teff / 5778)**4) / pl_orbsmax**2`.

**Deliverable**
- A clean DataFrame with columns `pl_name`, `hostname`, `pl_rade`, `st_teff`, `insolation` (plus the four inputs for traceability).
- An integer `n_dropped` for the caption footnote.

**Verification**
- `df` has no NaN in the five required columns.
- Earth-equivalent sanity check: a synthetic row with `st_rad=1`, `st_teff=5778`, `pl_orbsmax=1` produces `insolation == 1.0` (within 1e-9).
- `len(df) + n_dropped == 1174` (matches source row count).
- `insolation` is strictly positive everywhere; print min/median/max and confirm the range spans roughly 1e-3 to 1e+4 (catalog is dominated by hot, close-in transit detections).

---

## Phase 2 — Habitable-box subset and summary stats

**Work**
- Define the box: `0.3 <= insolation <= 1.7` AND `0.5 <= pl_rade <= 1.6`.
- Extract the subset; compute count and median host `st_teff`.
- Print the subset (`pl_name`, `hostname`, `pl_rade`, `insolation`, `st_teff`) for human review.

**Deliverable**
- `box_df` (the subset).
- Two scalars for the in-chart callout: `n_in_box`, `median_teff_in_box`.

**Verification**
- `n_in_box` is small (expected single or low-double digits); if it's zero or > 100, the box bounds or the insolation formula are wrong — stop and recheck.
- Spot-check at least one well-known temperate planet (e.g. a TRAPPIST-1 or Kepler-1649 entry, if present in the catalog) actually lands inside the box.
- Median host `st_teff` of the subset is below 5000 K — if it isn't, the M-dwarf-clustering hypothesis is not supported by this catalog and the story needs revisiting before we draw it.

---

## Phase 3 — Base scatter (no annotations yet)

**Work**
- Plot `insolation` (x, log, **inverted** so high flux is on the left to match the layout sketch in DESIGN.md) vs. `pl_rade` (y, log).
- Color by `st_teff` using a perceptually uniform diverging colormap (e.g. `coolwarm` reversed: cool→red, hot→blue), with the norm centered on 5778 K.
- Set point alpha ~0.5 to handle overplot in the Jovian cluster.
- Axis ranges: x roughly 1e-3 to 1e+4, y roughly 0.3 to 30.

**Deliverable**
- A saved PNG `chart_v1_base.png` showing the raw scatter with axes, no annotations.

**Verification**
- Visual: the Jovian cluster (R ≥ 10) sits in the upper portion; the rocky/temperate region is sparsely populated in the lower-middle. If it doesn't, axis orientation or units are wrong.
- Color sanity: pick three points by hand — the coolest-host point should be red, hottest blue, near-solar (~5778 K) near white.
- Axes are log on both dimensions and tick labels are not in scientific-notation soup (format as `0.01`, `1`, `100`, etc.).

---

## Phase 4 — Annotations and reference markers

**Work**
- Draw the temperate-rocky box (dashed outline, ~10% fill).
- Plot Earth (`insolation=1`, `pl_rade=1`) and Venus (`insolation≈1.91`, `pl_rade≈0.95`) as filled star markers with text labels.
- Add the callout pointing into the box: `"~{n_in_box} planets here, median host T_eff ≈ {median_teff_in_box:.0f} K"`.
- Add the colorbar for `st_teff` with anchors at 3000 / 5778 / 7500 K.
- Title: "Where Earth 2.0 Could Hide". Subtitle: "{len(df)} confirmed exoplanets, by insolation and size".

**Deliverable**
- `chart_v2_annotated.png`.

**Verification**
- Earth marker sits exactly at (1, 1); Venus marker sits inside or just to the left of the box (higher insolation than Earth).
- The callout text is readable, doesn't overlap points or the box border, and the leader line points unambiguously into the box.
- Colorbar is present, labeled "Host star T_eff (K)", and visually matches the points.
- Title and subtitle render without truncation at 1200×800 px.

---

## Phase 5 — Caption, validation, and final export

**Work**
- Add caption text below the plot:
  > "{transit_pct}% of points are transit detections. Insolation derived from pl_orbsmax, st_rad, st_teff. {n_dropped} planets dropped for missing inputs."
- Compute `transit_pct` from the original DataFrame (expected ~96%).
- Export final figure as `earth_2_0.png` (1600×1000, 200 dpi) and `earth_2_0.svg`.

**Deliverable**
- `earth_2_0.png` and `earth_2_0.svg`.
- A 3–5 line README/log entry summarizing the result: `n_in_box`, `median_teff_in_box`, `n_dropped`, `transit_pct`, and a one-sentence verdict on whether the M-dwarf-clustering hypothesis held.

**Verification**
- **Story test:** show the chart to someone who hasn't read DESIGN.md and ask, "What do you see?" If they identify (a) most planets are big and hot, and (b) the few temperate small ones are around cooler stars, the chart works. If they say neither, return to Phase 4 and strengthen annotations.
- **Numerical sanity:** caption numbers (`n_dropped`, `transit_pct`) match what was printed in Phases 1 and 3.
- **File integrity:** SVG opens in a browser; PNG is not blank and not over 2 MB.
- **Hypothesis check:** if `median_teff_in_box >= 5000 K`, write the README entry honestly — the catalog does *not* support the M-dwarf framing, and the chart should be re-titled or the story revisited rather than shipped under a misleading headline.

---

## Out of scope for this plan
- Interactive tooltips / hover labels (would require Plotly or Bokeh; static figure is sufficient for the stated audience).
- Error bars on `pl_rade` or `insolation` (DESIGN.md §"What we're leaving out").
- Per-method breakdowns or small multiples (DESIGN.md §"Rejected alternatives").
- Updates as new planets are confirmed — this is a one-shot snapshot of the supplied CSV.
