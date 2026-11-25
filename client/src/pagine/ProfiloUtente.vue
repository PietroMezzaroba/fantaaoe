<script setup>
import { computed, ref } from 'vue'
import { sessione, accedi, linkAccesso, salvaImpostazione, impostazioniPredefinite } from '../sessione.js'

const nomiRuolo = { admin: 'Admin', capitano: 'Capitano', giocatore: 'Giocatore', streamer: 'Streamer' }
const profilo = computed(() => {
  const nomi = sessione.ruoli.map((r) => nomiRuolo[r] ?? r)
  return nomi.length ? nomi.join(', ') : 'Nessuno'
})

const imp = computed(() => ({ ...impostazioniPredefinite, ...sessione.impostazioni }))

// Impostazioni: ognuna si salva da sola appena la cambi, non ci sono tasti di salvataggio.
const interruttori = computed(() => [
  {
    chiave: 'audioAttivo',
    titolo: 'Audio di sottofondo',
    acceso: imp.value.audioAttivo,
    nota: imp.value.audioAttivo ? 'Attivo: in alto trovi il controllo per sentirlo o no.' : 'Spento: il controllo audio non compare nel sito.',
  },
  {
    chiave: 'layoutScuro',
    titolo: 'Tema scuro',
    acceso: imp.value.layoutScuro,
    nota: imp.value.layoutScuro ? 'Attivo: il sito è scuro.' : 'Spento: il sito è chiaro.',
  },
  {
    chiave: 'animazioneIniziale',
    titolo: 'Animazione iniziale',
    acceso: imp.value.animazioneIniziale,
    nota: imp.value.animazioneIniziale
      ? "Attivo: l'elmo della home si anima."
      : "Spento: l'elmo della home resta fermo e il pulsante di pausa non compare.",
  },
])

// Cursore del mouse: due scelte (utenti_info.cursore)
const cursori = [
  { valore: 'accattivante', titolo: 'Accattivante', nota: 'Il puntatore personalizzato del sito.' },
  { valore: 'classico', titolo: 'Classico', nota: 'La freccia normale del sistema.' },
]

const errore = ref('')
async function cambia(chiave, valore) {
  errore.value = ''
  try {
    await salvaImpostazione(chiave, valore)
  } catch (e) {
    errore.value = `Non sono riuscito a salvare l'impostazione (${e.message}). Riprova.`
  }
}
</script>

<template>
  <section class="profilo" aria-labelledby="titolo-profilo">
    <h1 id="titolo-profilo">Il tuo profilo</h1>

    <p v-if="!sessione.caricata" class="muto">Caricamento…</p>

    <div v-else-if="!sessione.autenticato" class="avviso">
      <strong>Non sei connesso.</strong>
      <span>Accedi per vedere il tuo profilo.</span>
      <a v-if="sessione.loginAttivo || sessione.loginSviluppo" class="btn" :href="linkAccesso('/profilo')" @click="accedi">Accedi</a>
    </div>

    <template v-else>
      <dl class="config profilo__dati">
        <dt>Id Discord</dt>
        <dd>{{ sessione.discordId }}</dd>
        <dt>Nome</dt>
        <dd>{{ sessione.nome }}</dd>
        <dt>Profilo</dt>
        <dd>{{ profilo }}</dd>
      </dl>

      <div class="profilo__impostazioni">
        <label v-for="i in interruttori" :key="i.chiave" class="interruttore">
          <input
            type="checkbox"
            role="switch"
            class="interruttore__campo"
            :checked="i.acceso"
            @change="cambia(i.chiave, $event.target.checked)"
          />
          <span class="interruttore__traccia" aria-hidden="true"></span>
          <span class="interruttore__testo">
            <strong>{{ i.titolo }}</strong>
            <span class="piccolo muto">{{ i.nota }}</span>
          </span>
        </label>
        <fieldset class="opzioni">
          <legend>Cursore del mouse</legend>
          <label v-for="c in cursori" :key="c.valore" class="opzioni__voce" :title="c.nota">
            <input
              type="radio"
              name="cursore"
              class="opzioni__campo"
              :value="c.valore"
              :checked="imp.cursore === c.valore"
              @change="cambia('cursore', c.valore)"
            />
            <span class="opzioni__pillola">{{ c.titolo }}</span>
          </label>
        </fieldset>
        <p v-if="errore" class="profilo__errore" role="alert">{{ errore }}</p>
      </div>
    </template>
  </section>
