// Minimal TrueType reader/writer: enough to read glyph outlines, add simple glyphs and rewrite the font.
const fs = require('fs');

function readFont(path) {
  const buf = fs.readFileSync(path);
  const numTables = buf.readUInt16BE(4);
  const tables = {};
  for (let i = 0; i < numTables; i++) {
    const o = 12 + i * 16;
    const tag = buf.toString('latin1', o, o + 4);
    const off = buf.readUInt32BE(o + 8), len = buf.readUInt32BE(o + 12);
    tables[tag] = Buffer.from(buf.subarray(off, off + len));
  }
  const head = tables.head, maxp = tables.maxp, hhea = tables.hhea;
  const font = { tables, sfnt: buf.readUInt32BE(0) };
  font.unitsPerEm = head.readUInt16BE(18);
  font.locLong = head.readInt16BE(50) === 1;
  font.numGlyphs = maxp.readUInt16BE(4);
  font.numHMetrics = hhea.readUInt16BE(34);

  // hmtx
  const hm = tables.hmtx; font.advance = []; font.lsb = [];
  let last = 0;
  for (let g = 0; g < font.numGlyphs; g++) {
    if (g < font.numHMetrics) { last = hm.readUInt16BE(g * 4); font.advance.push(last); font.lsb.push(hm.readInt16BE(g * 4 + 2)); }
    else { font.advance.push(last); font.lsb.push(hm.readInt16BE(font.numHMetrics * 4 + (g - font.numHMetrics) * 2)); }
  }
  // loca + glyf raw
  const loca = tables.loca, glyf = tables.glyf; font.glyphData = [];
  for (let g = 0; g < font.numGlyphs; g++) {
    const a = font.locLong ? loca.readUInt32BE(g * 4) : loca.readUInt16BE(g * 2) * 2;
    const b = font.locLong ? loca.readUInt32BE(g * 4 + 4) : loca.readUInt16BE(g * 2 + 2) * 2;
    font.glyphData.push(Buffer.from(glyf.subarray(a, b)));
  }
  font.cmap = readCmap(tables.cmap);
  return font;
}

function readCmap(t) {
  const map = new Map();
  const n = t.readUInt16BE(2);
  let best = null;
  for (let i = 0; i < n; i++) {
    const pid = t.readUInt16BE(4 + i * 8), eid = t.readUInt16BE(6 + i * 8), off = t.readUInt32BE(8 + i * 8);
    const fmt = t.readUInt16BE(off);
    if (fmt === 12) best = { off, fmt };
    else if (fmt === 4 && (!best || best.fmt !== 12) && (pid === 3 || pid === 0)) best = { off, fmt };
  }
  const off = best.off;
  if (best.fmt === 4) {
    const segX2 = t.readUInt16BE(off + 6), seg = segX2 / 2;
    const ends = off + 14, starts = ends + segX2 + 2, deltas = starts + segX2, ranges = deltas + segX2;
    for (let s = 0; s < seg; s++) {
      const end = t.readUInt16BE(ends + s * 2), start = t.readUInt16BE(starts + s * 2);
      const delta = t.readInt16BE(deltas + s * 2), ro = t.readUInt16BE(ranges + s * 2);
      for (let c = start; c <= end && c !== 0xFFFF; c++) {
        let g;
        if (ro === 0) g = (c + delta) & 0xFFFF;
        else { const p = ranges + s * 2 + ro + (c - start) * 2; g = t.readUInt16BE(p); if (g) g = (g + delta) & 0xFFFF; }
        if (g) map.set(c, g);
      }
    }
  } else {
    const groups = t.readUInt32BE(off + 12);
    for (let i = 0; i < groups; i++) {
      const o = off + 16 + i * 12, s = t.readUInt32BE(o), e = t.readUInt32BE(o + 4), g0 = t.readUInt32BE(o + 8);
      for (let c = s; c <= e; c++) map.set(c, g0 + c - s);
    }
  }
  return map;
}

