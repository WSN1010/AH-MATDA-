import type {
  Artifact,
  ArtifactContent,
  ArtifactSaveResult,
  Decision,
  Finding,
  HardGate,
  IdeaInput,
  JobEvent,
  JobStage,
  JobStatus,
  Project,
  ProjectSummary,
  RegressionItem,
  ScoreArea,
  SpecStatus,
  ValidationRun,
} from './types'
import { problem } from './error'
import { renderAll, renderValidationReport } from './content'
import { createZip } from './zip'

/**
 * 백엔드와 Fake 모드가 모두 없을 때만 쓰는 인-프로세스 목이다.
 * IMPLEMENTATION-FRONTEND §3에 따라 목 서버 라이브러리를 쓰지 않고 fetch 래퍼 뒤에 둔다.
 * 요청 경로와 응답 모양은 TRD §9 계약을 그대로 따른다.
 */

/** 실패 화면 확인용 스위치. 정상 동작에서는 null이다. */
const FAILURE_STAGE: string | null = null

const STORAGE_KEY = 'ajure.mock.v1'
const STAGE_MS = 1400

interface StoredArtifact extends ArtifactContent {
  /** 마지막 생성 시점의 내용. 회귀 비교 기준이다. */
  baseline: string
}

interface StoredJob {
  jobId: string
  projectId: string
  specVersionId: string
  kind: 'generate' | 'validate'
  startedAtMs: number
  correlationId: string
}

interface StoredProject {
  id: string
  name: string
  idea: IdeaInput
  targetIds: string[]
  status: SpecStatus
  specVersionId: string
  specVersionNumber: number
  createdAt: string
  updatedAt: string
  decisions: Decision[]
  artifacts: StoredArtifact[]
  jobs: StoredJob[]
  runs: ValidationRun[]
}

interface MockState {
  projects: StoredProject[]
}

function uid(prefix: string): string {
  const random =
    typeof crypto !== 'undefined' && 'randomUUID' in crypto
      ? crypto.randomUUID().slice(0, 8)
      : Math.random().toString(36).slice(2, 10)
  return `${prefix}_${random}`
}

function load(): MockState {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    if (raw) return JSON.parse(raw) as MockState
  } catch {
    // 저장소를 읽을 수 없으면 빈 상태로 시작한다.
  }
  return { projects: [] }
}

let state: MockState = load()

function save(): void {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(state))
  } catch {
    // 용량 초과 등으로 저장할 수 없어도 현재 세션 동작은 유지한다.
  }
}

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms))
}

function projectOf(id: string): StoredProject {
  const found = state.projects.find((p) => p.id === id)
  if (!found) throw problem('project_not_found', '요청한 프로젝트를 찾을 수 없습니다.', false)
  return found
}

function projectByVersion(versionId: string): StoredProject {
  const found = state.projects.find((p) => p.specVersionId === versionId)
  if (!found) throw problem('version_not_found', '요청한 명세 버전을 찾을 수 없습니다.', false)
  return found
}

function projectByJob(jobId: string): StoredProject {
  const found = state.projects.find((p) => p.jobs.some((j) => j.jobId === jobId))
  if (!found) throw problem('job_not_found', '요청한 Job을 찾을 수 없습니다.', false)
  return found
}

function hash(text: string): string {
  let h = 0x811c9dc5
  for (let i = 0; i < text.length; i++) {
    h ^= text.charCodeAt(i)
    h = Math.imul(h, 0x01000193)
  }
  return (h >>> 0).toString(16).padStart(8, '0')
}

// ---------------------------------------------------------------- 결정 인터뷰

