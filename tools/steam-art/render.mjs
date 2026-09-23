// Renders art.html into the PNGs the launcher embeds for its Steam library entry.
// Usage (from this folder): npm install playwright && node render.mjs
import { chromium } from 'playwright';
import { writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

const here = new URL('.', import.meta.url);
const out = fileURLToPath(new URL('../../src/ERSC.Launcher.Core/SteamArt/', here));
const browser = await chromium.launch();
const page = await browser.newPage();
await page.goto(new URL('art.html', here).href, { waitUntil: 'networkidle' });
const images = await page.evaluate(() => window.render());
for (const [name, url] of Object.entries(images)) writeFileSync(out + name + '.png', Buffer.from(url.split(',')[1], 'base64'));
await browser.close();
console.log('Wrote', Object.keys(images).map(n => n + '.png').join(', '), 'to', out);
