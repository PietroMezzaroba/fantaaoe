<script setup>
// Dashboard admin "Persone online": chi sta navigando sul sito adesso (utenti connessi con Discord e ospiti anonimi),
// su quale pagina si trovano e chi è presente nell'asta. Dati iniziali da GET /api/admin/online e aggiornamenti in diretta
// da SignalR (/hubs/asta, gruppo "admin-online").
import { ref, computed, onMounted, onBeforeUnmount } from 'vue'
import { api, ora } from '../../api.js'
import { sessione } from '../../sessione.js'
import { ascoltaOnline } from '../../astaLive.js'
import { nomePagina, nomeRuolo, durataOnline } from '../../pagineSito.js'
import StatoDati from '../StatoDati.vue'
import NumeroVivo from '../NumeroVivo.vue'

const istantanea = ref(null)
const statoLive = ref('connessione')
const caricamento = ref(true)
const errore = ref('')
const filtroRuolo = ref('tutti')
const filtroPagina = ref('')
const cerca = ref('')
const adesso = ref(Date.now())

async function carica() {
  if (!istantanea.value) caricamento.value = true
  errore.value = ''
  try {
    istantanea.value = await api('admin/online')
  } catch (e) {
    errore.value = e.message
  } finally {
    caricamento.value = false
  }
}

let smetti = () => {}
let orologio = null

onMounted(() => {
  carica()
  smetti = ascoltaOnline({
    suOnline: (dati) => {
      istantanea.value = dati
      caricamento.value = false
      errore.value = ''
    },
    suStato: (s) => {
      statoLive.value = s
    },
  })
  orologio = setInterval(() => {
    adesso.value = Date.now()
  }, 15000)
})

onBeforeUnmount(() => {
  smetti()
  clearInterval(orologio)
})

const utenti = computed(() => istantanea.value?.utenti ?? [])
const perPagina = computed(() => istantanea.value?.perPagina ?? [])
const maxPagina = computed(() => Math.max(1, ...perPagina.value.map((p) => p.totale)))

const capitaniOnline = computed(() => utenti.value.filter((u) => u.ruoli?.includes('capitano')).length)
const adminOnline = computed(() => utenti.value.filter((u) => u.ruoli?.includes('admin')).length)
const streamerOnline = computed(() => utenti.value.filter((u) => u.ruoli?.includes('streamer')).length)
const inAstaOnline = computed(() => utenti.value.filter((u) => u.inAsta || u.pagine?.some((p) => p.startsWith('/asta'))).length)

const filtrati = computed(() => {
  const q = cerca.value.trim().toLowerCase()
  return utenti.value.filter((u) => {
    if (filtroRuolo.value === 'inAsta' && !(u.inAsta || u.pagine?.some((p) => p.startsWith('/asta')))) return false
    if (['capitano', 'admin', 'streamer'].includes(filtroRuolo.value) && !u.ruoli?.includes(filtroRuolo.value)) return false
    if (filtroPagina.value && !(u.pagine?.includes(filtroPagina.value) || u.pagina === filtroPagina.value)) return false
    if (!q) return true
    const campi = [u.nome, u.playerNick || '', nomePagina(u.pagina), u.pagina, ...(u.ruoli ?? [])]
    return campi.some((c) => c.toLowerCase().includes(q))
  })
})

function scegliPagina(percorso) {
  filtroPagina.value = filtroPagina.value === percorso ? '' : percorso
}

const etichettaStatoLive = computed(() => ({
  connesso: 'In diretta',
  connessione: 'Collegamento…',
  riconnessione: 'Riconnessione…',
  spento: 'Non in diretta',
}[statoLive.value] ?? 'In diretta'))

const iniziale = (nome) => (nome || '?').trim().charAt(0).toUpperCase()
</script>

