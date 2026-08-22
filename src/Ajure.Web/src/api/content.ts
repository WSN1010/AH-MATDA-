import type { Decision, IdeaInput, Project } from './types'
import { findTarget, mergedPaths, targetName } from './targets'

/**
 * 로컬 목 모드에서 사용자 입력과 결정 답변으로 명세 Markdown을 만든다.
 * 아주르는 제품 구현 코드를 만들지 않는다. 여기서도 명세 문장만 생성한다.
 */

function answerOf(decisions: Decision[], id: string): string {
  const decision = decisions.find((d) => d.id === id)
  if (!decision) return '미결정'
  if (decision.answerText) return decision.answerText
  const chosen = decision.answerOptionId ?? decision.recommendedOptionId
  return decision.options.find((o) => o.id === chosen)?.label ?? '미결정'
}

function orFallback(value: string, fallback: string): string {
  const trimmed = value.trim()
  return trimmed.length > 0 ? trimmed : fallback
}

function bullets(text: string, fallback: string): string {
  const lines = text
    .split(/\r?\n/)
    .map((line) => line.replace(/^[-*·]\s*/, '').trim())
    .filter((line) => line.length > 0)
  if (lines.length === 0) return `- ${fallback}`
  return lines.map((line) => `- ${line}`).join('\n')
}

function controlTable(project: Project, generatedAt: string): string {
  const targets = project.targetIds.map(targetName).join(', ')
  return [
    '| 필드 | 값 |',
    '|---|---|',
    `| Project | ${project.name} |`,
    `| Spec Version | \`v${project.specVersionNumber}\` |`,
    `| Targets | ${targets} |`,
    `| Generated At | \`${generatedAt}\` |`,
    '| Source | 아주르 ProjectSpec |',
  ].join('\n')
}

export function renderIdeation(project: Project, decisions: Decision[], generatedAt: string): string {
  const idea: IdeaInput = project.idea
  return `# IDEATION - ${project.name}

${controlTable(project, generatedAt)}

## 1. One-line Concept

${orFallback(idea.summary.split(/\r?\n/)[0] ?? '', project.name)}

이 문서는 배경과 결정을 보존한다. 제품 구현 소스 코드는 포함하지 않는다.

## 2. Problem

${bullets(idea.summary, '사용자가 필요한 결과에 도달하기까지 수동 단계가 많다.')}

문제를 해결하지 않으면 구현 에이전트가 매번 다른 가정을 세워 결과가 흔들린다.

## 3. Target Users and JTBD

| Persona | 상황 | 기대 결과 | 구분 |
|---|---|---|---|
| \`P-001\` | ${orFallback(idea.summary.slice(0, 60), '핵심 사용자')}를 직접 다루는 실무자 | 한 번의 요청으로 원하는 결과를 확인한다 | Primary |
| \`P-002\` | 결과를 검토하고 승인하는 담당자 | 변경 이유와 영향을 추적한다 | Secondary |

## 4. Evidence and Assumptions

- 확인된 사실: 사용자가 직접 입력한 조건 — ${orFallback(idea.constraints, '별도 조건 없음')}
- 가설: 아래 결정 답변이 실제 사용 맥락과 일치한다
- 검증 방법: 첫 릴리스 후 \`J-001\` 완주율과 실패 단계 로그를 측정한다

## 5. Options Considered

| 대안 | 내용 | 판정 |
|---|---|---|
| A | 기존 도구를 조합해 수동 운영 | 기각: 단계 간 상태가 유실된다 |
| B | 단일 제품으로 흐름을 통합 | 채택: 결정과 결과를 한 곳에서 추적한다 |

## 6. Value Proposition

- \`P-001\`: 필요한 결정을 한 번만 답하면 결과가 재현된다
- \`P-002\`: 무엇이 왜 바뀌었는지 문서로 확인한다

## 7. Scope

### MVP Must

${bullets(idea.summary, '핵심 사용자 여정 1개를 처음부터 끝까지 완주한다.')}

### Non-goals

${bullets(idea.exclusions, '이번 범위에서 제외할 항목이 명시되지 않았다. 기본값으로 관리자 기능과 결제를 제외한다.')}

## 8. Risks

| Risk | 가능성 | 영향 | 완화 |
|---|---|---|---|
| \`RISK-001\` | 중간 | 높음 | 외부 연동 실패 시 재시도와 사용자 안내 경로를 정의한다 |
| \`RISK-002\` | 낮음 | 중간 | 데이터 보관 정책을 \`TD-003\`에 고정한다 |

## 9. Success Definition

- 사용자 결과: \`J-001\`을 이탈 없이 완주한 비율 80% 이상
- 운영 지표: 치명 오류 0건, 복구 가능한 실패의 재시도 성공률 95% 이상

## 10. Locked Decisions

- 인증 방식: ${answerOf(decisions, 'DEC-001')}
- 데이터 보관: ${answerOf(decisions, 'DEC-002')}
- 첫 릴리스 핵심 여정: ${answerOf(decisions, 'DEC-003')}
- 대상 플랫폼: ${answerOf(decisions, 'DEC-004')}
`
}