function buildDecisions(): Decision[] {
  const base = (
    id: string,
    kind: Decision['kind'],
    question: string,
    why: string,
    impact: string,
    options: Decision['options'],
    recommendedOptionId: string,
    recommendationRationale: string,
    conflicts: Decision['conflicts'] = [],
  ): Decision => ({
    id,
    kind,
    question,
    why,
    impact,
    options,
    recommendedOptionId,
    recommendationRationale,
    answerOptionId: null,
    answerText: null,
    answeredAt: null,
    conflicts,
  })

  return [
    base(
      'DEC-001',
      'Critical',
      '사용자는 어떤 방식으로 인증하나요?',
      '인증 주체가 정해지지 않으면 구현 에이전트가 데이터 소유자와 권한 검사를 임의로 설계합니다.',
      'API 권한 검사, 데이터 소유 필드, 로그인 화면 유무가 달라집니다.',
      [
        { id: 'a', label: '이메일 매직 링크', detail: '비밀번호를 저장하지 않고 이메일 소유만 확인합니다.' },
        { id: 'b', label: 'GitHub / Google OAuth', detail: '외부 계정을 사용해 계정 관리 부담을 줄입니다.' },
        { id: 'c', label: '조직 SSO (OIDC)', detail: '회사 계정을 가진 사용자만 접근합니다.' },
        { id: 'd', label: '인증 없이 링크 공유', detail: '링크를 아는 사람은 누구나 접근합니다.' },
      ],
      'b',
      '초기 사용자 대부분이 개발자이고, 별도 계정 관리 없이 소유자를 식별할 수 있습니다.',
    ),
    base(
      'DEC-002',
      'Critical',
      '사용자 데이터는 어디에 얼마나 보관하나요?',
      '보관 기간이 없으면 삭제 요구와 개인정보 처리 기준을 검증할 수 없습니다.',
      '저장소 스키마, 삭제 배치, 개인정보 처리방침 문구가 달라집니다.',
      [
        { id: 'a', label: '30일 후 자동 삭제', detail: '짧은 보관으로 위험을 줄이지만 이력 조회가 제한됩니다.' },
        { id: 'b', label: '사용자가 삭제할 때까지 보관', detail: '이력을 유지하지만 소유자 식별이 필요합니다.' },
        { id: 'c', label: '90일 보관 후 익명화', detail: '분석에 필요한 통계를 남기고 식별 정보를 제거합니다.' },
        { id: 'd', label: '저장하지 않고 세션에서만 처리', detail: '새로고침하면 작업이 사라집니다.' },
      ],
      'c',
      '이력 확인 요구와 개인정보 최소 보관 원칙을 함께 만족합니다.',
      [
        {
          optionId: 'b',
          decisionId: 'DEC-001',
          withOptionId: 'd',
          message:
            '인증 없이 링크를 공유하면 "사용자가 삭제할 때까지"의 소유자를 특정할 수 없습니다. 인증 방식을 먼저 바꾸거나 다른 보관 정책을 선택하세요.',
        },
      ],
    ),
    base(
      'DEC-003',
      'Critical',
      '첫 릴리스에서 반드시 완주되어야 하는 사용자 여정은 무엇인가요?',
      '핵심 여정이 하나로 고정되지 않으면 구현 범위가 계속 늘어납니다.',
      'J-001의 단계와 출시 차단 조건이 달라집니다.',
      [
        { id: 'a', label: '입력 → 결과 생성 → 저장', detail: '혼자 쓰는 기본 흐름을 먼저 완성합니다.' },
        { id: 'b', label: '결과 공유와 공동 편집', detail: '협업이 핵심이면 권한 모델이 커집니다.' },
        { id: 'c', label: '외부 도구로 내보내기', detail: '연동 대상의 실패 처리까지 정의해야 합니다.' },
      ],
      'a',
      '나머지 여정은 이 흐름이 완주된 뒤에야 의미가 있습니다.',
    ),
    base(
      'DEC-004',
      'Important',
      '어떤 플랫폼을 먼저 지원하나요?',
      '입력 화면의 밀도와 반응형 기준이 플랫폼에 따라 달라집니다.',
      '레이아웃 기준 폭, 터치 영역, 배포 방식이 달라집니다.',
      [
        { id: 'a', label: '웹 (데스크톱 우선)', detail: '넓은 화면에서 여러 패널을 동시에 보여 줍니다.' },
        { id: 'b', label: '웹 (모바일 우선)', detail: '한 번에 한 단계만 보여 주는 흐름이 기본이 됩니다.' },
        { id: 'c', label: 'CLI', detail: '화면 대신 명령과 종료 코드로 결과를 확인합니다.' },
      ],
      'a',
      '핵심 작업이 비교와 검토여서 넓은 화면에서 이득이 큽니다.',
    ),
    base(
      'DEC-005',
      'Important',
      '진행 상태를 실시간으로 보여 줘야 하나요?',
      '장시간 작업의 진행 표시 방식이 API 설계를 바꿉니다.',
      'AC-003 검증 방법과 서버 연결 유지 비용이 달라집니다.',
      [
        { id: 'a', label: 'SSE로 단계 전달', detail: '재연결 시 마지막 이벤트부터 이어볼 수 있습니다.' },
        { id: 'b', label: '3초 간격 폴링', detail: '구현이 단순하지만 지연과 요청 수가 늘어납니다.' },
        { id: 'c', label: '완료 후 한 번만 표시', detail: '작업이 짧을 때만 허용됩니다.' },
      ],
      'a',
      '작업이 10초를 넘기고 단계별 결과가 사용자 판단에 필요합니다.',
    ),
    base(
      'DEC-006',
      'Defaultable',
      '기본 언어와 로케일은 무엇인가요?',
      '문구와 날짜 형식의 기준이 필요합니다.',
      'UI 문구, 날짜 표기, 정렬 규칙이 달라집니다.',
      [
        { id: 'a', label: '한국어 단일', detail: '문구를 한 벌만 유지합니다.' },
        { id: 'b', label: '한국어 + 영어', detail: '문구 키 관리와 번역 검수 작업이 추가됩니다.' },
        { id: 'c', label: '영어 단일', detail: '해외 사용자를 우선합니다.' },
      ],
      'a',
      '초기 사용자가 국내 팀이고 번역 유지 비용을 아직 감당할 이유가 없습니다.',
    ),
    base(
      'DEC-007',
      'Defaultable',
      '오류 리포팅을 어디까지 수집하나요?',
      '개인정보 최소 수집 기준을 문서에 고정해야 합니다.',
      '로그 스키마와 개인정보 처리 문구가 달라집니다.',
      [
        { id: 'a', label: '오류 메시지와 스택만', detail: '식별 정보 없이 원인 분석에 필요한 최소 정보만 남깁니다.' },
        { id: 'b', label: '사용자 식별자 포함', detail: '재현이 쉬워지지만 개인정보 처리 근거가 필요합니다.' },
        { id: 'c', label: '수집하지 않음', detail: '운영 중 원인 분석이 어려워집니다.' },
      ],
      'a',
      '원인 분석에 필요한 최소 정보만 남겨 개인정보 위험을 줄입니다.',
    ),
  ]
}

