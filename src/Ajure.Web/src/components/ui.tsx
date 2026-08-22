import type { KeyboardEvent, ReactNode } from 'react'
import { Link } from 'react-router-dom'
import type { ApiError } from '../api/error'
import type { ArtifactStatus, Severity, SpecStatus } from '../api/types'

/** 상태는 색만으로 구분하지 않는다. 글리프 + 텍스트를 항상 함께 쓴다. */
const SPEC_STATUS: Record<SpecStatus, { label: string; glyph: string; tone: string }> = {
  Draft: { label: '초안', glyph: '○', tone: 'neutral' },
  Analyzing: { label: '분석 중', glyph: '◐', tone: 'busy' },
  Generating: { label: '생성 중', glyph: '◐', tone: 'busy' },
  Validating: { label: '검증 중', glyph: '◐', tone: 'busy' },
  NeedsDecision: { label: '결정 필요', glyph: '!', tone: 'warn' },
  Ready: { label: 'Ready', glyph: '✓', tone: 'pass' },
  Superseded: { label: '대체됨', glyph: '↺', tone: 'neutral' },
}

export function StatusBadge({ status }: { status: SpecStatus }) {
  const meta = SPEC_STATUS[status]
  return (
    <span className={`badge badge--${meta.tone}`}>
      <span className="badge__glyph" aria-hidden="true">
        {meta.glyph}
      </span>
      {meta.label}
    </span>
  )
}

const ARTIFACT_STATUS: Record<ArtifactStatus, { label: string; glyph: string; tone: string }> = {
  Valid: { label: '최신', glyph: '✓', tone: 'pass' },
  Stale: { label: 'Stale', glyph: '△', tone: 'warn' },
  Error: { label: '오류', glyph: '×', tone: 'fail' },
}

export function ArtifactBadge({ status }: { status: ArtifactStatus }) {
  const meta = ARTIFACT_STATUS[status]
  return (
    <span className={`badge badge--sm badge--${meta.tone}`}>
      <span className="badge__glyph" aria-hidden="true">
        {meta.glyph}
      </span>
      {meta.label}
    </span>
  )
}

const SEVERITY: Record<Severity, { label: string; glyph: string; tone: string }> = {
  Critical: { label: 'Critical', glyph: '×', tone: 'fail' },
  Major: { label: 'Major', glyph: '!', tone: 'warn' },
  Minor: { label: 'Minor', glyph: '·', tone: 'neutral' },
}

export function SeverityChip({ severity }: { severity: Severity }) {
  const meta = SEVERITY[severity]
  return (
    <span className={`badge badge--sm badge--${meta.tone}`}>
      <span className="badge__glyph" aria-hidden="true">
        {meta.glyph}
      </span>
      {meta.label}
    </span>
  )
}

export function Skeleton({ rows = 4, label }: { rows?: number; label: string }) {
  return (
    <div className="skeleton" role="status" aria-live="polite">
      <span className="visually-hidden">{label}</span>
      {Array.from({ length: rows }, (_, index) => (
        <div key={index} className="skeleton__row" aria-hidden="true" />
      ))}
    </div>
  )
}

export function EmptyState({
  title,
  description,
  action,
}: {
  title: string
  description: string
  action?: ReactNode
}) {
  return (
    <div className="state state--empty">
      <p className="state__title">{title}</p>
      <p className="state__body">{description}</p>
      {action}
    </div>
  )
}

export function ErrorState({
  error,
  onRetry,
  preserved = '입력한 내용은 그대로 남아 있습니다.',
}: {
  error: ApiError
  onRetry?: () => void
  preserved?: string
}) {
  return (
    <div className="state state--error" role="alert">
      <p className="state__title">
        <span aria-hidden="true">×</span> {error.problem.message}
      </p>
      <p className="state__body">
        {error.problem.retryable
          ? '일시적인 문제입니다. 다시 시도하면 같은 지점에서 이어집니다.'
          : '다시 시도해도 같은 결과가 나옵니다. 입력을 확인하거나 다른 경로를 사용하세요.'}{' '}
        {preserved}
      </p>
      <p className="state__meta">
        <span className="mono">Correlation ID {error.problem.correlationId}</span>
        <span className="mono">code {error.problem.code}</span>
      </p>
      {onRetry && error.problem.retryable && (
        <button type="button" className="btn btn--secondary" onClick={onRetry}>
          다시 시도
        </button>
      )}
    </div>
  )
}

const STAGES = [
  { id: 'decisions', number: '01', label: '결정' },
  { id: 'spec', number: '02', label: '명세' },
  { id: 'validation', number: '03', label: '검증' },
  { id: 'delivery', number: '04', label: '전달' },
] as const

