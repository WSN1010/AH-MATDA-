import type { TargetDefinition } from './types'

/**
 * 대상 코딩 에이전트 레지스트리. 출처는 AI-FILE-SPEC.md §3 지원 매트릭스다.
 * TRD §9에 대상 목록 조회 엔드포인트가 없으므로 정적 상수로 둔다.
 */
export const TARGETS: TargetDefinition[] = [
  {
    id: 'claude-code',
    name: 'Claude Code',
    path: 'CLAUDE.md',
    support: 'Stable',
    discovery: '저장소 루트의 CLAUDE.md를 프로젝트 지침으로 자동 로드한다.',
  },
  {
    id: 'github-copilot',
    name: 'GitHub Copilot',
    path: 'AGENTS.md',
    support: 'Stable',
    discovery: '가장 가까운 AGENTS.md를 우선 적용하고 루트 파일은 저장소 전체에 적용한다.',
  },
  {
    id: 'openai-codex',
    name: 'OpenAI Codex',
    path: 'AGENTS.md',
    support: 'Stable',
    discovery: '루트부터 현재 경로까지 AGENTS.md를 계층으로 병합한다.',
  },
  {
    id: 'gemini-cli',
    name: 'Gemini CLI',
    path: 'GEMINI.md',
    support: 'Stable',
    discovery: '계층적 컨텍스트로 읽고 @file.md import를 지원한다.',
  },
  {
    id: 'cursor',
    name: 'Cursor',
    path: '.cursor/rules/ajure.mdc',
    support: 'Basic',
    discovery: 'YAML frontmatter가 붙은 규칙 파일을 규칙 활성화 모드에 따라 적용한다.',
  },
  {
    id: 'devin-windsurf',
    name: 'Devin Desktop / Windsurf Cascade',
    path: '.devin/rules/ajure.md',
    support: 'Basic',
    discovery: 'Workspace Rule로 읽는다. .windsurf/rules/는 레거시 폴백이다.',
  },
  {
    id: 'cline',
    name: 'Cline',
    path: '.clinerules/ajure.md',
    support: 'Basic',
    discovery: '.clinerules 폴더의 Markdown 규칙을 항상 읽는다.',
  },
  {
    id: 'amazon-q',
    name: 'Amazon Q Developer',
    path: '.amazonq/rules/ajure.md',
    support: 'Basic',
    discovery: '.amazonq/rules 폴더의 Markdown을 프로젝트 규칙으로 사용한다.',
  },
  {
    id: 'generic',
    name: '기타 / 범용 도구',
    path: 'AGENTS.md',
    support: 'Generic',
    discovery: 'AGENTS.md 호환을 가정한다. 도구가 자동으로 읽지 않으면 직접 전달해야 한다.',
  },
]

const BY_ID = new Map(TARGETS.map((t) => [t.id, t]))

export function findTarget(id: string): TargetDefinition | undefined {
  return BY_ID.get(id)
}

export function targetName(id: string): string {
  return BY_ID.get(id)?.name ?? id
}

/** 같은 경로를 쓰는 대상은 하나의 호환 파일로 병합된다. AI-FILE-SPEC §2 */
export function mergedPaths(targetIds: string[]): { path: string; targetIds: string[] }[] {
  const byPath = new Map<string, string[]>()
  for (const id of targetIds) {
    const target = BY_ID.get(id)
    if (!target) continue
    const list = byPath.get(target.path)
    if (list) list.push(id)
    else byPath.set(target.path, [id])
  }
  return [...byPath].map(([path, ids]) => ({ path, targetIds: ids }))
}

export const SUPPORT_LABEL: Record<TargetDefinition['support'], string> = {
  Stable: '정식 지원',
  Basic: '기본 지원',
  Generic: '범용 폴백',
}
