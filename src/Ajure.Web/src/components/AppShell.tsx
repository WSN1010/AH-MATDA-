import { useEffect, useState } from 'react'
import type { ReactNode } from 'react'
import { Link, NavLink } from 'react-router-dom'
import { isMockMode, onModeChange } from '../api/client'

function MockNotice() {
  const [usingMock, setUsingMock] = useState(isMockMode())
  useEffect(() => onModeChange(setUsingMock), [])
  if (!usingMock) return null
  return (
    <p className="mocknote" role="status">
      <span className="mocknote__dot" aria-hidden="true" />
      <span>
        <strong>로컬 목 데이터</strong>로 동작 중입니다. 백엔드에 연결되면 실제 생성·검증 결과로 바뀝니다.
      </span>
    </p>
  )
}

export function AppShell({
  title,
  context,
  children,
}: {
  title: string
  context?: ReactNode
  children: ReactNode
}) {
  useEffect(() => {
    document.title = `${title} · 아주르 AJURE`
  }, [title])

  return (
    <div className="shell">
      <a className="skip" href="#main">
        본문으로 건너뛰기
      </a>
      <header className="topbar">
        <div className="topbar__inner">
          <Link to="/" className="brand">
            <span className="brand__mark" aria-hidden="true">
              <span />
              <span />
              <span />
            </span>
            <span className="brand__text">
              아주르 <span className="brand__latin mono">AJURE</span>
            </span>
          </Link>
          {context && <div className="topbar__context">{context}</div>}
          <nav className="topbar__nav" aria-label="주요 메뉴">
            <NavLink to="/projects" className={({ isActive }) => (isActive ? 'navlink navlink--on' : 'navlink')}>
              프로젝트
            </NavLink>
            <NavLink
              to="/settings/providers"
              className={({ isActive }) => (isActive ? 'navlink navlink--on' : 'navlink')}
            >
              모델 설정
            </NavLink>
          </nav>
        </div>
        <MockNotice />
      </header>
      <main id="main" className="main" tabIndex={-1}>
        {children}
      </main>
      <footer className="footer">
        <p>
          아주르는 <strong>구현 명세</strong>를 만듭니다. 제품 코드를 작성하거나 저장소를 대신 실행하지 않습니다.
        </p>
        <p className="footer__meta mono">IDEATION.md · PRD.md · TRD.md · 대상 지침 파일 → ZIP 1개</p>
      </footer>
    </div>
  )
}
