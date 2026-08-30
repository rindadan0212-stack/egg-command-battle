import fs from 'node:fs/promises';
import { createHash } from 'node:crypto';
import path from 'node:path';
import process from 'node:process';
import { fileURLToPath } from 'node:url';

import { chromium } from 'playwright';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const pixelizerUrl = process.env.PIXELIZER_URL ?? 'http://127.0.0.1:5818/index.html';
const sourceDir = path.join(root, 'art', 'source', 'user-2026-08-29');
const sourceSpecs = {
  statusSheet: ['status-sheet.png', '93896870a11c5be13b9f096d84ee1aeb5f45dadfe80d8bda416c493d2f1bc29e'],
  graveFoe: ['grave-foe.png', '47c9b0403904ea3c5cbaeaaef5f253b6681ed6c0f300e1c958c9f1abbae72ee4'],
  graveAlly: ['grave-ally.png', '69eb6547ab9a908d18967913a3295df827cc457197e0311b91e8f998d200a56e'],
};
const sources = {};
for (const [key, [name, expectedHash]] of Object.entries(sourceSpecs)) {
  const bytes = await fs.readFile(path.join(sourceDir, name));
  const actualHash = createHash('sha256').update(bytes).digest('hex');
  if (actualHash !== expectedHash) {
    throw new Error(`${name} が取り込み時の原画と一致しません: ${actualHash}`);
  }
  sources[key] = `data:image/png;base64,${bytes.toString('base64')}`;
}

const statusNames = [
  'status-atk-up', 'status-def-up', 'status-spd-up', 'status-regen',
  'status-atk-down', 'status-def-down', 'status-spd-down', 'status-poison',
  'status-stun', 'status-sleep', 'status-guts', 'status-block',
  'status-immune', 'status-shield', 'status-taunt', 'status-seal',
];

const species = [
  { id: 'Hirabe', file: 'hirabe', variants: 3, svg: hirabeSvg() },
  { id: 'Togeru', file: 'togeru', variants: 3, svg: togeruSvg() },
  { id: 'Marumi', file: 'marumi', variants: 3, svg: marumiSvg() },
  { id: 'Kibane', file: 'kibane', variants: 4, svg: kibaneSvg() },
  { id: 'Iwao', file: 'iwao', variants: 4, svg: iwaoSvg() },
  { id: 'Homura', file: 'homura', variants: 4, svg: homuraSvg() },
  { id: 'Nushi', file: 'nushi', variants: 2, svg: nushiSvg() },
];

const iconDir = path.join(root, 'assets', 'ui', 'icon');
const paintDir = path.join(root, 'assets', 'ui', 'paint');
const archiveDir = path.join(root, 'art', 'status-pixel');
const provisionalDir = path.join(root, 'art', 'provisional-sprites');
const extrasDir = path.join(root, 'art', 'extras');

await Promise.all([iconDir, paintDir, archiveDir, provisionalDir, extrasDir].map(dir => fs.mkdir(dir, { recursive: true })));

const browser = await chromium.launch({ headless: true });
const page = await browser.newPage({ viewport: { width: 1280, height: 900 } });
await page.goto(pixelizerUrl, { waitUntil: 'networkidle' });

