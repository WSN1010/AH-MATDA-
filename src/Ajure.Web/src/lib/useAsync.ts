import { useCallback, useEffect, useState } from 'react'
import type { DependencyList } from 'react'
import { ApiError } from '../api/error'

export interface AsyncState<T> {
  status: 'loading' | 'error' | 'ready'
  data: T | null
  error: ApiError | null
}

export function asApiError(error: unknown): ApiError {
  if (error instanceof ApiError) return error
  return new ApiError({
    code: 'unexpected',
    message: error instanceof Error ? error.message : '알 수 없는 오류가 발생했습니다.',
    correlationId: '-',
    retryable: true,
  })
}

/** 화면 공통 데이터 로딩. loading / error / ready 세 상태만 다룬다. */
export function useAsync<T>(load: () => Promise<T>, deps: DependencyList) {
  const [state, setState] = useState<AsyncState<T>>({ status: 'loading', data: null, error: null })
  const [nonce, setNonce] = useState(0)

  useEffect(() => {
    let cancelled = false
    setState((previous) => ({ ...previous, status: 'loading', error: null }))
    load()
      .then((data) => {
        if (!cancelled) setState({ status: 'ready', data, error: null })
      })
      .catch((error: unknown) => {
        if (!cancelled) setState({ status: 'error', data: null, error: asApiError(error) })
      })
    return () => {
      cancelled = true
    }
    // load는 매 렌더마다 새로 만들어지므로 호출자가 넘긴 deps만 관찰한다.

  }, [...deps, nonce])

  const reload = useCallback(() => setNonce((n) => n + 1), [])
  const setData = useCallback((data: T) => setState({ status: 'ready', data, error: null }), [])

  return { ...state, reload, setData }
}
