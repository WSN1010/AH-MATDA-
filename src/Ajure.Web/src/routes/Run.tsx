import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { AppShell } from '../components/AppShell'
import { ErrorState, Skeleton, StageRail, StatusBadge, formatDateTime, formatDuration } from '../components/ui'
import { getJob, getProject, startGeneration, startValidation, streamJobEvents } from '../api/client'
import { asApiError, useAsync } from '../lib/useAsync'
import type { ApiError } from '../api/error'
import type { JobEvent, JobStatus } from '../api/types'

const STAGE_STATE_LABEL = {
  Pending: '대기',
  Running: '진행',
  Done: '완료',
  Failed: '실패',
} as const

function lastEventKey(jobId: string): string {
  return `ajure.lastEventId.${jobId}`
}

function applyEvent(job: JobStatus, event: JobEvent): JobStatus {
  const stages = job.stages.map((stage) => {
    if (stage.id !== event.stageId || event.eventType === 'terminal') return stage
    if (event.status === 'Running') return { ...stage, status: 'Running' as const, startedAt: event.occurredAt }
    if (event.status === 'Failed') return { ...stage, status: 'Failed' as const, durationMs: event.durationMs }
    return {
      ...stage,
      status: 'Done' as const,
      durationMs: event.durationMs,
      findingCount: event.findingCount,
    }
  })

  if (event.eventType === 'terminal') {
    return {
      ...job,
      stages,
      status: event.status === 'Failed' ? 'Failed' : 'Succeeded',
      lastSequence: event.sequence,
      failure:
        event.status === 'Failed'
          ? { stageId: event.stageId, code: 'stage_failed', message: event.summary, retryable: event.retryable }
          : null,
    }
  }

  return {
    ...job,
    stages,
    status: 'Running',
    lastSequence: event.sequence,
    failure:
      event.status === 'Failed'
        ? { stageId: event.stageId, code: 'stage_failed', message: event.summary, retryable: event.retryable }
        : job.failure,
  }
}