try {
  for (let i = 0; i < statusNames.length; i += 1) {
    const col = i % 4;
    const row = Math.floor(i / 4);
    // 原画は1536幅を4等分した位置ではなく、約306px間隔でカードが並ぶ。
    // 等分すると全カードの左右を切るため、実カード中心に合わせた固定256px枠を使う。
    const crop = {
      x: [184, 491, 797, 1098][col],
      y: [0, 255, 498, 746][row],
      width: 256,
      height: row === 2 ? 248 : 256,
    };
    const source = sources.statusSheet;
    const runtime = await pixelize({ source, crop, width: 16, height: 16, palette: 16, despeckle: true, outline: 1 });
    const archive = await pixelize({ source, crop, width: 32, height: 32, palette: 24, despeckle: true, outline: 1 });
    await writePng(path.join(iconDir, `${statusNames[i]}.png`), runtime.bytes);
    await writePng(path.join(archiveDir, `${String(i + 1).padStart(2, '0')}-${statusNames[i]}.png`), archive.bytes);
  }

  const generatedStatuses = [
    ['status-anchor', statusCardSvg('#226c8c', '#73dbff', anchorMark())],
    ['status-invincible', statusCardSvg('#725311', '#ffe069', invincibleMark())],
    ['status-counter', statusCardSvg('#7a2531', '#ff8975', counterMark())],
  ];
  for (let i = 0; i < generatedStatuses.length; i += 1) {
    const [name, svg] = generatedStatuses[i];
    const runtime = await pixelize({ svg, width: 16, height: 16, palette: 12, despeckle: true, outline: 1 });
    const archive = await pixelize({ svg, width: 32, height: 32, palette: 16, despeckle: true, outline: 1 });
    await writePng(path.join(iconDir, `${name}.png`), runtime.bytes);
    await writePng(path.join(archiveDir, `${String(17 + i).padStart(2, '0')}-${name}.png`), archive.bytes);
  }

  // 墓はユーザー側で透過・ドット化済み。再減色や縮小を掛けると、細い外周まで
  // 背景として消してしまうため、原画のalphaと輪郭をそのまま採用する。
  for (const [name, sourceKey] of [['grave-foe', 'graveFoe'], ['grave-ally', 'graveAlly']]) {
    const [sourceName] = sourceSpecs[sourceKey];
    await fs.copyFile(path.join(sourceDir, sourceName), path.join(paintDir, `${name}.png`));
  }

  const generatedSpecies = [];
  for (const entry of species) {
    const result = await pixelize({ svg: entry.svg, width: 64, height: 64, palette: 16, despeckle: true, outline: 1 });
    await writePng(path.join(provisionalDir, `${entry.file}-64.png`), result.bytes);
    generatedSpecies.push({ ...entry, art: indexedArt(result.pixels, 64, 64) });
  }
  await fs.writeFile(
    path.join(root, 'game', 'EggCommand.Core', 'ProvisionalSpeciesArt.Generated.cs'),
    generatedCSharp(generatedSpecies),
    'utf8',
  );

  const extras = [
    ['unknown-egg-32.png', unknownEggSvg(), 32, 32, 10],
    ['hatch-shell-fragments-64x48.png', shellFragmentsSvg(), 64, 48, 10],
    ['hit-spark-01-16.png', hitSparkSvg(0), 16, 16, 8],
    ['hit-spark-02-16.png', hitSparkSvg(1), 16, 16, 8],
    ['hit-spark-03-16.png', hitSparkSvg(2), 16, 16, 8],
  ];
  for (const [name, svg, width, height, palette] of extras) {
    const result = await pixelize({ svg, width, height, palette, despeckle: false, outline: 1 });
    await writePng(path.join(extrasDir, name), result.bytes);
  }
} finally {
  await browser.close();
}

console.log('Prepared 19 status icons, 2 graves, 7 provisional species sprites, and 5 reusable extras.');

