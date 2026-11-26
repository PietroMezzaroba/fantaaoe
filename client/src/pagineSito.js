// Etichette leggibili delle pagine del sito, dei ruoli e della durata di connessione per la dashboard admin.

const NOMI_PAGINE = {
  '/': 'Home',
  '/asta': 'Asta',
  '/asta/inizia': 'Asta (capitano)',
  '/asta/gestisci': 'Gestione asta',
  '/io': 'La tua pagina',
  '/profilo': 'Impostazioni profilo',
  '/classifica': 'Classifica',
  '/partite': 'Partite',
  '/civilta': 'Civiltà',
  '/torneo': 'Torneo',
  '/admin': 'Area admin',
}

export function nomePagina(percorso) {
  if (!percorso) return 'Home'
  if (NOMI_PAGINE[percorso]) return NOMI_PAGINE[percorso]
  if (percorso.startsWith('/giocatore/')) return 'Scheda giocatore'
  if (percorso.startsWith('/partita/')) return 'Scheda partita'
  return percorso
}

const NOMI_RUOLI = {
  admin: 'Admin',
  capitano: 'Capitano',
  streamer: 'Streamer',
  giocatore: 'Giocatore',
  utente: 'Utente',
}

export function nomeRuolo(chiave) {
  return NOMI_RUOLI[chiave] ?? chiave
}

export function durataOnline(iso, adessoMs = Date.now()) {
  if (!iso) return ''
  const sec = Math.max(0, Math.floor((adessoMs - new Date(iso).getTime()) / 1000))
  if (sec < 45) return 'adesso'
  const min = Math.floor(sec / 60)
  if (min < 60) return `${min}\u00a0min`
  const ore = Math.floor(min / 60)
  const resto = min % 60
  return resto ? `${ore}\u00a0h ${resto}\u00a0min` : `${ore}\u00a0h`
}
