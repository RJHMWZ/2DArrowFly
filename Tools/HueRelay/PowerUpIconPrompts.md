# Hue Relay image assets

Generated with the built-in `image_gen` tool. Exact prompts are saved in `PowerUpIconPrompts.json`.

Assets in `Assets/HueRelay/Art/PowerUps/`:

- `Undo.png`: blue counterclockwise return arrow, replacing the old hint bulb while preserving its GUID. The HUD and refill panel share this PNG; no runtime icon geometry.
- `Eraser.png`: blue/pink rubber eraser, used by the item button and animated cursor.
- `ExtraMoves.png`: orange plus sign.
- `Retry.png`: dark circular clockwise retry arrow.
- `EraseDust.png`: eight-frame eraser-crumb animation, four columns by two rows.

Icon imports use single sprites, bilinear filtering, no mipmaps, no Read/Write, no compression, and a maximum size of 128. All images preserve transparency; UI decorations do not intercept pointer input.

The eraser moves along the chosen arrow while the arrow is wiped from tail to head. Dust frames advance with the wipe. All presentation objects are authored in the scene and reused without per-frame GameObject creation.

Selection does not spend inventory. A valid arrow target spends one eraser, costs no moves, and awards no launch points. It erases exactly one arrow, including a blocked arrow, without launching a chain. Cancel, retry, tutorial entry, and victory safely clear or lock selection as appropriate.