function openCriticalCount(project: StoredProject): number {
  return project.decisions.filter((d) => d.kind === 'Critical' && d.answeredAt === null).length
}

// ------------------------------------------------------------------ Job 단계

const GENERATE_STAGES: { id: string; label: string; detail: string; findings: number | null }[] = [
  { id: 'intake', label: '입력 구조화', detail: '아이디어 원문에서 요구사항 후보와 제약을 추출', findings: null },
  { id: 'authoring', label: '공통 문서 생성', detail: 'IDEATION / PRD / TRD 초안 작성', findings: null },
  { id: 'deterministic', label: '결정적 검사', detail: 'ID 유일성, FR/NFR → AC 연결률, 필수 섹션 확인', findings: 1 },
  { id: 'review', label: '독립 평가', detail: '제품·기술·UX 평가자가 같은 ProjectSpec을 따로 채점', findings: 2 },
  { id: 'simulation', label: '구현 시뮬레이션', detail: '코드를 쓰지 않고 예상 작업과 파일 지도를 만들어 빈틈 탐지', findings: 0 },
  { id: 'regression', label: '회귀 검사', detail: '기준 버전의 요구사항 그래프와 후보 그래프를 비교', findings: 0 },
  { id: 'render', label: '대상 파일 렌더링', detail: '선택한 도구의 네이티브 지침 파일 생성', findings: null },
]

const VALIDATE_STAGE_IDS = ['deterministic', 'review', 'simulation', 'regression', 'render']

function stagesFor(kind: StoredJob['kind']) {
  return kind === 'generate'
    ? GENERATE_STAGES
    : GENERATE_STAGES.filter((s) => VALIDATE_STAGE_IDS.includes(s.id))
}

interface TimelineEntry {
  offsetMs: number
  event: JobEvent
}

function timeline(job: StoredJob): TimelineEntry[] {
  const stages = stagesFor(job.kind)
  const entries: TimelineEntry[] = []
  let sequence = 0
  let offset = 0
  let failed = false

  for (const stage of stages) {
    if (failed) break
    sequence += 1
    entries.push({
      offsetMs: offset,
      event: {
        sequence,
        eventType: 'stage',
        stageId: stage.id,
        status: 'Running',
        summary: stage.detail,
        occurredAt: new Date(job.startedAtMs + offset).toISOString(),
        durationMs: null,
        findingCount: null,
        retryable: false,
        correlationId: job.correlationId,
      },
    })
    offset += STAGE_MS
    sequence += 1
    const isFailure = FAILURE_STAGE === stage.id
    entries.push({
      offsetMs: offset,
      event: {
        sequence,
        eventType: 'stage',
        stageId: stage.id,
        status: isFailure ? 'Failed' : 'Done',
        summary: isFailure
          ? '평가 모델 세션이 응답하지 않아 단계를 마치지 못했습니다.'
          : stage.findings === null
            ? `${stage.label} 완료`
            : `${stage.label} 완료 · 발견 ${stage.findings}건`,
        occurredAt: new Date(job.startedAtMs + offset).toISOString(),
        durationMs: STAGE_MS,
        findingCount: stage.findings,
        retryable: isFailure,
        correlationId: job.correlationId,
      },
    })
    if (isFailure) failed = true
  }

  sequence += 1
  entries.push({
    offsetMs: offset + 300,
    event: {
      sequence,
      eventType: 'terminal',
      stageId: failed ? (FAILURE_STAGE ?? '') : 'render',
      status: failed ? 'Failed' : 'Succeeded',
      summary: failed
        ? '재시도 가능한 실패입니다. 실패한 단계부터 다시 실행할 수 있습니다.'
        : '검증까지 완료했습니다. 워크벤치에서 결과를 확인하세요.',
      occurredAt: new Date(job.startedAtMs + offset + 300).toISOString(),
      durationMs: offset + 300,
      findingCount: null,
      retryable: failed,
      correlationId: job.correlationId,
    },
  })
  return entries
}

