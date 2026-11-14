// The Kazo OS Store's screenshots (store/screenshots/), taken by install-and-open.mjs when it is
// given a screenshots folder, in the OS where Kazo Photo was just installed. The picture is the one
// sample-image.mjs draws (our own landscape), so no third-party content appears.

const STEP_MS = 60_000;

/** egui paints on input: move the pointer over the canvas so the last frame is drawn. */
async function repaint(page, id, windowById) {
    const box = await windowById(page, id).locator('iframe.craft-frame-iframe').boundingBox();
    for (let step = 0; step < 6; step++) {
        await page.mouse.move(box.x + box.width / 2 + step * 7, box.y + box.height - 80 - step * 5);
        await page.waitForTimeout(400);
    }
}

export async function takeScreenshots({ page, second, reopened, menu, shoot, windowById, openApp, HOME }) {
    const chrome = windowById(page, second).locator('header.window-chrome').first();

    // The File Explorer of the journey is reopened at the end, listing the files.
    for (const close of await page.locator('article[data-window-id]', { hasText: 'File Explorer' }).locator('header.window-chrome .chrome-button-close').all()) {
        await close.dispatchEvent('click');
    }

    // 1. The layered landscape, the window maximized, its layers listed.
    await chrome.dblclick({ position: { x: 300, y: 12 } });
    await page.waitForTimeout(2000);
    await menu(reopened, 'view.fitOnScreen').catch(() => {});
    await repaint(page, second, windowById);
    await shoot(page, '1.png');

    // 2. In a window of the desktop, Save As: the OS save dialog. (Not Save a Copy: PhotoCraft's
    // status bar already says "Saved a copy" while the OS dialog is still open.)
    await chrome.dblclick({ position: { x: 300, y: 12 } });
    await page.waitForTimeout(2000);
    await menu(reopened, 'view.fitOnScreen').catch(() => {});
    await repaint(page, second, windowById);
    await menu(reopened, 'file.saveAs');
    const dialog = page.locator('section.file-dialog', { hasText: 'Save the image' });
    await dialog.waitFor({ state: 'visible', timeout: STEP_MS });
    await dialog.getByLabel('File name').fill('Lake at sunset.psd');
    await shoot(page, '2.png');
    await dialog.getByRole('button', { name: 'Cancel' }).click();
    await dialog.waitFor({ state: 'hidden', timeout: STEP_MS });

    // 3. The File Explorer in front: .psd files open in Kazo Photo with a double-click.
    await openApp(page, 'File Explorer');
    const row = page.locator(`tr[data-item-path="${HOME}/landscape.psd"]`).first();
    await row.waitFor({ timeout: STEP_MS });
    await row.click({ position: { x: 40, y: 10 } });
    await shoot(page, '3.png');
}