async function pixelize({ source, svg, crop, width, height, palette, despeckle, outline, clearBorderBlack = false }) {
  return page.evaluate(async options => {
    const img = new Image();
    let objectUrl = '';
    if (options.svg) {
      objectUrl = URL.createObjectURL(new Blob([options.svg], { type: 'image/svg+xml' }));
      img.src = objectUrl;
    } else {
    img.src = options.source.startsWith("data:")
      ? options.source
      : `${options.source}?v=${Date.now()}`;
    }
    await new Promise((resolve, reject) => {
      img.onload = resolve;
      img.onerror = () => reject(new Error(`画像を読めません: ${img.src}`));
    });

    const sourceCanvas = document.createElement('canvas');
    const crop = options.crop ?? { x: 0, y: 0, width: img.naturalWidth, height: img.naturalHeight };
    sourceCanvas.width = crop.width;
    sourceCanvas.height = crop.height;
    const ctx = sourceCanvas.getContext('2d', { willReadFrequently: true });
    ctx.drawImage(img, crop.x, crop.y, crop.width, crop.height, 0, 0, crop.width, crop.height);
    if (objectUrl) URL.revokeObjectURL(objectUrl);

    if (options.clearBorderBlack) {
      const image = ctx.getImageData(0, 0, sourceCanvas.width, sourceCanvas.height);
      const data = image.data;
      const seen = new Uint8Array(sourceCanvas.width * sourceCanvas.height);
      const queue = [];
      const add = (x, y) => {
        if (x < 0 || y < 0 || x >= sourceCanvas.width || y >= sourceCanvas.height) return;
        const p = y * sourceCanvas.width + x;
        if (seen[p]) return;
        const i = p * 4;
        if (data[i] > 18 || data[i + 1] > 18 || data[i + 2] > 18) return;
        seen[p] = 1;
        queue.push(p);
      };
      for (let x = 0; x < sourceCanvas.width; x += 1) { add(x, 0); add(x, sourceCanvas.height - 1); }
      for (let y = 0; y < sourceCanvas.height; y += 1) { add(0, y); add(sourceCanvas.width - 1, y); }
      for (let head = 0; head < queue.length; head += 1) {
        const p = queue[head];
        data[p * 4 + 3] = 0;
        const x = p % sourceCanvas.width;
        const y = Math.floor(p / sourceCanvas.width);
        add(x - 1, y); add(x + 1, y); add(x, y - 1); add(x, y + 1);
      }
      ctx.putImageData(image, 0, 0);
    }

    state.sourceImg = sourceCanvas;
    state.grid = null;
    el.paletteLimit.value = String(options.palette);
    el.transparentBg.checked = false;
    el.dithering.checked = false;
    el.modeDownscale.checked = true;
    el.despeckleOpt.checked = options.despeckle;
    el.outlineStrength.value = String(options.outline);
    // Pixelizer は手作業保護のため、絵が在る状態でのサイズ変更を confirm する。
    // このバッチは毎回原画から作り直すので、旧フレームを先に明示破棄する。
    state.pixels = null;
    state.layers = [];
    applyCanvasSize(options.width, options.height);
    convert();

    const blob = await frameToPngBlob(state.currentFrame, 1);
    const bytes = Array.from(new Uint8Array(await blob.arrayBuffer()));
    return { bytes, pixels: Array.from(state.pixels), width: state.width, height: state.height };
  }, { source, svg, crop, width, height, palette, despeckle, outline, clearBorderBlack });
}

async function writePng(file, bytes) {
  await fs.writeFile(file, Buffer.from(bytes));
}

function indexedArt(pixels, width, height) {
  const counts = new Map();
  for (let i = 0; i < pixels.length; i += 4) {
    if (pixels[i + 3] < 128) continue;
    const key = `${pixels[i]},${pixels[i + 1]},${pixels[i + 2]}`;
    counts.set(key, (counts.get(key) ?? 0) + 1);
  }
  const colors = [...counts.entries()].sort((a, b) => b[1] - a[1]).map(([key]) => key.split(',').map(Number));
  const chars = '.123456789abcdefghijklmnopqrstuvwxyz';
  if (colors.length >= chars.length) throw new Error(`PixelSprite の色数上限35を超えました: ${colors.length}`);
  const index = new Map(colors.map((rgb, i) => [rgb.join(','), i + 1]));
  const rows = [];
  for (let y = 0; y < height; y += 1) {
    let row = '';
    for (let x = 0; x < width; x += 1) {
      const p = (y * width + x) * 4;
      row += pixels[p + 3] < 128 ? '.' : chars[index.get(`${pixels[p]},${pixels[p + 1]},${pixels[p + 2]}`)];
    }
    rows.push(row);
  }
  return { colors, rows };
}

function generatedCSharp(entries) {
  const lines = [
    '// <auto-generated>',
    '// 64×64の仮絵。tools/prepare-user-art.mjs がプロジェクトの Pixelizer を通して生成する。',
    '// 本番キャラクター絵へ差し替えるまでの戦闘・編成画面用。',
    '// </auto-generated>',
    'using System.Collections.Generic;',
    '',
    'namespace EggCommand.Core',
    '{',
    '    internal static class ProvisionalSpeciesArt',
    '    {',
  ];
  for (const entry of entries) {
    lines.push(`        internal static readonly PixelSprite ${entry.id}Sprite = PixelSprite.Parse(new[]`, '        {');
    for (const row of entry.art.rows) lines.push(`            "${row}",`);
    lines.push('        });', '');
    lines.push(`        internal static readonly IReadOnlyList<Palette> ${entry.id}Palettes = new[]`, '        {');
    for (let variant = 0; variant < entry.variants; variant += 1) {
      const shift = variant === 0 ? 0 : [140, 270, 48][variant - 1];
      const colors = entry.art.colors.map(rgb => `"${toHex(shiftHue(rgb, shift))}"`).join(', ');
      lines.push(`            new Palette(new[] { ${colors} }),`);
    }
    lines.push('        };', '');
  }
  lines.push('    }', '}', '');
  return lines.join('\n');
}

