-- =============================================================================
-- Medinilla test data seed.
--
-- Run against the dev Postgres container (see dev/seed.sh).
--
-- What it does:
--   1. Wipes every public.core_* table.
--   2. Inserts a primary test account named 'MedinillaTest-Core'. This is the
--      account that ChargingStationBooting.ProcessBootup looks up when a new
--      station boots (see Medillina.Core/v1/Services/ChargingStationBooting.cs).
--   3. Inserts a handful of secondary 'noise' accounts, each with several
--      stations, EVSE connectors, tariffs, auth users, id tokens, transactions
--      and transaction events, so the rest of the system has realistic data
--      to chew on.
--
-- All noise is generated inside the procedure with random()/gen_random_uuid()
-- so the same procedure is reusable for different seed sizes.
-- =============================================================================

DROP PROCEDURE IF EXISTS public.seed_medinilla_test_data();

CREATE OR REPLACE PROCEDURE public.seed_medinilla_test_data(
    noise_accounts INT DEFAULT 4,
    noise_stations_min INT DEFAULT 2,
    noise_stations_max INT DEFAULT 5,
    noise_connectors_min INT DEFAULT 1,
    noise_connectors_max INT DEFAULT 4,
    noise_tokens_min INT DEFAULT 1,
    noise_tokens_max INT DEFAULT 4,
    noise_transactions_per_station_max INT DEFAULT 6,
    noise_events_per_transaction_max INT DEFAULT 5
)
LANGUAGE plpgsql
AS $$
DECLARE
    -- Main test account
    main_account_id uuid;

    -- Noise account loop
    n_account_id uuid;
    n_account_name text;

    -- Per station
    station_id uuid;
    station_count int;
    connector_count int;
    connector_id uuid;
    auth_user_id uuid;
    token_id uuid;
    token_count int;

    -- Per transaction
    transaction_id text;
    transaction_count int;
    started_at timestamptz;
    ended_at timestamptz;
    metered_value numeric(10,3);
    unit_price numeric(10,4);
    total_cost numeric(12,4);
    event_count int;
    ev int;

    -- Random pickers
    model_pick text;
    vendor_pick text;
    location_pick text;
    alias_pick text;
    reason_pick text;
    status_pick text;
    idtype_pick text;
    evse_type_pick text;
    start_reason_pick text;
    end_reason_pick text;
    trigger_pick text;
    event_pick text;
    consumption_pick text;

    i INT;
