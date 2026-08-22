# IMPLEMENTATION-BACKEND - 아주르 백엔드 구현 가이드

| 항목 | 값 |
|---|---|
| 담당 | 백엔드 담당자 1인 |
| 범위 | `src/`의 `Ajure.Web`을 제외한 전 프로젝트 + `tests/` |
| 기준 문서 | [TRD.md](TRD.md), [PRD.md](PRD.md), [EVALUATION.md](EVALUATION.md) |
| 공통 규칙 | [AGENTS.md](AGENTS.md) 필수 준수 (ponytail 포함) |

## 1. 시작 전 읽기 순서

1. [AGENTS.md](AGENTS.md) — 작업 규칙
2. [TRD.md](TRD.md) §2~§10 — 아키텍처, MAF 워크플로, Copilot SDK, API, 저장소
3. [PRD.md](PRD.md) §7 — FR-001~FR-016
4. [EVALUATION.md](EVALUATION.md) — 점수 산식, Hard Gate, 회귀 규칙
5. [DOCUMENT-SPEC.md](DOCUMENT-SPEC.md), [AI-FILE-SPEC.md](AI-FILE-SPEC.md) — 렌더링 출력 계약

## 2. 소유 프로젝트

```text
src/
├─ Ajure.AppHost/          Aspire 오케스트레이션 (공유: 프론트 리소스 등록 시 협의)
├─ Ajure.ServiceDefaults/  공통 telemetry/resilience
├─ Ajure.Api/              ASP.NET Core API + SSE
├─ Ajure.Worker/           Queue 소비, MAF 워크플로 실행
├─ Ajure.Agent/            에이전트 역할 정의, 프롬프트, IChatClient 어댑터
├─ Ajure.Specification/    ProjectSpec 모델, ID 규칙, 렌더러
├─ Ajure.Validation/       결정적 검사, 점수, 회귀
└─ Ajure.Infrastructure/   Blob/Table/Queue, Copilot SDK 클라이언트
tests/                     전체
```

## 3. 마일스톤

순서대로 진행한다. 각 마일스톤은 완료 기준(DoD)을 실제 실행으로 확인한 뒤 푸시한다.

### B0. 솔루션 스캐폴드

- .NET 8+ 솔루션, TRD §15 구조대로 프로젝트 생성
- Aspire AppHost + ServiceDefaults 구성, Azurite를 실행 파일 리소스로 등록
- 로컬 Docker, 컨테이너 런타임, `RunAsEmulator()`를 사용하지 않음
- **DoD**: `dotnet build` 성공, `dotnet run --project src/Ajure.AppHost`로 대시보드에 현재 구현된 모든 백엔드 리소스 표시. `Ajure.Web` 등록은 프론트 프로젝트가 준비된 뒤 협의

### B1. P0 Copilot SDK 로컬 실증 게이트 (B4 차단)

TRD §5.4를 개발 호스트에서 수행한다. **이 게이트를 통과하기 전에는 B4를 시작하지 않는다.** Azure 호스팅 검증은 B7이며 이 게이트로 대체하지 않는다.

- 로컬 runtime 시작, 비대화형 인증, `ListModelsAsync`에서 서로 다른 모델 ID 2개 이상 확인
- 서로 다른 모델의 동시 세션 격리와 응답 수신 확인
- 파일 쓰기 요청의 도구 실행 이벤트 0건, 취소/제한 시간/세션 삭제 확인
- **DoD**: 결과를 §9에 기록. 실패 시 B4를 중단하고 원인을 보고하며 다른 제공자로 조용히 대체하지 않음

### B2. ProjectSpec 모델과 저장소

- `ProjectSpec` 엔터티 + 안정 ID 규칙 (TRD §6)
- Blob(문서/스냅샷), Table(프로젝트/버전/Job/JobEvent), Queue(Job) 구현 (TRD §10)
- **DoD**: `Ajure.Specification.Tests` 통과 — ID 안정성, 직렬화 왕복, 버전 불변성

