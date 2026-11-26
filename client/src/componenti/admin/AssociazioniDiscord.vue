<script setup>
// Gestione admin dell'associazione tra utenti Discord (fanta_torneo.utenti_discord) e giocatori della classifica TG Dito
// (fanta.giocatori). Pensata per revisionare in fretta i match "thomuggia_simile", collegare chi non ha nick e intercettare duplicati.
import { ref, computed, onMounted, nextTick } from 'vue'
import { api, apiPost, elo } from '../../api.js'
import StatoDati from '../StatoDati.vue'
import NumeroVivo from '../NumeroVivo.vue'
import FinestraModale from '../FinestraModale.vue'

const utenti = ref([])
const giocatori = ref([])
const giocatoriCaricati = ref(false)
const caricamento = ref(true)
const errore = ref('')
const avviso = ref('')
const filtro = ref('') // impostato al primo caricamento: 'controllare' se ci sono righe da verificare, altrimenti 'tutti'
const cerca = ref('')
const salvandoId = ref(null)

// modale di modifica / collegamento nick
const utenteModale = ref(null)
const cercaGiocatore = ref('')
const campoRicerca = ref(null)
const occupatoModale = ref(false)
const erroreModale = ref('')
const conflittoDuplicato = ref(null) // { playerId, playerNick, discordNome }

function avvisa(testo) {
  avviso.value = testo
  setTimeout(() => { if (avviso.value === testo) avviso.value = '' }, 4000)
}

async function carica(mantieniFiltro = false) {
  if (!utenti.value.length) caricamento.value = true
  errore.value = ''
  try {
    utenti.value = await api('admin/utenti-discord')
    if (!mantieniFiltro || !filtro.value) {
      filtro.value = daControllareCount.value > 0 ? 'controllare' : 'tutti'
    }
  } catch (e) {
    errore.value = e.message
  } finally {
    caricamento.value = false
  }
}

async function assicuraGiocatori() {
  if (giocatoriCaricati.value) return
  giocatori.value = await api('giocatori?tutti=true')
  giocatoriCaricati.value = true
}

onMounted(() => carica(false))

// mappa playerId -> lista di utenti Discord collegati (serve per evidenziare subito i duplicati)
const utentiPerPlayer = computed(() => {
  const m = new Map()
  for (const u of utenti.value) {
    if (u.playerId == null) continue
    if (!m.has(u.playerId)) m.set(u.playerId, [])
    m.get(u.playerId).push(u)
  }
  return m
})

const eDuplicato = (u) => u.playerId != null && (utentiPerPlayer.value.get(u.playerId)?.length ?? 0) > 1
const altriConStessoNick = (u) =>
  u.playerId == null ? [] : (utentiPerPlayer.value.get(u.playerId) ?? []).filter((x) => x.discordId !== u.discordId)

const eDaControllare = (u) =>
  (!u.verificato && (u.collegatoDa === 'thomuggia_simile' || u.collegatoDa === 'utente')) || eDuplicato(u)

const daControllareCount = computed(() => utenti.value.filter(eDaControllare).length)
const senzaNickCount = computed(() => utenti.value.filter((u) => u.playerId == null).length)
const thomuggiaEsattiCount = computed(() => utenti.value.filter((u) => !u.verificato && u.collegatoDa === 'thomuggia' && u.playerId != null).length)
const verificatiCount = computed(() => utenti.value.filter((u) => u.verificato).length)
const duplicatiCount = computed(() => utenti.value.filter(eDuplicato).length)

const filtrati = computed(() => {
  const q = cerca.value.trim().toLowerCase()
  return utenti.value.filter((u) => {
    if (filtro.value === 'controllare' && !eDaControllare(u)) return false
    if (filtro.value === 'senza' && u.playerId != null) return false
    if (filtro.value === 'thomuggia' && !(!u.verificato && u.collegatoDa === 'thomuggia' && u.playerId != null)) return false
    if (filtro.value === 'verificati' && !u.verificato) return false
    if (filtro.value === 'duplicati' && !eDuplicato(u)) return false
    if (!q) return true
    return [u.discordNome, u.discordId, u.playerNick || '', u.playerClan || '', u.collegatoDa || '']
      .some((v) => v.toLowerCase().includes(q))
  })
})

