import { useEffect, useRef, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { AppShell } from '../components/AppShell'
import { ErrorState } from '../components/ui'
import { SUPPORT_LABEL, TARGETS, findTarget, mergedPaths } from '../api/targets'
import { analyzeProject, createProject } from '../api/client'
import { asApiError } from '../lib/useAsync'
import type { ApiError } from '../api/error'
import type { IdeaInput } from '../api/types'

const STEPS = [
  { id: 1, label: '아이디어' },
  { id: 2, label: '대상 에이전트' },
  { id: 3, label: '검토' },
]

const EMPTY_IDEA: IdeaInput = { summary: '', constraints: '', exclusions: '', existingDocs: '' }

export function NewProject() {
  const navigate = useNavigate()
  const [step, setStep] = useState(1)
  const [name, setName] = useState('')
  const [idea, setIdea] = useState<IdeaInput>(EMPTY_IDEA)
  const [targetIds, setTargetIds] = useState<string[]>([])
  const [showErrors, setShowErrors] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<ApiError | null>(null)
  const headingRef = useRef<HTMLHeadingElement>(null)

  useEffect(() => {
    headingRef.current?.focus()
  }, [step])

  const nameError = name.trim().length === 0 ? '프로젝트 이름을 입력하세요.' : null
  const summaryError =
    idea.summary.trim().length < 10 ? '무엇을 누구를 위해 만들지 한 문장 이상 적어 주세요.' : null
  const step1Valid = !nameError && !summaryError
  const merged = mergedPaths(targetIds)

  function goToStep(next: number) {
    if (next === 2 && !step1Valid) {
      setShowErrors(true)
      return
    }
    setShowErrors(false)
    setStep(next)
  }

  function toggleTarget(id: string) {
    setTargetIds((current) => (current.includes(id) ? current.filter((x) => x !== id) : [...current, id]))
  }

  async function submit() {
    setSubmitting(true)
    setError(null)
    try {
      const project = await createProject({ name: name.trim(), idea, targetIds })
      const job = await analyzeProject(project.id)
      navigate(`/projects/${project.id}/run/${job.jobId}`)
    } catch (caught) {
      setError(asApiError(caught))
      setSubmitting(false)
    }
  }

  return (
    <AppShell title="새 명세 만들기">
      <div className="page page--narrow">
        <header className="page__head page__head--stack">
          <div>
            <p className="eyebrow mono">New Project</p>
            <h1 className="page__title">새 명세 만들기</h1>
            <p className="page__lead">
              긴 폼 하나 대신 세 단계로 나눕니다. 지금 적은 내용이 결정 질문과 문서의 근거가 됩니다.
            </p>
          </div>
          <ol className="steps" aria-label="생성 단계">
            {STEPS.map((item) => (
              <li
                key={item.id}
                className={`steps__item ${item.id === step ? 'steps__item--on' : item.id < step ? 'steps__item--done' : ''}`}
              >
                <span aria-current={item.id === step ? 'step' : undefined}>
                  <span className="steps__num mono" aria-hidden="true">
                    {String(item.id).padStart(2, '0')}
                  </span>
                  {item.label}
                </span>
              </li>
            ))}
          </ol>
        </header>

        {step === 1 && (
          <section className="card card--form" aria-labelledby="step1">
            <h2 className="card__title" id="step1" tabIndex={-1} ref={step === 1 ? headingRef : undefined}>
              01 아이디어
            </h2>

            <div className="field">
              <label htmlFor="pname">프로젝트 이름</label>
              <input
                id="pname"
                className="input"
                value={name}
                onChange={(event) => setName(event.target.value)}
                aria-invalid={showErrors && nameError ? true : undefined}
                aria-describedby={showErrors && nameError ? 'pname-error' : undefined}
                autoComplete="off"
              />
              {showErrors && nameError && (
                <p className="field__error" id="pname-error">
                  {nameError}
                </p>
              )}
            </div>

            <div className="field">
              <label htmlFor="summary">무엇을, 누구를 위해 만들고 싶나요?</label>
              <textarea
                id="summary"
                className="input input--area"
                rows={5}
                value={idea.summary}
                onChange={(event) => setIdea({ ...idea, summary: event.target.value })}
                aria-invalid={showErrors && summaryError ? true : undefined}
                aria-describedby={`summary-help${showErrors && summaryError ? ' summary-error' : ''}`}
              />
              <p className="field__help" id="summary-help">
                예: “팀 회고에서 나온 실행 항목을 담당자와 기한까지 추적하는 웹앱. 사용자는 5~15명 규모의 개발팀.”
              </p>
              {showErrors && summaryError && (
                <p className="field__error" id="summary-error">
                  {summaryError}
                </p>
              )}
            </div>

            <div className="field">
              <label htmlFor="constraints">이미 정해진 기술·배포 조건</label>
              <textarea
                id="constraints"
                className="input input--area"
                rows={3}
                value={idea.constraints}
                onChange={(event) => setIdea({ ...idea, constraints: event.target.value })}
                aria-describedby="constraints-help"
              />
              <p className="field__help" id="constraints-help">
                비워 두면 아주르가 결정 질문으로 물어봅니다.
              </p>
            </div>

            <div className="field">
              <label htmlFor="exclusions">반드시 제외할 범위</label>
              <textarea
                id="exclusions"
                className="input input--area"
                rows={3}
                value={idea.exclusions}
                onChange={(event) => setIdea({ ...idea, exclusions: event.target.value })}
                aria-describedby="exclusions-help"
              />
              <p className="field__help" id="exclusions-help">
                여기 적은 항목은 문서의 Non-goal로 고정되어 구현 에이전트가 만들지 않습니다.
              </p>
            </div>

            <div className="field">
              <label htmlFor="existing">기존 문서 붙여 넣기 (선택)</label>
              <textarea
                id="existing"
                className="input input--area"
                rows={3}
                value={idea.existingDocs}
                onChange={(event) => setIdea({ ...idea, existingDocs: event.target.value })}
              />
            </div>

            <div className="card__actions">
              <Link className="btn btn--ghost" to="/projects">
                취소
              </Link>
              <button type="button" className="btn btn--primary" onClick={() => goToStep(2)}>
                대상 에이전트 선택
              </button>
            </div>
          </section>
        )}

        {step === 2 && (
          <section className="card card--form" aria-labelledby="step2">
            <h2 className="card__title" id="step2" tabIndex={-1} ref={step === 2 ? headingRef : undefined}>
              02 대상 코딩 에이전트
            </h2>
            <p className="card__lead">
              문서를 만드는 생성 모델과, 문서를 받아 구현할 코딩 에이전트는 다릅니다. 여기서 고르는 것은{' '}
              <strong>구현을 맡길 도구</strong>입니다. 여러 개를 고를 수 있습니다.
            </p>

            <ul className="targets">
              {TARGETS.map((target) => {
                const checked = targetIds.includes(target.id)
                return (
                  <li key={target.id}>
                    <label className={`target ${checked ? 'target--on' : ''}`}>
                      <input
                        type="checkbox"
                        checked={checked}
                        onChange={() => toggleTarget(target.id)}
                        aria-describedby={`target-${target.id}-desc`}
                      />
                      <span className="target__body">
                        <span className="target__head">
                          <span className="target__name">{target.name}</span>
                          <span className={`badge badge--sm badge--${target.support === 'Stable' ? 'pass' : 'neutral'}`}>
                            {SUPPORT_LABEL[target.support]}
                          </span>
                        </span>
                        <code className="mono target__path">{target.path}</code>
                        <span className="target__desc" id={`target-${target.id}-desc`}>
                          {target.discovery}
                        </span>
                      </span>
                    </label>
                  </li>
                )
              })}
            </ul>

            {merged.some((group) => group.targetIds.length > 1) && (
              <p className="note note--info" role="status">
                {merged
                  .filter((group) => group.targetIds.length > 1)
                  .map(
                    (group) =>
                      `${group.targetIds.map((id) => findTarget(id)?.name ?? id).join('과 ')}는 같은 ${group.path}를 읽습니다. 호환 파일 하나로 병합됩니다.`,
                  )
                  .join(' ')}
              </p>
            )}

            <div className="card__actions">
              <button type="button" className="btn btn--ghost" onClick={() => goToStep(1)}>
                이전
              </button>
              <div className="card__actionsEnd">
                {targetIds.length === 0 && <p className="hint">대상 도구를 1개 이상 선택해야 검토로 넘어갑니다.</p>}
                <button
                  type="button"
                  className="btn btn--primary"
                  disabled={targetIds.length === 0}
                  onClick={() => goToStep(3)}
                >
                  검토
                </button>
              </div>
            </div>
          </section>
        )}

        {step === 3 && (
          <section className="card card--form" aria-labelledby="step3">
            <h2 className="card__title" id="step3" tabIndex={-1} ref={step === 3 ? headingRef : undefined}>
              03 검토
            </h2>

            <dl className="review">
              <div>
                <dt>프로젝트 이름</dt>
                <dd>{name}</dd>
              </div>
              <div>
                <dt>아이디어</dt>
                <dd className="review__long">{idea.summary}</dd>
              </div>
              <div>
                <dt>고정된 조건</dt>
                <dd className="review__long">{idea.constraints.trim() || '없음 — 결정 질문으로 물어봅니다.'}</dd>
              </div>
              <div>
                <dt>제외할 범위</dt>
                <dd className="review__long">{idea.exclusions.trim() || '없음 — 문서에 Non-goal이 비어 있게 됩니다.'}</dd>
              </div>
              <div>
                <dt>생성될 파일</dt>
                <dd>
                  <ul className="filetree">
                    <li>
                      <code className="mono">IDEATION.md</code>
                    </li>
                    <li>
                      <code className="mono">PRD.md</code>
                    </li>
                    <li>
                      <code className="mono">TRD.md</code>
                    </li>
                    {merged.map((group) => (
                      <li key={group.path}>
                        <code className="mono">{group.path}</code>{' '}
                        <span className="filetree__for">
                          {group.targetIds.map((id) => findTarget(id)?.name ?? id).join(', ')}
                        </span>
                      </li>
                    ))}
                  </ul>
                </dd>
              </div>
              <div>
                <dt>예상 질문 수</dt>
                <dd>
                  <span className="mono">7</span>개 (Critical 3 · Important 2 · Defaultable 2)
                </dd>
              </div>
            </dl>

            <p className="note">
              입력한 아이디어와 답변은 명세 생성과 검증에만 사용합니다. 문서 본문에는 토큰·연결 문자열 같은 비밀 값을
              넣지 않습니다.
            </p>

            {error && <ErrorState error={error} onRetry={submit} preserved="입력한 내용은 그대로 남아 있습니다." />}

            <div className="card__actions">
              <button type="button" className="btn btn--ghost" onClick={() => goToStep(2)} disabled={submitting}>
                이전
              </button>
              <button type="button" className="btn btn--primary" onClick={submit} disabled={submitting}>
                {submitting ? '결정 질문 준비 중…' : '결정 시작'}
              </button>
            </div>
          </section>
        )}
      </div>
    </AppShell>
  )
}