</template>

<style scoped>
.profilo { display: grid; gap: var(--space-lg); max-width: 40rem; }
h1 { margin: 0; font-size: var(--text-3xl); font-weight: var(--display-weight); letter-spacing: var(--tracking-display); }
.profilo__dati { margin: 0; padding: var(--space-md); background: var(--color-paper-2); border-radius: var(--radius-card); display: grid; grid-template-columns: auto minmax(0, 1fr); gap: var(--space-xs) var(--space-lg); }
.profilo__dati dt { color: var(--color-muted); }
.profilo__dati dd { margin: 0; font-variant-numeric: tabular-nums; overflow-wrap: anywhere; }
.profilo__errore { margin: 0; font-size: var(--text-sm); color: var(--color-error); }
.profilo__impostazioni { display: grid; gap: var(--space-md); padding: var(--space-md); background: var(--color-paper-2); border-radius: var(--radius-card); }

.interruttore { display: flex; align-items: center; gap: var(--space-md); cursor: pointer; }
.interruttore__campo { position: absolute; opacity: 0; width: 1px; height: 1px; }
.interruttore__traccia {
  position: relative; flex: 0 0 auto; width: 44px; height: 24px; border-radius: var(--radius-pill);
  background: var(--color-slot); box-shadow: inset 0 0 0 var(--rule-hair) var(--color-rule-2);
  transition: background-color var(--dur-short) var(--ease-out);
}
.interruttore__traccia::after {
  content: ''; position: absolute; top: 3px; left: 3px; width: 18px; height: 18px; border-radius: 50%;
  background: var(--color-ink-2); transition: transform var(--dur-short) var(--ease-out), background-color var(--dur-short) var(--ease-out);
}
.interruttore__campo:checked + .interruttore__traccia { background: var(--color-accent); }
.interruttore__campo:checked + .interruttore__traccia::after { transform: translateX(20px); background: var(--color-accent-ink); }
.interruttore__campo:focus-visible + .interruttore__traccia { outline: 2px solid var(--color-focus); outline-offset: 2px; }
.opzioni { margin: 0; padding: 0; border: 0; display: flex; flex-wrap: wrap; align-items: center; gap: var(--space-xs) var(--space-md); }
.opzioni legend { float: left; padding: 0; margin-right: var(--space-md); font-weight: 600; }
.opzioni__voce { cursor: pointer; }
.opzioni__campo { position: absolute; opacity: 0; width: 1px; height: 1px; }
.opzioni__pillola {
  display: inline-flex; align-items: center; min-height: 32px; padding: 0 var(--space-md); border-radius: var(--radius-pill);
  font-size: var(--text-sm); color: var(--color-ink-2); box-shadow: inset 0 0 0 var(--rule-hair) var(--color-rule-2);
  transition: background-color var(--dur-short) var(--ease-out), color var(--dur-short) var(--ease-out);
}
.opzioni__voce:hover .opzioni__pillola { background: var(--color-paper-3); }
.opzioni__campo:checked + .opzioni__pillola { background: var(--color-accent); color: var(--color-accent-ink); box-shadow: none; }
.opzioni__campo:focus-visible + .opzioni__pillola { outline: 2px solid var(--color-focus); outline-offset: 2px; }
.interruttore__testo { display: grid; gap: var(--space-3xs); }
</style>
