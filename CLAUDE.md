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

## Spec

### Controls (player)

| Action      | Input         |
|-------------|---------------|
| Move        | A / D         |
| Jump        | Space         |
| Punch       | Left mouse    |
| Kick        | Right mouse   |
| Use ability | E             |

### Combat

- Both fighters start at 100 HP. Reducing the opponent to 0 HP wins.
- An attack must never deal damage more than once per swing.
- Each fighter has a health bar.
- Knockout: the final blow launches the loser backward, with brief slow motion, a flicker and a topple.

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

### Screens

- **Title:** game title, controls, Start button.
- **Win / Loss:** Restart and Menu buttons.

### Art and audio

- Animations: idle, walk, jump, punch, kick, hurt.
- SFX: punch, kick, pickup, ability, win/loss.
- No music.
