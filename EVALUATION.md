# Evaluation and Regression Specification

## 1. 목적

아주르의 품질 목표는 “문장이 좋아 보이는 문서”가 아니다. 선택한 코딩 에이전트가 추가 기획 대화 없이 한 번의 구현 요청으로 요구사항을 최대한 정확히 충족하도록 만드는 것이다.

이를 위해 서로 다른 두 지표를 구분한다.

1. **One-Shot Readiness Score**: 현재 명세 번들이 구현 입력으로 준비됐는지 제품 안에서 측정하는 선행 지표
2. **Intent Fidelity**: 외부 코딩 에이전트가 실제 첫 구현에서 충족한 수용 기준 비율을 측정하는 결과 지표

Readiness 90점은 Intent Fidelity 90%를 보장하지 않는다. 벤치마크를 통해 두 지표의 상관을 지속적으로 보정한다.

## 2. Ready 판정

```text
Ready =
  One-Shot Readiness Score >= 90
  AND 모든 Hard Gate 통과
  AND 미해결 Critical Decision = 0
  AND 최신 회귀 검사 통과
  AND 모든 Artifact가 동일 Spec Version
  AND 서로 다른 모델 ID의 성공한 의미 평가 >= 2
```

점수가 100점이어도 Hard Gate가 실패하면 Ready가 아니다.

## 3. One-Shot Readiness Score

| 영역 | 배점 | 핵심 질문 |
|---|---:|---|
| Intent Coverage | 25 | 사용자의 목표, 범위, 비목표가 빠짐없이 고정됐는가 |
| Traceability | 20 | 요구사항-수용 기준-기술 결정이 연결됐는가 |
| Testability | 20 | 결과를 관찰하고 합격/실패로 판정할 수 있는가 |
| Technical Executability | 15 | 구현 선택, 인터페이스, 오류, 배포가 충분히 닫혔는가 |
| Target-Agent Fitness | 10 | 선택 도구가 읽는 정확한 파일과 실행 계약인가 |
| UX and Operations Completeness | 10 | UI 상태, 접근성, 보안, 관측성, 운영 실패가 포함됐는가 |
| **합계** | **100** | |

### 3.1 Intent Coverage - 25점

| 조건 | 점수 |
|---|---:|
| 문제, 대상 사용자, 핵심 가치가 구체적 | 5 |
| Must/Should/Non-goal이 분리됨 | 5 |
| 모든 핵심 사용자 여정이 정의됨 | 5 |
| Critical Decision이 모두 해결됨 | 5 |
| 입력 원문의 핵심 의도와 ProjectSpec의 의미 커버리지 | 5 |

### 3.2 Traceability - 20점

| 조건 | 점수 |
|---|---:|
| 모든 FR/NFR에 수용 기준 연결 | 8 |
| 모든 FR/NFR에 기술 결정 또는 영향 없음 연결 | 5 |
| 사용자 여정과 요구사항 연결 | 3 |
| 리스크와 완화책 연결 | 2 |
| 대상 지침 파일이 최신 문서 참조 | 2 |

FR/NFR -> AC 연결률이 100% 미만이면 이 영역 점수와 무관하게 Hard Gate가 실패한다.

### 3.3 Testability - 20점

| 조건 | 점수 |
|---|---:|
| Given/When/Then 또는 동등한 검증 구조 | 5 |
| 모호한 형용사가 측정 기준으로 치환됨 | 4 |
| 성공뿐 아니라 오류/권한/빈 상태 검증 | 4 |
| 비기능 목표에 수치 또는 검증 방식 존재 | 4 |
| 외부 연동의 실패/제한/타임아웃 검증 | 3 |

### 3.4 Technical Executability - 15점

| 조건 | 점수 |
|---|---:|
| 스택, 런타임, 배포 대상이 결정됨 | 3 |
| 주요 컴포넌트와 책임이 분리됨 | 3 |
| 데이터, API, 상태 전이가 정의됨 | 3 |
| 인증, 보안, 오류 처리 결정이 있음 | 3 |
| 테스트, 관측성, 배포 검증이 있음 | 3 |

### 3.5 Target-Agent Fitness - 10점