// Outline of a glyph as contours of {x, y, on}, composites flattened.
function outline(font, g, m = [1, 0, 0, 1, 0, 0]) {
  const d = font.glyphData[g];
  if (!d || d.length === 0) return [];
  const nc = d.readInt16BE(0);
  const tf = (x, y) => ({ x: m[0] * x + m[2] * y + m[4], y: m[1] * x + m[3] * y + m[5] });
  if (nc >= 0) {
    let p = 10; const ends = [];
    for (let i = 0; i < nc; i++) { ends.push(d.readUInt16BE(p)); p += 2; }
    const il = d.readUInt16BE(p); p += 2 + il;
    const n = nc ? ends[nc - 1] + 1 : 0, flags = [];
    while (flags.length < n) { const f = d[p++]; flags.push(f); if (f & 8) { let r = d[p++]; while (r--) flags.push(f); } }
    const xs = [], ys = []; let v = 0;
    for (const f of flags) { if (f & 2) { const dx = d[p++]; v += (f & 16) ? dx : -dx; } else if (!(f & 16)) { v += d.readInt16BE(p); p += 2; } xs.push(v); }
    v = 0;
    for (const f of flags) { if (f & 4) { const dy = d[p++]; v += (f & 32) ? dy : -dy; } else if (!(f & 32)) { v += d.readInt16BE(p); p += 2; } ys.push(v); }
    const contours = []; let s = 0;
    for (const e of ends) { const c = []; for (let i = s; i <= e; i++) c.push({ ...tf(xs[i], ys[i]), on: !!(flags[i] & 1) }); contours.push(c); s = e + 1; }
    return contours;
  }
  let p = 10, out = [], more = true;
  while (more) {
    const fl = d.readUInt16BE(p), gi = d.readUInt16BE(p + 2); p += 4;
    let a1, a2;
    if (fl & 1) { a1 = d.readInt16BE(p); a2 = d.readInt16BE(p + 2); p += 4; } else { a1 = d.readInt8(p); a2 = d.readInt8(p + 1); p += 2; }
    let t = [1, 0, 0, 1];
    if (fl & 8) { const s = d.readInt16BE(p) / 16384; p += 2; t = [s, 0, 0, s]; }
    else if (fl & 0x40) { t = [d.readInt16BE(p) / 16384, 0, 0, d.readInt16BE(p + 2) / 16384]; p += 4; }
    else if (fl & 0x80) { t = [d.readInt16BE(p) / 16384, d.readInt16BE(p + 2) / 16384, d.readInt16BE(p + 4) / 16384, d.readInt16BE(p + 6) / 16384]; p += 8; }
    const dx = (fl & 2) ? a1 : 0, dy = (fl & 2) ? a2 : 0;
    // child matrix then parent matrix
    const c = [t[0], t[1], t[2], t[3], dx, dy];
    const mm = [m[0] * c[0] + m[2] * c[1], m[1] * c[0] + m[3] * c[1], m[0] * c[2] + m[2] * c[3], m[1] * c[2] + m[3] * c[3], m[0] * c[4] + m[2] * c[5] + m[4], m[1] * c[4] + m[3] * c[5] + m[5]];
    out = out.concat(outline(font, gi, mm));
    more = !!(fl & 0x20);
  }
  return out;
}

function bbox(contours) {
  let x0 = Infinity, y0 = Infinity, x1 = -Infinity, y1 = -Infinity;
  for (const c of contours) for (const p of c) { x0 = Math.min(x0, p.x); y0 = Math.min(y0, p.y); x1 = Math.max(x1, p.x); y1 = Math.max(y1, p.y); }
  return { x0, y0, x1, y1, w: x1 - x0, h: y1 - y0, cx: (x0 + x1) / 2 };
}

function encodeSimple(contours) {
  const pts = contours.flat().map(p => ({ x: Math.round(p.x), y: Math.round(p.y), on: p.on }));
  if (!pts.length) return Buffer.alloc(0);
  const b = bbox([pts]);
  const parts = [];
  const hdr = Buffer.alloc(10);
  hdr.writeInt16BE(contours.length, 0); hdr.writeInt16BE(b.x0, 2); hdr.writeInt16BE(b.y0, 4); hdr.writeInt16BE(b.x1, 6); hdr.writeInt16BE(b.y1, 8);
  parts.push(hdr);
  const ends = Buffer.alloc(contours.length * 2 + 2); let n = -1;
  contours.forEach((c, i) => { n += c.length; ends.writeUInt16BE(n, i * 2); });
  ends.writeUInt16BE(0, contours.length * 2); // no instructions
  parts.push(ends);
  parts.push(Buffer.from(pts.map(p => p.on ? 1 : 0)));
  const xs = Buffer.alloc(pts.length * 2), ys = Buffer.alloc(pts.length * 2); let px = 0, py = 0;
  pts.forEach((p, i) => { xs.writeInt16BE(p.x - px, i * 2); ys.writeInt16BE(p.y - py, i * 2); px = p.x; py = p.y; });
  parts.push(xs, ys);
  let out = Buffer.concat(parts);
  if (out.length % 2) out = Buffer.concat([out, Buffer.alloc(1)]);
  return out;
}