export type StageId = (typeof STAGES)[number]['id']

/** 프로젝트 내부 4단계 Rail. 실제 진행 순서를 나타내므로 번호를 쓴다. */
export function StageRail({ current, links }: { current: StageId; links: Partial<Record<StageId, string>> }) {
  const currentIndex = STAGES.findIndex((stage) => stage.id === current)
  return (
    <nav className="rail" aria-label="명세 진행 단계">
      <ol className="rail__list">
        {STAGES.map((stage, index) => {
          const state = index < currentIndex ? 'done' : index === currentIndex ? 'current' : 'todo'
          const href = links[stage.id]
          const inner = (
            <>
              <span className="rail__number mono" aria-hidden="true">
                {stage.number}
              </span>
              <span className="rail__label">{stage.label}</span>
              <span className="visually-hidden">
                {state === 'done' ? ' 완료' : state === 'current' ? ' 현재 단계' : ' 대기'}
              </span>
            </>
          )
          return (
            <li key={stage.id} className={`rail__item rail__item--${state}`}>
              {href && state !== 'todo' ? (
                <Link to={href} aria-current={state === 'current' ? 'step' : undefined}>
                  {inner}
                </Link>
              ) : (
                <span aria-current={state === 'current' ? 'step' : undefined}>{inner}</span>
              )}
            </li>
          )
        })}
      </ol>
    </nav>
  )
}

/** 점수는 원형 게이지 대신 영역별 근거가 보이는 수평 바로 표현한다. */
export function QualityBar({
  label,
  score,
  max,
  evidence,
}: {
  label: string
  score: number
  max: number
  evidence: string
}) {
  const percent = Math.round((score / max) * 100)
  const tone = percent >= 90 ? 'pass' : percent >= 70 ? 'warn' : 'fail'
  return (
    <li className="qbar">
      <div className="qbar__head">
        <span className="qbar__label">{label}</span>
        <span className="qbar__value mono">
          {score}/{max}
        </span>
      </div>
      <div
        className={`qbar__track qbar__track--${tone}`}
        role="meter"
        aria-valuenow={score}
        aria-valuemin={0}
        aria-valuemax={max}
        aria-label={`${label} ${score}점 / ${max}점 만점`}
      >
        <span className="qbar__fill" style={{ inlineSize: `${percent}%` }} />
      </div>
      <p className="qbar__evidence">{evidence}</p>
    </li>
  )
}

/** 접근 가능한 탭 목록. 패널은 호출자가 그린다. */
export function Tabs({
  items,
  value,
  onChange,
  label,
  idPrefix,
}: {
  items: { id: string; label: string; badge?: number }[]
  value: string
  onChange: (id: string) => void
  label: string
  idPrefix: string
}) {
  function onKeyDown(event: KeyboardEvent<HTMLDivElement>) {
    const index = items.findIndex((item) => item.id === value)
    if (index === -1) return
    let next = index
    if (event.key === 'ArrowRight') next = (index + 1) % items.length
    else if (event.key === 'ArrowLeft') next = (index - 1 + items.length) % items.length
    else if (event.key === 'Home') next = 0
    else if (event.key === 'End') next = items.length - 1
    else return
    event.preventDefault()
    const target = items[next]
    if (!target) return
    onChange(target.id)
    document.getElementById(`${idPrefix}-tab-${target.id}`)?.focus()
  }

  return (
    <div className="tabs" role="tablist" aria-label={label} onKeyDown={onKeyDown}>
      {items.map((item) => {
        const selected = item.id === value
        return (
          <button
            key={item.id}
            type="button"
            role="tab"
            id={`${idPrefix}-tab-${item.id}`}
            aria-selected={selected}
            aria-controls={`${idPrefix}-panel-${item.id}`}
            tabIndex={selected ? 0 : -1}
            className={`tabs__tab ${selected ? 'tabs__tab--on' : ''}`}
            onClick={() => onChange(item.id)}
          >
            {item.label}
            {item.badge !== undefined && item.badge > 0 && <span className="tabs__badge mono">{item.badge}</span>}
          </button>
        )
      })}
    </div>
  )
}

export function formatDateTime(iso: string): string {
  const date = new Date(iso)
  if (Number.isNaN(date.getTime())) return iso
  return new Intl.DateTimeFormat('ko-KR', {
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  }).format(date)
}

export function formatDuration(ms: number): string {
  if (ms < 1000) return `${ms}ms`
  return `${(ms / 1000).toFixed(1)}초`
}
