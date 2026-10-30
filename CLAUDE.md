# Cyber Strike

2D cyberpunk fighting game: 1 player vs 1 AI in a single neon-lit alley arena.

## Rules

- **Engine:** Unity 6 (6000.6.7f1), 2D URP, new Input System package (`com.unity.inputsystem`, already installed; Active Input Handling = Input System only). Do not use the legacy `UnityEngine.Input` API.
- **Scripts** go in `Assets/Scripts/`.
- **Commit** after each working feature.
- **Shared fighter logic:** Player and AI use the same `Fighter` component. Only the input source differs: `PlayerInputController` vs `AIController`. Gameplay rules (movement, attacks, health, abilities) live in `Fighter` and related components, never in the input controllers.
- **Editor setup:** After writing code, tell the user exactly what to set up in the Unity Editor (GameObjects, components, field values, layers, tags, prefabs, scene wiring).
- **No game code ahead of the build order.** Implement one step at a time.

## Build order

1. Movement + jump
2. Punch/kick + hitboxes + health bars
3. AI
4. Pickups + abilities
5. Art / animation / SFX
6. Menus + win/loss
7. CPU fighter select + difficulty profiles
8. Blocking

## Spec

### Controls (player)

| Action      | Input         |
|-------------|---------------|
| Move        | A / D         |
| Jump        | Space         |
| Punch       | Left mouse    |
| Kick        | F             |
| Block       | Hold right mouse |
| Use ability | E             |

### Combat

- Both fighters start at 100 HP (the Hard opponent gets a small bonus, see CPU opponents). Reducing the opponent to 0 HP wins.
- An attack must never deal damage more than once per swing.
- Each fighter has a health bar.
- Knockout: the final blow launches the loser backward, with brief slow motion, a flicker and a topple.
- Victory pose: 1 s after the KO the winner lands (if airborne), turns to the loser and plays its victory pose; the win/loss screen appears 2.4 s later (`MatchManager`: `victoryDelay`, `landingTimeout`, `victoryPoseTime`). A double KO skips the pose.

### Blocking

- Hold the block input to guard. While guarding (including the guard startup) the fighter can't move, jump or attack. The guard only goes up on the ground, when not attacking or stunned.
- The guard takes 0.1 s to come up (`FighterCombat.blockStartup`); hits during that startup land normally, so it can't be spammed instantly.
- Only hits from the front are blocked; hits from behind land normally.
- A blocked hit deals 20% chip damage (rounded, minimum 1), causes no hit stun and no hurt reaction. Chip damage can KO.
- Double Damage: a blocked hit still only deals chip damage (20% of the doubled hit).
- Ice Strike: a blocked punch does not slow and does not use up the armed Ice Strike; the next punch that lands does.
- Feedback: block pose (raise / hold) and an impact recoil frame, a cyan energy shield flash in front of the defender (`BlockVisuals`), cyan deflect sparks with a light hit-pause (`CombatFeedback`) and a "deflect" SFX instead of the hit sound.

### Stamina

- Each fighter has 100 stamina and a stamina bar under its health bar.
- Punch costs 10, kick 18, jump 8. Without enough stamina the action doesn't happen and the bar flashes.
- Regenerates 30/s after 0.6 s without spending.

### Ability pickups

- A random ability pickup spawns every 10 s.
- Each fighter holds at most 1 ability. Both player and AI can collect pickups.
- Abilities:
  - **Double Damage:** all attacks deal 2x damage for 5 s.
  - **Healing:** +20 HP, capped at max HP.
  - **Ability Steal:** takes the opponent's held ability; if they have none, Ability Steal is stored for later.
  - **Ice Strike:** the next successful punch slows the opponent for 3 s.

### AI

- Approaches the player, attacks when in range, and goes for pickups.
- Blocks: may raise its guard (after its block reaction time) when the player starts an attack in front of it, after taking a hit, or pre-emptively while waiting to attack. It drops the guard after its hold time and then waits out a cooldown, so it never blocks forever. Easy rarely blocks, Medium sometimes, Hard often and fast.
- Difficulty comes from an `AIProfile` ScriptableObject per opponent: reaction delay, attack frequency, retreat chances, how fast it notices pickups, how smartly it uses abilities, blocking (chance, reaction time, hold time, cooldown), plus an HP / damage bonus. `AIController` only reads the profile; the stat bonus is applied to `Health` / `FighterCombat` by `OpponentLoader`.

### CPU opponents (fighter select = difficulty)

| Difficulty | Name        | Look                                              | Profile highlights                                   |
|------------|-------------|---------------------------------------------------|------------------------------------------------------|
| Easy (1★)  | JINX VALE   | Street punk rookie: pink/green mohawk, tank top, tattoos, lime shades | Slow reactions, hesitant, retreats often, careless abilities, rarely blocks (10%, slow) |
| Medium (2★)| SGT. HALDEN | Corporate security enforcer: slate armor, sealed helmet, amber visor | The original AI tuning, blocks sometimes (55%, 0.08–0.18 s) |
| Hard (3★)  | OBSIDIAN    | Elite cyborg assassin: bulky gunmetal armor, back unit, skull cap, red eyes | Near-instant reactions, relentless, smart abilities, blocks often and fast (70%, 0.02–0.07 s), +15 HP, 1.1x damage |

