import type {
  Artifact,
  ArtifactContent,
  ArtifactSaveResult,
  Decision,
  IdeaInput,
  JobEvent,
  JobStatus,
  Project,
  ProjectSummary,
  ValidationRun,
} from './types'
import { ApiError } from './error'
import { applyFindingFix, exportMockBundle, handleMock, streamMockEvents } from './mock'

/**
 * 아주르의 유일한 API 통로다. 화면은 fetch를 직접 쓰지 않는다.
 * 경로는 TRD.md §9 표에 있는 것만 사용한다.
 *
 * 백엔드나 Fake 모드가 없으면 첫 요청에서 이를 감지해 인-프로세스 목으로 내려간다.
 * `VITE_AJURE_API_MODE=live|mock`으로 강제할 수 있다.
 */

const BASE = import.meta.env.VITE_AJURE_API_BASE ?? ''
const FORCED = import.meta.env.VITE_AJURE_API_MODE

type Mode = 'unknown' | 'live' | 'mock'
let mode: Mode = FORCED === 'live' ? 'live' : FORCED === 'mock' ? 'mock' : 'unknown'

const listeners = new Set<(usingMock: boolean) => void>()

export function isMockMode(): boolean {
  return mode === 'mock'
}

/** 목 모드로 전환됐을 때 화면이 안내를 표시할 수 있게 알린다. */
export function onModeChange(listener: (usingMock: boolean) => void): () => void {
  listeners.add(listener)
  return () => listeners.delete(listener)
}

function fallbackToMock(): void {
  if (mode === 'mock') return
  mode = 'mock'
  for (const listener of listeners) listener(true)
}

function toApiError(payload: unknown, status: number): ApiError {
  if (payload && typeof payload === 'object' && 'code' in payload && 'message' in payload) {
    const problem = payload as ApiError['problem']
    return new ApiError(problem)
  }
  return new ApiError({
    code: `http_${status}`,
    message: '요청을 처리하지 못했습니다.',
    correlationId: '-',
    retryable: status >= 500,
  })
}

async function request<T>(method: string, path: string, body?: unknown): Promise<T> {
  if (isMockMode()) return (await handleMock(method, path, body)) as T

  try {
    const response = await fetch(BASE + path, {
      method,
      headers: body === undefined ? { Accept: 'application/json' } : { Accept: 'application/json', 'Content-Type': 'application/json' },
      body: body === undefined ? undefined : JSON.stringify(body),
    })

    // 동시에 나간 다른 요청이 이미 목으로 내려갔으면 이 응답은 버린다.
    if (mode === 'mock') return (await handleMock(method, path, body)) as T

    const contentType = response.headers.get('content-type') ?? ''
    const isJson = contentType.includes('json')

    if (mode === 'unknown' && (!isJson || response.status === 404)) {
      // 백엔드가 이 경로를 서비스하지 않는다. 로컬 목으로 내려간다.
      fallbackToMock()
      return (await handleMock(method, path, body)) as T
    }

    mode = 'live'
    if (!response.ok) throw toApiError(isJson ? await response.json() : null, response.status)
    if (response.status === 204) return undefined as T
    return (await response.json()) as T
  } catch (error) {
    if (error instanceof ApiError) throw error
    if (mode !== 'live') {
      fallbackToMock()
      return (await handleMock(method, path, body)) as T
    }
    throw new ApiError({
      code: 'network_unreachable',
      message: '서버에 연결하지 못했습니다. 네트워크를 확인하고 다시 시도하세요.',
      correlationId: '-',
      retryable: true,
    })
  }
}

// ------------------------------------------------------------------ 프로젝트

export function listProjects(): Promise<ProjectSummary[]> {
  return request<ProjectSummary[]>('GET', '/api/projects')
}

export function getProject(projectId: string): Promise<Project> {
  return request<Project>('GET', `/api/projects/${projectId}`)
}

export function createProject(input: {
  name: string
  idea: IdeaInput
  targetIds: string[]
}): Promise<Project> {
  return request<Project>('POST', '/api/projects', input)
}

export function analyzeProject(projectId: string): Promise<{ jobId: string }> {
  return request<{ jobId: string }>('POST', `/api/projects/${projectId}/analyze`, {})
}

// -------------------------------------------------------------------- 결정

export function getDecisions(projectId: string): Promise<Decision[]> {
  return request<Decision[]>('GET', `/api/projects/${projectId}/decisions`)
}

export function saveDecision(
  projectId: string,
  decisionId: string,
  answer: { optionId: string | null; text: string | null },
): Promise<Decision> {
  return request<Decision>('PUT', `/api/projects/${projectId}/decisions/${decisionId}`, answer)
}

// ---------------------------------------------------------------------- Job

