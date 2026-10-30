# REvoat v0.5-alpha

The admin panel is rebuilt from the ground up, and the three rules that decide
who can create a subverse are now yours to set from the web UI instead of
`appsettings.json`.

If you run REvoat as a website, the two parts that matter are the new
**Site settings** page and the one-off database step in **Upgrading** below.

## New: a sitewide admin panel

`/admin` used to be a single global-ban form that nothing linked to, and it had
a real hole — you could ban a user or domain but never unban one. It is now a
proper panel.

| Page | What it does | Who can use it |
|---|---|---|
| `/admin` | Sitewide counts: users, submissions, comments, subverses | Global staff |
| `/admin/Users` | Search accounts by name, open one, ban or unban it | Global staff; ban/unban needs a ban role |
| `/admin/Reports` | The reports queue, filterable by type, age and status | Global staff; "Ignore" needs a mod role |
| `/admin/Spam/Ban` | Ban a user or domain | Global staff; ban needs a ban role |
| `/admin/Spam/BannedUsers` | List and remove global user bans | Global staff; remove needs a ban role |
| `/admin/Spam/BannedDomains` | List and remove global domain bans | Global staff; remove needs a ban role |

"Global staff" means any of `GlobalAdmin`, `Admin`, `DelegateAdmin`,
`GlobalBans`, `GlobalJanitor`. Ban and unban need `GlobalAdmin`, `Admin`,
`DelegateAdmin` or `GlobalBans`. Marking a report reviewed needs
`GlobalAdmin`, `Admin` or `DelegateAdmin`, so janitors get a read-only queue.

There is also an **Admin** link in the account menu, visible only to those
roles.

### Two bugs fixed on the way

- **Unbanning silently did nothing.** The remove-ban form submitted the field
  under the wrong name, so the server received nothing, found no ban, and
  quietly redirected back to the list. The ban stayed. If you have a ban you
  could never clear, this release fixes it — try again.
- **Anyone logged in could read every report on the site.** The
  `/v/all/about/reports` page skipped its permission check entirely. It is now
  global-staff only.

## New: subverse-creation rules you can change

Creating a subverse is gated on three checks. All three were hardcoded; all
three are now editable at **`/admin/Settings`**, and each has an on/off switch
as well as a threshold.

| Gate | Default | Setting |
|---|---|---|
| Minimum account age | 30 days | `subverseCreation.minAccountAge.*` |
| Minimum comment (CCP) points | 10 | `subverseCreation.minCommentPoints.*` |
| Minimum submission (SCP) points | 10 | `subverseCreation.minSubmissionPoints.*` |

Untick a gate and it is skipped outright — its number is not consulted, so
there is no "off but still at 30" confusion.

**How to use it**

1. Log in as `GlobalAdmin` or `Admin`.
2. Open `/admin/Settings`.
3. Untick "Require a minimum account age" to let brand-new accounts create
   subverses, or set the day count to whatever you want.
4. Click **Save settings**. It applies immediately.

**Details worth knowing**

- **Nothing is seeded.** A fresh install with no saved values behaves exactly
  as before: gates on, 30 days / 10 / 10.
- Values are stored in the database and **override `appsettings.json` from the
  first save**. The page warns you about this. If you later change
  `MinimumAccountAgeInDaysForSubverseCreation` in config and nothing happens,
  that is why — clear the rows (see below) to fall back to config again.
- Changes apply to the very next subverse creation. **No restart, no cache to
  clear.**
- They survive restarts and `scripts/upgrade.sh`.
- Only `GlobalAdmin` and `Admin` can see or change this page. The sidebar link
  is hidden from the other three roles.
- Thresholds are clamped to 0–3650 days and 0–100000 points, on the way in
  and again on the way out, so a hand-edited row cannot produce a negative or
  unreachable number.

To go back to whatever `appsettings.json` says:

```bash
docker compose exec db psql -U voat -d voat -c \
  "DELETE FROM \"SiteSetting\" WHERE \"Name\" LIKE 'subverseCreation.%';"
```

## Upgrading

```bash
./scripts/upgrade.sh
```

That is the whole thing — it backs up first, rebuilds, and applies the new
schema. See [UPGRADING.md](UPGRADING.md) if you have not upgraded before.

**This release adds one table** (`SiteSetting`), so there is a schema step.
`scripts/upgrade.sh` runs it for you. If you deploy manually, run it once:

```bash
docker compose exec db psql -U voat -d voat -f /voat-sql/setup.sql
```

The schema scripts are additive and idempotent — re-running them never
overwrites content you have changed.

To confirm the table landed:

```bash
docker compose exec db psql -U voat -d voat -c '\d "SiteSetting"'
```

**Do not** upgrade by unpacking this into a new, separate folder. Docker
Compose names its data volumes after the folder you run it from, so a fresh
folder is a brand-new empty install — your data is untouched but invisible,
which looks exactly like a wipe. Unpack over the existing directory, or just
run `scripts/upgrade.sh`. Never run `docker compose down -v`.

## Notes

- The tests compile but cannot currently be *run* against a local test
  database on Windows: port 5433 falls inside a Hyper-V excluded range. This
  is a development-environment problem only and does not affect a running
  site.
- The DMCA contact in the User Agreement still reads `copyright@voat.co`, left
  verbatim deliberately — change it from the page editor if you want a
  different address.

See `CHANGELOG.md` for the full technical record of these changes.
