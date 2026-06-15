const KEY = 'rush-device-fp'

export function getDeviceFingerprint(): string {
  const existing = localStorage.getItem(KEY)
  if (existing !== null) return existing
  const fp = crypto.randomUUID()
  localStorage.setItem(KEY, fp)
  return fp
}
