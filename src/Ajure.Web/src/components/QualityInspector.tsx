import { useState } from 'react'
import { Link } from 'react-router-dom'
import { QualityBar, SeverityChip, Tabs } from './ui'
import type { Finding, RegressionItem, ValidationRun } from '../api/types'

const REGRESSION_LABEL: Record<RegressionItem['kind'], string> = {
  Removed: '삭제됨',
  Weakened: '약화됨',
  Unlinked: '연결 끊김',
  Contradicted: '충돌',
  ScopeLeak: '범위 확장',
  StateLoss: '상태 누락',
  QualityDrop: '점수 하락',
  StaleArtifact: '오래된 산출물',
  FormatRegression: '형식 손상',
  ApprovedChange: '승인된 변경',
}

function FindingCard({
  finding,
  projectId,
  onApplyFix,
  onLocate,
  busy,
}: {
  finding: Finding
  projectId: string
  onApplyFix: (finding: Finding) => void
  onLocate: (finding: Finding) => void
  busy: boolean
}) {
  return (
    <li className={`finding finding--${finding.severity.toLowerCase()} ${finding.resolved ? 'finding--done' : ''}`}>
      <div className="finding__head">
        <SeverityChip severity={finding.severity} />
        <code className="mono finding__id">{finding.id}</code>
        {finding.resolved && <span className="finding__resolved">해결됨</span>}
      </div>
      <p className="finding__title">{finding.title}</p>
      <p className="finding__evidence">{finding.evidence}</p>
      <p className="finding__where">
        <button type="button" className="linkbtn mono" onClick={() => onLocate(finding)}>
          {finding.artifactPath}
          {finding.line !== null && ` : ${finding.line}`}
        </button>
      </p>
      {finding.relatedIds.length > 0 && (
        <p className="finding__ids">
          {finding.relatedIds.map((id) => (
            <code className="mono md-rid" key={id}>
              {id}
            </code>
          ))}
        </p>
      )}
      {finding.suggestion && <p className="finding__fix">제안: {finding.suggestion}</p>}
      {!finding.resolved && (
        <div className="finding__actions">
          {finding.autoFixable && (
            <button type="button" className="btn btn--secondary btn--sm" onClick={() => onApplyFix(finding)} disabled={busy}>
              수정 적용
            </button>
          )}
          {finding.decisionId && (
            <Link className="btn btn--ghost btn--sm" to={`/projects/${projectId}/decisions`}>
              결정하기
            </Link>
          )}
          {finding.severity === 'Critical' && (
            <p className="finding__note">Critical Finding은 사유 없이 무시할 수 없습니다.</p>
          )}
        </div>
      )}
    </li>
  )
}

