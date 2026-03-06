#!/usr/bin/env node
// knowledge/index.json에서 stale 항목 추출
// Usage: node find-stale.js [all|stale] [cutoff-days]

const fs = require('fs');
const path = require('path');

const mode = process.argv[2] || 'stale';
const cutoffDays = parseInt(process.argv[3] || '30', 10);

const idxPath = path.join(process.cwd(), 'knowledge', 'index.json');
const idx = JSON.parse(fs.readFileSync(idxPath, 'utf8'));

let entries;
if (mode === 'all') {
  entries = idx.entries.filter(e => e.source_type !== 'internal');
} else {
  const cutoff = new Date();
  cutoff.setDate(cutoff.getDate() - cutoffDays);
  entries = idx.entries.filter(e =>
    e.source_type !== 'internal' &&
    new Date(e.date) < cutoff
  );
}

process.stdout.write(JSON.stringify(entries));
