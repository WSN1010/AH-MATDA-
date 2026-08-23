# UX Specification - 아주르 웹 앱

## 1. UX 목표

사용자가 “AI와 오래 대화했다”가 아니라 **필요한 결정을 끝내고, 검증된 구현 명세를 받았다**고 느끼게 한다.

핵심 경험은 채팅이 아니라 명세 워크벤치다.

- 질문은 결정 단위로 짧고 명확하게 제시한다.
- 생성 과정과 검증 근거를 숨기지 않는다.
- 점수보다 실패 이유와 해결 행동을 먼저 보여 준다.
- 문서 간 연결과 변경 영향을 시각적으로 확인할 수 있게 한다.
- Ready 상태에서는 다음 행동이 “다운로드 후 대상 에이전트에 전달”로 명확해야 한다.

## 2. 디자인 방향

### 컨셉: Specification Workbench

문서 작성 도구와 품질 검사 장비가 결합된 작업대 느낌을 사용한다. 일반적인 AI 채팅의 말풍선, 보라색 그라데이션, 반짝이 아이콘을 피한다.

### 확정 디자인 기준선

- 전체 무드: 신뢰감 있는 **전문 도구형**
- 기본 테마: 운영체제의 `prefers-color-scheme`을 따르는 라이트/다크
- 주 색상: 제품명과 연결되는 아주르 블루 계열
- 정보 밀도: 문서 영역은 여유 있게, 검사·상태 패널은 콤팩트하게 구성하는 균형형
- 타이포그래피: 본문 Sans와 Requirement ID·버전·경로용 Mono를 분리
- 브랜드 표기: **아주르 AJURE**
- 모션: 랜딩과 주요 상태 전환에만 깊이, 빛, 레이어 이동을 사용하는 절제된 2.5D
- 품질 시각화: 수평 품질 바와 Hard Gate 체크리스트 조합

3D 효과는 제품의 정밀한 검증 과정을 설명하는 데만 사용한다. 무거운 WebGL 장면이나 지속적인 카메라 움직임은 도입하지 않으며, 문서 편집 중에는 모션을 최소화한다. `prefers-reduced-motion`에서는 비필수 전환과 깊이 이동을 제거한다.

### 시각 언어

- 넓은 여백과 얇은 기준선으로 문서의 구조를 강조한다.
- Requirement ID, 버전, 검증 상태는 모노스페이스 라벨로 표시한다.
- 중요한 결정은 종이 메모처럼 보이는 “Decision Slip” 카드로 표현한다.
- 검증 흐름은 장식적인 로더가 아니라 실제 단계와 발견 수를 보여 주는 “Validation Rail”로 표현한다.
- 점수는 원형 게이지보다 영역별 근거가 보이는 수평 품질 바를 사용한다.

### 색상 토큰

| 용도 | 라이트 | 다크 | 의미 |
|---|---|---|---|
| Canvas | `#F4F6F3` | `#08111D` | 차분한 작업 배경 |
| Surface | `#FFFFFF` | `#101C2A` | 문서/패널 |
| Ink | `#172337` | `#E8EEF6` | 본문 |
| Muted | `#667085` | `#98A8BA` | 보조 텍스트 |
| Azure | `#146C94` | `#54B7E3` | 주요 행동/선택 |
| Azure Dark | `#0B3C5D` | `#B9E5F8` | 헤더/강조 |
| Amber | `#B54708` | `#F5B65B` | 결정 필요 |
| Green | `#16794A` | `#56C992` | Ready/통과 |
| Red | `#B42318` | `#FF8A80` | Critical/차단 |
| Border | `#D8DEE5` | `#2A3A4D` | 구조선 |

색상은 상태의 유일한 표현 수단으로 사용하지 않는다. 아이콘, 텍스트, 패턴을 함께 사용한다.

### 타이포그래피

