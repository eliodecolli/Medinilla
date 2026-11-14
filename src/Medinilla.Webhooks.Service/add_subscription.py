#!/usr/bin/env python3
"""Add a webhook subscription to the service configuration.

Generates a signing secret, stores it under Webhooks:Secrets, and appends a
subscription pointing at the requested URL.

Examples:
    python add_subscription.py https://example.test/hooks/medinilla
    python add_subscription.py http://localhost:9000/hook \
        --event-type connector.status \
        --event-type transaction.started
"""

from __future__ import annotations

import argparse
import json
import re
import secrets
from pathlib import Path
from urllib.parse import urlparse


PROJECT_DIR = Path(__file__).resolve().parent
DEFAULT_CONFIG = PROJECT_DIR / "appsettings.json"


def slugify(value: str) -> str:
    slug = re.sub(r"[^a-z0-9]+", "-", value.lower()).strip("-")
    return slug[:40] or "webhook"


def env_suffix(secret_ref: str) -> str:
    return re.sub(r"[^A-Z0-9]", "_", secret_ref.upper())


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Add a webhook subscription and generate its signing secret."
    )
    parser.add_argument("url", help="HTTP(S) URL to receive webhook deliveries")
    parser.add_argument(
        "--config",
        type=Path,
        default=DEFAULT_CONFIG,
        help=f"configuration file to update (default: {DEFAULT_CONFIG})",
    )
    parser.add_argument(
        "--id",
        help="stable subscription ID (default: generated from the URL)",
    )
    parser.add_argument(
        "--secret-ref",
        help="secret reference name (default: same as the subscription ID)",
    )
    parser.add_argument(
        "--event-type",
        action="append",
        dest="event_types",
        metavar="TYPE",
        help="limit deliveries to this EventType; repeat for multiple types",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()

    parsed_url = urlparse(args.url)
    if parsed_url.scheme not in {"http", "https"} or not parsed_url.netloc:
        raise SystemExit(f"Invalid absolute HTTP(S) URL: {args.url}")

    config_path = args.config.expanduser().resolve()
    if not config_path.is_file():
        raise SystemExit(f"Configuration file not found: {config_path}")

    config = json.loads(config_path.read_text(encoding="utf-8"))
    webhooks = config.setdefault("Webhooks", {})
    subscriptions = webhooks.setdefault("Subscriptions", [])
    stored_secrets = webhooks.setdefault("Secrets", {})

    if not isinstance(subscriptions, list):
        raise SystemExit("Webhooks:Subscriptions must be a JSON array.")
    if not isinstance(stored_secrets, dict):
        raise SystemExit("Webhooks:Secrets must be a JSON object.")

    subscription_id = args.id or f"{slugify(parsed_url.netloc + parsed_url.path)}-{secrets.token_hex(3)}"
    secret_ref = args.secret_ref or subscription_id

    if any(subscription.get("Id") == subscription_id for subscription in subscriptions):
        raise SystemExit(f"Subscription ID already exists: {subscription_id}")
    if any(subscription.get("Url") == args.url for subscription in subscriptions):
        raise SystemExit(f"Subscription URL already exists: {args.url}")
    if secret_ref in stored_secrets:
        raise SystemExit(f"Secret reference already exists: {secret_ref}")

    event_types = list(dict.fromkeys(args.event_types or []))
    signing_secret = secrets.token_urlsafe(32)

    subscription = {
        "Id": subscription_id,
        "Url": args.url,
        "Enabled": True,
        "SigningSecretRef": secret_ref,
    }
    if event_types:
        subscription["EventTypes"] = event_types

    subscriptions.append(subscription)
    stored_secrets[secret_ref] = signing_secret

    config_path.write_text(
        json.dumps(config, indent=2) + "\n",
        encoding="utf-8",
    )

    environment_name = f"MEDINILLA_WEBHOOK_SECRET_{env_suffix(secret_ref)}"
    print(f"Added subscription: {subscription_id}")
    print(f"URL: {args.url}")
    print(f"Event types: {', '.join(event_types) if event_types else 'all'}")
    print(f"Secret reference: {secret_ref}")
    print(f"Generated signing secret: {signing_secret}")
    print()
    print(f"Updated: {config_path}")
    print(
        "For production, remove the value from Webhooks:Secrets and provide it "
        f"through the environment instead: {environment_name}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