function addGlyph(font, contours, advance) {
  const data = encodeSimple(contours);
  const b = contours.length ? bbox(contours) : { x0: 0 };
  font.glyphData.push(data); font.advance.push(advance); font.lsb.push(Math.round(b.x0 === Infinity ? 0 : b.x0));
  return font.glyphData.length - 1;
}

function checksum(buf) {
  const b = buf.length % 4 ? Buffer.concat([buf, Buffer.alloc(4 - buf.length % 4)]) : buf;
  let s = 0; for (let i = 0; i < b.length; i += 4) s = (s + b.readUInt32BE(i)) >>> 0; return s;
}

function buildCmap(map) {
  const codes = [...map.keys()].filter(c => c < 0xFFFF).sort((a, b) => a - b);
  const segs = [];
  for (const c of codes) { const l = segs[segs.length - 1]; if (l && c === l.end + 1 && map.get(c) === map.get(l.end) + 1) l.end = c; else segs.push({ start: c, end: c }); }
  segs.push({ start: 0xFFFF, end: 0xFFFF });
  const n = segs.length, len = 16 + n * 8;
  const f4 = Buffer.alloc(len);
  f4.writeUInt16BE(4, 0); f4.writeUInt16BE(len, 2); f4.writeUInt16BE(0, 4); f4.writeUInt16BE(n * 2, 6);
  const es = Math.floor(Math.log2(n)), sr = 2 * Math.pow(2, es);
  f4.writeUInt16BE(sr, 8); f4.writeUInt16BE(es, 10); f4.writeUInt16BE(n * 2 - sr, 12);
  segs.forEach((s, i) => {
    f4.writeUInt16BE(s.end, 14 + i * 2);
    f4.writeUInt16BE(s.start, 16 + n * 2 + i * 2);
    const delta = s.start === 0xFFFF ? 1 : (map.get(s.start) - s.start);
    f4.writeInt16BE(((delta + 32768) & 0xFFFF) - 32768, 16 + n * 4 + i * 2);
    f4.writeUInt16BE(0, 16 + n * 6 + i * 2);
  });
  const hdr = Buffer.alloc(4 + 16);
  hdr.writeUInt16BE(0, 0); hdr.writeUInt16BE(2, 2);
  hdr.writeUInt16BE(0, 4); hdr.writeUInt16BE(3, 6); hdr.writeUInt32BE(20, 8);
  hdr.writeUInt16BE(3, 12); hdr.writeUInt16BE(1, 14); hdr.writeUInt32BE(20, 16);
  return Buffer.concat([hdr, f4]);
}