<template>
  <div class="online">
    <div class="online__barra">
      <p class="muto piccolo online__stato" role="status">
        <span :class="statoLive === 'connesso' ? 'vinto' : 'muto'">{{ etichettaStatoLive }}</span>
        <span v-if="istantanea?.aggiornatoIl">Aggiornato alle {{ ora(istantanea.aggiornatoIl) }}</span>
      </p>
      <button type="button" class="btn btn--linea" @click="carica">Aggiorna</button>
    </div>

    <StatoDati :caricamento="caricamento && !istantanea" :errore="errore" :vuoto="!istantanea" :righe="5" @riprova="carica">
      <dl class="misure online__misure">
        <div class="misura">
          <dt>Online adesso</dt>
          <dd class="segnale"><NumeroVivo :valore="istantanea.totalePersone" /></dd>
          <small>{{ istantanea.totaleConnessioni }} {{ istantanea.totaleConnessioni === 1 ? 'scheda aperta' : 'schede aperte' }}</small>
        </div>
        <div class="misura">
          <dt>Utenti con Discord</dt>
          <dd><NumeroVivo :valore="istantanea.loggati" /></dd>
          <small>{{ capitaniOnline }} {{ capitaniOnline === 1 ? 'capitano' : 'capitani' }}</small>
        </div>
        <div class="misura">
          <dt>Ospiti anonimi</dt>
          <dd><NumeroVivo :valore="istantanea.ospiti" /></dd>
          <small>senza accesso Discord</small>
        </div>
        <div class="misura">
          <dt>Sull'asta ora</dt>
          <dd><NumeroVivo :valore="inAstaOnline" /></dd>
          <small>utenti loggati nelle pagine asta</small>
        </div>
      </dl>

      <section v-if="perPagina.length" class="online__sezione" aria-labelledby="online-pagine">
        <div class="online__sezione-testa">
          <h2 id="online-pagine">Dove sono adesso</h2>
          <button v-if="filtroPagina" type="button" class="btn btn--linea btn--sm" @click="filtroPagina = ''">Mostra tutte le pagine</button>
        </div>
        <ul class="pagine-lista">
          <li v-for="p in perPagina" :key="p.pagina">
            <button
              type="button"
              class="pagina-voce"
              :aria-pressed="filtroPagina === p.pagina"
              @click="scegliPagina(p.pagina)"
            >
              <span class="pagina-voce__nome">
                <strong>{{ nomePagina(p.pagina) }}</strong>
                <span class="muto piccolo">{{ p.pagina }}</span>
              </span>
              <span class="barretta pagina-voce__barra" aria-hidden="true">
                <span class="barretta__traccia">
                  <span :style="{ transform: `scaleX(${p.totale / maxPagina})` }"></span>
                </span>
              </span>
              <span class="pagina-voce__numeri num piccolo">
                <strong>{{ p.totale }}</strong>
                <span class="muto">({{ p.loggati }} loggati, {{ p.ospiti }} ospiti)</span>
              </span>
            </button>
          </li>
        </ul>
      </section>

      <section class="online__sezione" aria-labelledby="online-utenti">
        <div class="online__sezione-testa">
          <h2 id="online-utenti">Utenti connessi</h2>
          <span class="muto piccolo">{{ filtrati.length }} su {{ utenti.length }}</span>
        </div>

        <div class="strumenti online__strumenti">
          <div class="scelta" role="group" aria-label="Filtra utenti online">
            <button type="button" :aria-pressed="filtroRuolo === 'tutti'" @click="filtroRuolo = 'tutti'">Tutti ({{ utenti.length }})</button>
            <button type="button" :aria-pressed="filtroRuolo === 'capitano'" @click="filtroRuolo = 'capitano'">Capitani ({{ capitaniOnline }})</button>
            <button type="button" :aria-pressed="filtroRuolo === 'admin'" @click="filtroRuolo = 'admin'">Admin ({{ adminOnline }})</button>
            <button type="button" :aria-pressed="filtroRuolo === 'streamer'" @click="filtroRuolo = 'streamer'">Streamer ({{ streamerOnline }})</button>
            <button type="button" :aria-pressed="filtroRuolo === 'inAsta'" @click="filtroRuolo = 'inAsta'">In asta ({{ inAstaOnline }})</button>
          </div>
          <input
            v-model="cerca"
            type="search"
            class="cerca"
            placeholder="Cerca nome Discord, nick o pagina…"
            aria-label="Cerca tra gli utenti online"
          />
        </div>

        <p v-if="!utenti.length" class="muto online__vuoto">
          Nessun utente con login Discord connesso in questo momento. Chi entra sul sito compare qui da solo.
        </p>
        <p v-else-if="!filtrati.length" class="muto online__vuoto">
          Nessun utente online corrisponde ai filtri attivi.
        </p>

        <TransitionGroup v-else tag="ul" name="online-riga" class="utenti-online">
          <li v-for="u in filtrati" :key="u.idDiscord" class="utente-riga">
            <div class="utente-riga__identita">
              <img v-if="u.avatar" class="utente-riga__avatar" :src="u.avatar" alt="" width="36" height="36" />
              <span v-else class="utente-riga__avatar utente-riga__avatar--iniziale" aria-hidden="true">{{ iniziale(u.nome) }}</span>
              <div class="utente-riga__nomi">
                <div class="utente-riga__titolo">
                  <strong>{{ u.nome }}</strong>
                  <span v-if="u.idDiscord === sessione.discordId" class="etichetta etichetta--accento">Tu</span>
                  <span v-if="u.inAsta" class="etichetta etichetta--ok">Presente in asta</span>
                </div>
                <div class="piccolo">
                  <RouterLink v-if="u.playerId && u.playerNick" :to="`/giocatore/${u.playerId}`">{{ u.playerNick }}</RouterLink>
                  <span v-else class="muto">Nick classifica non collegato</span>
                </div>
              </div>
            </div>

            <div class="utente-riga__ruoli">
              <span v-for="r in u.ruoli" :key="r" class="etichetta">{{ nomeRuolo(r) }}</span>
              <span v-if="!u.ruoli?.length" class="muto piccolo">Nessun ruolo</span>
            </div>

            <div class="utente-riga__pagina">
              <span>{{ nomePagina(u.pagina) }}</span>
              <span class="muto piccolo">{{ u.pagina }}</span>
              <span v-if="u.schede > 1" class="muto piccolo" :title="u.pagine.map(nomePagina).join(', ')">
                {{ u.schede }} schede aperte
              </span>
            </div>

            <div class="utente-riga__tempo num piccolo" :title="`Connesso dalle ${ora(u.connessoDal)}`">
              <span>da {{ durataOnline(u.connessoDal, adesso) }}</span>
              <span class="muto">alle {{ ora(u.connessoDal) }}</span>
            </div>
          </li>
        </TransitionGroup>
      </section>
    </StatoDati>
  </div>