- 한국어/본문: `Pretendard Variable`, 대체 `Noto Sans KR`, 시스템 sans-serif
- ID/버전/코드 경로: `IBM Plex Mono`, 대체 monospace
- H1은 과도하게 크지 않은 40~48px, 문서 제목은 28~32px
- 본문 최소 16px, 보조 정보 최소 14px

### 시그니처 표현

문서 파일 카드가 서로 다른 깊이의 레이어에서 정렬되고, 검증이 진행되면 요구사항 연결선이 빛을 따라 이동해 하나의 구현 번들로 잠기는 장면을 대표 표현으로 사용한다. 이 효과는 랜딩 Hero와 Ready 전환에 집중하고 워크벤치 본문에서는 얇은 기준선, 미세한 그림자, 상태 변화만 남긴다.

## 3. 전역 정보 구조

```text
Home
├─ Projects
│  ├─ New Project
│  │  ├─ Idea
│  │  ├─ Target Agents
│  │  └─ Decisions
│  └─ Project
│     ├─ Workbench
│     │  ├─ Documents
│     │  ├─ Validation
│     │  └─ Traceability
│     └─ History
└─ Settings
   └─ Model providers
```

## 4. 전역 내비게이션

### 데스크톱

- 좌측 상단: 아주르 워드마크
- 중앙: 현재 프로젝트명 / Spec Version / 상태
- 우측: Projects, 모델 설정
- 프로젝트 내부 상단에 4단계 Rail:
  - `01 결정`
  - `02 명세`
  - `03 검증`
  - `04 전달`

### 모바일

- 상단 바에 뒤로가기, 프로젝트명, 상태만 표시
- 단계 Rail은 가로 스크롤 가능한 compact stepper
- 부가 메뉴는 bottom sheet

## 5. 화면별 상세

## 5.1 Landing - `/`

### 목적

제품이 코드를 생성하는 도구가 아니라 원샷 구현을 위한 명세 도구임을 즉시 전달한다.

### Hero

- 제목: **“코드를 쓰기 전에, AI가 틀릴 여지를 없앱니다.”**
- 설명: 아이디어를 4종 명세로 만들고 회귀 검증해 선택한 코딩 에이전트에 전달
- Primary CTA: `새 명세 만들기`
- Secondary CTA: `검증 방식 보기`

### 핵심 시각

가운데에 다음 변환을 실제 파일 카드로 보여 준다.

```text
아이디어 메모
   ↓ 아주르 검증 루프
IDEATION.md  PRD.md  TRD.md  CLAUDE.md
```

가짜 채팅 데모 대신 문서 사이 연결선과 `FR-004 -> AC-009 -> TD-003` 같은 추적 예시를 보여 준다.

### 지원 대상

도구 로고 나열보다 파일 경로를 함께 표시한다.

- Claude Code — `CLAUDE.md`
- Copilot / Codex — `AGENTS.md`
- Gemini CLI — `GEMINI.md`
- Cursor / Devin Desktop(Windsurf) / Cline / Amazon Q — native rules

## 5.2 Projects - `/projects`

### 항목

- 프로젝트명
- 최신 상태
- Readiness Score
- 최신 Spec Version
- 선택 대상
- 마지막 수정

### 상태

- Empty: “아직 명세가 없습니다. 첫 아이디어를 구조화해 보세요.”
- Loading: 6개 이하의 고정 skeleton row
- Error: 재시도 버튼과 Correlation ID
- Ready: 파일 수와 다운로드 바로가기
- Needs Decision: 남은 Critical Decision 수

## 5.3 New Project - `/projects/new`

단일 긴 폼이 아니라 세 단계로 진행한다.

### Step 1 - Idea

필드:

- 프로젝트 이름
- “무엇을, 누구를 위해 만들고 싶나요?” 자유 입력
- 이미 정해진 기술/배포 조건
- 반드시 제외할 범위
- 기존 문서 붙여 넣기(선택)

입력 영역 아래에 좋은 입력 예시를 한 문장으로 제공하되 템플릿 작성을 강요하지 않는다.

