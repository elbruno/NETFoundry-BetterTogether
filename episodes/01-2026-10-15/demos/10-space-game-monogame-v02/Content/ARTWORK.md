# Game artwork

These original game images were generated using the `t2i` CLI 1.6.0 with
provider `foundry-gpt-image-2`, model `gpt-image-2`. They support the in-game
title screen and gameplay, not social promotion. No provider credentials or
endpoints are needed to load the bundled images. Running the game does not
call an image-generation service.

| Image | Dimensions | Preparation |
| --- | --- | --- |
| `atlas.png` | 1152 by 1152 RGBA | Nine isolated objects cropped from a generated 1024-square smooth sprite sheet, magenta background removed, Lanczos-scaled and centered in 384-pixel cells, then premultiplied |
| `background-light.png` | 1920 by 1080 RGB | Generated light starfield, center-cropped to 16:9 and Lanczos-resized |
| `welcome-light.png` | 1920 by 1080 RGB | Generated light title scene, center-cropped to 16:9 and Lanczos-resized |
| `background-dark.png` | 1920 by 1080 RGB | Demo 09 dark starfield, center-cropped to 16:9 and Lanczos-resized |
| `welcome-dark.png` | 1920 by 1080 RGB | Demo 09 dark title scene, center-cropped to 16:9 and Lanczos-resized |

The sprite atlas contains three columns and three rows of 384-square cells,
read left to right, top to bottom: player ship, enemy scout, enemy fighter,
enemy heavy, player laser, enemy laser, explosion, hull-hit ring, asteroid.
The heavy ship already faces downward, so the game does not rotate it.
Cell centers are (192, 192) relative to each source rectangle.

Sprites are drawn at 96 pixels over 44-pixel logical cells. Enemy columns are
three cells apart, so the larger ships stay visually separated. Cosmetic
designs and effects do not change collision bounds, enemy health or game rules.
The asteroid is optional decoration, not a new obstacle. Atlas alpha is
premultiplied. A checkerboard is not embedded in any runtime image.

Light theme is the default. Dark theme reuses the demo 09 scenes at the larger
canvas size. Press T to switch. Theme changes are presentation only.

The light welcome screen reads ".NET + Foundry", "Better Together",
"Decision Space Game" and "The Grand Finale". Brand names describe the
developer event and demo, not an endorsement.

AI-generated output is not a claim of exclusive ownership or legal clearance,
and no third-party asset-pack license is asserted. The bundled font has
separate license and attribution notices.
