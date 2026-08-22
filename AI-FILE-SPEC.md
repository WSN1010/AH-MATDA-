# AI File Specification

## 1. 목적

이 문서는 아주르가 선택한 **대상 코딩 에이전트**에 맞춰 생성할 지침 파일의 경로, 역할, 공통 내용, 충돌 처리, 검증 규칙을 정의한다.

여기서 “대상”은 Claude/GPT 같은 기반 모델만을 뜻하지 않는다. 사용자가 실제로 구현을 맡길 Claude Code, GitHub Copilot, Codex CLI, Gemini CLI, Cursor 같은 **코딩 도구**를 뜻한다. 같은 모델이라도 도구가 다르면 읽는 지침 파일이 다를 수 있다.

## 2. 산출 원칙

1. 공통 문서 3개가 제품과 기술의 소스 오브 트루스다.
2. 대상 지침 파일은 요구사항을 새로 만들지 않는다.
3. 대상 지침 파일은 해당 도구가 자동 발견하는 네이티브 경로를 우선한다.
4. 대상 지침 파일에는 “무엇을 만들지”보다 “어떤 문서를 어떤 순서로 읽고, 어떤 조건까지 완료할지”를 쓴다.
5. 하나의 범용 파일을 이름만 바꿔 복사하지 않고 도구의 계층/문법/권한 모델에 맞게 렌더링한다.
6. 공식 규격이 없는 대상은 `AGENTS.md` 범용 프로필로 명확히 폴백한다.

## 3. 지원 매트릭스

| Target ID | 대상 도구 | 기본 출력 | 형식/탐색 특성 | 지원 |
|---|---|---|---|---|
| `claude-code` | Claude Code | `CLAUDE.md` | 루트 프로젝트 지침, 하위 경로 지침 가능 | MVP |
| `github-copilot` | GitHub Copilot | `AGENTS.md` | 가까운 `AGENTS.md` 우선; 루트 파일은 저장소 전체 | MVP |
| `openai-codex` | OpenAI Codex | `AGENTS.md` | 루트부터 현재 경로까지 계층 병합 | MVP |
| `gemini-cli` | Gemini CLI | `GEMINI.md` | 계층적 컨텍스트, `@file.md` import 지원 | MVP |
| `cursor` | Cursor | `.cursor/rules/ajure.mdc` | Markdown + YAML frontmatter, 규칙 활성화 모드 | MVP 기본 |
| `devin-windsurf` | Devin Desktop / Windsurf Cascade | `.devin/rules/ajure.md` | 현재 우선 Workspace Rule; `.windsurf/rules/`는 레거시 폴백 | MVP 기본 |
| `cline` | Cline | `.clinerules/ajure.md` | Markdown 규칙, 선택적 path frontmatter | MVP 기본 |
| `amazon-q` | Amazon Q Developer | `.amazonq/rules/ajure.md` | 해당 폴더의 Markdown을 프로젝트 규칙으로 사용 | MVP 기본 |
| `generic` | 기타/범용 | `AGENTS.md` | AGENTS.md 호환을 가정하되 경고 표시 | MVP |

“MVP 기본”은 전체 프로젝트에 적용되는 단일 규칙 파일을 뜻한다. 디렉터리별 조건부 규칙 분할은 후속 범위다.

## 4. 기본 구현 번들

### 단일 대상: Claude Code

```text
/
├─ IDEATION.md
├─ PRD.md
├─ TRD.md
└─ CLAUDE.md
```

### 다중 대상: Claude Code + Copilot + Gemini CLI + Cursor

```text
/
├─ IDEATION.md
├─ PRD.md
├─ TRD.md
├─ CLAUDE.md
├─ AGENTS.md
├─ GEMINI.md
└─ .cursor/
   └─ rules/
      └─ ajure.mdc
```

평가 리포트와 내부 매니페스트는 기본 구현 번들에 넣지 않는다. 외부 코딩 에이전트가 읽을 필요 없는 컨텍스트를 줄이기 위함이다.

## 5. 공통 지침 payload

대상별 렌더러는 같은 `AgentInstructionSpec`을 입력으로 받는다.

