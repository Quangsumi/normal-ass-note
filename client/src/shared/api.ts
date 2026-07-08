import { API_BASE } from './constants'

export async function apiRequest(path: string, init?: RequestInit) {
  try {
    return await fetch(`${API_BASE}${path}`, init)
  } catch {
    return null
  }
}

export async function readError(response: Response) {
  try {
    const problem = await response.json()
    if (problem.errors) {
      return Object.values(problem.errors).flat().join(' ')
    }

    return problem.title ?? problem.detail ?? 'request failed'
  } catch {
    try {
      return await response.text()
    } catch {
      return 'request failed'
    }
  }
}
