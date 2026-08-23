// 아주르 API 계약 타입. 유일한 기준은 TRD.md §9 표다.
// 이 파일 밖에서 새 엔드포인트나 새 응답 모양을 추측하지 않는다.

export type SpecStatus =
  | 'Draft'
  | 'Analyzing'
  | 'Generating'
  | 'Validating'
  | 'NeedsDecision'
  | 'Ready'
  | 'Superseded'

export type DecisionKind = 'Critical' | 'Important' | 'Defaultable'
export type Severity = 'Critical' | 'Major' | 'Minor'
export type ArtifactStatus = 'Valid' | 'Stale' | 'Error'
export type ArtifactKind = 'Ideation' | 'Prd' | 'Trd' | 'AgentInstruction'
export type JobState = 'Queued' | 'Running' | 'Succeeded' | 'Failed'
export type StageState = 'Pending' | 'Running' | 'Done' | 'Failed'
export type SupportLevel = 'Stable' | 'Basic' | 'Generic'
export type ModelProviderSource = 'local' | 'environment' | null

/** 사용자가 입력한 아이디어 원문. FR-001 */
export interface IdeaInput {
  /** 무엇을, 누구를 위해 만들고 싶은가 */
  summary: string
  /** 이미 정해진 기술·배포 조건 */
  constraints: string
  /** 반드시 제외할 범위 */
  exclusions: string
  /** 기존 문서 붙여 넣기(선택) */
  existingDocs: string
}

export interface ProjectSummary {
  id: string
  name: string
  status: SpecStatus
  /** One-Shot Readiness Score. 아직 검증 전이면 null */
  readinessScore: number | null
  specVersionId: string
  specVersionNumber: number
  targetIds: string[]
  artifactCount: number
  openCriticalDecisions: number
  updatedAt: string
}

export interface Project extends ProjectSummary {
  idea: IdeaInput
  createdAt: string
  /** 최신 검증 실행 ID. 검증 전이면 null */
  latestRunId: string | null
  /** 진행 중이거나 마지막으로 실행한 Job */
  latestJobId: string | null
  baseVersionNumber: number | null
}

export interface DecisionOption {
  id: string
  label: string
  detail: string
}

/** FR-003 결정 중심 인터뷰 */
export interface Decision {
  id: string
  kind: DecisionKind
  question: string
  /** 왜 이 결정이 필요한가 */
  why: string
  /** 선택에 따른 구현 영향 */
  impact: string
  options: DecisionOption[]
  recommendedOptionId: string
  recommendationRationale: string
  answerOptionId: string | null
  answerText: string | null
  answeredAt: string | null
  /** 이 결정과 충돌할 수 있는 다른 결정의 선택지 */
  conflicts: { optionId: string; decisionId: string; withOptionId: string; message: string }[]
}

export interface JobStage {
  id: string
  label: string
  /** 지금 무엇을 하고 있는지 실제 작업으로 서술 */
  detail: string
  status: StageState
  startedAt: string | null
  durationMs: number | null
  findingCount: number | null
}

export interface JobFailure {
  stageId: string
  code: string
  message: string
  retryable: boolean
}

export interface JobStatus {
  jobId: string
  projectId: string
  specVersionId: string | null
  status: JobState
  stages: JobStage[]
  lastSequence: number
  correlationId: string
  failure: JobFailure | null
  /** 성공 시 결과를 확인할 검증 실행 ID */
  validationRunId: string | null
}

/** SSE `/api/jobs/{jobId}/events` 이벤트 payload */
export interface JobEvent {
  sequence: number
  eventType: 'stage' | 'terminal'
  stageId: string
  status: StageState | JobState
  summary: string
  occurredAt: string
  durationMs: number | null
  findingCount: number | null
  retryable: boolean
  correlationId: string
}

export interface Artifact {
  id: string
  kind: ArtifactKind
  /** AgentInstruction일 때만 채워진다 */
  targetId: string | null
  path: string
  status: ArtifactStatus
  specVersionNumber: number
  contentHash: string
  updatedAt: string
  /** Stale 사유를 사용자 문장으로 */
  staleReason: string | null
}

export interface ArtifactContent extends Artifact {
  content: string
}

export interface ArtifactSaveResult {
  artifact: Artifact
  /** 이 편집으로 Stale이 된 다른 산출물 경로 */
  affectedPaths: string[]
  projectStatus: SpecStatus
}

export interface ScoreArea {
  id: string
  label: string
  score: number
  max: number
  /** 감점 근거 한 문장 */
  evidence: string
}

export interface HardGate {
  id: string
  label: string
  passed: boolean
  /** 실패 시 해결 행동 */
  action: string | null
}

export interface Finding {
  id: string
  severity: Severity
  /** 한 문장 문제 */
  title: string
  /** 근거 */
  evidence: string
  relatedIds: string[]
  artifactPath: string
  /** Finding이 가리키는 문서의 줄 번호(1-base) */
  line: number | null
  autoFixable: boolean
  /** 자동 수정으로 적용될 문장 */
  suggestion: string | null
  /** 사용자 결정이 필요하면 해당 Decision ID */
  decisionId: string | null
  resolved: boolean
}

export type RegressionKind =
  | 'Removed'
  | 'Weakened'
  | 'Unlinked'
  | 'Contradicted'
  | 'ScopeLeak'
  | 'StateLoss'
  | 'QualityDrop'
  | 'StaleArtifact'
  | 'FormatRegression'
  | 'ApprovedChange'

export interface RegressionItem {
  id: string
  kind: RegressionKind
  severity: Severity
  requirementId: string
  before: string
  after: string
  /** 의미 변화 요약 */
  summary: string
  approved: boolean
}

export interface ValidationRun {
  id: string
  specVersionId: string
  specVersionNumber: number
  baseVersionNumber: number | null
  score: number
  previousScore: number | null
  areas: ScoreArea[]
  hardGates: HardGate[]
  findings: Finding[]
  regression: RegressionItem[]
  ready: boolean
  /** Ready가 아닌 이유 한 문장 */
  blockedReason: string | null
  completedAt: string
}

/** TRD §9 오류 형식: Problem Details */
export interface ProblemDetails {
  code: string
  message: string
  correlationId: string
  retryable: boolean
  details?: Record<string, string>
}

export interface ModelProviderStatus {
  id: 'openai' | 'anthropic' | 'gemini'
  displayName: string
  configured: boolean
  source: ModelProviderSource
  model: string
  editable: boolean
  errorCode: string | null
}

export interface ModelProviderList {
  requiredCount: number
  configuredCount: number
  providers: ModelProviderStatus[]
}

/** 대상 코딩 에이전트 레지스트리 항목. AI-FILE-SPEC §3 */
export interface TargetDefinition {
  id: string
  name: string
  path: string
  support: SupportLevel
  /** 도구가 파일을 자동으로 읽는 방식 */
  discovery: string
}