| 조건 | 점수 |
|---|---:|
| 네이티브 파일명/경로/문법 정확성 | 3 |
| 공통 문서 읽기 순서와 우선순위 | 2 |
| 범위/비목표/잠긴 결정 보존 | 2 |
| 완료 정의와 검증 루프 | 2 |
| 도구가 지원하지 않는 필수 기능 없음 | 1 |

### 3.6 UX and Operations Completeness - 10점

| 조건 | 점수 |
|---|---:|
| 핵심 화면과 상호작용 | 2 |
| 로딩/빈 상태/오류/성공/비활성 상태 | 2 |
| 모바일/데스크톱 반응형 | 1 |
| 키보드/레이블/대비 등 접근성 | 1 |
| 개인정보와 비밀 관리 | 2 |
| 로그, 지표, 복구 방법 | 2 |

## 4. Hard Gates

| Gate ID | 실패 조건 |
|---|---|
| HG-01 | 미해결 Critical Decision 존재 |
| HG-02 | FR/NFR 중 수용 기준이 없는 항목 존재 |
| HG-03 | PRD와 TRD 사이 Critical 모순 존재 |
| HG-04 | Must 요구사항이 대상 지침 파일에서 누락 또는 약화됨 |
| HG-05 | 공통 문서와 대상 파일의 Spec Version/hash 불일치 |
| HG-06 | 승인 없이 기준 버전의 Must 요구사항 삭제 |
| HG-07 | 수용 기준이 구현 결과로 검증 불가능 |
| HG-08 | 필수 인증/권한/데이터 보호 결정 누락 |
| HG-09 | 대상 파일 경로 또는 필수 문법 오류 |
| HG-10 | 아주르 산출물에 제품 구현 코드 또는 실제 비밀 포함 |
| HG-11 | Repair 3회 후 동일 Critical Finding 반복 |
| HG-12 | 평가자 결과가 상충하고 1회 타이브레이크 후에도 합의되지 않음 |
| HG-13 | 필수 Copilot SDK 작성/평가 단계가 완료되지 않음 |
| HG-14 | 서로 다른 모델 ID의 성공한 의미 평가가 2건 미만이거나 필수 평가 봉투가 유효하지 않음 |

## 5. 검증 단계

### Stage 1 - Input Coverage

- 사용자 원문을 의미 단위 Statement로 분해한다.
- 각 Statement가 Goal, Requirement, Constraint, Non-goal, Decision 또는 Risk에 매핑됐는지 확인한다.
- 매핑되지 않은 핵심 Statement는 누락 Finding으로 만든다.

### Stage 2 - Deterministic Validation

- 스키마
- ID 유일성
- 추적 링크
- [DOCUMENT-SPEC.md](DOCUMENT-SPEC.md)의 문서별 필수 섹션
- 대상 파일 경로/frontmatter
- 버전/hash
- 금지 패턴

이 단계가 실패하면 비싼 AI 평가를 실행하지 않는다.

### Stage 3 - Independent Review

작성 세션과 분리된 평가자가 동일한 ProjectSpec을 독립 검토한다.

- Product Reviewer
- Technical Reviewer
- UX Reviewer

평가 역할은 구성된 모델 풀에 결정적으로 배정하며, 성공한 평가에는 서로 다른 모델 ID가 2개 이상 포함돼야 한다. 역할마다 별도 세션을 사용하고 작성 세션을 재사용하지 않는다.

각 Finding은 다음 구조를 가져야 한다.

```text
id
severity: Critical | Major | Minor
category
ruleKey
statement
evidence[]
affectedIds[]
suggestedResolution
requiresUserDecision
```

근거 없는 점수 차감은 허용하지 않는다.
평가자는 `reviewComplete`, 여섯 영역 점수, Finding 배열을 포함한 봉투로 응답해야 한다. 봉투 누락, 스키마 위반 또는 2회 파싱 실패는 Finding 0건이 아니라 평가 실패로 처리한다.

### Stage 4 - Implementation Simulation

Implementation Simulator는 코드를 생성하지 않고 다음만 만든다.

