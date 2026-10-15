# Game artwork

These original game images were generated using the `t2i` CLI 1.6.0 with
provider `foundry-gpt-image-2`, model `gpt-image-2`, on October 15, 2026.
They support the in-game title screen and gameplay, not social promotion.
No provider credentials or endpoints are needed to load the bundled images.
Running the game does not call an image-generation service.

| Image | Dimensions | Preparation |
| --- | --- | --- |
| `atlas.png` | 768 by 768 RGBA | Nine isolated objects cropped from a generated 1024-square source, magenta background removed, nearest-neighbor resized and centered |
| `background.png` | 1280 by 720 RGB | Generated 1024-square dark starfield, center-cropped to 16:9 and resized |
| `welcome.png` | 1280 by 720 RGB | Generated 1792 by 1024 title scene, center-cropped to 16:9 and resized |

The sprite atlas contains three columns and three rows of 256-square cells,
read left to right, top to bottom: player ship, enemy scout, enemy fighter,
enemy heavy, player laser, enemy laser, explosion, hull-hit ring, asteroid.
Each cell has at least 24 pixels of transparent padding. Cell centers are
(128, 128) relative to each source rectangle.

The violet ship was rotated 180 degrees to face downward. Cosmetic designs
and effects do not change collision bounds, enemy health or game rules.
The asteroid is optional decoration, not a new obstacle. PNG alpha is straight,
not premultiplied. A checkerboard is not embedded in any runtime image.

Art direction: original top-down retro sci-fi, cyan player, amber/coral/violet
enemies, readable silhouettes, dark navy space. The welcome screen reads
“.NET + Foundry”, “Better Together”, “Decision Space Game” and “The Grand
Finale”. Brand names describe the developer event and demo, not an endorsement.

AI-generated output is not a claim of exclusive ownership or legal clearance,
and no third-party asset-pack license is asserted. The bundled font has
separate license and attribution notices.