| 필드 | 설명 |
|---|---|
| `specVersion` | 공통 문서와 일치해야 하는 명세 버전 |
| `targetId` | 대상 코딩 도구 |
| `mission` | 한 번의 구현으로 달성할 최종 결과 |
| `sourceFiles` | 읽어야 할 공통 문서와 순서 |
| `precedence` | 문서 충돌 시 우선순위 |
| `lockedDecisions` | 임의 변경할 수 없는 제품/기술 결정 |
| `scope` | 반드시 구현할 범위 |
| `nonGoals` | 구현하지 않을 범위 |
| `workflow` | 분석부터 검증까지 실행 순서 |
| `qualityGates` | 빌드, 테스트, lint, 접근성, 배포 등 완료 조건 |
| `ambiguityPolicy` | 명세가 충분한 경우 되묻지 않고 실행할 기본 원칙 |
| `safetyRules` | 비밀, 파괴적 작업, 사용자 데이터 보호 |
| `doneDefinition` | 원샷 구현이 끝났다고 판단할 조건 |

## 6. 모든 대상 파일의 필수 섹션

### 6.1 Mission

- 프로젝트의 최종 사용자 가치 한 문장
- “스캐폴드만 만들지 말고 수용 기준을 끝까지 구현”이라는 완료 기대
- 명세에 없는 기능을 임의 추가하지 않는다는 범위

### 6.2 Read Order

기본 순서는 다음과 같다.

1. `IDEATION.md`: 배경, 문제, 범위 이해
2. `PRD.md`: 제품 행동과 수용 기준 확정
3. `TRD.md`: 기술 구조와 운영 제약 확정
4. 현재 대상 지침 파일: 실행 및 검증 방식

대상 도구가 import 문법을 지원하면 참조를 사용하되, 지원하지 않으면 경로를 명시한다.

### 6.3 Source Precedence

충돌 시 다음 순서를 기본으로 한다.

1. 사용자가 구현 시점에 명시한 최신 요청
2. 대상 지침 파일의 안전/실행/검증 규칙
3. `PRD.md`의 사용자 행동과 수용 기준
4. `TRD.md`의 기술/운영 제약
5. `IDEATION.md`의 배경 설명

제품 행동과 기술 구현이 충돌하면 임의로 한쪽을 버리지 않고, 구현에 필요한 최소 가정을 결과에 명시하도록 한다.

### 6.4 Implementation Workflow

대상 파일은 코딩 에이전트에 다음 순서를 요구한다.

1. 세 공통 문서와 저장소 상태를 읽는다.
2. 요구사항 ID와 수용 기준을 구현 작업에 매핑한다.
3. 스캐폴딩이 아닌 수직 슬라이스로 기능을 완성한다.
4. 로딩, 빈 상태, 오류, 권한, 반응형 상태를 포함한다.
5. TRD의 테스트/빌드/배포 검증을 실행한다.
6. 실패를 수정하고 검증을 다시 실행한다.
7. 구현한 요구사항과 남은 제한을 최종 응답에 명시한다.

### 6.5 Guardrails

- 승인된 기술 스택을 임의로 교체하지 않는다.
- 요구하지 않은 인증, 결제, 데이터베이스, 프레임워크를 추가하지 않는다.
- 비밀을 코드나 설정 파일에 하드코딩하지 않는다.
- 사용자의 기존 변경을 되돌리지 않는다.
- 테스트를 삭제하거나 완화해 성공처럼 보이게 하지 않는다.
- 명세의 Non-goal을 구현하지 않는다.

### 6.6 Definition of Done

- 모든 Must 요구사항이 구현됨
- 모든 수용 기준에 검증 근거가 있음
- 빌드/타입 검사/테스트가 통과함
- 핵심 사용자 흐름이 실제로 동작함
- UI는 데스크톱/모바일 및 필수 상태를 지원함
- 오류를 숨기지 않음
- 문서와 실행 방법이 최신 상태임
- 미완료 항목을 완료한 것처럼 보고하지 않음

## 7. 대상별 렌더링 규칙

### 7.1 Claude Code - `CLAUDE.md`

