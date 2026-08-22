import { useEffect, useRef, useState } from 'react'
import { ErrorState } from './ui'
import { findTarget } from '../api/targets'
import { asApiError } from '../lib/useAsync'
import type { ApiError } from '../api/error'
import type { Artifact, ValidationRun } from '../api/types'

export function ExportSheet({
  open,
  onClose,
  artifacts,
  targetIds,
  projectName,
  run,
  download,
}: {
  open: boolean
  onClose: () => void
  artifacts: Artifact[]
  targetIds: string[]
  projectName: string
  run: ValidationRun
  download: (includeReport: boolean) => Promise<void>
}) {
  const dialogRef = useRef<HTMLDialogElement>(null)
  const [includeReport, setIncludeReport] = useState(false)
  const [busy, setBusy] = useState(false)
  const [done, setDone] = useState(false)
  const [error, setError] = useState<ApiError | null>(null)

  useEffect(() => {
    const dialog = dialogRef.current
    if (!dialog) return
    if (open && !dialog.open) dialog.showModal()
    if (!open && dialog.open) dialog.close()
  }, [open])

  async function startDownload() {
    setBusy(true)
    setError(null)
    try {
      await download(includeReport)
      setDone(true)
    } catch (caught) {
      setError(asApiError(caught))
    } finally {
      setBusy(false)
    }
  }

  const passedGates = run.hardGates.filter((gate) => gate.passed).length

  return (
    <dialog className="sheet" ref={dialogRef} onClose={onClose} aria-labelledby="export-title">
      <div className="sheet__inner">
        <header className="sheet__head">
          <h2 id="export-title">구현 번들 내보내기</h2>
          <button type="button" className="iconbtn" onClick={onClose} aria-label="내보내기 창 닫기">
            <span aria-hidden="true">×</span>
          </button>
        </header>

        <dl className="sheet__meta">
          <div>
            <dt>프로젝트</dt>
            <dd>{projectName}</dd>
          </div>
          <div>
            <dt>Spec Version</dt>
            <dd className="mono">v{run.specVersionNumber}</dd>
          </div>
          <div>
            <dt>점수 / Hard Gate</dt>
            <dd className="mono">
              {run.score}/100 · {passedGates}/{run.hardGates.length}
            </dd>
          </div>
          <div>
            <dt>대상 도구</dt>
            <dd>{targetIds.map((id) => findTarget(id)?.name ?? id).join(', ')}</dd>
          </div>
        </dl>

        <h3 className="sheet__subtitle">포함 파일</h3>
        <ul className="filetree">
          {artifacts.map((artifact) => (
            <li key={artifact.id}>
              <code className="mono">{artifact.path}</code>
              {artifact.targetId && (
                <span className="filetree__for">{findTarget(artifact.targetId)?.name ?? artifact.targetId}</span>
              )}
            </li>
          ))}
          {includeReport && (
            <li>
              <code className="mono">VALIDATION-REPORT.md</code>
              <span className="filetree__for">선택 항목</span>
            </li>
          )}
        </ul>

        <label className="check">
          <input
            type="checkbox"
            checked={includeReport}
            onChange={(event) => setIncludeReport(event.target.checked)}
            aria-describedby="report-help"
          />
          <span>
            <span className="mono">VALIDATION-REPORT.md</span> 포함
          </span>
        </label>
        <p className="field__help" id="report-help">
          기본값은 제외입니다. 구현 에이전트가 읽을 필요 없는 컨텍스트를 늘리지 않기 위해서입니다.
        </p>

        {error && (
          <ErrorState error={error} onRetry={startDownload} preserved="문서와 검증 결과는 그대로 남아 있습니다." />
        )}

        {done && (
          <div className="state state--success" role="status">
            <p className="state__title">
              <span aria-hidden="true">✓</span> 구현 번들을 내려받았습니다.
            </p>
            <ol className="handoff handoff--compact">
              <li>ZIP을 새 프로젝트나 저장소 루트에 풉니다.</li>
              <li>선택한 코딩 에이전트를 그 루트에서 실행합니다.</li>
              <li>“이 명세를 기준으로 전체 구현하고 검증까지 완료해”라고 한 번만 요청합니다.</li>
            </ol>
            <p className="state__body">아주르는 이 저장소에 접근하거나 구현을 대신 실행하지 않습니다.</p>
          </div>
        )}

        <div className="sheet__actions">
          <button type="button" className="btn btn--ghost" onClick={onClose}>
            닫기
          </button>
          <button type="button" className="btn btn--primary" onClick={startDownload} disabled={busy || !run.ready}>
            {busy ? '번들 만드는 중…' : '구현 번들 다운로드'}
          </button>
        </div>
        {!run.ready && <p className="hint">{run.blockedReason ?? 'Ready 상태에서만 내보낼 수 있습니다.'}</p>}
      </div>
    </dialog>
  )
}