export function renderPrd(project: Project, decisions: Decision[], generatedAt: string): string {
  const idea = project.idea
  return `# PRD - ${project.name}

${controlTable(project, generatedAt)}

## 1. Product Overview

${orFallback(idea.summary, project.name)}

이번 Release 범위는 아래 \`FR-001\`~\`FR-006\`이다.

## 2. Goals and Non-goals

| Goal | 내용 | 성공 지표 |
|---|---|---|
| \`GOAL-001\` | 핵심 여정을 한 번에 완주하게 한다 | 완주율 80% 이상 |
| \`GOAL-002\` | 실패 원인을 사용자가 스스로 해결하게 한다 | 오류 화면 이탈률 20% 이하 |

Non-goals:

${bullets(idea.exclusions, '관리자 콘솔과 결제는 이번 범위가 아니다.')}

## 3. Personas

- \`P-001\` 실무자: 결과를 만드는 주 사용자
- \`P-002\` 검토자: 결과를 승인하고 변경을 추적하는 사용자

## 4. User Journeys

### J-001 핵심 여정

- 진입 조건: ${answerOf(decisions, 'DEC-001')} 기준으로 접근 권한이 확인된다
- 단계: 입력 → 확인 → 실행 → 결과 확인
- 성공 종료: 사용자가 결과를 저장하거나 내보낸다
- 실패 경로: 입력 검증 실패, 외부 연동 실패, 권한 부족

## 5. Functional Requirements

### FR-001 [Must] 입력 수집

- Statement: 사용자는 필요한 입력을 한 화면에서 제출할 수 있어야 한다.
- Rationale: 여러 화면으로 나뉘면 중간 이탈이 발생한다.
- Journeys: \`J-001\`
- Acceptance: \`AC-001\`, \`AC-002\`

### FR-002 [Must] 결과 생성

- Statement: 시스템은 제출된 입력으로 결과를 생성하고 상태를 표시해야 한다.
- Rationale: 진행 상태가 없으면 사용자가 중복 실행한다.
- Journeys: \`J-001\`
- Acceptance: \`AC-003\`

### FR-003 [Must] 결과 확인과 수정

- Statement: 사용자는 생성된 결과를 확인하고 수정할 수 있어야 한다.
- Journeys: \`J-001\`
- Acceptance: \`AC-004\`

### FR-004 [Must] 접근 제어

- Statement: 시스템은 ${answerOf(decisions, 'DEC-001')} 방식으로 접근을 통제해야 한다.
- Acceptance: \`AC-005\`

### FR-005 [Should] 결과 내보내기

- Statement: 사용자는 결과를 파일로 내보낼 수 있어야 한다.
- Acceptance: \`AC-006\`

### FR-006 [Should] 실패 복구

- Statement: 재시도 가능한 실패는 사용자가 같은 지점에서 다시 시도할 수 있어야 한다.
- Acceptance: \`AC-007\`

## 6. State Matrix

| 화면 | Loading | Empty | Error | Success | Disabled |
|---|---|---|---|---|---|
| 입력 | 제출 진행 표시 | 최초 안내 문구 | 필드별 오류 메시지 | 다음 단계 이동 | 필수값 미입력 시 제출 비활성 |
| 결과 | 단계 표시 skeleton | "아직 결과가 없습니다" | 재시도 가능 여부 표시 | 결과 요약 | 권한 없을 때 편집 비활성 |

## 7. Non-functional Requirements

| NFR | 내용 | 검증 |
|---|---|---|
| \`NFR-001\` | 주요 화면 상호작용 응답 200ms 이내 | 성능 측정 |
| \`NFR-002\` | 키보드만으로 \`J-001\` 완주 가능 | 수동 접근성 점검 |
| \`NFR-003\` | 데이터 보관: ${answerOf(decisions, 'DEC-002')} | 보관 정책 검토 |
| \`NFR-004\` | 실시간성: ${answerOf(decisions, 'DEC-005')} | 통합 테스트 |

## 8. Business Rules

- 권한이 없는 사용자는 결과를 조회할 수 없다.
- 동일 입력의 중복 제출은 마지막 요청만 적용한다.

## 9. Analytics

| 이벤트 | 속성 | 목적 |
|---|---|---|
| \`journey_started\` | entryPoint | 진입 경로 비교 |
| \`journey_completed\` | durationMs | 완주율 측정 |
| \`journey_failed\` | stage, retryable | 실패 단계 파악 |

## 10. Acceptance Criteria

| AC | Given / When / Then | 검증 유형 | 연결 |
|---|---|---|---|
| \`AC-001\` | 필수 입력이 비었을 때 제출하면 필드별 오류가 표시된다 | UI | \`FR-001\` |
| \`AC-002\` | 유효한 입력을 제출하면 다음 단계로 이동한다 | UI | \`FR-001\` |
| \`AC-003\` | 생성 중에는 현재 단계와 경과 시간이 표시된다 | UI | \`FR-002\` |
| \`AC-004\` | 결과를 수정하고 저장하면 변경 이력이 남는다 | API | \`FR-003\` |
| \`AC-005\` | 권한 없는 사용자가 접근하면 403과 안내가 표시된다 | API | \`FR-004\` |
| \`AC-006\` | 내보내기를 실행하면 파일 하나가 다운로드된다 | UI | \`FR-005\` |
| \`AC-007\` | 재시도 가능한 실패에서 다시 시도하면 같은 단계부터 진행한다 | 통합 | \`FR-006\` |

## 11. Traceability Matrix

| Requirement | Acceptance Criteria | Journey |
|---|---|---|
| \`FR-001\` | \`AC-001\`, \`AC-002\` | \`J-001\` |
| \`FR-002\` | \`AC-003\` | \`J-001\` |
| \`FR-003\` | \`AC-004\` | \`J-001\` |
| \`FR-004\` | \`AC-005\` | \`J-001\` |
| \`FR-005\` | \`AC-006\` | \`J-001\` |
| \`FR-006\` | \`AC-007\` | \`J-001\` |

## 12. Release Scope

- MVP: \`FR-001\`~\`FR-004\`
- 후속: \`FR-005\`, \`FR-006\`
- 출시 차단 조건: \`AC-005\` 미충족
`
}

