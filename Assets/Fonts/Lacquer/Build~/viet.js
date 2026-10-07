// Builds Lacquer VN: every Vietnamese letter Lacquer lacks, assembled from Lacquer's own strokes.
const T = require('./ttf.js');
const SRC = require('path').join(__dirname, '..', 'Lacquer-Regular.ttf');
const OUT = process.argv[2] || require('path').join(__dirname, '..', 'LacquerVN-Regular.ttf');
const f = T.readFont(SRC);
const glyphOf = ch => f.cmap.get(ch.codePointAt(0));
const shape = ch => T.outline(f, glyphOf(ch));

const map = (cs, fn) => cs.map(c => c.map(p => ({ ...fn(p), on: p.on })));
// Contours moved so their box is centred on x 0 with its bottom on y 0, then scaled.
function norm(cs, sx = 1, sy = sx) {
  const b = T.bbox(cs);
  return map(cs, p => ({ x: (p.x - b.cx) * sx, y: (p.y - b.y0) * sy }));
}
const place = (cs, x, y) => map(cs, p => ({ x: p.x + x, y: p.y + y }));
// A contour scaled by a negative factor on one axis runs the other way round; flip it back.
const largest = cs => [cs.reduce((a, c) => { const A = T.bbox([a]), B = T.bbox([c]); return B.w * B.h > A.w * A.h ? c : a; })];

const marks = {
  0x0300: norm(shape('\u0300')),
  0x0301: norm(shape('\u0301')),
  0x0302: norm(shape('\u0302')),
  0x0303: norm(shape('\u0303')),
  0x0306: norm(shape('u'), 0.62, 0.36),            // breve: a small cup cut from u
  0x0309: norm(largest(shape('?')), 0.42),          // hook above: the hook of ?, without its dot
  0x031B: norm(shape('\u2019'), 1.1),              // horn: the right quote's curl
  0x0323: norm(shape('.'), 1.15),                   // dot below
};
const bar = norm(shape('-'), 0.95, 0.34);           // stroke of Đ đ
const GAP = 30;

function compose(baseCh, ms) {
  let cs = shape(baseCh);
  const base = T.bbox(cs);
  let adv = f.advance[glyphOf(baseCh)];
  let top = base.y1;
  if (ms.includes(0x031B)) {
    const h = T.bbox(marks[0x031B]);
    const horn = place(marks[0x031B], base.x1 + h.w * 0.1, base.y1 - h.h * 0.7);
    cs = cs.concat(horn);
    adv = Math.max(adv, Math.round(T.bbox(horn).x1 + 25));
  }
  const tone = ms.find(m => [0x0300, 0x0301, 0x0303, 0x0309].includes(m));
  const shelf = ms.find(m => m === 0x0302 || m === 0x0306);
  if (shelf) {
    const s = place(marks[shelf], base.cx, top + GAP);
    cs = cs.concat(s);
    const sb = T.bbox(s);
    if (tone && shelf === 0x0302) {
      // Vietnamese sets the tone beside the circumflex, to its upper right, so the stack stays low.
      const t = norm(marks[tone], 0.72);
      const tb = T.bbox(t);
      cs = cs.concat(place(t, sb.x1 + tb.w * 0.15, sb.y0 + sb.h * 0.35));
    } else if (tone) {
      const t = norm(marks[tone], 0.8);
      cs = cs.concat(place(t, base.cx, sb.y1 + GAP * 0.6));
    }
  } else if (tone) {
    cs = cs.concat(place(marks[tone], base.cx, top + GAP));
  }
  if (ms.includes(0x0323)) {
    const d = T.bbox(marks[0x0323]);
    // Under the baseline, not under the drips that hang off most letters.
    // U, Y and I drip from their middle, so their dot sits to the right of the drip.
    const dx = 'UuYyIi'.includes(baseCh) ? base.w * 0.32 : 0;
    cs = cs.concat(place(marks[0x0323], base.cx + dx, -GAP - d.h));
  }
  return { cs, adv };
}

const vowels = 'aăâeêioôơuưy';
const tones = ['', '\u0300', '\u0301', '\u0309', '\u0303', '\u0323'];
const wanted = new Set(['Đ', 'đ']);
for (const v of vowels) for (const t of tones) for (const c of [v, v.toUpperCase()]) wanted.add((c + t).normalize('NFC'));

const made = [], kept = [];
for (const ch of wanted) {
  const code = ch.codePointAt(0);
  if (f.cmap.has(code)) { kept.push(ch); continue; }
  let g;
  if (ch === 'Đ' || ch === 'đ') {
    const baseCh = ch === 'Đ' ? 'D' : 'd';
    const cs = shape(baseCh), b = T.bbox(cs), bb = T.bbox(bar);
    // Across the stem at mid cap height; the box bottom is a drip, not the baseline.
    g = T.addGlyph(f, cs.concat(place(bar, b.x0 + 70, 300 - bb.h / 2)), f.advance[glyphOf(baseCh)]);
  } else {
    const parts = [...ch.normalize('NFD')];
    const baseCh = parts[0], ms = parts.slice(1).map(m => m.codePointAt(0));
    const { cs, adv } = compose(baseCh, ms);
    g = T.addGlyph(f, cs, adv);
  }
  f.cmap.set(code, g);
  made.push(ch);
}
// The combining marks Lacquer lacks, so a decomposed string still finds them.
for (const m of [0x0306, 0x0309, 0x031B, 0x0323]) if (!f.cmap.has(m)) {
  const b = T.bbox(marks[m]);
  f.cmap.set(m, T.addGlyph(f, place(marks[m], 0, m === 0x0323 ? -GAP - b.h : 680), 0));
}
f.renameFamily = 'Lacquer VN';
T.writeFont(f, OUT);
console.log('made', made.length, made.join(''));
console.log('kept', kept.length, kept.join(''));