### B3. API 스켈레톤 + Job 파이프라인 + SSE

프론트 통합을 가장 먼저 푸는 단계다. 실제 모델 없이 동작해야 한다.

- TRD §9의 전체 엔드포인트를 스텁으로 구현 (Problem Details 오류 형식 포함)
- Queue → Worker → JobEvent 저장 → API SSE 재생(`Last-Event-ID`, heartbeat 15초) 연결
- **Fake 모드**: `AJURE_FAKE_MODEL=true`면 Worker가 고정 지연과 함께 미리 준비된 단계 이벤트·샘플 문서 4종을 생성한다. 프론트는 이 모드로 전체 흐름을 개발한다.
- Fake 모드는 `Development`에서만 켤 수 있고 생성 버전, Validation Run, Export manifest에 `isSimulated=true`를 기록한다. UI 통합을 위한 simulated Ready/Export는 허용하지만 실제 Ready, 출시 또는 벤치마크 근거로 보고하지 않는다.
- **DoD**: Fake 모드에서 프로젝트 생성 → generate → SSE 진행 → 문서 4종 조회 → export ZIP 다운로드까지 curl로 전 구간 성공

### B4. MAF 워크플로 + IChatClient 어댑터

- TRD §4.1의 에이전트 역할과 §4.2 상태 그래프를 Microsoft Agent Framework로 구현
- Copilot SDK → `IChatClient` 어댑터 (`Ajure.Infrastructure`), 도구 허용 목록 제한 (TRD §5.2)
- Reviewer 역할을 구성된 모델 풀에 결정적으로 배정하고 서로 다른 모델 ID 2개 이상의 독립 세션을 사용
- Spec Architect와 Reviewer 세션 분리, Copilot SDK 필수 단계 실패 시 Job 명시적 실패 (FR-016)
- **DoD**: 실제 모델로 아이디어 1건 입력 → ProjectSpec 초안 생성, 어댑터 계약 테스트 통과

### B5. 검증 엔진 + 회귀

- 결정적 검사: 추적성, ID, 링크, 필수 섹션, 금지 패턴 (TRD §8.1)
- 독립 평가 + 점수 산식 + Hard Gate (EVALUATION §3~§4)
- Finding 정규화/fingerprint/결정적 집계, HG-14, 타이브레이크 1회 제한
- 회귀: 기준선 스냅샷, 의미 매핑, 삭제/약화 탐지 (EVALUATION §7)
- Repair 루프 최대 3회 (FR-008)
- **DoD**: `Ajure.Validation.Tests` 통과 — 점수 산식, 게이트, 회귀 감지 케이스

### B6. 렌더링 + ZIP Export

**제품의 최종 산출물은 ZIP 파일 1개다.** 여러 md 파일이 나오므로 반드시 ZIP으로 묶는다 (FR-013).

- ProjectSpec → `IDEATION.md`, `PRD.md`, `TRD.md` 렌더링 (DOCUMENT-SPEC 준수)
- 대상 도구별 네이티브 파일 렌더링 (AI-FILE-SPEC §3 경로 매트릭스 준수)
- ZIP 구성 규칙:
  - Ready 상태에서만 생성
  - 기본 포함: 공통 문서 3종 + 선택 대상의 지침 파일
  - `.cursor/rules/ajure.mdc`처럼 경로가 있는 파일은 ZIP 내부에 디렉터리 그대로 배치
  - 평가 리포트는 옵션 체크 시에만 포함
  - 파일별 SHA-256 해시를 버전 레코드에 저장
  - Export manifest에 평가 모델 ID와 `isSimulated`를 기록
- **DoD**: 통합 테스트 — 다중 대상(예: Claude+Cursor) 선택 시 ZIP 내부 경로/해시 검증 통과

### B7. Azure 호스팅 게이트 및 배포 (유예)