- 예상 구현 컴포넌트
- 요구사항별 작업 목록
- 예상 파일/모듈 지도
- 의존성 및 순서
- 요구사항별 검증 방법
- 명세만으로 결정할 수 없는 지점

시뮬레이션 중 추가 질문이 필요하면 해당 항목은 모호성 Finding이다. 문서에 답이 있는데 찾지 못했다면 구조/검색성 Finding이다.

### Stage 5 - Regression Validation

기준 버전이 있으면 요구사항 그래프를 비교한다. 상세 규칙은 7장을 따른다.

### Stage 6 - Repair

- 사용자 결정이 필요 없는 Finding만 자동 수정한다.
- 변경 범위는 affected IDs로 제한한다.
- 수정 후 Stage 1부터 전체 검사를 다시 수행한다.
- 최대 3회 반복한다.
- 평가자와 모델 배정은 Validation Run 시작 시 고정하고 Repair 반복 사이에 변경하지 않는다.
- 점수를 올리기 위해 요구사항을 삭제하거나 완화할 수 없다.

### Stage 7 - Ready or Needs Decision

- Ready 조건 충족 시 번들을 잠근다.
- 사용자 결정이 필요하면 선택지, 영향, 추천안을 제공한다.
- 단순 “정보를 더 주세요” 대신 구현 결과가 어떻게 달라지는지 설명한다.

## 6. 평가자 합의와 점수 안정성

### 독립성

- 작성자와 평가자의 세션을 분리한다.
- 작성 모델과 평가 모델은 서로 다른 세션을 사용하고, 평가자 집합에는 서로 다른 모델 ID가 2개 이상 있어야 한다.
- 평가자에게 이전 점수나 목표 점수 90을 노출하지 않는다.
- 평가자는 수정 권한이 없으며 Finding만 반환한다.

### 모델 배정

1. Worker 시작 시 Copilot SDK의 사용 가능 모델과 구성 `Ajure:Review:ModelPool`의 교집합을 구성 순서대로 만든다.
2. 모델이 2개 미만이면 `model_diversity_unavailable` 오류와 HG-14로 실패한다.
3. `Product -> Technical -> UX` 고정 Reviewer 순서에 모델을 라운드 로빈으로 배정한다. Implementation Simulator는 Stage 4에서 별도 세션으로 실행하되 Reviewer 점수 집계에는 포함하지 않는다.
4. 역할마다 독립 세션을 만들며 모델 ID와 세션 ID를 Validation Run에 저장한다.
5. 실패한 역할은 같은 모델에서 한 번만 재시도한다. 이후 성공한 서로 다른 모델이 2개 미만이면 실패한다.

### Finding 정규화

- `ruleKey`는 `missing_ac`, `unverifiable_ac`, `contradiction_prd_trd`, `undefined_term`, `missing_state`, `missing_authz`, `missing_failure_handling`, `unjustified_component`, `scope_leak`, `nongoal_violation`, `ambiguous_metric`, `traceability_break`, `target_file_mismatch`, `security_gap`, `ops_gap`, `other` 중 하나다.
- `affectedIds`는 실제 ProjectSpec에 존재하는 ID만 남기고 정렬한다. 존재하지 않는 ID는 별도 Minor Finding으로 보존한다.
- 근거는 ProjectSpec 필드 또는 렌더 산출물에서 확인되는 경우에만 점수 및 보정 근거로 사용한다.
- 의미 fingerprint는 `ruleKey + 정렬된 affectedIds`의 SHA-256이다. `other`이면서 영향 ID가 없는 Finding은 집계하지 않는다.

### 집계

