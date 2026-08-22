# MATDA-TONE Agent Ideation

## 1. Problem
대회 준비 단계에서 아이디어를 문서화할 때, 모델별 실험 흔적이 흩어지고 PRD/TRD 품질 편차가 커져 반복 작업이 많아진다.

## 2. Core Idea
사용자가 주제/제약/대상 모델을 입력하면 에이전트가 아래 문서를 자동 생성한다.

1. IDEATION.md (아이디어 확장 + 후보안 비교)
2. PRD.md (제품 요구사항)
3. TRD.md (기술 요구사항/아키텍처)

생성 시 **모델별 프로파일**(강점, 톤, 문서 스타일)을 적용해 결과를 차별화한다.

## 3. Why This Is Compelling
- 단일 입력으로 기획 산출물 일괄 생성
- 팀 내 문서 형식 표준화
- 모델별 결과 비교를 통한 품질 개선
- 해커톤/대회 초기 속도 극대화

## 4. Target Users
- 해커톤 참가자
- PM/PO 역할을 겸하는 개발자
- MVP를 빠르게 시작해야 하는 소규모 팀

## 5. Candidate Feature Set

### Must-have (MVP)
- 주제 입력 폼(문제, 타겟 사용자, 성공 지표)
- 모델 선택(단일/복수)
- IDEATION/PRD/TRD 자동 생성
- 생성 이력 저장 및 버전 비교

### Should-have
- 템플릿 커스터마이징(조직 문서 양식)
- PRD -> TRD 추적 링크(요구사항 ID 매핑)

### Could-have
- GitHub PR 코멘트용 요약 자동 생성
- Azure OpenAI/다른 모델 공급자 A/B 생성 비교 리포트

## 6. Competition Alignment
- **Microsoft Agent Framework**: 멀티스텝 문서 생성 워크플로 오케스트레이션
- **Copilot SDK + IChatClient**: 모델 호출 인터페이스 표준화
- **Aspire**: 로컬 개발/관측성(AppHost) 구성 단순화
- **Azure 배포**: Container Apps + Azure AI 서비스 기반 운영

## 7. Risks & Mitigations
- 환각/부정확성: 템플릿 기반 구조화 + 검증 체크리스트 삽입
- 문서 과장/장황함: 섹션별 토큰 예산과 길이 제한
- 모델 편차: 동일 입력 다중 모델 생성 후 scorecard 비교

## 8. MVP Success Metrics
- 첫 문서 생성까지 3분 이내
- PRD/TRD 최소 완성도 체크리스트 90% 이상 충족
- 사용자 재생성(2회 이상) 비율 60% 이상
