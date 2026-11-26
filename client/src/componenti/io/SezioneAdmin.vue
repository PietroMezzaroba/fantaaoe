<script setup>
// «La tua pagina», parte degli admin: cosa aspetta una decisione (iscrizioni, ticket, abbinamenti Discord <-> nick)
// e chi è online sul sito in questo momento (in diretta via SignalR). Il lavoro di dettaglio si fa in /admin.
import { ref, computed, onMounted, onBeforeUnmount } from 'vue'
import { api } from '../../api.js'
import { ascoltaOnline } from '../../astaLive.js'
import { nomePagina } from '../../pagineSito.js'
import StatoDati from '../StatoDati.vue'
import NumeroVivo from '../NumeroVivo.vue'

const iscritti = ref([])
const ticket = ref([])
const capitani = ref([])
const utentiDiscord = ref([])
const online = ref(null)
const astaAttiva = ref(false)
const caricamento = ref(true)
const errore = ref('')

async function carica() {
  caricamento.value = true
  errore.value = ''
  try {
    const [i, t, c, s, u, o] = await Promise.all([
      api('admin/utenti-iscritti'),
      api('ticket?tutti=true'),
      api('admin/flusso-asta'),
      api('asta/stato'),
      api('admin/utenti-discord').catch(() => []),
      api('admin/online').catch(() => null),
    ])
    iscritti.value = i
    ticket.value = t
    capitani.value = c
    astaAttiva.value = !!s.attiva
    utentiDiscord.value = u
    if (o) online.value = o
  } catch (e) {
    errore.value = e.message
  } finally {
    caricamento.value = false
  }
}

let smettiOnline = () => {}
onMounted(() => {
  carica()
  smettiOnline = ascoltaOnline((dati) => { online.value = dati })
})
onBeforeUnmount(() => smettiOnline())

const daApprovare = computed(() => iscritti.value.filter((i) => i.statoValidazione === 'in_validazione'))
const ticketAperti = computed(() => ticket.value.filter((t) => t.stato === 'aperto' || t.stato === 'in_corso'))
const senzaOrdine = computed(() => capitani.value.filter((c) => !c.ordine).length)

const associazioniDaControllare = computed(() => {
  const conteggi = new Map()
  for (const u of utentiDiscord.value) {
    if (u.playerId != null) conteggi.set(u.playerId, (conteggi.get(u.playerId) ?? 0) + 1)
  }
  return utentiDiscord.value.filter(
    (u) => (!u.verificato && (u.collegatoDa === 'thomuggia_simile' || u.collegatoDa === 'utente'))
      || (u.playerId != null && (conteggi.get(u.playerId) ?? 0) > 1),
  ).length
})
const senzaNick = computed(() => utentiDiscord.value.filter((u) => u.playerId == null).length)
const utentiOnline = computed(() => online.value?.utenti ?? [])
</script>

<template>
  <section class="io-sezione" aria-labelledby="io-admin">
    <div class="io-sezione__testa">
      <h2 id="io-admin">Da fare come admin</h2>
      <div class="io-admin-azioni">
        <RouterLink to="/admin?tab=online" class="btn btn--linea">Chi è online</RouterLink>
        <RouterLink to="/admin?tab=associazioni" class="btn btn--linea">Discord ↔ Nick</RouterLink>
        <RouterLink to="/admin" class="btn btn--linea">Apri l'admin</RouterLink>
      </div>
    </div>
    <StatoDati :caricamento="caricamento" :errore="errore" :vuoto="false" :righe="2" @riprova="carica">
      <dl class="io-admin-misure">
        <div class="misura">
          <dt>Online adesso</dt>
          <dd :class="{ segnale: online?.totalePersone }"><NumeroVivo :valore="online?.totalePersone ?? 0" /></dd>
          <small v-if="online">{{ online.loggati }} con Discord, {{ online.ospiti }} ospiti</small>
          <small v-else>in diretta sul sito</small>
        </div>
        <div class="misura">
          <dt>Discord ↔ Nick da controllare</dt>
          <dd :class="{ segnale: associazioniDaControllare }"><NumeroVivo :valore="associazioniDaControllare" /></dd>
          <small>{{ senzaNick ? `${senzaNick} senza nick collegato` : 'tutti hanno un nick collegato' }}</small>
        </div>
        <div class="misura">
          <dt>Iscrizioni da approvare</dt>
          <dd :class="{ segnale: daApprovare.length }"><NumeroVivo :valore="daApprovare.length" /></dd>
          <small>su {{ iscritti.length }} iscritti</small>
        </div>
        <div class="misura">
          <dt>Ticket da gestire</dt>
          <dd :class="{ segnale: ticketAperti.length }"><NumeroVivo :valore="ticketAperti.length" /></dd>
          <small>aperti o in lavorazione</small>
        </div>
        <div class="misura">
          <dt>Capitani</dt>
          <dd><NumeroVivo :valore="capitani.length" /></dd>
          <small>{{ senzaOrdine ? `${senzaOrdine} senza posto nell'ordine` : 'ordine di chiamata completo' }}</small>
        </div>
        <div class="misura">
          <dt>Asta</dt>
          <dd class="io-civ">{{ astaAttiva ? 'aperta' : 'chiusa' }}</dd>
          <small>si apre dalla scheda Configurazioni</small>
        </div>
      </dl>

      <div v-if="utentiOnline.length" class="io-admin-blocco">
        <div class="riga-spazio">
          <h3 class="io-titoletto">Connessi adesso</h3>
          <RouterLink to="/admin?tab=online" class="piccolo">Apri dashboard completa</RouterLink>
        </div>
        <ul class="io-elenco">
          <li v-for="u in utentiOnline.slice(0, 6)" :key="u.idDiscord">
            <span>
              <strong>{{ u.nome }}</strong>
              <span v-if="u.playerNick" class="muto piccolo"> ({{ u.playerNick }})</span>
            </span>
            <span class="muto piccolo">{{ nomePagina(u.pagina) }}</span>
          </li>
        </ul>
      </div>

      <div v-if="daApprovare.length" class="io-admin-blocco">
        <div class="riga-spazio">
          <h3 class="io-titoletto">Iscrizioni in attesa</h3>
          <RouterLink to="/admin?tab=iscritti" class="piccolo">Gestisci iscritti</RouterLink>
        </div>
        <ul class="io-elenco">
          <li v-for="i in daApprovare.slice(0, 6)" :key="i.idDiscord">
            <span>{{ i.nickname || i.idDiscord }}</span>
            <span class="muto piccolo">aspetta l'approvazione</span>
          </li>
        </ul>
      </div>
    </StatoDati>
  </section>
</template>

<style scoped>
.io-admin-azioni { display: flex; flex-wrap: wrap; gap: var(--space-xs); }
.io-admin-misure {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 0 var(--space-lg);
  margin: var(--space-xs) 0 var(--space-sm);
  border-top: var(--rule-hair) solid var(--color-rule);
}
.io-admin-misure .misura {
  padding: var(--space-md) 0;
  border-left: 0;
  border-bottom: var(--rule-hair) solid var(--color-rule);
}
.io-admin-blocco { display: grid; gap: var(--space-xs); }
@media (max-width: 820px) {
  .io-admin-misure { grid-template-columns: repeat(2, minmax(0, 1fr)); }
}
@media (max-width: 520px) {
  .io-admin-misure { grid-template-columns: minmax(0, 1fr); }
}
</style>