function etichettaOrigine(u) {
  if (u.playerId == null) return { testo: 'Non collegato', tono: 'muto' }
  if (u.verificato && u.collegatoDa === 'admin') return { testo: 'Verificato da admin', tono: 'ok' }
  if (u.verificato && u.collegatoDa === 'utente_confermato') return { testo: 'Confermato dall\'utente', tono: 'ok' }
  if (u.verificato) return { testo: 'Verificato', tono: 'ok' }
  if (u.collegatoDa === 'thomuggia_simile') return { testo: 'Thomuggia simile (da controllare)', tono: 'attenzione' }
  if (u.collegatoDa === 'utente') return { testo: 'Scelto dall\'utente (da verificare)', tono: 'attenzione' }
  if (u.collegatoDa === 'thomuggia') return { testo: 'Thomuggia esatto (non confermato)', tono: 'neutro' }
  return { testo: u.collegatoDa || 'Da confermare', tono: 'neutro' }
}

async function confermaRapida(u) {
  if (u.playerId == null) return
  salvandoId.value = u.discordId
  errore.value = ''
  try {
    await apiPost(`admin/utenti-discord/${encodeURIComponent(u.discordId)}/collega`, {
      playerId: u.playerId,
      verificato: true,
      forza: eDuplicato(u),
    })
    await carica(true)
    avvisa(`Confermato: ${u.discordNome} = ${u.playerNick}.`)
  } catch (e) {
    if (e.status === 409) {
      await apriModale(u)
      erroreModale.value = e.message
      conflittoDuplicato.value = {
        playerId: u.playerId,
        playerNick: u.playerNick,
        discordNome: e.dati?.duplicato?.discordNome ?? 'un altro utente',
      }
    } else {
      errore.value = e.message
    }
  } finally {
    salvandoId.value = null
  }
}

async function apriModale(u) {
  utenteModale.value = u
  cercaGiocatore.value = ''
  erroreModale.value = ''
  conflittoDuplicato.value = null
  try {
    await assicuraGiocatori()
  } catch (e) {
    erroreModale.value = e.message
  }
  await nextTick()
  campoRicerca.value?.focus()
}

function chiudiModale() {
  if (occupatoModale.value) return
  utenteModale.value = null
  erroreModale.value = ''
  conflittoDuplicato.value = null
}

// punteggio semplice di somiglianza tra nome Discord e nick AoE2 per proporre subito i candidati più probabili
function punteggioCandidato(discordNome, g) {
  const norm = (s) => (s || '').toLowerCase().replace(/[^a-z0-9]/g, '')
  const d = norm(discordNome)
  const n = norm(g.nick)
  if (!d || !n) return 0
  if (d === n) return 100
  if (n.includes(d) || d.includes(n)) return 75
  // prefisso comune di almeno 3 caratteri
  let comuni = 0
  const len = Math.min(d.length, n.length)
  while (comuni < len && d[comuni] === n[comuni]) comuni++
  if (comuni >= 3) return 40 + comuni * 3
  return 0
}

const giocatoriProposti = computed(() => {
  const u = utenteModale.value
  if (!u) return []
  const q = cercaGiocatore.value.trim().toLowerCase()
  if (q) {
    return giocatori.value
      .filter((g) => g.nick.toLowerCase().includes(q) || (g.clan || '').toLowerCase().includes(q))
      .slice(0, 18)
  }
  // senza ricerca scritta: mostra prima i candidati simili al nome Discord (più eventuale nick già collegato)
  const conPunteggio = giocatori.value
    .map((g) => ({
      g,
      punti: (g.playerId === u.playerId ? 120 : 0) + punteggioCandidato(u.discordNome, g),
    }))
    .filter((x) => x.punti > 0)
    .sort((a, b) => b.punti - a.punti || (b.g.eloInterno ?? 0) - (a.g.eloInterno ?? 0))
    .slice(0, 12)
    .map((x) => x.g)

  if (conPunteggio.length) return conPunteggio
  return giocatori.value.slice(0, 12)
})

const collegatoAltrove = (playerId) => {
  const u = utenteModale.value
  const altri = (utentiPerPlayer.value.get(playerId) ?? []).filter((x) => x.discordId !== u?.discordId)
  return altri.length ? altri.map((x) => x.discordNome).join(', ') : ''
}

