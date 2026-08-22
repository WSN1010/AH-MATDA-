import { useEffect, useMemo, useRef, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { AppShell } from '../components/AppShell'
import { ExportSheet } from '../components/ExportSheet'
import { Markdown, extractHeadings } from '../components/Markdown'
import { QualityInspector } from '../components/QualityInspector'
import { ArtifactBadge, ErrorState, Skeleton, StageRail, StatusBadge, Tabs, formatDateTime } from '../components/ui'
import {
  exportBundle,
  getArtifact,
  getArtifacts,
  getProject,
  getValidationRun,
  saveArtifact,
  startValidation,
  suggestFix,
} from '../api/client'
import { findTarget } from '../api/targets'
import { collapseUnchanged, diffLines } from '../lib/diff'
import { useMedia } from '../lib/useMedia'
import { asApiError, useAsync } from '../lib/useAsync'
import type { ApiError } from '../api/error'
import type { Artifact, ArtifactKind, Finding, Project, ValidationRun } from '../api/types'

const KIND_LABEL: Record<ArtifactKind, string> = {
  Ideation: '배경 · 범위',
  Prd: '제품 요구사항',
  Trd: '기술 설계',
  AgentInstruction: '대상 지침',
}

function safeFileName(name: string): string {
  return name.replace(/[\\/:*?"<>|]+/g, '-').trim() || 'ajure'
}

export function Workspace() {
  const { id = '' } = useParams()
  const navigate = useNavigate()

  const bundle = useAsync(async () => {
    const project = await getProject(id)
    const [artifacts, run] = await Promise.all([
      getArtifacts(project.specVersionId),
      project.latestRunId ? getValidationRun(project.latestRunId) : Promise.resolve(null),
    ])
    return { project, artifacts, run }
  }, [id])

  const [project, setProject] = useState<Project | null>(null)
  const [artifacts, setArtifacts] = useState<Artifact[]>([])
  const [run, setRun] = useState<ValidationRun | null>(null)
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [mode, setMode] = useState('preview')
  const [draft, setDraft] = useState('')
  const [dirty, setDirty] = useState(false)
  const [notice, setNotice] = useState('')
  const [saveError, setSaveError] = useState<ApiError | null>(null)
  const [saving, setSaving] = useState(false)
  const [revalidating, setRevalidating] = useState(false)
  const [highlightLine, setHighlightLine] = useState<number | null>(null)
  const [pendingFix, setPendingFix] = useState<Finding | null>(null)
  const [inspectorOpen, setInspectorOpen] = useState(false)
  const [exportOpen, setExportOpen] = useState(false)

  const confirmRef = useRef<HTMLDialogElement>(null)
  const drawerRef = useRef<HTMLDialogElement>(null)
  const diffRef = useRef<HTMLDialogElement>(null)
  const canvasRef = useRef<HTMLDivElement>(null)
  const loadedContentId = useRef<string | null>(null)

  const wide = useMedia('(min-width: 1280px)')
  const compactNav = useMedia('(max-width: 1023px)')
  const narrow = useMedia('(max-width: 767px)')

  useEffect(() => {
    if (!bundle.data) return
    setProject(bundle.data.project)
    setArtifacts(bundle.data.artifacts)
    setRun(bundle.data.run)
    setSelectedId((current) => current ?? bundle.data?.artifacts[0]?.id ?? null)
  }, [bundle.data])

  const content = useAsync(
    () => (selectedId ? getArtifact(selectedId) : Promise.resolve(null)),
    [selectedId],
  )

  useEffect(() => {
    const loaded = content.data
    if (!loaded) return
    // 같은 문서를 다시 렌더링할 때 사용자의 편집 초안을 덮어쓰지 않는다.
    if (loadedContentId.current === loaded.id) return
    loadedContentId.current = loaded.id
    setDraft(loaded.content)
    setDirty(false)
    // 다른 문서의 Finding에서 넘어온 자동 수정은 문서를 다 불러온 뒤 적용한다.
    if (pendingFix && pendingFix.artifactPath === loaded.path) {
      applySuggestion(pendingFix, loaded.content)
      setPendingFix(null)
    }
  }, [content.data, narrow, pendingFix])

  useEffect(() => {
    const dialog = drawerRef.current
    if (!dialog) return
    if (inspectorOpen && !wide && !dialog.open) dialog.showModal()
    if ((!inspectorOpen || wide) && dialog.open) dialog.close()
  }, [inspectorOpen, wide])

  const selected = artifacts.find((artifact) => artifact.id === selectedId) ?? null
  const headings = useMemo(() => extractHeadings(draft), [draft])
  const diffRows = useMemo(
    () => (content.data ? collapseUnchanged(diffLines(content.data.content, draft)) : []),
    [content.data, draft],
  )
  const staleCount = artifacts.filter((artifact) => artifact.status === 'Stale').length

  function selectArtifact(nextId: string) {
    if (dirty && !window.confirm('저장하지 않은 편집이 있습니다. 다른 문서로 이동하면 편집 내용이 사라집니다.')) return
    setSelectedId(nextId)
    setMode('preview')
    setHighlightLine(null)
    setNotice('')
  }

  function requestSave() {
    setSaveError(null)
    confirmRef.current?.showModal()
  }

  async function commitSave() {
    if (!selected) return
    confirmRef.current?.close()
    setSaving(true)
    setSaveError(null)
    try {
      const result = await saveArtifact(selected.id, draft)
      setArtifacts((current) =>
        current.map((artifact) => {
          if (artifact.id === result.artifact.id) return result.artifact
          if (!result.affectedPaths.includes(artifact.path)) return artifact
          return {
            ...artifact,
            status: 'Stale',
            staleReason: `${result.artifact.path} 편집 이후 다시 렌더링되지 않았습니다.`,
          }
        }),
      )
      setProject((current) => (current ? { ...current, status: result.projectStatus } : current))
      setDirty(false)
      setMode('preview')
      setNotice(
        result.affectedPaths.length > 0
          ? `저장했습니다. ${result.affectedPaths.join(', ')}가 Stale이 됐고 Ready 판정이 해제됐습니다. 재검증하세요.`
          : '저장했습니다. 재검증하면 점수와 Hard Gate가 다시 계산됩니다.',
      )
      if (project?.latestRunId) setRun(await getValidationRun(project.latestRunId))
    } catch (caught) {
      setSaveError(asApiError(caught))
    } finally {
      setSaving(false)
    }
  }

  function applyFix(finding: Finding) {
    const target = artifacts.find((artifact) => artifact.path === finding.artifactPath)
    if (!target) return
    setInspectorOpen(false)

    if (target.id === selectedId) {
      applySuggestion(finding, draft)
      return
    }
    // 다른 문서면 그 문서를 먼저 불러온 뒤 초안에 적용한다.
    setPendingFix(finding)
    setSelectedId(target.id)
  }

  function applySuggestion(finding: Finding, source: string) {
    const next = suggestFix(finding.id, source)
    if (next === source) {
      setNotice(
        `제안을 자동으로 적용하지 못했습니다. ${finding.artifactPath}에서 직접 반영하세요: ${finding.suggestion ?? ''}`,
      )
      return
    }
    setDraft(next)
    setDirty(true)
    setMode(narrow ? 'edit' : 'diff')
    setNotice('제안을 초안에 적용했습니다. 변경 내용을 확인하고 저장하세요.')
    if (narrow) window.requestAnimationFrame(() => diffRef.current?.showModal())
  }

  function locate(finding: Finding) {
    const target = artifacts.find((artifact) => artifact.path === finding.artifactPath)
    if (!target) return
    setInspectorOpen(false)
    setSelectedId(target.id)
    setMode('preview')
    setHighlightLine(finding.line)
    window.requestAnimationFrame(() => {
      canvasRef.current?.querySelector('.md-block--hit')?.scrollIntoView({ block: 'center' })
    })
  }

  async function revalidate() {
    if (!project) return
    setRevalidating(true)
    try {
      const job = await startValidation(project.specVersionId)
      navigate(`/projects/${id}/run/${job.jobId}`)
    } catch (caught) {
      setSaveError(asApiError(caught))
      setRevalidating(false)
    }
  }

  async function download(includeReport: boolean) {
    if (!project) return
    const blob = await exportBundle(project.specVersionId, includeReport)
    const url = URL.createObjectURL(blob)
    const anchor = document.createElement('a')
    anchor.href = url
    anchor.download = `${safeFileName(project.name)}-v${project.specVersionNumber}-implementation-bundle.zip`
    document.body.appendChild(anchor)
    anchor.click()
    anchor.remove()
    URL.revokeObjectURL(url)
  }

  const inspector = run && project && (
    <QualityInspector
      run={run}
      projectId={id}
      onApplyFix={applyFix}
      onLocate={locate}
      onRevalidate={revalidate}
      onExport={() => {
        setInspectorOpen(false)
        setExportOpen(true)
      }}
      busy={saving}
      revalidating={revalidating}
    />
  )

  const context = project && (
    <>
      <span className="topbar__project">{project.name}</span>
      <span className="mono topbar__version">v{project.specVersionNumber}</span>
      <StatusBadge status={project.status} />
    </>
  )

  return (
    <AppShell title="문서 워크벤치" context={context}>
      <div className="page page--full">
        <StageRail
          current="delivery"
          links={{
            decisions: `/projects/${id}/decisions`,
            validation: project?.latestJobId ? `/projects/${id}/run/${project.latestJobId}` : undefined,
            delivery: `/projects/${id}/workspace`,
          }}
        />

        {bundle.status === 'loading' && <Skeleton rows={6} label="워크벤치를 불러오는 중" />}
        {bundle.status === 'error' && bundle.error && <ErrorState error={bundle.error} onRetry={bundle.reload} />}

        {bundle.status === 'ready' && artifacts.length === 0 && (
          <div className="state state--empty">
            <p className="state__title">아직 생성된 문서가 없습니다.</p>
            <p className="state__body">결정을 마치면 명세 생성이 시작되고 여기에 4종 문서가 나타납니다.</p>
            <Link className="btn btn--primary" to={`/projects/${id}/decisions`}>
              결정 이어가기
            </Link>
          </div>
        )}

        {project && artifacts.length > 0 && (
          <div className={`wb ${wide ? 'wb--three' : 'wb--two'}`}>
            <nav className="wb__nav" aria-label="산출물">
              {compactNav ? (
                <div className="field field--inline">
                  <label htmlFor="artifact-select">문서 선택</label>
                  <select
                    id="artifact-select"
                    className="input"
                    value={selectedId ?? ''}
                    onChange={(event) => selectArtifact(event.target.value)}
                  >
                    {artifacts.map((artifact) => (
                      <option key={artifact.id} value={artifact.id}>
                        {artifact.path} — {artifact.status === 'Stale' ? 'Stale' : '최신'}
                      </option>
                    ))}
                  </select>
                </div>
              ) : (
                <ul className="artifacts">
                  {artifacts.map((artifact) => (
                    <li key={artifact.id}>
                      <button
                        type="button"
                        className={`artifact ${artifact.id === selectedId ? 'artifact--on' : ''}`}
                        onClick={() => selectArtifact(artifact.id)}
                        aria-current={artifact.id === selectedId ? 'true' : undefined}
                      >
                        <span className="artifact__path mono">{artifact.path}</span>
                        <span className="artifact__kind">
                          {artifact.targetId
                            ? (findTarget(artifact.targetId)?.name ?? KIND_LABEL[artifact.kind])
                            : KIND_LABEL[artifact.kind]}
                        </span>
                        <ArtifactBadge status={artifact.status} />
                      </button>
                    </li>
                  ))}
                </ul>
              )}

              {staleCount > 0 && (
                <p className="note note--warn">
                  <span aria-hidden="true">△ </span>
                  {staleCount}개 파일이 최신 명세와 어긋납니다. 재검증하면 다시 렌더링됩니다.
                </p>
              )}

              {headings.length > 0 && !compactNav && (
                <div className="minimap">
                  <h2 className="minimap__title">문서 구조</h2>
                  <ul>
                    {headings.map((heading) => (
                      <li key={heading.id} className={`minimap__l${heading.level}`}>
                        <button
                          type="button"
                          className="linkbtn"
                          onClick={() => document.getElementById(heading.id)?.scrollIntoView({ block: 'start' })}
                        >
                          {heading.text}
                        </button>
                      </li>
                    ))}
                  </ul>
                </div>
              )}
            </nav>

            <section className="wb__canvas" aria-label="문서 본문">
              <header className="canvas__head">
                <div>
                  <h1 className="canvas__title mono">{selected?.path ?? '문서'}</h1>
                  <p className="canvas__meta mono">
                    v{selected?.specVersionNumber} · hash {selected?.contentHash} ·{' '}
                    {selected ? formatDateTime(selected.updatedAt) : ''}
                  </p>
                  {selected?.staleReason && (
                    <p className="canvas__stale">
                      <span aria-hidden="true">△ </span>
                      {selected.staleReason}
                    </p>
                  )}
                </div>
                <Tabs
                  idPrefix="canvas"
                  label="문서 보기 모드"
                  value={mode}
                  onChange={(next) => {
                    setMode(next)
                    if (next === 'diff' && narrow) diffRef.current?.showModal()
                  }}
                  items={
                    narrow
                      ? [
                          { id: 'preview', label: 'Preview' },
                          { id: 'edit', label: 'Edit' },
                        ]
                      : [
                          { id: 'preview', label: 'Preview' },
                          { id: 'edit', label: 'Edit' },
                          { id: 'diff', label: 'Diff' },
                        ]
                  }
                />
              </header>

              <p className="canvas__notice" aria-live="polite">
                {notice}
              </p>

              {saveError && <ErrorState error={saveError} onRetry={commitSave} />}

              {content.status === 'loading' && <Skeleton rows={5} label="문서를 불러오는 중" />}
              {content.status === 'error' && content.error && (
                <ErrorState error={content.error} onRetry={content.reload} />
              )}

              {content.status === 'ready' && (
                <div className="canvas__body" ref={canvasRef}>
                  {mode === 'preview' && (
                    <div id="canvas-panel-preview" role="tabpanel" aria-labelledby="canvas-tab-preview" tabIndex={0}>
                      <Markdown content={draft} highlightLine={highlightLine} />
                    </div>
                  )}

                  {mode === 'edit' && (
                    <div id="canvas-panel-edit" role="tabpanel" aria-labelledby="canvas-tab-edit" tabIndex={0}>
                      <div className="field">
                        <label htmlFor="editor">Markdown 원문</label>
                        <textarea
                          id="editor"
                          className="input input--editor mono"
                          value={draft}
                          spellCheck={false}
                          onChange={(event) => {
                            setDraft(event.target.value)
                            setDirty(event.target.value !== content.data?.content)
                          }}
                          aria-describedby="editor-help"
                        />
                        <p className="field__help" id="editor-help">
                          저장하면 이 버전의 Ready 판정이 해제되고 대상 지침 파일이 Stale이 됩니다.
                        </p>
                      </div>
                      {!narrow && dirty && (
                        <button type="button" className="btn btn--ghost btn--sm" onClick={() => setMode('diff')}>
                          변경 비교
                        </button>
                      )}
                      {narrow && dirty && (
                        <button
                          type="button"
                          className="btn btn--ghost btn--sm"
                          onClick={() => diffRef.current?.showModal()}
                        >
                          변경 비교
                        </button>
                      )}
                    </div>
                  )}

                  {mode === 'diff' && !narrow && (
                    <div id="canvas-panel-diff" role="tabpanel" aria-labelledby="canvas-tab-diff" tabIndex={0}>
                      <DiffView rows={diffRows} dirty={dirty} />
                    </div>
                  )}
                </div>
              )}

              <div className="canvas__foot">
                <p className="canvas__dirty">
                  {dirty ? '저장하지 않은 편집이 있습니다.' : '저장된 내용과 같습니다.'}
                </p>
                <div className="canvas__footActions">
                  {dirty && (
                    <button type="button" className="btn btn--ghost" onClick={() => setDraft(content.data?.content ?? '')}>
                      편집 되돌리기
                    </button>
                  )}
                  <button type="button" className="btn btn--primary" onClick={requestSave} disabled={!dirty || saving}>
                    {saving ? '저장 중…' : '저장'}
                  </button>
                </div>
              </div>
            </section>

            {wide ? (
              <aside className="wb__inspector" aria-label="품질 검사">
                {inspector ?? (
                  <p className="state state--empty state--inline">
                    아직 검증 결과가 없습니다. 재검증을 실행하면 점수와 Hard Gate가 계산됩니다.
                  </p>
                )}
              </aside>
            ) : (
              <button type="button" className="btn btn--secondary wb__drawerBtn" onClick={() => setInspectorOpen(true)}>
                품질 검사 열기
                {run && <span className="mono"> · {run.score}/100</span>}
              </button>
            )}
          </div>
        )}

        {project && artifacts.length > 0 && narrow && (
          <div className="stickybar">
            <button type="button" className="btn btn--ghost btn--sm" onClick={revalidate} disabled={revalidating}>
              검증
            </button>
            <Link className="btn btn--ghost btn--sm" to={`/projects/${id}/decisions`}>
              결정 보기
            </Link>
            <button
              type="button"
              className="btn btn--primary btn--sm"
              onClick={() => setExportOpen(true)}
              disabled={!run?.ready}
            >
              내보내기
            </button>
          </div>
        )}
      </div>

      <dialog className="drawer" ref={drawerRef} onClose={() => setInspectorOpen(false)} aria-label="품질 검사">
        <div className="drawer__inner">
          <header className="drawer__head">
            <h2>Quality Inspector</h2>
            <button type="button" className="iconbtn" onClick={() => setInspectorOpen(false)} aria-label="품질 검사 닫기">
              <span aria-hidden="true">×</span>
            </button>
          </header>
          {inspector}
        </div>
      </dialog>

      <dialog className="sheet" ref={diffRef} aria-label="변경 비교">
        <div className="sheet__inner">
          <header className="sheet__head">
            <h2>변경 비교</h2>
            <button type="button" className="iconbtn" onClick={() => diffRef.current?.close()} aria-label="변경 비교 닫기">
              <span aria-hidden="true">×</span>
            </button>
          </header>
          <DiffView rows={diffRows} dirty={dirty} />
        </div>
      </dialog>

      <dialog className="confirm" ref={confirmRef} aria-labelledby="confirm-title">
        <div className="confirm__inner">
          <h2 id="confirm-title">저장하면 Ready 판정이 해제됩니다</h2>
          <p>
            사용자가 직접 편집한 내용은 새 후보로 저장됩니다. 다음 파일이 Stale이 되고 재검증 전까지 내보낼 수 없습니다.
          </p>
          <ul className="filetree">
            {artifacts
              .filter((artifact) => artifact.kind === 'AgentInstruction')
              .map((artifact) => (
                <li key={artifact.id}>
                  <code className="mono">{artifact.path}</code>
                </li>
              ))}
          </ul>
          <div className="confirm__actions">
            <button type="button" className="btn btn--ghost" onClick={() => confirmRef.current?.close()}>
              취소
            </button>
            <button type="button" className="btn btn--primary" onClick={commitSave}>
              저장하고 재검증 대기
            </button>
          </div>
        </div>
      </dialog>

      {project && run && (
        <ExportSheet
          open={exportOpen}
          onClose={() => setExportOpen(false)}
          artifacts={artifacts}
          targetIds={project.targetIds}
          projectName={project.name}
          run={run}
          download={download}
        />
      )}
    </AppShell>
  )
}

function DiffView({
  rows,
  dirty,
}: {
  rows: (ReturnType<typeof diffLines>[number] | { type: 'gap'; count: number })[]
  dirty: boolean
}) {
  if (!dirty) {
    return <p className="state state--empty state--inline">아직 변경한 내용이 없습니다. Edit 탭에서 문서를 고치세요.</p>
  }
  return (
    <div className="diff mono">
      {rows.map((row, index) =>
        row.type === 'gap' ? (
          <p key={index} className="diff__gap">
            … {row.count}줄 동일
          </p>
        ) : (
          <p key={index} className={`diff__row diff__row--${row.type}`}>
            <span className="diff__sign" aria-hidden="true">
              {row.type === 'add' ? '+' : row.type === 'del' ? '−' : ' '}
            </span>
            <span className="visually-hidden">{row.type === 'add' ? '추가' : row.type === 'del' ? '삭제' : '동일'}</span>
            {row.text || ' '}
          </p>
        ),
      )}
    </div>
  )
}
