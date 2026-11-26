-- Produzione, 26/11/2026: quello che serve all'asta in tempo reale (SignalR, timer, presenze) arrivata con il merge del branch Jason.
--
-- Crea solo ciò che manca e non tocca i dati esistenti, quindi si può rilanciare senza danni.
-- Va lanciato PRIMA che il nuovo codice vada online: al vecchio codice le tabelle in più non danno fastidio.
--
-- Uso (sul server, come postgres, sul database del sito):
--   sudo -u postgres psql -v ON_ERROR_STOP=1 -d <database> -f db/migrazioni/2026-11-26-asta-tempo-reale.sql
-- In locale funziona uguale: i permessi per l'utente del sito si danno solo se quell'utente esiste.

BEGIN;

-- Giocatori chiamati all'asta: il giocatore chiamato (id_utente, una sola chiamata per giocatore), chi ha fatto la prima
-- chiamata, lo stato della chiamata e il costo iniziale (stessa definizione di db/setup-locale.sql)
CREATE TABLE IF NOT EXISTS fanta_torneo.asta_giocatori_chiamati (
    id_utente         text PRIMARY KEY REFERENCES fanta_torneo.utenti_discord (discord_id),
    id_chiamato_da    text NOT NULL REFERENCES fanta_torneo.utenti_discord (discord_id),
    stato             text NOT NULL,
    costo_iniziale    int  NOT NULL CHECK (costo_iniziale >= 0),
    data_inserimento  timestamptz NOT NULL DEFAULT now()
);

-- Offerte fatte all'asta: per quale giocatore (id_utente), chi offre (id_chiamato_da), quanto e quando
CREATE TABLE IF NOT EXISTS fanta_torneo.asta_offerte (
    id_incrementale  serial PRIMARY KEY,
    id_utente        text NOT NULL REFERENCES fanta_torneo.utenti_discord (discord_id),
    id_chiamato_da   text NOT NULL REFERENCES fanta_torneo.utenti_discord (discord_id),
    puntata          int  NOT NULL CHECK (puntata >= 0),
    data_puntata     timestamptz NOT NULL DEFAULT now()
);

-- In produzione il sito si collega come fanta_lettura, che non riceve in automatico i permessi sulle tabelle nuove
-- (sullo schema fanta_torneo non ci sono default privileges): senza questi GRANT ogni chiamata all'asta darebbe errore.
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'fanta_lettura') THEN
        GRANT SELECT, INSERT, UPDATE, DELETE ON fanta_torneo.asta_giocatori_chiamati, fanta_torneo.asta_offerte TO fanta_lettura;
        GRANT USAGE, SELECT ON SEQUENCE fanta_torneo.asta_offerte_id_incrementale_seq TO fanta_lettura;
    END IF;
END
$$;

-- Configurazione dell'asta: senza queste righe l'asta risulta spenta, le iscrizioni chiuse e le chiamate non cambiano fase.
-- Per aprire l'asta la sera della prova:
--   update fanta_torneo.configurazioni set valore = 'asta attiva' where chiave = 'stato_globale_asta';
INSERT INTO fanta_torneo.configurazioni (chiave, valore, descrizione)
VALUES ('stato_globale_asta', 'iscrizione', 'Fase in cui si trova l''asta nel suo insieme (iniziale: iscrizione)'),
       ('fase_flusso_asta', 'chiamata_giocatore', 'Fase in cui si trova la chiamata di un singolo giocatore (iniziale: chiamata_giocatore)'),
       ('timer_asta_secondi', '120', 'Secondi del timer dell''offerta: parte dalla prima puntata, si riporta a questo valore a ogni offerta e a zero chiude l''offerta')
ON CONFLICT (chiave) DO NOTHING;

COMMIT;
