# Image prompts — built-in imagegen, 2026-10-13

## Skin atlas

Use case: stylized-concept. Asset type: production 2D top-down spaceship sprite ATLAS for Unity game, ONE transparent PNG containing exactly 15 separate spacecraft arranged in an exact 5 columns x 3 rows equal-cell grid. No text, labels, borders, backdrop, shadows or UI. Each craft strictly overhead orthographic, nose points UP, symmetrical, centered in its cell, entire silhouette and short engine exhaust contained inside central 75% of its cell, empty transparent gutters. Crisp richly detailed hand-painted pixel-inspired science fiction game sprites, distinct SHAPES and material design, NOT recolors. Consistent scale and lighting. Left to right row1: Cobalt blue angular interceptor with twin thin wings; Magma lava armored fighter with orange fissures; Toxic green insectoid acid fighter; Rose Quartz pink crystal-wing ship; Arctic white ice spear ship. Row2: Nebula Violet crescent-wing purple ship; Solar Flare gold sunblade ship; Abyss black navy stealth manta; Jade Dragon emerald mechanical dragon ship; Aurora Prism iridescent silver crystal moth. Row3: Void Emperor black red imperial dreadfighter; Golden Comet gold swept-wing racer; Starter Ace steel cyan compact jet; Season Ace red white tournament arrowhead; Star Sovereign white gold regal star-wing cruiser. Real transparent background for all empty regions. Each of 15 crafts fully separated with no overlap. Atlas uniform 5x3 equal cells, landscape 5:3. No words anywhere.

### Refinement used for final image

Edit target is this 5 columns 3 rows transparent spaceship atlas. Keep exactly all 15 ship designs and same ordering, colors and top-down orientation. CRITICAL: scale EVERY individual ship down to occupy no more than 68% width and 68% height of its equal grid cell, with CLEAR EMPTY TRANSPARENT GUTTERS. Recenter each ship at exact center of its own equal-sized cell. Keep 5 columns x 3 rows evenly divided, canvas landscape 5:3. Do NOT resize the entire sheet as one object; each ship individually shrinks within its own cell. Outer margin also needed. No touching or overlapping between ships. Preserve transparency, no text, no grid lines. Final atlas ready to slice into 15 equal rectangles.

Final asset: `Assets/Resources/Images/Skins/ship-skins-v1.png`.

## Boss v2 (edit reference: dreadnought-v1.png)

Use case precise-object-edit. Improve this spaceship RAID BOSS sprite for a 2D overhead Unity game. Preserve same top-down upright silhouette, centered pivot, vertical nose UP, 5 cannons, symmetry, full body and approximate proportions. Upgrade surface to high-contrast crisp hand-painted sci-fi game art: layered dark navy steel armor with brighter silver bevels, copper machinery, vivid red-orange reactor core and emissive vein circuitry, cyan three rear engine nozzles. More imposing but readable at small game size, detailed panels, no background. Keep pure overhead orthographic NO tilt/perspective. Entire ship centered inside square canvas with 7% margin all around, no crop. True transparent background, no text, shadows, scenery or UI. NO diffuse glow outside silhouette; glow will be animated in engine.

Final asset: `Assets/Resources/Images/Bosses/dreadnought-v2.png`.