Primary CTA: `아이디어 분석`

### Step 2 - Target Agents

카드는 기반 모델이 아니라 실제 도구 단위로 표시한다.

각 카드:

- 도구명
- 생성 파일 경로
- 지원 수준: Stable / Basic / Generic
- 해당 도구가 자동으로 파일을 읽는 방식

다중 선택 가능하다. Copilot과 Codex처럼 같은 `AGENTS.md`를 공유하면 “호환 파일 하나로 병합됩니다”를 즉시 표시한다.

### Step 3 - Review

- 입력 요약
- 선택 대상과 출력 파일
- 예상 질문 수
- 데이터 처리 안내

CTA: `결정 시작`

## 5.4 Decisions - `/projects/{id}/questions`

### 레이아웃

데스크톱:

- 좌측 30%: 결정 목록과 진행률
- 우측 70%: 현재 Decision Slip

모바일:

- 현재 질문 하나에 집중
- 목록은 bottom sheet

### Decision Slip

- 분류: Critical / Important / Defaultable
- 질문
- “왜 필요한가”
- 선택에 따른 구현 영향
- 추천 선택지와 근거
- 직접 입력
- `결정 저장`

### 동작

- Critical은 건너뛰기 불가
- Defaultable은 `추천값 사용`으로 빠르게 통과 가능
- 이전 답변과 충돌하면 바로 경고
- 답변 저장 후 다음 미결정으로 이동
- 키보드 `1~4`로 선택, `Enter`로 확정 가능

### 완료

남은 Critical 0개일 때 `명세 생성` 활성화.

## 5.5 Generation - Workbench Progress

명세 생성은 별도 무의미한 대기 화면 대신 Workbench에서 진행한다.

### Validation Rail

```text
[완료] 입력 구조화        12개 요구사항
[진행] 공통 문서 생성     PRD 작성 중
[대기] 교차 검증
[대기] 회귀 검사
[대기] 대상 파일 렌더링
```

각 단계는 시작 시각, 소요 시간, 발견 수를 표시한다. 내부 Chain-of-Thought는 노출하지 않는다.

### 취소/복구

- `생성 취소`는 확인 후 Job을 중단한다.
- 페이지를 새로 고쳐도 진행 상태가 복원된다.
- 재시도 가능한 실패는 `실패 단계부터 재시도` 제공.

## 5.6 Workbench - `/projects/{id}/workbench`

### 데스크톱 3열 구조

```text
┌──────────────┬──────────────────────────────┬────────────────────┐
│ Artifact Nav │ Document Canvas              │ Quality Inspector  │
│ 240px        │ flexible                     │ 360px              │
└──────────────┴──────────────────────────────┴────────────────────┘
```

#### Artifact Nav

- `IDEATION.md`
- `PRD.md`
- `TRD.md`
- 대상 지침 파일
- 파일별 상태: Valid / Stale / Error
- Traceability 보기
- Version History

#### Document Canvas

모드:

- Preview
- Edit
- Diff

기능:

- 헤딩 미니맵
- Requirement ID anchor
- Finding이 있는 줄 강조
- 저장 시 즉시 Ready를 해제하기 전에 영향 안내
- 자동 보정 변경은 accept/reject 가능

#### Quality Inspector

상단:

- `Ready 92/100` 또는 `Needs Decision`
- Hard Gate `12/12`
- 기준 버전 대비 `+4`

탭:

- Findings
- Coverage
- Regression

Finding 카드:

- 심각도
- 한 문장 문제
- 근거
- 관련 ID 링크
- 자동 수정 가능 여부
- `수정 적용`, `결정하기`, `무시 요청`

Critical Finding은 이유 없이 무시할 수 없다.

### 좁은 화면

- 1024px 미만: Inspector를 우측 drawer로 전환
- 768px 미만: Artifact Nav를 top dropdown으로 전환
- 모바일 편집 시 Preview/Edit만 표시하고 Diff는 전체 화면 modal
- 하단 sticky action: `검증`, `결정 보기`, `내보내기`

