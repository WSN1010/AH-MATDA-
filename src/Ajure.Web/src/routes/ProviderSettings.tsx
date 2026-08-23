import { useState } from 'react'
import { deleteModelProvider, listModelProviders, saveModelProvider } from '../api/client'
import type { ApiError } from '../api/error'
import type { ModelProviderList, ModelProviderStatus } from '../api/types'
import { AppShell } from '../components/AppShell'
import { ErrorState, Skeleton } from '../components/ui'
import { asApiError, useAsync } from '../lib/useAsync'

const PROVIDER_COPY: Record<
  ModelProviderStatus['id'],
  { shortName: string; description: string; environmentKey: string }
> = {
  openai: {
    shortName: 'OpenAI',
    description: 'GPT를 명세 작성 또는 독립 검토에 사용합니다.',
    environmentKey: 'OPENAI_API_KEY',
  },
  anthropic: {
    shortName: 'Claude',
    description: 'Claude를 명세 작성 또는 독립 검토에 사용합니다.',
    environmentKey: 'ANTHROPIC_API_KEY',
  },
  gemini: {
    shortName: 'Gemini',
    description: 'Gemini를 명세 작성 또는 독립 검토에 사용합니다.',
    environmentKey: 'GEMINI_API_KEY',
  },
}

function connectionLabel(provider: ModelProviderStatus): string {
  if (provider.errorCode === 'credential_unreadable') return '다시 연결 필요'
  if (provider.source === 'environment') return '운영자 관리'
  return provider.configured ? '연결됨' : '연결 안 됨'
}

function ProviderQuorum({ data }: { data: ModelProviderList }) {
  const remaining = Math.max(0, data.requiredCount - data.configuredCount)
  const ready = remaining === 0
  return (
    <section className={`provider-quorum ${ready ? 'provider-quorum--ready' : ''}`} aria-labelledby="quorum-title">
      <div className="provider-quorum__head">
        <div>
          <p className="eyebrow mono">MODEL QUORUM</p>
          <h2 id="quorum-title">{ready ? '모델 연결 준비 완료' : `${remaining}개 더 연결하세요.`}</h2>
          <p>
            {ready
              ? '서로 다른 모델 2개 이상으로 독립 검토할 수 있습니다.'
              : 'Ready 검증에는 서로 다른 공급자 모델이 2개 이상 필요합니다.'}
          </p>
        </div>
        <p className="provider-quorum__count mono" aria-label={`${data.configuredCount}개 구성됨, ${data.requiredCount}개 필요`}>
          <strong>{data.configuredCount}</strong>
          <span> / {data.requiredCount} CONNECTED</span>
        </p>
      </div>
      <ul className="provider-quorum__track" aria-label="모델 공급자 연결 상태">
        {data.providers.map((provider) => {
          const failed = provider.errorCode === 'credential_unreadable'
          const state = failed ? 'failed' : provider.configured ? 'connected' : 'empty'
          return (
            <li className={`provider-node provider-node--${state}`} key={provider.id}>
              <span className="provider-node__port" aria-hidden="true">
                {failed ? '!' : provider.configured ? '✓' : '○'}
              </span>
              <span className="provider-node__name">{PROVIDER_COPY[provider.id].shortName}</span>
              <span className="provider-node__state">{connectionLabel(provider)}</span>
            </li>
          )
        })}
      </ul>
    </section>
  )
}

