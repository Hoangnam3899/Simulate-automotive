---
name: pcan-python-ui
description: Work on the PCAN/CANoe Python desktop UI, including PySide6/PyQt-Fluent-Widgets screens, UIController flow, ICanoeService boundaries, MockCanoeService behavior, DBC/device UI scope, project specs, and PCAN/ISO-TP integration planning. Use when editing or reviewing files under src/python_ui, Docs/design-specs, Docs/test-specs, or implementing real PEAK-System PCAN behavior.
---

# PCAN Python UI

## ⚠️ MANDATORY PRE-TASK CHECKLIST — NO EXCEPTIONS

**Do not write or edit any code until every applicable step below is done.**
This checklist exists because skipping it has caused repeated rule violations in this project.

```
BEFORE EVERY TASK:
[ ] 1. Read this file (pcan-python-ui SKILL.md) — always first
[ ] 2. Read frontend-ui-engineering SKILL.md   — if task touches any visible UI
[ ] 3. Read api-and-interface-design SKILL.md  — if task changes ICanoeService
[ ] 4. Read Docs/PROJECT_STATUS.md
[ ] 5. Read relevant design spec (Docs/design-specs/ui_design_spec.md)
[ ] 6. Read relevant test spec   (Docs/test-specs/ui_test_spec.md)  — if behavior changes
[ ] 7. Read the specific source files being changed
[ ] 8. Read UI_Frameworks/PyQt-Fluent-Widgets reference — if creating/modifying any widget, style, or QSS

Only after all applicable steps are checked: start implementing.
```

**What "applicable" means:**
- Any change to `src/python_ui/` → steps 1, 4, 7 always required
- Any visible UI change (text, layout, widget, color, style) → add step 2, 8
- Any ICanoeService / interface change → add step 3
- Any behavior change (signals, state machine, recording, connection) → add step 6
- Update `Docs/PROJECT_STATUS.md` after every completed task

**Why this matters — lessons learned:**
- Skipped step 8 → wrote raw CSS instead of using `setCustomStyleSheet` (Fluent API)
- Skipped step 2 → invented custom widget patterns that `qfluentwidgets` already provides
- Skipped step 1 → implemented from memory, missed Non-Negotiable Boundaries
- Skipped step 4/5/6 → changed behavior without updating specs first

---

## Overview

Use this skill to keep PCAN/CANoe UI work grounded in the current repo state. The project is a Python desktop UI with mock data today; real PCAN/CANoe integration must stay behind `ICanoeService` and be verified against PEAK-System documentation before being treated as complete.

---

## Current Product State

- Runtime entry: `src/python_ui/main.py` (imports widgets inside `main()` after `QApplication` + `setTheme`)
- App runs on `MockCanoeService` — real PCAN/CANoe integration **not started**
- UI language: **English** (fully translated from Vietnamese as of last task)
- 5-zone layout implemented, verified runtime, dark theme applied
- Zone card header: **no numbered badge** — English title only, colored per zone
- Device/DBC/CANoe Process fields: UI/mock only — not wired to real driver
- Recording controls (Start/Stop buttons): UI state + timer only — no file I/O
- `signal_tree.py` **does not exist** — Zone 2 uses `SignalMonitor` (checkbox table)
- MockCanoeService: 18 signals across CAN1/CAN2/LIN1

### Current file states (post last rebuild)

| File | Key state |
|------|-----------|
| `main.py` | Deferred imports inside `main()` to ensure QApp+theme before widget load |
| `ui/main_window.py` | Dark QSS on QMainWindow/centralWidget; splitter 280:1100 |
| `ui/components/zone_card.py` | No badge; English title only; color border per zone |
| `ui/components/connection_zone.py` | No legacy path_edit; Disconnect button red via `setCustomStyleSheet` |
| `ui/components/signal_monitor.py` | Checkbox table; value text colored per signal; 8-color palette |
| `ui/components/graph_view.py` | Unix timestamp X-axis; per-signal ViewBox/Y-axis; legend 4-col |
| `ui/components/status_bar.py` | 2 PushButtons (Start green/Stop outline); HH:MM:SS; auto filename |
| `ui/components/system_status_bar.py` | CSS dot colors; buffer % label; English labels |
| `core/interfaces.py` | MockCanoeService generates 18 signals at 10 Hz |

---

## UI Framework Source of Truth

- For **any** Python UI design, layout, component, styling, or interaction change → check `UI_Frameworks/PyQt-Fluent-Widgets` **before** implementing
- Before creating a custom control/style/QSS → search the relevant `qfluentwidgets` component, `examples/`, or `docs/` first
- Use `setCustomStyleSheet(widget, lightQss, darkQss)` for theme-aware custom button colors — **not** `widget.setStyleSheet()`
- Prefer `qfluentwidgets` controls over bare Qt widgets when equivalent exists
- `UI_Frameworks/PyQt-Fluent-Widgets` is **read-only** unless user explicitly asks to modify framework code

