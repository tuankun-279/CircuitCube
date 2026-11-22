# New shape checklist

When a group (shape) is added in Level Lab:

- [ ] Copy `templates/shape-board.lb` to `Tools/CloudCode/Deploy/shape_<group id>.lb`, set `Name`.
- [ ] `ugs deploy Tools/CloudCode/Deploy`.
- [ ] Nothing in Play Console or Firebase.
- [ ] The game finds the board by id (`RankScore.ShapeBoard(group id)`), no code change.
- [ ] Weekly boards for the shape: `weekly_<group>_1..3.lb` and `weekly_<group>.lb` from `weekly-board.lb`; add the
      group id to the next week's manifest `challenge.groups` once it has challenge levels.
