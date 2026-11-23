<script setup>
import { computed, watchEffect } from 'vue'
import { useRoute } from 'vue-router'
import { trovaRuolo } from '../ruoli.js'
import { sessione, puoEntrare, linkAccesso } from '../sessione.js'

import PannelloGiocatore from '../componenti/pannelli/PannelloGiocatore.vue'
import PannelloCapitano from '../componenti/pannelli/PannelloCapitano.vue'
import PannelloAdmin from '../componenti/pannelli/PannelloAdmin.vue'
import PannelloStreamer from '../componenti/pannelli/PannelloStreamer.vue'

const props = defineProps({ ruolo: { type: String, required: true } })
const route = useRoute()
const r = computed(() => trovaRuolo(props.ruolo))
const entra = computed(() => sessione.autenticato && puoEntrare(props.ruolo))

const componenti = {
  giocatore: PannelloGiocatore,
  capitano: PannelloCapitano,
  admin: PannelloAdmin,
  streamer: PannelloStreamer,
}
const vistaAttiva = computed(() => componenti[props.ruolo] || null)

watchEffect(() => {
  if (r.value) document.title = `${r.value.nome}, FantaAge`
})
</script>

<template>
  <div v-if="!r" class="avviso">
    <strong>Questo pannello non esiste.</strong>
    <RouterLink to="/pannelli" class="btn btn--linea">Torna ai pannelli</RouterLink>
  </div>
  <template v-else>
    <nav class="briciole" aria-label="Percorso">
      <RouterLink to="/pannelli">Pannelli</RouterLink>
      <span aria-hidden="true">/</span>
      <span aria-current="page">{{ r.nome }}</span>
    </nav>

    <component :is="vistaAttiva" v-if="entra && vistaAttiva" />

    <div v-else class="ruolo">
      <section aria-labelledby="titolo-ruolo">
        <div class="testa-pagina">
          <h1 id="titolo-ruolo">{{ r.nome }}</h1>
          <p>{{ r.intro }}</p>
        </div>
        <h2>Cosa troverai qui</h2>
        <ul class="funzioni">
          <li v-for="[titolo, testo] in r.funzioni" :key="titolo">
            <strong>{{ titolo }}</strong>
            <span>{{ testo }}</span>
          </li>
        </ul>
      </section>

      <aside class="accesso angoli" aria-labelledby="titolo-accesso" aria-live="polite">
        <template v-if="!sessione.caricata">
          <h2 id="titolo-accesso">Controllo l'accesso</h2>
          <p>Un attimo, leggo chi sei.</p>
        </template>

        <template v-else-if="sessione.autenticato">
          <h2 id="titolo-accesso">Non hai questo ruolo</h2>
          <p v-if="!sessione.membro">
            Il tuo account Discord non risulta nel server di Age of Empires Italia. Entra nel server e poi accedi di nuovo.
          </p>
          <p v-else>
            Sul server di AoE Italia non hai il ruolo che apre il pannello {{ r.nome }}. Se pensi sia un errore, scrivi a un admin.
          </p>
          <a class="btn btn--linea" href="/api/auth/esci">Esci e cambia account</a>
        </template>

        <template v-else>
          <h2 id="titolo-accesso">Si entra con Discord</h2>
          <p>Il sito riconosce il tuo ruolo dal server Discord di AoE Italia, senza password nuove.</p>
          <a v-if="sessione.loginAttivo" class="btn" :href="linkAccesso(route.fullPath)">Accedi con Discord</a>
          <template v-else>
            <button type="button" class="btn" aria-disabled="true">Accedi con Discord</button>
            <p>Il login non è ancora attivo: manca l'ultimo collegamento con Discord.</p>
          </template>
          <p>Intanto classifica, partite e civiltà sono aperte a tutti.</p>
        </template>
      </aside>
    </div>
  </template>
</template>
