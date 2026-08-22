import type { ProblemDetails } from './types'

/** TRD §9 Problem Details를 그대로 담는 오류. 화면은 이 형식만 다룬다. */
export class ApiError extends Error {
  readonly problem: ProblemDetails

  constructor(problem: ProblemDetails) {
    super(problem.message)
    this.name = 'ApiError'
    this.problem = problem
  }
}

let counter = 0

export function correlationId(): string {
  counter += 1
  return `mock-${Date.now().toString(36)}-${counter.toString(36).padStart(3, '0')}`
}

export function problem(
  code: string,
  message: string,
  retryable = false,
  details?: Record<string, string>,
): ApiError {
  return new ApiError({ code, message, correlationId: correlationId(), retryable, details })
}
