# Result — Where Earth 2.0 Could Hide

Run date: 2026-05-04. Source: `exoplanets.csv` (1,174 rows). Script: `earth_2_0.py`.

| metric | value |
| --- | --- |
| rows kept | 1108 |
| rows dropped (missing pl_rade / pl_orbsmax / st_rad / st_teff) | 66 |
| planets inside habitable box (0.3–1.7 S⊕, 0.5–1.6 R⊕) | 7 |
| median host T_eff inside box | **2850 K** |
| transit detections (whole catalog) | 96.2% |

**Verdict:** the catalog supports the M-dwarf framing. All seven habitable-box residents orbit stars cooler than the Sun (T_eff range 2566–4925 K); five of seven are M-dwarfs. Three of the seven belong to a single system (TRAPPIST-1), so the apparent diversity is even narrower than the count suggests.

**Caveats logged for future runs:**
- Insolation range in the catalog (1.3e-08 to 6.1e+05 S⊕) is wider than DESIGN.md predicted; the chart's xlim clips a small number of extreme imaging discoveries and ultra-hot transits. Acceptable for the habitability story but flagged for honesty.
- Cold-reader story test (Phase 5) was not performed — chart was reviewed by the implementer, not a fresh viewer.
- Final figure size raised from 1600×1000 to 2000×1250 (figsize 10×6.25 @ 200 dpi) after a redesign — the original 8×5 was too cramped to fit the seven labeled candidates plus the TRAPPIST-1 grouping.

**Style update:** the final chart now uses a stellar-blackbody colormap on a dark sky background, fades the 1,101 non-box planets to ~20% alpha, and emphasizes the seven temperate-rocky candidates with large white-edged markers, individual labels, and a TRAPPIST-1 system grouping ellipse. The story is now editorial: "the seven, in context" rather than "here is the catalog."

Outputs: `chart_v1_base.png`, `chart_v2_annotated.png`, `earth_2_0.png`, `earth_2_0.svg`.