export function renderTrd(project: Project, decisions: Decision[], generatedAt: string): string {
  const idea = project.idea
  return `# TRD - ${project.name}

${controlTable(project, generatedAt)}

## 1. Technical Scope and Constraints

고정 조건:

${bullets(idea.constraints, '런타임과 배포 대상이 지정되지 않았다. 기본값으로 컨테이너 기반 배포를 사용한다.')}

- 대상 플랫폼: ${answerOf(decisions, 'DEC-004')}
- 금지: 명세에 없는 외부 결제·과금 연동

이 문서는 API 계약과 구조를 정의하며 제품 구현 소스 코드는 포함하지 않는다.

## 2. Architecture

\`\`\`text
Client ──▶ API ──▶ Application Service ──▶ Storage
                     │
                     └──▶ Background Worker
\`\`\`

신뢰 경계는 Client와 API 사이다. 자격증명은 서버에만 둔다.

## 3. Technical Decisions

### TD-001 실행 플랫폼

- 결정: ${answerOf(decisions, 'DEC-004')}
- 근거: 사용자 접근 경로와 운영 비용
- 연결: \`FR-001\`, \`FR-002\`

### TD-002 인증과 권한

- 결정: ${answerOf(decisions, 'DEC-001')}
- 근거: \`AC-005\`를 관찰 가능한 방식으로 검증하기 위함
- 연결: \`FR-004\`

### TD-003 데이터 보관

- 결정: ${answerOf(decisions, 'DEC-002')}
- 근거: \`NFR-003\` 보관 정책
- 연결: \`FR-003\`, \`RISK-002\`

### TD-004 진행 상태 전달

- 결정: ${answerOf(decisions, 'DEC-005')}
- 근거: \`AC-003\`의 단계 표시 요구
- 연결: \`FR-002\`

### TD-005 로케일

- 결정: ${answerOf(decisions, 'DEC-006')}
- 연결: \`FR-001\`

## 4. Data Model

\`\`\`text
Record
  id, ownerId, status, input, result, createdAt, updatedAt
AuditEvent
  id, recordId, actor, action, occurredAt
\`\`\`

## 5. API Contract

| Method | Path | 목적 |
|---|---|---|
| \`POST\` | \`/api/records\` | 입력 제출 |
| \`GET\` | \`/api/records/{id}\` | 결과 조회 |
| \`PUT\` | \`/api/records/{id}\` | 결과 수정 |
| \`POST\` | \`/api/records/{id}/export\` | 결과 내보내기 |

오류는 \`code\`, \`message\`, \`correlationId\`, \`retryable\`을 가진 Problem Details로 응답한다.

## 6. Error Handling and Recovery

- 재시도 가능한 실패는 같은 요청 ID로 다시 실행한다.
- 재시도 불가능한 실패는 사용자 행동 안내를 함께 반환한다.

## 7. Security and Privacy

- 인증: \`TD-002\`
- 보관: \`TD-003\`
- 비밀은 코드나 문서에 저장하지 않고 시크릿 저장소를 사용한다.
- 오류 수집 범위: ${answerOf(decisions, 'DEC-007')}

## 8. Observability

- 구조화 로그에 \`correlationId\`를 포함한다.
- 실패 단계, 재시도 횟수, 처리 시간을 지표로 남긴다.

## 9. Testing Strategy

| 유형 | 대상 |
|---|---|
| 단위 | 입력 검증, 상태 전이 |
| 통합 | \`AC-003\`, \`AC-007\` |
| E2E | \`J-001\` 전체 완주 |
| 접근성 | 키보드 완주와 대비 확인 |

## 10. Deployment

- 빌드 후 컨테이너 이미지로 배포한다.
- 배포 검증: 헬스 체크 통과와 \`J-001\` 스모크 테스트
`
}

