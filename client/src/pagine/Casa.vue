<script setup>
import { ref, onMounted } from 'vue'
import { api, elo, fa } from '../api.js'
import { consultazione, ruoli } from '../ruoli.js'
import StatoDati from '../componenti/StatoDati.vue'
import { sessione, puoEntrare } from '../sessione.js'

const etichetta = (chiave) => {
  if (!sessione.autenticato) return 'Con Discord'
  return puoEntrare(chiave) ? 'Entra' : 'Non è il tuo ruolo'
}

const primi = ref([])
const attivi = ref(0)
const caricamento = ref(true)
const errore = ref('')

async function carica() {
  caricamento.value = true
  errore.value = ''
  try {
    const tutti = await api('giocatori')
    attivi.value = tutti.length
    primi.value = tutti.slice(0, 10)
  } catch (e) {
    errore.value = e.message
  } finally {
    caricamento.value = false
  }
}
onMounted(carica)
</script>

<template>
  <div class="casa">
    <section aria-labelledby="titolo-pannelli">
      <div class="casa__intro">
        <h1 id="titolo-pannelli">I pannelli del torneo</h1>
        <p>Scegli il tuo pannello. Classifica e statistiche dei giocatori sono aperte a tutti.</p>
      </div>

      <ul class="porte">
        <li class="porta">
          <RouterLink :to="consultazione.link">
            <span class="porta__nome">{{ consultazione.nome }}</span>
            <span class="porta__cosa">{{ consultazione.cosa }}</span>
            <span class="porta__accesso porta__accesso--libero">{{ consultazione.accesso }}</span>
          </RouterLink>
        </li>
        <li v-for="r in ruoli" :key="r.chiave" class="porta">
          <RouterLink :to="`/pannello/${r.chiave}`">
            <span class="porta__nome">{{ r.nome }}</span>
            <span class="porta__cosa">{{ r.cosa }}</span>
            <span class="porta__accesso" :class="{ 'porta__accesso--tuo': sessione.autenticato && puoEntrare(r.chiave) }">
              {{ etichetta(r.chiave) }}
            </span>
          </RouterLink>
        </li>
      </ul>
    </section>

    <section class="tabellone angoli" aria-labelledby="titolo-tabellone">
      <div class="tabellone__testa">
        <h2 id="titolo-tabellone">Il tabellone</h2>
        <RouterLink to="/classifica" class="piccolo">Classifica completa</RouterLink>
      </div>
      <StatoDati :caricamento="caricamento" :errore="errore" :vuoto="!primi.length" :righe="10" @riprova="carica">
        <div class="tabella">
          <table>
            <caption class="visually-hidden">I primi dieci giocatori attivi per ELO interno</caption>
            <thead>
              <tr>
                <th scope="col"><span class="visually-hidden">Posizione</span></th>
                <th scope="col">Giocatore</th>
                <th scope="col" class="num">Interno</th>
                <th scope="col" class="num">1v1</th>
                <th scope="col" class="num">TG</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(g, i) in primi" :key="g.playerId">
                <td class="pos num">{{ i + 1 }}</td>
                <td class="nome"><RouterLink :to="`/giocatore/${g.playerId}`">{{ g.nick }}</RouterLink></td>
                <td class="num forte">{{ elo(g.eloInterno) }}</td>
                <td class="num tenue">{{ elo(g.elo1v1) }}</td>
                <td class="num tenue">{{ elo(g.eloTg) }}</td>
              </tr>
            </tbody>
          </table>
        </div>
        <div class="tabellone__piede">
          <span>{{ attivi }} giocatori attivi negli ultimi 90 giorni</span>
          <span v-if="primi[0]">ultimo Dito del primo: {{ fa(primi[0].ultimoDito) }}</span>
        </div>
      </StatoDati>
    </section>
  </div>
</template>
