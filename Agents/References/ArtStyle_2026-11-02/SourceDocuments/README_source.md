# Great Voyage Asset Request Guide

## 1. What to prepare

To get stable, repeatable results from the Agent, prepare these materials:

### Required
1. **Style reference images**
   - Use the approved Great Voyage sprite sheets
   - Include the original Prison Architect style reference images
2. **A clear request**
   - Tell the Agent what kind of asset you need
   - List the important objects or tile purpose

### Recommended
3. **Output specification**
   - Canvas size or tile size
   - Transparent background 여부
   - Scaling target such as 200%+ in-game
4. **Restrictions**
   - What must be preserved
   - What must not appear

---

## 2. Choose the correct request type

There are 3 main request types.

### A. Sprite Sheet
Use this when you need multiple separate objects on one atlas.

Examples:
- harbor props
- cargo objects
- tavern furniture
- military objects
- environment objects

Recommended request phrase:
"Create a sprite sheet."

### B. Seamless Texture
Use this when one texture must repeat infinitely without visible seams.

Examples:
- wooden deck floor
- dock planks
- stone floor
- packed dirt
- sand

Recommended request phrase:
"Create a seamless tile texture."

### C. Auto Tile / Modular Tile Set
Use this when map tiles must connect using corners, edges, and transitions.

Examples:
- coastline tiles
- walls
- roads
- water-to-sand transitions
- fence systems

Recommended request phrase:
"Create an autotile set" or "Create a modular tile set."

---

## 3. Basic request format

Use this structure:

- Output Type:
- Style:
- Asset Category:
- Specific Assets:
- Canvas / Tile Size:
- Background:
- Scaling Goal:
- Must Preserve:
- Must Avoid:
- Task Type:

Example:

Output Type: Sprite Sheet
Style: Use registered Prison Architect style references and approved Great Voyage sheets
Asset Category: Harbor cargo
Specific Assets: crates, barrels, sacks, rope coils, anchors
Canvas Size: up to 2048x2048
Background: transparent
Scaling Goal: clean at 200%+
Must Preserve: clean outlines, simple surfaces, restrained shading
Must Avoid: painterly textures, noisy details, modern objects
Task Type: new production

---

## 4. Example requests

### Example 1 — Sprite Sheet
Create a sprite sheet for an Age of Sail harbor.

- Output Type: Sprite Sheet
- Asset Category: Harbor & Shipyard
- Specific Assets: rowboats, dock sections, anchors, ropes, barrels, lanterns
- Canvas Size: up to 2048x2048
- Background: transparent
- Scaling Goal: 200%+ clarity
- Priority: fewer but larger sprites if needed

### Example 2 — Seamless Texture
Create a seamless tile texture for a wooden ship deck.

- Output Type: Seamless Texture
- Asset Category: Deck Floor
- Specific Assets: wooden planks only
- Tile Size: 256x256
- Background: opaque allowed
- Scaling Goal: must remain clean at 200%+
- Special Rule: all four edges must tile seamlessly
- Validation: check in a 3x3 repeated preview

### Example 3 — Auto Tile
Create a modular coastline tile set.

- Output Type: Auto Tile
- Asset Category: Coastline
- Specific Assets: water, sand, corner transitions, edge transitions, inner corners, outer corners
- Tile Size: 256x256 each
- Background: transparent if needed
- Scaling Goal: 200%+ clarity
- Special Rule: all neighboring transitions must connect correctly

---

## 5. How to request repeating textures

If you need a repeating texture, do not ask for a normal sprite sheet.

Use wording like:

- "Create a seamless texture tile"
- "This must tile perfectly on all edges"
- "Provide a 3x3 repeat validation preview"
- "Keep the texture simple and consistent with the reference style"

Good examples:
- ship deck floor
- pier wood planks
- stone fort floor
- tropical dirt ground

Bad example:
"Make many deck floor pieces on one sprite sheet"
This may produce separated decorative pieces instead of a true repeatable tile.

---

## 6. How to request modular connection sets

If you need connected map-building parts, ask for modular or autotile behavior.

Use wording like:

- "Create a modular tile set"
- "Include center, edges, inner corners, outer corners, and transitions"
- "All neighboring tiles must connect cleanly"
- "Maintain the same lighting and outline rules across all parts"

Good examples:
- coastlines
- walls
- fences
- roads
- water channels

---

## 7. Recommended best practices

1. Always provide style references
2. Keep one request focused on one asset category
3. Prioritize clarity over quantity
4. Explicitly say if 200%+ scaling matters
5. Separate sprite sheet requests from tile requests
6. Ask for transparent background unless a texture tile truly needs otherwise
7. If continuity matters, tell the Agent to match previously approved sheets

---

## 8. Quick request templates

### Sprite Sheet Template
Create a sprite sheet.

- Output Type: Sprite Sheet
- Style: use registered references exactly
- Asset Category:
- Specific Assets:
- Canvas Size:
- Background: transparent
- Scaling Goal:
- Must Preserve:
- Must Avoid:
- Task Type:

### Seamless Texture Template
Create a seamless tile texture.

- Output Type: Seamless Texture
- Style: use registered references exactly
- Asset Category:
- Texture Subject:
- Tile Size:
- Background:
- Scaling Goal:
- Must Preserve:
- Must Avoid:
- Validation: 3x3 repeat preview
- Task Type:

### Auto Tile Template
Create a modular tile set.

- Output Type: Auto Tile
- Style: use registered references exactly
- Asset Category:
- Tile Subject:
- Tile Size:
- Required Variants: center / edge / inner corner / outer corner / transitions
- Background:
- Scaling Goal:
- Must Preserve:
- Must Avoid:
- Validation: adjacency test
- Task Type:

---

## 9. Final reminder

If the goal is production-ready game assets:
- provide the approved reference images
- specify the output type correctly
- state size and scaling requirements clearly
- prefer quality over quantity
- say whether you need sprite sheet, seamless texture, or autotile

This will produce the most stable and reusable results.