function jobStatusOf(job: StoredProject['jobs'][number], project: StoredProject): JobStatus {
  const entries = timeline(job)
  const elapsed = Date.now() - job.startedAtMs
  const applied = entries.filter((e) => e.offsetMs <= elapsed)
  const stages: JobStage[] = stagesFor(job.kind).map((stage) => ({
    id: stage.id,
    label: stage.label,
    detail: stage.detail,
    status: 'Pending',
    startedAt: null,
    durationMs: null,
    findingCount: null,
  }))

  let status: JobStatus['status'] = applied.length === 0 ? 'Queued' : 'Running'
  let failure: JobStatus['failure'] = null

  for (const { event } of applied) {
    if (event.eventType === 'terminal') {
      status = event.status === 'Failed' ? 'Failed' : 'Succeeded'
      continue
    }
    const stage = stages.find((s) => s.id === event.stageId)
    if (!stage) continue
    if (event.status === 'Running') {
      stage.status = 'Running'
      stage.startedAt = event.occurredAt
    } else if (event.status === 'Failed') {
      stage.status = 'Failed'
      stage.durationMs = event.durationMs
      failure = {
        stageId: stage.id,
        code: 'model_session_timeout',
        message: event.summary,
        retryable: true,
      }
    } else {
      stage.status = 'Done'
      stage.durationMs = event.durationMs
      stage.findingCount = event.findingCount
    }
  }

  const last = applied.at(-1)
  return {
    jobId: job.jobId,
    projectId: job.projectId,
    specVersionId: job.specVersionId,
    status,
    stages,
    lastSequence: last ? last.event.sequence : 0,
    correlationId: job.correlationId,
    failure,
    validationRunId: status === 'Succeeded' ? (project.runs.at(-1)?.id ?? null) : null,
  }
}

/** Job이 끝났으면 산출물과 검증 결과를 확정한다. 여러 번 호출해도 결과가 같다. */
function settleJob(project: StoredProject, job: StoredJob): void {
  const status = jobStatusOf(job, project)
  if (status.status !== 'Succeeded') return
  if (project.runs.some((r) => r.id === `run_${job.jobId}`)) return
  const generatedAt = new Date(job.startedAtMs).toISOString()

  if (job.kind === 'generate' && project.artifacts.length === 0) {
    project.artifacts = renderAll(toProject(project), project.decisions, generatedAt).map((rendered) => ({
      id: uid('art'),
      kind: rendered.kind,
      targetId: rendered.targetId,
      path: rendered.path,
      status: 'Valid',
      specVersionNumber: project.specVersionNumber,
      contentHash: hash(rendered.content),
      updatedAt: generatedAt,
      staleReason: null,
      content: rendered.content,
      baseline: rendered.content,
    }))
  }

  if (job.kind === 'validate') {
    // 지침 파일은 명세에서 파생되므로 다시 렌더링하고, 사용자가 직접 고친 문서는 유지한다.
    const rerendered = renderAll(toProject(project), project.decisions, generatedAt)
    for (const artifact of project.artifacts) {
      if (artifact.kind === 'AgentInstruction') {
        const match = rerendered.find((r) => r.path === artifact.path)
        if (match) {
          artifact.content = match.content
          artifact.contentHash = hash(match.content)
        }
      }
      artifact.status = 'Valid'
      artifact.staleReason = null
      artifact.updatedAt = generatedAt
    }
  }

  const run = buildValidationRun(project, `run_${job.jobId}`)
  project.runs.push(run)
  project.status = run.ready ? 'Ready' : 'NeedsDecision'
  project.updatedAt = new Date().toISOString()
  for (const artifact of project.artifacts) {
    artifact.baseline = artifact.content
  }
  save()
}

// -------------------------------------------------------------- 검증 리포트

function lineOf(content: string, needle: string): number | null {
  const lines = content.split('\n')
  const index = lines.findIndex((line) => line.includes(needle))
  return index === -1 ? null : index + 1
}

const AC_FIX_ANCHOR = '| `AC-007` |'
const AC_FIX_ROWS = [
  '| `AC-008` | 목록 화면을 1,000건 데이터로 열면 상호작용 응답이 200ms 이내다 | 성능 | `NFR-001` |',
  '| `AC-009` | 키보드만으로 `J-001`의 모든 단계를 완주할 수 있다 | 수동 | `NFR-002` |',
  '| `AC-010` | 보관 기간이 지난 레코드는 다음 배치에서 조회되지 않는다 | API | `NFR-003` |',
  '| `AC-011` | 생성 중 진행 이벤트가 3초 이상 멈추지 않는다 | 통합 | `NFR-004` |',
].join('\n')
const TRACE_FIX_ANCHOR = '| `FR-006` | `AC-007` | `J-001` |'
const TRACE_FIX_ROWS = [
  '| `NFR-001` | `AC-008` | `J-001` |',
  '| `NFR-002` | `AC-009` | `J-001` |',
  '| `NFR-003` | `AC-010` | `J-001` |',
  '| `NFR-004` | `AC-011` | `J-001` |',
].join('\n')

const EXPORT_AC_BEFORE = '| `AC-006` | 내보내기를 실행하면 파일 하나가 다운로드된다 | UI | `FR-005` |'
const EXPORT_AC_AFTER =
  '| `AC-006` | 내보내기를 실행하면 UTF-8 CSV 파일 하나가 5초 이내에 다운로드된다 | UI | `FR-005` |'

