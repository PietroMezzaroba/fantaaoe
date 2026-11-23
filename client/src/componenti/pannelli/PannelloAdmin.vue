<script setup>
import { ref, computed, onMounted } from 'vue'
import { api, apiPost, apiPatch, apiDelete, elo, data } from '../../api.js'
import { sessione } from '../../sessione.js'
import { usaAstaLive } from '../../astaLive.js'

const scheda = ref('asta')
const { dati: asta, connesso } = usaAstaLive()

const giocatori = ref([])
const iscritti = ref([])
const squadre = ref([])
const utentiDiscord = ref([])
const ticket = ref([])

const messaggio = ref(null)
const errore = ref(null)
const occupato = ref(false)

// Regia Asta
const iscrittoDaChiamare = ref('')
const messaggioAsta = ref('')
const squadraOffertaAdmin = ref('')
const importoOffertaAdmin = ref('')

// Nuovo iscritto
const ricercaGiocatore = ref('')
const nuovoIscritto = ref({ playerId: null, nick: '', fascia: 'C', seeding: '', prezzoBase: 1, discordId: '', note: '' })

// Nuova squadra
const nuovaSquadra = ref({ nome: '', capitanoNome: '', capitanoDiscordId: '', berluIniziali: 401, ordineChiamata: 1, canaleDiscordUrl: '' })

// Risposte ticket (mappa id -> { stato, rispostaAdmin })
const bozzeTicket = ref({})

const giocatoriFiltrati = computed(() => {
  const q = ricercaGiocatore.value.trim().toLowerCase()
  if (!q) return []
  return giocatori.value.filter((g) => g.nick.toLowerCase().includes(q)).slice(0, 8)
})

const squadraMigliore = computed(() =>
  asta.value?.squadre?.find((s) => s.id === asta.value.squadraMiglioreId) || null
)

function avvisa(testo) {
  messaggio.value = testo
  setTimeout(() => { if (messaggio.value === testo) messaggio.value = null }, 3500)
}

async function caricaTutto() {
  errore.value = null
  try {
    const [g, isc, sq, ud, tk] = await Promise.all([
      api('giocatori?tutti=true'),
      api('torneo/iscritti'),
      api('torneo/squadre'),
      api('admin/utenti-discord'),
      api('ticket?tutti=true'),
    ])
    giocatori.value = g
    iscritti.value = isc
    squadre.value = sq
    utentiDiscord.value = ud
    ticket.value = tk
    nuovaSquadra.value.ordineChiamata = sq.length + 1
    const m = {}
    for (const t of tk) {
      m[t.id] = { stato: t.stato, rispostaAdmin: t.rispostaAdmin || '' }
    }
    bozzeTicket.value = m
  } catch (e) {
    errore.value = e.message
  }
}

async function esegui(fn, okTesto) {
  occupato.value = true
  errore.value = null
  try {
    await fn()
    if (okTesto) avvisa(okTesto)
    await caricaTutto()
  } catch (e) {
    errore.value = e.message
  } finally {
    occupato.value = false
  }
}

// --- Regia Asta ---
const chiamaGiocatore = () => {
  if (!iscrittoDaChiamare.value) return
  esegui(() => apiPost('admin/asta/chiama', { iscrittoId: Number(iscrittoDaChiamare.value) }), 'Giocatore chiamato all\'asta.')
}
const impostaStatoAsta = (stato) =>
  esegui(() => apiPost('admin/asta/stato', { stato, messaggio: messaggioAsta.value || null }), `Stato asta: ${stato}.`)
const aggiudicaAsta = () =>
  esegui(() => apiPost('admin/asta/aggiudica', {}), 'Giocatore aggiudicato!')
const annullaOfferta = () =>
  esegui(() => apiPost('admin/asta/annulla-offerta', {}), 'Ultima offerta annullata.')
const offertaAdmin = () => {
  if (!squadraOffertaAdmin.value || !importoOffertaAdmin.value) return
  esegui(async () => {
    await apiPost('asta/offerta', {
      squadraId: Number(squadraOffertaAdmin.value),
      importo: Number(importoOffertaAdmin.value),
    })
    importoOffertaAdmin.value = ''
  }, 'Offerta registrata.')
}

// --- Iscritti ---
function selezionaDaClassifica(g) {
  nuovoIscritto.value.playerId = g.playerId
  nuovoIscritto.value.nick = g.nick
  ricercaGiocatore.value = ''
}