- 프로젝트 루트에 생성한다.
- 핵심 내용은 간결한 Markdown 헤딩과 명령형 bullet로 작성한다.
- 공통 문서는 `@IDEATION.md`, `@PRD.md`, `@TRD.md` import를 사용할 수 있다.
- 너무 긴 상세 규칙은 향후 `.claude/rules/*.md`로 분리할 수 있으나 MVP는 단일 파일을 사용한다.
- 개인용 `CLAUDE.local.md` 내용은 생성하지 않는다.

### 7.2 GitHub Copilot - `AGENTS.md`

- 프로젝트 루트에 생성한다.
- 저장소 전체 에이전트 지침으로 사용한다.
- `.github/copilot-instructions.md`는 MVP에서 중복 생성하지 않는다.
- 향후 경로별 지침이 필요하면 하위 `AGENTS.md` 또는 `.github/instructions/*.instructions.md`를 선택적으로 생성한다.

### 7.3 OpenAI Codex - `AGENTS.md`

- 프로젝트 루트에 생성한다.
- Codex의 루트-하위 디렉터리 병합을 고려해 저장소 공통 규칙만 포함한다.
- 임시 override인 `AGENTS.override.md`는 생성하지 않는다.
- Copilot과 Codex를 함께 선택하면 하나의 `AGENTS.md`에 공통 규칙을 쓰고, 특정 기능에 의존하는 문구는 넣지 않는다.

### 7.4 Gemini CLI - `GEMINI.md`

- 프로젝트 루트에 생성한다.
- 공통 문서를 `@./IDEATION.md`, `@./PRD.md`, `@./TRD.md`로 import할 수 있다.
- `/memory` 같은 사용자 로컬 명령을 필수 구현 단계로 가정하지 않는다.
- 커스텀 context filename 설정이 없는 기본 환경을 기준으로 한다.

### 7.5 Cursor - `.cursor/rules/ajure.mdc`

- `.md`가 아닌 `.mdc` 확장자를 사용한다.
- YAML frontmatter와 Markdown 본문을 생성한다.
- MVP는 `alwaysApply: true`인 프로젝트 공통 규칙 하나를 만든다.
- 후속 버전은 frontend/backend 등 glob 기반 파일 규칙으로 분리할 수 있다.

### 7.6 Devin Desktop / Windsurf Cascade - `.devin/rules/ajure.md`

- 현재 우선 Workspace Rule 경로인 `.devin/rules/`에 Markdown 파일을 생성한다.
- MVP는 전체 프로젝트에 적용되는 규칙으로 렌더링한다.
- 전체 적용 규칙은 `trigger: always_on` frontmatter를 사용한다.
- 레거시 Windsurf 호환 옵션을 명시적으로 선택한 경우에만 `.windsurf/rules/ajure.md`를 추가한다.
- 현재 도구는 `.devin/`을 우선하므로 두 파일을 기본으로 동시에 생성해 지침을 중복 적용하지 않는다.

### 7.7 Cline - `.clinerules/ajure.md`

- `.clinerules/` 아래 Markdown 파일을 생성한다.
- MVP는 frontmatter가 없는 항상 활성 규칙을 사용한다.
- 후속 버전은 `paths` 조건으로 영역별 규칙을 생성할 수 있다.

### 7.8 Amazon Q Developer - `.amazonq/rules/ajure.md`

- `.amazonq/rules/` 아래 Markdown 파일을 생성한다.
- 프로젝트 공통 코딩 표준과 구현 완료 조건을 명령형으로 작성한다.
- Amazon 서비스 사용을 자동으로 전제하지 않는다. TRD가 요구할 때만 AWS 관련 규칙을 넣는다.

### 7.9 Generic - `AGENTS.md`

- 대상 도구의 공식 규격을 찾지 못했을 때만 사용한다.
- UI와 Validation Report에 “자동 탐색 여부는 대상 도구에서 확인 필요” 경고를 표시한다.
- 사용자가 직접 파일명/경로를 지정할 수 있게 하되 Ready 점수에서 네이티브 호환성 항목은 만점을 주지 않는다.

## 8. 다중 대상 충돌 처리

### 같은 파일을 공유하는 경우

Copilot과 Codex처럼 `AGENTS.md`를 공유하면 다음 규칙을 적용한다.