function ProviderBay({
  provider,
  onSaved,
  onRemoved,
}: {
  provider: ModelProviderStatus
  onSaved: (provider: ModelProviderStatus) => void
  onRemoved: (providerId: ModelProviderStatus['id']) => Promise<ModelProviderStatus>
}) {
  const [apiKey, setApiKey] = useState('')
  const [model, setModel] = useState(provider.model)
  const [showKey, setShowKey] = useState(false)
  const [activity, setActivity] = useState<'idle' | 'saving' | 'removing'>('idle')
  const [confirming, setConfirming] = useState(false)
  const [error, setError] = useState<ApiError | null>(null)
  const [feedback, setFeedback] = useState('')
  const copy = PROVIDER_COPY[provider.id]
  const busy = activity !== 'idle'
  const hasLocalCredential = provider.source === 'local'
  const failed = provider.errorCode === 'credential_unreadable'

  async function save() {
    setActivity('saving')
    setError(null)
    setFeedback('')
    try {
      const saved = await saveModelProvider(provider.id, {
        apiKey: apiKey.trim(),
        model: model.trim(),
      })
      setApiKey('')
      setModel(saved.model)
      setShowKey(false)
      setFeedback('저장됨. API 키는 다시 표시되지 않습니다.')
      onSaved(saved)
    } catch (caught) {
      setError(asApiError(caught))
    } finally {
      setActivity('idle')
    }
  }

  async function remove() {
    setActivity('removing')
    setError(null)
    setFeedback('')
    try {
      await deleteModelProvider(provider.id)
      setApiKey('')
      setShowKey(false)
      setConfirming(false)
      setFeedback('연결을 제거했습니다.')
      const removed = await onRemoved(provider.id)
      setModel(removed.model)
    } catch (caught) {
      setError(asApiError(caught))
    } finally {
      setActivity('idle')
    }
  }

  const badgeTone = failed ? 'fail' : provider.configured ? 'pass' : 'neutral'
  return (
    <article className={`provider-bay ${provider.configured ? 'provider-bay--connected' : ''}`}>
      <header className="provider-bay__head">
        <span className="provider-bay__sigil mono" aria-hidden="true">
          {copy.shortName.slice(0, 1)}
        </span>
        <div>
          <p className="provider-bay__id mono">{provider.id}</p>
          <h2>{provider.displayName}</h2>
        </div>
        <span className={`badge badge--${badgeTone}`}>
          <span className="badge__glyph" aria-hidden="true">
            {failed ? '!' : provider.configured ? '✓' : '○'}
          </span>
          {connectionLabel(provider)}
        </span>
      </header>
      <p className="provider-bay__description">{copy.description}</p>

      {failed && (
        <p className="note note--warn" role="alert">
          저장된 보호 키를 읽지 못했습니다. 새 키로 다시 저장하거나 연결을 제거하세요.
        </p>
      )}

      <div className="provider-bay__form">
        <div className="field">
          <label htmlFor={`${provider.id}-model`}>모델 ID</label>
          <input
            id={`${provider.id}-model`}
            className="input mono"
            value={model}
            onChange={(event) => setModel(event.target.value)}
            disabled={!provider.editable || busy}
            autoComplete="off"
            spellCheck={false}
          />
        </div>

        <div className="field">
          <label htmlFor={`${provider.id}-key`}>API 키</label>
          <div className="secret-input">
            <input
              id={`${provider.id}-key`}
              className="input mono"
              type={showKey ? 'text' : 'password'}
              value={apiKey}
              onChange={(event) => setApiKey(event.target.value)}
              disabled={!provider.editable || busy}
              placeholder={
                provider.source === 'environment'
                  ? '환경 변수로 설정됨'
                  : hasLocalCredential
                    ? '새 키를 입력하면 기존 키를 교체합니다'
                    : 'API 키 입력'
              }
              autoComplete="new-password"
              spellCheck={false}
              aria-describedby={`${provider.id}-key-help`}
            />
            <button
              type="button"
              className="btn btn--ghost secret-input__toggle"
              onClick={() => setShowKey((current) => !current)}
              aria-pressed={showKey}
              disabled={!provider.editable || busy || apiKey.length === 0}
            >
              {showKey ? '숨기기' : '표시'}
            </button>
          </div>
          <p className="field__help" id={`${provider.id}-key-help`}>
            저장 후 키 원문이나 일부 문자를 다시 보여 주지 않습니다.
          </p>
        </div>
      </div>

      {provider.source === 'environment' && (
        <p className="provider-bay__locked">
          <span aria-hidden="true">⌁</span>
          <span>
            <code className="mono">{copy.environmentKey}</code>로 관리 중입니다. 이 화면에서는 바꾸거나 제거할 수 없습니다.
          </span>
        </p>
      )}

      {error && (
        <p className="provider-bay__error" role="alert">
          <span aria-hidden="true">×</span> {error.problem.message}{' '}
          <span className="mono">({error.problem.code})</span>
        </p>
      )}
      <p className="provider-bay__feedback" role="status" aria-live="polite">
        {feedback}
      </p>

      {provider.editable && (
        <div className="provider-bay__actions">
          {confirming ? (
            <div className="provider-bay__confirm" role="group" aria-label={`${provider.displayName} 연결 제거 확인`}>
              <p>저장된 키와 모델 연결을 이 PC에서 제거할까요?</p>
              <button type="button" className="btn btn--ghost" onClick={() => setConfirming(false)} disabled={busy}>
                취소
              </button>
              <button type="button" className="btn btn--secondary" onClick={remove} disabled={busy}>
                {activity === 'removing' ? '제거 중…' : '연결 제거'}
              </button>
            </div>
          ) : (
            <>
              {hasLocalCredential && (
                <button type="button" className="btn btn--ghost" onClick={() => setConfirming(true)} disabled={busy}>
                  연결 제거
                </button>
              )}
              <button
                type="button"
                className="btn btn--primary"
                onClick={save}
                disabled={busy || apiKey.trim().length === 0 || model.trim().length === 0}
              >
                {activity === 'saving' ? '저장 중…' : hasLocalCredential ? '키 교체' : '연결 저장'}
              </button>
            </>
          )}
        </div>
      )}
    </article>
  )
}