export function Run() {
  const { id = '', jobId = '' } = useParams()
  const navigate = useNavigate()
  const project = useAsync(() => getProject(id), [id])
  const reloadProject = project.reload

  const [job, setJob] = useState<JobStatus | null>(null)
  const [latest, setLatest] = useState<string>('진행 상태를 불러오는 중입니다.')
  const [connection, setConnection] = useState<'connecting' | 'open' | 'closed' | 'lost'>('connecting')
  const [error, setError] = useState<ApiError | null>(null)
  const [resumedFrom, setResumedFrom] = useState(0)
  const [attempt, setAttempt] = useState(0)
  const [retrying, setRetrying] = useState(false)

  const handleEvent = useCallback(
    (event: JobEvent) => {
      sessionStorage.setItem(lastEventKey(jobId), String(event.sequence))
      setLatest(event.summary)
      setJob((current) => (current ? applyEvent(current, event) : current))
    },
    [jobId],
  )

  useEffect(() => {
    const controller = new AbortController()
    let cancelled = false

    async function run() {
      setError(null)
      setConnection('connecting')
      try {
        const snapshot = await getJob(jobId)
        if (cancelled) return
        setJob(snapshot)
        const stored = Number(sessionStorage.getItem(lastEventKey(jobId)) ?? '0')
        const from = Number.isFinite(stored) && stored > 0 ? stored : 0
        setResumedFrom(from)
        setConnection('open')
        await streamJobEvents(jobId, from, handleEvent, controller.signal)
        if (!cancelled && !controller.signal.aborted) {
          setConnection('closed')
          reloadProject()
        }
      } catch (caught) {
        if (cancelled) return
        setError(asApiError(caught))
        setConnection('lost')
      }
    }

    void run()
    return () => {
      cancelled = true
      controller.abort()
    }
  }, [jobId, attempt, handleEvent, reloadProject])

  const done = job?.stages.filter((stage) => stage.status === 'Done').length ?? 0
  const total = job?.stages.length ?? 0
  const findings = useMemo(
    () => (job?.stages ?? []).reduce((sum, stage) => sum + (stage.findingCount ?? 0), 0),
    [job],
  )

  async function retry() {
    if (!job?.specVersionId) return
    setRetrying(true)
    setError(null)
    try {
      const isGeneration = job.stages.some((stage) => stage.id === 'intake')
      const next = isGeneration
        ? await startGeneration(job.specVersionId)
        : await startValidation(job.specVersionId)
      sessionStorage.removeItem(lastEventKey(jobId))
      navigate(`/projects/${id}/run/${next.jobId}`, { replace: true })
    } catch (caught) {
      setError(asApiError(caught))
      setRetrying(false)
    }
  }

  const context = project.data && (
    <>
      <span className="topbar__project">{project.data.name}</span>
      <span className="mono topbar__version">v{project.data.specVersionNumber}</span>
      <StatusBadge status={project.data.status} />
    </>
  )

  return (
    <AppShell title="명세 생성 진행" context={context}>
      <div className="page page--wide">
        <StageRail
          current="validation"
          links={{ decisions: `/projects/${id}/decisions`, validation: `/projects/${id}/run/${jobId}` }}
        />

        <header className="page__head page__head--stack">
          <div>
            <p className="eyebrow mono">02 명세 · 03 검증</p>
            <h1 className="page__title">생성과 검증을 단계별로 보여 줍니다.</h1>
            <p className="page__lead">
              내부 추론은 공개하지 않습니다. 각 단계의 시작 시각, 소요 시간, 발견 수만 그대로 표시합니다.
            </p>
          </div>
        </header>

        {!job && !error && <Skeleton rows={5} label="Job 상태를 불러오는 중" />}

        {error && (
          <ErrorState
            error={error}
            onRetry={() => setAttempt((n) => n + 1)}
            preserved="진행 상태는 서버에 저장돼 있어 다시 연결하면 마지막 단계부터 이어집니다."
          />
        )}

        {job && (
          <div className="run">
            <section className="run__rail" aria-labelledby="rail-title">
              <div className="run__railHead">
                <h2 id="rail-title">Validation Rail</h2>
                <p className="mono run__counter">
                  {done}/{total} 단계 · 발견 {findings}건
                </p>
              </div>

              <p className="run__live" aria-live="polite">
                {job.status === 'Succeeded'
                  ? '모든 단계를 마쳤습니다.'
                  : job.status === 'Failed'
                    ? `실패: ${job.failure?.message ?? '단계를 마치지 못했습니다.'}`
                    : latest}
              </p>

              <ol className="stages">
                {job.stages.map((stage) => (
                  <li key={stage.id} className={`stage stage--${stage.status.toLowerCase()}`}>
                    <span className="stage__marker" aria-hidden="true">
                      {stage.status === 'Done' ? '✓' : stage.status === 'Failed' ? '×' : stage.status === 'Running' ? '◐' : '○'}
                    </span>
                    <div className="stage__body">
                      <p className="stage__title">
                        <span className="stage__state mono">[{STAGE_STATE_LABEL[stage.status]}]</span> {stage.label}
                      </p>
                      <p className="stage__detail">{stage.detail}</p>
                      <p className="stage__meta mono">
                        {stage.startedAt ? `시작 ${formatDateTime(stage.startedAt)}` : '시작 전'}
                        {stage.durationMs !== null && ` · ${formatDuration(stage.durationMs)}`}
                        {stage.findingCount !== null && ` · 발견 ${stage.findingCount}건`}
                      </p>
                    </div>
                  </li>
                ))}
              </ol>
            </section>

            <aside className="run__side">
              <div className="panel">
                <h2 className="panel__title">연결 상태</h2>
                <p className="panel__row">
                  <span className={`badge badge--sm badge--${connection === 'lost' ? 'fail' : connection === 'closed' ? 'neutral' : 'pass'}`}>
                    <span className="badge__glyph" aria-hidden="true">
                      {connection === 'lost' ? '×' : connection === 'closed' ? '·' : '◐'}
                    </span>
                    {connection === 'connecting'
                      ? '연결 중'
                      : connection === 'open'
                        ? '이벤트 수신 중'
                        : connection === 'closed'
                          ? '스트림 종료'
                          : '연결 끊김'}
                  </span>
                </p>
                <p className="panel__note mono">Last-Event-ID {job.lastSequence}</p>
                <p className="panel__note mono">Correlation ID {job.correlationId}</p>
                {resumedFrom > 0 && (
                  <p className="panel__note">
                    저장된 <span className="mono">Last-Event-ID {resumedFrom}</span> 이후부터 이어봤습니다. 새로고침해도
                    같은 지점에서 다시 이어집니다.
                  </p>
                )}
                {connection === 'lost' && (
                  <button type="button" className="btn btn--secondary" onClick={() => setAttempt((n) => n + 1)}>
                    이어서 보기
                  </button>
                )}
              </div>

              {job.status === 'Failed' && (
                <div className="panel panel--fail">
                  <h2 className="panel__title">실패한 단계</h2>
                  <p className="panel__row">{job.failure?.message}</p>
                  <p className="panel__note">
                    {job.failure?.retryable
                      ? '재시도 가능한 실패입니다. 이미 끝난 단계는 다시 계산하지 않습니다.'
                      : '재시도로 해결되지 않습니다. 결정 답변을 확인하세요.'}
                  </p>
                  {job.failure?.retryable && job.specVersionId && (
                    <button type="button" className="btn btn--primary" onClick={retry} disabled={retrying}>
                      {retrying ? '재시작 중…' : '실패 단계부터 재시도'}
                    </button>
                  )}
                </div>
              )}

              {job.status === 'Succeeded' && (
                <div className="panel panel--pass">
                  <h2 className="panel__title">{job.specVersionId ? '생성 완료' : '분석 완료'}</h2>
                  <p className="panel__row">
                    {job.specVersionId
                      ? '문서와 대상 지침 파일이 준비됐습니다. 점수와 Hard Gate는 워크벤치의 Quality Inspector에서 확인합니다.'
                      : '구현 결과를 바꾸는 결정을 확인할 준비가 됐습니다.'}
                  </p>
                  <Link
                    className="btn btn--primary"
                    to={job.specVersionId ? `/projects/${id}/workspace` : `/projects/${id}/decisions`}
                  >
                    {job.specVersionId ? '문서 워크벤치 열기' : '결정 확인하기'}
                  </Link>
                </div>
              )}

              <div className="panel">
                <h2 className="panel__title">이 화면을 떠나도 됩니다</h2>
                <p className="panel__note">
                  진행 상태는 서버에 저장됩니다. 새로고침하거나 나중에 다시 열어도 마지막 이벤트부터 이어서 봅니다.
                </p>
                <Link className="btn btn--ghost" to="/projects">
                  프로젝트 목록으로
                </Link>
              </div>
            </aside>
          </div>
        )}
      </div>
    </AppShell>
  )
}
