# Gameplay

## The world

Three competitions are simulated in full at the same time, and every club plays a 22-round league
season plus knockout rugby.

| Competition | Clubs | Tier | Salary cap | Finals | Movement |
| --- | --- | --- | --- | --- | --- |
| National Rugby League (NRL) | 12 | 1 (Australia) | £4.6m | top 6 | 1 relegated |
| NRL Championship | 12 | 2 (Australia) | £2.6m | top 4 | 1 promoted |
| Super League | 12 | 1 (England) | £3.1m | top 6 | 1 relegated |
| The Championship | 12 | 2 (England) | £1.5m | top 4 | 1 promoted |

Both countries run a two-tier pyramid, so every manager has a route up and a trapdoor down. Money
follows the competition: the Australian divisions trade in dollars and the two English ones in pounds, so
all fees, wages and prize money are shown in the right currency.

- **Challenge Shield** — a knockout cup for all 48 clubs, seeded by reputation and re-seeded each
  round (best remaining team plays worst). A cup round becomes due every third league week, so the
  cup final lands late in the season.
- **Promotion and relegation** — it happens separately in each country: the second tier's finals
  winner goes up automatically, swapping with the bottom club of the division above. A relegation
  play-off then pits the second-bottom club of the top flight against the strongest remaining
  second-tier finisher; if the challenger wins, the clubs swap divisions as well. That means NRL ↔
  NRL Championship and Super League ↔ The Championship. The game news feed reports every movement.
- **Finals** — the top clubs play a seeded knockout for the league title, giving the top two (or
  the top seed in a four-team series) a bye into the semi-finals.

## A week in the life of a manager

The career hub is the home screen. Each week you can work through any of the sections before
advancing:

1. **Dashboard** — next opponent, competition, weather forecast, board expectation, balance, cap
   space, injuries and the news feed. *Matchday* opens the fixture, *Quick result* simulates it
   instantly, and *Advance week* plays every other fixture and moves the calendar on.
2. **Squad** — the full roster with positions, ratings, form, fitness and injuries. Pick a starting
   XIII and four interchange players, or hit *Select best XVII*. Injured and suspended players are
   shown but cannot be selected. Facility and youth-academy upgrades are bought here.
3. **Tactics** — choose a playing style and a formation from the presets (or adjust individual
   slot positions), then set the four sliders: kick early, offloads, line speed and width. Formation
   and tactics feed both the simulation and the 3D side's shape.
4. **Training** — pick the week's focus (balanced, fitness, attack, defence, handling, kicking or
   recovery). Young players with room to grow improve fastest; older players decline. A tired squad
   trained hard risks knocks, and the report lists who improved and who got hurt.
5. **Transfers** — search transfer-listed players and free agents, view a full player card, bid,
   negotiate a contract, list your own players, and respond to incoming bids. The window closes
   after round 8; free agents can be signed whenever there is cap room.
6. **Club** — finances (balance, wage bill, cap spend, ticket price), the transaction ledger, the
   stadium, facilities, current sponsors and any sponsor offers on the table.
7. **Competitions** — every league table, the cup bracket and the finals series, with round-by-round
   navigation.

After the last league round the game moves to finals, then to the off-season: contracts expire,
veterans retire, youth players arrive, promotion and relegation are settled and you start the next
season. The career autosaves after every match and on exit, so a phone can be locked at any time.

## Matchday

`Matchday` shows the fixture, both lineups,  the weather and your tactical setup. You then choose:

- **Watch simulation** — the top-down match. Watch the animated pitch, pause, switch between 1×, 4×
  and 12× speed, or skip to the result. A HUD shows the score, set count, field position and a live
  event ticker.
- **Play in 3D** — the player-controlled match (below).
- **Quick result** — resolve the fixture immediately from the same engine and go straight to the
  post-match report.

The post-match screen shows the scoreline, scorers, team and player statistics, the updated table,
any injuries and the financial effect, then returns you to the hub.

## Rules modelled

The engine follows the NRL / Super League law book:

- Six-tackle sets with a 10-metre retreat; failure to complete a set hands over possession.
- Tries are worth 4, conversions and penalty goals 2, drop goals 1; a conversion is only attempted
  from the try's position, so wide tries are harder to convert.
- Penalties restart the set; repeated infringements can produce a sin bin (ten minutes a man down),
  which visibly weakens the defence. Binned players are listed with a countdown while they are off.
- Head injury assessments: a head knock takes the player off for fifteen minutes of assessment. An
  interchange covers the position, and at the end of the assessment he either passes and returns or
  is ruled out for the rest of the match and misses weeks through a head knock.
