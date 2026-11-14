// The picture of the installed run and of the Store screenshots: a landscape drawn in code (sky
// gradient, sun, three mountain ranges, a lake), so no third-party image appears anywhere.

/** Draws the landscape in the browser page and returns it as PNG bytes. */
export async function landscapePng(page, width = 1600, height = 1000) {
    const base64 = await page.evaluate(async ([w, h]) => {
        const canvas = new OffscreenCanvas(w, h);
        const g = canvas.getContext('2d');
        const sky = g.createLinearGradient(0, 0, 0, h * 0.62);
        sky.addColorStop(0, '#1e1b4b');
        sky.addColorStop(0.55, '#7c3aed');
        sky.addColorStop(1, '#fb923c');
        g.fillStyle = sky;
        g.fillRect(0, 0, w, h);

        const sun = g.createRadialGradient(w * 0.68, h * 0.5, 0, w * 0.68, h * 0.5, h * 0.22);
        sun.addColorStop(0, '#fef3c7');
        sun.addColorStop(0.45, '#fde68a');
        sun.addColorStop(1, 'rgba(253, 230, 138, 0)');
        g.fillStyle = sun;
        g.fillRect(0, 0, w, h);

        // Ranges from far to near: a deterministic ridge line for each.
        const ranges = [['#4c1d95', 0.46, 0.09, 3], ['#312e81', 0.52, 0.12, 5], ['#0f172a', 0.6, 0.1, 7]];
        for (const [color, base, amplitude, seed] of ranges) {
            g.fillStyle = color;
            g.beginPath();
            g.moveTo(0, h);
            for (let x = 0; x <= w; x += 8) {
                const t = x / w;
                const y = base - amplitude * (Math.sin(t * 7 + seed) * 0.6 + Math.sin(t * 17 + seed * 2) * 0.3 + Math.sin(t * 41 + seed) * 0.1);
                g.lineTo(x, y * h);
            }
            g.lineTo(w, h);
            g.closePath();
            g.fill();
        }

        // The lake, with the sun's reflection.
        const lake = g.createLinearGradient(0, h * 0.66, 0, h);
        lake.addColorStop(0, '#1e3a8a');
        lake.addColorStop(1, '#020617');
        g.fillStyle = lake;
        g.fillRect(0, h * 0.66, w, h * 0.34);
        g.fillStyle = 'rgba(253, 230, 138, 0.35)';
        for (let row = 0; row < 14; row++) {
            const y = h * 0.68 + row * h * 0.02;
            const half = w * 0.06 * (1 - row / 16);
            g.fillRect(w * 0.68 - half, y, half * 2, h * 0.006);
        }

        const blob = await canvas.convertToBlob({ type: 'image/png' });
        const bytes = new Uint8Array(await blob.arrayBuffer());
        let text = '';
        for (let i = 0; i < bytes.length; i += 0x8000) text += String.fromCharCode(...bytes.subarray(i, i + 0x8000));
        return btoa(text);
    }, [width, height]);
    return Buffer.from(base64, 'base64');
}
