---
allowed-tools: Read, Write, Edit, Bash(git:*), WebFetch
description: knowledge/ 폴더의 문서를 갱신하고 새 항목을 추가합니다.
argument-hint: [--add URL "제목"] [--refresh] [--refresh-all] [--list]
---

# /update-knowledge — knowledge 폴더 자동 갱신

`knowledge/index.json`에 등록된 URL을 재취득하거나 새 URL을 추가해 요약 마크다운을 생성·갱신합니다.

## 사용법

```bash
/update-knowledge --list                          # 현재 등록된 항목 목록
/update-knowledge --add URL "제목"                # 새 URL 추가 및 요약 생성
/update-knowledge --refresh                       # stale(30일 이상) 항목만 갱신
/update-knowledge --refresh-all                   # 전체 항목 강제 갱신
/update-knowledge --refresh 2026-02-10-claude-character   # 특정 ID만 갱신
```

---

## 0단계: 인자 파싱

`$ARGUMENTS`에서 모드를 결정한다.

| 인자 | 동작 |
|------|------|
| `--list` | index.json 항목 목록 출력 |
| `--add URL "제목"` | 새 항목 추가 |
| `--refresh` | stale 항목 갱신 (기본: 30일 기준) |
| `--refresh-all` | 전체 갱신 |
| `--refresh ID` | 특정 항목만 갱신 |
| 인자 없음 | `--list` 동작 |

---

## 1단계: index.json 읽기

`knowledge/index.json`을 읽어 항목 목록을 파악한다.

```json
{
  "entries": [
    {
      "id": "YYYY-MM-DD-slug",
      "title": "문서 제목",
      "source_type": "web | internal | dotnet-docs | github",
      "source_url": "https://...",
      "date": "YYYY-MM-DD",
      "tags": ["tag1", "tag2"],
      "file": "summaries/YYYY-MM-DD-slug.md"
    }
  ]
}
```

---

## 2단계: 모드별 실행

### --list 모드

index.json의 모든 항목을 표 형식으로 출력한다:

```
════════════════════════════════════════════════════════
  knowledge/ 항목 목록 (총 N개)
════════════════════════════════════════════════════════

  ID                                    날짜        태그
  ──────────────────────────────────────────────────────
  2026-02-10-building-effective-agents  2026-02-10  ai-agents, llm
  2026-02-10-claude-character           2026-02-10  AI-alignment
  2026-02-14-pattern-verification       2026-02-14  verification (internal)

  갱신 필요 (30일 초과): N개
  다음 단계: /update-knowledge --refresh

════════════════════════════════════════════════════════
```

---

### --add 모드

새 URL을 knowledge에 추가한다.

**단계:**

1. URL이 유효한지 확인 (`source_type` 자동 감지):
   - `learn.microsoft.com`, `docs.microsoft.com` → `dotnet-docs`
   - `github.com` → `github`
   - 그 외 → `web`

2. WebFetch로 페이지 내용 취득

3. 다음 형식으로 요약 마크다운 생성:

```markdown
---
title: "[제목]"
source_type: web
source_url: [URL]
date: [오늘 날짜]
tags: [자동 추출된 태그]
---

[3~5단락 요약. 핵심 내용, 실용적 적용법, .NET/Blazor 프로젝트와의 관련성 위주로.]

## 핵심 포인트
- [포인트 1]
- [포인트 2]
- [포인트 3]

## .NET 프로젝트 적용
[이 내용을 Blazor/.NET 개발에 어떻게 적용할 수 있는지]
```

4. 파일명 결정: `YYYY-MM-DD-{slug}.md` (slug는 제목에서 자동 생성)

5. `knowledge/summaries/`에 파일 저장

6. `knowledge/index.json`에 새 항목 추가:

```json
{
  "id": "YYYY-MM-DD-slug",
  "title": "[제목]",
  "source_type": "web",
  "source_url": "[URL]",
  "date": "[오늘]",
  "tags": [...],
  "file": "summaries/YYYY-MM-DD-slug.md"
}
```

7. 결과 출력:
```
✅ 추가 완료: knowledge/summaries/2026-03-04-new-article.md
   제목: [제목]
   태그: [tag1, tag2]
```

---

### --refresh 모드

stale 항목(date가 30일 이상 지남)을 재취득·갱신한다.

**stale 판단:**
```
오늘 날짜 - entry.date > 30일 → stale
source_type == "internal" → 갱신 건너뜀 (URL 없음)
```

**각 stale 항목에 대해:**

1. `source_url`로 WebFetch 재실행
2. 기존 요약 파일(`file`)을 새 내용으로 덮어씀
3. index.json의 `date`를 오늘 날짜로 업데이트

**--refresh ID 모드:**
- 지정된 ID만 날짜 관계없이 강제 갱신

**--refresh-all 모드:**
- `source_type != "internal"` 인 모든 항목 갱신

---

## 3단계: 결과 출력

```
════════════════════════════════════════════════════════
  update-knowledge 완료
════════════════════════════════════════════════════════

  처리 결과:
    ✅ 갱신: 2026-02-10-building-effective-agents
    ✅ 갱신: 2026-02-10-claude-character
    ⏭️  건너뜀: 2026-02-14-pattern-verification (internal)
    ❌ 실패: [URL] — 접근 불가 (404)

  요약:
    갱신: N개 | 건너뜀: N개 | 실패: N개

  커밋 여부: /commit-push-pr 로 변경사항 반영 가능

════════════════════════════════════════════════════════
```

---

## 활용 예시

### .NET 관련 문서 추가

```bash
# Microsoft 공식 문서
/update-knowledge --add https://learn.microsoft.com/en-us/aspnet/core/blazor/ "Blazor 공식 문서"

# Supabase 문서
/update-knowledge --add https://supabase.com/docs/guides/auth "Supabase Auth 가이드"

# 블로그 포스트
/update-knowledge --add https://andrewlock.net/... "Andrew Lock 블로그"
```

### 주기적 갱신

```bash
# 매월 실행 (오래된 항목 갱신)
/update-knowledge --refresh

# 특정 항목이 업데이트됐을 때
/update-knowledge --refresh 2026-02-10-building-effective-agents
```
