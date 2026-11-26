-- Produzione: toglie i dati di prova dell'asta (richiesta di Jason, 26/11 sera: «bisogna pulire tutti i miei dati di test,
-- devo eliminare tutti i Giocatore, Admin, Streamer, Capitano ma anche su tutte le tabelle referenziate»).
--
-- Dati di prova = gli utenti con id Discord finto «2000000000000000xx» (Giocatore_N, Capitano_N, Admin_N, Streamer_N,
-- Utente_N) e tutto quello che li riguarda; più la tabella del login simulato (utenti_dev), che in produzione non serve.
-- Restano: gli utenti veri (abbinati da Thomuggia e chi ha fatto l'accesso), i loro ruoli e le loro iscrizioni, l'anagrafica
-- delle civiltà, i ruoli e le configurazioni (l'asta torna all'inizio della chiamata).
--
-- ⚠️ Cancella in modo definitivo: fare prima il backup.
--   sudo sh -c "sudo -u postgres pg_dump -Fc -n fanta_torneo <database> > /root/backup-.../fanta_torneo-pre-pulizia.dump"
-- Prova senza cambiare niente (stampa i conteggi e annulla):
--   sed 's/^COMMIT;$/ROLLBACK;/' db/migrazioni/2026-11-27-pulizia-dati-di-prova.sql | sudo -u postgres psql -v ON_ERROR_STOP=1 -d <database>
-- Esecuzione vera:
--   sudo -u postgres psql -v ON_ERROR_STOP=1 -d <database> -f db/migrazioni/2026-11-27-pulizia-dati-di-prova.sql

BEGIN;

CREATE TEMP TABLE prova_utenti ON COMMIT DROP AS
    SELECT discord_id FROM fanta_torneo.utenti_discord WHERE discord_id LIKE '2000000000000000%';
CREATE TEMP TABLE prova_squadre ON COMMIT DROP AS
    SELECT id FROM fanta_torneo.squadre WHERE id_capitano IN (SELECT discord_id FROM prova_utenti);

SELECT 'utenti di prova' AS cosa, count(*) FROM prova_utenti
UNION ALL SELECT 'squadre con capitano di prova', count(*) FROM prova_squadre;

-- asta: offerte, chiamate, acquisti, ordine e portafogli dei capitani di prova
DELETE FROM fanta_torneo.asta_offerte
 WHERE id_utente IN (SELECT discord_id FROM prova_utenti) OR id_chiamato_da IN (SELECT discord_id FROM prova_utenti);
DELETE FROM fanta_torneo.asta_giocatori_chiamati
 WHERE id_utente IN (SELECT discord_id FROM prova_utenti) OR id_chiamato_da IN (SELECT discord_id FROM prova_utenti);
DELETE FROM fanta_torneo.asta_giocatori_acquistati
 WHERE id_iscritto IN (SELECT discord_id FROM prova_utenti) OR id_squadra IN (SELECT id FROM prova_squadre);
DELETE FROM fanta_torneo.asta_ordine_capitani WHERE id_utente IN (SELECT discord_id FROM prova_utenti);
DELETE FROM fanta_torneo.asta_portafoglio_utente WHERE id_utente IN (SELECT discord_id FROM prova_utenti);

-- un iscritto vero assegnato a una squadra di prova torna libero, poi le squadre di prova spariscono
UPDATE fanta_torneo.utenti_iscritti SET id_squadra = NULL WHERE id_squadra IN (SELECT id FROM prova_squadre);
DELETE FROM fanta_torneo.squadre WHERE id IN (SELECT id FROM prova_squadre);

-- disponibilità e ticket scritti dagli utenti di prova
DELETE FROM fanta_torneo.disponibilita WHERE discord_id IN (SELECT discord_id FROM prova_utenti);
DELETE FROM fanta_torneo.ticket WHERE autore_discord_id IN (SELECT discord_id FROM prova_utenti);

-- gli utenti di prova: utenti_info, utenti_iscritti e utenti_ruoli vanno via con loro (ON DELETE CASCADE)
DELETE FROM fanta_torneo.utenti_discord WHERE discord_id IN (SELECT discord_id FROM prova_utenti);

-- il login simulato esiste solo in sviluppo: in produzione la tabella non serve
DROP TABLE IF EXISTS fanta_torneo.utenti_dev;

-- l'asta riparte dall'inizio della chiamata, senza timer né storico appesi
UPDATE fanta_torneo.configurazioni SET valore = 'chiamata_giocatore' WHERE chiave = 'fase_flusso_asta';
DELETE FROM fanta_torneo.configurazioni WHERE chiave IN ('scadenza_asta', 'timer_residuo_ms', 'inizio_log_asta');

-- cosa resta
SELECT 'utenti_discord' AS tabella, count(*) AS righe FROM fanta_torneo.utenti_discord
UNION ALL SELECT 'utenti di prova rimasti', count(*) FROM fanta_torneo.utenti_discord WHERE discord_id LIKE '2000000000000000%'
UNION ALL SELECT 'utenti_info', count(*) FROM fanta_torneo.utenti_info
UNION ALL SELECT 'utenti_ruoli', count(*) FROM fanta_torneo.utenti_ruoli
UNION ALL SELECT 'utenti_iscritti', count(*) FROM fanta_torneo.utenti_iscritti
UNION ALL SELECT 'squadre', count(*) FROM fanta_torneo.squadre
UNION ALL SELECT 'asta_giocatori_acquistati', count(*) FROM fanta_torneo.asta_giocatori_acquistati
UNION ALL SELECT 'asta_portafoglio_utente', count(*) FROM fanta_torneo.asta_portafoglio_utente
UNION ALL SELECT 'asta_ordine_capitani', count(*) FROM fanta_torneo.asta_ordine_capitani;

COMMIT;
