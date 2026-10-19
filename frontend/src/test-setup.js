import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach, vi } from 'vitest'

HTMLDialogElement.prototype.showModal = function () {
  this.open = true
}
window.matchMedia ??= () => ({
  matches: false,
  addEventListener() {},
  removeEventListener() {},
  addListener() {},
  removeListener() {},
})
HTMLDialogElement.prototype.close = function () {
  this.open = false
  this.dispatchEvent(new Event('close'))
}
afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
  localStorage.clear()
})