- 공통으로 이해되는 Markdown만 사용한다.
- 한 도구에만 존재하는 명령이나 기능을 필수 단계로 쓰지 않는다.
- 대상별 차이가 필요한 경우 별도 “Tool-specific Notes”를 만들되 제품 요구사항은 중복하지 않는다.
- 병합된 파일은 두 Target ID와 템플릿 버전을 내부 매니페스트에 기록한다.

### 다른 파일이 같은 규칙을 담는 경우

공통 지침 payload에서 각각 렌더링하므로 의미는 같아야 한다. 결정적 검사는 다음 항목의 의미 해시를 비교한다.

- Mission
- Scope/Non-goals
- Locked Decisions
- Quality Gates
- Definition of Done

도구별 문법 차이는 허용하지만 위 내용의 누락은 허용하지 않는다.

## 9. 버전과 Stale 판정

각 대상 파일은 렌더링 시 다음 메타데이터를 내부 Artifact 레코드에 가진다.

- `specVersion`
- `projectSpecHash`
- `adapterId`
- `adapterVersion`
- `targetIds`
- `generatedAt`

파일 본문에는 사람이 읽을 수 있는 명세 버전을 표시하되 비밀이나 내부 Blob URL을 넣지 않는다.

다음 경우 Stale이다.

- ProjectSpec hash가 다름
- 공통 문서 중 하나가 수동 편집됨
- 대상 또는 어댑터 버전이 변경됨
- Hard Gate 판정 이후 내용이 변경됨

Stale 파일은 내보낼 수 없다.

## 10. 품질 검사

### 결정적 검사

- 정확한 대소문자와 경로
- 대상별 확장자
- 필요한 frontmatter 파싱
- 공통 문서 3개 참조
- 명세 버전 일치
- 필수 섹션 존재
- 비어 있는 Scope/Done Definition 금지
- 비밀 또는 실제 토큰 패턴 금지

### 의미 검사

- 대상 파일이 PRD/TRD에 없는 기능을 추가하지 않는가
- Must 요구사항을 선택 사항처럼 약화하지 않는가
- Non-goal을 구현하도록 지시하지 않는가
- “테스트해라”가 아니라 TRD의 검증 유형을 반영하는가
- 구현을 중간 스캐폴딩에서 끝내도록 허용하지 않는가
- 대상 도구가 이해하지 못하는 기능을 필수로 요구하지 않는가

## 11. 수용 기준

1. Claude Code 선택 시 루트 `CLAUDE.md`가 생성된다.
2. Copilot 또는 Codex 선택 시 루트 `AGENTS.md`가 생성된다.
3. Gemini CLI 선택 시 루트 `GEMINI.md`가 생성된다.
4. Cursor 선택 시 `.cursor/rules/ajure.mdc`가 생성되고 frontmatter가 파싱된다.
5. 여러 대상을 선택해도 공통 문서의 제품 의미가 대상 파일별로 달라지지 않는다.
6. 공통 문서 버전이 바뀌면 모든 관련 대상 파일이 Stale이 된다.
7. 대상 파일에 공통 문서에 없는 Must 요구사항이 생기면 Hard Gate가 실패한다.
8. Generic 폴백은 경고 없이 네이티브 지원으로 표시되지 않는다.

## 12. 공식 규격 참고

아래 자료를 2026-08-22 기준 설계 근거로 사용하며 구현 시 다시 확인한다.

- GitHub Copilot custom instructions: <https://docs.github.com/en/copilot/how-tos/copilot-on-github/customize-copilot/add-custom-instructions/add-repository-instructions>
- Claude Code memory: <https://code.claude.com/docs/en/memory>
- Gemini CLI `GEMINI.md`: <https://github.com/google-gemini/gemini-cli/blob/main/docs/cli/gemini-md.md>
- OpenAI Codex `AGENTS.md`: <https://developers.openai.com/codex/guides/agents-md>
- Cursor Rules: <https://cursor.com/docs/rules>
- Devin Desktop / Windsurf Cascade Rules: <https://docs.devin.ai/desktop/cascade/memories#rules>
- Cline Rules: <https://docs.cline.bot/customization/cline-rules>
- Amazon Q project rules: <https://docs.aws.amazon.com/amazonq/latest/qdeveloper-ug/context-project-rules.html>