async function assegnaGiocatore(playerId, playerNick, forza = false) {
  const u = utenteModale.value
  if (!u) return
  occupatoModale.value = true
  erroreModale.value = ''
  conflittoDuplicato.value = null
  try {
    await apiPost(`admin/utenti-discord/${encodeURIComponent(u.discordId)}/collega`, {
      playerId,
      verificato: playerId != null,
      forza,
    })
    const nomeDiscord = u.discordNome
    utenteModale.value = null
    await carica(true)
    avvisa(playerId != null ? `Collegato: ${nomeDiscord} = ${playerNick}.` : `Collegamento rimosso per ${nomeDiscord}.`)
  } catch (e) {
    if (e.status === 409) {
      erroreModale.value = e.message
      conflittoDuplicato.value = {
        playerId,
        playerNick,
        discordNome: e.dati?.duplicato?.discordNome ?? 'un altro utente',
      }
    } else {
      erroreModale.value = e.message
    }
  } finally {
    occupatoModale.value = false
  }
}

const iniziale = (nome) => (nome || '?').trim().charAt(0).toUpperCase()
</script>

<template>
  <div class="assoc">
    <p v-if="avviso" class="avviso-riga assoc__avviso" role="status">{{ avviso }}</p>

    <StatoDati :caricamento="caricamento && !utenti.length" :errore="errore" :vuoto="!utenti.length" :righe="6" testo-vuoto="Nessun utente Discord registrato." @riprova="carica(true)">
      <dl class="misure assoc__misure">
        <div class="misura">
          <dt>Da controllare</dt>
          <dd :class="{ segnale: daControllareCount }"><NumeroVivo :valore="daControllareCount" /></dd>
          <small>match simili, scelti dall'utente o duplicati</small>
        </div>
        <div class="misura">
          <dt>Senza nick</dt>
          <dd><NumeroVivo :valore="senzaNickCount" /></dd>
          <small>senza profilo della classifica</small>
        </div>
        <div class="misura">
          <dt>Thomuggia esatti</dt>
          <dd><NumeroVivo :valore="thomuggiaEsattiCount" /></dd>
          <small>in attesa di conferma</small>
        </div>
        <div class="misura">
          <dt>Verificati</dt>
          <dd><NumeroVivo :valore="verificatiCount" /></dd>
          <small>su {{ utenti.length }} utenti Discord</small>
        </div>
      </dl>

      <div v-if="duplicatiCount" class="avviso-riga assoc__duplicati" role="alert">
        <span>Ci sono <strong>{{ duplicatiCount }}</strong> utenti Discord che condividono lo stesso nick AoE2.</span>
        <button type="button" class="btn btn--linea btn--sm" @click="filtro = 'duplicati'">Mostra duplicati</button>
      </div>

      <div class="strumenti assoc__strumenti">
        <div class="scelta" role="group" aria-label="Filtra associazioni Discord">
          <button type="button" :aria-pressed="filtro === 'controllare'" @click="filtro = 'controllare'">Da controllare ({{ daControllareCount }})</button>
          <button type="button" :aria-pressed="filtro === 'senza'" @click="filtro = 'senza'">Senza nick ({{ senzaNickCount }})</button>
          <button type="button" :aria-pressed="filtro === 'thomuggia'" @click="filtro = 'thomuggia'">Thomuggia ({{ thomuggiaEsattiCount }})</button>
          <button type="button" :aria-pressed="filtro === 'verificati'" @click="filtro = 'verificati'">Verificati ({{ verificatiCount }})</button>
          <button v-if="duplicatiCount" type="button" :aria-pressed="filtro === 'duplicati'" @click="filtro = 'duplicati'">Duplicati ({{ duplicatiCount }})</button>
          <button type="button" :aria-pressed="filtro === 'tutti'" @click="filtro = 'tutti'">Tutti ({{ utenti.length }})</button>
        </div>
        <input
          v-model="cerca"
          type="search"
          class="cerca"
          placeholder="Cerca Discord, ID o nick AoE2…"
          aria-label="Cerca tra le associazioni Discord"
        />
      </div>

      <p v-if="!filtrati.length" class="muto assoc__vuoto">
        Nessuna associazione corrisponde a questo filtro.
      </p>

      <ul v-else class="assoc-lista">
        <li v-for="u in filtrati" :key="u.discordId" class="assoc-riga" :class="{ 'assoc-riga--attenzione': eDaControllare(u) }">
          <div class="assoc-riga__discord">
            <img v-if="u.discordAvatar" class="assoc-riga__avatar" :src="u.discordAvatar" alt="" width="36" height="36" />
            <span v-else class="assoc-riga__avatar assoc-riga__avatar--iniziale" aria-hidden="true">{{ iniziale(u.discordNome) }}</span>
            <div class="assoc-riga__testo">
              <div class="assoc-riga__titolo">
                <strong>{{ u.discordNome }}</strong>
                <span v-if="u.iscritto" class="etichetta etichetta--accento">Iscritto</span>
              </div>
              <span class="muto piccolo num">{{ u.discordId }}</span>
            </div>
          </div>

          <div class="assoc-riga__nick">
            <template v-if="u.playerId && u.playerNick">
              <div class="assoc-riga__titolo">
                <RouterLink :to="`/giocatore/${u.playerId}`" class="forte">{{ u.playerNick }}</RouterLink>
                <span v-if="u.playerClan" class="clan">{{ u.playerClan }}</span>
                <span v-if="eDuplicato(u)" class="etichetta etichetta--errore">
                  Duplicato ({{ altriConStessoNick(u).map((x) => x.discordNome).join(', ') }})
                </span>
              </div>
              <div class="assoc-riga__stats muto piccolo num">
                <span>Dito {{ elo(u.eloInterno) }}</span>
                <span v-if="u.elo1v1 != null">1v1 {{ elo(u.elo1v1) }}</span>
                <span v-if="u.nDiti != null">{{ u.nDiti }} Diti</span>
              </div>
            </template>
            <span v-else class="muto piccolo">Nessun nick collegato</span>
          </div>

          <div class="assoc-riga__stato">
            <span class="etichetta" :class="`etichetta--${etichettaOrigine(u).tono}`">{{ etichettaOrigine(u).testo }}</span>
          </div>

          <div class="assoc-riga__azioni">
            <button
              v-if="u.playerId && !u.verificato"
              type="button"
              class="btn btn--sm"
              :disabled="salvandoId === u.discordId"
              @click="confermaRapida(u)"
            >
              {{ salvandoId === u.discordId ? 'Salvo…' : 'Conferma' }}
            </button>
            <button
              type="button"
              class="btn btn--linea btn--sm"
              :disabled="salvandoId === u.discordId"
              @click="apriModale(u)"
            >
              {{ u.playerId ? 'Cambia' : 'Collega' }}
            </button>
          </div>
        </li>
      </ul>
    </StatoDati>

    <FinestraModale
      v-if="utenteModale"
      largo
      :titolo="`Associa nick: ${utenteModale.discordNome}`"
      @chiudi="chiudiModale"
    >
      <div class="picker">
        <p class="muto piccolo picker__meta">
          <span>ID Discord: <strong class="num">{{ utenteModale.discordId }}</strong></span>
          <span v-if="utenteModale.playerNick">Attuale: <strong>{{ utenteModale.playerNick }}</strong></span>
        </p>

        <input
          ref="campoRicerca"
          v-model="cercaGiocatore"
          type="search"
          class="cerca"
          placeholder="Scrivi nick o clan della classifica…"
          aria-label="Cerca giocatore in classifica"
        />

        <div v-if="erroreModale" class="picker__errore" role="alert">
          <p>{{ erroreModale }}</p>
          <button
            v-if="conflittoDuplicato"
            type="button"
            class="btn btn--linea btn--sm"
            :disabled="occupatoModale"
            @click="assegnaGiocatore(conflittoDuplicato.playerId, conflittoDuplicato.playerNick, true)"
          >
            Collega comunque
          </button>
        </div>

        <p class="muto piccolo picker__intestazione">
          {{ cercaGiocatore.trim() ? `Risultati (${giocatoriProposti.length})` : 'Suggeriti per questo nome Discord' }}
        </p>

        <ul v-if="giocatoriProposti.length" class="picker__lista">
          <li v-for="g in giocatoriProposti" :key="g.playerId">
            <button
              type="button"
              class="picker__voce"
              :class="{ 'picker__voce--attuale': g.playerId === utenteModale.playerId }"
              :disabled="occupatoModale"
              @click="assegnaGiocatore(g.playerId, g.nick, false)"
            >
              <span class="picker__nome">
                <strong>{{ g.nick }}</strong>
                <span v-if="g.clan" class="clan">{{ g.clan }}</span>
                <span v-if="g.playerId === utenteModale.playerId" class="etichetta etichetta--accento">Attuale</span>
                <span v-else-if="collegatoAltrove(g.playerId)" class="etichetta etichetta--errore">
                  già di {{ collegatoAltrove(g.playerId) }}
                </span>
              </span>
              <span class="picker__elo muto piccolo num">
                <span>Dito {{ elo(g.eloInterno) }}</span>
                <span v-if="g.elo1v1 != null">1v1 {{ elo(g.elo1v1) }}</span>
                <span>{{ g.nDiti }} Diti</span>
              </span>
            </button>
          </li>
        </ul>
        <p v-else class="muto piccolo">Nessun giocatore trovato con questa ricerca.</p>

        <div class="picker__azioni">
          <button
            v-if="utenteModale.playerId != null"
            type="button"
            class="btn btn--linea"
            :disabled="occupatoModale"
            @click="assegnaGiocatore(null, null, false)"
          >
            Scollega nick
          </button>
          <button type="button" class="btn btn--linea" :disabled="occupatoModale" @click="chiudiModale">Chiudi</button>
        </div>
      </div>
    </FinestraModale>
  </div>
