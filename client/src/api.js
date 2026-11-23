// Chiamate alle API e piccole funzioni di formato condivise dalle pagine.

export async function api(percorso) {
  const r = await fetch(`/api/${percorso}`, { credentials: 'same-origin' })
  if (!r.ok) {
    let msg = r.status === 404 ? 'non trovato' : `errore ${r.status}`
    try { const j = await r.json(); if (j?.errore) msg = j.errore } catch {}
    throw new Error(msg)
  }
  return r.json()
}

export async function apiInvia(percorso, metodo, corpo) {
  const r = await fetch(`/api/${percorso}`, {
    method: metodo,
    credentials: 'same-origin',
    headers: corpo !== undefined ? { 'Content-Type': 'application/json' } : undefined,
    body: corpo !== undefined ? JSON.stringify(corpo) : undefined,
  })
  if (!r.ok) {
    let msg = `errore ${r.status}`
    try { const j = await r.json(); if (j?.errore) msg = j.errore } catch {}
    throw new Error(msg)
  }
  return r.json()
}

export const apiPost = (p, b) => apiInvia(p, 'POST', b)
export const apiPut = (p, b) => apiInvia(p, 'PUT', b)
export const apiPatch = (p, b) => apiInvia(p, 'PATCH', b)
export const apiDelete = (p) => apiInvia(p, 'DELETE')

const dataBreve = new Intl.DateTimeFormat('it-IT', { day: 'numeric', month: 'short', year: 'numeric' })
const soloGiorno = new Intl.DateTimeFormat('it-IT', { weekday: 'short', day: 'numeric', month: 'short', year: 'numeric' })
const soloOra = new Intl.DateTimeFormat('it-IT', { hour: '2-digit', minute: '2-digit' })

const ND = 'n.d.'
export const data = (s) => (s ? dataBreve.format(new Date(s)) : ND)
export const giorno = (s) => (s ? soloGiorno.format(new Date(s)) : ND)
export const ora = (s) => (s ? soloOra.format(new Date(s)) : '')
export const elo = (n) => (n == null ? ND : Math.round(n).toString())
export const percento = (n) => (n == null ? ND : `${Math.round(n)} %`)

export function delta(n) {
  if (n == null) return ''
  const v = Math.round(n * 10) / 10
  const testo = Math.abs(v).toLocaleString('it-IT', { maximumFractionDigits: 1 })
  return v > 0 ? `+${testo}` : v < 0 ? `−${testo}` : '0'
}

export function fa(s) {
  if (!s) return ND
  const giorni = Math.floor((Date.now() - new Date(s)) / 86400000)
  if (giorni < 1) return 'oggi'
  if (giorni === 1) return 'ieri'
  if (giorni < 30) return `${giorni} giorni fa`
  if (giorni < 365) return `${Math.floor(giorni / 30)} mesi fa`
  const anni = Math.floor(giorni / 365)
  return anni === 1 ? 'un anno fa' : `${anni} anni fa`
}
