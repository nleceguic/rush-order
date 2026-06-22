export function formatCurrency(amount: number): string {
  return new Intl.NumberFormat(navigator.language, {
    style:    'currency',
    currency: 'EUR',
  }).format(amount)
}

export function formatDate(
  date: string | Date,
  options: Intl.DateTimeFormatOptions = { day: 'numeric', month: 'short', year: 'numeric' },
): string {
  return new Intl.DateTimeFormat(navigator.language, options).format(
    typeof date === 'string' ? new Date(date) : date,
  )
}
