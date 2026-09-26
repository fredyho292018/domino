# Future manual rollback

Record previous full SHA, new full SHA, Compose backup and health before any deployment.
Keep the previous verified image available. If new image health fails, restore the previous image SHA and
approved Compose configuration, then recreate only API with `up -d --no-deps --wait api` and recheck health,
ports and protected endpoints. Compare Redis container ID/start time; do not restart or prune Redis.

Keep credentials and cursor unchanged; restoring an image does not restore Firestore data/schema.
Review schema compatibility before rolling back a future release. No automatic data rollback, global Docker
prune, host reboot, zero-downtime promise or automatic rollback is implemented in INFRA-1.
