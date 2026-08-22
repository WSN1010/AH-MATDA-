# Common Document Specification

## 1. 목적

이 문서는 아주르가 모든 프로젝트에서 생성하는 `IDEATION.md`, `PRD.md`, `TRD.md`의 필수 구조와 품질 계약을 정의한다.

세 파일은 서로 다른 역할을 가진다.

- `IDEATION.md`: **왜 이 제품이며 어떤 선택을 했는가**
- `PRD.md`: **사용자에게 무엇이 어떻게 동작해야 하는가**
- `TRD.md`: **그 동작을 어떤 기술 계약으로 구현해야 하는가**

같은 내용을 세 파일에 반복하지 않는다. 문서 간 연결은 안정적인 ID와 링크로 표현한다.

## 2. 공통 규칙

### 2.1 문서 제어 정보

각 파일 첫 부분에 다음 표를 포함한다.

| 필드 | 설명 |
|---|---|
| Project | 프로젝트 표시명 |
| Spec Version | 모든 산출물에 공통인 불변 버전 |
| Status | Draft / Validating / Ready / Superseded |
| Targets | 선택한 대상 코딩 에이전트 |
| Generated At | ISO 8601 시각 |
| Source | 아주르 ProjectSpec |

내부 hash, Blob URL, 토큰, 사용자 식별자는 본문에 넣지 않는다.

### 2.2 ID 규칙

| 종류 | 형식 |
|---|---|
| Goal | `GOAL-001` |
| Persona | `P-001` |
| Journey | `J-001` |
| Functional Requirement | `FR-001` |
| Non-functional Requirement | `NFR-001` |
| Acceptance Criterion | `AC-001` |
| Technical Decision | `TD-001` |
| UX Decision | `UX-001` |
| Risk | `RISK-001` |

- ID는 Spec Version 사이에서 의미가 유지되는 동안 바뀌지 않는다.
- 항목을 합치거나 나누면 변경 이벤트를 남긴다.
- ID를 순서 정렬용으로 재할당하지 않는다.

### 2.3 표현 규칙

- 요구사항은 `해야 한다` 수준의 검증 가능한 문장으로 쓴다.
- Must/Should/Could 또는 동등한 우선순위를 명시한다.
- “빠르게”, “직관적으로”, “안전하게”, “예쁘게”는 수치나 관찰 조건 없이 단독 사용하지 않는다.
- 결정되지 않은 내용을 사실처럼 쓰지 않는다.
- 추천 기본값을 적용한 경우 Assumption 또는 Decision으로 남긴다.
- 동일 용어는 Glossary 정의를 따른다.
- 제품 구현 소스 코드를 생성하지 않는다.
- API 계약, 데이터 필드, 상태도, 디렉터리 구조, 명령 이름은 명세에 필요한 범위에서 허용한다.

### 2.4 Ready 문서 규칙

Ready 문서에는 다음이 없어야 한다.

- 미해결 Critical Decision
- `TBD`, `나중에 결정`, `적절한 방식`처럼 구현을 막는 placeholder
- 서로 충돌하는 사용자 행동
- 연결되지 않은 Must 요구사항
- 검증 방법이 없는 수용 기준
- 승인되지 않은 Scope 확장

## 3. `IDEATION.md` 규격

### 목적

제품의 배경과 의사결정을 보존한다. 코딩 에이전트가 기능 목록만 보고 잘못된 방향으로 최적화하지 않도록 “왜”와 범위를 제공한다.

### 필수 섹션

1. **One-line Concept**
   - 한 문장 제품 정의
   - 코드 생성기/명세 생성기 등 제품 유형

2. **Problem**
   - 현재 사용자의 불편
   - 기존 해결 방식의 한계
   - 문제를 해결하지 않을 때의 비용

3. **Target Users and JTBD**
   - Persona ID
   - 상황, 동기, 기대 결과
   - Primary/Secondary 구분

4. **Evidence and Assumptions**
   - 확인된 사실과 검증되지 않은 가설 분리
   - 가설의 검증 방법

5. **Options Considered**
   - 최소 2개의 가능한 접근
   - 선택한 접근과 기각 이유
   - 선택이 이미 명확한 경우 “대안 검토 불필요” 근거

6. **Value Proposition**
   - 대상별 핵심 가치
   - 기존 대안 대비 차이

