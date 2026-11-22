# New shape checklist

When a group (shape) is added in Level Lab:

- [ ] Copy `templates/shape-board.lb` to `Tools/CloudCode/Deploy/shape_<group id>.lb`, set `Name`.
- [ ] `ugs deploy Tools/CloudCode/Deploy`.
- [ ] Nothing in Play Console or Firebase.
- [ ] The game finds the board by id (`RankScore.ShapeBoard(group id)`), no code change.
