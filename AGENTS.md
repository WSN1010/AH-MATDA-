# AGENTS.md - 아주르 저장소 구현 규칙

이 파일은 **이 저장소에서 코드를 작성하는 코딩 에이전트(GitHub Copilot)** 를 위한 규칙이다.
아주르 제품이 사용자에게 생성해 주는 `AGENTS.md` 산출물과는 다른 파일이다.

## 프로젝트 한 줄 요약

아주르(AJURE)는 아이디어를 받아 `IDEATION.md`/`PRD.md`/`TRD.md`와 대상 코딩 에이전트의 네이티브 지침 파일을 생성·검증하고, **최종 결과물을 ZIP 파일 하나로 내보내는** 웹 앱이다. 이 저장소의 코드는 그 웹 앱의 구현이다.

## 문서 읽기 순서

작업 시작 전 반드시 순서대로 읽는다.

1. 이 파일 전체
2. 담당 영역의 구현 가이드: [IMPLEMENTATION-BACKEND.md](IMPLEMENTATION-BACKEND.md) 또는 [IMPLEMENTATION-FRONTEND.md](IMPLEMENTATION-FRONTEND.md)
3. [TRD.md](TRD.md) — 아키텍처와 API 계약(§9)
4. 작업과 관련된 [PRD.md](PRD.md)의 FR/AC 항목
5. 필요 시 [EVALUATION.md](EVALUATION.md), [DOCUMENT-SPEC.md](DOCUMENT-SPEC.md), [AI-FILE-SPEC.md](AI-FILE-SPEC.md), [UX-SPEC.md](UX-SPEC.md)

## 필수 도구 규칙

### 1. 모든 코드 작업에 ponytail 플러그인을 무조건 사용한다

- 코드를 작성, 수정, 리팩터링, 리뷰하는 **모든 작업**에서 ponytail 스킬을 적용한다. 예외 없다.
- 원칙: 동작하는 가장 단순한 해법. 표준 라이브러리 우선, 의존성 추가는 필요가 증명될 때만, 추측성 추상화 금지.
- 단, ponytail은 **구현 복잡도만** 다스린다. 필수 기술 조건(아래), 접근성, 디자인 인터뷰 결과, 검증 의무를 축소하는 근거로 쓰지 않는다.

### 2. 프론트엔드 작업은 관련 스킬을 스스로 찾아 적용한다

- `src/Ajure.Web` 작업 시, 설치된 스킬 목록에서 관련 스킬(디자인/프론트엔드 스킬 등)을 **에이전트가 알아서 탐색해 로드하고 적용**한다. 사용자가 스킬 이름을 지정해 줄 때까지 기다리지 않는다.
- 두 작업 컴퓨터 모두 ponytail 플러그인과 디자인 스킬이 이미 설치되어 있다.
- UI 작업에서 디자인 스킬과 ponytail이 충돌하면: 디자인 방향·시각 품질·접근성은 디자인 스킬을 따르고, 코드 구조·의존성 선택은 ponytail을 따른다.

### 3. 시각 디자인은 임의로 정하지 않는다

- 스타일링 전 [IMPLEMENTATION-FRONTEND.md](IMPLEMENTATION-FRONTEND.md) §5의 디자인 인터뷰 질문을 사용자에게 묻고, 확정된 답만 기준으로 삼는다.
- 답이 없는 항목만 UX-SPEC §2 기본값을 쓴다.

## 구현 규칙

1. **필수 기술을 대체하지 않는다**: Microsoft Agent Framework, GitHub Copilot SDK, `IChatClient`, .NET Aspire, Azure Container Apps는 고정이다. 어떤 문제가 생겨도 다른 스택으로 조용히 바꾸지 않고 사용자에게 보고한다.
2. **스펙이 코드보다 먼저다**: 요구사항·API 계약을 바꿔야 하면 코드 수정 전에 PRD/TRD를 먼저 수정하고 커밋한다.
3. **API 계약은 TRD §9가 유일한 기준이다**: 프론트·백 모두 이 표 밖의 엔드포인트를 만들거나 호출하지 않는다.
4. **모델 세션에 코드 실행 도구를 주지 않는다**: 파일 쓰기, 셸, Git 도구 노출 금지 (TD-003). 아주르 산출물은 Markdown 명세뿐이다.
5. **최종 산출물은 ZIP이다**: 사용자에게 전달되는 결과물은 여러 md 파일을 묶은 ZIP 파일 하나다 (FR-013). 개별 파일 다운로드를 임의로 추가하지 않는다.
6. **구현 가이드의 마일스톤 순서를 따른다**: 특히 백엔드 B1(Copilot SDK 스파이크)은 차단 게이트다.

## 작업 분담과 소유권

| 담당 | 소유 영역 | 수정 금지 영역 |
|---|---|---|
| 백엔드 | `src/`(Web 제외), `tests/`, `azure.yaml` | `src/Ajure.Web` |
| 프론트엔드 | `src/Ajure.Web` | 그 외 `src/`, `tests/` |
| 공유(협의 후 수정) | `Ajure.AppHost`의 리소스 등록, TRD §9, 루트 문서 | - |

상대 영역의 변경이 필요하면 코드를 고치지 말고 필요 내용을 사용자에게 알린다.

## Git 규칙

- 원격: `https://github.com/WSN1010/AH-MATDA-` `main` 브랜치에 직접 푸시한다 (2인 해커톤).
- 푸시 전 반드시 `git pull --rebase`로 상대 커밋을 먼저 반영한다.
- 커밋은 작게, 메시지는 `[be]`/`[fe]`/`[docs]` 접두사 + 한 줄 요약.
- 빌드가 깨진 상태로 푸시하지 않는다.
- 비밀 값(토큰, 연결 문자열, `.env`)은 절대 커밋하지 않는다. Key Vault 또는 로컬 user-secrets를 쓴다.

## 검증 의무

**"완료"라고 말하기 전에 실제로 실행하고 결과를 확인한다.** 실행하지 않은 검증을 통과했다고 보고하지 않는다.

- 백엔드: `dotnet build` + 변경 영역 `dotnet test`, B3 이후 Fake 모드 E2E
- 프론트: `npm run build` + 타입 오류 0 + 실제 브라우저에서 360px/1440px 확인
- 실패한 검증은 고치고 다시 실행한 뒤 푸시한다.

## 금지 사항 요약

- ponytail 없이 코드 작업
- 사용자에게 묻지 않은 디자인 확정
- 필수 기술 스택 대체
- 상대 담당 영역 수정
- TRD §9 밖의 API
- 비밀 커밋
- 검증 없는 완료 보고
