-- Bootstrap settings for every account that does not already have a settings row.
-- Existing settings (including an empty AllowedMeasurands list) are left unchanged.
-- Requires the AddAccountSettings migration to have been applied.
--
-- The measurands below match DefaultAllowedMeasurands in
-- src/Medinilla.Core.Service/defaults.json; update this literal if defaults change.
--
-- Run against the intended database with:
--   psql "$MEDINILLA_CONNECTION_URI" -v ON_ERROR_STOP=1 -f src/dev/bootstrap-account-settings.sql

BEGIN;

INSERT INTO public.core_account_settings ("AccountId", "AllowedMeasurands")
SELECT account."Id", 'EnergyActiveImportRegister,EnergyActiveExportRegister'
FROM public.core_account AS account
WHERE NOT EXISTS (
    SELECT 1
    FROM public.core_account_settings AS settings
    WHERE settings."AccountId" = account."Id"
)
ON CONFLICT ("AccountId") DO NOTHING
RETURNING "AccountId", "AllowedMeasurands";

COMMIT;
