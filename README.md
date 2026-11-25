# Saturday Studio – S2 (Salih Gulbay)

A small exploratory VR apartment made with **Meta Building Blocks** for **Unity 6000.6** (URP).

## Open
1. Clone this repository and add the folder in Unity Hub (Unity **6000.6.4f1**; add *Android Build Support* to build for Quest).
2. Open `Assets/SaturdayStudio/Scenes/SaturdayStudio.unity` and press Play.

## In VR (Meta Quest 3)
- **Move:** left thumbstick · **Turn:** right thumbstick
- **Grab:** hold the grip button (or pinch) with your hand on the object
- **Distance grab:** point at an object further away, then grip / pinch to pull it to your hand
- **Make the bed:** point the ray at the bed and press the trigger / pinch
- **Build to the headset:** File → Build Profiles → Meta Quest / Android → Build and Run (headset connected by USB, Developer Mode on)

## Without a headset (desktop test mode, starts automatically in the Editor)
WASD walk · Shift run · mouse look · E or click: pick up / drop / use · Q or right-click: throw · scroll: hold distance · Esc: free the cursor

## Two minds: VS1 ADHD · VS2 OCD
A **start menu** stands in front of you: choose **VS1 · ADHD** or **VS2 · OCD**, a level (**mild / moderate / severe**), then **Start**
(VR: point the ray and press the trigger, or touch a button · desktop: aim + E). Same room, same four chores, only the mind changes.
The **wall clock** in the kitchen shows the time (10:00 → 13:00, about 12 real minutes); the **TV** shows the to-do list with progress bars
and the controls for the chosen mind. No text pops up in front of your eyes. At 13:00 the doorbell rings, the TV shows where the time went,
and the menu returns to play again.

| | VS1 · ADHD – attention drifts away | VS2 · OCD – attention gets stuck |
|---|---|---|
| What happens | Slow start on boring dishes · zone-outs (blur + grey, clock ×4, can't grab) · other chores glow and call · phone buzzes and steals 15–30 min · washer beep is easy to miss; laundry left 50 min must be rewashed | Dirty things make the hands feel contaminated, and it spreads · washing hands calms only briefly · doubt about stove / door / washer (certainty fades after 20 s) · crooked things pull at you · dishes "still don't feel clean" |
| VR controls | **A** ×3: snap back · raise **left wrist** to your eyes: watch | Hands under the **tap**: wash (the dirt fades off your hands) · hand on **stove knobs / door / washer door**: check · hand next to a crooked object: straighten · hold **B**: resist · **B twice**: calm down (two slow breaths) |
| Desktop keys | **R** ×3: snap back · hold **T**: watch | **E** at tap / stove / door / washer · **G**: straighten · hold **F**: resist · **F twice**: calm down |

Code: `Assets/SaturdayStudio/Scripts/Minds/`.

## Chores
- Carry the dirty dishes to the sink to wash them
- Make the bed
- Put the dirty clothes from the laundry basket into the washing machine; after the wash they come out on top of the machine, wet
- Hang the wet clothes on the drying rack by the window; they dry after a few seconds
- Put the clean dishes in the drying rack next to the sink
- Clean the floor: hold the cleaning rag (by the sink) on each stain (ADHD only; the OCD morning has no stains)

The note on the wall tracks progress. All grabbable objects are custom procedural meshes (`Assets/SaturdayStudio/Meshes`).

## Project map
| What | Where |
|---|---|
| Game scene | `Assets/SaturdayStudio/Scenes/SaturdayStudio.unity` |
| Gameplay scripts (sink, washer, rack, bed, checklist, desktop mode) | `Assets/SaturdayStudio/Scripts/` |
| Room generator (menu **Saturday Studio → Build Room**) | `Assets/SaturdayStudio/Editor/SaturdayStudioBuilder.cs` |
| Materials / meshes | `Assets/SaturdayStudio/Materials/`, `Assets/SaturdayStudio/Meshes/` |

> ⚠️ **Build Room** regenerates the whole room and overwrites hand-made changes in the scene. Only run it if everyone agrees.

---

# Working together

## Rules
1. **Pull before you start** (GitHub Desktop: Fetch origin → Pull origin).
2. **Only one person edits the scene at a time.** Say it in the group chat and in the *Who is working on what* table below.
3. **Commit small and often**, with a clear message (e.g. `Add towel to the bathroom`).
4. **Push when you stop working.** If GitHub Desktop shows a conflict, stop and ask before choosing anything.

## Who is working on what
| Person | Working on | Since | Touches the scene? |
|---|---|---|---|
| Salih | – | – | – |
|  |  |  |  |

## Requests & ideas
Write what you would like changed. Be as specific as you can: **where** in the room, **what** should happen, and **how you will know it works**. Salih collects these and turns them into tasks.

Template (copy it, fill it in, add it to the bottom of the list):

```
### [short title]
- Asked by:
- Date:
- Type: bug / new feature / change / question
- What I want:
- Where (object / place in the room / script):
- How to test it (what should happen when it works):
- Priority: must have / nice to have
- Status: open
```

<!-- Add new requests below this line -->

## Bugs found while testing
| Date | Found by | What happened | Steps to reproduce | Desktop or Quest? | Status |
|---|---|---|---|---|---|
|  |  |  |  |  | open |

## Change log
Short notes on what changed, newest first.

| Date | Who | What changed |
|---|---|---|
| 2026-11-25 | Salih | Hands that get dirty (OCD) + hand washing, double-B calm, ceiling, city view, real clothes with stains / wet look, dish drying rack, no stains in OCD |
| 2026-11-20 | Salih | Start menu with levels, wall clock, to-do list on the TV, no on-screen pop-ups |
| 2026-11-20 | Salih | VS1 ADHD and VS2 OCD modes from the presentation, floor chore, 13:00 summary |
| 2026-11-19 | Salih | Darker kitchen countertop so clean plates are visible |
| 2026-11-04 | Salih | Quest 3 build, distance grab, laundry fixes |
| 2026-11-02 | Salih | First version: room, chores, desktop test mode |
