# Tactile Wars Recreation: Game Design Document

Scope: an original-asset Unity 6 recreation of the ORIGINAL 2015 "Tactile Wars" by Ankama (com.ankama.tactilwar), not "Tactile Wars: All Stars". No assets, names of units, or text from the original are reused.

Evidence labels used throughout:
- **VERIFIED** (source): stated directly by a source in `Docs/References/web/RESEARCH_MECHANICS.md` (called RM below) or visible in `Docs/References/VISUAL_ANALYSIS.md` (called VA below).
- **INFERRED**: reasoned from a source, not stated.
- **ESTIMATE**: our own value or behavior. Always configurable in a ScriptableObject.
- **UNVERIFIED**: no evidence exists; we must choose (see section 17).

Caveat on sources: RM was read through a summarizing fetch tool, so wording is paraphrase. VA is based on marketing screenshots plus one 360p upscaled let's play (game v1.2.2, a PvP match). The original is closed and possibly delisted; no shutdown source exists.

## 1. Vision

A fast, tactile, paint-splattered squad tactics game where the player commands a crowd of tiny helmeted soldiers ("pigments", VERIFIED, RM section 3) with one finger: tap to march, draw to reshape. The fantasy is being "The Hand" (VERIFIED, RM section 8) conducting a living, colorful army. Battles are short, arcade-style and readable at a glance (VERIFIED, RM section 1).

Recreation vision: a polished offline-first vertical slice that captures the original's feel (drawing formations, paint streams, bubble captures) using original 3D assets under an orthographic camera, with the base-defense meta loop playable against AI-generated bases.

## 2. Design pillars

1. **Draw to command.** The finger is the entire interface in battle. No unit selection, no ability buttons beyond the POW badge. (VERIFIED: tap to move, draw to reform, RM section 2.)
2. **Readable swarm.** 30 to 60 tiny units must read as one organism. Bold silhouettes, team color as the only saturated color, no health bars or damage numbers. (VERIFIED: no bars or numbers in any frame, VA section 4.)
3. **Paint is the language.** Bullets are paint dots, deaths leave splats, the squad anchor is a paint ring. (VERIFIED, VA sections 3 and 4.)
4. **Attack is a puzzle, defense is a craft.** Bases are authored by players under space and budget limits; attacks are about reading and counter-formationing them. (VERIFIED, RM section 5.)
5. **No friction.** No energy, no build timers (VERIFIED: RM section 1 and 6). Everything is gold-gated or level-gated.

## 3. Core loop

Moment to moment (seconds): tap or draw, squad reforms, auto-fires, enemies pop, splats accumulate.
Battle (about 2 to 5 minutes, ESTIMATE): capture point 1, hold it briefly, move to capture point 2, hold it, win.
Session: pick an opponent (higher rank gives better rewards, VERIFIED, RM section 6), attack, collect gold and XP, spend gold on research and on rebuying consumed troops and defenses, edit your own base, repeat.
Long term: level up to unlock research, climb four leagues, belong to a color faction.

## 4. Attack flow

VERIFIED (RM section 1, mmos.com): each base has two circular capture points on a linear map; capture one, defend it briefly, capture the next, win. The battle is lost when all of the player's units die.

Recreation flow:
1. Opponent selection screen (AI base list, offline).
2. Loading splash with a "VS" card and tip line (VERIFIED look, VA section 7: f_0008, f_0012).
3. Battle starts with the squad at the spawn end of the map. Camera follows the squad.
4. Player advances, fights enemy squads and defenses along the lane toward capture point 1.
5. Standing in the capture ring captures it over a short time (ESTIMATE, see Gameplay Mechanics). A white flag plants (VERIFIED look, VA section 4). Brief hold phase.
6. Advance to capture point 2, same procedure. Both captured means victory.
7. Squad wiped means defeat. 
8. Result card with rewards (victory card layout VERIFIED, VA section 7: f_0086; defeat card UNVERIFIED).

Map shape: a long corridor roughly 3 to 4 screens long (ESTIMATE) with two capture points; the first about one third in, the second at the far end.

## 5. Squad

