set -euo pipefail

readonly ENV_VAL=$1
readonly APP_NAME=$2
readonly OPENSHIFT_NAMESPACE=$3
readonly D365_API_KEY_SCHEME=$4
readonly D365_API_AUTH_SETTINGS=$5
readonly D365_DEFAULT_SENDER_ID=$6
readonly D365_DEFAULT_CONTACT_ID=$7
readonly D365_RECIPIENTS=$8
readonly D365_BC_REGISTRY_API=$9
readonly D365_BCCAS_API_URL=${10}

SERVER_FRONTEND="https://ofm-frontend-$ENV_VAL-$OPENSHIFT_NAMESPACE.apps.silver.devops.gov.bc.ca"
if [ "$ENV_VAL" = "prod" ]; then
  SERVER_FRONTEND="" # TODO: Set this to our production domain
fi
readonly SERVER_FRONTEND

echo
echo Generating D365 Configuration

D365_LOG_LEVEL=$(cat << JSON
{
  "LogLevel": {
    "Default": "Error",
    "OFM.Portal.ProviderProfile": "Error",
    "OFM.D365.Process": "Error",
    "OFM.D365.Batch": "Error",
    "Microsoft.AspNetCore": "Error"
  },
  "Console": {
    "FormatterName": "simple",
    "FormatterOptions": {
      "SingleLine": true,
      "IncludeScopes": true,
      "TimestampFormat": "yyyy-MM-ddTHH:mm:ss",
      "UseUtcTimestamp": false,
      "JsonWriterOptions": {
        "Indented": true
      }
    }
  },
  "Debug": {
    "LogLevel": {
      "Default": "Critical"
    }
  }
}
JSON
)
if [ "$ENV_VAL" = "dev" ]; then
D365_LOG_LEVEL=$(cat << JSON
{
  "LogLevel": {
    "Default": "Warning",
    "OFM.Portal.ProviderProfile": "Error",
    "OFM.D365.Process": "Warning",
    "OFM.D365.Batch": "Warning",
    "Microsoft.AspNetCore": "Warning"
  },
  "Console": {
    "FormatterName": "simple",
    "FormatterOptions": {
      "SingleLine": true,
      "IncludeScopes": true,
      "TimestampFormat": "yyyy-MM-ddTHH:mm:ss",
      "UseUtcTimestamp": false,
      "JsonWriterOptions": {
        "Indented": true
      }
    }
  },
  "Debug": {
    "LogLevel": {
      "Default": "Critical"
    }
  }
}
JSON
)
fi
readonly D365_LOG_LEVEL

D365_EMAIL_SAFE_LIST_ENABLE=true
if [ "$ENV_VAL" = "prod" ]; then
  D365_EMAIL_SAFE_LIST_ENABLE=false
fi
readonly D365_EMAIL_SAFE_LIST_ENABLE

