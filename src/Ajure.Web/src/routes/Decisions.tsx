import { useEffect, useMemo, useRef, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { AppShell } from '../components/AppShell'
import { ErrorState, Skeleton, StageRail, StatusBadge } from '../components/ui'
import { getDecisions, getProject, saveDecision, startGeneration } from '../api/client'
import { asApiError, useAsync } from '../lib/useAsync'
import type { ApiError } from '../api/error'
import type { Decision, DecisionKind } from '../api/types'

const KIND_LABEL: Record<DecisionKind, string> = {
  Critical: 'Critical · 반드시 답해야 함',
  Important: 'Important · 건너뛰면 추천값 적용',
  Defaultable: 'Defaultable · 추천값으로 통과 가능',
}

const KIND_SHORT: Record<DecisionKind, string> = {
  Critical: 'Critical',
  Important: 'Important',
  Defaultable: 'Defaultable',
}

const KIND_TONE: Record<DecisionKind, string> = {
  Critical: 'fail',
  Important: 'warn',
  Defaultable: 'neutral',
}

export function Decisions() {
  const { id = '' } = useParams()
  const navigate = useNavigate()
  const project = useAsync(() => getProject(id), [id])
  const loaded = useAsync(() => getDecisions(id), [id])

  const [decisions, setDecisions] = useState<Decision[] | null>(null)
  const [currentId, setCurrentId] = useState<string | null>(null)
  const [selected, setSelected] = useState<string | null>(null)
  const [customText, setCustomText] = useState('')
  const [saving, setSaving] = useState(false)
  const [saveError, setSaveError] = useState<ApiError | null>(null)
  const [generating, setGenerating] = useState(false)
  const slipHeading = useRef<HTMLHeadingElement>(null)

  useEffect(() => {
    if (loaded.data) setDecisions(loaded.data)
  }, [loaded.data])

  const current = useMemo(() => {
    if (!decisions || decisions.length === 0) return null
    return (
      decisions.find((d) => d.id === currentId) ??
      decisions.find((d) => d.answeredAt === null) ??
      decisions[0] ??
      null
    )
  }, [decisions, currentId])

  useEffect(() => {
    if (!current) return
    setSelected(current.answerOptionId ?? current.recommendedOptionId)
    setCustomText(current.answerText ?? '')
    setSaveError(null)
  }, [current])

  const openCritical = decisions?.filter((d) => d.kind === 'Critical' && d.answeredAt === null).length ?? 0
  const answered = decisions?.filter((d) => d.answeredAt !== null).length ?? 0
  const total = decisions?.length ?? 0

  const conflict = useMemo(() => {
    if (!current || !decisions || !selected) return null
    return (
      current.conflicts.find((rule) => {
        if (rule.optionId !== selected) return false
        const other = decisions.find((d) => d.id === rule.decisionId)
        return other?.answerOptionId === rule.withOptionId
      }) ?? null
    )
  }, [current, decisions, selected])

  async function persist(optionId: string | null, text: string | null) {
    if (!current) return
    setSaving(true)
    setSaveError(null)
    try {
      const updated = await saveDecision(id, current.id, { optionId, text })
      const next = (decisions ?? []).map((d) => (d.id === updated.id ? updated : d))
      setDecisions(next)
      const following = next.find((d) => d.answeredAt === null)
      setCurrentId(following ? following.id : updated.id)
      window.requestAnimationFrame(() => slipHeading.current?.focus())
    } catch (caught) {
      setSaveError(asApiError(caught))
    } finally {
      setSaving(false)
    }
  }

  useEffect(() => {
    function onKeyDown(event: KeyboardEvent) {
      const target = event.target
      if (target instanceof HTMLTextAreaElement || target instanceof HTMLInputElement) return
      if (!current) return
      const index = Number(event.key)
      if (Number.isInteger(index) && index >= 1 && index <= current.options.length) {
        setSelected(current.options[index - 1]?.id ?? null)
        event.preventDefault()
        return
      }
      if (event.key === 'Enter' && selected && !saving) {
        void persist(selected, customText.trim() || null)
        event.preventDefault()
      }
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  })

  async function generate() {
    if (!project.data) return
    setGenerating(true)
    setSaveError(null)
    try {
      const job = await startGeneration(project.data.specVersionId)
      navigate(`/projects/${id}/run/${job.jobId}`)
    } catch (caught) {
      setSaveError(asApiError(caught))
      setGenerating(false)
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
    <AppShell title="결정 인터뷰" context={context}>
      <div className="page page--wide">
        <StageRail current="decisions" links={{ decisions: `/projects/${id}/decisions` }} />

        <header className="page__head page__head--stack">
          <div>
            <p className="eyebrow mono">01 결정</p>
            <h1 className="page__title">구현 결과를 바꾸는 결정만 묻습니다.</h1>
            <p className="page__lead">
              여기서 답한 내용은 문서의 근거로 남고, 대상 지침 파일에서 잠긴 결정으로 전달됩니다.
            </p>
          </div>
        </header>

        {(loaded.status === 'loading' || project.status === 'loading') && (
          <Skeleton rows={5} label="결정 질문을 준비하는 중" />
        )}

        {loaded.status === 'error' && loaded.error && <ErrorState error={loaded.error} onRetry={loaded.reload} />}

        {decisions && decisions.length === 0 && loaded.status === 'ready' && (
          <div className="state state--empty">
            <p className="state__title">확인이 필요한 결정이 없습니다.</p>
            <p className="state__body">입력만으로 구현 결과가 고정됩니다. 바로 명세를 생성할 수 있습니다.</p>
          </div>
        )}

        {decisions && decisions.length > 0 && current && (
          <div className="decisions">
            <aside className="decisions__list" aria-label="결정 목록">
              <div className="progress">
                <p className="progress__text">
                  <span className="mono">
                    {answered}/{total}
                  </span>{' '}
                  결정 완료
                </p>
                <div
                  className="meter meter--pass"
                  role="meter"
                  aria-valuenow={answered}
                  aria-valuemin={0}
                  aria-valuemax={total}
                  aria-label={`전체 ${total}개 중 ${answered}개 결정 완료`}
                >
                  <span style={{ inlineSize: `${total === 0 ? 0 : (answered / total) * 100}%` }} />
                </div>
              </div>

              <ol className="dlist">
                {decisions.map((decision) => (
                  <li key={decision.id}>
                    <button
                      type="button"
                      className={`dlist__item ${decision.id === current.id ? 'dlist__item--on' : ''}`}
                      onClick={() => setCurrentId(decision.id)}
                      aria-current={decision.id === current.id ? 'true' : undefined}
                    >
                      <span className={`badge badge--sm badge--${KIND_TONE[decision.kind]}`}>
                        <span className="badge__glyph" aria-hidden="true">
                          {decision.kind === 'Critical' ? '!' : decision.kind === 'Important' ? '△' : '·'}
                        </span>
                        {KIND_SHORT[decision.kind]}
                      </span>
                      <span className="dlist__q">{decision.question}</span>
                      <span className={`dlist__state ${decision.answeredAt ? 'dlist__state--done' : ''}`}>
                        {decision.answeredAt ? '답변함' : '미결정'}
                      </span>
                    </button>
                  </li>
                ))}
              </ol>
            </aside>

            <section className="slip" aria-labelledby="slip-title">
              <p className={`slip__kind slip__kind--${KIND_TONE[current.kind]}`}>{KIND_LABEL[current.kind]}</p>
              <h2 className="slip__title" id="slip-title" tabIndex={-1} ref={slipHeading}>
                {current.question}
              </h2>

              <div className="slip__why">
                <h3>왜 필요한가</h3>
                <p>{current.why}</p>
                <h3>선택에 따른 구현 영향</h3>
                <p>{current.impact}</p>
              </div>

              <fieldset className="slip__options">
                <legend>선택지 — 숫자 키로 선택하고 Enter로 저장합니다.</legend>
                {current.options.map((option, index) => (
                  <label key={option.id} className={`option ${selected === option.id ? 'option--on' : ''}`}>
                    <input
                      type="radio"
                      name={`decision-${current.id}`}
                      value={option.id}
                      checked={selected === option.id}
                      onChange={() => setSelected(option.id)}
                    />
                    <span className="option__body">
                      <span className="option__head">
                        <span className="option__key mono" aria-hidden="true">
                          {index + 1}
                        </span>
                        <span className="option__label">{option.label}</span>
                        {option.id === current.recommendedOptionId && <span className="option__rec">추천</span>}
                      </span>
                      <span className="option__detail">{option.detail}</span>
                    </span>
                  </label>
                ))}
              </fieldset>

              <p className="slip__rationale">
                <strong>추천 근거</strong> {current.recommendationRationale}
              </p>

              {conflict && (
                <p className="note note--warn" role="alert">
                  <span aria-hidden="true">! </span>
                  {conflict.message}
                </p>
              )}

              <div className="field">
                <label htmlFor="custom">직접 입력 (선택)</label>
                <textarea
                  id="custom"
                  className="input input--area"
                  rows={2}
                  value={customText}
                  onChange={(event) => setCustomText(event.target.value)}
                  aria-describedby="custom-help"
                />
                <p className="field__help" id="custom-help">
                  선택지에 없는 조건이면 여기에 적습니다. 적은 내용이 선택지보다 우선합니다.
                </p>
              </div>

              {saveError && <ErrorState error={saveError} onRetry={() => persist(selected, customText || null)} />}

              <div className="slip__actions">
                {current.kind === 'Defaultable' && (
                  <button
                    type="button"
                    className="btn btn--ghost"
                    disabled={saving}
                    onClick={() => persist(current.recommendedOptionId, null)}
                  >
                    추천값 사용
                  </button>
                )}
                <button
                  type="button"
                  className="btn btn--primary"
                  disabled={saving || !selected}
                  onClick={() => persist(selected, customText.trim() || null)}
                >
                  {saving ? '저장 중…' : '결정 저장'}
                </button>
              </div>

              <div className="slip__finish">
                {openCritical > 0 ? (
                  <p className="hint">
                    Critical 결정 {openCritical}건을 해결해야 명세를 생성할 수 있습니다.
                  </p>
                ) : (
                  <p className="hint">Critical 결정이 모두 해결됐습니다. 남은 질문은 추천값으로 채워집니다.</p>
                )}
                <button
                  type="button"
                  className="btn btn--primary"
                  disabled={openCritical > 0 || generating || !project.data}
                  onClick={generate}
                >
                  {generating ? '생성 Job 시작 중…' : '명세 생성'}
                </button>
              </div>
            </section>
          </div>
        )}

        <p className="backlink">
          <Link to="/projects">프로젝트 목록으로</Link>
        </p>
      </div>
    </AppShell>
  )
}