- The player commands one squad of mini-soldiers with paint guns (VERIFIED, RM section 3).
- Observed size: roughly 40 to 60 soldiers on screen (VERIFIED visually, VA section 2: ps_08 about 50, ps_12 about 50 to 60). The true cap and the loss and replenishment rules are UNVERIFIED (RM section 3).
- Recreation: squad cap and starting count are ESTIMATE values in `SquadConfig` (default 40 start, cap 60 via research).
- All soldiers are identical recolors of one model (VERIFIED, VA section 2). One optional leader/mercenary unit with a pointed helmet spike appears (VERIFIED look, VA section 2).
- Soldiers are consumable: killed soldiers must be re-supplied between battles with gold (INFERRED from "troops and defenses must be rebought with gold", RM section 3 and 5).

## 6. Enemies and defenses

Named by sources (VERIFIED, RM sections 3 and 4): trackers, snipers, bunkers, heli-towers; infantry, turrets, armored vehicles, traps; categories Traps (mines, geysers, trapped slabs, tesla antennas) and Artillery (turrets, tanks, cannons, vannons, robotanks). Behaviors and stats are UNVERIFIED except: mines damage units on detonation and are best run through quickly; geysers explode; mortar and vehicle fire is dodged by positioning (RM section 4, levelwinner and multiplayer.it).

Seen in images (VERIFIED, VA section 5): enemy infantry in loose rings or grids around flag-bearers, fenced enclosures, small tanks, stone bunkers, walls, spawn pad holes with skull, dark disc skull marks on the ground (static, mines unconfirmed, VA section 5 note).

Recreation roster (names are ours, behaviors ESTIMATE):

| Entity | Role | Basis |
|---|---|---|
| Enemy infantry squad | Dot-stream fire at nearest player unit, loose cluster around a flag-bearer | VERIFIED look (ps_12, ps_06); behavior ESTIMATE |
| Flag-bearer | Squad leader in a bubble, see section 7 | VERIFIED (RM section 1) |
| Turret | Rotating head, single paint stream, long range | Name VERIFIED (RM); behavior ESTIMATE |
| Tank | Slow moving or static, shorter burst, tougher | Name VERIFIED; behavior ESTIMATE |
| Cannon | Slow arcing area shot, dodged by positioning | Mortar-dodge VERIFIED (multiplayer.it); numbers ESTIMATE |
| Mine | Ground disc with skull, detonates under units, area damage | Name and avoid-by-running VERIFIED (levelwinner); look ESTIMATE |
| Tesla antenna | Short-range zap trap | Name VERIFIED; behavior ESTIMATE |
| Bunker | Sturdy block with firing slot | Look VERIFIED (VA); behavior ESTIMATE |
| Sandbags and walls | Obstacles and cover; walls are not mentioned by any source as defenses | Look VERIFIED; function ESTIMATE |

Not in scope for the first milestone: snipers, heli-towers, geysers, trapped slabs, vannons, robotanks, bosses (names VERIFIED, behavior UNVERIFIED).

## 7. Flag-bearer bubble mechanic

VERIFIED (RM section 1): most enemy squads carry a flag-bearer. Killing it creates a bubble the player can tap to instantly destroy that enemy squad; advice is to kill the flag-bearer first so the squad is removed with no casualties. App Store text: "capture enemy captains with a tap."

Visual (VERIFIED, VA section 5): the flag-bearer stands in a glass bubble about 3 soldier sizes wide with a team-colored ring arc beneath and a skull pennant about 4 soldiers tall.

Recreation (ESTIMATE where noted):
- Flag-bearer has more HP than a grunt (ESTIMATE).
- On death a tappable bubble appears at its position and lasts a limited time (duration ESTIMATE; one guide snippet says do not pop it immediately, UNVERIFIED and not used).
- Tap the bubble: all living members of that squad die at once with the ghost and puff effect, flag plants, reinforcement meter credit given (credit amount ESTIMATE).
- If not tapped before expiry, the squad continues fighting leaderless (this expiry behavior is our choice).

## 8. Reinforcement meter

VERIFIED (RM section 1): a Reinforcement bar at top right fills as squads are killed; when full the player can summon reinforcements.

Visual evidence: the round top-right badge (white ring, three soldier busts, partial red fill) changes fill across frames (VA section 7, f_0033 to f_0048). Its meaning is not labeled in any frame (VA section 9). Decision: the badge IS the reinforcement meter (INFERRED, consistent with RM position "top right").

