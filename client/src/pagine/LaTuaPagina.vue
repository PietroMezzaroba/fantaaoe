<script setup>
// «La tua pagina» (/io): una sola pagina personale che si compone in base a chi sei.
//  - capitano: la tua squadra, i Berlu, il turno di chiamata, la rosa e quando possono giocare (SezioneCapitano);
//  - streamer: il bottone per gestire l'asta;
//  - admin: cosa aspetta una decisione (SezioneAdmin);
//  - chiunque sia connesso: iscrizione, squadra, profilo della classifica, sere libere e ticket (SezioneGiocatore);
//  - chi non è connesso: l'invito ad accedere con Discord.
// Prende il posto dei vecchi pannelli (/pannelli, /pannello/:ruolo), che leggevano tabelle che non esistono più.
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { sessione, linkAccesso, accedi } from '../sessione.js'
import SezioneCapitano from '../componenti/io/SezioneCapitano.vue'
import SezioneAdmin from '../componenti/io/SezioneAdmin.vue'
import SezioneGiocatore from '../componenti/io/SezioneGiocatore.vue'

const route = useRoute()
const ha = (r) => sessione.ruoli.includes(r)
const eCapitano = computed(() => ha('capitano'))
const eAdmin = computed(() => ha('admin'))
const eStreamer = computed(() => ha('streamer'))

// i ruoli detti a parole: «Sei capitano e admin.»
const chiSei = computed(() => {
  const nomi = [eCapitano.value && 'capitano', eStreamer.value && 'streamer', eAdmin.value && 'admin'].filter(Boolean)
  if (!nomi.length) return 'Qui trovi la tua iscrizione, la tua squadra e le sere in cui puoi giocare.'
  const elenco = nomi.length === 1 ? nomi[0] : `${nomi.slice(0, -1).join(', ')} e ${nomi.at(-1)}`
  return `Sei ${elenco}: qui sopra trovi quello che serve a te, sotto la tua parte da giocatore.`
})
</script>

<template>
  <div class="io">
    <div v-if="!sessione.caricata" class="scheletro" aria-label="Caricamento"><span style="--w: 40%"></span><span style="--w: 70%"></span></div>

    <section v-else-if="!sessione.autenticato" class="io-ospite" aria-labelledby="io-ospite">
      <h1 id="io-ospite">La tua pagina</h1>
      <p class="lead">Accedi con Discord: se sei capitano vedi la tua squadra, se giochi la tua iscrizione e le sere libere.</p>
      <a v-if="sessione.loginAttivo || sessione.loginSviluppo" class="btn btn--grande" :href="linkAccesso(route.fullPath)" @click="accedi">Accedi con Discord</a>
      <p v-else class="muto">Il login con Discord non è ancora attivo.</p>
    </section>

    <template v-else>
      <header class="io-testa">
        <img v-if="sessione.avatar" class="io-testa__foto" :src="sessione.avatar" alt="" width="56" height="56" />
        <div class="io-testa__testo">
          <h1>Ciao, {{ sessione.nome }}</h1>
          <p class="muto">{{ chiSei }}</p>
        </div>
        <RouterLink to="/profilo" class="btn btn--linea io-testa__impostazioni">Impostazioni</RouterLink>
      </header>

      <SezioneCapitano v-if="eCapitano" />
      <section v-if="eStreamer" class="io-sezione" aria-labelledby="io-streamer">
        <div class="io-sezione__testa">
          <h2 id="io-streamer">In diretta</h2>
          <RouterLink to="/asta/gestisci" class="btn">Gestisci l'asta</RouterLink>
        </div>
        <p class="muto">Da lì termini le offerte, metti in pausa il timer e avvii la chiamata successiva.</p>
      </section>
      <SezioneAdmin v-if="eAdmin" />
      <SezioneGiocatore />
    </template>
  </div>
</template>

<style>
/* «La tua pagina» e le sue sezioni (componenti/io/): classi io-*, colori e misure dai token del tema */
.io { display: grid; gap: var(--space-2xl); }
.io-ospite { display: grid; gap: var(--space-md); justify-items: start; max-width: 40rem; padding-block: var(--space-2xl); }
.io-testa { display: grid; grid-template-columns: auto minmax(0, 1fr) auto; gap: var(--space-md); align-items: center; }
.io-testa__foto { width: 56px; height: 56px; border-radius: 50%; }
.io-testa__testo { display: grid; gap: var(--space-2xs); min-width: 0; }
.io-testa__testo h1 { margin: 0; overflow-wrap: break-word; min-width: 0; }
.io-testa__testo p { margin: 0; max-width: 60ch; }
.io-sezione { display: grid; gap: var(--space-md); }
.io-sezione__testa { display: flex; flex-wrap: wrap; justify-content: space-between; align-items: center; gap: var(--space-sm);
  padding-bottom: var(--space-xs); border-bottom: var(--rule-hair) solid var(--color-rule); }