</template>

<style scoped>
.assoc { display: grid; grid-template-columns: minmax(0, 1fr); gap: var(--space-lg); min-width: 0; }
.assoc__misure { margin-block: 0; }
.assoc__avviso {
  margin-bottom: 0;
  box-shadow: inset 3px 0 0 var(--color-accent);
}
.assoc__duplicati {
  margin-bottom: 0;
  display: flex; flex-wrap: wrap; justify-content: space-between; align-items: center; gap: var(--space-sm);
  box-shadow: inset 3px 0 0 var(--color-error);
}
.assoc__strumenti { margin-block: 0; min-width: 0; }
.assoc__strumenti .scelta { overflow-x: auto; max-width: 100%; min-width: 0; }
.assoc__vuoto { margin: var(--space-md) 0; }

.assoc-lista { list-style: none; margin: 0; padding: 0; border-top: var(--rule-hair) solid var(--color-rule); }
.assoc-riga {
  display: grid; grid-template-columns: minmax(0, 1.8fr) minmax(0, 2fr) minmax(0, 1.4fr) auto;
  align-items: center; gap: var(--space-md); padding: var(--space-sm) var(--space-xs);
  border-bottom: var(--rule-hair) solid var(--color-rule);
  transition: background-color var(--dur-short) var(--ease-out);
}
.assoc-riga:hover { background: var(--color-paper-3); }
.assoc-riga--attenzione { box-shadow: inset 3px 0 0 var(--color-error); padding-left: var(--space-sm); }

