# D365 Config Map Updater

This readme serves as documentation for what secrets are used for deployment and
what their expected types are. Note that the output of the update script is a
valid JSON file, so these types should be in JSON.

| Key                     | Type            |
| ----------------------- | --------------- |
| D365_API_KEY_SCHEME     | `Object`        |
| D365_API_AUTH_SETTINGS  | `Object`        |
| D365_DEFAULT_SENDER_ID  | `String`        |
| D365_DEFAULT_CONTACT_ID | `String`        |
| D365_RECIPIENTS         | `Array<String>` |
| D365_BC_REGISTRY_API    | `Object`        |
| D365_BCCAS_API_URL      | `String`        |
| D365_CGI_BATCH_NUMBER   | `String`        |

Each of these keys are environment specific, so make sure you update each
environment where applicable.
