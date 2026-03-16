#!/usr/bin/env node
// knowledge 항목 URL 재취득 후 Claude Haiku로 요약 갱신
// Usage: node refresh.js
// Env: ANTHROPIC_API_KEY, ENTRIES (JSON array), KNOWLEDGE_DIR

const fs = require('fs');
const https = require('https');
const http = require('http');
const path = require('path');

const entries = JSON.parse(process.env.ENTRIES || '[]');
const today = new Date().toISOString().split('T')[0];
const knowledgeDir = process.env.KNOWLEDGE_DIR || path.join(process.cwd(), 'knowledge');
const idxPath = path.join(knowledgeDir, 'index.json');

function fetchUrl(url, redirectCount = 0) {
  if (redirectCount > 5) {
    console.log('  [fetchUrl] 리다이렉트 횟수 초과 (>5): ' + url);
    return Promise.resolve(null);
  }

  const client = url.startsWith('https') ? https : http;

  return new Promise((resolve) => {
    const req = client.get(url, { timeout: 15000 }, (res) => {
      if (res.statusCode >= 300 && res.statusCode < 400 && res.headers.location) {
        console.log('  [fetchUrl] 리다이렉트 ' + res.statusCode + ' → ' + res.headers.location);
        return fetchUrl(res.headers.location, redirectCount + 1).then(resolve);
      }
      if (res.statusCode >= 400) {
        console.log('  [fetchUrl] HTTP 에러: ' + res.statusCode + ' ' + res.statusMessage + ' (' + url + ')');
        resolve(null);
        return;
      }
      let body = '';
      res.on('data', d => { body += d; });
      res.on('end', () => {
        console.log('  [fetchUrl] 성공: ' + body.length + '자 수신 (' + url + ')');
        resolve(body.substring(0, 8000));
      });
    });
    req.on('error', (err) => {
      console.log('  [fetchUrl] 네트워크 에러: ' + err.message + ' (' + url + ')');
      resolve(null);
    });
    req.on('timeout', () => {
      console.log('  [fetchUrl] 타임아웃: ' + url);
      req.destroy();
      resolve(null);
    });
  });
}

function summarizeWithClaude(title, url, content) {
  if (!process.env.ANTHROPIC_API_KEY) {
    console.log('  [claude] ANTHROPIC_API_KEY 환경변수가 설정되지 않았습니다.');
    return Promise.resolve('');
  }

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

  return new Promise((resolve) => {
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
        if (res.statusCode >= 400) {
          console.log('  [claude] API 에러 HTTP ' + res.statusCode + ': ' + body.substring(0, 500));
          resolve('');
          return;
        }
        try {
          const parsed = JSON.parse(body);
          if (parsed.content && parsed.content[0] && parsed.content[0].text) {
            console.log('  [claude] 요약 생성 성공 (' + parsed.content[0].text.length + '자)');
            resolve(parsed.content[0].text);
          } else {
            console.log('  [claude] 응답에 content가 없음: ' + JSON.stringify(parsed).substring(0, 300));
            resolve('');
          }
        } catch (e) {
          console.log('  [claude] JSON 파싱 실패: ' + e.message + ' — 원문: ' + body.substring(0, 300));
          resolve('');
        }
      });
    });
    req.on('error', (err) => {
      console.log('  [claude] 네트워크 에러: ' + err.message);
      resolve('');
    });
    req.on('timeout', () => {
      console.log('  [claude] 요청 타임아웃');
      req.destroy();
      resolve('');
    });
    req.write(payload);
    req.end();
  });
}

async function main() {
  console.log('=== Knowledge Base 갱신 시작 ===');
  console.log('처리 대상: ' + entries.length + '개 항목');
  console.log('ANTHROPIC_API_KEY 설정: ' + (process.env.ANTHROPIC_API_KEY ? '있음' : '없음'));
  console.log('');

  const idx = JSON.parse(fs.readFileSync(idxPath, 'utf8'));
  let updated = 0;
  let failed = 0;

  for (const entry of entries) {
    console.log('[' + (updated + failed + 1) + '/' + entries.length + '] 처리 중: ' + entry.id);
    console.log('  URL: ' + entry.source_url);

    try {
      const content = await fetchUrl(entry.source_url);
      if (!content) {
        console.log('  ❌ URL 접근 불가 — 건너뜀');
        failed++;
        continue;
      }

      const summary = await summarizeWithClaude(entry.title, entry.source_url, content);
      if (!summary) {
        console.log('  ❌ 요약 생성 실패 — 건너뜀');
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

      const filePath = path.join(knowledgeDir, entry.file);
      fs.writeFileSync(filePath, fileContent);
      console.log('  파일 저장: ' + filePath);

      const idxEntry = idx.entries.find(e => e.id === entry.id);
      if (idxEntry) idxEntry.date = today;

      console.log('  ✅ 완료: ' + entry.id);
      updated++;
    } catch (err) {
      console.log('  ❌ 예기치 않은 에러: ' + err.message);
      console.log('  스택: ' + err.stack);
      failed++;
    }

    await new Promise(r => setTimeout(r, 1000));
  }

  fs.writeFileSync(idxPath, JSON.stringify(idx, null, 2) + '\n');

  console.log('');
  console.log('=== 결과 ===');
  console.log('갱신 성공: ' + updated);
  console.log('실패: ' + failed);
  console.log('총 처리: ' + entries.length);

  if (failed > 0 && updated === 0) {
    console.log('');
    console.log('⚠️ 모든 항목이 실패하여 exit code 1로 종료합니다.');
    process.exit(1);
  }
}

main().catch(e => {
  console.error('치명적 에러:', e.message);
  console.error('스택:', e.stack);
  process.exit(1);
});
