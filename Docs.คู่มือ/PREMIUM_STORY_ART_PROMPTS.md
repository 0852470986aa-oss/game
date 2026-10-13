# Generated project art

Mode: built-in image generation (not CLI), true transparent background requested. No originals overwritten.

## Crystal and ticket atlas

Saved: `Assets/Resources/Images/Premium/crystal-ticket-v1.png`

Prompt: Use case: stylized-concept. Asset type: transparent game UI sprite atlas for a sci-fi space shooter. Create ONE atlas with exactly TWO isolated icons side by side in two equal square cells, large transparent margins around both. LEFT: beautiful faceted cyan diamond crystal cluster, three luminous gems, polished turquoise glass with bright white facets and subtle blue glow. RIGHT: a tangible futuristic purple-and-gold gacha ticket, wide rectangular perforated admission coupon with clipped corners, gold metal rim, central luminous four-point star, tiny circuitry decorative lines. Orthographic clean inventory item art, high contrast readable at 48 pixels, detailed painted game sprite matching a dark navy space UI. No lettering, no numbers, no labels, no background, no shadow plane. True alpha transparent background. Keep every pixel of each item strictly inside its own half, no overlap.

## Story fleet atlas

Saved: `Assets/Resources/Images/Bosses/story-fleet-v1.png`

Generation prompt: Use case: stylized-concept. Asset type: one transparent sprite atlas of enemy spacecraft for a top-down 2D space shooter. Exactly SIX separate spacecraft arranged in a strict 3 columns by 2 rows equal cell grid. All ships face straight UP, symmetrical orthographic top view, no perspective, full ship visible. Each sprite fills only 65 percent of its cell with wide transparent gutters on all sides, no labels or text. Top row left: small crimson angular raider interceptor with twin guns; top middle: emerald armored insect drone with four fins; top right: purple manta-shaped stealth escort. Bottom row left: large dark steel siege dreadnought with orange reactor and broad armored wings; bottom middle: volcanic black-and-copper boss cruiser with fiery red core and forward horns; bottom right: regal void mothership in black violet and silver, luminous magenta core, six angular wing blades. Detailed crisp hand-painted game assets, readable silhouette and medium panel detail, luminous small engines, coherent navy sci-fi visual family. No weapon beams, no large halos, no scenery, no cast shadow, no backgrounds. True transparent alpha. Nothing crosses between cells.

Final edit prompt: Edit this six-ship atlas. Preserve all six spacecraft designs and their order. REQUIRED: remove ALL background haze/color/lighting completely to genuine transparent alpha, not black. Re-layout into exactly 3 columns x 2 EQUAL rows. Each ship must be fully contained within central 65% of its cell, with 17.5% blank transparent margin on ALL four sides. All ships must have equal displayed height regardless of boss or drone: top row centered at 25% canvas height, bottom row centered at 75%. No oversized bottom row. No glow outside the hull except tiny exhaust. No backdrops, no text. Strict orthographic top-down facing up.

Verification: actual output has unequal row heights, so runtime slicing uses measured transparent gutters x=512/1006 and top-down y=435 in 1536x1024 source, followed by alpha-bounds trim. Source pixels are not edited. Crystal/ticket atlas is 1774x887 with two equal cells. Sprite generation preserves alpha.
