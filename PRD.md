# PRD - 아주르 Document Automation Agent

## 1. Product Overview
아주르는 사용자의 아이디어 입력을 바탕으로, 선택한 AI 모델 특성을 반영한 **IDEATION.md, PRD.md, TRD.md**를 자동 생성하는 에이전트다.

## 2. Goals
- 문서 초안 작성 시간을 수동 대비 80% 단축
- 모델별 문서 결과를 일관된 포맷으로 비교 가능하게 제공
- 대회 제출 요건(PRD/TRD)을 누락 없이 생성

## 3. Non-Goals
- 일반 목적 코드 생성기 제공
- 협업 편집기 자체 구현
- 복잡한 워크플로 엔진 구축

## 4. Personas
- 해커톤 팀 리더: 빠른 제출 문서 작성 필요
- 개발자: 기술 설계 문서 표준화 필요

## 5. User Scenarios
1. 사용자가 제품 아이디어를 입력한다.
2. 생성 대상 문서(IDEATION/PRD/TRD)와 모델을 선택한다.
3. 에이전트가 모델별로 문서를 생성하고 결과를 저장한다.
4. 사용자는 버전과 모델 차이를 비교해 최종본을 채택한다.

## 6. Functional Requirements

### FR-1 Input Collection
- 문제 정의, 타겟 사용자, 핵심 가치, 성공 지표를 입력받는다.

### FR-2 Model-aware Generation
- 모델 프로파일(톤/깊이/강조점)을 적용해 문서를 생성한다.
- 최소 1개 모델, 최대 N개 모델(설정값) 동시 실행을 지원한다.

### FR-3 Document Pipeline
- IDEATION 생성 후, 같은 컨텍스트로 PRD 생성, 이후 TRD 생성 순서를 보장한다.
- 각 문서는 섹션 템플릿을 준수한다.

### FR-4 Versioning
- 생성 시각, 모델명, 프롬프트 버전, 입력 요약을 메타데이터로 저장한다.

### FR-5 Comparison View
- 같은 입력 기준으로 모델별 문서 차이를 요약한다(강점/약점/권장안).

### FR-6 Mandatory Technical Constraints
- 에이전트 오케스트레이션은 **Microsoft Agent Framework를 필수 사용**한다.
- Azure 배포는 **.NET Aspire 기반 배포 경로를 필수 사용**한다.

## 7. Non-Functional Requirements
- 응답성: 단일 모델 3문서 생성 120초 이내(목표)
- 신뢰성: 실패 단계 재시도(최대 2회), 부분 실패 보고
- 보안: API 키는 Key Vault/환경변수로만 관리
- 관측성: 요청/단계/토큰 사용량 로깅

## 8. UX Requirements
- 최소 입력 폼 + 생성 버튼
- 생성 진행 상태(IDEATION -> PRD -> TRD) 표시
- 문서 미리보기 및 다운로드(.md)

## 9. Acceptance Criteria
1. 필수 입력이 채워지면 3종 문서가 순차 생성된다.
2. 모델명을 바꾸면 문서 톤/구조 차이가 확인된다.
3. 생성 이력에서 이전 결과를 다시 열람할 수 있다.
4. PRD/TRD는 지정 템플릿 섹션을 누락 없이 포함한다.

## 10. Release Plan (MVP)
- v0.1: 단일 모델 + 3문서 생성 + 저장
- v0.2: 다중 모델 비교 + 품질 scorecard
- v0.3: GitHub 연동(커밋/PR 첨부)