function writeFont(font, path) {
  const t = font.tables;
  const n = font.glyphData.length;
  // glyf + loca (long)
  const loca = Buffer.alloc((n + 1) * 4); let off = 0;
  font.glyphData.forEach((g, i) => { loca.writeUInt32BE(off, i * 4); off += g.length; });
  loca.writeUInt32BE(off, n * 4);
  t.glyf = Buffer.concat(font.glyphData); t.loca = loca;
  t.head.writeInt16BE(1, 50);
  // hmtx: every glyph a long metric
  const hm = Buffer.alloc(n * 4);
  for (let g = 0; g < n; g++) { hm.writeUInt16BE(font.advance[g], g * 4); hm.writeInt16BE(font.lsb[g], g * 4 + 2); }
  t.hmtx = hm; t.hhea.writeUInt16BE(n, 34);
  // maxp
  t.maxp.writeUInt16BE(n, 4);
  if (t.maxp.length >= 32) {
    let mp = t.maxp.readUInt16BE(6), mc = t.maxp.readUInt16BE(8);
    for (const g of font.glyphData) if (g.length && g.readInt16BE(0) > 0) { const nc = g.readInt16BE(0); mc = Math.max(mc, nc); mp = Math.max(mp, g.readUInt16BE(10 + (nc - 1) * 2) + 1); }
    t.maxp.writeUInt16BE(mp, 6); t.maxp.writeUInt16BE(mc, 8);
  }
  t.cmap = buildCmap(font.cmap);
  // post v3: no glyph names
  const post = Buffer.from(t.post.subarray(0, 32)); post.writeUInt32BE(0x00030000, 0); t.post = post;
  for (const drop of ['DSIG', 'hdmx', 'LTSH', 'VDMX']) delete t[drop];
  if (font.renameFamily) t.name = renameFamily(t.name, font.renameFamily);

  t.head.writeUInt32BE(0, 8);
  const tags = Object.keys(t).sort();
  const nt = tags.length, es = Math.floor(Math.log2(nt)), sr = Math.pow(2, es) * 16;
  const dir = Buffer.alloc(12 + nt * 16);
  dir.writeUInt32BE(font.sfnt, 0); dir.writeUInt16BE(nt, 4); dir.writeUInt16BE(sr, 6); dir.writeUInt16BE(es, 8); dir.writeUInt16BE(nt * 16 - sr, 10);
  let pos = dir.length; const bodies = [];
  tags.forEach((tag, i) => {
    const b = t[tag]; const pad = (4 - b.length % 4) % 4;
    dir.write(tag, 12 + i * 16, 'latin1'); dir.writeUInt32BE(checksum(b), 16 + i * 16); dir.writeUInt32BE(pos, 20 + i * 16); dir.writeUInt32BE(b.length, 24 + i * 16);
    bodies.push(b, Buffer.alloc(pad)); pos += b.length + pad;
  });
  let out = Buffer.concat([dir, ...bodies]);
  const headPos = dir.readUInt32BE(20 + tags.indexOf('head') * 16);
  out.writeUInt32BE((0xB1B0AFBA - checksum(out)) >>> 0, headPos + 8);
  fs.writeFileSync(path, out);
}

function readNames(t) {
  const count = t.readUInt16BE(2), so = t.readUInt16BE(4), names = [];
  for (let i = 0; i < count; i++) {
    const o = 6 + i * 12, pid = t.readUInt16BE(o), eid = t.readUInt16BE(o + 2), lid = t.readUInt16BE(o + 4), id = t.readUInt16BE(o + 6), len = t.readUInt16BE(o + 8), off = t.readUInt16BE(o + 10);
    const raw = t.subarray(so + off, so + off + len);
    const str = pid === 3 || pid === 0 ? Buffer.from(raw).swap16().toString('utf16le') : raw.toString('latin1');
    names.push({ pid, eid, lid, id, str });
  }
  return names;
}

// Rewrites every name record, replacing the family in ids 1, 3, 4, 6, 16.
function renameFamily(t, family) {
  const names = readNames(t).map(n => {
    if (n.id === 1 || n.id === 16) n.str = family;
    else if (n.id === 4) n.str = family + ' Regular';
    else if (n.id === 6) n.str = family.replace(/\s/g, '') + '-Regular';
    else if (n.id === 3) n.str = family + ';Regular';
    return n;
  });
  const recs = Buffer.alloc(6 + names.length * 12); const strs = [];
  recs.writeUInt16BE(0, 0); recs.writeUInt16BE(names.length, 2); recs.writeUInt16BE(recs.length, 4);
  let off = 0;
  names.forEach((n, i) => {
    const b = n.pid === 3 || n.pid === 0 ? Buffer.from(n.str, 'utf16le').swap16() : Buffer.from(n.str, 'latin1');
    const o = 6 + i * 12;
    recs.writeUInt16BE(n.pid, o); recs.writeUInt16BE(n.eid, o + 2); recs.writeUInt16BE(n.lid, o + 4); recs.writeUInt16BE(n.id, o + 6); recs.writeUInt16BE(b.length, o + 8); recs.writeUInt16BE(off, o + 10);
    strs.push(b); off += b.length;
  });
  return Buffer.concat([recs, ...strs]);
}

module.exports = { readFont, writeFont, outline, bbox, addGlyph, readNames };
