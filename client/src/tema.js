// Tema del sito: scuro (predefinito) o chiaro. La scelta vera sta nel database (utenti_info.layout_scuro);
// qui se ne tiene solo una copia nel browser per dipingere subito la pagina giusta al ricaricamento,
// senza il lampo del tema sbagliato prima che arrivi la risposta del server.

const CHIAVE = 'fantaage_tema'

export function applicaTema(scuro) {
  document.documentElement.dataset.tema = scuro ? 'scuro' : 'chiaro'
  document.querySelector('meta[name="theme-color"]')?.setAttribute('content', scuro ? '#17212d' : '#f1f4f8')
  try {
    if (scuro) localStorage.removeItem(CHIAVE)
    else localStorage.setItem(CHIAVE, 'chiaro')
  } catch { /* senza storage resta valido il tema appena applicato */ }
}

// Da chiamare prima di montare l'app.
export function applicaTemaIniziale() {
  let chiaro = false
  try { chiaro = localStorage.getItem(CHIAVE) === 'chiaro' } catch {}
  document.documentElement.dataset.tema = chiaro ? 'chiaro' : 'scuro'
  if (chiaro) document.querySelector('meta[name="theme-color"]')?.setAttribute('content', '#f1f4f8')
}
