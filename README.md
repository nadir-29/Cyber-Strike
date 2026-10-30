# Cyber Strike

A 2D cyberpunk pixel-art fighting game: you against a CPU fighter in a neon-lit alley.
Pick your opponent on the fighter select screen; it doubles as the difficulty.

Built with Unity 6 (6000.6.7f1), 2D URP and the Input System package.

## Controls

| Action      | Input              |
|-------------|--------------------|
| Move        | A / D              |
| Dash        | Left Shift + A / D |
| Jump        | Space              |
| Turn around | Jump + opposite direction (A / D) |
| Punch       | Left mouse         |
| Kick        | F                  |
| Special     | Q (meter full)     |
| Block       | Hold right mouse   |
| Use ability | E (slot 1) / R (slot 2) |

Menus: mouse, or Left/Right (A/D) + Enter on the fighter select screen, Up/Down (W/S) for hearts; Esc goes back.

## Movement and facing

- On the ground you never turn around: A / D walk forward or backward (with a backward walk), always facing the same way.
- To turn, jump and hold the opposite direction: you flip in mid-air and land facing the new way.
- Jumps are low and snappy (about 3.6 units high).
- Punches, kicks, the special and blocking all go the way you're facing, so if your opponent gets behind you, jump to turn around first.
- The CPU follows the same rules: it has to jump to turn too (Hard does it almost instantly, Easy is slow to react).

## Blocking

- Hold right mouse to guard. While guarding you can't move, jump or attack.
- The guard takes 0.1 s to come up, so block before the hit arrives.
- Only hits from the front are blocked; a blocked hit still deals 20% chip damage.
- Double Damage hits are reduced to chip damage too, and a blocked punch never triggers Ice Strike.

## Special meter and cyber uppercut

- The magenta bar under your stamina bar is the special meter. It fills when you land hits and when you take damage.
- When it's full it pulses and chimes: press Q for the cyber uppercut, a big hit that launches the opponent into the air.
- It uses the whole meter. Blocked, it only does chip damage, so don't throw it into a guard.

## Dash

- Hold Left Shift and press A or D (or press Shift while moving) to dash that way: a quick burst along the ground with an afterimage trail.
- Dashing away from the way you face is a backdash: you keep facing your opponent.
- About 1 s cooldown. You can't attack, jump or turn mid-dash.
- The CPU dashes too: to close the distance and to dodge your attacks (Hard does both a lot more).

## Opponents

| Difficulty | Name        |                                   |
|------------|-------------|-----------------------------------|
| Easy       | JINX VALE   | Street punk rookie                |
| Medium     | SGT. HALDEN | Corporate security enforcer       |
| Hard       | OBSIDIAN    | Elite cyborg assassin (+HP, +dmg) |

## Victory poses

After a knockout the winner strikes a victory pose before the win/loss screen: the player throws a
cyber fist at the sky and headbangs, JINX VALE shrugs and beckons with a cocky grin, SGT. HALDEN snaps
to attention and salutes, and OBSIDIAN points at you, eyes flaring red, and clenches a fist.

## Atmosphere

The arena is drenched: two layers of pixel rain (faint and slow far away, brighter and faster up close)
with tiny splashes on the alley floor, neon signs that now and then buzz, glitch or cut out (never all
at once) with an electric hum, and a quiet looping rain ambience. There's no music.
All of it stays behind the fighters and the HUD.

## Changes from the one-pager

- **Falling pickups:** a pickup still spawns every 10 s, but now either on the ground or falling from the sky
  at a random spot, glowing as it drops. Jump into one to grab it mid-air, or pick it up after it lands.
- **Two ability slots:** hold up to 2 abilities; E uses slot 1, R slot 2. With both full you can't pick up.
  Ability Steal takes the opponent's slot 1 ability (slot 2 if slot 1 is empty), or stays stored if they have none.
- **Hearts:** each fighter has hearts (pick 1–5 on the fighter select screen, default 3; Restart keeps it).
  At 0 HP you lose a heart, get knocked out briefly, then respawn at your start with full HP, an empty special
  meter, your abilities kept and ~1.5 s of flashing invincibility. Run out of hearts and you lose the match.
- **CPU:** jumps for falling pickups when it's close and safe, uses both slots sensibly, and plays more
  carefully on its last heart (Hard most of all).

## Running

Open the project in Unity and press Play; it always starts at the title screen.
Scenes: `MainMenu` → `FighterSelect` → `Arena`.

## Play in a browser

Online: https://nadir-29.github.io/Cyber-Strike/ (published from `WebBuild/` by `.github/workflows/pages.yml` on
every push to `main`).

`WebBuild/` holds a WebGL build. Browsers won't run it straight from disk (`file://`), so serve the folder:

```
py -m http.server 8000 -d WebBuild
```

then open http://localhost:8000 and click the game once so it gets keyboard focus. The folder can also be
uploaded as-is to any static host (GitHub Pages, itch.io as an HTML game, Netlify, ...). The build is left
uncompressed on purpose (Player Settings > Web > Publishing Settings > Compression Format: Disabled): compressed
`.br` / `.gz` builds need the host to send a `Content-Encoding` header, which GitHub Pages doesn't, and Pages gzips
the files in transit anyway. There's no Quit button on the web.

To rebuild: File > Build Profiles > Web, output folder `WebBuild`.

Art and sound are generated by the scripts in `Tools/` (`py Tools/generate_art.py`, `py Tools/generate_sfx.py`).
