CREATE OR REPLACE PROCEDURE seed_test_data(
    num_accounts INT DEFAULT 5,
    stations_per_account INT DEFAULT 3,
    connectors_per_station INT DEFAULT 2
)
LANGUAGE plpgsql
AS $$
DECLARE
    account_id uuid;
    station_id uuid;
    auth_user_id uuid;
    connector_id uuid;
    i INT;
    j INT;
    k INT;
BEGIN
    -- Clear existing data
    DELETE FROM public.core_transactions_event;
    DELETE FROM public.core_transactions_snapshot;
    DELETE FROM public.core_id_token;
    DELETE FROM public.core_auth_user;
    DELETE FROM public.core_auth_details;
    DELETE FROM public.core_tariff;
    DELETE FROM public.core_evse_connector;
    DELETE FROM public.core_charging_station;
    DELETE FROM public.core_account;

    -- Seed Accounts
    FOR i IN 1..num_accounts LOOP
        account_id := gen_random_uuid();
        INSERT INTO public.core_account ("Id", "Name")
        VALUES (account_id, 'Account ' || i);

        -- Seed Charging Stations for each Account
        FOR j IN 1..stations_per_account LOOP
            station_id := gen_random_uuid();
            INSERT INTO public.core_charging_station (
                "Id", "ClientIdentifier", "Model", "Vendor", 
                "LatestBootNotificationReason", "CreatedAt", "ModifiedAt",
                "AuthDetailsId", "Alias", "Location", "AccountId"
            )
            VALUES (
                station_id,
                'CS_' || i || '_' || j,
                (ARRAY['Model A', 'Model B', 'Model X'])[floor(random() * 3 + 1)],
                (ARRAY['Vendor 1', 'Vendor 2', 'Vendor 3'])[floor(random() * 3 + 1)],
                (ARRAY['PowerUp', 'Reboot', 'LocalReset'])[floor(random() * 3 + 1)],
                NOW() - (random() * interval '365 days'),
                NOW() - (random() * interval '30 days'),
                gen_random_uuid(),
                'Station ' || i || '-' || j,
                (ARRAY['Location A', 'Location B', 'Location C'])[floor(random() * 3 + 1)],
                account_id
            );

            -- Seed Auth Details
            INSERT INTO public.core_auth_details ("Id", "ChargingStationId", "AuthBlob")
            VALUES (
                gen_random_uuid(),
                station_id,
                '{"authType": "Basic", "credentials": "dummy"}'::jsonb
            );

            -- Seed Tariffs
            INSERT INTO public.core_tariff ("Id", "ChargingStationId", "UnitName", "UnitPrice")
            VALUES (
                gen_random_uuid(),
                station_id,
                'kWh',
                (random() * 0.5 + 0.2)::numeric(10,2)
            );

            -- Seed EVSE Connectors
            FOR k IN 1..connectors_per_station LOOP
                connector_id := gen_random_uuid();
                INSERT INTO public.core_evse_connector (
                    "Id", "ChargingStationId", "EvseId", "ConnectorId",
                    "ConnectorStatus", "ModifiedAt"
                )
                VALUES (
                    connector_id,
                    station_id,
                    k,
                    k,
                    (ARRAY['Available', 'Occupied', 'Reserved'])[floor(random() * 3 + 1)],
                    NOW() - (random() * interval '7 days')
                );
            END LOOP;

            -- Seed Auth Users
            auth_user_id := gen_random_uuid();
            INSERT INTO public.core_auth_user (
                "Id", "ChargingStationId", "DisplayName", 
                "IsActive", "ActiveCredit"
            )
            VALUES (
                auth_user_id,
                station_id,
                'User ' || i || '-' || j,
                random() > 0.1,
                (random() * 1000)::numeric(10,2)
            );

            -- Seed ID Tokens
            INSERT INTO public.core_id_token (
                "Id", "ChargingStationId", "AuthorizationUserId",
                "Token", "IdType", "CreatedDate", "ExpiryDate",
                "Blocked", "IsUnderTx"
            )
            VALUES (
                gen_random_uuid(),
                station_id,
                auth_user_id,
                'TOKEN_' || i || '_' || j,
                (ARRAY['ISO14443', 'ISO15693', 'KeyCode'])[floor(random() * 3 + 1)],
                NOW() - (random() * interval '180 days'),
                NOW() + (random() * interval '365 days'),
                random() > 0.9,
                random() > 0.8
            );

            -- Seed Transaction Snapshots and Events (if needed)
            IF random() > 0.5 THEN
                INSERT INTO public.core_transactions_snapshot (
                    "Id", "TransactionId", "ChargingStationId",
                    "TotalMeteredValue", "Unit", "TotalCost",
                    "StartedAt", "EndedAt", "TokenId",
                    "EvseConnectorId", "EndReason", "StartReason"
                )
                VALUES (
                    gen_random_uuid(),
                    'TX_' || i || '_' || j,
                    station_id,
                    (random() * 50)::numeric(10,2),
                    'kWh',
                    (random() * 100)::numeric(10,2),
                    NOW() - (random() * interval '30 days'),
                    NOW() - (random() * interval '1 day'),
                    'TOKEN_' || i || '_' || j,
                    connector_id,
                    (ARRAY['EVDisconnected', 'Local', 'PowerLoss'])[floor(random() * 3 + 1)],
                    (ARRAY['Authorized', 'EVConnected'])[floor(random() * 2 + 1)]
                );
            END IF;
        END LOOP;
    END LOOP;
END;
$$;

CALL seed_test_data(5, 3, 2);