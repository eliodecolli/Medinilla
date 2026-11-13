-- =============================================================================
-- Medinilla bootstrap account seed.
--
-- WARNING: This script deletes all rows from the public core_* application
-- tables before reseeding the account required for first-time station
-- bootstrapping. The EF Core migration history is intentionally left intact.
-- =============================================================================

BEGIN;

-- Delete child/configuration data before the entities it references.
DELETE FROM public.core_command_executions;
DELETE FROM public.core_component_variables;
DELETE FROM public.core_charger_components;
DELETE FROM public.core_report_base_statuses;
DELETE FROM public.core_transactions_event;
DELETE FROM public.core_transactions_snapshot;
DELETE FROM public.core_id_token;
DELETE FROM public.core_auth_user;
DELETE FROM public.core_auth_details;
DELETE FROM public.core_tariff;
DELETE FROM public.core_evse_connector;
DELETE FROM public.core_charging_station;
DELETE FROM public.core_account;

-- The application looks up this account when a charging station first boots.
INSERT INTO public.core_account ("Id", "Name")
VALUES (gen_random_uuid(), 'MedinillaTest-Core');

COMMIT;

SELECT "Id", "Name"
FROM public.core_account
WHERE "Name" = 'MedinillaTest-Core';
