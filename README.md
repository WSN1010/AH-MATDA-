# 아주르 (AJURE)

> 아이디어를 코드로 만드는 에이전트가 아니라, **다른 코딩 에이전트가 한 번에 의도대로 구현할 수 있는 명세 묶음**을 만드는 웹 에이전트

## 제품 정의

아주르는 사용자의 아이디어를 구조화하고 다음 산출물을 함께 생성한다.

1. `IDEATION.md`: 문제, 사용자, 가설, 대안, 범위를 확정한다.
2. `PRD.md`: 기능 요구사항과 검증 가능한 수용 기준을 정의한다.
3. `TRD.md`: 기술 스택, 구조, 인터페이스, 보안, 배포, 테스트를 정의한다.
4. 대상 코딩 에이전트의 네이티브 지침 파일: 예를 들어 Claude Code는 `CLAUDE.md`, Gemini CLI는 `GEMINI.md`, Copilot/Codex는 `AGENTS.md`를 생성한다.

기획에서 말하는 `AI.md`는 네 번째 파일의 개념적 이름이다. 실제 내보내기에서는 `AI.md`라는 고정 이름을 쓰지 않고 선택 도구가 자동으로 읽는 네이티브 파일명을 사용한다.

생성된 문서는 단순 초안이 아니다. 아주르는 문서 간 추적성, 누락, 모순, 모호성, 구현 가능성을 반복 평가하고 이전 승인 버전과 회귀 비교한다. 필수 게이트를 모두 통과하고 **One-Shot Readiness Score 90점 이상**이 되면 구현용 번들을 내보낸다.

## 아주르가 하지 않는 일

- 제품 코드를 작성하거나 저장소를 직접 구현하지 않는다.
- 사용자를 대신해 코딩 에이전트의 도구 권한을 실행하지 않는다.
- 근거 없이 “90% 성공”을 보장하지 않는다.

90%는 문서 자체 점수만 뜻하지 않는다. 별도의 벤치마크에서 외부 코딩 에이전트가 한 번의 구현 요청으로 충족한 수용 기준의 비율을 측정하는 제품 목표다.

## 핵심 사용자 흐름

```text
아이디어 입력
  -> 대상 코딩 에이전트 선택
  -> 아주르의 핵심 확인 질문
  -> 4종 명세 생성
  -> 결정적 검사 + 서로 다른 모델 2개 이상의 독립 AI 평가
  -> 이전 버전 회귀 검사
  -> 부족한 부분 자동 보정 또는 사용자 결정 요청
  -> Ready(90+) 판정
  -> ZIP/GitHub용 구현 번들 내보내기
```

## 지원 대상

| 대상 코딩 에이전트 | 생성 파일 |
|---|---|
| Claude Code | `CLAUDE.md` |
| GitHub Copilot | `AGENTS.md` |
| OpenAI Codex | `AGENTS.md` |
| Gemini CLI | `GEMINI.md` |
| Cursor | `.cursor/rules/ajure.mdc` |
| Devin Desktop / Windsurf Cascade | `.devin/rules/ajure.md` (`.windsurf/rules/ajure.md`는 레거시 호환) |
| Cline | `.clinerules/ajure.md` |
| Amazon Q Developer | `.amazonq/rules/ajure.md` |
| 범용/미지원 도구 | `AGENTS.md` |

사용자가 여러 도구를 선택하면 각 도구의 네이티브 파일을 함께 생성한다. 같은 경로를 공유하는 도구는 호환 가능한 하나의 파일로 병합한다.

## 필수 기술 조건

- **Microsoft Agent Framework**: 작성, 검토, 회귀, 보정 에이전트 워크플로를 실행한다.
- **OpenAI / Anthropic / Gemini API**: 사용자가 제공한 API 키로 문서 생성 및 평가 모델을 호출한다.
- **`IChatClient`**: 모델 호출을 표준화해 작성자와 평가자를 공급자와 분리한다.
- **SQLite**: 프로젝트, Job 큐, 이벤트, 산출물을 외부 인프라 없이 저장한다.
- **.NET Aspire**: 선택적인 로컬 개발 실행기이며 운영 필수 요소가 아니다.

## 문서

- [IDEATION.md](IDEATION.md): 아이디어와 제품 방향
- [PRD.md](PRD.md): 제품 요구사항과 수용 기준
- [TRD.md](TRD.md): 기술 설계와 셀프호스트 구조
- [DOCUMENT-SPEC.md](DOCUMENT-SPEC.md): 공통 문서 3종의 출력 계약
- [AI-FILE-SPEC.md](AI-FILE-SPEC.md): 도구별 지침 파일 생성 규격
- [EVALUATION.md](EVALUATION.md): 품질 점수와 회귀 검증 규격
- [UX-SPEC.md](UX-SPEC.md): 웹 앱 정보 구조와 화면 설계

### 구현 문서

- [AGENTS.md](AGENTS.md): 이 저장소에서 작업하는 코딩 에이전트의 구현 규칙
- [IMPLEMENTATION-BACKEND.md](IMPLEMENTATION-BACKEND.md): 백엔드 담당 구현 가이드
- [IMPLEMENTATION-FRONTEND.md](IMPLEMENTATION-FRONTEND.md): 프론트엔드 담당 구현 가이드

## 현재 상태

기획이 확정되어 **구현 단계**에 진입했다. Azure와 GitHub Copilot SDK를 제거하고 SQLite 및 OpenAI/Anthropic/Gemini 직접 API를 사용하는 MIT 라이선스 셀프호스트 구조로 전환 중이다. 2인(프론트/백) 분담과 규칙은 구현 문서를 따른다.