export function QualityInspector({
  run,
  projectId,
  onApplyFix,
  onLocate,
  onRevalidate,
  onExport,
  busy,
  revalidating,
}: {
  run: ValidationRun
  projectId: string
  onApplyFix: (finding: Finding) => void
  onLocate: (finding: Finding) => void
  onRevalidate: () => void
  onExport: () => void
  busy: boolean
  revalidating: boolean
}) {
  const [tab, setTab] = useState('findings')
  const openFindings = run.findings.filter((finding) => !finding.resolved)
  const passedGates = run.hardGates.filter((gate) => gate.passed).length
  const delta = run.previousScore === null ? null : run.score - run.previousScore
  const unapprovedRegression = run.regression.filter((item) => !item.approved)

  return (
    <div className="inspector">
      <div className={`inspector__score ${run.ready ? 'inspector__score--ready' : ''}`}>
        <p className="inspector__verdict">
          <span className="inspector__verdictGlyph" aria-hidden="true">
            {run.ready ? '✓' : '!'}
          </span>
          {run.ready ? 'Ready' : '결정 필요'}
          <span className="inspector__number mono">
            {run.score}
            <span className="inspector__max">/100</span>
          </span>
        </p>
        <p className="inspector__gates mono">
          Hard Gate {passedGates}/{run.hardGates.length}
          {delta !== null && (
            <span className={`inspector__delta ${delta >= 0 ? 'is-up' : 'is-down'}`}>
              {delta >= 0 ? `+${delta}` : delta} vs 이전 검증
            </span>
          )}
        </p>
        {run.blockedReason ? (
          <p className="inspector__blocked">{run.blockedReason}</p>
        ) : (
          <p className="inspector__blocked inspector__blocked--ok">
            정의된 검증을 모두 통과했습니다. Ready는 완벽함이 아니라 이 기준을 통과한 상태입니다.
          </p>
        )}
      </div>

      <details className="gates" open={passedGates !== run.hardGates.length}>
        <summary>
          Hard Gate 체크리스트 <span className="mono">{passedGates}/{run.hardGates.length}</span>
        </summary>
        <ul className="gates__list">
          {run.hardGates.map((gate) => (
            <li key={gate.id} className={gate.passed ? 'gate gate--pass' : 'gate gate--fail'}>
              <span className="gate__glyph" aria-hidden="true">
                {gate.passed ? '✓' : '×'}
              </span>
              <span className="gate__body">
                <code className="mono gate__id">{gate.id}</code> {gate.label}
                <span className="visually-hidden">{gate.passed ? ' 통과' : ' 미통과'}</span>
                {gate.action && <span className="gate__action">{gate.action}</span>}
              </span>
            </li>
          ))}
        </ul>
      </details>

      <Tabs
        idPrefix="qi"
        label="검증 결과"
        value={tab}
        onChange={setTab}
        items={[
          { id: 'findings', label: 'Findings', badge: openFindings.length },
          { id: 'coverage', label: 'Coverage' },
          { id: 'regression', label: 'Regression', badge: unapprovedRegression.length },
        ]}
      />

      <div className="inspector__panels">
        {tab === 'findings' && (
          <div role="tabpanel" id="qi-panel-findings" aria-labelledby="qi-tab-findings" tabIndex={0}>
            {run.findings.length === 0 ? (
              <p className="state state--empty state--inline">
                아직 Finding이 없습니다. 문서를 수정한 뒤 재검증을 실행하세요.
              </p>
            ) : (
              <ul className="findings">
                {run.findings.map((finding) => (
                  <FindingCard
                    key={finding.id}
                    finding={finding}
                    projectId={projectId}
                    onApplyFix={onApplyFix}
                    onLocate={onLocate}
                    busy={busy}
                  />
                ))}
              </ul>
            )}
          </div>
        )}

        {tab === 'coverage' && (
          <div role="tabpanel" id="qi-panel-coverage" aria-labelledby="qi-tab-coverage" tabIndex={0}>
            <ul className="qbars">
              {run.areas.map((area) => (
                <QualityBar key={area.id} label={area.label} score={area.score} max={area.max} evidence={area.evidence} />
              ))}
            </ul>
          </div>
        )}

        {tab === 'regression' && (
          <div role="tabpanel" id="qi-panel-regression" aria-labelledby="qi-tab-regression" tabIndex={0}>
            {run.regression.length === 0 ? (
              <p className="state state--empty state--inline">
                {run.baseVersionNumber === null
                  ? '기준 버전이 없어 회귀 비교를 건너뜁니다. 이 버전을 내보내면 다음 검증의 기준선이 됩니다.'
                  : '기준 버전 대비 요구사항 손실이 없습니다.'}
              </p>
            ) : (
              <ul className="regression">
                {run.regression.map((item) => (
                  <li key={item.id} className={`reg reg--${item.severity.toLowerCase()}`}>
                    <p className="reg__head">
                      <span className={`badge badge--sm badge--${item.approved ? 'neutral' : 'fail'}`}>
                        <span className="badge__glyph" aria-hidden="true">
                          {item.approved ? '·' : '×'}
                        </span>
                        {REGRESSION_LABEL[item.kind]}
                      </span>
                      <code className="mono">{item.requirementId}</code>
                    </p>
                    <p className="reg__summary">{item.summary}</p>
                    <p className="reg__before">이전: {item.before}</p>
                    <p className="reg__after">현재: {item.after}</p>
                  </li>
                ))}
              </ul>
            )}
          </div>
        )}
      </div>

      <div className="inspector__actions">
        <button type="button" className="btn btn--secondary" onClick={onRevalidate} disabled={revalidating}>
          {revalidating ? '재검증 시작 중…' : '재검증'}
        </button>
        <button type="button" className="btn btn--primary" onClick={onExport} disabled={!run.ready}>
          내보내기
        </button>
        {!run.ready && <p className="hint">{run.blockedReason ?? 'Ready 상태에서만 내보낼 수 있습니다.'}</p>}
      </div>
    </div>
  )
}
