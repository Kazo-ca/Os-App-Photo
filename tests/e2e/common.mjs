// Shared helpers of the end-to-end runs: the browser, the Kazo Photo windows and what runs in
// their iframes (PhotoCraft's page).
import { chromium } from 'playwright';
import { boot } from './host.mjs';

// PhotoCraft's first start downloads about 40 MB of .wasm from the local host and compiles it.
export const START_TIMEOUT_MS = 120_000;

// A software WebGPU adapter (SwiftShader), so both of PhotoCraft's renderers can be tried.
export const launchBrowser = () => chromium.launch({
    args: ['--enable-unsafe-webgpu', '--enable-features=Vulkan', '--use-vulkan=swiftshader', '--use-webgpu-adapter=swiftshader', '--use-angle=vulkan'],
});

/** A fresh page with the OS booted; `errors` collects the page's uncaught errors. */
export async function bootPage(browser, baseUrl) {
    const page = await browser.newPage({ viewport: { width: 1400, height: 900 } });
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    await boot(page, baseUrl);
    return { page, errors };
}

/** Kazo Photo's OS windows, in opening order. */
export const photoWindows = page => page.locator('article[data-window-id]', { has: page.locator('.photo-window') });

/** The iframe element of the window at `index` (opening order). */
export const iframeAt = (page, index) => photoWindows(page).nth(index).locator('iframe.craft-frame-iframe');

/** The PhotoCraft frame of the window at `index`, once the module put the iframe there. */
export async function frameAt(page, index) {
    const iframe = iframeAt(page, index);
    await iframe.waitFor({ state: 'attached', timeout: START_TIMEOUT_MS });
    return (await iframe.elementHandle()).contentFrame();
}

/**
 * Waits until PhotoCraft runs in a frame: its start code removes #photocraft_loading once eframe
 * started on the canvas (apps/photocraft-web/src/web.rs), and the canvas then has a size.
 */
export async function waitForPhotoCraft(frame) {
    await frame.waitForFunction(() => {
        const canvas = document.getElementById('photocraft_canvas');
        return document.getElementById('photocraft_loading') === null && canvas !== null && canvas.width > 0;
    }, undefined, { timeout: START_TIMEOUT_MS });
}

/** The renderer PhotoCraft's canvas uses: `webgpu`, `webgl2` or null. */
export const rendererOf = frame => frame.evaluate(() => {
    const canvas = document.getElementById('photocraft_canvas');
    if (canvas.getContext('webgpu')) {
        return 'webgpu';
    }
    return canvas.getContext('webgl2') ? 'webgl2' : null;
});
