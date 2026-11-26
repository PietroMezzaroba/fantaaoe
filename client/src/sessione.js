// Chi è connesso, letto una volta da /api/io e condiviso da tutte le pagine.
import { reactive } from 'vue'
import { applicaTema } from './tema.js'
import { applicaAudioUtente, impostaAudioAbilitato } from './suoni.js'
import { apiPut } from './api.js'

export const sessione = reactive({
  caricata: false,
  loginAttivo: false,
  loginSviluppo: false, // solo in sviluppo: login simulato con username e password
  accessoAperto: false, // form del login simulato visibile
  autenticato: false,
  discordId: null,
  nome: null,
  avatar: null,
  membro: false,
  ruoli: [],
  configurazione: null,
  impostazioni: null, // { audioAttivo, layoutScuro, cursore, animazioneIniziale } salvate nel database, solo da connessi
})

export const impostazioniPredefinite = { audioAttivo: true, layoutScuro: true, cursore: 'classico', animazioneIniziale: true }

export async function caricaSessione() {
  try {
    const r = await fetch('/api/io', { credentials: 'same-origin' })
    if (r.ok) {
      Object.assign(sessione, await r.json())
      // tema e audio seguono le impostazioni dell'utente; da non connessi valgono quelle predefinite
      const imp = sessione.autenticato ? sessione.impostazioni : null
      applicaTema(imp?.layoutScuro ?? true)
      applicaAudioUtente(imp?.audioAttivo ?? true)
    }
  } catch {
    /* senza risposta resta "non connesso" */
  } finally {
    sessione.caricata = true
  }
}

// Salva subito un'impostazione (nessun tasto di salvataggio). Se il salvataggio fallisce torna al valore di prima e lancia l'errore.
export async function salvaImpostazione(chiave, valore) {
  const prima = { ...impostazioniPredefinite, ...sessione.impostazioni }
  const applica = (imp) => {
    sessione.impostazioni = imp
    if (chiave === 'layoutScuro') applicaTema(imp.layoutScuro)
    else if (chiave === 'audioAttivo') impostaAudioAbilitato(imp.audioAttivo)
  }
  applica({ ...prima, [chiave]: valore })
  try {
    await apiPut('profilo/impostazioni', { [chiave]: valore })
  } catch (e) {
    if (chiave === 'layoutScuro') applicaTema(prima.layoutScuro)
    else if (chiave === 'audioAttivo') applicaAudioUtente(prima.audioAttivo)
    sessione.impostazioni = prima
    throw e
  }
}

// L'admin entra in tutti i pannelli.
export const puoEntrare = (ruolo) => sessione.ruoli.includes(ruolo) || sessione.ruoli.includes('admin')

// Nei link "Accedi": in sviluppo apre la form del login simulato, altrimenti segue il link a Discord.
export function accedi(evento) {
  if (!sessione.loginSviluppo) return
  evento.preventDefault()
  sessione.accessoAperto = true
}

export const linkAccesso = (ritorno) => `/api/auth/login?ritorno=${encodeURIComponent(ritorno || '/')}`