const PRECEDENCE = [
  '1. 사용자의 최신 명시 요청',
  '2. 이 지침 파일의 실행·검증 규칙',
  '3. `PRD.md`의 제품 행동과 수용 기준',
  '4. `TRD.md`의 기술 제약과 운영 조건',
  '5. `IDEATION.md`의 배경과 의도',
].join('\n')

function instructionBody(project: Project, decisions: Decision[], toolNames: string[]): string {
  return `## Mission

${orFallback(project.idea.summary, project.name)}

스캐폴드만 만들지 말고 \`PRD.md\`의 수용 기준을 끝까지 구현한다. 명세에 없는 기능은 임의로 추가하지 않는다.

## 읽는 순서

1. \`IDEATION.md\` — 배경, 범위, 잠긴 결정
2. \`PRD.md\` — 요구사항과 수용 기준
3. \`TRD.md\` — 기술 계약과 배포 조건
4. 이 파일 — 실행 순서와 완료 기준

## 문서 우선순위

${PRECEDENCE}

## 잠긴 결정

- 인증: ${answerOf(decisions, 'DEC-001')}
- 데이터 보관: ${answerOf(decisions, 'DEC-002')}
- 대상 플랫폼: ${answerOf(decisions, 'DEC-004')}
- 진행 상태 전달: ${answerOf(decisions, 'DEC-005')}

## 구현 범위

- \`FR-001\`~\`FR-004\`를 먼저 완성한다.
- \`FR-005\`, \`FR-006\`은 앞의 요구사항이 모두 통과한 뒤 진행한다.

## 하지 않을 것

${bullets(project.idea.exclusions, '관리자 콘솔과 결제 연동은 구현하지 않는다.')}

## 실행 순서

1. 세 문서를 읽고 요구사항 목록을 만든다.
2. 데이터 모델과 API 계약을 \`TRD.md\` §4~§5에 맞춰 구현한다.
3. 화면 상태(Loading/Empty/Error/Success/Disabled)를 \`PRD.md\` §6대로 구현한다.
4. 수용 기준마다 검증 코드를 추가한다.
5. 빌드·테스트·접근성 점검을 실행한다.

## 완료 기준

- \`AC-001\`~\`AC-007\`이 모두 통과한다.
- 빌드와 테스트가 성공한다.
- 키보드만으로 \`J-001\`을 완주할 수 있다.
- 비밀 값이 저장소에 포함되지 않는다.

## 안전 규칙

- 실제 비밀 값을 파일에 쓰지 않는다.
- 사용자 데이터를 지우는 파괴적 작업은 확인 없이 실행하지 않는다.

## 모호할 때

명세가 충분하면 되묻지 않고 실행한다. \`PRD.md\`와 \`TRD.md\`가 충돌하면 위 우선순위를 따르고 충돌 사실을 결과에 기록한다.

## 대상 도구

${toolNames.map((name) => `- ${name}`).join('\n')}
`
}

