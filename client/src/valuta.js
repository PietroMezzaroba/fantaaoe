// Nome della moneta dell'asta (configurazioni.valuta), condiviso da tutte le pagine dell'asta.
import { reactive } from 'vue'
import { api } from './api.js'

export const moneta = reactive({ nome: 'Berlu' })

export async function caricaValuta() {
  try {
    moneta.nome = (await api('asta/valuta')).valuta || moneta.nome
  } catch {
    /* resta il nome precedente */
  }
}