export function ProviderSettings() {
  const providers = useAsync(() => listModelProviders(), [])

  function replaceProvider(next: ModelProviderStatus) {
    if (!providers.data) return
    const updated = providers.data.providers.map((provider) => (provider.id === next.id ? next : provider))
    providers.setData({
      ...providers.data,
      configuredCount: updated.filter((provider) => provider.configured).length,
      providers: updated,
    })
  }

  async function refreshAfterRemove(providerId: ModelProviderStatus['id']) {
    const refreshed = await listModelProviders()
    providers.setData(refreshed)
    const removed = refreshed.providers.find((provider) => provider.id === providerId)
    if (!removed) throw new Error('제거한 모델 공급자 상태를 다시 불러오지 못했습니다.')
    return removed
  }

  return (
    <AppShell title="모델 설정">
      <div className="page page--wide provider-page">
        <header className="page__head page__head--stack">
          <div>
            <p className="eyebrow mono">LOCAL MODEL PROVIDERS</p>
            <h1 className="page__title">모델 API 연결</h1>
            <p className="page__lead">
              OpenAI, Claude, Gemini 키를 이 PC에만 저장합니다. 서로 다른 공급자 2개를 연결하면 독립 검토를 시작할 수
              있습니다.
            </p>
          </div>
          <p className="provider-page__privacy">
            <span aria-hidden="true">⌁</span> 키는 로컬에서 암호화되며 브라우저에 저장되지 않습니다.
          </p>
        </header>

        {providers.status === 'loading' && (
          <div className="provider-loading">
            <Skeleton rows={1} label="모델 연결 상태를 불러오는 중" />
            <div className="provider-grid" aria-hidden="true">
              {Array.from({ length: 3 }, (_, index) => (
                <Skeleton rows={4} label="" key={index} />
              ))}
            </div>
          </div>
        )}

        {providers.status === 'error' && providers.error && (
          <ErrorState
            error={providers.error}
            onRetry={providers.reload}
            preserved="이미 저장된 로컬 키는 삭제되지 않았습니다."
          />
        )}

        {providers.status === 'ready' && providers.data && (
          <>
            <ProviderQuorum data={providers.data} />
            <div className="provider-grid">
              {providers.data.providers.map((provider) => (
                <ProviderBay
                  key={provider.id}
                  provider={provider}
                  onSaved={replaceProvider}
                  onRemoved={refreshAfterRemove}
                />
              ))}
            </div>
          </>
        )}
      </div>
    </AppShell>
  )
}
