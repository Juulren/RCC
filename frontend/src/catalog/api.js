export class CatalogError extends Error {
  constructor(status, code, fields = {}) {
    super(code)
    this.status = status
    this.code = code
    this.fields = fields
  }
}

export async function catalogApi(
  path,
  { method = 'GET', body, signal, headers: extraHeaders = {} } = {},
) {
  try {
    const requestSignal = signal
      ? AbortSignal.any([signal, AbortSignal.timeout(15000)])
      : AbortSignal.timeout(15000)
    const headers = { ...extraHeaders }
    if (method !== 'GET') {
      const tokenResponse = await fetch('/api/auth/csrf', {
        credentials: 'same-origin',
        cache: 'no-store',
        signal: requestSignal,
      })
      if (!tokenResponse.ok) throw new CatalogError(tokenResponse.status, 'NETWORK')
      const { token } = await tokenResponse.json()
      if (!token) throw new CatalogError(0, 'NETWORK')
      headers['X-CSRF-TOKEN'] = token
      headers['Content-Type'] = 'application/json'
    }
    const response = await fetch(`/api${path}`, {
      method,
      credentials: 'same-origin',
      cache: 'no-store',
      headers,
      signal: requestSignal,
      ...(body ? { body: JSON.stringify(body) } : {}),
    })
    if (!response.ok) {
      const problem = await response.json().catch(() => ({}))
      const error = new CatalogError(
        response.status,
        problem.code ?? 'NETWORK',
        problem.errors ?? {},
      )
      error.quote = problem.quote
      throw error
    }
    return response.status === 204 ? null : await response.json()
  } catch (error) {
    if (error.name === 'AbortError' || error instanceof CatalogError) throw error
    throw new CatalogError(0, 'NETWORK')
  }
}

export const money = (value, language) =>
  new Intl.NumberFormat(language === 'es' ? 'es-MX' : 'en-GB', {
    style: 'currency',
    currency: 'CNY',
  }).format(value)