const salvaIscritto = () =>
  esegui(async () => {
    await apiPost('admin/iscritti', {
      playerId: nuovoIscritto.value.playerId || null,
      discordId: nuovoIscritto.value.discordId || null,
      nick: nuovoIscritto.value.nick,
      fascia: nuovoIscritto.value.fascia,
      seeding: nuovoIscritto.value.seeding ? Number(nuovoIscritto.value.seeding) : null,
      prezzoBase: Number(nuovoIscritto.value.prezzoBase) || 1,
      note: nuovoIscritto.value.note || null,
    })
    nuovoIscritto.value = { playerId: null, nick: '', fascia: 'C', seeding: '', prezzoBase: 1, discordId: '', note: '' }
  }, 'Iscritto salvato.')

const autoSeeding = () =>
  esegui(() => apiPost('admin/iscritti/auto-seeding', {}), 'Seeding e fasce A/B/C calcolati per ELO 1v1.')

const rimuoviIscritto = (id) =>
  esegui(() => apiDelete(`admin/iscritti/${id}`), 'Iscritto rimosso.')

// --- Squadre ---
const salvaSquadra = () =>
  esegui(async () => {
    await apiPost('admin/squadre', {
      nome: nuovaSquadra.value.nome,
      capitanoNome: nuovaSquadra.value.capitanoNome,
      capitanoDiscordId: nuovaSquadra.value.capitanoDiscordId || null,
      berluIniziali: Number(nuovaSquadra.value.berluIniziali) || 401,
      ordineChiamata: Number(nuovaSquadra.value.ordineChiamata) || 1,
      canaleDiscordUrl: nuovaSquadra.value.canaleDiscordUrl || null,
    })
    nuovaSquadra.value = { nome: '', capitanoNome: '', capitanoDiscordId: '', berluIniziali: 401, ordineChiamata: squadre.value.length + 2, canaleDiscordUrl: '' }
  }, 'Squadra salvata.')

const eliminaSquadra = (id) =>
  esegui(() => apiDelete(`admin/squadre/${id}`), 'Squadra eliminata.')

// --- Utenti Discord ---
const verificaUtente = (u, verificato) =>
  esegui(() => apiPost(`admin/utenti-discord/${encodeURIComponent(u.discordId)}/collega`, {
    playerId: u.playerId ? Number(u.playerId) : null,
    verificato,
  }), 'Collegamento aggiornato.')

// --- Ticket ---
const salvaTicket = (id) => {
  const b = bozzeTicket.value[id]
  if (!b) return
  esegui(() => apiPatch(`admin/ticket/${id}`, b), `Ticket #${id} aggiornato.`)
}

onMounted(caricaTutto)
</script>