</template>

<style scoped>
.online { display: grid; grid-template-columns: minmax(0, 1fr); gap: var(--space-lg); min-width: 0; }
.online__barra { display: flex; flex-wrap: wrap; justify-content: space-between; align-items: center; gap: var(--space-sm); }
.online__stato { display: flex; flex-wrap: wrap; gap: var(--space-md); margin: 0; }
.online__misure { margin-block: 0; }
.online__sezione { display: grid; grid-template-columns: minmax(0, 1fr); gap: var(--space-sm); min-width: 0; }
.online__sezione-testa {
  display: flex; flex-wrap: wrap; justify-content: space-between; align-items: baseline; gap: var(--space-sm);
  padding-bottom: var(--space-xs); border-bottom: var(--rule-hair) solid var(--color-rule);
}
.online__sezione-testa h2 { margin: 0; font-size: var(--text-lg); text-wrap: balance; }
.online__strumenti { margin-block: 0; min-width: 0; }
.online__strumenti .scelta { overflow-x: auto; max-width: 100%; min-width: 0; }
.online__vuoto { margin: var(--space-md) 0; }

.pagine-lista { list-style: none; margin: 0; padding: 0; }
.pagine-lista li { border-bottom: var(--rule-hair) solid var(--color-rule); }
.pagina-voce {
  width: 100%; min-height: 44px; display: grid; grid-template-columns: minmax(0, 16rem) minmax(0, 1fr) auto;
  align-items: center; gap: var(--space-md); padding: var(--space-xs) var(--space-xs);
  border: 0; background: transparent; color: inherit; font: inherit; text-align: left; cursor: pointer;
  border-radius: var(--radius-card);
  transition: background-color var(--dur-short) var(--ease-out), transform var(--dur-micro) var(--ease-out);
}
.pagina-voce:hover { background: var(--color-paper-3); }
.pagina-voce:active { transform: translateY(1px); }
.pagina-voce:disabled { opacity: 0.55; cursor: not-allowed; transform: none; }
.pagina-voce[aria-pressed="true"] {
  background: color-mix(in oklch, var(--color-accent) 16%, var(--color-paper-2));
  box-shadow: inset 3px 0 0 var(--color-accent);
}
.pagina-voce:focus-visible { outline: 2px solid var(--color-focus); outline-offset: -2px; }
.pagina-voce__nome { display: flex; align-items: baseline; gap: var(--space-xs); min-width: 0; }
.pagina-voce__nome strong { white-space: nowrap; }
.pagina-voce__nome span { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.pagina-voce__barra { min-width: 4rem; }
.pagina-voce__barra .barretta__traccia span { background: var(--color-accent); transition: transform var(--dur-short) var(--ease-out); }
.pagina-voce__numeri { display: flex; align-items: baseline; gap: var(--space-xs); white-space: nowrap; }

.utenti-online { list-style: none; margin: 0; padding: 0; border-top: var(--rule-hair) solid var(--color-rule); }
.utente-riga {
  display: grid; grid-template-columns: minmax(0, 2.2fr) minmax(0, 1.3fr) minmax(0, 1.5fr) auto;
  align-items: center; gap: var(--space-md); padding: var(--space-sm) var(--space-xs);
  border-bottom: var(--rule-hair) solid var(--color-rule);
  transition: background-color var(--dur-short) var(--ease-out);
}
.utente-riga:hover { background: var(--color-paper-3); }
.utente-riga__identita { display: flex; align-items: center; gap: var(--space-sm); min-width: 0; }
.utente-riga__avatar { width: 36px; height: 36px; border-radius: 50%; flex-shrink: 0; object-fit: cover; }
.utente-riga__avatar--iniziale {
  display: inline-flex; align-items: center; justify-content: center;
  background: var(--color-paper-3); color: var(--color-ink-2); font-size: var(--text-sm); font-weight: 400;
}
.utente-riga__nomi { display: grid; gap: var(--space-3xs); min-width: 0; }
.utente-riga__titolo { display: flex; flex-wrap: wrap; align-items: center; gap: var(--space-xs); min-width: 0; }
.utente-riga__titolo strong { overflow-wrap: anywhere; }
.utente-riga__ruoli { display: flex; flex-wrap: wrap; gap: var(--space-2xs); }
.utente-riga__pagina { display: grid; gap: var(--space-3xs); min-width: 0; }
.utente-riga__pagina span { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.utente-riga__tempo { display: grid; gap: var(--space-3xs); justify-items: end; white-space: nowrap; }

.etichetta {
  display: inline-flex; align-items: center; padding: var(--space-3xs) var(--space-xs);
  border-radius: var(--radius-pill); background: var(--color-paper-3); color: var(--color-ink-2);
  font-size: var(--text-xs); line-height: 1.25; white-space: nowrap;
}
.etichetta--accento {
  background: color-mix(in oklch, var(--color-accent) 20%, var(--color-paper-3));
  color: var(--color-ink);
  box-shadow: inset 0 0 0 var(--rule-hair) var(--color-accent);
}
.etichetta--ok {
  background: color-mix(in oklch, var(--color-success) 18%, var(--color-paper-3));
  color: var(--color-ink);
  box-shadow: inset 0 0 0 var(--rule-hair) var(--color-success);
}

.online-riga-move,
.online-riga-enter-active { transition: transform var(--dur-short) var(--ease-out); }
.online-riga-enter-from { transform: translateY(-6px); }

@media (prefers-reduced-motion: reduce) {
  .online-riga-move, .online-riga-enter-active, .pagina-voce, .pagina-voce__barra .barretta__traccia span { transition: none; }
}

@media (max-width: 820px) {
  .online__misure { grid-template-columns: repeat(2, minmax(0, 1fr)); }
  .online__misure .misura + .misura { border-left: 0; padding-left: 0; }
  .online__misure .misura { border-bottom: var(--rule-hair) solid var(--color-rule); }
  .pagina-voce { grid-template-columns: minmax(0, 1fr) auto; gap: var(--space-xs); }
  .pagina-voce__barra { display: none; }
  .pagina-voce__nome span { display: none; }
  .pagina-voce__numeri .muto { display: none; }
  .utente-riga { grid-template-columns: minmax(0, 1fr); gap: var(--space-xs); }
  .utente-riga__tempo { justify-items: start; display: flex; gap: var(--space-sm); }
}

@media (max-width: 420px) {
  .online__misure { grid-template-columns: minmax(0, 1fr); }
}
</style>
