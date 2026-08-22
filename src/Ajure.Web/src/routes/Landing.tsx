import { Link } from 'react-router-dom'
import { AppShell } from '../components/AppShell'
import { TraceScene } from '../components/TraceScene'
import { SUPPORT_LABEL, TARGETS } from '../api/targets'

const LOOP = [
  { id: 'intake', title: '입력 구조화', body: '아이디어 원문에서 요구사항 후보와 제약을 뽑아 결정 질문으로 바꿉니다.' },
  { id: 'authoring', title: '공통 문서 생성', body: '하나의 ProjectSpec에서 IDEATION·PRD·TRD를 파생시켜 문서 간 사실이 어긋나지 않게 합니다.' },
  { id: 'deterministic', title: '결정적 검사', body: 'ID 유일성, FR/NFR → 수용 기준 연결률, 필수 섹션을 규칙으로 확인합니다.' },
  { id: 'review', title: '독립 평가', body: '작성자와 다른 세션의 제품·기술·UX 평가자가 같은 명세를 따로 채점합니다.' },
  { id: 'simulation', title: '구현 시뮬레이션', body: '코드를 쓰지 않고 예상 작업과 파일 지도를 만들어 빠진 결정을 찾습니다.' },
  { id: 'regression', title: '회귀 검사', body: '기준 버전과 비교해 삭제·약화·모순·연결 끊김을 잡아냅니다.' },
  { id: 'render', title: '대상 파일 렌더링', body: '선택한 도구가 자동으로 읽는 네이티브 경로에 지침 파일을 만듭니다.' },
]

export function Landing() {
  return (
    <AppShell title="명세 워크벤치">
      <section className="hero">
        <div className="hero__copy">
          <p className="eyebrow mono">IDEATION.md · PRD.md · TRD.md · 대상 지침 파일</p>
          <h1 className="hero__title">
            코드를 쓰기 전에,
            <br />
            AI가 틀릴 여지를 없앱니다.
          </h1>
          <p className="hero__lead">
            아이디어를 4종 명세로 만들고 회귀 검증한 뒤, 선택한 코딩 에이전트가 자동으로 읽는 파일로 전달합니다.
            아주르는 명세를 만들 뿐 제품 코드를 쓰지 않습니다.
          </p>
          <div className="hero__actions">
            <Link className="btn btn--primary btn--lg" to="/projects/new">
              새 명세 만들기
            </Link>
            <a className="btn btn--secondary btn--lg" href="#validation">
              검증 방식 보기
            </a>
          </div>
          <dl className="hero__facts">
            <div>
              <dt>Ready 판정</dt>
              <dd>
                점수 <span className="mono">90</span>점 이상 + Hard Gate 전부 통과 + 미해결 Critical 결정 0건
              </dd>
            </div>
            <div>
              <dt>전달 형식</dt>
              <dd>
                파일 하나 <span className="mono">implementation-bundle.zip</span>
              </dd>
            </div>
          </dl>
        </div>
        <TraceScene />
      </section>

      <section className="section" id="validation" aria-labelledby="validation-title">
        <header className="section__head">
          <p className="eyebrow mono">검증 방식</p>
          <h2 className="section__title" id="validation-title">
            점수보다 먼저 실패 이유를 보여 줍니다.
          </h2>
          <p className="section__lead">
            생성은 한 번의 요청이 아니라 7단계 루프입니다. 각 단계의 시작 시각, 소요 시간, 발견 수를 그대로 공개합니다.
          </p>
        </header>
        <ol className="loop">
          {LOOP.map((stage, index) => (
            <li className="loop__item" key={stage.id}>
              <span className="loop__index mono" aria-hidden="true">
                {String(index + 1).padStart(2, '0')}
              </span>
              <div>
                <h3 className="loop__title">{stage.title}</h3>
                <p className="loop__body">{stage.body}</p>
              </div>
            </li>
          ))}
        </ol>
        <p className="note">
          Ready는 완벽하다는 뜻이 아닙니다. 아주르가 정의한 Hard Gate 13개를 모두 통과하고 회귀 검사에서 요구사항 손실이
          없는 상태를 뜻합니다.
        </p>
      </section>

      <section className="section" aria-labelledby="targets-title">
        <header className="section__head">
          <p className="eyebrow mono">지원 대상</p>
          <h2 className="section__title" id="targets-title">
            도구가 실제로 읽는 경로에 씁니다.
          </h2>
          <p className="section__lead">
            같은 모델이라도 도구가 다르면 읽는 파일이 다릅니다. 아주르는 도구 단위로 네이티브 경로를 만듭니다.
          </p>
        </header>
        <div className="tablewrap">
          <table className="table">
            <thead>
              <tr>
                <th scope="col">코딩 에이전트</th>
                <th scope="col">생성 파일</th>
                <th scope="col">지원 수준</th>
              </tr>
            </thead>
            <tbody>
              {TARGETS.map((target) => (
                <tr key={target.id}>
                  <th scope="row">{target.name}</th>
                  <td>
                    <code className="mono">{target.path}</code>
                  </td>
                  <td>{SUPPORT_LABEL[target.support]}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>

      <section className="section section--last" aria-labelledby="handoff-title">
        <header className="section__head">
          <p className="eyebrow mono">전달</p>
          <h2 className="section__title" id="handoff-title">
            내려받은 다음은 세 단계입니다.
          </h2>
        </header>
        <ol className="handoff">
          <li>
            <span className="handoff__index mono" aria-hidden="true">
              01
            </span>
            ZIP을 새 프로젝트나 저장소 루트에 풉니다.
          </li>
          <li>
            <span className="handoff__index mono" aria-hidden="true">
              02
            </span>
            선택한 코딩 에이전트를 그 루트에서 실행합니다.
          </li>
          <li>
            <span className="handoff__index mono" aria-hidden="true">
              03
            </span>
            “이 명세를 기준으로 전체 구현하고 검증까지 완료해”라고 한 번만 요청합니다.
          </li>
        </ol>
        <div className="cta">
          <Link className="btn btn--primary btn--lg" to="/projects/new">
            새 명세 만들기
          </Link>
          <Link className="btn btn--ghost btn--lg" to="/projects">
            내 프로젝트 보기
          </Link>
        </div>
      </section>
    </AppShell>
  )
}