- 40/20 kicks, goal-line drop-outs, restarts from the 20-metre line, and interchanges from the bench.
- Knockout and finals matches use golden point: ten extra minutes, first score wins; if nobody
  scores, the stronger goalkicker settles it.
- Weather (clear, overcast, rain, wind, cold) modifies handling errors and kicking accuracy; wind
  has a speed and a bearing, and it pushes conversions and downfield kicks off line. Home advantage
  is worth a small edge; fatigue, form and morale move the probabilities during the match.

## The simulation

The top-down mode is not a separate game: it animates the same engine that produces the score. Each
carry is resolved from:

- the ball carrier's speed, strength, handling and vision against the defenders' tackling, position
  and formation coverage;
- the attacking team's style and tactics sliders, and the defensive line speed and width;
- field position, tackle count (a tired defence is easier to break on tackle 4–6), weather, home
  advantage, player fitness and any sin-bin;
- a seeded random roll, so the same fixture always produces the same match.

That means a well-drilled, fit squad with tactics suited to the weather really does win more often —
the tests assert that a stronger squad beats a weaker one more often than not over many seeds.

## The 3D match

A stylised, arcade third-person match on a low-poly pitch, rendered with `BasicEffect` meshes and a
textured ground plane. The camera follows the ball. You control:

- **Attack** — the ball carrier. The left thumbstick moves; **Pass** finds the best-supported
  teammate, **Kick** boots it downfield, **Sprint** drains stamina, and running into the defence
  draws tacklers.
- **Defence** — the nearest defender. Move to close the gap and press **Tackle** when in range.
- The other sixteen players hold the chosen formation and react to play; the opposition plays to its
  own style and ratings, so squad quality still matters.

On desktop, arrow keys/WASD move, **Space** passes, **K** kicks, **Shift** sprints and **E** tackles.
On a phone the joystick and buttons are drawn in the corners and support multi-touch, so you can
move, sprint and pass at the same time.

### Goal kicking

Every try is followed by a conversion, and both teams take theirs:

- **Your kick** — drag left or right to place the ball, which walks it back from the posts or wider
  for a better angle. Tap **KICK**, then strike as the power needle crosses the green band; the band
  is where the distance needs the ball to be struck. The wind shown in the top-right corner pushes
  the ball off line during flight.
- **The opposition kick** — the AI kicker lines up, waits for the needle to reach the band and
  strikes; the ball is animated between the posts and you see whether it is good or wide.
- The maths lives in `GoalKicking` in Core, so the conversion and the kicking drill obey the same
  rules: goal-kicking and kicking ratings set the spread, power error and the cross wind set the miss.
- The clock stops during a conversion, as it does in a real match.

**Goal-kicking training** — the club tab's training card has an extra kicking session (cost rises
with the training base). It sharpens the two best kickers at the club by a point of goal kicking each.

The 3D result is submitted to the career exactly like a simulated one: it updates the table, the
cup, player statistics, finances and injuries.

## Fictional clubs and players

Every club is an invention. The names borrow the *place* of a real rugby-league heartland and pair it
with an original nickname and identity:

- NRL: Brisbane Stallions, Sydney Cockerels, Melbourne Cyclones, Penrith Pumas, Parramatta Rays,
  Sutherland Marlins, Manly Kites, South Sydney Hares, Illawarra Wyverns, Canberra Marauders,
  Townsville Drovers, Auckland Voyagers.
- NRL Championship: Newcastle Paladins, Canterbury Bulls, Gold Coast Colossi, Inner West Cougars,
  Moreton Bay Porpoises, Perth Pioneers, Christchurch Crows, Adelaide Anchors, Toowoomba
  Timberjacks, Rockhampton Rustlers, Hobart Thylacines, Darwin Darters.
- Super League: Wigan Legion, St Helens Vulcans, Leeds Mammoths, Warrington Coyotes, Hull Dockers,
  Hull Trawlers, Catalan Suns, Leigh Lynx, Salford Imps, Huddersfield Engineers, Castleford Jaguars,
  Wakefield Abbeys.
- The Championship: Bradford Aurochs, Toulouse Aviators, Featherstone Forgers, York Constables,
  London Meteors, Widnes Longships, Halifax Ravens, Barrow Buccaneers, Sheffield Falcons, Batley
  Beagles, Dewsbury Tups, Whitehaven Herders.

No real club's name, nickname, badge, colours, stadium or sponsor is reproduced, and every player is
generated. Sponsors (main, sleeve, shorts, stadium naming and kit supplier) are invented brands too.
