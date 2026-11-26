<script setup>
// Un numero che scorre fino al valore nuovo invece di cambiare di colpo (crediti, puntate, totali, numeri della landing).
// allaVista: la prima volta parte da zero quando il numero entra nello schermo. Con "riduci movimento" il valore cambia subito.
// Lo screen reader legge solo il valore finale; le cifre che scorrono sono nascoste agli strumenti di accessibilità.
import { ref, watch, onMounted, onBeforeUnmount } from 'vue'

const props = defineProps({
  valore: { type: Number, default: null },
  allaVista: Boolean,
  durata: { type: Number, default: 600 },
  vuoto: { type: String, default: '—' },
})

const el = ref(null)
const mostrato = ref(props.allaVista ? 0 : props.valore)
const formato = new Intl.NumberFormat('it-IT')
const testo = (v) => (v == null || Number.isNaN(v) ? props.vuoto : formato.format(Math.round(v)))

let raf = 0
// reattivo: finché il numero non si è visto il modello mostra 0 e non legge 'mostrato'
const visto = ref(!props.allaVista)
const ridotto = () => window.matchMedia('(prefers-reduced-motion: reduce)').matches

// ease-out del tema (curva morbida in uscita) applicata al valore
const esci = (t) => 1 - Math.pow(1 - t, 4)

function scorri(da, a) {
  cancelAnimationFrame(raf)
  if (da == null || a == null || ridotto() || da === a) {
    mostrato.value = a
    return
  }
  const inizio = performance.now()
  const passo = (ora) => {
    const t = Math.min(1, (ora - inizio) / props.durata)
    mostrato.value = da + (a - da) * esci(t)
    if (t < 1) raf = requestAnimationFrame(passo)
  }
  raf = requestAnimationFrame(passo)
}

watch(() => props.valore, (nuovo, vecchio) => {
  if (!visto.value) return
  scorri(vecchio ?? (props.allaVista ? 0 : null), nuovo)
})

let osservatore
onMounted(() => {
  if (!props.allaVista) return
  osservatore = new IntersectionObserver(([v]) => {
    if (!v.isIntersecting || visto.value) return
    visto.value = true
    osservatore.disconnect()
    scorri(0, props.valore)
  }, { threshold: 0 })
  osservatore.observe(el.value)
})
onBeforeUnmount(() => {
  cancelAnimationFrame(raf)
  osservatore?.disconnect()
})
</script>

<template>
  <span ref="el" class="numero-vivo"><span aria-hidden="true">{{ testo(visto ? mostrato : 0) }}</span><span class="visually-hidden">{{ testo(valore) }}</span></span>
</template>