The POW hex badge beside it appears in power-up states (VERIFIED look, VA section 7); its function is UNVERIFIED. Decision: POW is the "call reinforcements" button that lights when the meter is full (ESTIMATE).

Recreation: meter fills per squad killed (squads, not soldiers, per RM wording), summon spawns a fixed batch of soldiers at the squad (batch size ESTIMATE).

## 9. Base defense and editing

VERIFIED (RM section 5): a player's base is the map attackers fight through, with no buildings (Clash of Clans comparison, no buildings). Placed items: traps, turrets, mercenaries, other defenses. Limits: space and a budget cap; each defense consumes budget points; budget rises with level; space expands via upgrades; territory extensions add spots and extra bases. You can test by attacking your own base. Defenses and troops are consumed and must be rebought with gold.

Recreation:
- Base editor scene (`BaseEditor.unity`) with a top-down-ish view of the same lane map. Slots or free placement on a coarse grid (grid size ESTIMATE; true value UNVERIFIED).
- Two capture points are fixed; the player places defenses and defender squads between and around them.
- Each defense has a `budgetCost` and a `spaceCost`. Placement is refused if either cap is exceeded (the "weight too high" user report at level 5 supports this, INFERRED).
- Caps rise with player level and space upgrades (numbers ESTIMATE).
- After a defense is destroyed in an attack against you, it must be rebought with gold. In the offline build there are no real attacks on the player, so a simulated "raid result" consumes defenses on a schedule or after each own-base test attack (decision, section 17).
- Test button: attack your own base (VERIFIED feature).

## 10. Economy

VERIFIED sources (RM section 6):
- **Gold**: earned in attacks, buyable with Prisms or ad videos, buys consumable troops/defenses and research unlocks.
- **Prisms**: premium currency. MMOs says it only buys gold; the App Store IAP list is broader in later versions. Conflict recorded.
- **XP and levels**: troops upgraded via experience and technologies; medals and XP drive rank.
- **Research**: items locked to a player level, cost gold once the level is reached, cannot be bought early with money.
- **No energy system**, no timers (VERIFIED). Keys that gate chest opening are reported by other sources and may be a post-launch addition (INFERRED); not included in the first milestone.
- On-screen currencies in the map screen: gold star coin and a hex gem (VERIFIED look, VA section 7: f_0090, f_0091). Victory card showed star coin x23 and EXP x298 for a level 20 vs 21 match (one data point, VA f_0086).

Recreation: no real-money purchases. Prisms exist as a premium currency earned in small amounts from milestones (decision, ESTIMATE), and can buy gold at a fixed rate (rate ESTIMATE). Ad-video rewards are replaced by a free daily gold grant (decision). Research tree uses our own names and costs (all ESTIMATE).

## 11. Factions

VERIFIED (RM section 7): six colors, Blue, Green, Orange, Red, Pink, Purple. Player chooses one and cannot attack their own color. Color-changing item exists (press release). Balance complaints that Purple and Pink win too often are user claims.

Recreation: `TeamColorId { Blue, Green, Orange, Red, Pink, Purple }` (matches contract). Player picks one at first launch; AI opponents are drawn from the other five. Default demo matchup Red vs Teal is a visual-test pairing only; teal is seen in the references (ps_12) but is not one of the six VERIFIED faction names (the Colors War menu in f_0094 lists purple, teal, green, orange, red, magenta, which conflicts with the text sources; recorded as a conflict). Decision: ship the six text-source names and map Teal onto Blue and Magenta onto Pink in the palette.

Faction perks: sources say research effects vary by color (VERIFIED, levelwinner guide 2, details UNVERIFIED). Decision: no faction perks in the first milestone.

## 12. Leagues

VERIFIED (RM section 7): four Leagues, wins raise rank, higher-ranked opponents give better rewards. League names and thresholds UNVERIFIED.

Recreation: four leagues with our own names (Bronze, Silver, Gold, Platinum placeholders, ESTIMATE), rank points won and lost per battle (ESTIMATE), matchmaking picks AI bases near the player's rank.

## 13. Alliances