BEGIN
    ----------------------------------------------------------------------------
    -- 1. Clear existing data
    ----------------------------------------------------------------------------
    DELETE FROM public.core_transactions_event;
    DELETE FROM public.core_transactions_snapshot;
    DELETE FROM public.core_id_token;
    DELETE FROM public.core_auth_user;
    DELETE FROM public.core_auth_details;
    DELETE FROM public.core_tariff;
    DELETE FROM public.core_evse_connector;
    DELETE FROM public.core_charging_station;
    DELETE FROM public.core_account;

    ----------------------------------------------------------------------------
    -- 2. Main test account
    ----------------------------------------------------------------------------
    main_account_id := gen_random_uuid();
    INSERT INTO public.core_account ("Id", "Name")
    VALUES (main_account_id, 'MedinillaTest-Core');

    -- Give the main account a few stations too, so it isn't an empty shell.
    FOR i IN 1..3 LOOP
        station_id := gen_random_uuid();

        model_pick    := (ARRAY['Terra 94', 'Terra 184', 'AC22', 'V2X-50', 'FlexCharge 60'])[floor(random() * 5 + 1)];
        vendor_pick   := (ARRAY['ABB', 'Signet', 'Wallbox', 'EVBox', 'Delta'])[floor(random() * 5 + 1)];
        location_pick := (ARRAY['HQ Garage', 'Depot North', 'Customer Lot', 'Rooftop', 'Yard B'])[floor(random() * 5 + 1)];
        alias_pick    := 'Main Station ' || i;
        reason_pick   := (ARRAY['PowerUp', 'Reboot', 'LocalReset', 'FirmwareUpdate', 'RemoteReset'])[floor(random() * 5 + 1)];

        INSERT INTO public.core_charging_station (
            "Id", "AccountId", "AuthDetailsId", "Booted", "ClientIdentifier", "Model", "Vendor",
            "LatestBootNotificationReason", "Location", "Alias",
            "CreatedAt", "ModifiedAt"
        )
        VALUES (
            station_id,
            main_account_id,
            -- placeholder, replaced after auth_details insert
            '00000000-0000-0000-0000-000000000000',
            random() > 0.3,
            'MT_' || lpad(i::text, 3, '0'),
            model_pick,
            vendor_pick,
            reason_pick,
            location_pick,
            alias_pick,
            NOW() - (random() * interval '90 days'),
            NOW() - (random() * interval '7 days')
        );

        DECLARE
            auth_details_id uuid := gen_random_uuid();
        BEGIN
            INSERT INTO public.core_auth_details ("Id", "ChargingStationId", "AuthBlob")
            VALUES (
                auth_details_id,
                station_id,
                jsonb_build_object(
                    'authType', 'Basic',
                    'credentials', replace(gen_random_uuid()::text, '-', ''),
                    'provisionedBy', 'medinilla-test-core'
                )
            );

            UPDATE public.core_charging_station
            SET "AuthDetailsId" = auth_details_id
            WHERE "Id" = station_id;
        END;

        -- A couple of tariffs per station.
        FOR ev IN 1..2 LOOP
            INSERT INTO public.core_tariff ("Id", "ChargingStationId", "UnitName", "UnitPrice")
            VALUES (
                gen_random_uuid(),
                station_id,
                (ARRAY['kWh', 'minute', 'session'])[ev],
                (random() * 0.6 + 0.18)::numeric(10,4)
            );
        END LOOP;

        connector_count := floor(random() * 3 + 2)::int;  -- 2..4
        FOR ev IN 1..connector_count LOOP
            connector_id := gen_random_uuid();
            status_pick := (ARRAY['Available', 'Available', 'Available', 'Occupied', 'Reserved', 'Faulted'])[floor(random() * 6 + 1)];

            INSERT INTO public.core_evse_connector (
                "Id", "ChargingStationId", "EvseId", "ConnectorId", "ConnectorStatus", "ModifiedAt"
            )
            VALUES (
                connector_id, station_id, ev, ev, status_pick, NOW() - (random() * interval '6 hours')
            );
        END LOOP;

        -- Auth user + id token.
        auth_user_id := gen_random_uuid();
        INSERT INTO public.core_auth_user (
            "Id", "ChargingStationId", "DisplayName", "IsActive", "ActiveCredit"
        )
        VALUES (
            auth_user_id, station_id, 'Main User ' || i, true, (random() * 250 + 25)::numeric(10,2)
        );

        token_count := floor(random() * 3 + 1)::int;  -- 1..3
        FOR ev IN 1..token_count LOOP
            token_id := gen_random_uuid();
            idtype_pick := (ARRAY['ISO14443', 'ISO15693', 'KeyCode', 'Local'])[floor(random() * 4 + 1)];

            INSERT INTO public.core_id_token (
                "Id", "ChargingStationId", "AuthorizationUserId",
                "Token", "IdType", "CreatedDate", "ExpiryDate",
                "Blocked", "IsUnderTx"
            )
            VALUES (
                token_id,
                station_id,
                auth_user_id,
                'MAIN_TOKEN_' || i || '_' || ev,
                idtype_pick,
                NOW() - (random() * interval '120 days'),
                NOW() + (random() * interval '365 days'),
                false,
                false
            );
        END LOOP;

        -- A handful of completed transactions with events.
        transaction_count := floor(random() * 4 + 2)::int;  -- 2..5
        FOR ev IN 1..transaction_count LOOP
            transaction_id := 'MT_TX_' || lpad(i::text, 3, '0') || '_' || lpad(ev::text, 3, '0');

            started_at := NOW() - (random() * interval '60 days');
            ended_at   := started_at + (random() * interval '6 hours' + interval '5 minutes');
            metered_value := (random() * 45 + 0.5)::numeric(10,3);
            unit_price := (random() * 0.5 + 0.2)::numeric(10,4);
            total_cost := (metered_value * unit_price)::numeric(12,4);

            start_reason_pick := (ARRAY['Authorized', 'EVConnected', 'ChargingRateChanged'])[floor(random() * 3 + 1)];
            end_reason_pick   := (ARRAY['EVDisconnected', 'Local', 'PowerLoss', 'StopAuthorized', 'Other'])[floor(random() * 5 + 1)];

            -- pick any connector from this station
            SELECT "Id" INTO connector_id
            FROM public.core_evse_connector
            WHERE "ChargingStationId" = station_id
            ORDER BY random()
            LIMIT 1;

            -- pick any token from this station
            SELECT "Id" INTO token_id
            FROM public.core_id_token
            WHERE "ChargingStationId" = station_id
            ORDER BY random()
            LIMIT 1;

            INSERT INTO public.core_transactions_snapshot (
                "Id", "ChargingStationId", "IdTokenId", "TransactionId",
                "StartReason", "EndReason", "TotalMeteredValue", "TotalCost",
                "StartedAt", "EndedAt", "EvseConnectorId"
            )
            VALUES (
                gen_random_uuid(), station_id, token_id, transaction_id,
                start_reason_pick, end_reason_pick, metered_value, total_cost,
                started_at, ended_at, connector_id
            );

            -- events for this transaction
            event_count := floor(random() * (noise_events_per_transaction_max - 1) + 2)::int;
            FOR k IN 1..event_count LOOP
                event_pick       := (ARRAY['Started', 'Updated', 'Ended', 'MeterValue', 'Status'])[floor(random() * 5 + 1)];
                trigger_pick     := (ARRAY['MeterValuePeriodic', 'MeterValueClock', 'EVConnected', 'EVDisconnected', 'RemoteStart', 'RemoteStop'])[floor(random() * 6 + 1)];
                consumption_pick := (ARRAY['Periodic', 'Cumulative'])[floor(random() * 2 + 1)];

                INSERT INTO public.core_transactions_event (
                    "Id", "ChargingStationId", "IdTokenId", "TransactionId", "SeqNo",
                    "EVSEId", "Timestamp", "Offline", "RegisterValue",
                    "PhaseOneValue", "PhaseTwoValue", "PhaseThreeValue",
                    "ConsumptionType", "UnitName", "TriggerReason", "EventType"
                )
                VALUES (
                    gen_random_uuid(),
                    station_id,
                    token_id,
                    transaction_id,
                    k,
                    (SELECT "EvseId" FROM public.core_evse_connector WHERE "Id" = connector_id),
                    started_at + (random() * (ended_at - started_at)),
                    random() > 0.8,
                    (random() * metered_value)::numeric(10,3),
                    (random() * metered_value / 3)::numeric(10,3),
                    (random() * metered_value / 3)::numeric(10,3),
                    (random() * metered_value / 3)::numeric(10,3),
                    consumption_pick,
                    'kWh',
                    trigger_pick,
                    event_pick
                );
            END LOOP;
        END LOOP;
    END LOOP;

    ----------------------------------------------------------------------------
    -- 3. Noise accounts
    ----------------------------------------------------------------------------
    FOR i IN 1..noise_accounts LOOP
        n_account_id   := gen_random_uuid();
        n_account_name := (ARRAY[
            'Account Alpha', 'Account Beta', 'Account Gamma',
            'Account Delta', 'Account Epsilon', 'Account Zeta'
        ])[i];

        INSERT INTO public.core_account ("Id", "Name")
        VALUES (n_account_id, n_account_name);

        station_count := floor(random() * (noise_stations_max - noise_stations_min + 1) + noise_stations_min)::int;

        FOR j IN 1..station_count LOOP
            station_id := gen_random_uuid();

            model_pick    := (ARRAY['Terra 94', 'Terra 184', 'AC22', 'V2X-50', 'FlexCharge 60', 'HyperCharge 350'])[floor(random() * 6 + 1)];
            vendor_pick   := (ARRAY['ABB', 'Signet', 'Wallbox', 'EVBox', 'Delta', 'Tritium', 'Alpitronic'])[floor(random() * 7 + 1)];
            location_pick := (ARRAY['Garage A', 'Garage B', 'Street 12', 'Mall West', 'Office Park', 'Highway KM42', 'Yard C'])[floor(random() * 7 + 1)];
            alias_pick    := n_account_name || ' St-' || j;
            reason_pick   := (ARRAY['PowerUp', 'Reboot', 'LocalReset', 'FirmwareUpdate', 'RemoteReset', 'Unknown'])[floor(random() * 6 + 1)];

            INSERT INTO public.core_charging_station (
                "Id", "AccountId", "AuthDetailsId", "Booted", "ClientIdentifier", "Model", "Vendor",
                "LatestBootNotificationReason", "Location", "Alias",
                "CreatedAt", "ModifiedAt"
            )
            VALUES (
                station_id,
                n_account_id,
                -- placeholder, replaced after auth_details insert
                '00000000-0000-0000-0000-000000000000',
                random() > 0.4,
                'NS_' || lpad(i::text, 2, '0') || '_' || lpad(j::text, 2, '0'),
                model_pick,
                vendor_pick,
                reason_pick,
                location_pick,
                alias_pick,
                NOW() - (random() * interval '365 days'),
                NOW() - (random() * interval '30 days')
            );

            DECLARE
                noise_auth_details_id uuid := gen_random_uuid();
            BEGIN
                INSERT INTO public.core_auth_details ("Id", "ChargingStationId", "AuthBlob")
                VALUES (
                    noise_auth_details_id,
                    station_id,
                    jsonb_build_object(
                        'authType', (ARRAY['Basic', 'OAuth2', 'mTLS'])[floor(random() * 3 + 1)],
                        'credentials', replace(gen_random_uuid()::text, '-', '') || replace(gen_random_uuid()::text, '-', ''),
                        'provisionedBy', n_account_name
                    )
                );

                UPDATE public.core_charging_station
                SET "AuthDetailsId" = noise_auth_details_id
                WHERE "Id" = station_id;
            END;

            INSERT INTO public.core_tariff ("Id", "ChargingStationId", "UnitName", "UnitPrice")
            VALUES (
                gen_random_uuid(),
                station_id,
                'kWh',
                (random() * 0.7 + 0.15)::numeric(10,4)
            );

            connector_count := floor(random() * (noise_connectors_max - noise_connectors_min + 1) + noise_connectors_min)::int;
            FOR k IN 1..connector_count LOOP
                connector_id := gen_random_uuid();
                status_pick := (ARRAY['Available', 'Available', 'Available', 'Occupied', 'Occupied', 'Reserved', 'Faulted', 'Unavailable'])[floor(random() * 8 + 1)];

                INSERT INTO public.core_evse_connector (
                    "Id", "ChargingStationId", "EvseId", "ConnectorId", "ConnectorStatus", "ModifiedAt"
                )
                VALUES (
                    connector_id, station_id, k, k, status_pick, NOW() - (random() * interval '24 hours')
                );
            END LOOP;

            -- 1-2 auth users per station
            FOR k IN 1..(floor(random() * 2 + 1)::int) LOOP
                auth_user_id := gen_random_uuid();
                INSERT INTO public.core_auth_user (
                    "Id", "ChargingStationId", "DisplayName", "IsActive", "ActiveCredit"
                )
                VALUES (
                    auth_user_id, station_id,
                    n_account_name || ' User ' || j || '-' || k,
                    random() > 0.15,
                    (CASE WHEN random() > 0.3 THEN (random() * 500)::numeric(10,2) ELSE NULL END)
                );

                token_count := floor(random() * (noise_tokens_max - noise_tokens_min + 1) + noise_tokens_min)::int;
                FOR m IN 1..token_count LOOP
                    token_id := gen_random_uuid();
                    idtype_pick := (ARRAY['ISO14443', 'ISO15693', 'KeyCode', 'Local', 'EMAID'])[floor(random() * 5 + 1)];

                    INSERT INTO public.core_id_token (
                        "Id", "ChargingStationId", "AuthorizationUserId",
                        "Token", "IdType", "CreatedDate", "ExpiryDate",
                        "Blocked", "IsUnderTx"
                    )
                    VALUES (
                        token_id,
                        station_id,
                        auth_user_id,
                        'NOISE_TOKEN_' || i || '_' || j || '_' || k || '_' || m,
                        idtype_pick,
                        NOW() - (random() * interval '180 days'),
                        NOW() + (random() * interval '365 days'),
                        random() > 0.92,
                        random() > 0.85
                    );
                END LOOP;
            END LOOP;

            -- transactions
            transaction_count := floor(random() * noise_transactions_per_station_max)::int;
            FOR k IN 1..transaction_count LOOP
                transaction_id := 'NS_TX_' || lpad(i::text, 2, '0') || '_' || lpad(j::text, 2, '0') || '_' || lpad(k::text, 3, '0');

                started_at := NOW() - (random() * interval '90 days');
                ended_at   := started_at + (random() * interval '8 hours' + interval '2 minutes');
                metered_value := (random() * 80 + 0.1)::numeric(10,3);
                unit_price := (random() * 0.7 + 0.15)::numeric(10,4);
                total_cost := (metered_value * unit_price)::numeric(12,4);

                start_reason_pick := (ARRAY['Authorized', 'EVConnected', 'ChargingRateChanged'])[floor(random() * 3 + 1)];
                end_reason_pick   := (ARRAY['EVDisconnected', 'Local', 'PowerLoss', 'StopAuthorized', 'Other', 'DeAuthorized'])[floor(random() * 6 + 1)];

                SELECT "Id" INTO connector_id
                FROM public.core_evse_connector
                WHERE "ChargingStationId" = station_id
                ORDER BY random()
                LIMIT 1;

                SELECT "Id" INTO token_id
                FROM public.core_id_token
                WHERE "ChargingStationId" = station_id
                ORDER BY random()
                LIMIT 1;

                INSERT INTO public.core_transactions_snapshot (
                    "Id", "ChargingStationId", "IdTokenId", "TransactionId",
                    "StartReason", "EndReason", "TotalMeteredValue", "TotalCost",
                    "StartedAt", "EndedAt", "EvseConnectorId"
                )
                VALUES (
                    gen_random_uuid(), station_id, token_id, transaction_id,
                    start_reason_pick, end_reason_pick, metered_value, total_cost,
                    started_at, ended_at, connector_id
                );

                event_count := floor(random() * noise_events_per_transaction_max + 1)::int;
                FOR m IN 1..event_count LOOP
                    event_pick       := (ARRAY['Started', 'Updated', 'Ended', 'MeterValue', 'Status'])[floor(random() * 5 + 1)];
                    trigger_pick     := (ARRAY['MeterValuePeriodic', 'MeterValueClock', 'EVConnected', 'EVDisconnected', 'RemoteStart', 'RemoteStop'])[floor(random() * 6 + 1)];
                    consumption_pick := (ARRAY['Periodic', 'Cumulative'])[floor(random() * 2 + 1)];

                    INSERT INTO public.core_transactions_event (
                        "Id", "ChargingStationId", "IdTokenId", "TransactionId", "SeqNo",
                        "EVSEId", "Timestamp", "Offline", "RegisterValue",
                        "PhaseOneValue", "PhaseTwoValue", "PhaseThreeValue",
                        "ConsumptionType", "UnitName", "TriggerReason", "EventType"
                    )
                    VALUES (
                        gen_random_uuid(),
                        station_id,
                        token_id,
                        transaction_id,
                        m,
                        (SELECT "EvseId" FROM public.core_evse_connector WHERE "Id" = connector_id),
                        started_at + (random() * (ended_at - started_at)),
                        random() > 0.85,
                        (random() * metered_value)::numeric(10,3),
                        (random() * metered_value / 3)::numeric(10,3),
                        (random() * metered_value / 3)::numeric(10,3),
                        (random() * metered_value / 3)::numeric(10,3),
                        consumption_pick,
                        'kWh',
                        trigger_pick,
                        event_pick
                    );
                END LOOP;
            END LOOP;
        END LOOP;
    END LOOP;