- 결정적 항목은 코드 결과를 그대로 사용한다.
- 의미 항목은 최소 2개의 독립 평가 결과 중앙값을 사용한다.
- 평가 수가 짝수이면 정렬된 가운데 두 값의 산술 평균을 중앙값으로 사용한다.
- 영역 점수는 소수점 첫째 자리까지 반올림하고 총점도 같은 정밀도를 유지한다.
- 같은 fingerprint 또는 같은 `ruleKey`이면서 영향 ID Jaccard 유사도가 0.5 이상인 Finding을 하나의 cluster로 병합한다.
- cluster의 심각도는 가장 높은 값을 사용하고, 지지 수는 서로 다른 모델 ID 수로 계산한다.
- Critical은 성공 모델의 과반수이면서 최소 2개 모델이 지지해야 Confirmed다. 그 외에는 Disputed로 분류해 자동 보정하지 않는다.
- Major/Minor 단독 Finding은 점수에 반영할 수 있지만 단독으로 Ready를 차단하지 않는다.
- 영역 점수 차이가 3점 이상이거나 Disputed Critical이 있으면 세 번째 사용 가능 모델로 Validation Run당 한 번만 타이브레이크한다. 이후에도 상충하면 HG-12와 Needs Decision이다.
- 심각도는 최대값, 영역 점수는 중앙값을 사용한다. 두 규칙은 의도적으로 다르다.

### 보정 입력

- Confirmed이고, 사용자 결정이 필요 없으며, 검증 가능한 근거가 있는 Finding만 Repair Agent에 전달한다.
- 입력은 심각도 내림차순, 영향 ID, fingerprint 순으로 정렬하고 수정 가능 범위를 영향 ID 합집합으로 제한한다.
- 같은 Critical fingerprint가 3회 연속 Confirmed면 HG-11이다.
- 성공한 서로 다른 평가 모델이 2개 미만이거나 필수 Copilot SDK 단계가 누락되면 점수와 관계없이 Ready가 아니다.

### 안정성 검사

동일 입력을 동일 설정으로 3회 평가했을 때:

- 총점 범위 5점 이내
- Hard Gate 판정 일치율 100%
- Critical Finding 의미 일치율 95% 이상

기준을 벗어나면 해당 평가 프롬프트/모델 조합을 배포하지 않는다.

## 7. 회귀 검증

### 7.1 기준선

- 사용자가 승인하거나 Ready 번들을 내보낸 Spec Version을 기준선으로 사용할 수 있다.
- 기준선은 불변이다.
- 새 변경은 항상 새 후보 버전으로 생성한다.

### 7.2 회귀 유형

| 유형 | 설명 | 기본 심각도 |
|---|---|---|
| Removed | 이전 요구사항이 사라짐 | Must면 Critical |
| Weakened | 조건, 결과, 품질 기준이 약해짐 | Major/Critical |
| Unlinked | Requirement-AC-TD 연결이 끊김 | Critical |
| Contradicted | 새 내용이 기존 결정과 충돌 | Critical |
| ScopeLeak | 승인되지 않은 기능이 추가됨 | Major |
| StateLoss | 오류/빈 상태/권한/반응형 상태가 사라짐 | Major |
| QualityDrop | 영역 점수가 허용 범위 이상 하락 | Major |
| StaleArtifact | 대상 지침 파일이 이전 버전 | Critical |
| FormatRegression | 대상 네이티브 문법/경로가 깨짐 | Critical |

### 7.3 의미 매핑

1. 동일 ID를 먼저 비교한다.
2. ID가 바뀌었으면 의미 유사도와 연결 관계로 후보를 찾는다.
3. 합치기/나누기가 발생하면 변경 이벤트가 있는지 확인한다.
4. 확신할 수 없는 매핑은 자동으로 삭제 승인하지 않고 사용자에게 보여 준다.

### 7.4 불변 조건

- 승인된 Must 요구사항은 승인 없이 삭제할 수 없다.
- FR/NFR -> AC 추적률은 100% 아래로 내려갈 수 없다.
- 보안/개인정보 요구사항은 명시적 승인 없이 약화할 수 없다.
- Non-goal이 새 Scope로 이동하려면 승인해야 한다.
- 대상 지침 파일은 항상 후보 ProjectSpec과 같은 버전이어야 한다.
- Ready 후보의 총점은 기준선보다 5점 이상 하락할 수 없다. 의도적 범위 변경은 예외 사유를 기록한다.

### 7.5 변경 승인

사용자에게 다음을 보여 준다.

- 이전 문장과 새 문장
- 의미 변화 요약
- 영향을 받는 요구사항/수용 기준/기술 결정
- 예상 구현 영향
- 승인, 수정, 되돌리기 선택

승인 기록에는 사용자, 시각, 사유, 영향 ID를 저장한다.