.io-sezione__testa h2 { margin: 0; }
.io-titoletto { margin: 0 0 var(--space-xs); font-size: var(--text-lg); font-weight: 600; }
.io-misure { margin-block: var(--space-xs) var(--space-sm); }
.io-civ { font-size: var(--text-xl) !important; font-weight: 400 !important; }
.io-due { display: grid; grid-template-columns: minmax(0, 2fr) minmax(0, 3fr); gap: var(--space-xl); align-items: start; }
.io-riquadro { display: grid; gap: var(--space-xs); padding: var(--space-lg); background: var(--color-paper-2); border-radius: var(--radius-card); }
.io-riquadro + .io-riquadro { margin-top: var(--space-md); }
.io-riquadro--accento { box-shadow: inset 3px 0 0 var(--color-accent); }
.io-grande { margin: 0; font-size: var(--text-lg); }
.io-conferma { display: grid; gap: var(--space-xs); padding-bottom: var(--space-md); margin-bottom: var(--space-sm); border-bottom: var(--rule-hair) solid var(--color-rule); }
.io-conferma p { margin: 0; }

/* turno di chiamata del capitano */
.io-turno { display: flex; flex-wrap: wrap; align-items: center; gap: var(--space-sm); margin: 0; padding: var(--space-md) var(--space-lg);
  background: var(--color-paper-2); border-radius: var(--radius-card); }
.io-turno--tuo { background: color-mix(in oklch, var(--color-accent) 18%, var(--color-paper-2)); box-shadow: inset 3px 0 0 var(--color-accent); font-size: var(--text-lg); }
.io-turno .btn { margin-left: auto; }

/* rosa */
.io-rosa { list-style: none; margin: 0; padding: 0; border-top: var(--rule-hair) solid var(--color-rule); }
.io-rosa li { display: grid; grid-template-columns: minmax(0, 1fr) auto 3.5rem 5.5rem; gap: var(--space-sm); align-items: baseline;
  padding: var(--space-xs) 0; border-bottom: var(--rule-hair) solid var(--color-rule); }
.io-rosa li .num { text-align: right; }
.io-rosa__nome { min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.io-rosa--capitano .io-rosa__nome { font-weight: 600; }

/* disponibilità della squadra: una griglia giocatori x giorni che scorre dentro il suo riquadro */
.io-disp-scorri { overflow-x: auto; margin-top: var(--space-xs); }
.io-disp { border-collapse: collapse; font-size: var(--text-sm); }
.io-disp th, .io-disp td { padding: 4px 3px; text-align: center; white-space: nowrap; }
.io-disp thead th { font-weight: 400; color: var(--color-muted); font-size: var(--text-xs); }
.io-disp tbody th, .io-disp tfoot th { text-align: left; font-weight: 400; max-width: 9rem; overflow: hidden; text-overflow: ellipsis; padding-right: var(--space-sm); }
.io-disp--we { background: color-mix(in oklch, var(--color-paper-3) 45%, transparent); }
.io-disp--migliore { color: var(--color-accent); font-weight: 700; }
.io-disp__cella { display: inline-block; width: 14px; height: 14px; border-radius: 3px; vertical-align: middle; }
.io-disp__cella--disponibile { background: var(--color-accent); }
.io-disp__cella--forse { background: var(--color-ink-2); opacity: 0.6; }
.io-disp__cella--indisponibile { background: var(--color-error); }
.io-disp__cella--vuoto { box-shadow: inset 0 0 0 1px var(--color-rule-2); }
.io-legenda { display: flex; flex-wrap: wrap; gap: var(--space-md); margin: var(--space-xs) 0 0; }
.io-legenda > span { display: inline-flex; align-items: center; gap: var(--space-2xs); }

/* sere libere del giocatore */
.io-sere { list-style: none; margin: var(--space-xs) 0 0; padding: 0; display: grid; grid-template-columns: repeat(auto-fill, minmax(15rem, 1fr)); gap: var(--space-2xs) var(--space-lg); }
.io-sere li { display: flex; justify-content: space-between; align-items: center; gap: var(--space-sm); padding: var(--space-2xs) 0; border-bottom: var(--rule-hair) solid var(--color-rule); }
.io-sere--we .io-sere__giorno { color: var(--color-ink); font-weight: 600; }
.io-sere__giorno { color: var(--color-ink-2); }
.io-nuovo-ticket summary { cursor: pointer; color: var(--color-ink-2); margin-top: var(--space-xs); }
.io-nuovo-ticket summary:focus-visible { outline: 2px solid var(--color-focus); outline-offset: 2px; }
.io-nuovo-ticket .modulo { margin-top: var(--space-md); max-width: 32rem; }
.io-elenco { list-style: none; margin: 0; padding: 0; }
.io-elenco li { display: flex; justify-content: space-between; gap: var(--space-sm); padding: var(--space-2xs) 0; border-bottom: var(--rule-hair) solid var(--color-rule); }

/* movimento: il turno che arriva si accende, la rosa che si riempie scivola */
.turno-enter-active { transition: transform var(--dur-long) var(--ease-out); }
.turno-enter-from { transform: translateY(-6px) scale(0.98); }
.io-rosa-move { transition: transform var(--dur-long) var(--ease-out); }
.io-rosa-enter-active { transition: transform var(--dur-long) var(--ease-out); }
.io-rosa-enter-from { transform: translateX(-14px); }

@media (max-width: 760px) {
  .io-due { grid-template-columns: minmax(0, 1fr); }
  .io-testa { grid-template-columns: auto minmax(0, 1fr); }
  .io-testa__impostazioni { grid-column: 1 / -1; justify-self: start; }
  .io-rosa li { grid-template-columns: minmax(0, 1fr) 3rem 4.5rem; }
  .io-rosa__fascia { display: none; }
}
</style>
