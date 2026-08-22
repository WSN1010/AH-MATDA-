# TRD - 아주르 Document Automation Agent

## 1. Technical Scope
본 문서는 아주르 에이전트의 기술 구조를 정의한다. 요구 조건:

- Microsoft Agent Framework 필수 사용
- Aspire를 활용한 Azure 배포 필수
- Copilot SDK 사용
- IChatClient 사용 권장

## 2. Proposed Architecture

### 2.1 Components
- **Web/API Frontend**: 사용자 입력 수집, 생성 요청/조회
- **Agent Orchestrator (Microsoft Agent Framework)**:
  - 단계 실행: IDEATION -> PRD -> TRD
  - 실패 재시도, 단계 상태 관리
- **Model Adapter Layer (Copilot SDK + IChatClient)**:
  - 모델별 설정 주입
  - 공통 요청/응답 인터페이스 제공
- **Storage**:
  - 문서 본문: Azure Blob Storage
  - 메타데이터/이력: Azure Table 또는 Cosmos DB
- **Observability**:
  - Application Insights + OpenTelemetry

### 2.2 Data Flow
1. Client가 생성 요청을 전송한다.
2. Orchestrator가 입력 검증 후 작업 ID를 발급한다.
3. IDEATION 생성 -> 결과 저장.
4. IDEATION 컨텍스트로 PRD 생성 -> 저장.
5. PRD/IDEATION 컨텍스트로 TRD 생성 -> 저장.
6. 완료 이벤트와 비교 요약을 반환한다.

## 3. Suggested Repository Structure
```text
src/
  AppHost/                     # .NET Aspire AppHost
  Ajure.Api/                   # Minimal API or ASP.NET Core API
  Ajure.Agent/                 # Microsoft Agent Framework orchestration
  Ajure.AI/                    # Copilot SDK + IChatClient adapters
  Ajure.Domain/                # DTOs, entities, interfaces
  Ajure.Infrastructure/        # Blob/DB/telemetry implementations
  Ajure.Worker/                # Background generation worker (optional)
docs/
  IDEATION.md
  PRD.md
  TRD.md
```

## 4. Key Interfaces

### 4.1 Generation Contract
- `GenerateDocumentsCommand`
  - `IdeaInput`
  - `SelectedModels[]`
  - `TemplateVersion`

### 4.2 AI Client Contract
- `IChatClient` 기반 메서드:
  - `GenerateIdeationAsync(...)`
  - `GeneratePrdAsync(...)`
  - `GenerateTrdAsync(...)`

### 4.3 Persistence Contract
- `IDocumentRepository`
  - `SaveAsync(documentType, model, content, metadata)`
  - `GetByJobAsync(jobId)`

## 5. Azure Deployment Design

### 5.1 Services
- Azure Container Apps: API/Worker 호스팅
- Azure Container Registry: 이미지 저장
- Azure OpenAI (또는 호환 모델 엔드포인트)
- Azure Blob Storage / Cosmos DB
- Azure Key Vault
- Application Insights

### 5.2 Deployment Path
1. Aspire AppHost 로컬 실행으로 서비스 구성 확인
2. Aspire 배포 설정(환경/리소스) 확정
3. Container image build/push
4. Azure Container Apps 배포
5. 환경변수/시크릿(Key Vault 참조) 설정
6. 헬스체크/로그 검증

## 6. Reliability & Error Handling
- 단계별 실패를 명시적으로 기록하고 job 상태를 `FailedStep`으로 노출
- 재시도는 모델 호출 실패에 한정(지수 백오프, 최대 2회)
- 영구 실패 시 부분 산출물과 실패 원인을 함께 반환

## 7. Security
- 민감 정보는 Key Vault 또는 비밀 환경변수 사용
- 사용자 입력/모델 출력은 감사 로깅 시 마스킹 정책 적용
- 최소 권한 기반의 Managed Identity 사용

## 8. Observability
- 요청 단위 Correlation ID
- 단계별 latency, token usage, success/failure 비율 측정
- 대시보드: 생성 시간, 재시도율, 모델별 품질 점수

## 9. MVP Delivery Checklist
1. 단일 요청으로 IDEATION/PRD/TRD 생성 완료
2. 모델 프로파일 적용으로 결과 차별화 확인
3. Azure 상에서 API/Worker 정상 기동
4. 장애 시 실패 단계와 원인이 추적 가능