## 5.7 Traceability

그래프를 기본으로 강요하지 않는다. 읽기 쉬운 표를 기본으로 하고 그래프를 보조로 제공한다.

| Requirement | Acceptance Criteria | Technical Decision | 상태 |
|---|---|---|---|
| FR-004 | AC-006, AC-007 | TD-002 | Complete |

필터:

- 미연결만
- Must만
- Critical Finding이 있는 항목
- 변경된 항목

행 선택 시 PRD/TRD의 관련 위치를 나란히 연다.

## 5.8 History - `/projects/{id}/history`

### 버전 타임라인

- 버전
- 상태
- 점수
- 변경 요약
- 대상
- 내보낸 시각

### 비교

- 기준선과 후보 선택
- Added / Changed / Removed / Weakened 구분
- 텍스트 diff보다 의미 변화 요약을 먼저 표시
- 삭제/약화 항목은 사용자 승인 기록 표시

## 5.9 Export

Ready일 때 우측 Inspector와 상단 상태 영역에서 활성화한다.

### Export Sheet

- 포함 파일 트리
- 대상 도구
- Spec Version
- 점수/Hard Gate
- 선택 옵션: `VALIDATION-REPORT.md 포함`
- Primary CTA: `구현 번들 다운로드`

다운로드 후 안내:

1. ZIP을 새 프로젝트 또는 저장소 루트에 푼다.
2. 선택한 코딩 에이전트를 해당 루트에서 실행한다.
3. “이 명세를 기준으로 전체 구현하고 검증까지 완료해”라는 단일 요청을 사용한다.

아주르가 직접 구현하거나 저장소에 접근한 것처럼 표현하지 않는다.

## 5.10 Model Providers - `/settings/providers`

### 목적

로컬 사용자가 셸이나 설정 파일을 열지 않고 OpenAI, Anthropic, Gemini 자격증명을 내 PC에 연결한다.

### 레이아웃

- 상단 `모델 연결 상태` Rail에서 `구성됨 n / 필요 2`를 텍스트와 연결선으로 표시한다.
- 아래에 OpenAI GPT, Anthropic Claude, Google Gemini를 동일한 공급자 Bay로 배치한다.
- 각 Bay는 공급자명, 구성 상태, 모델 ID, 비밀번호형 API 키 입력, 저장/제거 행동을 가진다.
- API 키는 저장 후 즉시 입력란에서 지우며 다시 표시하거나 일부 문자를 힌트로 노출하지 않는다.
- 환경 변수/user-secrets 공급자는 `운영자 관리` 배지를 표시하고 입력과 제거 행동을 비활성화한다.

### 상태

- Loading: Rail과 공급자 Bay 3개의 고정 skeleton
- Empty: `모델 2개를 연결해야 생성할 수 있습니다.`
- Partial: 현재 수와 남은 수를 함께 표시
- Ready: `모델 연결 준비 완료`와 구성된 공급자명을 표시
- Saving: 해당 Bay의 입력을 잠그고 `저장 중`
- Success: 토스트에만 의존하지 않고 Bay 상태를 `연결됨`으로 갱신
- Error: 입력을 유지하고 Problem Details의 행동 가능한 메시지를 표시
- Offline/Mock: 목 저장으로 대체하지 않고 `localhost API를 먼저 실행하세요.` 표시

### 반응형

- `>= 1024px`: 연결 Rail 아래에 공급자 Bay 3열
- `< 1024px`: 1열로 쌓고 저장 행동을 각 Bay 하단에 유지
- 360px에서도 새 키 표시 토글, 모델 입력, 저장, 제거를 키보드와 터치로 완료할 수 있어야 한다.

## 6. 공통 컴포넌트

