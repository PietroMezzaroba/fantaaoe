-- Produzione, 26/11/2026: il cursore personalizzato non è più la scelta predefinita (branch sito-dinamico).
-- Cambia solo il valore che riceve chi entra per la prima volta: chi ha già una riga in utenti_info tiene la sua scelta,
-- e dal profilo si può sempre riaccendere il cursore "Accattivante". Si può rilanciare senza danni.
--
-- Uso (sul server, come postgres, sul database del sito), insieme alla pubblicazione del codice:
--   sudo -u postgres psql -v ON_ERROR_STOP=1 -d <database> -f db/migrazioni/2026-11-26-cursore-classico.sql

ALTER TABLE fanta_torneo.utenti_info ALTER COLUMN cursore SET DEFAULT 'classico';