7. **Scope**
   - MVP Must
   - Should/Could
   - 명시적 Non-goals

8. **Risks**
   - Risk ID, 가능성, 영향, 완화책

9. **Success Definition**
   - 사용자 결과 지표
   - 제품/운영 지표

10. **Locked Decisions**
    - 구현 에이전트가 임의로 바꿔서는 안 되는 제품 결정

### 금지

- PRD의 전체 기능 요구사항 복사
- TRD의 프레임워크/데이터 구조 상세 복사
- 근거 없는 시장 규모 수치
- 해결책을 문제처럼 서술

### 문서별 게이트

- Problem -> Persona/JTBD 연결
- Scope와 Non-goal의 경계가 상호 배타적
- 모든 Risk에 완화 또는 수용 결정
- Locked Decision이 PRD/TRD와 충돌하지 않음

## 4. `PRD.md` 규격

### 목적

코딩 에이전트와 검증자가 사용자 관점에서 구현 완료 여부를 판단할 수 있는 제품 계약을 제공한다.

### 필수 섹션

1. **Product Overview**
   - 제품 정의
   - 현재 Release 범위

2. **Goals and Non-goals**
   - Goal ID
   - 성공 지표
   - 제외 범위

3. **Personas**
   - Persona ID
   - 목표, 사용 환경, 제약

4. **User Journeys**
   - Journey ID
   - 진입 조건
   - 단계
   - 성공 종료
   - 실패/중단 경로

5. **Functional Requirements**
   - FR ID
   - 우선순위
   - 요구사항
   - 근거
   - 연결 Journey/AC

6. **State Matrix**
   - 화면/기능별 Loading, Empty, Error, Success, Disabled, Permission 상태
   - 해당하지 않으면 이유 명시

7. **Non-functional Requirements**
   - NFR ID
   - 측정값 또는 검증 방법
   - 성능, 접근성, 보안, 개인정보, 신뢰성

8. **Business Rules**
   - 계산, 상태 전이, 권한, 제한
   - 서로 겹치는 규칙의 우선순위

9. **Analytics**
   - 이벤트명
   - 필요한 비민감 속성
   - 측정 목적

10. **Acceptance Criteria**
    - AC ID
    - Given/When/Then 또는 동등한 구조
    - 자동/수동/API/UI 등 검증 유형
    - 연결 Requirement ID

11. **Traceability Matrix**
    - 모든 FR/NFR -> AC
    - 모든 Journey -> FR

12. **Release Scope**
    - MVP
    - 후속 범위
    - 출시 차단 조건

### 요구사항 형식

```text
FR-001 [Must] 요구사항 제목
Statement: 사용자는 ...
Rationale: ...
Journeys: J-001
Acceptance: AC-001, AC-002
```

형식은 Markdown으로 보기 좋게 렌더링할 수 있지만 필드의 의미는 유지한다.

### 문서별 게이트

- 모든 FR/NFR에 AC 연결
- 모든 Must Journey에 성공/실패 경로
- UI 기능에 필수 상태 정의
- Non-goal이 FR에 포함되지 않음
- 수용 기준이 구현 방법이 아니라 사용자 관찰 결과를 우선함

## 5. `TRD.md` 규격

### 목적

PRD의 제품 계약을 구현하기 위한 기술 선택과 검증 방법을 고정한다. 코딩 에이전트가 중요한 스택/구조를 다시 선택하지 않도록 한다.

### 필수 섹션

1. **Technical Scope and Constraints**
   - 런타임/플랫폼/배포 제약
   - Must 기술
   - 금지된 선택

2. **Architecture**
   - 논리 다이어그램
   - 요청/이벤트/데이터 흐름
   - 신뢰 경계

3. **Components**
   - 컴포넌트명
   - 책임
   - 의존성
   - 연결 FR/NFR

4. **Repository Structure**
   - 구현 시 생성할 주요 프로젝트/폴더
   - 각 영역의 소유 책임

5. **Domain and Data**
   - 핵심 엔터티
   - 필드/관계/불변 조건
   - 저장/보존/삭제

6. **API and Integration Contracts**
   - Endpoint 또는 메시지
   - 입력/출력
   - 인증/권한
   - 오류
   - 제한 시간/재시도/멱등성

7. **State and Workflow**
   - 상태 목록
   - 허용 전이
   - 실패/취소/복구