.assoc-riga__discord { display: flex; align-items: center; gap: var(--space-sm); min-width: 0; }
.assoc-riga__avatar { width: 36px; height: 36px; border-radius: 50%; flex-shrink: 0; object-fit: cover; }
.assoc-riga__avatar--iniziale {
  display: inline-flex; align-items: center; justify-content: center;
  background: var(--color-paper-3); color: var(--color-ink-2); font-size: var(--text-sm);
}
.assoc-riga__testo { display: grid; gap: var(--space-3xs); min-width: 0; }
.assoc-riga__testo .num { overflow-wrap: anywhere; }
.assoc-riga__titolo { display: flex; flex-wrap: wrap; align-items: baseline; gap: var(--space-xs); min-width: 0; }
.assoc-riga__titolo strong, .assoc-riga__titolo a { overflow-wrap: anywhere; }
.assoc-riga__nick { display: grid; gap: var(--space-3xs); min-width: 0; }
.assoc-riga__stats { display: flex; flex-wrap: wrap; gap: var(--space-sm); }
.assoc-riga__stato { justify-self: start; min-width: 0; }
.assoc-riga__azioni { display: flex; flex-wrap: wrap; align-items: center; justify-content: flex-end; gap: var(--space-xs); }

.etichetta {
  display: inline-flex; align-items: center; padding: var(--space-3xs) var(--space-xs);
  border-radius: var(--radius-pill); background: var(--color-paper-3); color: var(--color-ink-2);
  font-size: var(--text-xs); line-height: 1.3; max-width: 100%;
}
.etichetta--ok {
  background: color-mix(in oklch, var(--color-success) 18%, var(--color-paper-3));
  color: var(--color-ink);
  box-shadow: inset 0 0 0 var(--rule-hair) var(--color-success);
}
.etichetta--accento {
  background: color-mix(in oklch, var(--color-accent) 20%, var(--color-paper-3));
  color: var(--color-ink);
  box-shadow: inset 0 0 0 var(--rule-hair) var(--color-accent);
}
.etichetta--attenzione,
.etichetta--errore {
  background: color-mix(in oklch, var(--color-error) 20%, var(--color-paper-3));
  color: var(--color-ink);
  box-shadow: inset 0 0 0 var(--rule-hair) var(--color-error);
}
.etichetta--muto { color: var(--color-muted); }