function shiftHue([r, g, b], shift) {
  if (!shift) return [r, g, b];
  r /= 255; g /= 255; b /= 255;
  const max = Math.max(r, g, b); const min = Math.min(r, g, b); const d = max - min;
  const l = (max + min) / 2;
  const s = d === 0 ? 0 : d / (1 - Math.abs(2 * l - 1));
  if (s < 0.12 || l < 0.08) return [Math.round(r * 255), Math.round(g * 255), Math.round(b * 255)];
  let h = max === r ? 60 * (((g - b) / d) % 6) : max === g ? 60 * ((b - r) / d + 2) : 60 * ((r - g) / d + 4);
  h = (h + shift + 360) % 360;
  const c = (1 - Math.abs(2 * l - 1)) * s;
  const x = c * (1 - Math.abs((h / 60) % 2 - 1));
  const m = l - c / 2;
  const [rr, gg, bb] = h < 60 ? [c, x, 0] : h < 120 ? [x, c, 0] : h < 180 ? [0, c, x] : h < 240 ? [0, x, c] : h < 300 ? [x, 0, c] : [c, 0, x];
  return [Math.round((rr + m) * 255), Math.round((gg + m) * 255), Math.round((bb + m) * 255)];
}

function toHex(rgb) { return `#${rgb.map(v => v.toString(16).padStart(2, '0')).join('')}`; }
function svg(body, width = 512, height = 512) { return `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}" viewBox="0 0 ${width} ${height}">${body}</svg>`; }
function outline() { return 'stroke="#211b26" stroke-width="22" stroke-linejoin="round" stroke-linecap="round"'; }

function hirabeSvg() { return svg(`<g transform="translate(0 24)"><path ${outline()} fill="#4aa7ae" d="M52 337Q97 234 220 215Q346 194 445 272L470 336Q393 418 241 427Q100 428 52 337Z"/><path fill="#8ed6cd" d="M93 310Q181 238 331 257Q201 271 137 346Z"/><circle cx="350" cy="292" r="21" fill="#f4e6bc"/><circle cx="358" cy="295" r="9" fill="#211b26"/><path ${outline()} fill="#f1bd5b" d="M101 351L48 402L132 391Z"/></g>`); }
function togeruSvg() { return svg(`<path ${outline()} fill="#c84b52" d="M69 354L101 291L62 245L132 232L121 164L189 189L220 110L265 178L331 126L345 206L430 190L404 262L470 304L406 350L432 421L341 404L291 458L239 407L160 440L145 374Z"/><ellipse cx="289" cy="297" rx="119" ry="96" fill="#e96c59"/><circle cx="331" cy="273" r="20" fill="#f7dda2"/><circle cx="339" cy="277" r="9" fill="#211b26"/>`); }
function marumiSvg() { return svg(`<path ${outline()} fill="#dfe9e2" d="M256 64Q353 106 423 222Q441 351 338 430Q232 474 124 400Q61 310 105 194Q164 92 256 64Z"/><path fill="#f8f2d0" d="M164 173Q241 91 332 142Q227 152 158 258Z"/><path fill="#a6c5c0" d="M118 342Q246 399 377 326Q350 423 246 438Q154 424 118 342Z"/><circle cx="326" cy="250" r="19" fill="#211b26"/>`); }
function kibaneSvg() { return svg(`<g transform="translate(0 48)"><path ${outline()} fill="#493a8e" d="M58 371Q90 191 244 98Q344 54 454 83Q398 144 365 227Q427 229 470 278Q400 353 316 369Q205 453 58 371Z"/><path fill="#8d63bd" d="M117 337Q169 196 315 125Q244 229 263 347Z"/><path ${outline()} fill="#c9b3e9" d="M278 213L395 249L335 335L246 298Z"/><circle cx="339" cy="270" r="18" fill="#f3c563"/></g>`); }
function iwaoSvg() { return svg(`<g transform="translate(0 48)"><path ${outline()} fill="#787877" d="M84 407L112 205L192 90L334 105L431 230L449 407Z"/><path fill="#aaa797" d="M131 221L207 111L257 117L191 267Z"/><path fill="#55565c" d="M287 111L339 237L282 269L319 327L278 411L366 411L410 240L334 133Z"/><path ${outline()} fill="none" d="M255 151L226 248L285 282L235 367"/><circle cx="339" cy="246" r="17" fill="#e4d295"/></g>`); }
function homuraSvg() { return svg(`<path ${outline()} fill="#dc4b2f" d="M257 50Q324 142 306 211Q374 181 407 113Q449 238 403 337Q363 433 259 463Q149 442 102 350Q61 267 140 152Q146 236 195 252Q171 151 257 50Z"/><path fill="#f59635" d="M251 188Q329 282 292 391Q207 408 171 337Q147 285 220 220Q215 287 251 302Q279 256 251 188Z"/><path fill="#ffe173" d="M242 279Q290 340 252 400Q195 373 211 325Z"/><circle cx="335" cy="271" r="17" fill="#211b26"/>`); }
function nushiSvg() { return svg(`<g transform="translate(0 48)"><path ${outline()} fill="#6c385d" d="M70 405L91 201L151 126L210 160L256 62L302 160L369 120L424 209L448 405Z"/><path ${outline()} fill="#d09a42" d="M104 187L95 82L194 139L256 48L318 139L418 82L402 190Z"/><path fill="#9d5474" d="M126 237Q226 153 373 225L390 380L111 380Z"/><path ${outline()} fill="#eee4cc" d="M197 243L256 285L316 243L355 311L256 366L158 310Z"/><circle cx="256" cy="303" r="21" fill="#d34c4c"/></g>`); }