8. **Security and Privacy**
   - 인증/인가
   - 비밀 관리
   - 입력/출력 검증
   - 개인정보/감사

9. **Reliability**
   - 오류 분류
   - 재시도 가능한 조건
   - 부분 실패
   - 복구/중복 실행

10. **Observability**
    - Trace, Metric, Log
    - Correlation ID
    - 기록하면 안 되는 데이터

11. **Deployment**
    - 환경
    - 인프라
    - 설정/비밀
    - 배포와 rollback
    - Health/Smoke 검증

12. **Testing Strategy**
    - Unit, Contract, Integration, E2E
    - 요구사항별 검증 레벨
    - 외부 연동 테스트

13. **Technical Decisions**
    - TD ID
    - 결정/근거/대안/영향
    - 연결 FR/NFR

14. **Technical Traceability**
    - FR/NFR -> Component/TD/Test

15. **Known Risks and Implementation Order**
    - 기술 Spike
    - 위험 선행 검증
    - 의존성 순서

### API 계약 최소 필드

```text
Operation
Purpose
Auth
Request
Success Response
Error Responses
Idempotency
Timeout/Retry
Requirement IDs
```

### 문서별 게이트

- 모든 컴포넌트가 Requirement에 근거함
- 모든 Must Requirement가 구현 컴포넌트/기술 결정/Test에 연결됨
- 외부 연동마다 인증, 실패, 제한, timeout 처리
- 데이터마다 소유권, 보존, 삭제 정의
- 배포 가능한 단위와 Health Check 정의
- 프레임워크 이름만 나열하지 않고 사용 경계 정의

## 6. 문서 간 우선순위

문서 내용이 충돌하면 자동으로 한쪽을 선택하지 않는다. 다음 원칙으로 Finding을 만든다.

- 사용자 행동 충돌: PRD를 제품 의도 후보로 보고 사용자 결정 요청
- 구현 방식 충돌: TRD의 Locked Decision 여부 확인
- 배경과 기능 충돌: IDEATION의 Scope/Non-goal을 기준으로 누락 또는 Scope Leak 조사
- 대상 지침 파일 충돌: 공통 문서가 우선하며 지침 파일을 다시 렌더링

Ready 상태에서는 충돌 Finding이 0이어야 한다.

## 7. 길이와 컨텍스트 예산

길이를 점수 목표로 삼지는 않지만 불필요한 중복을 막기 위해 기본 예산을 둔다.

| 파일 | 권장 범위 |
|---|---:|
| `IDEATION.md` | 100~220줄 |
| `PRD.md` | 180~450줄 |
| `TRD.md` | 220~550줄 |
| 대상 지침 파일 | 80~200줄 |

범위를 넘으면 실패가 아니라 중복/분할 검토 Finding을 만든다. 핵심 계약을 줄여 길이를 맞추면 안 된다.

## 8. 렌더링과 편집

- 렌더러는 ProjectSpec에서 결정적으로 섹션 순서와 ID를 만든다.
- LLM은 각 섹션의 의미 내용을 작성하지만 파일 경로, ID, 링크는 렌더러가 확정한다.
- 사용자가 Markdown을 편집하면 변경 내용을 바로 덮어쓰지 않고 ProjectSpec 변경 제안으로 파싱한다.
- 파싱할 수 없는 자유 텍스트는 사용자 원문을 보존한 채 Decision으로 전환한다.
- 새 ProjectSpec 승인 후 세 공통 문서와 대상 지침 파일을 다시 렌더링한다.

## 9. 공통 수용 기준

1. 세 문서는 같은 Spec Version을 표시한다.
2. 같은 ID는 세 문서에서 같은 의미를 가진다.
3. PRD의 모든 FR/NFR은 AC와 연결된다.
4. PRD의 모든 Must FR/NFR은 TRD의 Component, TD 또는 Test와 연결된다.
5. IDEATION의 Non-goal은 PRD Scope나 대상 지침 파일에서 구현 대상으로 나타나지 않는다.
6. 문서에 미해결 Critical placeholder가 있으면 Ready가 아니다.
7. 문서 내용 변경 후 영향받은 다른 파일은 Stale이 된다.
8. 세 문서에는 제품 구현 소스 코드나 실제 비밀이 포함되지 않는다.