<template>
  <div class="pannello-app">
    <div class="pannello-testa">
      <div>
        <h1>Admin</h1>
        <p class="muto">
          Regia dell'asta, iscritti, squadre, collegamenti Discord e ticket.
          <span class="live-dot" :class="{ 'live-dot--on': connesso }">
            {{ connesso ? 'Live connesso' : 'Riconnessione...' }}
          </span>
        </p>
      </div>
      <div class="scelta" role="tablist">
        <button type="button" :aria-pressed="scheda === 'asta'" @click="scheda = 'asta'">Regia asta</button>
        <button type="button" :aria-pressed="scheda === 'iscritti'" @click="scheda = 'iscritti'">Iscritti ({{ iscritti.length }})</button>
        <button type="button" :aria-pressed="scheda === 'squadre'" @click="scheda = 'squadre'">Squadre ({{ squadre.length }})</button>
        <button type="button" :aria-pressed="scheda === 'utenti'" @click="scheda = 'utenti'">Discord</button>
        <button type="button" :aria-pressed="scheda === 'ticket'" @click="scheda = 'ticket'">
          Ticket ({{ ticket.filter((t) => t.stato === 'aperto').length }})
        </button>
      </div>
    </div>

    <p v-if="messaggio" class="avviso-riga avviso-riga--ok" role="status">{{ messaggio }}</p>
    <p v-if="errore" class="avviso-riga avviso-riga--err" role="alert">{{ errore }}</p>

    <!-- ==================== REGIA ASTA ==================== -->
    <section v-if="scheda === 'asta'" class="blocco-pannello due">
      <div class="riquadro angoli">
        <div class="riga-spazio">
          <h2>Stato asta</h2>
          <span class="badge" :class="`badge--asta-${asta?.stato || 'attesa'}`">{{ asta?.stato || 'attesa' }}</span>
        </div>

        <div v-if="asta?.giocatore" class="asta-scheda">
          <span class="badge">Fascia {{ asta.giocatore.fascia }}</span>
          <h3 class="asta-nick">{{ asta.giocatore.nick }}</h3>
          <p class="piccolo muto">
            Offerta: <strong class="vinto">{{ asta.offertaAttuale }} Berlu</strong>
            <template v-if="squadraMigliore"> · {{ squadraMigliore.nome }}</template>
          </p>
        </div>
        <p v-else class="piccolo muto">Nessun giocatore chiamato. Scegli un iscritto disponibile qui sotto.</p>

        <div class="modulo" style="margin-top: var(--space-md)">
          <label class="campo">
            <span>Chiama un giocatore all'asta</span>
            <div class="azioni-inline">
              <select v-model="iscrittoDaChiamare" class="cerca">
                <option value="">Seleziona giocatore disponibile...</option>
                <option v-for="i in (asta?.disponibili || [])" :key="i.id" :value="i.id">
                  [{{ i.fascia }}] {{ i.nick }} (seed {{ i.seeding ?? '-' }}, 1v1 {{ elo(i.elo1v1) }})
                </option>
              </select>
              <button type="button" class="btn" :disabled="occupato || !iscrittoDaChiamare" @click="chiamaGiocatore">Chiama</button>
            </div>
          </label>

          <div class="azioni-inline">
            <button type="button" class="btn" :disabled="occupato || !asta?.squadraMiglioreId" @click="aggiudicaAsta">
              Aggiudica ({{ asta?.offertaAttuale ?? 0 }})
            </button>
            <button type="button" class="btn btn--linea" :disabled="occupato || !asta?.offerte?.length" @click="annullaOfferta">
              Annulla ultima offerta
            </button>
          </div>

          <label class="campo">
            <span>Messaggio per l'overlay OBS (es. Pausa 5 minuti)</span>
            <input v-model="messaggioAsta" type="text" class="cerca" placeholder="Opzionale: testo mostrato su OBS" />
          </label>

          <div class="azioni-inline">
            <button type="button" class="btn btn--linea" :disabled="occupato" @click="impostaStatoAsta('in_corso')">Riprendi</button>
            <button type="button" class="btn btn--linea" :disabled="occupato" @click="impostaStatoAsta('pausa')">Metti in pausa</button>
            <button type="button" class="btn btn--linea" :disabled="occupato" @click="impostaStatoAsta('attesa')">Attesa</button>
            <button type="button" class="btn btn--linea" :disabled="occupato" @click="impostaStatoAsta('chiusa')">Chiudi asta</button>
          </div>
        </div>
      </div>

      <div class="riquadro angoli">
        <h2>Registra offerta per una squadra</h2>
        <p class="piccolo muto">Utile se un capitano offre a voce su Discord invece che dal pannello.</p>
        <form class="modulo" style="margin-top: var(--space-sm)" @submit.prevent="offertaAdmin">
          <label class="campo">
            <span>Squadra</span>
            <select v-model="squadraOffertaAdmin" class="cerca">
              <option value="">Scegli squadra...</option>
              <option v-for="s in (asta?.squadre || [])" :key="s.id" :value="s.id">
                {{ s.nome }} ({{ s.berluResidui }} Berlu · {{ s.nGiocatori }}/{{ asta?.maxRosa || 10 }})
              </option>
            </select>
          </label>
          <label class="campo">
            <span>Importo (Berlu)</span>
            <input v-model="importoOffertaAdmin" type="number" min="1" class="cerca" placeholder="Es. 25" />
          </label>
          <button type="submit" class="btn" :disabled="occupato || asta?.stato !== 'in_corso'">Registra offerta</button>
        </form>

        <div v-if="asta?.offerte?.length" class="sezione">
          <h3>Offerte su {{ asta.giocatore?.nick }}</h3>
          <ul class="offerte-log">
            <li v-for="o in asta.offerte" :key="o.id">
              <span><strong>{{ o.squadraNome }}</strong> <span class="muto piccolo">({{ o.creataDa }})</span></span>
              <span class="num vinto">{{ o.importo }} Berlu</span>
            </li>
          </ul>
        </div>
      </div>
    </section>

    <!-- ==================== ISCRITTI & SEEDING ==================== -->
    <section v-else-if="scheda === 'iscritti'" class="blocco-pannello">
      <div class="riquadro angoli">
        <div class="riga-spazio">
          <h2>Aggiungi iscritto</h2>
          <button type="button" class="btn btn--linea" :disabled="occupato || !iscritti.length" @click="autoSeeding">
            Calcola auto-seeding e fasce A/B/C
          </button>
        </div>

        <div class="strumenti">
          <input v-model="ricercaGiocatore" type="search" class="cerca" placeholder="Cerca giocatore dalla classifica Dito per compilare al volo..." />
        </div>
        <div v-if="giocatoriFiltrati.length" class="chip-lista" style="margin-bottom: var(--space-md)">
          <button
            v-for="g in giocatoriFiltrati"
            :key="g.playerId"
            type="button"
            class="chip chip--btn"
            @click="selezionaDaClassifica(g)"
          >
            <strong>{{ g.nick }}</strong>
            <span class="muto">1v1 {{ elo(g.elo1v1) }}</span>
          </button>
        </div>

        <form class="form-riga" @submit.prevent="salvaIscritto">
          <input v-model="nuovoIscritto.nick" type="text" class="cerca" required placeholder="Nick" />
          <select v-model="nuovoIscritto.fascia" class="cerca select-compatta">
            <option value="A">Fascia A</option>
            <option value="B">Fascia B</option>
            <option value="C">Fascia C</option>
          </select>
          <input v-model="nuovoIscritto.seeding" type="number" min="1" class="cerca select-compatta" placeholder="Seed" />
          <input v-model="nuovoIscritto.prezzoBase" type="number" min="1" class="cerca select-compatta" placeholder="Base" />
          <button type="submit" class="btn" :disabled="occupato">Aggiungi</button>
        </form>

        <div class="tabella" style="margin-top: var(--space-lg)">
          <table>
            <thead>
              <tr>
                <th class="num">Seed</th>
                <th>Fascia</th>
                <th>Nick</th>
                <th class="num">1v1</th>
                <th class="num">ELO int.</th>
                <th>Squadra</th>
                <th class="num">Prezzo</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="i in iscritti" :key="i.id">
                <td class="num pos">{{ i.seeding ?? '—' }}</td>
                <td><span class="badge">{{ i.fascia }}</span></td>
                <td class="forte">{{ i.nick }}</td>
                <td class="num">{{ elo(i.elo1v1) }}</td>
                <td class="num">{{ elo(i.eloInterno) }}</td>
                <td class="tenue">{{ i.squadraNome || 'Disponibile' }}</td>
                <td class="num">{{ i.prezzoAsta ?? `base ${i.prezzoBase}` }}</td>
                <td class="num">
                  <button type="button" class="btn btn--linea btn--sm" :disabled="occupato" @click="rimuoviIscritto(i.id)">Togli</button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </section>

    <!-- ==================== SQUADRE & CAPITANI ==================== -->
    <section v-else-if="scheda === 'squadre'" class="blocco-pannello">
      <div class="riquadro angoli">
        <h2>Squadre e capitani</h2>
        <form class="form-riga" style="margin-bottom: var(--space-lg)" @submit.prevent="salvaSquadra">
          <input v-model="nuovaSquadra.nome" type="text" class="cerca" required placeholder="Nome squadra (es. Team Pier)" />
          <input v-model="nuovaSquadra.capitanoNome" type="text" class="cerca" required placeholder="Nome capitano" />
          <input v-model="nuovaSquadra.capitanoDiscordId" type="text" class="cerca" placeholder="Discord ID capitano (opz.)" />
          <input v-model="nuovaSquadra.berluIniziali" type="number" min="1" class="cerca select-compatta" placeholder="Berlu" />
          <input v-model="nuovaSquadra.ordineChiamata" type="number" min="1" class="cerca select-compatta" placeholder="Ordine" />
          <input v-model="nuovaSquadra.canaleDiscordUrl" type="url" class="cerca" placeholder="Link canale Discord (opz.)" />
          <button type="submit" class="btn" :disabled="occupato">Crea squadra</button>
        </form>

        <div class="tabella">
          <table>
            <thead>
              <tr>
                <th class="num">#</th>
                <th>Squadra</th>
                <th>Capitano</th>
                <th>Discord ID</th>
                <th class="num">Rosa</th>
                <th class="num">Berlu</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="s in squadre" :key="s.id">
                <td class="num pos">{{ s.ordineChiamata }}</td>
                <td class="forte">{{ s.nome }}</td>
                <td>{{ s.capitanoNome }}</td>
                <td class="piccolo muto">{{ s.capitanoDiscordId || '—' }}</td>
                <td class="num">{{ s.nGiocatori }}/10</td>
                <td class="num forte">{{ s.berluResidui }} / {{ s.berluIniziali }}</td>
                <td class="num">
                  <button type="button" class="btn btn--linea btn--sm" :disabled="occupato" @click="eliminaSquadra(s.id)">Elimina</button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </section>

    <!-- ==================== UTENTI DISCORD & RUOLI ==================== -->
    <section v-else-if="scheda === 'utenti'" class="blocco-pannello">
      <div class="riquadro angoli">
        <h2>Utenti Discord connessi</h2>
        <p class="piccolo muto">Verifica o modifica il collegamento tra l'account Discord e il profilo della classifica Dito.</p>
        <div class="tabella" style="margin-top: var(--space-md)">
          <table>
            <thead>
              <tr>
                <th>Discord</th>
                <th>ID</th>
                <th>Giocatore collegato</th>
                <th>Stato</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="u in utentiDiscord" :key="u.discordId">
                <td class="forte">{{ u.discordNome }}</td>
                <td class="piccolo muto">{{ u.discordId }}</td>
                <td>
                  <select v-model="u.playerId" class="cerca select-compatta">
                    <option :value="null">— nessuno —</option>
                    <option v-for="g in giocatori" :key="g.playerId" :value="g.playerId">{{ g.nick }}</option>
                  </select>
                </td>
                <td class="piccolo" :class="u.verificato ? 'vinto' : 'muto'">{{ u.verificato ? 'Verificato' : 'Da confermare' }}</td>
                <td class="num">
                  <button type="button" class="btn btn--linea btn--sm" :disabled="occupato" @click="verificaUtente(u, true)">Salva e verifica</button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <div v-if="sessione.configurazione" class="config" style="margin-top: var(--space-xl)">
          <strong>Dati server e ruoli Discord (per la configurazione)</strong>
          <dl>
            <dt>Server</dt>
            <dd>{{ sessione.configurazione.guildId || 'non trovato' }}</dd>
            <dt>Il tuo ID</dt>
            <dd>{{ sessione.configurazione.discordId }}</dd>
            <dt>I tuoi ruoli</dt>
            <dd>{{ sessione.configurazione.ruoliDiscord.join(', ') || 'nessuno' }}</dd>
          </dl>
        </div>
      </div>
    </section>

    <!-- ==================== TICKET ==================== -->
    <section v-else-if="scheda === 'ticket'" class="blocco-pannello">
      <div class="riquadro angoli">
        <h2>Gestione ticket</h2>
        <p v-if="!ticket.length" class="muto piccolo">Nessun ticket aperto.</p>
        <ul v-else class="ticket-lista">
          <li v-for="t in ticket" :key="t.id" class="ticket-item">
            <div class="riga-spazio">
              <strong>#{{ t.id }} · {{ t.titolo }}</strong>
              <span class="badge" :class="`badge--${t.stato}`">{{ t.stato }}</span>
            </div>
            <p class="piccolo muto">{{ t.autoreNome }} · {{ t.tipo }} · {{ data(t.creatoIl) }}</p>
            <p class="piccolo">{{ t.descrizione }}</p>
            <div class="form-riga" style="margin-top: var(--space-sm)">
              <select v-model="bozzeTicket[t.id].stato" class="cerca select-compatta">
                <option value="aperto">Aperto</option>
                <option value="in_corso">In lavorazione</option>
                <option value="risolto">Risolto</option>
                <option value="respinto">Respinto</option>
              </select>
              <input v-model="bozzeTicket[t.id].rispostaAdmin" type="text" class="cerca" placeholder="Risposta per il giocatore..." />
              <button type="button" class="btn btn--sm" :disabled="occupato" @click="salvaTicket(t.id)">Salva</button>
            </div>
          </li>
        </ul>
      </div>
    </section>
  </div>
</template>
