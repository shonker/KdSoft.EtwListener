## Import Styles before production build or development run

Any CSS that is not available as CSS-in-JS in the form of lit-element css`...my styles...` must
be imported/converted to that format. Place your CSS into src/css and then run "npm run import-styles".

## Run in Development mode

Before starting a Visual Studio debug session, run "npm run dev" from a terminal window
in the project directory. This will start the vite dev server, from which index.js is loaded.

## Build for production (ASPNETCORE_ENVIRONMENT = Production)

Before starting a debug session, run "npm run build" from a terminal window in the project directory.
This will populate wwwroot with bundled Javascript and CSS assets.

## Certificates

Connection security and authentication/authorization are both performed using certificates.

### TLS

Specified in appsettings.json in the Kestrel section.

```json
  "Kestrel": {
    "EndpointDefaults": {
      "Protocols": "Http1AndHttp2"
    },
    "Endpoints": {
      "Https": {
        "Url": "https://0.0.0.0:5099",
        WE CAN SPECIFY A CERTIFICATE FILE
        "Certificate": {
          "Path": "D:\\PlayPen\\EtwListener\\localhost.p12",
          "Password": "XXXXXXXXXXXXX"
        }
        OR WE CAN USE THE LOCAL CERTIFICATE STORAGE
        // will select the matching certificate with the latest expiry date
        "Certificate": {
          "Subject": "localhost", // matches any Subject containing that string
          "Store": "My" / Only stores indicated by the StoreName type are accepted
          "Location": "LocalMachine",
          "AllowInvalid": false // must be false for server certificates>
        }
      }
    }
  }
```

### Client/User Authentication

We can use a self-signed root certificate , as client authentication certificates are provided by us. 

- The self-signed root certificate must be installed in the "**Local Computer\Personal**" folder of the local certificate storage.
- The client certificate must be configured to support client authorization.

A useful tool for creating certificates is [XCA](https://www.hohnstaedt.de/xca/).

We specify authorized users in appsettings.json, in the **ClientValidation** section:

```json
  "ClientValidation": {
    "RootCertificateThumbprint": "d87dce532fb17cabd3804e77d7f344ec4e49c80f",
    "AuthorizedCommonNames": [
      "karl@waclawek.net"
    ]
  }
```

### Authentication as Client of Grpc Server

We must have obtained a certificate from the site owning the server. This certificate must be installed in the  "**CurrentUser\Personal**" folder of the local certificate storage.

The common name in the certificate must correspond to one of the authorized common names configured on the Grpc server.

We specify the certificate we use to authorize this web application as the client of the Grpc server in appsettings.json as well, in the **ClientCertificate** section:

```json
  "ClientCertificate": {
    "Location": "CurrentUser",
    "Thumbprint": "558efcc579f8b7be36f02d709cf58ea29020644e"
  }
```