function statusCardSvg(bg, glow, mark) { return svg(`<rect x="28" y="28" width="456" height="456" rx="92" fill="${bg}" stroke="#201827" stroke-width="22"/><path fill="${glow}" opacity=".35" d="M65 105Q174 35 421 61L92 247Z"/>${mark}`); }
function anchorMark() { return `<path fill="none" stroke="#d9f5ff" stroke-width="42" stroke-linejoin="round" stroke-linecap="round" d="M256 126V353M185 169H327M116 287Q126 402 256 409Q386 402 396 287M116 287L78 326M396 287L434 326"/><circle cx="256" cy="116" r="48" fill="none" stroke="#d9f5ff" stroke-width="34"/>`; }
function invincibleMark() { return `<path ${outline()} fill="#fff2a8" d="M256 86L357 138L404 245Q388 368 256 431Q124 368 108 245L155 138Z"/><path fill="#ffd73f" d="M256 135L322 170L352 248Q331 329 256 369Q181 329 160 248L190 170Z"/><path fill="#fff8d5" d="M256 167L278 232L347 234L291 274L311 340L256 301L201 340L221 274L165 234L234 232Z"/>`; }
function counterMark() { return `<path fill="none" stroke="#fff0d7" stroke-width="46" stroke-linejoin="round" stroke-linecap="round" d="M125 189Q210 85 343 155Q407 188 421 253M421 253L363 197M421 253L355 290M387 333Q302 437 169 367Q105 334 91 269M91 269L149 325M91 269L157 232"/>`; }
function unknownEggSvg() { return svg(`<path ${outline()} fill="#756c78" d="M256 61Q359 61 414 209Q472 365 350 444Q256 496 162 444Q40 365 98 209Q153 61 256 61Z"/><path fill="#aca4ad" d="M174 132Q250 73 320 111Q201 190 159 319Z"/><path fill="#2c2630" d="M216 209Q231 175 270 175Q319 175 319 218Q319 251 286 271Q260 286 260 319H218Q216 263 265 237Q282 228 282 215Q282 199 263 198Q241 198 233 222Z"/><rect x="217" y="344" width="45" height="47" fill="#2c2630"/>`); }
function shellFragmentsSvg() { return svg(`<path ${outline()} fill="#efe3c2" d="M45 326L135 126L225 285L177 424Z"/><path ${outline()} fill="#dbc893" d="M238 424L284 154L377 302L340 437Z"/><path ${outline()} fill="#f7edcf" d="M355 328L449 198L478 398L394 431Z"/>`, 512, 384); }
function hitSparkSvg(frame) { const s = [0, 28, 52][frame]; return svg(`<path fill="#fff1a8" stroke="#5d3030" stroke-width="24" stroke-linejoin="round" d="M256 ${52 + s}L298 206L442 118L349 256L472 310L318 302L349 460L256 334L172 462L198 307L42 335L166 256L65 145L211 205Z"/>`); }