---

## Architecture Map

```
main.py  (QApplication → setTheme(DARK) → deferred imports)
  └─ MockCanoeService (core/interfaces.py)
  └─ UIController (controllers/ui_controller.py)
       ├─ Signal: connection_state_changed(str)  → MainWindow
       └─ Signal: data_updated(dict)             → MainWindow

MainWindow (ui/main_window.py)
  ├─ Zone 1 — ConnectionZone        (ui/components/connection_zone.py)
  │     Signals: connect_requested(dbc_path, channel)
  │              disconnect_requested
  │              reload_config_requested
  ├─ Zone 2 — SignalMonitor         (ui/components/signal_monitor.py)
  │     Signals: color_changed(str, QColor) → GraphView.add_signal + update_signal_color
  │              signal_removed(str)        → GraphView.remove_signal
  ├─ Zone 3 — GraphView             (ui/components/graph_view.py)
  │     pyqtgraph PlotWidget, per-signal ViewBox+AxisItem, Unix timestamp X-axis
  ├─ Zone 4 — StatusBar             (ui/components/status_bar.py)
  │     PushButton Start(green)/Stop(outline), timer, mock size/rate, no file I/O
  └─ SystemStatusBar                (ui/components/system_status_bar.py)
        Mock CPU/RAM, real clock, real data-rate counter, buffer ProgressBar
```

Zone colors (border + title):
- Zone 1: `#ef4444`  Zone 2: `#22c55e`  Zone 3: `#3b82f6`  Zone 4: `#8b5cf6`

---

## Signal / Data Flow

```
User action
  → Qt widget signal
  → UIController slot
  → MockCanoeService method
  → _generate_data() thread → _callback (10 Hz)
  → UIController.current_values updated
  → update_timer (100 ms) → data_updated(dict)
  → MainWindow._on_data_updated
      → SignalMonitor.update_values()
      → GraphView.update_data()
      → SystemStatusBar.register_data_received()
      → StatusBar.on_data_received()
```

Connection states: `idle → connecting → connected → idle` (or `→ error → idle`)

---

## Non-Negotiable Boundaries

- `MockCanoeService` only; real integration not implemented
- `combo_device` value is NOT passed to `ICanoeService.connect()` — display only
- `dbc_edit` is display only — does NOT parse DBC or change signal list
- Recording buttons manage UI state/timer ONLY — no file written to disk
- CPU/RAM in SystemStatusBar are `random.randint` — NOT real system metrics
- `connect_service()` blocks the main thread (mock sleep 1.5 s) — must move to QThread for real PCAN
- Making any of the above real requires design-spec update + interface review first

---

## Workflow

1. **Complete the Pre-Task Checklist above — no skipping**
2. Identify the runtime path: widget signal → UIController → service → emitted data → UI update
3. For visible UI: inspect PyQt-Fluent-Widgets reference before implementing any widget/style
4. Keep UI-only changes out of service code; service changes go behind `ICanoeService`
5. For real PCAN/ISO-TP: verify against `Docs/PCAN-ISO-TP-API_UserMan_eng.pdf` and `Docs/Python/` samples
6. Update `Docs/PROJECT_STATUS.md` after every completed task
7. Run `python -m py_compile` on every changed `.py` file before committing
8. Label any unverified hardware behavior `NEEDS_VERIFY`

---

## Skill Combinations

| Task type | Skills to read |
|-----------|---------------|
| Visible UI / layout / styling / text | `pcan-python-ui` + `frontend-ui-engineering` |
| ICanoeService contract change | `pcan-python-ui` + `api-and-interface-design` |
| New/changed behavior | `pcan-python-ui` + `test-driven-development` |
| PEAK-System SDK / protocol work | `pcan-python-ui` + `source-driven-development` |
| Debug connection/timer/signal/graph | `pcan-python-ui` + `debugging-and-error-recovery` |
| Breaking PCAN integration into steps | `pcan-python-ui` + `incremental-implementation` |

---

## Pending / Next Work

| Area | Status |
|------|--------|
| Real PCAN/PCAN-Basic integration | Not started — needs `ICanoeService` concrete impl |
| Real CANoe (Vector) integration | Not started |
| DBC parsing → real signal list | Not started |
| CSV file recording (real I/O) | Not started |
| Connect on background QThread | Not started — current mock blocks UI ~1.5 s |
| Python sample reference | `Docs/Python/07_classic_can_read_write/` (basic CAN); `Docs/Python/01_server_ISO15765-2_normal_addressing/` (ISO-TP) |