END;
$$;

CALL public.seed_medinilla_test_data(
    noise_accounts => 4,
    noise_stations_min => 2,
    noise_stations_max => 5,
    noise_connectors_min => 1,
    noise_connectors_max => 4,
    noise_tokens_min => 1,
    noise_tokens_max => 4,
    noise_transactions_per_station_max => 6,
    noise_events_per_transaction_max => 5
);

-- Quick summary so the operator can eyeball what was inserted.
SELECT 'accounts'         AS entity, COUNT(*)::text AS count FROM public.core_account
UNION ALL SELECT 'charging_stations',       COUNT(*)::text FROM public.core_charging_station
UNION ALL SELECT 'evse_connectors',         COUNT(*)::text FROM public.core_evse_connector
UNION ALL SELECT 'tariffs',                 COUNT(*)::text FROM public.core_tariff
UNION ALL SELECT 'auth_details',            COUNT(*)::text FROM public.core_auth_details
UNION ALL SELECT 'auth_users',              COUNT(*)::text FROM public.core_auth_user
UNION ALL SELECT 'id_tokens',               COUNT(*)::text FROM public.core_id_token
UNION ALL SELECT 'transactions_snapshot',   COUNT(*)::text FROM public.core_transactions_snapshot
UNION ALL SELECT 'transactions_event',      COUNT(*)::text FROM public.core_transactions_event
ORDER BY entity;