## 8. 외부 원샷 벤치마크

### 8.1 목적

Readiness Score가 실제 구현 품질을 예측하는지 검증한다. 이 과정은 아주르의 사용자 기능과 분리된 평가 환경에서 수행한다.

### 8.2 데이터셋

MVP 출시 전 최소 20개, 안정화 후 50개 이상의 Brief를 유지한다.

- CRUD/SaaS 대시보드
- 실시간 협업
- 콘텐츠/검색
- 외부 API 연동
- 인증/권한
- 파일 업로드
- 결제 또는 민감 데이터
- 반응형 소비자 UI

각 Brief는 다음을 가진다.

- 원본 사용자 아이디어
- 승인된 핵심 결정
- Gold 요구사항/수용 기준
- 금지된 Scope
- 자동 검증과 사람 검토 항목

데이터셋은 Calibration과 Holdout으로 분리한다. Holdout 결과를 보고 프롬프트를 수정한 뒤 같은 결과를 출시 근거로 재사용하지 않는다.

### 8.3 원샷 정의

- 코딩 에이전트에 생성 번들과 하나의 구현 요청을 전달한다.
- 구현 중 에이전트 자체의 파일 탐색, 빌드, 테스트, 오류 수정은 허용한다.
- 사람이 추가 요구사항이나 수정 프롬프트를 보내면 원샷 실패다.
- 시간/토큰/도구 한도는 대상별로 고정하고 기록한다.

### 8.4 Intent Fidelity

```text
Intent Fidelity =
  충족된 수용 기준 가중치 합
  / 전체 수용 기준 가중치 합
  * 100

가중치:
  Must = 2
  Should = 1
```

다음 중 하나면 해당 케이스는 원샷 성공으로 보지 않는다.

- Critical 수용 기준 실패
- Non-goal을 구현해 핵심 동작을 훼손
- 빌드/실행 불가
- 필수 보안 조건 위반
- 핵심 사용자 흐름 완료 불가

### 8.5 제품 목표

- 전체 케이스 Intent Fidelity 중앙값 90% 이상
- 케이스의 80% 이상에서 Critical 실패 0건
- 대상별 중앙값 85% 미만인 어댑터는 Stable로 표시하지 않음
- Readiness Score와 Intent Fidelity의 양의 상관을 분기마다 검토

## 9. 점수 악용 방지

- 요구사항 수를 줄여 추적률을 높이지 않는다.
- 검증하기 어려운 요구사항을 Non-goal로 이동하지 않는다.
- 평가자가 원하는 문구를 반복해 점수를 올리는 것을 중복 검사한다.
- “좋다”, “완전하다” 같은 자기평가 문장을 근거로 인정하지 않는다.
- 사용자의 원본 의도와 Decision 로그를 항상 Coverage 기준으로 유지한다.
- 점수 모델/프롬프트 변경 시 과거 벤치마크를 재실행한다.

## 10. 사용자에게 표시할 Validation Report

### 상단

- 상태: Ready / Needs Decision / Failed
- 총점과 이전 버전 대비 변화
- Hard Gate 통과 수
- 자동 보정 횟수

### 영역별

- 점수
- 통과 근거
- 발견된 Finding
- 관련 Requirement/AC/TD 링크

### 회귀

- 추가, 변경, 삭제, 약화 항목
- 승인 필요 여부
- 대상 파일 Stale 여부

### 내보내기

- 포함 파일
- Spec Version
- 대상 도구
- 마지막 검증 시각

## 11. MVP 종료 조건

1. 모든 점수 계산이 동일 입력에서 재현 가능하다.
2. Hard Gate 단위 테스트가 100% 존재한다.
3. 최소 20개 외부 원샷 벤치마크를 완료한다.
4. Holdout Intent Fidelity 중앙값이 목표에 도달하거나, 미달 사실과 개선 계획을 공개한다.
5. 점수 90 이상이지만 Critical 실패가 발생한 사례를 별도 분석한다.
6. 평가 리포트에서 모든 차감에 근거와 관련 ID가 존재한다.
7. Fake 모드 실행 결과는 종료 조건 판정과 외부 벤치마크 근거에서 제외한다.
