/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** 백엔드 API 원본. Aspire 서비스 디스커버리가 주입한다. 비워 두면 같은 오리진을 쓴다. */
  readonly VITE_AJURE_API_BASE?: string
  /** 'live'는 목 폴백을 끄고, 'mock'은 항상 인-프로세스 목을 쓴다. */
  readonly VITE_AJURE_API_MODE?: 'live' | 'mock'
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
