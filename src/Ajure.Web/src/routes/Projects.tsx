import { Link } from 'react-router-dom'
import { AppShell } from '../components/AppShell'
import { EmptyState, ErrorState, Skeleton, StatusBadge, formatDateTime } from '../components/ui'
import { listProjects } from '../api/client'
import { targetName } from '../api/targets'
import { useAsync } from '../lib/useAsync'
import type { ProjectSummary } from '../api/types'

function destination(project: ProjectSummary): string {
  if (project.artifactCount > 0) return `/projects/${project.id}/workspace`
  return `/projects/${project.id}/decisions`
}

function ProjectRow({ project }: { project: ProjectSummary }) {
  const score = project.readinessScore
  return (
    <li className="plist__item">
      <div className="plist__main">
        <h2 className="plist__name">
          <Link to={destination(project)}>{project.name}</Link>
        </h2>
        <p className="plist__meta">
          <StatusBadge status={project.status} />
          <span className="mono">v{project.specVersionNumber}</span>
          <span className="mono">{formatDateTime(project.updatedAt)} 수정</span>
        </p>
        <p className="plist__targets">
          {project.targetIds.length > 0
            ? project.targetIds.map(targetName).join(' · ')
            : '선택한 대상 코딩 에이전트가 없습니다.'}
        </p>
      </div>

      <div className="plist__score">
        {score === null ? (
          <p className="plist__scoreEmpty">아직 검증하지 않았습니다.</p>
        ) : (
          <>
            <p className="plist__scoreValue">
              <span className="mono">{score}</span>
              <span className="plist__scoreMax mono">/100</span>
            </p>
            <div
              className={`meter meter--${score >= 90 ? 'pass' : 'warn'}`}
              role="meter"
              aria-valuenow={score}
              aria-valuemin={0}
              aria-valuemax={100}
              aria-label={`One-Shot Readiness Score ${score}점`}
            >
              <span style={{ inlineSize: `${score}%` }} />
            </div>
          </>
        )}
        {project.status === 'Ready' ? (
          <p className="plist__next">
            파일 {project.artifactCount}개 준비됨 ·{' '}
            <Link to={`/projects/${project.id}/workspace#export`}>ZIP 내보내기</Link>
          </p>
        ) : project.openCriticalDecisions > 0 ? (
          <p className="plist__next">
            Critical 결정 {project.openCriticalDecisions}건 남음 ·{' '}
            <Link to={`/projects/${project.id}/decisions`}>결정 이어가기</Link>
          </p>
        ) : (
          <p className="plist__next">
            <Link to={destination(project)}>워크벤치 열기</Link>
          </p>
        )}
      </div>
    </li>
  )
}

export function Projects() {
  const projects = useAsync(() => listProjects(), [])

  return (
    <AppShell title="프로젝트">
      <div className="page">
        <header className="page__head">
          <div>
            <p className="eyebrow mono">Projects</p>
            <h1 className="page__title">프로젝트</h1>
            <p className="page__lead">각 프로젝트는 하나의 명세 버전과 하나의 구현 번들로 이어집니다.</p>
          </div>
          <Link className="btn btn--primary" to="/projects/new">
            새 명세 만들기
          </Link>
        </header>

        {projects.status === 'loading' && <Skeleton rows={6} label="프로젝트 목록을 불러오는 중" />}

        {projects.status === 'error' && projects.error && (
          <ErrorState error={projects.error} onRetry={projects.reload} preserved="저장된 프로젝트는 삭제되지 않았습니다." />
        )}

        {projects.status === 'ready' && projects.data && projects.data.length === 0 && (
          <EmptyState
            title="아직 명세가 없습니다."
            description="첫 아이디어를 구조화해 보세요. 이름과 한 문단이면 결정 질문을 만들 수 있습니다."
            action={
              <Link className="btn btn--primary" to="/projects/new">
                첫 명세 만들기
              </Link>
            }
          />
        )}

        {projects.status === 'ready' && projects.data && projects.data.length > 0 && (
          <ul className="plist">
            {projects.data.map((project) => (
              <ProjectRow key={project.id} project={project} />
            ))}
          </ul>
        )}
      </div>
    </AppShell>
  )
}