/** Finding이 제안하는 수정을 문서 내용에 적용한다. 적용 여부는 내용으로 판별한다. */
export function applyFindingFix(findingId: string, content: string): string {
  if (findingId === 'FND-001') {
    if (content.includes('`AC-008`')) return content
    return content
      .replace(AC_FIX_ANCHOR, `${AC_FIX_ROWS}\n${AC_FIX_ANCHOR}`)
      .replace(TRACE_FIX_ANCHOR, `${TRACE_FIX_ANCHOR}\n${TRACE_FIX_ROWS}`)
  }
  if (findingId === 'FND-002') {
    return content.replace(EXPORT_AC_BEFORE, EXPORT_AC_AFTER)
  }
  return content
}

function buildFindings(project: StoredProject): Finding[] {
  const prd = project.artifacts.find((a) => a.kind === 'Prd')
  const trd = project.artifacts.find((a) => a.kind === 'Trd')
  const prdContent = prd?.content ?? ''
  const decision = project.decisions.find((d) => d.id === 'DEC-007')

  const findings: Finding[] = [
    {
      id: 'FND-001',
      severity: 'Critical',
      title: 'NFR-001~NFR-004에 연결된 수용 기준이 없습니다.',
      evidence:
        'PRD §11 추적 매트릭스에 FR만 있고 NFR 행이 없습니다. Hard Gate HG-02는 수용 기준이 없는 요구사항을 차단합니다.',
      relatedIds: ['NFR-001', 'NFR-002', 'NFR-003', 'NFR-004', 'HG-02'],
      artifactPath: 'PRD.md',
      line: lineOf(prdContent, '## 11. Traceability Matrix'),
      autoFixable: true,
      suggestion: 'NFR별 수용 기준 AC-008~AC-011을 추가하고 추적 매트릭스에 연결합니다.',
      decisionId: null,
      resolved: prdContent.includes('`AC-008`'),
    },
    {
      id: 'FND-002',
      severity: 'Major',
      title: 'AC-006이 구현 결과로 판정하기 어렵습니다.',
      evidence: '"파일 하나가 다운로드된다"에는 형식과 시간 기준이 없어 통과/실패를 관찰할 수 없습니다.',
      relatedIds: ['AC-006', 'FR-005'],
      artifactPath: 'PRD.md',
      line: lineOf(prdContent, '`AC-006`'),
      autoFixable: true,
      suggestion: '파일 형식과 완료 시간을 넣어 "UTF-8 CSV 파일 하나가 5초 이내에 다운로드된다"로 바꿉니다.',
      decisionId: null,
      resolved: prdContent.includes('5초 이내에 다운로드'),
    },
    {
      id: 'FND-003',
      severity: 'Minor',
      title: '오류 리포팅 수집 범위가 개인정보 최소 수집 기준과 연결되지 않았습니다.',
      evidence: 'TRD §7에 수집 범위가 적혀 있지만 사용자가 직접 승인한 결정 기록이 없습니다.',
      relatedIds: ['TD-003', 'DEC-007'],
      artifactPath: 'TRD.md',
      line: lineOf(trd?.content ?? '', '오류 수집 범위'),
      autoFixable: false,
      suggestion: null,
      decisionId: 'DEC-007',
      resolved: Boolean(decision?.answeredAt),
    },
  ]
  return findings
}

const GATE_LABELS: { id: string; label: string }[] = [
  { id: 'HG-01', label: '미해결 Critical Decision 없음' },
  { id: 'HG-02', label: '모든 FR/NFR에 수용 기준 연결' },
  { id: 'HG-03', label: 'PRD와 TRD 사이 Critical 모순 없음' },
  { id: 'HG-04', label: 'Must 요구사항이 대상 지침 파일에 보존됨' },
  { id: 'HG-05', label: '공통 문서와 대상 파일의 Spec Version 일치' },
  { id: 'HG-06', label: '기준 버전의 Must 요구사항 무단 삭제 없음' },
  { id: 'HG-07', label: '모든 수용 기준이 구현 결과로 검증 가능' },
  { id: 'HG-08', label: '인증·권한·데이터 보호 결정 존재' },
  { id: 'HG-09', label: '대상 파일 경로와 문법 정상' },
  { id: 'HG-10', label: '산출물에 구현 코드나 실제 비밀 없음' },
  { id: 'HG-11', label: '동일 Critical Finding 반복 없음' },
  { id: 'HG-12', label: '평가자 결과 합의됨' },
  { id: 'HG-13', label: 'Copilot SDK 작성·평가 단계 완료' },
]