- 현재 백엔드 B0~B6 구현 범위에 포함하지 않으며 수행 전에는 통과로 표시하지 않음
- TRD §5.5의 Copilot SDK Azure 호스팅, GitHub App installation token, 동시성/메모리/정책 검증
- `azd init` → `azure.yaml`, Container Apps + Storage + Key Vault + App Insights (TRD §14)
- Copilot SDK 자격증명은 Key Vault 기반으로 runtime에 주입
- **DoD**: `azd up` 성공, 배포된 URL에서 Fake 모드 E2E 1회 + 실제 모델 E2E 1회 성공

## 4. API 계약 관리

- TRD §9가 유일한 계약이다. 프론트는 이 표만 보고 개발한다.
- 계약 변경이 필요하면 **코드보다 먼저 TRD §9를 수정**하고 프론트 담당자에게 알린 뒤 구현한다.
- SSE 이벤트 페이로드 스키마는 B3 완료 시 `docs/contracts/sse-events.md`로 추출해 공유한다.

## 5. 로컬 실행

```bash
dotnet run --project src/Ajure.AppHost        # 전체 (Azurite 포함)
dotnet test                                    # 전체 테스트
```

```powershell
$env:AJURE_FAKE_MODEL="true"
dotnet run --project src/Ajure.AppHost          # 모델 없이
```

## 6. 검증 의무

푸시 전 최소한 다음을 실행하고 통과를 확인한다.

1. `dotnet build` (경고를 오류로 취급)
2. 변경 영역의 `dotnet test`
3. B3 이후에는 Fake 모드 E2E 경로(생성→SSE→export) 수동 1회

## 7. 금지 사항

- Copilot SDK 실패를 다른 제공자로 조용히 대체 (FR-016 위반)
- 모델 세션에 파일 쓰기/셸/Git 도구 노출 (TD-003 위반)
- 로컬 Docker/컨테이너 런타임 또는 `RunAsEmulator()` 사용
- B7 수행 전에 Azure 배포나 호스팅 검증을 통과했다고 보고
- Fake 모드 결과를 실제 Ready 근거로 보고
- 프론트(`src/Ajure.Web`) 파일 수정 — 필요하면 프론트 담당자에게 요청
- 계약(TRD §9) 무단 변경

## 8. 완료 정의

- [ ] B0~B6 전체 DoD 통과
- [ ] AC-001~AC-022 중 백엔드 책임 항목 검증 가능
- [ ] B7은 별도 Azure 호스팅/배포 게이트로 남아 있으며 미수행 상태가 명확함

## 9. 스파이크 기록 (B1 수행 후 작성)

2026-08-22 개발 호스트에서 `GitHub.Copilot.SDK 1.0.9`와
`Microsoft.Agents.AI.GitHub.Copilot 1.18.0`으로 B1을 수행했다.

- SDK 번들 runtime을 `CopilotClientMode.Empty`로 시작했고 로그인된 로컬 사용자 자격증명을 비대화형으로 사용했다.
- `ListModelsAsync`는 `claude-opus-5`, `gpt-5.6-sol`을 포함한 서로 다른 모델 ID 30개를 반환했다.
- 구성 모델 풀의 `claude-opus-5`와 `gpt-5.6-sol`에 독립 세션을 동시에 만들었고 두 세션 모두 `AJURE_PROBE_OK`를 반환했다.
- 두 세션 ID는 서로 달랐고 `AvailableTools=[]`, 사용자 도구 없음, 모든 권한 요청 거부 상태에서 도구 실행 이벤트는 0건이었다.
- 이미 취소된 토큰은 `OperationCanceledException`, 1ms 제한 시간은 `TimeoutException`으로 관찰됐고 실행 중 요청을 중단했다.
- 모든 probe 세션을 dispose한 뒤 `DeleteSessionAsync`로 영구 삭제했으며 client도 정상 종료했다.
- 로컬 B1은 Docker를 사용하지 않았다. Azure 호스팅, GitHub App installation token, 라이선스, 비용/쿼터 검증은 B7 미수행 상태다.