export function startGeneration(versionId: string): Promise<{ jobId: string }> {
  return request<{ jobId: string }>('POST', `/api/spec-versions/${versionId}/generate`, {})
}

export function startValidation(versionId: string): Promise<{ jobId: string }> {
  return request<{ jobId: string }>('POST', `/api/spec-versions/${versionId}/validate`, {})
}

export function getJob(jobId: string): Promise<JobStatus> {
  return request<JobStatus>('GET', `/api/jobs/${jobId}`)
}

function parseSseChunk(chunk: string): JobEvent | null {
  let data = ''
  for (const line of chunk.split('\n')) {
    if (line.startsWith('data:')) data += line.slice(5).trim()
  }
  if (!data) return null
  try {
    return JSON.parse(data) as JobEvent
  } catch {
    return null
  }
}

/**
 * `GET /api/jobs/{jobId}/events`를 구독한다.
 * 새로고침 후에도 이어보기 위해 저장해 둔 sequence를 `Last-Event-ID` 헤더로 보낸다.
 * (EventSource는 헤더를 지정할 수 없어 fetch 스트림으로 읽는다.)
 */
export async function streamJobEvents(
  jobId: string,
  lastEventId: number,
  onEvent: (event: JobEvent) => void,
  signal: AbortSignal,
): Promise<void> {
  if (isMockMode()) return streamMockEvents(jobId, lastEventId, onEvent, signal)

  let response: Response
  try {
    response = await fetch(`${BASE}/api/jobs/${jobId}/events`, {
      headers: lastEventId > 0
        ? { Accept: 'text/event-stream', 'Last-Event-ID': String(lastEventId) }
        : { Accept: 'text/event-stream' },
      signal,
    })
  } catch (error) {
    if (signal.aborted || mode === 'live') throw error
    fallbackToMock()
    return streamMockEvents(jobId, lastEventId, onEvent, signal)
  }

  // 다른 요청이 이미 목으로 내려갔으면 목 스트림을 쓴다.
  if (mode === 'mock') return streamMockEvents(jobId, lastEventId, onEvent, signal)

  if (mode === 'unknown' && !(response.headers.get('content-type') ?? '').includes('event-stream')) {
    fallbackToMock()
    return streamMockEvents(jobId, lastEventId, onEvent, signal)
  }
  mode = 'live'

  if (!response.ok || !response.body) {
    throw new ApiError({
      code: 'stream_failed',
      message: '진행 상태 연결에 실패했습니다. 다시 연결하면 마지막 단계부터 이어볼 수 있습니다.',
      correlationId: '-',
      retryable: true,
    })
  }

  const reader = response.body.pipeThrough(new TextDecoderStream()).getReader()
  let buffer = ''
  while (true) {
    const { done, value } = await reader.read()
    if (done) break
    buffer += value
    let boundary = buffer.indexOf('\n\n')
    while (boundary !== -1) {
      const event = parseSseChunk(buffer.slice(0, boundary))
      buffer = buffer.slice(boundary + 2)
      if (event) onEvent(event)
      boundary = buffer.indexOf('\n\n')
    }
  }
}

// ------------------------------------------------------------------- 산출물

export function getArtifacts(versionId: string): Promise<Artifact[]> {
  return request<Artifact[]>('GET', `/api/spec-versions/${versionId}/artifacts`)
}

export function getArtifact(artifactId: string): Promise<ArtifactContent> {
  return request<ArtifactContent>('GET', `/api/artifacts/${artifactId}`)
}

export function saveArtifact(artifactId: string, content: string): Promise<ArtifactSaveResult> {
  return request<ArtifactSaveResult>('PUT', `/api/artifacts/${artifactId}`, { content })
}

// ---------------------------------------------------------------------- 검증

export function getValidationRun(runId: string): Promise<ValidationRun> {
  return request<ValidationRun>('GET', `/api/validation-runs/${runId}`)
}

/** Finding의 제안을 문서 초안에 적용한다. 적용 결과는 사용자가 Diff로 확인한 뒤 저장한다. */
export function suggestFix(findingId: string, content: string): string {
  return applyFindingFix(findingId, content)
}

// -------------------------------------------------------------------- 내보내기

export async function exportBundle(versionId: string, includeValidationReport: boolean): Promise<Blob> {
  if (mode === 'mock') return exportMockBundle(versionId, includeValidationReport)

  const response = await fetch(`${BASE}/api/spec-versions/${versionId}/export`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Accept: 'application/zip' },
    body: JSON.stringify({ includeValidationReport }),
  })
  if (!response.ok) {
    const contentType = response.headers.get('content-type') ?? ''
    throw toApiError(contentType.includes('json') ? await response.json() : null, response.status)
  }
  return response.blob()
}