function buildRegression(project: StoredProject): RegressionItem[] {
  const items: RegressionItem[] = []
  const idPattern = /\b(?:FR|NFR|AC|TD|J|GOAL|RISK)-\d{3}\b/g

  for (const artifact of project.artifacts) {
    if (artifact.baseline === artifact.content) continue
    const before = new Set(artifact.baseline.match(idPattern) ?? [])
    const after = new Set(artifact.content.match(idPattern) ?? [])
    const removed = [...before].filter((id) => !after.has(id))

    for (const id of removed) {
      items.push({
        id: `REG-${id}`,
        kind: 'Removed',
        severity: 'Critical',
        requirementId: id,
        before: `${artifact.path}에 ${id}가 있었습니다.`,
        after: `${artifact.path}에서 ${id} 언급이 사라졌습니다.`,
        summary: '기준 버전의 요구사항이 후보 버전에서 확인되지 않습니다. 삭제 의도라면 승인 기록이 필요합니다.',
        approved: false,
      })
    }

    const added = [...after].filter((id) => !before.has(id))
    items.push({
      id: `REG-EDIT-${artifact.path}`,
      kind: 'ApprovedChange',
      severity: 'Minor',
      requirementId: artifact.path,
      before: '아주르가 생성한 원문',
      after: '사용자가 직접 편집한 내용',
      summary:
        added.length > 0
          ? `사용자 편집으로 ${added.join(', ')}이(가) 추가됐습니다.`
          : '사용자가 문서를 직접 편집했고 요구사항 ID 손실은 없습니다.',
      approved: true,
    })
  }
  return items
}

function buildValidationRun(project: StoredProject, id: string): ValidationRun {
  const findings = buildFindings(project)
  const resolved = (findingId: string) => findings.find((f) => f.id === findingId)?.resolved === true
  const traceBonus = resolved('FND-001') ? 6 : 0
  const testBonus = resolved('FND-002') ? 3 : 0
  const intentBonus = resolved('FND-003') ? 1 : 0

  const areas: ScoreArea[] = [
    {
      id: 'intent',
      label: 'Intent Coverage',
      score: 22 + intentBonus,
      max: 25,
      evidence: intentBonus
        ? '목표, 비목표, 핵심 여정이 모두 사용자 결정으로 고정됐습니다.'
        : 'DEC-007이 추천값으로만 남아 있어 결정 기록이 비어 있습니다.',
    },
    {
      id: 'traceability',
      label: 'Traceability',
      score: 14 + traceBonus,
      max: 20,
      evidence: traceBonus
        ? '모든 FR/NFR이 수용 기준과 연결됐습니다.'
        : 'NFR-001~NFR-004가 수용 기준과 연결되지 않았습니다.',
    },
    {
      id: 'testability',
      label: 'Testability',
      score: 17 + testBonus,
      max: 20,
      evidence: testBonus
        ? '모든 수용 기준에 관찰 가능한 판정 조건이 있습니다.'
        : 'AC-006에 파일 형식과 시간 기준이 없습니다.',
    },
    {
      id: 'executability',
      label: 'Technical Executability',
      score: 14,
      max: 15,
      evidence: '배포 검증 절차가 스모크 테스트 한 줄로만 정의돼 있습니다.',
    },
    {
      id: 'fitness',
      label: 'Target-Agent Fitness',
      score: 10,
      max: 10,
      evidence: '선택한 도구의 네이티브 경로와 읽기 순서가 정확합니다.',
    },
    {
      id: 'uxops',
      label: 'UX and Operations Completeness',
      score: 7,
      max: 10,
      evidence: '권한 없음 상태와 복구 지표가 아직 정의되지 않았습니다.',
    },
  ]

  const score = areas.reduce((sum, area) => sum + area.score, 0)
  const staleArtifact = project.artifacts.find((a) => a.status === 'Stale')
  const regression = buildRegression(project)
  const criticalRegression = regression.some((r) => r.severity === 'Critical' && !r.approved)

  const gates: HardGate[] = GATE_LABELS.map((gate) => {
    if (gate.id === 'HG-01') {
      const open = openCriticalCount(project)
      return {
        ...gate,
        passed: open === 0,
        action: open === 0 ? null : `Critical Decision ${open}건을 먼저 답하세요.`,
      }
    }
    if (gate.id === 'HG-02') {
      return {
        ...gate,
        passed: resolved('FND-001'),
        action: resolved('FND-001') ? null : 'FND-001의 수정 적용으로 NFR 수용 기준을 추가하세요.',
      }
    }
    if (gate.id === 'HG-05') {
      return {
        ...gate,
        passed: !staleArtifact,
        action: staleArtifact ? `${staleArtifact.path}가 이전 버전을 참조합니다. 재검증하세요.` : null,
      }
    }
    if (gate.id === 'HG-06') {
      return {
        ...gate,
        passed: !criticalRegression,
        action: criticalRegression ? '회귀 탭에서 삭제된 요구사항을 확인하고 승인 또는 복구하세요.' : null,
      }
    }
    if (gate.id === 'HG-07') {
      return {
        ...gate,
        passed: resolved('FND-002'),
        action: resolved('FND-002') ? null : 'FND-002의 수정 적용으로 AC-006에 판정 기준을 추가하세요.',
      }
    }
    return { ...gate, passed: true, action: null }
  })

  const failedGate = gates.find((g) => !g.passed)
  const previous = project.runs.at(-1)
  const ready = score >= 90 && !failedGate

  return {
    id,
    specVersionId: project.specVersionId,
    specVersionNumber: project.specVersionNumber,
    baseVersionNumber: previous ? project.specVersionNumber : null,
    score,
    previousScore: previous?.score ?? null,
    areas,
    hardGates: gates,
    findings,
    regression,
    ready,
    blockedReason: ready
      ? null
      : failedGate
        ? `Hard Gate ${failedGate.id} 미통과: ${failedGate.label}`
        : `One-Shot Readiness Score가 ${score}점이라 90점 기준에 미달합니다.`,
    completedAt: new Date().toISOString(),
  }
}