- Data: `Assets/Data/Opponents/` holds `OpponentRoster`, one `OpponentData` (name, bio, stars, portrait, accent, sprites, AI profile) and one `AIProfile` per opponent.
- Art: `Tools/generate_art.py` draws each opponent in the shared fighter style (`OPPONENT_PALETTES`) into `Assets/Art/Fighters/Opponents/` (sheet, KO strip, portrait). Sheets use the same slicing as `fighter_ai.png`.
- `FighterSkin` swaps the CPU fighter's base-sheet sprites for the opponent's by frame name, so all opponents share the AI animation clips.
- The pick is kept in `OpponentRoster.Selected` (static) so Restart keeps the same opponent. If the Arena loads with no opponent picked it uses the roster default (Easy). The pick is cleared at the start of each Play session.
- `Assets/Scripts/Editor/PlayFromTitle.cs` sets `EditorSceneManager.playModeStartScene` to MainMenu, so Play in the editor always starts at the title screen.
- The opponent's name is shown above the CPU health bar.

### Screens

- **Flow:** Title → Fighter Select → Fight. Restart keeps the same opponent; Menu goes back to Title.
- **Title:** game title, controls panel (matches the Controls table), Start button (goes to Fighter Select).
- **Fighter Select** (`FighterSelect` scene): 3 neon cards with portrait, name, difficulty stars (1–3) and bio. Hover or Left/Right (A/D) highlights; click or Enter picks; Back button or Esc returns to Title.
- **Win / Loss:** Restart and Menu buttons.

### Art and audio

- Player look (`PLAYER_PALETTE`, style "rocker" in `Tools/generate_art.py`): original long-haired rocker merc with shoulder-length black hair, red-lensed aviators, stubble, black armored vest over bare arms, studded belt, brown leather pants and a gunmetal cyber arm (front arm) lit with the P1 cyan.
- Animations: idle, walk, jump, punch, kick, hurt, block (+ block impact), KO, victory.
- Victory poses (`fighter_<who>_win.png`, 8 frames: 4 intro played once by `Win`, 4 looped by `WinLoop`; fired by the `Win` animator trigger via `FighterAnimator.PlayVictory`), one per personality (`victory_frames` in `Tools/generate_art.py`):
  - Player (rocker): wind-up, cyber fist thrown at the sky, then headbangs while air-guitaring with the other hand.
  - JINX VALE (cocky): open-armed shrug, a hop with arms in a V, then a "come on" beckon with one hand behind the head, grinning (tongue out every other beat).
  - SGT. HALDEN (stiff, professional): snaps to attention, then a crisp salute held motionless while a glint sweeps the visor.
  - OBSIDIAN (cold, menacing): points at the fallen foe as the red eyes flare, then a clenched fist at the chest, eyes pulsing, a slight head tilt.
  - The CPU's base sheet has a plain fist pump; opponents get theirs through `FighterSkin` (their `OpponentData.sprites` include the win strip).
- SFX: punch, kick, hit, deflect (blocked hit), pickup, ability, win/loss, neon buzz; plus a looping rain ambience (see Atmosphere).
- UI SFX (`Assets/Audio/UI/`, generated by `Tools/generate_sfx.py`): a soft hover blip and a click confirm, quieter than combat. Every menu button (Title Start/Quit, Fighter Select cards/Back, Win/Loss Restart/Menu) has a `UIButtonSound`: hover plays once per enter, click/Enter confirms, missing clips are skipped. Sounds play through `AudioManager.PlayUI`, a persistent `DontDestroyOnLoad` source, so a click that loads a scene is still heard. Fighter Select drives card hover/click sounds from its keyboard highlight.
- `README.md` also lists the controls; keep it in sync with the Controls table.
- No music.

### Atmosphere (Arena)

- **Rain** (`PixelRain`, under the `Atmosphere` object): two full-screen layers of pixel-snapped streaks, each one mesh / one draw call, no per-drop objects. Far = thin, slow, faint (`RainFar`, sorting order -15, drawn behind the near alley walls); near = brighter, faster, longer (`RainNear`, -5). Near drops land at a random spot on the alley floor and spawn a tiny splash (`RainSplashes`, -4: flat flash + two hopping droplets, pooled, one mesh). Unlit shader `Cyber Strike/Vertex Color Unlit` (`PixelRain.mat`). Runs on scaled time, so it freezes in hit-pause and slows in the KO slow motion.
- **Neon flicker:** the five billboards and the right wall lamp are separate sprites (`Assets/Art/Backdrop/Signs/`, lit + dead versions, cut out of the backdrop by `Tools/generate_art.py`), under `Backdrop/NeonSigns`. `NeonFlicker` picks a random idle sign every 1.2–4 s to buzz (nervous dimming), glitch (pixel jumps, colour shifts) or cut out (sputter, dark, sputter back), never more than 2 at once and never all of them. Each event plays a quiet `neon_buzz` SFX.
- **Rain ambience:** `AmbientLoop` plays `Audio/Ambience/rain_loop.wav` (seamless 6 s, generated by `Tools/generate_sfx.py`) quietly on loop with a fade-in. Ambience, not music.
- Sorting: everything atmospheric stays below the fighters (9/10) and pickups (9); the HUD is a screen-space overlay, so nothing covers them.