/* picker nella finestra modale */
.picker { display: grid; grid-template-columns: minmax(0, 1fr); gap: var(--space-sm); }
.picker__meta { display: flex; flex-wrap: wrap; gap: var(--space-md); margin: 0; }
.picker__intestazione { margin: 0; }
.picker__errore {
  display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: var(--space-sm);
  padding: var(--space-xs) var(--space-sm); border-radius: var(--radius-card);
  background: color-mix(in oklch, var(--color-error) 18%, var(--color-paper));
  box-shadow: inset 0 0 0 var(--rule-hair) var(--color-error);
  color: var(--color-ink); font-size: var(--text-sm);
}
.picker__errore p { margin: 0; }
.picker__lista {
  list-style: none; margin: 0; padding: 0; max-height: 19rem; overflow-y: auto;
  border-top: var(--rule-hair) solid var(--color-rule);
}
.picker__lista li { border-bottom: var(--rule-hair) solid var(--color-rule); }
.picker__voce {
  width: 100%; display: grid; gap: var(--space-3xs); padding: var(--space-xs) var(--space-xs);
  border: 0; background: transparent; color: inherit; font: inherit; text-align: left; cursor: pointer;
  border-radius: var(--radius-card);
  transition: background-color var(--dur-short) var(--ease-out), transform var(--dur-micro) var(--ease-out);
}
.picker__voce:hover { background: var(--color-paper-3); }
.picker__voce:active { transform: translateY(1px); }
.picker__voce--attuale { background: color-mix(in oklch, var(--color-accent) 14%, var(--color-paper-2)); }
.picker__voce:focus-visible { outline: 2px solid var(--color-focus); outline-offset: -2px; }
.picker__voce:disabled { opacity: 0.55; cursor: not-allowed; transform: none; }
.picker__nome { display: flex; flex-wrap: wrap; align-items: center; gap: var(--space-xs); }
.picker__elo { display: flex; flex-wrap: wrap; gap: var(--space-sm); }
.picker__azioni { display: flex; flex-wrap: wrap; justify-content: flex-end; gap: var(--space-xs); margin-top: var(--space-xs); }

@media (prefers-reduced-motion: reduce) {
  .assoc-riga, .picker__voce { transition: none; }
}

@media (max-width: 860px) {
  .assoc__misure { grid-template-columns: repeat(2, minmax(0, 1fr)); }
  .assoc__misure .misura + .misura { border-left: 0; padding-left: 0; }
  .assoc__misure .misura { border-bottom: var(--rule-hair) solid var(--color-rule); }
  .assoc-riga { grid-template-columns: minmax(0, 1fr); gap: var(--space-xs); }
  .assoc-riga__azioni { justify-content: flex-start; }
}

@media (max-width: 420px) {
  .assoc__misure { grid-template-columns: minmax(0, 1fr); }
}
</style>