// -------------------------------------------------------------------- 매핑

function toArtifact(stored: StoredArtifact): Artifact {
  return {
    id: stored.id,
    kind: stored.kind,
    targetId: stored.targetId,
    path: stored.path,
    status: stored.status,
    specVersionNumber: stored.specVersionNumber,
    contentHash: stored.contentHash,
    updatedAt: stored.updatedAt,
    staleReason: stored.staleReason,
  }
}

function toSummary(project: StoredProject): ProjectSummary {
  return {
    id: project.id,
    name: project.name,
    status: project.status,
    readinessScore: project.runs.at(-1)?.score ?? null,
    specVersionId: project.specVersionId,
    specVersionNumber: project.specVersionNumber,
    targetIds: project.targetIds,
    artifactCount: project.artifacts.length,
    openCriticalDecisions: openCriticalCount(project),
    updatedAt: project.updatedAt,
  }
}

function toProject(project: StoredProject): Project {
  return {
    ...toSummary(project),
    idea: project.idea,
    createdAt: project.createdAt,
    latestRunId: project.runs.at(-1)?.id ?? null,
    latestJobId: project.jobs.at(-1)?.jobId ?? null,
    baseVersionNumber: project.runs.length > 1 ? project.specVersionNumber : null,
  }
}

// ------------------------------------------------------------------- 라우팅

function startJob(project: StoredProject, kind: StoredJob['kind']): { jobId: string } {
  const job: StoredJob = {
    jobId: uid('job'),
    projectId: project.id,
    specVersionId: project.specVersionId,
    kind,
    startedAtMs: Date.now(),
    correlationId: uid('cid'),
  }
  project.jobs.push(job)
  project.status = kind === 'generate' ? 'Generating' : 'Validating'
  project.updatedAt = new Date().toISOString()
  save()
  return { jobId: job.jobId }
}

