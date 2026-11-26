<script setup>
// «La tua pagina», parte degli admin: cosa aspetta una decisione (iscrizioni da approvare, ticket aperti) e come sta l'asta.
// Dati: /api/admin/utenti-iscritti, /api/ticket?tutti=true, /api/admin/flusso-asta, /api/asta/stato. Il lavoro vero si fa in /admin.
import { ref, computed, onMounted } from 'vue'
import { api } from '../../api.js'
import StatoDati from '../StatoDati.vue'
import NumeroVivo from '../NumeroVivo.vue'

const iscritti = ref([])
const ticket = ref([])
const capitani = ref([])
const astaAttiva = ref(false)
const caricamento = ref(true)
const errore = ref('')

async function carica() {
  caricamento.value = true
  errore.value = ''
  try {
    const [i, t, c, s] = await Promise.all([api('admin/utenti-iscritti'), api('ticket?tutti=true'), api('admin/flusso-asta'), api('asta/stato')])
    iscritti.value = i
    ticket.value = t
    capitani.value = c
    astaAttiva.value = !!s.attiva
  } catch (e) {
    errore.value = e.message
  } finally {
    caricamento.value = false
  }
}
onMounted(carica)

const daApprovare = computed(() => iscritti.value.filter((i) => i.statoValidazione === 'in_validazione'))
const ticketAperti = computed(() => ticket.value.filter((t) => t.stato === 'aperto' || t.stato === 'in_corso'))
const senzaOrdine = computed(() => capitani.value.filter((c) => !c.ordine).length)
</script>

<template>
  <section class="io-sezione" aria-labelledby="io-admin">
    <div class="io-sezione__testa">
      <h2 id="io-admin">Da fare come admin</h2>
      <RouterLink to="/admin" class="btn btn--linea">Apri l'admin</RouterLink>
    </div>
    <StatoDati :caricamento="caricamento" :errore="errore" :vuoto="false" :righe="2" @riprova="carica">
      <dl class="misure io-misure">
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
      <ul v-if="daApprovare.length" class="io-elenco">
        <li v-for="i in daApprovare.slice(0, 6)" :key="i.idDiscord">
          <span>{{ i.nickname || i.idDiscord }}</span>
          <span class="muto piccolo">aspetta l'approvazione</span>
        </li>
      </ul>
    </StatoDati>
  </section>
</template>