| 컴포넌트 | 역할 |
|---|---|
| `SpecStatusBadge` | Draft/Validating/Needs Decision/Ready/Stale |
| `ValidationRail` | 실제 Job 단계와 진행 |
| `DecisionSlip` | 질문, 영향, 추천값 |
| `ArtifactTree` | 대상별 실제 파일 경로 |
| `DocumentCanvas` | Preview/Edit/Diff |
| `QualityBar` | 6개 점수 영역과 근거 |
| `FindingCard` | 문제, 근거, 관련 ID, 행동 |
| `TraceabilityTable` | FR-AC-TD 관계 |
| `RegressionDelta` | 의미 변화 유형 |
| `ExportSheet` | 최종 파일과 버전 확인 |

## 7. 상태와 피드백

### Loading

- 콘텐츠 위치가 바뀌지 않는 skeleton
- 2초 이상 작업은 현재 단계 텍스트 표시
- “거의 완료” 같은 근거 없는 문구 금지

### Empty

- 왜 비어 있는지와 다음 행동을 한 문장으로 제공
- 예: “아직 Finding이 없습니다. 첫 검증을 실행하세요.”

### Error

- 사용자 행동으로 해결 가능/불가능 구분
- 재시도 가능 여부
- 데이터가 보존됐는지 명시
- Correlation ID 복사

### Success

- 토스트만 사용하지 않고 문맥상 상태도 업데이트
- Ready 전환 시 파일 수와 다음 행동 표시

### Disabled

- 마우스 hover에만 의존하지 않고 가까운 설명 텍스트 제공
- 예: “Critical Decision 2개를 해결해야 내보낼 수 있습니다.”

## 8. 접근성

- 모든 입력에 연결된 label과 오류 설명
- 단계 변경 시 focus를 새 제목으로 이동
- SSE 상태 변경은 과도하지 않은 `aria-live="polite"` 사용
- Critical 오류는 텍스트와 아이콘 동시 제공
- 모달과 drawer focus trap/복귀
- 키보드로 tab, 문서 선택, Finding 이동, 결정 저장 가능
- Markdown heading 계층 유지
- 최소 44x44px 터치 영역
- 본문/배경 대비 4.5:1 이상
- 애니메이션 감소 설정 존중

## 9. 반응형 기준

| 너비 | 동작 |
|---|---|
| `>= 1280px` | 3열 Workbench 전체 표시 |
| `1024~1279px` | 2열 + Inspector drawer |
| `768~1023px` | 문서 중심, Artifact dropdown |
| `< 768px` | 단일 단계, sticky bottom actions |

모바일에서 기능을 삭제하지 않는다. 복잡한 비교만 전체 화면으로 전환한다.

## 10. 콘텐츠 원칙

- “AI가 생각 중” 대신 “PRD와 TRD의 인증 요구사항을 비교 중”처럼 실제 작업을 쓴다.
- “품질이 낮음” 대신 “FR-009에 수용 기준이 없습니다”처럼 행동 가능하게 쓴다.
- “에이전트”와 “모델”을 구분한다.
- Ready는 “완벽함”이 아니라 정의된 검증 통과 상태임을 설명한다.
- 90%를 보장 문구로 사용하지 않는다.

## 11. UX 수용 기준

1. 처음 방문한 사용자가 10초 안에 “코드 생성기가 아님”을 이해할 수 있다.
2. 새 프로젝트에서 대상 파일 경로를 생성 전 확인할 수 있다.
3. Critical 질문과 추천 기본값을 시각적으로 구분할 수 있다.
4. 생성 중 현재 단계, 완료 단계, 실패 단계를 확인할 수 있다.
5. 점수 차감마다 근거와 해결 행동이 있다.
6. PRD 수정 후 관련 파일이 Stale임을 즉시 알 수 있다.
7. 모바일에서 질문 응답, Finding 해결, ZIP 내보내기를 완료할 수 있다.
8. 키보드만으로 핵심 여정을 완료할 수 있다.
9. 색을 보지 않아도 Ready, Warning, Critical을 구분할 수 있다.
10. 내보내기 전에 정확한 파일 트리와 Spec Version을 확인할 수 있다.
