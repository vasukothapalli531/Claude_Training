# Where Earth 2.0 Could Hide

## Goal
Show that despite 1,174 confirmed exoplanets, only a small fraction occupy the temperate, Earth-sized regime — and that those candidates cluster around small, cool stars. The visualization should help the viewer see, in one frame, both the scarcity of Earth analogs and the systematic skew of the search toward M-dwarf hosts.

## Audience
Astronomy students and early-career researchers exploring habitability. Assumed comfort with log axes, stellar effective temperature, and the concept of a habitable zone. Not aimed at the general public.

## Story hypothesis
The set of "potentially habitable" planets in the current catalog is small *and* biased: most candidates sit around cooler stars (lower `st_teff`), because transit surveys preferentially recover small planets in short orbits when the host star is small. Habitability research priorities — atmospheric follow-up, biosignature target lists — are therefore implicitly an M-dwarf research program, and the chart should make that visible rather than implied.

## Visualization choice and why
**Primary chart:** scatter of insolation flux (x, log) vs. planet radius (y, log), with points colored by host-star effective temperature on a perceptually uniform diverging scale (cool reds → hot blues, anchored at the Sun's 5778 K).

A shaded rectangle marks the "temperate rocky" box: roughly 0.3–1.7× Earth insolation on x, 0.5–1.6 R⊕ on y. Earth and Venus are plotted as reference markers.

**Why this encoding:**
- Insolation (not orbital period or semi-major axis) is the physically meaningful x-axis for habitability — it normalizes across stars of different luminosities, so the habitable zone becomes a fixed vertical band rather than a star-dependent diagonal.
- Log–log keeps the hot-Jupiter pile-up from crushing the small-planet region.
- Color-by-`st_teff` is the load-bearing channel: it lets the viewer see the M-dwarf clustering inside the habitable box without a second chart.
- A single panel beats small multiples here — the story is about co-occurrence (small planet *and* temperate *and* cool host), and splitting by host-star bin would hide exactly that.

**Rejected alternatives:**
- Period vs. radius: doesn't normalize across stars; habitable zone is no longer a single band.
- Mass–radius: tells a composition story, not a habitability story.
- Faceting by stellar type: hides the central insight (the clustering itself).

## Key columns used
- `pl_rade` — planet radius (Earth radii), y-axis
- `pl_orbsmax` — semi-major axis (AU), input to insolation
- `st_rad` — stellar radius (solar radii), input to insolation
- `st_teff` — stellar effective temperature (K), input to insolation *and* point color
- `pl_name`, `hostname` — tooltips / labels for the highlighted habitable-box residents

**Derived:** `insolation = (st_rad² · (st_teff / 5778)⁴) / pl_orbsmax²` — flux relative to Earth.

## What we're leaving out
- `pl_bmasse` — mass adds composition nuance but isn't needed for the habitability framing and would force a third visual channel.
- `discoverymethod` — ~96% are Transit; coloring by method would waste the most powerful channel on near-uniform data. Mention in caption instead.
- `disc_year`, `sy_pnum`, `sy_dist`, `ra`, `dec`, `st_mass`, `st_met` — irrelevant to this story.
- `pl_eqt` / `pl_eqt_computed` — equilibrium temperature is a function of insolation and albedo assumptions; using insolation directly is cleaner and avoids the measured-vs-computed inconsistency in this column.
- Error bars — catalog uncertainties vary wildly by method and would clutter the scatter without changing the conclusion. Note the limitation in the caption.
- Planets missing any of the four required inputs — drop silently, report the count in a footnote.

## Layout

```
+----------------------------------------------------------------------+
|  Where Earth 2.0 Could Hide                                          |
|  1,174 confirmed exoplanets, by insolation and size                  |
+----------------------------------------------------------------------+
|                                                                      |
|   100 +                                       . :::..  .             |
|       |                                  . :::::::::: ..             |
|   R   |                              . ..:::::::::::::.. .           |
|   a   |                          . ..:::::::::::::::::::..           |
|   d  10+                       ..::::::::::::::::::::::::..  Jovian  |
|   i   |                      .:::::::::::::::::::::::::::.          |
|   u   |                   ..::::::::::::::::::::::::::.. .          |
|   s   |                .::::::::::::::::::::::::::.                  |
|       |              ..::::::::::::::::::::.. .                      |
|  (R   |           .::::::::::::::::::::.                             |
|   E   |  +----------------+ ..:::::::.   .                           |
|  ar  1 +  | TEMPERATE     |.::::::.                  Earth *         |
|   t   |  | ROCKY BOX     |::::.       Venus *                       |
|   h   |  | (habitable)   |..                                         |
|   )   |  +----------------+                                          |
|       |    o o o   <-- mostly red points (cool M-dwarf hosts)        |
|       |                                                              |
|   0.5 +-----+--------+--------+---------+---------+---------+-----   |
|      1000   100      10        1        0.1      0.01     0.001     |
|                       Insolation (Earth = 1, log)                    |
|                                                                      |
|  Color: host star T_eff   [3000 K red] ----- [5778 K white] ----- [7500 K blue]
|                                                                      |
|  Caption: 96% of points are transit detections. Insolation derived   |
|  from pl_orbsmax, st_rad, st_teff. N planets dropped for missing     |
|  inputs: TBD.                                                        |
+----------------------------------------------------------------------+
```

**Annotations to add in the final render:**
- Label Earth and Venus as filled stars at their known coordinates.
- Outline the temperate-rocky box with a thin dashed border; light fill at ~10% opacity.
- A short callout pointing into the box: "~N planets here, median host T_eff ≈ X K" — fill in once computed.
