// Chiamate alle API e piccole funzioni di formato condivise dalle pagine.

export async function api(percorso) {
  const r = await fetch(`/api/${percorso}`)
  if (!r.ok) throw new Error(r.status === 404 ? 'Non trovato' : `Errore ${r.status}`)
  return r.json()
}

const dataBreve = new Intl.DateTimeFormat('it-IT', { day: 'numeric', month: 'short', year: 'numeric' })
const dataOra = new Intl.DateTimeFormat('it-IT', {
  day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit',
})

export const data = (s) => (s ? dataBreve.format(new Date(s)) : '—')
export const dataConOra = (s) => (s ? dataOra.format(new Date(s)) : '—')
export const elo = (n) => (n == null ? '—' : Math.round(n).toString())
export const percento = (n) => (n == null ? '—' : `${Math.round(n)}%`)

export function delta(n) {
  if (n == null) return ''
  const v = Math.round(n * 10) / 10
  return v > 0 ? `+${v}` : `${v}`
}

export function fa(s) {
  if (!s) return '—'
  const giorni = Math.floor((Date.now() - new Date(s)) / 86400000)
  if (giorni < 1) return 'oggi'
  if (giorni === 1) return 'ieri'
  if (giorni < 30) return `${giorni} giorni fa`
  if (giorni < 365) return `${Math.floor(giorni / 30)} mesi fa`
  return `${Math.floor(giorni / 365)} anni fa`
}