export async function handleMock(method: string, path: string, body: unknown): Promise<unknown> {
  state = load()
  const url = path.split('?')[0] ?? path
  const segments = url.replace(/^\/api\//, '').split('/')
  await delay(method === 'GET' ? 140 : 320)

  if (segments[0] === 'projects' && segments.length === 1) {
    if (method === 'GET') return state.projects.map(toSummary)
    if (method === 'POST') {
      const input = body as { name: string; idea: IdeaInput; targetIds: string[] }
      const now = new Date().toISOString()
      const project: StoredProject = {
        id: uid('prj'),
        name: input.name,
        idea: input.idea,
        targetIds: input.targetIds,
        status: 'Draft',
        specVersionId: uid('ver'),
        specVersionNumber: 1,
        createdAt: now,
        updatedAt: now,
        decisions: [],
        artifacts: [],
        jobs: [],
        runs: [],
      }
      state.projects.push(project)
      save()
      return toProject(project)
    }
  }

  if (segments[0] === 'projects' && segments.length === 2 && method === 'GET') {
    const project = projectOf(segments[1] ?? '')
    for (const job of project.jobs) settleJob(project, job)
    return toProject(project)
  }

  if (segments[0] === 'projects' && segments[2] === 'analyze' && method === 'POST') {
    const project = projectOf(segments[1] ?? '')
    if (project.decisions.length === 0) project.decisions = buildDecisions()
    project.status = 'NeedsDecision'
    project.updatedAt = new Date().toISOString()
    save()
    return { decisionCount: project.decisions.length }
  }

  if (segments[0] === 'projects' && segments[2] === 'decisions') {
    const project = projectOf(segments[1] ?? '')
    if (method === 'GET') {
      if (project.decisions.length === 0) project.decisions = buildDecisions()
      save()
      return project.decisions
    }
    if (method === 'PUT') {
      const decisionId = segments[3] ?? ''
      const decision = project.decisions.find((d) => d.id === decisionId)
      if (!decision) throw problem('decision_not_found', '요청한 결정을 찾을 수 없습니다.', false)
      const input = body as { optionId: string | null; text: string | null }
      decision.answerOptionId = input.optionId
      decision.answerText = input.text
      decision.answeredAt = new Date().toISOString()
      project.updatedAt = decision.answeredAt
      save()
      return decision
    }
  }

  if (segments[0] === 'spec-versions' && segments[2] === 'generate' && method === 'POST') {
    const project = projectByVersion(segments[1] ?? '')
    if (openCriticalCount(project) > 0) {
      throw problem(
        'critical_decision_open',
        `Critical Decision ${openCriticalCount(project)}건이 남아 있어 명세를 생성할 수 없습니다.`,
        false,
      )
    }
    return startJob(project, 'generate')
  }

  if (segments[0] === 'spec-versions' && segments[2] === 'validate' && method === 'POST') {
    const project = projectByVersion(segments[1] ?? '')
    return startJob(project, 'validate')
  }

  if (segments[0] === 'spec-versions' && segments[2] === 'artifacts' && method === 'GET') {
    const project = projectByVersion(segments[1] ?? '')
    for (const job of project.jobs) settleJob(project, job)
    return project.artifacts.map(toArtifact)
  }

  if (segments[0] === 'jobs' && segments.length === 2 && method === 'GET') {
    const project = projectByJob(segments[1] ?? '')
    const job = project.jobs.find((j) => j.jobId === segments[1])
    if (!job) throw problem('job_not_found', '요청한 Job을 찾을 수 없습니다.', false)
    settleJob(project, job)
    return jobStatusOf(job, project)
  }

  if (segments[0] === 'artifacts' && segments.length === 2) {
    const artifactId = segments[1] ?? ''
    const project = state.projects.find((p) => p.artifacts.some((a) => a.id === artifactId))
    if (!project) throw problem('artifact_not_found', '요청한 문서를 찾을 수 없습니다.', false)
    const artifact = project.artifacts.find((a) => a.id === artifactId)
    if (!artifact) throw problem('artifact_not_found', '요청한 문서를 찾을 수 없습니다.', false)

    if (method === 'GET') return { ...toArtifact(artifact), content: artifact.content } satisfies ArtifactContent

    if (method === 'PUT') {
      const input = body as { content: string }
      artifact.content = input.content
      artifact.contentHash = hash(input.content)
      artifact.updatedAt = new Date().toISOString()
      artifact.status = 'Valid'
      artifact.staleReason = null

      const affected = project.artifacts.filter((a) => a.id !== artifact.id && a.kind === 'AgentInstruction')
      for (const other of affected) {
        other.status = 'Stale'
        other.staleReason = `${artifact.path} 편집 이후 다시 렌더링되지 않았습니다.`
      }
      project.status = 'NeedsDecision'
      project.updatedAt = artifact.updatedAt
      save()
      return {
        artifact: toArtifact(artifact),
        affectedPaths: affected.map((a) => a.path),
        projectStatus: project.status,
      } satisfies ArtifactSaveResult
    }
  }

  if (segments[0] === 'validation-runs' && segments.length === 2 && method === 'GET') {
    const runId = segments[1] ?? ''
    const project = state.projects.find((p) => p.runs.some((r) => r.id === runId))
    if (!project) throw problem('run_not_found', '요청한 검증 결과를 찾을 수 없습니다.', false)
    const stored = project.runs.find((r) => r.id === runId)
    if (!stored) throw problem('run_not_found', '요청한 검증 결과를 찾을 수 없습니다.', false)
    // 편집 이후 상태를 반영해 다시 계산한다.
    const fresh = buildValidationRun(project, stored.id)
    Object.assign(stored, fresh)
    project.status = fresh.ready ? 'Ready' : 'NeedsDecision'
    save()
    return stored
  }

  throw problem('not_found', `로컬 목이 처리하지 않는 요청입니다: ${method} ${path}`, false)
}

export async function exportMockBundle(versionId: string, includeReport: boolean): Promise<Blob> {
  state = load()
  const project = projectByVersion(versionId)
  const run = project.runs.at(-1)
  if (!run?.ready) {
    throw problem('not_ready', 'Ready 상태에서만 구현 번들을 내보낼 수 있습니다.', false)
  }
  await delay(500)
  const entries = project.artifacts.map((a) => ({ path: a.path, content: a.content }))
  if (includeReport) {
    entries.push({
      path: 'VALIDATION-REPORT.md',
      content: renderValidationReport(
        toProject(project),
        run.score,
        run.hardGates.filter((g) => g.passed).length,
        run.hardGates.length,
        new Date().toISOString(),
      ),
    })
  }
  return createZip(entries)
}

/** SSE와 같은 순서·같은 sequence로 이벤트를 흘려보낸다. Last-Event-ID 이후만 재생한다. */
export function streamMockEvents(
  jobId: string,
  lastEventId: number,
  onEvent: (event: JobEvent) => void,
  signal: AbortSignal,
): Promise<void> {
  state = load()
  const project = projectByJob(jobId)
  const job = project.jobs.find((j) => j.jobId === jobId)
  if (!job) throw problem('job_not_found', '요청한 Job을 찾을 수 없습니다.', false)

  const entries = timeline(job).filter((e) => e.event.sequence > lastEventId)
  const timers: number[] = []

  return new Promise<void>((resolve) => {
    const finish = () => {
      for (const timer of timers) window.clearTimeout(timer)
      resolve()
    }
    signal.addEventListener('abort', finish, { once: true })

    for (const entry of entries) {
      const remaining = job.startedAtMs + entry.offsetMs - Date.now()
      const emit = () => {
        if (signal.aborted) return
        if (entry.event.eventType === 'terminal') {
          settleJob(project, job)
        }
        onEvent(entry.event)
        if (entry.event.eventType === 'terminal') finish()
      }
      if (remaining <= 0) emit()
      else timers.push(window.setTimeout(emit, remaining))
    }
    if (entries.length === 0) finish()
  })
}
