// Chi è connesso, letto una volta da /api/io e condiviso da tutte le pagine.
import { reactive } from 'vue'

export const sessione = reactive({
  caricata: false,
  loginAttivo: false,
  autenticato: false,
  nome: null,
  avatar: null,
  membro: false,
  ruoli: [],
  configurazione: null,
})

export async function caricaSessione() {
  try {
    const r = await fetch('/api/io', { credentials: 'same-origin' })
    if (r.ok) Object.assign(sessione, await r.json())
  } catch {
    /* senza risposta resta "non connesso" */
  } finally {
    sessione.caricata = true
  }
}

// L'admin entra in tutti i pannelli.
export const puoEntrare = (ruolo) => sessione.ruoli.includes(ruolo) || sessione.ruoli.includes('admin')

export const linkAccesso = (ritorno) => `/api/auth/login?ritorno=${encodeURIComponent(ritorno || '/')}`
