# Administrator and email setup

The administrator account is configured outside the database. Set both
`SUPERADMIN_EMAIL` and `SUPERADMIN_PASSWORD` in the deployment environment.
The password must contain at least 16 characters. The configured administrator
signs in through the usual login form and receives the `SuperAdmin` API role.
If both are empty, the rest of the site still starts but the admin account and
panel are disabled; setting only one is a configuration error.

Configure Mailjet API email delivery in the same environment:

- `EMAIL_MAILJET_API_KEY` and `EMAIL_MAILJET_API_SECRET` from your Mailjet
  account
- `EMAIL_FROM_ADDRESS`, a sender address validated in Mailjet, and optionally
  `EMAIL_FROM_NAME`
- `EMAIL_FRONTEND_URL`, the public frontend origin used in confirmation links

Create API credentials in Mailjet and validate the Gmail address you want to
use as the sender. Configure both API credentials as environment secrets; do
not commit them. Mailjet uses HTTPS, so it does not require a custom domain or
SMTP access from the host.

New accounts remain pending until the user opens the confirmation link and
chooses a password. The pending record contains no password hash. The link
expires after 24 hours. An already registered user cannot log in until their
email is confirmed. Coupon expiry emails are checked every 15 minutes;
only redemptions created after this feature is enabled are eligible for a
notification.

The administrator dashboard lists registered users, pending email
confirmations, courses, coupon/access redemptions, and lesson progress.
Deleting a user permanently deletes their related
redemptions and progress. Configure `SUPERADMIN_*` and `EMAIL_*` as secrets in
the host or in an untracked local `.env` file; do not commit credential values.
For local Compose, copy `.env.example` to `.env` and replace the blank
superadmin credentials and Mailjet API credentials first.