D365_CONFIGURATION=$(jq << JSON
{
  "Logging": $D365_LOG_LEVEL,
  "AllowedHosts": "*",
  "AppSettings": {
    "PageSize": 50,
    "MaxPageSize": 150,
    "RetryEnabled": true,
    "MaxRetries": 5,
    "AutoRetryDelay": "00:00:08",
    "MinsToCache": 60
  },
  "AuthenticationSettings": {
    "Schemes": {
      "ApiKeyScheme": $(cat "$D365_API_KEY_SCHEME")
    }
  },
  "D365AuthSettings": $(cat "$D365_API_AUTH_SETTINGS"),
  "DocumentSettings": {
    "MaxFileSize": 3999999,
    "AcceptedFommat": [
      "jpg",
      "jpeg",
      "pdf",
      "png",
      "doc",
      "docx",
      "heic",
      "xls",
      "xlsx"
    ],
    "FileSizeErrorMessage": "The file size exceeds the limit allowed.",
    "FileFormatErrorMessage": "The file format is not supported."
  },
  "NotificationSettings": {
    "UnreadEmailOptions": {
      "FirstReminderInDays": 14,
      "SecondReminderInDays": 22,
      "ThirdReminderInDays": 29,
      "TimeOffsetInDays": 0
    },
    "RenewalReminderOptions": {
      "FirstReminderInDays": 60,
      "SecondReminderInDays": 30,
      "ThirdReminderInDays": 18
    },
    "DefaultSenderId": "$D365_DEFAULT_SENDER_ID",
    "EmailTemplates": [
      {
        "TemplateNumber": 201,
        "Description": "Nightly email reminder template"
      },
      {
        "TemplateNumber": 210,
        "Description": "Funding Agreement is ready to sign template"
      },
      {
        "TemplateNumber": 215,
        "Description": "Funding Agreement is ready to sign template"
      },
      {
        "TemplateNumber": 220,
        "Description": "Supplementary Email Reminder template ID"
      },
      {
        "TemplateNumber": 400,
        "Description": "Not Good standing Template"
      },
      {
        "TemplateNumber": 235,
        "Description": "Direct Deposit Enrollment Template"
      }
    ],
    "CommunicationTypes": {
      "Information": 1,
      "ActionRequired": 2,
      "DebtLetter": 3,
      "Reminder": 4,
      "FundingAgreement": 5
    },
    "EmailSafeList": {
      "Enable": $D365_EMAIL_SAFE_LIST_ENABLE,
      "DefaultContactId": "$D365_DEFAULT_CONTACT_ID",
      "Recipients": $(cat "$D365_RECIPIENTS")
    },
    "fundingUrl": "$SERVER_FRONTEND/funding",
    "fundingTabUrl": "$SERVER_FRONTEND/funding/overview"
  },
  "ProcessSettings": {
    "MaxRequestInactiveDays": 21,
    "ClosingReason": "No action"
  },
  "Features": {
    "Environment": {
      "Enable": true,
      "Message": "Welcome our environment."
    },
    "Search": {
      "Enable": false,
      "Message": "Welcome to the BC Childcare Search services."
    },
    "Batch": {
      "Enable": true,
      "Message": "Welcome to the BC Childcare Batch Operation services."
    },
    "DocumentUpload": {
      "Enable": true,
      "Message": "Welcome to the BC Childcare Document Upload services."
    }
  },
  "ExternalServices": {
    "BCRegistryApi": $(cat "$D365_BC_REGISTRY_API"),
    "BCCASApi": {
      "Enable": true,
      "Url": "$D365_BCCAS_API_URL",
      "KeyName": "",
      "KeyValue": "",
      "MinsToCache": 5,
      "transactionCount": 5,
      "clientCode": "62",
      "cGIBatchNumber": "623540001",
      "oracleBatchNumber": "001",
      "batchType": "AP",
      "delimiter": "\u001d",
      "transactionType": "BH",
      "trailertransactionType": "BT",
      "messageVersionNumber": "0001",
      "feederNumber": "3540",
      "InvoiceHeader": {
        "headertransactionType": "IH",
        "invoiceType": "ST",
        "remittanceCode": "00",
        "CAD": "CAD",
        "termsName": "30 days",
        "payflag": "Y"
      },
      "InvoiceLines": {
        "linetransactionType": "IL",
        "lineCode": "D",
        "distributionACK": "0622265006500650822007400000000000",
        "unitPrice": "000000000000.00",
        "quantity": "0000000.00",
        "committmentLine": "0000"
      }
    }
  }
}
JSON
)
readonly D365_CONFIGURATION
echo "$D365_CONFIGURATION" > /tmp/appsettings.json

echo
echo Creating D365 config map "$APP_NAME-d365api-$ENV_VAL-config-map"
oc create -n "$OPENSHIFT_NAMESPACE" configmap \
  "$APP_NAME-d365api-$ENV_VAL-config-map" \
  --from-file="appsettings.json=/tmp/appsettings.json" \
  --dry-run -o yaml | oc apply -f -

echo
echo Setting environment variables for "$APP_NAME-d365api-$ENV_VAL" application
oc -n "$OPENSHIFT_NAMESPACE" set env \
  --from="configmap/$APP_NAME-d365api-$ENV_VAL-config-map" \
  "dc/$APP_NAME-d365api-$ENV_VAL"
