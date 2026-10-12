# Changelog

## 0.2.0 (2026-10-11)

- **Lists picked from pictures**: "Listed pieces" and "Pieces left alone" have a Pick button in the settings window (F1): it opens a window of everything the list can hold, each with its picture, where a click puts a thing on the list or takes it off. The typed box stays beside it, and the list is written as the mod reads it, with any names the game doesn't know kept. The window is set in the new section "Lists (every mod)" (its size, its pictures', whether things not found yet show).

## 0.1.0 (2026-10-11)

- **First version**, as a mod of miikeskii's QoL Pack and on its own.
- **Overlapping pieces stop flickering**: where two building pieces are laid into each other (their drawings sharing more than a centimetre of space) and a surface of one lies on a surface of the other, one of them is drawn bigger on every side by the nudge (1 mm by default, 0.5 to 5) and lifted by half of it, so their surfaces no longer meet at one depth; a piece laid into several others is drawn a step bigger for each, so no two such pieces are ever drawn alike. Roofs, stairs and the slanted beams and walls grow without tilting and are lifted by the whole nudge, so sloped surfaces come apart too. Pieces that only touch, or share no surface, are drawn as the game draws them.
- Pieces whose drawing can't all be moved without what you bump into (some doors and gates, chests, pots and banners, and a few 67° pieces), and turrets, are drawn as the game draws them; the pieces laid into them are drawn off them.
- **Which one shows on top**: the smaller piece (the default), the bigger one, by kind (floors, then walls, roofs, stairs, beams and poles, doors, and furniture and decor on top) or no preference; the same every time the pieces load.
- Only the drawing objects of a piece change, never its place, its colliders, its snap points or its saved record.
- Settings for which pieces (every piece, or only listed ones), pieces left alone (the rugs by default), pieces built by others (fixed too, or only mine), and a log of what it does (off).
- On for everyone out of the box, at his call.
