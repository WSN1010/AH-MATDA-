const DOCS = [
  { name: 'IDEATION.md', meta: '문제 · 범위 · 잠긴 결정' },
  { name: 'PRD.md', meta: '요구사항 · 수용 기준' },
  { name: 'TRD.md', meta: '구조 · 계약 · 배포' },
  { name: 'CLAUDE.md', meta: '대상 도구 실행 지침' },
]

/**
 * 랜딩 시그니처. 아이디어 메모가 검증 루프를 지나 네 문서로 정리되고,
 * 요구사항 추적선이 하나의 구현 번들로 잠기는 장면을 CSS만으로 표현한다.
 * prefers-reduced-motion에서는 깊이 이동과 빛 이동을 제거한다.
 */
export function TraceScene() {
  return (
    <div
      className="scene"
      role="img"
      aria-label="아이디어 메모가 아주르 검증 루프를 지나 IDEATION.md, PRD.md, TRD.md, CLAUDE.md 네 파일로 정리되고, FR-004에서 AC-009와 TD-003으로 이어지는 추적선이 하나의 구현 번들 ZIP으로 모이는 그림"
    >
      <div className="scene__stage">
        <span className="scene__beam" aria-hidden="true" />

        <div className="scene__memo">
          <span className="scene__memoLabel mono">idea.txt</span>
          <p className="scene__memoText">
            팀 회고에서 나온 실행 항목을 놓치지 않게 관리하고 싶어요. 로그인은 GitHub, 데이터는 90일만 보관.
          </p>
        </div>

        <div className="scene__loop">
          <span className="scene__loopLine" aria-hidden="true" />
          <span className="scene__loopLabel mono">아주르 검증 루프</span>
          <span className="scene__loopLine" aria-hidden="true" />
        </div>

        <ul className="scene__docs">
          {DOCS.map((doc, index) => (
            <li key={doc.name} className="scene__doc" data-depth={index}>
              <span className="scene__docRules" aria-hidden="true" />
              <span className="scene__docName mono">{doc.name}</span>
              <span className="scene__docMeta">{doc.meta}</span>
            </li>
          ))}
        </ul>

        <p className="scene__trace mono">
          <span className="scene__node">FR-004</span>
          <span className="scene__link" aria-hidden="true" />
          <span className="scene__node">AC-009</span>
          <span className="scene__link" aria-hidden="true" />
          <span className="scene__node">TD-003</span>
        </p>

        <svg className="scene__converge" viewBox="0 0 120 28" preserveAspectRatio="none" aria-hidden="true">
          <path d="M15 0 C15 18 60 12 60 28" />
          <path d="M45 0 C45 18 60 14 60 28" />
          <path d="M75 0 C75 18 60 14 60 28" />
          <path d="M105 0 C105 18 60 12 60 28" />
        </svg>

        <div className="scene__bundle">
          <span className="scene__lock" aria-hidden="true">
            ▣
          </span>
          <span className="scene__bundleName mono">implementation-bundle.zip</span>
          <span className="scene__bundleMeta">Hard Gate 13/13 · Ready 92/100</span>
        </div>
      </div>
    </div>
  )
}