Advertised (VERIFIED, App Store and press release): alliances of same-color players. MMOs saw no team features at launch (conflict). Decision: alliances are interface-only in this build; a stub `IAllianceService` with a local offline provider and a fake alliance roster (no gameplay effect).

## 14. Offline-first scope and AI bases

The original is asynchronous PvP against real players' bases (VERIFIED, RM section 1). The recreation has no server. Scope:
- `Online/*` contains interfaces only (`IMatchmakingService`, `IBaseRepository`, `ILeaderboardService`, `IAllianceService`) with a local offline provider.
- AI bases are generated from a seeded generator using the same placement rules and budget caps as player bases, so the attack content is a plausible stand-in for player bases (decision).
- Leaderboards and alliances show synthetic data labeled as offline.
- The player's own base is saved locally (JSON in `Application.persistentDataPath`).

## 15. Mercenaries (reduced scope)

Verified existence only (RM section 4): legendary mercenaries with special abilities, hireable or rentable, boosts. Names and abilities UNVERIFIED. Visual hints: spiked-helmet minigunner, cannon-backpack unit, ball robot with flag, rocket cube, spinning-top disc (VA section 5, promo only). The tip "Keep your Mercenary alive" (f_0012) suggests one mercenary accompanies the squad and its death matters (INFERRED).

Recreation: one mercenary slot with one example unit (spiked-helmet heavy gunner) as a stretch goal in Phase 7. Abilities are our own.

## 16. Out of scope (first milestone)

Real multiplayer, chests and keys, ad rewards, real-money IAP, Colors War world map mechanics, boss battles, sniper towers and heli-towers, chat, localization beyond English.

## 17. Design decisions for this recreation

Every value or behavior below is chosen by us and is not established by the references. All live in ScriptableObjects unless noted.

| # | Decision | Why it is a choice |
|---|---|---|
| D1 | Auto-fire with facing set by formation direction; no manual aiming | Sources conflict (RM section 2: press release says aim with finger, multiplayer.it says auto-attack in range). Auto-fire better supported. |
| D2 | Starting squad 40, cap 60 | Observed counts about 40 to 60 (VA); true rules UNVERIFIED. |
| D3 | Soldier HP, damage, fire rate, range, speed | No stats in any source. |
| D4 | Capture time and hold time | Sources say "briefly"; no seconds. |
| D5 | Capture ring radius 2.0 units | Our contract value, VA shows rings but no capture rules. |
| D6 | Flag-bearer bubble lifetime, leaderless fallback | Not stated. |
| D7 | Reinforcement fill per squad, batch size, POW button function | Bar verified, numbers and POW role not. |
| D8 | Defense stats and exact behaviors for turret, tank, cannon, mine, tesla | Only names and light hints verified. |
| D9 | Budget and space numbers, grid size, level scaling | Concept verified, numbers UNVERIFIED. |
| D10 | Four league names, point deltas | Count verified, rest not. |
| D11 | Research tree names, costs, level gates | Mechanism verified, content not. |
| D12 | Gold and XP reward amounts | One victory card sample (23 coins, 298 EXP). Formula ours. |
| D13 | Prisms earned free, fixed gold exchange rate, daily gold gift instead of ad videos | No IAP or ads in the build. |
| D14 | Map length and shape (corridor of about 3 to 4 screens) | Linear map verified, dimensions not. |
| D15 | Battle timer: none shown in HUD; optional soft time stat on result card | Sources do not document a timer; the victory card lists TIME (VA f_0086) as a stat only. |
| D16 | Grade scale on result card (S, A, B, C letters) | Letters seen in f_0086; meaning not known. |
| D17 | Simulated defender raids to consume defenses offline | No server; original consumption rule is verified only in concept. |
| D18 | Alliances and leaderboards stubbed with synthetic data | Offline-only. |
| D19 | Teal mapped to Blue and Magenta to Pink faction names | Text sources and Colors War menu disagree. |
| D20 | Tap on bubble pops immediately; tap anywhere else moves the squad | Exact input arbitration unknown. |
| D21 | Defeat screen layout | Never seen in references. |
| D22 | 3D models under an orthographic camera instead of 2D sprites | Owner requirement, see Visual Style Guide. |