/** 대상 지침 파일 하나를 렌더링한다. 같은 경로를 공유하는 도구는 한 파일로 병합한다. */
export function renderInstruction(
  project: Project,
  decisions: Decision[],
  path: string,
  targetIds: string[],
  generatedAt: string,
): string {
  const toolNames = targetIds.map(targetName)
  const title = `${project.name} 구현 지침`
  const body = instructionBody(project, decisions, toolNames)
  const meta = `> Spec Version \`v${project.specVersionNumber}\` · Generated \`${generatedAt}\` · Source 아주르 ProjectSpec`

  if (path.endsWith('.mdc')) {
    return `---
description: ${title}
globs: ["**/*"]
alwaysApply: true
---

# ${title}

${meta}

${body}`
  }

  return `# ${title}

${meta}

${body}`
}

export interface RenderedArtifact {
  kind: 'Ideation' | 'Prd' | 'Trd' | 'AgentInstruction'
  targetId: string | null
  path: string
  content: string
}

export function renderAll(project: Project, decisions: Decision[], generatedAt: string): RenderedArtifact[] {
  const common: RenderedArtifact[] = [
    { kind: 'Ideation', targetId: null, path: 'IDEATION.md', content: renderIdeation(project, decisions, generatedAt) },
    { kind: 'Prd', targetId: null, path: 'PRD.md', content: renderPrd(project, decisions, generatedAt) },
    { kind: 'Trd', targetId: null, path: 'TRD.md', content: renderTrd(project, decisions, generatedAt) },
  ]
  const instructions: RenderedArtifact[] = mergedPaths(project.targetIds).map(({ path, targetIds }) => ({
    kind: 'AgentInstruction' as const,
    targetId: targetIds[0] ?? null,
    path,
    content: renderInstruction(project, decisions, path, targetIds, generatedAt),
  }))
  return [...common, ...instructions]
}

/** VALIDATION-REPORT.md는 기본 번들에 넣지 않고 선택 항목으로만 제공한다. AI-FILE-SPEC §4 */
export function renderValidationReport(
  project: Project,
  score: number,
  gatesPassed: number,
  gatesTotal: number,
  generatedAt: string,
): string {
  return `# VALIDATION REPORT - ${project.name}

| 필드 | 값 |
|---|---|
| Spec Version | \`v${project.specVersionNumber}\` |
| One-Shot Readiness Score | ${score} / 100 |
| Hard Gate | ${gatesPassed} / ${gatesTotal} |
| Generated At | \`${generatedAt}\` |

Ready는 완벽함이 아니라 아주르가 정의한 검증을 통과한 상태를 뜻한다.

## 대상 파일

${mergedPaths(project.targetIds)
  .map(({ path, targetIds }) => `- \`${path}\` — ${targetIds.map((id) => findTarget(id)?.name ?? id).join(', ')}`)
  .join('\n')}
`
}
