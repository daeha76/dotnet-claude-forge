#!/usr/bin/env node
// knowledge 항목 URL 재취득 후 Claude Haiku로 요약 갱신
// Usage: node refresh.js
// Env: ANTHROPIC_API_KEY, ENTRIES (JSON array), KNOWLEDGE_DIR

const fs = require('fs');
const https = require('https');
const path = require('path');

const entries = JSON.parse(process.env.ENTRIES || '[]');
const today = new Date().toISOString().split('T')[0];
const knowledgeDir = process.env.KNOWLEDGE_DIR || path.join(process.cwd(), 'knowledge');
const idxPath = path.join(knowledgeDir, 'index.json');

function fetchUrl(url) {
  return new Promise((resolve) => {
    const req = https.get(url, { timeout: 15000 }, (res) => {
      if (res.statusCode >= 300 && res.statusCode < 400 && res.headers.location) {
        return fetchUrl(res.headers.location).then(resolve);
      }
      let body = '';
      res.on('data', d => { body += d; });
      res.on('end', () => resolve(body.substring(0, 8000)));
    });
    req.on('error', () => resolve(null));
    req.on('timeout', () => { req.destroy(); resolve(null); });
  });
}

function summarizeWithClaude(title, url, content) {
  const payload = JSON.stringify({
    model: 'claude-haiku-4-5-20251001',
    max_tokens: 1024,
    messages: [{
      role: 'user',
      content: [
        '다음 웹 페이지 내용을 3~5단락으로 요약해 주세요.',
        '제목: ' + title,
        'URL: ' + url,
        '',
        '내용:',
        content,
        '',
        '요약 형식:',
        '[3~5단락 요약]',
        '',
        '## 핵심 포인트',
        '- [포인트 1]',
        '- [포인트 2]',
        '- [포인트 3]'
      ].join('\n')
    }]
  });

  return new Promise((resolve, reject) => {
    const options = {
      hostname: 'api.anthropic.com',
      path: '/v1/messages',
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'x-api-key': process.env.ANTHROPIC_API_KEY,
        'anthropic-version': '2023-06-01',
        'Content-Length': Buffer.byteLength(payload)
      }
    };

    const req = https.request(options, (res) => {
      let body = '';
      res.on('data', d => { body += d; });
      res.on('end', () => {
        try {
          const parsed = JSON.parse(body);
          resolve(parsed.content && parsed.content[0] ? parsed.content[0].text : '');
        } catch (e) {
          resolve('');
        }
      });
    });
    req.on('error', reject);
    req.write(payload);
    req.end();
  });
}

async function main() {
  const idx = JSON.parse(fs.readFileSync(idxPath, 'utf8'));
  let updated = 0;
  let failed = 0;

  for (const entry of entries) {
    console.log('처리 중: ' + entry.id);

    const content = await fetchUrl(entry.source_url);
    if (!content) {
      console.log('  접근 불가: ' + entry.source_url);
      failed++;
      continue;
    }

    const summary = await summarizeWithClaude(entry.title, entry.source_url, content);
    if (!summary) {
      console.log('  요약 실패: ' + entry.id);
      failed++;
      continue;
    }

    const fileContent = [
      '---',
      'title: "' + entry.title + '"',
      'source_type: ' + entry.source_type,
      'source_url: ' + entry.source_url,
      'date: ' + today,
      'tags: [' + entry.tags.join(', ') + ']',
      '---',
      '',
      summary
    ].join('\n');

    fs.writeFileSync(path.join(knowledgeDir, entry.file), fileContent);

    const idxEntry = idx.entries.find(e => e.id === entry.id);
    if (idxEntry) idxEntry.date = today;

    console.log('  완료: ' + entry.id);
    updated++;

    await new Promise(r => setTimeout(r, 1000));
  }

  fs.writeFileSync(idxPath, JSON.stringify(idx, null, 2) + '\n');
  console.log('\n완료 — 갱신: ' + updated + ', 실패: ' + failed);

  if (failed > 0 && updated === 0) process.exit(1);
}

main().catch(e => { console.error(e); process.exit(1); });
